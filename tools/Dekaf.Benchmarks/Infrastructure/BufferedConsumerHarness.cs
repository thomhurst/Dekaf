using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Infrastructure;

/// <summary>
/// Shared reflection-seeding harness for Docker-free consumer benchmarks: initializes a
/// <see cref="KafkaConsumer{TKey, TValue}"/> for the buffered fast path and seeds its
/// pending-fetch queue directly, so benchmarks measure the drain/poll paths without a
/// broker (#2211 lineage).
/// </summary>
/// <remarks>
/// This is a stringly-typed contract against private <c>KafkaConsumer</c> fields
/// (<c>_pendingFetches</c>, <c>_fetchPositions</c>, <c>_initialized</c>,
/// <c>_assignmentEnsureVersion</c>, <c>_lastManualAssignmentEnsureVersion</c>); a rename
/// there fails these helpers at runtime, which is why the contract lives in exactly one
/// place. Callers must keep their seeded batch count at or below the
/// <see cref="RecordBatch"/> pool capacity or every reseed allocates the excess; the
/// pool defaults to 2048 and constructing a consumer ratchets it upward, so staying
/// under the pre-ratchet 2048 is always safe.
/// </remarks>
internal static class BufferedConsumerHarness
{
    public delegate ValueTask PrefetchResponseHandler(int brokerId, List<TopicPartition> partitions,
        int startIndex, int count, int connectionIndex, int epoch, CancellationToken cancellationToken);

    // Setup-only bindings for both follower-error handlers. The returned capability
    // selects the declared correctness expectation, never the measured workload.
    public static bool BindFollowerFetchHandlers<TKey, TValue>(KafkaConsumer<TKey, TValue> consumer,
        out Func<int, List<TopicPartition>, int, CancellationToken, ValueTask<List<PendingFetchData>?>> fetch,
        out PrefetchResponseHandler prefetch,
        out Action<TopicPartition, long, bool> setPosition)
    {
        var consumerType = consumer.GetType();
        MethodInfo RequireMethod(string name) => consumerType.GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{name} method not found.");
        fetch = RequireMethod("FetchFromBrokerAsync")
            .CreateDelegate<Func<int, List<TopicPartition>, int, CancellationToken, ValueTask<List<PendingFetchData>?>>>(consumer);
        prefetch = RequireMethod("PrefetchFromBrokerAsync").CreateDelegate<PrefetchResponseHandler>(consumer);
        setPosition = RequireMethod("SetPosition").CreateDelegate<Action<TopicPartition, long, bool>>(consumer);
        return consumerType.GetMethod("HandleFetchOffsetOutOfRangeAsync",
            BindingFlags.Instance | BindingFlags.NonPublic) is not null;
    }

