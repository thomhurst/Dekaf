#if NETSTANDARD2_0
using StringSet = System.Collections.Generic.IReadOnlyCollection<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using StringSet = System.Collections.Generic.IReadOnlySet<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

namespace Dekaf.Consumer;

/// <summary>
/// Restricted consumer operations available during a rebalance callback.
/// </summary>
/// <remarks>
/// A view is valid only for the duration of the callback that received it. Retaining and
/// using the view after the callback completes throws <see cref="InvalidOperationException"/>.
/// Consumption, lifecycle, subscription, and assignment mutation operations are deliberately
/// absent so callbacks cannot recursively poll or change group membership.
/// </remarks>
public interface IRebalanceConsumer
{
    /// <summary>Gets the current subscription.</summary>
    StringSet Subscription { get; }

    /// <summary>Gets the current server-side topic regex subscription, if any.</summary>
    string? SubscriptionPattern { get; }

    /// <summary>Gets the assignment visible to this callback.</summary>
    TopicPartitionSet Assignment { get; }

    /// <summary>Gets the paused partitions.</summary>
    TopicPartitionSet Paused { get; }

    /// <summary>Gets the current group member ID.</summary>
    string? MemberId { get; }

    /// <summary>Gets current consumer group metadata, if the group has been joined.</summary>
    ConsumerGroupMetadata? ConsumerGroupMetadata { get; }

    /// <summary>Commits all currently stored offsets.</summary>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Commits the specified offsets.</summary>
    ValueTask CommitAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an offset for the automatic commit loop.</summary>
    void StoreOffset(TopicPartitionOffset offset);

    /// <summary>Gets the committed offset for a partition.</summary>
    ValueTask<long?> GetCommittedOffsetAsync(
        TopicPartition partition,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the current position for a partition.</summary>
    long? GetPosition(TopicPartition partition);

    /// <summary>Seeks to a specific offset.</summary>
    void Seek(TopicPartitionOffset offset);

    /// <summary>Seeks partitions to their beginning.</summary>
    void SeekToBeginning(params TopicPartition[] partitions);

    /// <summary>Seeks partitions to their end.</summary>
    void SeekToEnd(params TopicPartition[] partitions);

    /// <summary>Pauses consumption from partitions.</summary>
    void Pause(params TopicPartition[] partitions);

    /// <summary>Resumes consumption from partitions.</summary>
    void Resume(params TopicPartition[] partitions);

    /// <summary>Looks up offsets by timestamp.</summary>
    ValueTask<IReadOnlyDictionary<TopicPartition, long>> GetOffsetsForTimesAsync(
        IEnumerable<TopicPartitionTimestamp> timestampsToSearch,
        CancellationToken cancellationToken = default);

    /// <summary>Gets cached watermark offsets for a partition.</summary>
    WatermarkOffsets? GetWatermarkOffsets(TopicPartition topicPartition);

    /// <summary>Queries current watermark offsets for a partition.</summary>
    ValueTask<WatermarkOffsets> QueryWatermarkOffsetsAsync(
        TopicPartition topicPartition,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Rebalance listener that receives a callback-scoped restricted consumer view.
/// </summary>
/// <remarks>
/// Existing <see cref="IRebalanceListener"/> implementations remain supported through the
/// same builder registration API.
/// </remarks>
public interface IConsumerAwareRebalanceListener
{
    /// <summary>Called when partitions are assigned after a rebalance completes.</summary>
    ValueTask OnPartitionsAssignedAsync(
        IRebalanceConsumer consumer,
        IEnumerable<TopicPartition> partitions,
        CancellationToken cancellationToken);

    /// <summary>Called when partitions are revoked during a cooperative rebalance.</summary>
    ValueTask OnPartitionsRevokedAsync(
        IRebalanceConsumer consumer,
        IEnumerable<TopicPartition> partitions,
        CancellationToken cancellationToken);

    /// <summary>Called when partitions are lost due to involuntary group removal.</summary>
    ValueTask OnPartitionsLostAsync(
        IRebalanceConsumer consumer,
        IEnumerable<TopicPartition> partitions,
        CancellationToken cancellationToken);
}

internal interface IRebalanceConsumerScope : IRebalanceConsumer
{
    void Invalidate();
}
