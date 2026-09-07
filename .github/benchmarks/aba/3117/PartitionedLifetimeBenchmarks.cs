using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks.Sources;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks;

// A bounded partition cannot restart after StopAsync. Each measured invocation owns
// one lifetime; the handler replenishes its fixed queue to expose the steady path.
[MemoryDiagnoser]
public class PartitionedLifetimeBenchmarks
{
    private const int RecordCount = 262144;
    private const int Capacity = 128;
    private readonly byte[] _key = new byte[sizeof(int)];
    private readonly long[] _lastByKey = new long[RecordCount];
    private readonly HandlerGate _handlerGate = new();
    private PartitionProcessor<int, int> _processor = null!;
    private PartitionLane<int, int> _lane = null!;
    private int _written;
    private int _handled;
    private int _thread;
    private long _allocationStart;
    private long _allocationEnd;
    private long _steadyAllocation;
    private readonly bool _profiling = Environment.GetEnvironmentVariable("DEKAF_DISPATCH_PROFILE") == "1";
    private Process? _process;
    private long[]? _enqueuedAt;
    private long[]? _latencies;
    private long _cpuTicks;
    private long _elapsedTicks;
    private long _profileRecords;

    [ParamsAllValues]
    public Scenario Mode { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        if (_profiling)
        {
            _process = Process.GetCurrentProcess();
            _enqueuedAt = new long[RecordCount];
            _latencies = new long[RecordCount];
        }
        var batches = Mode is Scenario.PartitionBatches or Scenario.KeyBatchesRepeated
            or Scenario.KeyBatchesDistinct or Scenario.KeyBatchesPaired;
        var options = new PartitionedProcessingOptions
        {
            Ordering = Mode is Scenario.PartitionRecords or Scenario.PartitionBatches
                ? PartitionedProcessingOrder.Partition : PartitionedProcessingOrder.Key,
            MaxBufferedRecordsPerPartition = Capacity,
            MaxConcurrentHandlersPerPartition = 2,
            MaxHandlerBatchSize = 16
        };
        var factory = typeof(PartitionedConsumerExtensions).GetMethod(
            batches ? "CreateBatchProcessor" : "CreateRecordProcessor", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(int), typeof(int));
        Delegate handler = batches
            ? (PartitionBatchProcessor<int, int>)HandleBatch
            : (PartitionRecordProcessor<int, int>)HandleRecord;
        _processor = (PartitionProcessor<int, int>)factory.Invoke(null, [handler, options])!;
        await ProcessLifetime();
        _cpuTicks = 0;
        _elapsedTicks = 0;
        _profileRecords = 0;
    }

    [Benchmark(OperationsPerInvoke = RecordCount)]
    public async ValueTask<long> ProcessLifetime()
    {
        var cpuBefore = _profiling ? _process!.TotalProcessorTime.Ticks : 0;
        var elapsedBefore = _profiling ? Stopwatch.GetTimestamp() : 0;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        _handlerGate.Reset();
        using var registration = deadline.Token.Register(_handlerGate.Cancel);
        _written = 0;
        _handled = 0;
        _thread = Environment.CurrentManagedThreadId;
        Array.Fill(_lastByKey, -1);
        _lane = new PartitionLane<int, int>(new TopicPartition("lifetime", 0), Capacity,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        for (var index = 0; index < Capacity; index++)
            EnqueueNext();

        await _processor(new PartitionProcessorContext<int, int>(_lane), deadline.Token);
        _steadyAllocation = _allocationEnd - _allocationStart;
        // Every handler checks the probe's thread. Teardown may resume elsewhere,
        // after both allocation snapshots have already been taken on that thread.
        var checkpoint = _lane.GetCommitOffset();
        if (_handled != RecordCount || checkpoint is not { Offset: RecordCount, LeaderEpoch: 7 })
            throw new InvalidOperationException($"Handled {_handled}; checkpoint {checkpoint}; expected {RecordCount}/7.");
        if (_profiling)
        {
            _elapsedTicks += Stopwatch.GetTimestamp() - elapsedBefore;
            _cpuTicks += _process!.TotalProcessorTime.Ticks - cpuBefore;
            _profileRecords += RecordCount;
        }
        return _handled;
    }

    private ValueTask HandleRecord(PartitionRecordProcessorContext<int, int> context,
        ConsumeResult<int, int> record, CancellationToken token)
    {
        Observe(record);
        if (Mode == Scenario.KeyRecordsStalledFirst)
        {
            if (record.Offset == 0)
                return _handlerGate.Wait(1);
            if (_handled == RecordCount)
                _handlerGate.Complete(1);
            return default;
        }
        return CompletePair(record.Key, 1);
    }

    private ValueTask HandleBatch(PartitionBatchProcessorContext<int, int> context,
        IReadOnlyList<ConsumeResult<int, int>> records, CancellationToken token)
    {
        var key = records[0].Key;
        for (var index = 0; index < records.Count; index++)
            Observe(records[index]);
        return CompletePair(key, records.Count);
    }

    private ValueTask CompletePair(int key, int count)
    {
        if (Mode is not (Scenario.KeyRecordsPaired or Scenario.KeyBatchesPaired))
            return default;
        if (key == 0)
            return _handlerGate.Wait(count);
        _handlerGate.Complete(count);
        return default;
    }

    private void Observe(ConsumeResult<int, int> record)
    {
        if (_profiling)
            _latencies![record.Offset] = Stopwatch.GetTimestamp() - _enqueuedAt![record.Offset];
        if (Environment.CurrentManagedThreadId != _thread || record.Offset <= _lastByKey[record.Key])
            throw new InvalidOperationException("Handler changed threads or reordered an equal key.");
        _lastByKey[record.Key] = record.Offset;
        _handled++;
        if (_written < RecordCount)
            EnqueueNext();
        if (_handled == Capacity * 2)
            _allocationStart = GC.GetAllocatedBytesForCurrentThread();
        if (_handled == RecordCount)
        {
            _allocationEnd = GC.GetAllocatedBytesForCurrentThread();
            var stopped = _lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan);
            if (!stopped.IsCompletedSuccessfully)
                throw new InvalidOperationException("Closing an unstarted lane unexpectedly suspended.");
            stopped.GetAwaiter().GetResult();
        }
    }

