using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the pooled pending-append enqueue path under BufferMemory backpressure.
/// Accumulator setup, disposal, and failure observation run outside the measured operation.
/// </summary>
[MemoryDiagnoser(displayGenColumns: false)]
[MedianColumn]
[MaxColumn]
[SimpleJob(
    RunStrategy.Monitoring,
    launchCount: 1,
    warmupCount: 5,
    iterationCount: 10,
    invocationCount: 1)]
public class PendingAppendEnqueueBenchmarks
{
    private const string Topic = "pending-append-benchmark";
    private RecordAccumulator _accumulator = null!;
    private ValueTask<bool> _warmupAppend;
    private ValueTask<bool> _pendingAppend;

    [IterationSetup]
    public void Setup()
    {
        _accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 4096,
            BufferMemory = 1,
            LingerMs = 0
        });

        _warmupAppend = Enqueue();
        if (_warmupAppend.IsCompleted)
            throw new InvalidOperationException("Warmup append bypassed BufferMemory backpressure.");
    }

    [IterationCleanup]
    public void Cleanup()
    {
        _accumulator.DisposeAsync().AsTask().GetAwaiter().GetResult();

        ObserveDisposed(_warmupAppend);
        ObserveDisposed(_pendingAppend);
    }

    [Benchmark]
    public void EnqueuePendingAppend()
    {
        _pendingAppend = Enqueue();

        if (_pendingAppend.IsCompleted)
            throw new InvalidOperationException("Append bypassed BufferMemory backpressure.");
    }

    private ValueTask<bool> Enqueue() =>
        _accumulator.AppendAsync(
            Topic,
            0,
            0,
            PooledMemory.Null,
            PooledMemory.Null,
            null,
            0,
            null,
            null,
            CancellationToken.None);

    private static void ObserveDisposed(ValueTask<bool> pending)
    {
        try
        {
            pending.GetAwaiter().GetResult();
        }
        catch (ObjectDisposedException)
        {
            // Expected: disposal drains and fails each pending append.
        }
    }
}
