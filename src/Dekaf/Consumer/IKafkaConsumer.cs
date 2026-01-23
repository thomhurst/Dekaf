using Dekaf.Producer;
using Dekaf.Protocol.Records;
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
    /// Gets the consumer group metadata for use with transactional producers.
    /// Returns null if not part of a consumer group or if the group has not yet been joined.
    /// </summary>
    ConsumerGroupMetadata? ConsumerGroupMetadata { get; }

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
/// This is a struct to avoid heap allocations in the hot path.
/// Key and Value are deserialized lazily on first access to minimize allocations.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public readonly struct ConsumeResult<TKey, TValue>
{
    // Raw data stored for lazy deserialization (zero-copy from network buffer)
    private readonly ReadOnlyMemory<byte> _keyData;
    private readonly ReadOnlyMemory<byte> _valueData;
    private readonly bool _isKeyNull;
    private readonly bool _isValueNull;

    // Deserializers for lazy access
    private readonly IDeserializer<TKey>? _keyDeserializer;
    private readonly IDeserializer<TValue>? _valueDeserializer;

    /// <summary>
    /// Creates a new ConsumeResult with lazy deserialization.
    /// </summary>
    public ConsumeResult(
        string topic,
        int partition,
        long offset,
        ReadOnlyMemory<byte> keyData,
        bool isKeyNull,
        ReadOnlyMemory<byte> valueData,
        bool isValueNull,
        IReadOnlyList<RecordHeader>? headers,
        DateTimeOffset timestamp,
        TimestampType timestampType,
        int? leaderEpoch,
        IDeserializer<TKey>? keyDeserializer,
        IDeserializer<TValue>? valueDeserializer)
    {
        Topic = topic;
        Partition = partition;
        Offset = offset;
        _keyData = keyData;
        _isKeyNull = isKeyNull;
        _valueData = valueData;
        _isValueNull = isValueNull;
        Headers = headers;
        Timestamp = timestamp;
        TimestampType = timestampType;
        LeaderEpoch = leaderEpoch;
        _keyDeserializer = keyDeserializer;
        _valueDeserializer = valueDeserializer;
    }

    /// <summary>
    /// The topic.
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// The partition.
    /// </summary>
    public int Partition { get; }

    /// <summary>
    /// The offset.
    /// </summary>
    public long Offset { get; }

    /// <summary>
    /// The deserialized key. Deserialized lazily on first access.
    /// Note: Accessing this property multiple times will deserialize each time.
    /// Cache the result if you need to access it multiple times.
    /// </summary>
    public TKey? Key
    {
        get
        {
            if (_isKeyNull)
                return default;

            var context = new SerializationContext
            {
                Topic = Topic,
                Component = SerializationComponent.Key,
                Headers = null
            };
            return _keyDeserializer!.Deserialize(new System.Buffers.ReadOnlySequence<byte>(_keyData), context);
        }
    }

    /// <summary>
    /// The deserialized value. Deserialized lazily on first access.
    /// Note: Accessing this property multiple times will deserialize each time.
    /// Cache the result if you need to access it multiple times.
    /// </summary>
    public TValue Value
    {
        get
        {
            var context = new SerializationContext
            {
                Topic = Topic,
                Component = SerializationComponent.Value,
                Headers = null
            };

            if (_isValueNull)
                return _valueDeserializer!.Deserialize(System.Buffers.ReadOnlySequence<byte>.Empty, context);

            return _valueDeserializer!.Deserialize(new System.Buffers.ReadOnlySequence<byte>(_valueData), context);
        }
    }

    /// <summary>
    /// The message headers. Returns null if no headers.
    /// Uses RecordHeader directly to avoid per-message conversion allocations.
    /// </summary>
    public IReadOnlyList<RecordHeader>? Headers { get; }

    /// <summary>
    /// The message timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// The timestamp type.
    /// </summary>
    public TimestampType TimestampType { get; }

    /// <summary>
    /// The leader epoch.
    /// </summary>
    public int? LeaderEpoch { get; }

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
