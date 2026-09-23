using BenchmarkDotNet.Attributes;
using Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures the BufferMemory backpressure round trip in steady state: an append queues as a
/// pooled <see cref="PendingAppend"/>, a memory release drains it (scan, reservation, claim,
/// append), and the completed operation returns to its pool. Covers the generation-checked
/// queue entry and claim (#3389). BufferMemory stays full between operations; sealed batches
/// are recycled once per invocation, keeping the per-message path allocation-free.
/// </summary>
[MemoryDiagnoser]
public class PendingAppendDrainCycleBenchmarks
{
    private const string Topic = "pending-append-drain-cycle";
    private const int OperationsPerInvoke = 256;
    private const long BufferMemory = 1024 * 1024;

    private static readonly TopicPartition Partition = new(Topic, 0);

    private RecordAccumulator _accumulator = null!;
    private int _recordSize;

    [GlobalSetup]
    public void Setup()
    {
        _accumulator = new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 16_384,
            BufferMemory = (ulong)BufferMemory,
            LingerMs = 60_000,
            // No broker acknowledges these batches; keep the startup admission window out of the loop.
            DeliveryLatencyTargetMs = 0
        }, resolveLeaderId: static (_, _) => 1);
        _recordSize = PartitionBatch.EstimateRecordSize(0, 0, null, 0);

        // Hold a bulk scope for the fixture's lifetime so the app-limited bypass does not seal a
        // batch per drained append; batches seal on BatchSize as under sustained backpressure.
        _ = _accumulator.EnterBulkProduceScope();

        if (!_accumulator.TryReserveMemoryForTest((int)BufferMemory))
            throw new InvalidOperationException("Could not fill BufferMemory.");

        // Warm the PendingAppend pool, partition deque and batch pools.
        for (var i = 0; i < 8; i++)
            DrainCycle();
    }

    [GlobalCleanup]
    public void Cleanup() => _accumulator.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void DrainCycle()
    {
        for (var i = 0; i < OperationsPerInvoke; i++)
        {
            // Sealing a batch refunds its estimate-versus-encoded difference; take that slack
            // back so every append meets a full buffer.
            var free = BufferMemory - _accumulator.BufferedBytes;
            if (free > 0 && !_accumulator.TryReserveMemoryForTest((int)free))
                throw new InvalidOperationException("Could not refill BufferMemory.");

            var append = _accumulator.AppendAsync(
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
            if (append.IsCompleted)
                throw new InvalidOperationException("Append bypassed BufferMemory backpressure.");

            // Frees exactly one record; the drain reserves it again for the queued append.
            _accumulator.ReleaseMemory(_recordSize);
            if (!append.IsCompleted || !append.GetAwaiter().GetResult())
                throw new InvalidOperationException("Drain did not serve the queued append.");
        }

        // Recycle sealed batches without releasing their memory: it becomes the fill that
        // keeps the next invocation on the backpressure path.
        while (_accumulator.TryDrainBatch(Partition, out var batch))
            _accumulator.ReturnReadyBatch(batch);
    }
}
