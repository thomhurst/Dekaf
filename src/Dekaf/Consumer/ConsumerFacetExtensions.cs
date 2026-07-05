using Dekaf.Consumer;

namespace Dekaf;

/// <summary>
/// Extension methods that forward advanced consumer operations to focused facets.
/// </summary>
public static class ConsumerFacetExtensions
{
    /// <summary>
    /// Manually assigns partitions.
    /// </summary>
    public static void Assign<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        params TopicPartition[] partitions)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        consumer.Partitions.Assign(partitions);
    }

    /// <summary>
    /// Unassigns all partitions.
    /// </summary>
    public static void Unassign<TKey, TValue>(this IKafkaConsumer<TKey, TValue> consumer)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        consumer.Partitions.Unassign();
    }

    /// <summary>
    /// Incrementally adds partitions to the current assignment.
    /// </summary>
    public static void IncrementalAssign<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        IEnumerable<TopicPartitionOffset> partitions)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        consumer.Partitions.IncrementalAssign(partitions);
    }

    /// <summary>
    /// Incrementally removes partitions from the current assignment.
    /// </summary>
    public static void IncrementalUnassign<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        IEnumerable<TopicPartition> partitions)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        consumer.Partitions.IncrementalUnassign(partitions);
    }

    /// <summary>
    /// Gets the committed offset for a partition.
    /// </summary>
    public static ValueTask<long?> GetCommittedOffsetAsync<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        TopicPartition partition,
        CancellationToken cancellationToken = default)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        return consumer.Positions.GetCommittedOffsetAsync(partition, cancellationToken);
    }

    /// <summary>
    /// Gets the current position for a partition.
    /// </summary>
    public static long? GetPosition<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        TopicPartition partition)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        return consumer.Positions.GetPosition(partition);
    }

    /// <summary>
    /// Seeks to a specific offset.
    /// </summary>
    public static void Seek<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        TopicPartitionOffset offset)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        consumer.Positions.Seek(offset);
    }

    /// <summary>
    /// Seeks to the beginning of partitions.
    /// </summary>
    public static void SeekToBeginning<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        params TopicPartition[] partitions)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        consumer.Positions.SeekToBeginning(partitions);
    }

    /// <summary>
    /// Seeks to the end of partitions.
    /// </summary>
    public static void SeekToEnd<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        params TopicPartition[] partitions)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        consumer.Positions.SeekToEnd(partitions);
    }

    /// <summary>
    /// Pauses consumption from partitions.
    /// </summary>
    public static void Pause<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        params TopicPartition[] partitions)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        consumer.Partitions.Pause(partitions);
    }

    /// <summary>
    /// Resumes consumption from partitions.
    /// </summary>
    public static void Resume<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        params TopicPartition[] partitions)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        consumer.Partitions.Resume(partitions);
    }

    /// <summary>
    /// Looks up offsets for partitions by timestamp.
    /// </summary>
    public static ValueTask<IReadOnlyDictionary<TopicPartition, long>> GetOffsetsForTimesAsync<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        IEnumerable<TopicPartitionTimestamp> timestampsToSearch,
        CancellationToken cancellationToken = default)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        return consumer.Offsets.GetOffsetsForTimesAsync(timestampsToSearch, cancellationToken);
    }

    /// <summary>
    /// Gets cached watermark offsets for a partition.
    /// </summary>
    public static WatermarkOffsets? GetWatermarkOffsets<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        TopicPartition topicPartition)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        return consumer.Offsets.GetWatermarkOffsets(topicPartition);
    }

    /// <summary>
    /// Queries current watermark offsets for a partition.
    /// </summary>
    public static ValueTask<WatermarkOffsets> QueryWatermarkOffsetsAsync<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        TopicPartition topicPartition,
        CancellationToken cancellationToken = default)
    {
        CompatibilityThrowHelpers.ThrowIfNull(consumer);
        return consumer.Offsets.QueryWatermarkOffsetsAsync(topicPartition, cancellationToken);
    }
}
