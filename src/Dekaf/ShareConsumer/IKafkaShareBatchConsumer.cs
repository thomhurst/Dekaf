namespace Dekaf.ShareConsumer;

/// <summary>Optional share-consumer capability for borrowed batch delivery.</summary>
/// <remarks>
/// Choose either PollAsync or PollBatchesAsync for the lifetime of a consumer instance.
/// Batch polling preserves share-group acknowledgement, retry, and renewal semantics while
/// storing record views and first-use acknowledgement state in pooled batch storage.
/// All consumer and batch operations require external synchronization when used concurrently.
/// </remarks>
public interface IKafkaShareBatchConsumer<TKey, TValue> : IKafkaShareConsumer<TKey, TValue>
{
    /// <summary>Polls for borrowed batches of acquired records.</summary>
    /// <remarks>
    /// Enumerate each batch synchronously. Advancing or disposing this iterator ends the current
    /// batch lease. Only enumerated records are eligible for implicit acknowledgement. Explicit
    /// dispositions must be set on the batch before its lease ends; CommitAsync can run afterwards.
    /// MaxPollRecords limits each fetch request. Batch-optimized acquisition can exceed that
    /// budget; this stream delivers all acquired records from every broker before fetching again.
    /// </remarks>
    IAsyncEnumerable<ShareConsumeBatch<TKey, TValue>> PollBatchesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Accesses optional share-consumer batch capabilities.</summary>
public static class ShareConsumerBatchExtensions
{
    /// <summary>Polls for borrowed batches when the consumer supports batch delivery.</summary>
    /// <exception cref="NotSupportedException">The consumer does not implement the optional batch capability.</exception>
    public static IAsyncEnumerable<ShareConsumeBatch<TKey, TValue>> PollBatchesAsync<TKey, TValue>(
        this IKafkaShareConsumer<TKey, TValue> consumer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        if (consumer is not IKafkaShareBatchConsumer<TKey, TValue> batchConsumer)
            throw new NotSupportedException("This share consumer does not support borrowed batch delivery.");
        return batchConsumer.PollBatchesAsync(cancellationToken);
    }
}
