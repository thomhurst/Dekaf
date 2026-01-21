using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Consumer;

/// <summary>
/// Interface for Kafka consumer.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public interface IKafkaConsumer<TKey, TValue> : IAsyncDisposable
{
    /// <summary>
    /// Gets the current subscription.
    /// </summary>
    IReadOnlySet<string> Subscription { get; }

    /// <summary>
    /// Gets the current assignment.
    /// </summary>
    IReadOnlySet<TopicPartition> Assignment { get; }

    /// <summary>
    /// Gets the member ID if part of a consumer group.
    /// </summary>
    string? MemberId { get; }

    /// <summary>
    /// Subscribes to topics.
    /// </summary>
    IKafkaConsumer<TKey, TValue> Subscribe(params string[] topics);

    /// <summary>
    /// Subscribes to topics matching a pattern.
    /// </summary>
    IKafkaConsumer<TKey, TValue> Subscribe(Func<string, bool> topicFilter);

    /// <summary>
    /// Unsubscribes from all topics.
    /// </summary>
    IKafkaConsumer<TKey, TValue> Unsubscribe();

    /// <summary>
    /// Manually assigns partitions.
    /// </summary>
    IKafkaConsumer<TKey, TValue> Assign(params TopicPartition[] partitions);

    /// <summary>
    /// Unassigns all partitions.
    /// </summary>
    IKafkaConsumer<TKey, TValue> Unassign();

    /// <summary>
    /// Consumes messages as an async enumerable.
    /// </summary>
    IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes a single message.
    /// </summary>
    ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits consumed offsets.
    /// </summary>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits specific offsets.
    /// </summary>
    ValueTask CommitAsync(IEnumerable<TopicPartitionOffset> offsets, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the committed offset for a partition.
    /// </summary>
    ValueTask<long?> GetCommittedOffsetAsync(TopicPartition partition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current position (next offset to consume) for a partition.
    /// </summary>
    long? GetPosition(TopicPartition partition);

    /// <summary>
    /// Seeks to a specific offset.
    /// </summary>
    IKafkaConsumer<TKey, TValue> Seek(TopicPartitionOffset offset);

    /// <summary>
    /// Seeks to the beginning of partitions.
    /// </summary>
    IKafkaConsumer<TKey, TValue> SeekToBeginning(params TopicPartition[] partitions);

    /// <summary>
    /// Seeks to the end of partitions.
    /// </summary>
    IKafkaConsumer<TKey, TValue> SeekToEnd(params TopicPartition[] partitions);

    /// <summary>
    /// Pauses consumption from partitions.
    /// </summary>
    IKafkaConsumer<TKey, TValue> Pause(params TopicPartition[] partitions);

    /// <summary>
    /// Resumes consumption from partitions.
    /// </summary>
    IKafkaConsumer<TKey, TValue> Resume(params TopicPartition[] partitions);

    /// <summary>
    /// Gets the paused partitions.
    /// </summary>
    IReadOnlySet<TopicPartition> Paused { get; }

    /// <summary>
    /// Wakes up the consumer if it's blocked on a fetch.
    /// </summary>
    void Wakeup();
}

/// <summary>
/// Result of consuming a message.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed record ConsumeResult<TKey, TValue>
{
    /// <summary>
    /// The topic.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// The partition.
    /// </summary>
    public required int Partition { get; init; }

    /// <summary>
    /// The offset.
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// The deserialized key.
    /// </summary>
    public TKey? Key { get; init; }

    /// <summary>
    /// The deserialized value.
    /// </summary>
    public required TValue Value { get; init; }

    /// <summary>
    /// The message headers.
    /// </summary>
    public Headers? Headers { get; init; }

    /// <summary>
    /// The message timestamp.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// The timestamp type.
    /// </summary>
    public TimestampType TimestampType { get; init; }

    /// <summary>
    /// The leader epoch.
    /// </summary>
    public int? LeaderEpoch { get; init; }

    /// <summary>
    /// Gets the topic-partition-offset.
    /// </summary>
    public TopicPartitionOffset TopicPartitionOffset => new(Topic, Partition, Offset);
}

/// <summary>
/// Type of timestamp on a message.
/// </summary>
public enum TimestampType
{
    /// <summary>
    /// Timestamp not available.
    /// </summary>
    NotAvailable = -1,

    /// <summary>
    /// Timestamp set by the producer.
    /// </summary>
    CreateTime = 0,

    /// <summary>
    /// Timestamp set by the broker when appending to the log.
    /// </summary>
    LogAppendTime = 1
}