    /// <summary>
    /// Adapts revisions that omit batch callbacks by binding their existing interceptor
    /// chain for the caller to execute. Native revisions return null. Call only in setup,
    /// then validate per-record replacements and callback counts before measurement.
    /// </summary>
    public static Func<ConsumeResult<TKey, TValue>, ConsumeResult<TKey, TValue>>? BindBaselineBatchInterceptors<TKey, TValue>(
        KafkaConsumer<TKey, TValue> consumer, bool hasInterceptors)
    {
        var consumerType = typeof(KafkaConsumer<TKey, TValue>);
        if (!hasInterceptors || consumerType.GetField("_onBatchConsume", BindingFlags.Instance | BindingFlags.NonPublic) is not null)
            return null;

        var method = consumerType.GetMethod("ApplyOnConsumeInterceptorsSlow", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The consumer interceptor chain was not found.");
        return method.CreateDelegate<Func<ConsumeResult<TKey, TValue>, ConsumeResult<TKey, TValue>>>(consumer);
    }

    /// <summary>
    /// Marks the consumer initialized, assigns the partition, and acknowledges the manual
    /// assignment so the buffered fast path's currency check passes.
    /// </summary>
    public static void InitializeForBufferedFastPath<TKey, TValue>(
        KafkaConsumer<TKey, TValue> consumer,
        string topic,
        int partition)
    {
        SetPrivateField(consumer, "_initialized", true);

        var topicPartition = new TopicPartition(topic, partition);
        consumer.Assign(topicPartition);
        GetFetchPositions(consumer)[topicPartition] = 0;

        var ensureVersion = GetPrivateField(consumer, "_assignmentEnsureVersion");
        SetPrivateField(consumer, "_lastManualAssignmentEnsureVersion", ensureVersion);
    }

    /// <summary>
    /// Drains any leftover pending fetch, then enqueues one <see cref="PendingFetchData"/>
    /// of <paramref name="batchCount"/> pooled batches cycling the given seed record
    /// arrays. Batch disposal only nulls the batch's own record-list reference, never the
    /// array contents, so seed arrays are safely shared across batches and iterations.
    /// </summary>
    public static void ReseedPendingFetches<TKey, TValue>(
        KafkaConsumer<TKey, TValue> consumer,
        string topic,
        int partition,
        Record[][] seedRecordArrays,
        int batchCount,
        int recordsPerBatch)
    {
        DrainPendingFetches(consumer);

        var batches = new RecordBatch[batchCount];
        for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            var batch = RecordBatch.RentFromPool();
            batch.BaseOffset = (long)batchIndex * recordsPerBatch;
            batch.BaseTimestamp = 1_700_000_000_000L;
            batch.MaxTimestamp = 1_700_000_000_000L + recordsPerBatch - 1;
            batch.LastOffsetDelta = recordsPerBatch - 1;
            batch.Attributes = RecordBatchAttributes.None;
            batch.Records = seedRecordArrays[batchIndex % seedRecordArrays.Length];
            batches[batchIndex] = batch;
        }

        // Create attaches this PendingFetchData's owner/generation to every batch, so
        // draining and disposing it returns all rented batches to the pool.
        GetPendingFetches(consumer).Enqueue(PendingFetchData.Create(topic, partition, batches));
    }

    public static void DrainPendingFetches<TKey, TValue>(KafkaConsumer<TKey, TValue> consumer)
    {
        var pendingFetches = GetPendingFetches(consumer);
        while (pendingFetches.Count > 0)
            pendingFetches.Dequeue().Dispose();
    }

    /// <summary>
    /// Installs the immutable state captured by a bounded snapshot so the complete buffered
    /// delivery loop, including its per-record snapshot validation and position publication,
    /// can be measured without broker I/O.
    /// </summary>
    public static void ActivateSnapshotForBufferedFastPath<TKey, TValue>(
        KafkaConsumer<TKey, TValue> consumer,
        string topic,
        int partition,
        long endOffset)
    {
        var topicPartition = new TopicPartition(topic, partition);
        var assignment = (IReadOnlySet<TopicPartition>)GetPrivateField(consumer, "_assignmentSnapshot")!;
        var paused = (IReadOnlySet<TopicPartition>)GetPrivateField(consumer, "_pausedSnapshot")!;
        var snapshot = new SnapshotConsumeState(
            new Dictionary<TopicPartition, long> { [topicPartition] = endOffset },
            assignment,
            paused,
            new Dictionary<TopicPartition, long> { [topicPartition] = 0 });
        SetPrivateField(consumer, "_activeSnapshot", snapshot);
    }

    public static void DeactivateSnapshotForBufferedFastPath<TKey, TValue>(
        KafkaConsumer<TKey, TValue> consumer) =>
        SetPrivateField(consumer, "_activeSnapshot", null);

    private static Queue<PendingFetchData> GetPendingFetches<TKey, TValue>(
        KafkaConsumer<TKey, TValue> consumer)
        => (Queue<PendingFetchData>)GetPrivateField(consumer, "_pendingFetches")!;

    public static ConcurrentDictionary<TopicPartition, long> GetFetchPositions<TKey, TValue>(
        KafkaConsumer<TKey, TValue> consumer)
        => (ConcurrentDictionary<TopicPartition, long>)GetPrivateField(consumer, "_fetchPositions")!;

    public static object? GetPrivateField<TKey, TValue>(
        KafkaConsumer<TKey, TValue> consumer,
        string fieldName)
        => RequireField<TKey, TValue>(fieldName).GetValue(consumer);

    public static void SetPrivateField<TKey, TValue>(
        KafkaConsumer<TKey, TValue> consumer,
        string fieldName,
        object? value)
        => RequireField<TKey, TValue>(fieldName).SetValue(consumer, value);

    private static FieldInfo RequireField<TKey, TValue>(string fieldName)
        => typeof(KafkaConsumer<TKey, TValue>)
               .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{fieldName} field not found.");
}
