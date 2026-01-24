namespace Dekaf.Statistics;

/// <summary>
/// Statistics for a topic from the producer's perspective.
/// </summary>
public sealed class TopicStatistics
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Number of messages produced to this topic.
    /// </summary>
    public long MessagesProduced { get; init; }

    /// <summary>
    /// Number of messages delivered to this topic (acknowledged by broker).
    /// </summary>
    public long MessagesDelivered { get; init; }

    /// <summary>
    /// Number of messages that failed to deliver to this topic.
    /// </summary>
    public long MessagesFailed { get; init; }

    /// <summary>
    /// Number of bytes produced to this topic.
    /// </summary>
    public long BytesProduced { get; init; }

    /// <summary>
    /// Statistics per partition.
    /// </summary>
    public IReadOnlyDictionary<int, PartitionStatistics> Partitions { get; init; } =
        new Dictionary<int, PartitionStatistics>();
}

/// <summary>
/// Statistics for a topic from the consumer's perspective.
/// </summary>
public sealed class ConsumerTopicStatistics
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Number of messages consumed from this topic.
    /// </summary>
    public long MessagesConsumed { get; init; }

    /// <summary>
    /// Number of bytes consumed from this topic.
    /// </summary>
    public long BytesConsumed { get; init; }

    /// <summary>
    /// Statistics per partition.
    /// </summary>
    public IReadOnlyDictionary<int, ConsumerPartitionStatistics> Partitions { get; init; } =
        new Dictionary<int, ConsumerPartitionStatistics>();
}
