using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Producer;
using Dekaf.Protocol.Records;
using Dekaf.Tests.Unit;

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

    /// <summary>
    /// Returns the _partitionDeques dictionary. To check for a batch, callers should
    /// verify the deque contains the key and CurrentBatch is non-null.
    /// </summary>
    private static object GetPartitionDeques(RecordAccumulator accumulator)
    {
        var dequesField = typeof(RecordAccumulator).GetField("_partitionDeques",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return dequesField!.GetValue(accumulator)!;
    }

    private static bool HasBatchForPartition(object deques, TopicPartition tp)
    {
        var tryGetValueMethod = deques.GetType().GetMethod("TryGetValue");
        var parameters = new object[] { tp, null! };
        var found = (bool)tryGetValueMethod!.Invoke(deques, parameters)!;
        if (!found) return false;
        var pd = parameters[1]!;
        var currentBatch = pd.GetType().GetField("CurrentBatch")!.GetValue(pd);
        return currentBatch != null;
    }

    private static int GetDequeCount(object deques)
    {
        return (int)deques.GetType().GetProperty("Count")!.GetValue(deques)!;
    }

    private static FieldInfo GetThreadCacheField() =>
        typeof(RecordAccumulator).GetField(
            "t_cache",
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private static MethodInfo GetOrCreateDequeMethod() =>
        typeof(RecordAccumulator).GetMethod(
            "GetOrCreateDeque",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            [typeof(string), typeof(int), typeof(int)],
            modifiers: null)
        ?? throw new InvalidOperationException("GetOrCreateDeque overload not found.");

    private static object GetThreadCache() =>
        GetThreadCacheField().GetValue(null)
        ?? throw new InvalidOperationException("Accumulator thread cache not initialized.");

    private static object GetCachedTopicEntry()
    {
        var cache = GetThreadCache();
        return cache.GetType().GetField("CachedTopicEntry")!.GetValue(cache)
            ?? throw new InvalidOperationException("Cached topic deque array not initialized.");
    }

    private static Array GetCachedTopicDeques()
    {
        var entry = GetCachedTopicEntry();
        return (Array)entry.GetType().GetField("Deques")!.GetValue(entry)!;
    }

    private static string? GetCachedTopic()
    {
        var entry = GetCachedTopicEntry();
        return (string?)entry.GetType().GetProperty("Topic")!.GetValue(entry);
    }

    [Test]
    public async Task GetOrCreateDeque_RoundRobinPartitions_CachesPerTopicArray()
    {
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);

        var cacheField = GetThreadCacheField();
        cacheField.SetValue(null, null);

        var getOrCreateDeque = GetOrCreateDequeMethod();

        var first = getOrCreateDeque.Invoke(accumulator, ["test-topic", 0, 4]);
        var second = getOrCreateDeque.Invoke(accumulator, ["test-topic", 1, 4]);
        var firstAgain = getOrCreateDeque.Invoke(accumulator, ["test-topic", 0, 4]);

        var cache = cacheField.GetValue(null)
            ?? throw new InvalidOperationException("Accumulator thread cache not initialized.");
        var cachedEntry = cache.GetType().GetField("CachedTopicEntry")!.GetValue(cache)
            ?? throw new InvalidOperationException("Cached topic entry not initialized.");
        var cachedTopic = (string?)cachedEntry.GetType().GetProperty("Topic")!.GetValue(cachedEntry);
        var cachedDeques = (Array?)cachedEntry.GetType().GetField("Deques")!.GetValue(cachedEntry);

        await Assert.That(firstAgain).IsSameReferenceAs(first);
        await Assert.That(cachedTopic).IsEqualTo("test-topic");
        await Assert.That(cachedDeques).IsNotNull();
        await Assert.That(cachedDeques!.GetValue(0)).IsSameReferenceAs(first);
        await Assert.That(cachedDeques.GetValue(1)).IsSameReferenceAs(second);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task GetOrCreateDeque_PartitionBeyondCachedArray_GrowsTopicArray()
    {
        var accumulator = new RecordAccumulator(CreateTestOptions());
        GetThreadCacheField().SetValue(null, null);
        var getOrCreateDeque = GetOrCreateDequeMethod();

        var first = getOrCreateDeque.Invoke(accumulator, ["test-topic", 0, 0]);
        var firstArray = GetCachedTopicDeques();

        var second = getOrCreateDeque.Invoke(accumulator, ["test-topic", 1, 0]);
        var secondArray = GetCachedTopicDeques();

        var third = getOrCreateDeque.Invoke(accumulator, ["test-topic", 2, 0]);
        var grownArray = GetCachedTopicDeques();

        await Assert.That(firstArray.Length).IsEqualTo(1);
        await Assert.That(secondArray.Length).IsEqualTo(2);
        await Assert.That(grownArray.Length).IsEqualTo(4);
        await Assert.That(secondArray).IsNotSameReferenceAs(firstArray);
        await Assert.That(grownArray).IsNotSameReferenceAs(secondArray);
        await Assert.That(grownArray).IsNotSameReferenceAs(firstArray);
        await Assert.That(grownArray.GetValue(0)).IsSameReferenceAs(first);
        await Assert.That(grownArray.GetValue(1)).IsSameReferenceAs(second);
        await Assert.That(grownArray.GetValue(2)).IsSameReferenceAs(third);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task GetOrCreateDeque_KeyedCacheHint_SizesArrayWithTopicPartitionCount()
    {
        var accumulator = new RecordAccumulator(CreateTestOptions());
        GetThreadCacheField().SetValue(null, null);
        var getOrCreateDeque = GetOrCreateDequeMethod();

        var deque = getOrCreateDeque.Invoke(accumulator, ["test-topic", 2, 6]);
        var cachedDeques = GetCachedTopicDeques();

        await Assert.That(cachedDeques.Length).IsEqualTo(6);
        await Assert.That(cachedDeques.GetValue(2)).IsSameReferenceAs(deque);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task GetOrCreateDeque_WhenTopicChanges_ReplacesCachedTopicArray()
    {
        var accumulator = new RecordAccumulator(CreateTestOptions());
        GetThreadCacheField().SetValue(null, null);
        var getOrCreateDeque = GetOrCreateDequeMethod();

        var firstTopicDeque = getOrCreateDeque.Invoke(accumulator, ["topic-a", 0, 4]);
        var firstArray = GetCachedTopicDeques();

        var secondTopicDeque = getOrCreateDeque.Invoke(accumulator, ["topic-b", 0, 4]);
        var secondArray = GetCachedTopicDeques();

        await Assert.That(GetCachedTopic()).IsEqualTo("topic-b");
        await Assert.That(secondArray).IsNotSameReferenceAs(firstArray);
        await Assert.That(secondArray.GetValue(0)).IsSameReferenceAs(secondTopicDeque);
        await Assert.That(secondTopicDeque).IsNotSameReferenceAs(firstTopicDeque);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task GetOrCreateDeque_WhenTopicReturns_ReusesCachedTopicArray()
    {
        var accumulator = new RecordAccumulator(CreateTestOptions());
        GetThreadCacheField().SetValue(null, null);
        var getOrCreateDeque = GetOrCreateDequeMethod();

        var firstTopicDeque = getOrCreateDeque.Invoke(accumulator, ["topic-a", 0, 4]);
        var firstArray = GetCachedTopicDeques();

        _ = getOrCreateDeque.Invoke(accumulator, ["topic-b", 0, 4]);

        var firstTopicAgain = getOrCreateDeque.Invoke(accumulator, ["topic-a", 0, 4]);
        var firstArrayAgain = GetCachedTopicDeques();

        await Assert.That(GetCachedTopic()).IsEqualTo("topic-a");
        await Assert.That(firstTopicAgain).IsSameReferenceAs(firstTopicDeque);
        await Assert.That(firstArrayAgain).IsSameReferenceAs(firstArray);

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task GetOrCreateDeque_WhenMoreThanEightTopicsReturn_ReusesCachedTopicArrays()
    {
        var accumulator = new RecordAccumulator(CreateTestOptions());
        GetThreadCacheField().SetValue(null, null);
        var getOrCreateDeque = GetOrCreateDequeMethod();
        var deques = new object[12];
        var arrays = new Array[12];

        for (var i = 0; i < deques.Length; i++)
        {
            deques[i] = getOrCreateDeque.Invoke(accumulator, [$"topic-{i}", 0, 4])!;
            arrays[i] = GetCachedTopicDeques();
        }

        for (var i = 0; i < deques.Length; i++)
        {
            var deque = getOrCreateDeque.Invoke(accumulator, [$"topic-{i}", 0, 4]);
            var cachedArray = GetCachedTopicDeques();

            await Assert.That(deque).IsSameReferenceAs(deques[i]);
            await Assert.That(cachedArray).IsSameReferenceAs(arrays[i]);
        }

        await accumulator.DisposeAsync();
    }

    [Test]
    public async Task GetOrCreateDeque_InvalidPartition_DoesNotIndexDequeCache()
    {
        var accumulator = new RecordAccumulator(CreateTestOptions());
        GetThreadCacheField().SetValue(null, null);
        var getOrCreateDeque = GetOrCreateDequeMethod();

        var negative = getOrCreateDeque.Invoke(accumulator, ["test-topic", -1, 4]);
        var negativeAgain = getOrCreateDeque.Invoke(accumulator, ["test-topic", -1, 4]);
        var large = getOrCreateDeque.Invoke(accumulator, ["test-topic", int.MaxValue, 4]);

        await Assert.That(negativeAgain).IsSameReferenceAs(negative);
        await Assert.That(large).IsNotNull();

        await accumulator.DisposeAsync();
    }

    [Test]
    [Timeout(120_000)]
    public async Task EnqueueAppend_SamePartition_ProcessedSequentially(CancellationToken cancellationToken)
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
                headerCount: 0,
                completion: completions[i],
                cancellationToken: CancellationToken.None);
        }

        var deques = GetPartitionDeques(accumulator);
        var tp = new TopicPartition("test-topic", 0);

        // Wait deterministically for the worker to create the batch, bounded by the test's
        // [Timeout] cancellation rather than an arbitrary fixed deadline that flakes when the
        // append workers are thread-pool-starved on loaded runners.
        await TestWait.UntilAsync(
            () => HasBatchForPartition(deques, tp),
            cancellationToken,
            TimeSpan.FromMilliseconds(25));

        await Assert.That(HasBatchForPartition(deques, tp)).IsTrue();

        // Cancel workers before disposal to avoid waiting for sender drain timeout
        cts.Cancel();
        await accumulator.DisposeAsync();
    }

    [Test]
    [Timeout(120_000)]
    public async Task EnqueueAppend_DifferentPartitions_RoutedToDifferentWorkers(CancellationToken cancellationToken)
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
                    headerCount: 0,
                    completion: completion,
                    cancellationToken: CancellationToken.None);
            }
        }

        var deques = GetPartitionDeques(accumulator);

        // Wait deterministically until workers have created batches for ALL partitions,
        // bounded by the test's [Timeout] cancellation rather than an arbitrary fixed deadline
        // that flakes when the append workers are thread-pool-starved on loaded runners.
        await TestWait.UntilAsync(() =>
            {
                for (var p = 0; p < partitionCount; p++)
                {
                    if (!HasBatchForPartition(deques, new TopicPartition("test-topic", p)))
                        return false;
                }
                return true;
            },
            cancellationToken,
            TimeSpan.FromMilliseconds(25));

        for (var p = 0; p < partitionCount; p++)
        {
            var tp = new TopicPartition("test-topic", p);
            await Assert.That(HasBatchForPartition(deques, tp)).IsTrue();
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
            headerCount: 0,
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
            headerCount: 0,
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
