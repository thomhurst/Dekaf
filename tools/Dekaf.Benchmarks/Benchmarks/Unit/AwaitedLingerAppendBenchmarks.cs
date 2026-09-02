using System.Text;
using BenchmarkDotNet.Attributes;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the awaited-produce append (<see cref="RecordAccumulator.TryAppendFromSpansWithCompletion"/>)
/// with LingerMs &gt; 0 under sustained demand: batches stay open until full, so every append lands in
/// a partition that is already queued for the linger sweep. A background drainer releases sealed
/// batches so appends stay on the synchronous fast path. No broker.
/// </summary>
[MemoryDiagnoser]
public class AwaitedLingerAppendBenchmarks
{
    private const string Topic = "awaited-linger-topic";
    private const int MessageSize = 100;
    private const int PartitionCount = 8;

    // One completion source shared by every record: the accumulator only stores the reference, and
    // the drainer's CompleteSend completes it exactly once (every later TrySetResult is a no-op),
    // so completion-source pooling stays out of the measurement (see ValueTaskSourcePoolBenchmarks
    // for that cost).
    private readonly PooledValueTaskSource<RecordMetadata> _completion = new();
    private RecordAccumulator _accumulator = null!;
    private RecordAccumulator.BulkProduceScope _bulkScope;
    private byte[] _key = null!;
    private byte[] _value = null!;
    private CancellationTokenSource _drainerCts = null!;
    private Task _drainerTask = null!;
    // MemoryDiagnoser's Allocated column is process-wide (drainer and timer threads included);
    // these isolate the append thread itself. Printed at cleanup as an [alloc] line covering
    // every invocation, pilot and warmup included.
    private long _appendThreadAllocatedBytes;
    private long _appendThreadAppends;

    [GlobalSetup]
    public void Setup()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 1_048_576,
            BufferMemory = 256L * 1024 * 1024,
            LingerMs = 1000,
        };

        _accumulator = new RecordAccumulator(options);
        // Concurrent demand: without it the app-limited bypass (#2510) seals every append as a
        // one-record batch, which is the serial-awaiter shape rather than the loaded one.
        _bulkScope = _accumulator.EnterBulkProduceScope();
        _key = Encoding.UTF8.GetBytes("awaited-key-0");
        _value = new byte[MessageSize];

        // Warm the arena, batch, and ready-batch pools by sealing and draining whole batches.
        var msgsPerBatch = options.BatchSize / (MessageSize + 20);
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var p = 0; p < PartitionCount; p++)
            FillAndDrain(p, batchCount: p == 0 ? 4 : 1, msgsPerBatch, ts);

        _drainerCts = new CancellationTokenSource();
        _drainerTask = Task.Run(() => DrainLoop(_drainerCts.Token));
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _drainerCts.Cancel();
        // DrainLoop polls the token instead of throwing on it, so the task completes normally.
        await _drainerTask.ConfigureAwait(false);
        _bulkScope.Dispose();
        await _accumulator.DisposeAsync().ConfigureAwait(false);

        var appends = Volatile.Read(ref _appendThreadAppends);
        var allocated = Volatile.Read(ref _appendThreadAllocatedBytes);
        Console.WriteLine(
            $"[alloc] appends={appends} appendThreadAllocatedBytes={allocated} " +
            $"bytesPerAppend={(appends == 0 ? 0 : (double)allocated / appends):F3}");
    }

    /// <summary>
    /// Awaited appends into one partition whose open batch is already linger-queued.
    /// </summary>
    [Benchmark(OperationsPerInvoke = 100)]
    public void AppendAwaitedSinglePartition()
    {
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < 100; i++)
            Append(partition: 0, ts);
        RecordAppendThreadAllocation(allocatedBefore, 100);
    }

    private void RecordAppendThreadAllocation(long allocatedBefore, int appends)
    {
        _appendThreadAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        _appendThreadAppends += appends;
    }

    /// <summary>
    /// Same as <see cref="AppendAwaitedSinglePartition"/> spread across eight partitions.
    /// </summary>
    [Benchmark(OperationsPerInvoke = 100)]
    public void AppendAwaitedMultiPartition()
    {
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var i = 0; i < 100; i++)
            Append(i % PartitionCount, ts);
        RecordAppendThreadAllocation(allocatedBefore, 100);
    }

    private void Append(int partition, long ts)
    {
        var spinner = new SpinWait();
        while (!_accumulator.TryAppendFromSpansWithCompletion(
                   Topic, partition, ts,
                   _key, keyIsNull: false,
                   _value, valueIsNull: false,
                   headers: null, headerCount: 0,
                   _completion))
        {
            // Buffer full: the drainer fell behind. Production falls back to the async slow path;
            // here wait for the drainer so the measurement stays on the awaited fast path.
            spinner.SpinOnce(sleep1Threshold: -1);
        }
    }

    private void FillAndDrain(int partition, int batchCount, int msgsPerBatch, long ts)
    {
        var totalMessages = msgsPerBatch * batchCount;
        for (var i = 0; i < totalMessages; i++)
        {
            Append(partition, ts);
            if (i % msgsPerBatch == msgsPerBatch - 1)
                DrainAll();
        }

        DrainAll();
    }

    private void DrainAll()
    {
        while (_accumulator.TryDrainBatch(out var batch))
            Retire(batch);
    }

    /// <summary>
    /// Retires a sealed batch the way the sender does after an acknowledgement: complete
    /// delivery first so the pooled batch is not recycled as an orphan (that path allocates
    /// a diagnostic exception per batch), then release BufferMemory and return it to its pool.
    /// </summary>
    private void Retire(ReadyBatch batch)
    {
        batch.CompleteSend(baseOffset: 0, DateTimeOffset.UnixEpoch);
        _accumulator.ReleaseMemory(batch.DataSize);
        _accumulator.ReturnReadyBatch(batch);
    }

    private void DrainLoop(CancellationToken ct)
    {
        var spinner = new SpinWait();
        while (!ct.IsCancellationRequested)
        {
            if (_accumulator.TryDrainBatch(out var batch))
            {
                Retire(batch);
                spinner.Reset();
            }
            else
            {
                spinner.SpinOnce(sleep1Threshold: -1);
            }
        }
    }
}
