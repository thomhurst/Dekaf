namespace Dekaf.Statistics;

/// <summary>
/// Statistics snapshot for a Kafka consumer.
/// </summary>
public sealed class ConsumerStatistics
{
    /// <summary>
    /// Timestamp when these statistics were collected.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Total number of messages consumed.
    /// </summary>
    public long MessagesConsumed { get; init; }

    /// <summary>
    /// Total bytes consumed.
    /// </summary>
    public long BytesConsumed { get; init; }

    /// <summary>
    /// Total number of rebalances that have completed.
    /// </summary>
    public long TotalRebalances { get; init; }

    /// <summary>
    /// Duration of the most recent rebalance in milliseconds.
    /// Returns null if no rebalance has completed yet.
    /// </summary>
    public long? LastRebalanceDurationMs { get; init; }

    /// <summary>
    /// Cumulative time spent rebalancing in milliseconds.
    /// </summary>
    public long TotalRebalanceDurationMs { get; init; }

    /// <summary>
    /// Number of partitions currently assigned.
    /// </summary>
    public int AssignedPartitions { get; init; }

    /// <summary>
    /// Number of partitions currently paused.
    /// </summary>
    public int PausedPartitions { get; init; }

    /// <summary>
    /// Total consumer lag across all partitions (sum of individual partition lags).
    /// Returns null if lag information is not available for any partition.
    /// </summary>
    public long? TotalLag { get; init; }

    /// <summary>
    /// Current number of messages in the prefetch buffer.
    /// </summary>
    public int PrefetchedMessages { get; init; }

    /// <summary>
    /// Current size of the prefetch buffer in bytes.
    /// </summary>
    public long PrefetchedBytes { get; init; }

    /// <summary>
    /// Total number of fetch requests sent.
    /// </summary>
    public long FetchRequestsSent { get; init; }

    /// <summary>
    /// Total number of fetch responses received.
    /// </summary>
    public long FetchResponsesReceived { get; init; }

    /// <summary>
    /// Average fetch request latency in milliseconds.
    /// </summary>
    public double AvgFetchLatencyMs { get; init; }

    /// <summary>
    /// Consumer group ID, if part of a consumer group.
    /// </summary>
    public string? GroupId { get; init; }

    /// <summary>
    /// Consumer member ID, if part of a consumer group.
    /// </summary>
    public string? MemberId { get; init; }

    /// <summary>
    /// Current generation ID, if part of a consumer group.
    /// </summary>
    public int? GenerationId { get; init; }

    /// <summary>
    /// Whether this consumer is the group leader.
    /// </summary>
    public bool? IsLeader { get; init; }

    /// <summary>
    /// Statistics per broker.
    /// </summary>
    public IReadOnlyDictionary<int, BrokerStatistics> Brokers { get; init; } =
        new Dictionary<int, BrokerStatistics>();

    /// <summary>
    /// Statistics per topic.
    /// </summary>
    public IReadOnlyDictionary<string, ConsumerTopicStatistics> Topics { get; init; } =
        new Dictionary<string, ConsumerTopicStatistics>();
}