    private void EnqueueNext()
    {
        var key = Mode switch
        {
            Scenario.KeyRecordsDistinct or Scenario.KeyBatchesDistinct => _written,
            Scenario.KeyRecordsPaired or Scenario.KeyBatchesPaired => _written & 1,
            Scenario.KeyRecordsStalledFirst => _written == 0 ? 0 : 1,
            _ => 0
        };
        BinaryPrimitives.WriteInt32BigEndian(_key, key);
        if (_profiling)
            _enqueuedAt![_written] = Stopwatch.GetTimestamp();
        var record = new ConsumeResult<int, int>("lifetime", 0, _written++, _key, false,
            default, true, null, 0, TimestampType.CreateTime, 7, Serializers.Int32, null);
        if (!_lane.TryEnqueue(record))
            throw new InvalidOperationException("Replenished input exceeded the bounded queue.");
    }

    [GlobalCleanup]
    public void ReportAllocationProbe()
    {
        Console.WriteLine($"STEADY_ALLOCATION mode={Mode} records={RecordCount - Capacity * 2} bytes={_steadyAllocation}");
        if (!_profiling)
            return;
        var sampleDirectory = Environment.GetEnvironmentVariable("DEKAF_DISPATCH_SAMPLE_DIRECTORY")!;
        Directory.CreateDirectory(sampleDirectory);
        var sampleFile = Path.Combine(sampleDirectory,
            $"{Environment.GetEnvironmentVariable("DEKAF_DISPATCH_JOB")}-{Mode}-ticks.bin");
        using (var output = File.Create(sampleFile))
            output.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(_latencies.AsSpan()));
        Array.Sort(_latencies!);
        var nsPerTick = 1_000_000_000d / Stopwatch.Frequency;
        Console.WriteLine("DISPATCH_METRICS " + System.Text.Json.JsonSerializer.Serialize(new
        {
            Mode = Mode.ToString(),
            CpuAndThroughputRecords = _profileRecords,
            LatencyRecords = RecordCount,
            StopwatchFrequency = Stopwatch.Frequency,
            RawLatencyFile = sampleFile,
            CpuNsPerMessage = _cpuTicks * 100d / _profileRecords,
            MessagesPerSecond = _profileRecords / (_elapsedTicks / (double)Stopwatch.Frequency),
            P50Ns = _latencies![RecordCount / 2 - 1] * nsPerTick,
            P99Ns = _latencies[(int)Math.Ceiling(RecordCount * 0.99) - 1] * nsPerTick,
            MaxNs = _latencies[RecordCount - 1] * nsPerTick,
            SteadyAllocatedBytes = _steadyAllocation
        }));
        _process!.Dispose();
    }

    public enum Scenario
    {
        PartitionRecords,
        PartitionBatches,
        KeyRecordsRepeated,
        KeyRecordsDistinct,
        KeyBatchesRepeated,
        KeyBatchesDistinct,
        KeyRecordsPaired,
        KeyBatchesPaired,
        KeyRecordsStalledFirst
    }

    private sealed class HandlerGate : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<bool> _source;
        private int _waiting;
        private int _credits;
        private int _needed;

        internal void Reset()
        {
            _credits = 0;
            Volatile.Write(ref _waiting, 0);
        }

        internal void Cancel()
        {
            if (Interlocked.CompareExchange(ref _waiting, 0, 1) == 1)
                _source.SetException(new TimeoutException("Paired handler fixture did not make progress."));
        }

        internal ValueTask Wait(int count)
        {
            if (_credits >= count)
            {
                _credits -= count;
                return default;
            }
            _source.Reset();
            _needed = count;
            Volatile.Write(ref _waiting, 1);
            return new ValueTask(this, _source.Version);
        }

        internal void Complete(int count)
        {
            _credits += count;
            if (Volatile.Read(ref _waiting) == 0 || _credits < _needed)
                return;
            _credits -= _needed;
            if (Interlocked.CompareExchange(ref _waiting, 0, 1) == 1)
                _source.SetResult(true);
        }

        public void GetResult(short token) => _source.GetResult(token);
        public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token,
            ValueTaskSourceOnCompletedFlags flags) => _source.OnCompleted(continuation, state, token, flags);
    }
}
