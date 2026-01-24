namespace Dekaf.Statistics;

/// <summary>
/// Statistics for a partition from the producer's perspective.
/// </summary>
public sealed class PartitionStatistics
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public required int Partition { get; init; }

    /// <summary>
    /// Number of messages produced to this partition.
    /// </summary>
    public long MessagesProduced { get; init; }

    /// <summary>
    /// Number of messages delivered to this partition.
    /// </summary>
    public long MessagesDelivered { get; init; }

    /// <summary>
    /// Number of messages that failed to deliver to this partition.
    /// </summary>
    public long MessagesFailed { get; init; }

    /// <summary>
    /// Number of bytes produced to this partition.
    /// </summary>
    public long BytesProduced { get; init; }

    /// <summary>
    /// Current number of messages queued for this partition.
    /// </summary>
    public int QueuedMessages { get; init; }

    /// <summary>
    /// The leader broker ID for this partition.
    /// </summary>
    public int? LeaderNodeId { get; init; }
}

/// <summary>
/// Statistics for a partition from the consumer's perspective.
/// </summary>
public sealed class ConsumerPartitionStatistics
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public required int Partition { get; init; }

    /// <summary>
    /// Number of messages consumed from this partition.
    /// </summary>
    public long MessagesConsumed { get; init; }

    /// <summary>
    /// Number of bytes consumed from this partition.
    /// </summary>
    public long BytesConsumed { get; init; }

    /// <summary>
    /// The current consumer position (next offset to consume).
    /// </summary>
    public long? Position { get; init; }

    /// <summary>
    /// The committed offset for this partition.
    /// </summary>
    public long? CommittedOffset { get; init; }

    /// <summary>
    /// The high watermark (latest available offset) for this partition.
    /// </summary>
    public long? HighWatermark { get; init; }

    /// <summary>
    /// The consumer lag (high watermark - committed offset).
    /// Returns null if high watermark or committed offset is not available.
    /// </summary>
    public long? Lag => HighWatermark.HasValue && CommittedOffset.HasValue
        ? HighWatermark.Value - CommittedOffset.Value
        : null;

    /// <summary>
    /// The leader broker ID for this partition.
    /// </summary>
    public int? LeaderNodeId { get; init; }

    /// <summary>
    /// Whether this partition is currently paused.
    /// </summary>
    public bool IsPaused { get; init; }
}
