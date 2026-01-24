namespace Dekaf.Statistics;

/// <summary>
/// Statistics snapshot for a Kafka producer.
/// </summary>
public sealed class ProducerStatistics
{
    /// <summary>
    /// Timestamp when these statistics were collected.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Total number of messages produced (sent to accumulator).
    /// </summary>
    public long MessagesProduced { get; init; }

    /// <summary>
    /// Total number of messages delivered (acknowledged by broker).
    /// </summary>
    public long MessagesDelivered { get; init; }

    /// <summary>
    /// Total number of messages that failed to deliver.
    /// </summary>
    public long MessagesFailed { get; init; }

    /// <summary>
    /// Total bytes produced.
    /// </summary>
    public long BytesProduced { get; init; }

    /// <summary>
    /// Current number of messages queued in the accumulator.
    /// </summary>
    public int QueuedMessages { get; init; }

    /// <summary>
    /// Number of batches currently in the ready queue waiting to be sent.
    /// Backpressure is applied when this reaches the channel capacity.
    /// </summary>
    public int BatchesInQueue { get; init; }

    /// <summary>
    /// Total number of produce requests sent.
    /// </summary>
    public long RequestsSent { get; init; }

    /// <summary>
    /// Total number of produce responses received.
    /// </summary>
    public long ResponsesReceived { get; init; }

    /// <summary>
    /// Number of retries performed.
    /// </summary>
    public long Retries { get; init; }

    /// <summary>
    /// Average produce request latency in milliseconds.
    /// </summary>
    public double AvgRequestLatencyMs { get; init; }

    /// <summary>
    /// Statistics per broker.
    /// </summary>
    public IReadOnlyDictionary<int, BrokerStatistics> Brokers { get; init; } =
        new Dictionary<int, BrokerStatistics>();

    /// <summary>
    /// Statistics per topic.
    /// </summary>
    public IReadOnlyDictionary<string, TopicStatistics> Topics { get; init; } =
        new Dictionary<string, TopicStatistics>();
}
