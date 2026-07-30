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
/// <see cref="RecordBatch"/> pool capacity (2048) or every reseed allocates the excess.
/// </remarks>
internal static class BufferedConsumerHarness
{
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

    public static Queue<PendingFetchData> GetPendingFetches<TKey, TValue>(
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
