namespace Dekaf.Consumer;

/// <summary>
/// Optional capability for finite snapshot consumption and offset-count tail seeks.
/// </summary>
/// <remarks>
/// Dekaf's built-in consumers implement this interface. The separate capability keeps existing
/// <see cref="IKafkaConsumer{TKey,TValue}"/> implementations binary compatible while
/// <see cref="BoundedConsumerExtensions"/> exposes the APIs on every consumer.
/// </remarks>
public interface IBoundedKafkaConsumer<TKey, TValue>
{
    /// <summary>
    /// Consumes records between the current positions and a fixed snapshot of the assigned
    /// partitions' end offsets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The assignment and isolation-aware end offset of each assigned partition are captured once
    /// when enumeration starts. Records appended later are not returned. Ordering across partitions
    /// follows normal fetch order; ordering within each partition remains by offset.
    /// </para>
    /// <para>
    /// Empty partitions complete immediately. Control batches, aborted transactions, compacted
    /// offsets, and retention gaps advance progress without being returned. Paused partitions are
    /// rejected. Changing the assignment or pause state terminates enumeration with an
    /// <see cref="InvalidOperationException"/>; newly added topic partitions are not included.
    /// </para>
    /// <para>
    /// This method is finite but has no implicit timeout. Cancellation stops the operation. With
    /// <see cref="Protocol.Messages.IsolationLevel.ReadCommitted"/>, an open transaction beyond the
    /// captured last stable offset does not delay completion.
    /// </para>
    /// </remarks>
    IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeSnapshotAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeks a partition to the start of its final <paramref name="offsetCount"/> Kafka offsets.
    /// </summary>
    /// <remarks>
    /// The count is explicitly an offset count, not a visible-record count. Compaction, control
    /// batches, aborted transactions, or retention can therefore make fewer records visible. The
    /// resolved offset is <c>max(low watermark, high watermark - offsetCount)</c>; zero seeks to the
    /// captured high watermark. The partition must be assigned before records can be consumed.
    /// </remarks>
    ValueTask<TopicPartitionOffset> SeekToTailAsync(
        TopicPartition partition,
        int offsetCount,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Finite snapshot and tail operations for Kafka consumers.
/// </summary>
public static class BoundedConsumerExtensions
{
    /// <inheritdoc cref="IBoundedKafkaConsumer{TKey,TValue}.ConsumeSnapshotAsync"/>
    public static IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeSnapshotAsync<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        return GetCapability(consumer).ConsumeSnapshotAsync(cancellationToken);
    }

    /// <inheritdoc cref="IBoundedKafkaConsumer{TKey,TValue}.SeekToTailAsync"/>
    public static ValueTask<TopicPartitionOffset> SeekToTailAsync<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        TopicPartition partition,
        int offsetCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        return GetCapability(consumer).SeekToTailAsync(partition, offsetCount, cancellationToken);
    }

    private static IBoundedKafkaConsumer<TKey, TValue> GetCapability<TKey, TValue>(
        IKafkaConsumer<TKey, TValue> consumer) =>
        consumer as IBoundedKafkaConsumer<TKey, TValue>
        ?? throw new NotSupportedException(
            $"Consumer type {consumer.GetType().FullName} does not support bounded consumption.");
}
