using System.Threading.Tasks.Sources;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Runs typed records through the key-ordered coordinator, handler completion and
/// automatic commit frontier. One operation includes an entire partition lifetime;
/// MemoryDiagnoser includes its amortized construction and shutdown costs. A separate
/// exact allocation probe checks the steady dispatch window after initial storage use.
/// </summary>
[MemoryDiagnoser]
public class KeyOrderedDispatchBenchmarks
{
    private const int RecordCount = 262144;
    private const int Capacity = 128;
    private readonly long[] _lastByKey = new long[RecordCount];
    private readonly HandlerGate _handlerGate = new();
    private Func<IReadOnlyList<ConsumeResult<int, int>>, CancellationToken, ValueTask> _handler = null!;
    private PartitionLane<int, int> _lane = null!;
    private int _written;
    private int _handled;
    private int _largestBatch;
    private int _thread;
    private long _allocationStart;
    private long _allocationEnd;

    [Params(KeyPattern.Repeated, KeyPattern.Distinct, KeyPattern.PendingPairs)]
    public KeyPattern Pattern { get; set; }

    [Params(1, 16)]
    public int BatchSize { get; set; }

    public long SteadyAllocatedBytes { get; private set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _handler = HandleBatch;
        await DispatchLifetime().ConfigureAwait(false);
    }

    [Benchmark]
    public async ValueTask<int> DispatchLifetime()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        _handlerGate.Reset();
        using var registration = deadline.Token.UnsafeRegister(
            static state => ((HandlerGate)state!).Cancel(), _handlerGate);
        _written = 0;
        _handled = 0;
        _largestBatch = 0;
        _thread = Environment.CurrentManagedThreadId;
        Array.Fill(_lastByKey, -1);
        _lane = new(new TopicPartition("key-dispatch", 0), Capacity,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        for (var index = 0; index < Capacity; index++)
            EnqueueNext();
        var dispatcher = new KeyOrderedPartitionDispatcher<int, int>(
            new PartitionProcessorContext<int, int>(_lane), BatchSize, 2, Capacity,
            _handler, automaticCompletion: true);

        await dispatcher.RunAsync(deadline.Token).ConfigureAwait(false);
        SteadyAllocatedBytes = _allocationEnd - _allocationStart;
        if (_handled != RecordCount
            || _lane.GetCommitOffset() != new TopicPartitionOffset("key-dispatch", 0, RecordCount, 7))
            throw new InvalidOperationException("Dispatch lost records or committed incomplete progress.");
        if (SteadyAllocatedBytes != 0)
            throw new InvalidOperationException($"Steady dispatch allocated {SteadyAllocatedBytes} bytes.");
        if (Pattern == KeyPattern.PendingPairs && _largestBatch != BatchSize)
            throw new InvalidOperationException("Paired handlers did not exercise the configured batch size.");
        return _handled;
    }

    private ValueTask HandleBatch(IReadOnlyList<ConsumeResult<int, int>> records, CancellationToken token)
    {
        var key = records[0].Key;
        _largestBatch = Math.Max(_largestBatch, records.Count);
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record.Key != key || record.Offset <= _lastByKey[key]
                || Environment.CurrentManagedThreadId != _thread)
                throw new InvalidOperationException("Dispatch mixed keys, reordered records or changed the probe thread.");
            _lastByKey[key] = record.Offset;
            _handled++;
            if (_written < RecordCount)
                EnqueueNext();
            if (_handled == Capacity * 2)
                _allocationStart = GC.GetAllocatedBytesForCurrentThread();
            if (_handled == RecordCount)
            {
                _allocationEnd = GC.GetAllocatedBytesForCurrentThread();
                var stopped = _lane.StopAsync(PartitionStopPolicy.Drain, Timeout.InfiniteTimeSpan, CancellationToken.None);
                if (!stopped.IsCompletedSuccessfully || stopped.GetAwaiter().GetResult() is not null)
                    throw new InvalidOperationException("Closing the fixture input unexpectedly suspended or failed.");
            }
        }

        if (Pattern != KeyPattern.PendingPairs)
            return default;
        if (key == 0)
            return _handlerGate.Wait(records.Count);
        _handlerGate.Complete(records.Count);
        return default;
    }

    private void EnqueueNext()
    {
        var key = Pattern switch
        {
            KeyPattern.Distinct => _written,
            // Hold the first key while more of its records queue, then let the
            // second key release it. Alternating single records never builds batches.
            KeyPattern.PendingPairs => (_written / (BatchSize * 2)) & 1,
            _ => 0
        };
        var record = new ConsumeResult<int, int>("key-dispatch", 0, _written++, key, 0,
            null, 0, TimestampType.CreateTime, 7);
        if (!_lane.TryEnqueue(record))
            throw new InvalidOperationException("Replenished input exceeded the bounded queue.");
    }

    [GlobalCleanup]
    public void ReportSteadyAllocation() => Console.WriteLine(
        $"STEADY_ALLOCATION pattern={Pattern} batchSize={BatchSize} largestBatch={_largestBatch} records={RecordCount - Capacity * 2} bytes={SteadyAllocatedBytes}");

    public enum KeyPattern
    {
        Repeated,
        Distinct,
        PendingPairs
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
            return new(this, _source.Version);
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
