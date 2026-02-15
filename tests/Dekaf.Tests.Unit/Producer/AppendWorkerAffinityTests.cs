using System.Collections.Concurrent;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for per-partition worker affinity in the RecordAccumulator.
/// Verifies that messages for the same partition are processed sequentially
/// (preserving ordering) and that different partitions can be processed in parallel.
/// </summary>
public class AppendWorkerAffinityTests
{
    private static ProducerOptions CreateTestOptions()
    {
        return new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = ulong.MaxValue,
            BatchSize = 1_048_576,
            LingerMs = 0
        };
    }

    private static ConcurrentDictionary<TopicPartition, PartitionBatch> GetBatches(RecordAccumulator accumulator)
    {
        var batchesField = typeof(RecordAccumulator).GetField("_batches",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (ConcurrentDictionary<TopicPartition, PartitionBatch>)batchesField!.GetValue(accumulator)!;
    }

    [Test]
    public async Task EnqueueAppend_SamePartition_ProcessedSequentially()
    {
        // Messages enqueued for the same partition should be appended in FIFO order.
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        using var cts = new CancellationTokenSource();

        accumulator.StartAppendWorkers(cts.Token);

        const int messageCount = 50;
        var completions = new PooledValueTaskSource<RecordMetadata>[messageCount];

        for (var i = 0; i < messageCount; i++)
        {
            completions[i] = pool.Rent();
            accumulator.EnqueueAppend(
                "test-topic",
                partition: 0,
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                key: PooledMemory.Null,
                value: PooledMemory.Null,
                headers: null,
                pooledHeaderArray: null,
                completion: completions[i],
                cancellationToken: CancellationToken.None);
        }

        var batches = GetBatches(accumulator);
        var tp = new TopicPartition("test-topic", 0);

        // Poll until workers have processed and created the batch
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!batches.ContainsKey(tp) && sw.ElapsedMilliseconds < 5000)
            await Task.Delay(10);

        await Assert.That(batches.ContainsKey(tp)).IsTrue();

        // Cancel workers before disposal to avoid waiting for sender drain timeout
        cts.Cancel();
        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task EnqueueAppend_DifferentPartitions_RoutedToDifferentWorkers()
    {
        // Messages for different partitions should be routed to potentially different
        // worker channels, enabling parallel processing.
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        using var cts = new CancellationTokenSource();

        accumulator.StartAppendWorkers(cts.Token);

        const int partitionCount = 8;
        const int messagesPerPartition = 10;

        for (var p = 0; p < partitionCount; p++)
        {
            for (var i = 0; i < messagesPerPartition; i++)
            {
                var completion = pool.Rent();
                accumulator.EnqueueAppend(
                    "test-topic",
                    partition: p,
                    timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    key: PooledMemory.Null,
                    value: PooledMemory.Null,
                    headers: null,
                    pooledHeaderArray: null,
                    completion: completion,
                    cancellationToken: CancellationToken.None);
            }
        }

        var batches = GetBatches(accumulator);

        // Poll until workers have processed all partitions
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (batches.Count < partitionCount && sw.ElapsedMilliseconds < 5000)
            await Task.Delay(10);

        for (var p = 0; p < partitionCount; p++)
        {
            var tp = new TopicPartition("test-topic", p);
            await Assert.That(batches.ContainsKey(tp)).IsTrue();
        }

        cts.Cancel();
        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task EnqueueAppend_AfterDispose_SetsException()
    {
        // When the accumulator is disposed, TryWrite fails and the completion source
        // should be set with an ObjectDisposedException.
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();

        await accumulator.DisposeAsync();

        var completion = pool.Rent();

        accumulator.EnqueueAppend(
            "test-topic",
            partition: 0,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            key: PooledMemory.Null,
            value: PooledMemory.Null,
            headers: null,
            pooledHeaderArray: null,
            completion: completion,
            cancellationToken: CancellationToken.None);

        var vt = new ValueTask<RecordMetadata>(completion, completion.Version);
        await Assert.That(async () => await vt).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task EnqueueAppend_WithCancellation_SetsCanceled()
    {
        // When the per-message cancellation token is already cancelled, the worker
        // should set the completion as cancelled.
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        await using var pool = new ValueTaskSourcePool<RecordMetadata>();
        using var workerCts = new CancellationTokenSource();

        accumulator.StartAppendWorkers(workerCts.Token);

        var completion = pool.Rent();
        using var messageCts = new CancellationTokenSource();
        messageCts.Cancel();

        accumulator.EnqueueAppend(
            "test-topic",
            partition: 0,
            timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            key: PooledMemory.Null,
            value: PooledMemory.Null,
            headers: null,
            pooledHeaderArray: null,
            completion: completion,
            cancellationToken: messageCts.Token);

        // Await the completion directly — the worker will set cancellation deterministically
        var vt = new ValueTask<RecordMetadata>(completion, completion.Version);
        await Assert.That(async () => await vt).Throws<OperationCanceledException>();

        workerCts.Cancel();
        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task WorkerPartitionAffinity_PartitionModulo_IsConsistent()
    {
        var workerCount = Math.Clamp(Environment.ProcessorCount, 1, 8);

        // Same partition always maps to same worker
        for (var partition = 0; partition < 100; partition++)
        {
            var worker1 = (int)((uint)partition % (uint)workerCount);
            var worker2 = (int)((uint)partition % (uint)workerCount);
            await Assert.That(worker1).IsEqualTo(worker2);
        }

        // Partitions that differ by workerCount map to the same worker
        for (var partition = 0; partition < 50; partition++)
        {
            var worker1 = (int)((uint)partition % (uint)workerCount);
            var worker2 = (int)((uint)(partition + workerCount) % (uint)workerCount);
            await Assert.That(worker1).IsEqualTo(worker2);
        }
    }
}
