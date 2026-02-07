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
    /// Incrementally adds partitions to the current assignment.
    /// Used with cooperative rebalancing (CooperativeSticky assignor).
    /// Unlike <see cref="Assign"/>, does not replace the entire assignment.
    /// </summary>
    /// <param name="partitions">The partitions to add with optional starting offsets.</param>
    /// <returns>This consumer for method chaining.</returns>
    IKafkaConsumer<TKey, TValue> IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions);

    /// <summary>
    /// Incrementally removes partitions from the current assignment.
    /// Used with cooperative rebalancing (CooperativeSticky assignor).
    /// Unlike <see cref="Unassign"/>, only removes the specified partitions.
    /// </summary>
    /// <param name="partitions">The partitions to remove.</param>
    /// <returns>This consumer for method chaining.</returns>
    IKafkaConsumer<TKey, TValue> IncrementalUnassign(IEnumerable<TopicPartition> partitions);

    /// <summary>
    /// Consumes messages as an async enumerable.
    /// </summary>
    IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes a single message.
    /// </summary>
    ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the offsets of all consumed messages.
    /// Use with OffsetCommitMode.Manual to control when offsets are committed.
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

    /// <summary>
    /// Gracefully closes the consumer: commits pending offsets,
    /// leaves the consumer group, and releases resources.
    /// This method is idempotent and safe to call multiple times.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous close operation.</returns>
    ValueTask CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Look up the offsets for the given partitions by timestamp.
    /// The returned offset for each partition is the earliest offset whose timestamp is greater than or equal to the given timestamp.
    /// </summary>
    /// <param name="timestampsToSearch">The partitions and timestamps to search for. Must not be null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A dictionary mapping each topic-partition to the offset of the first message with timestamp greater than or equal to the target timestamp.
    /// If no such message exists, the offset will be -1.
    /// If the input collection is empty, an empty dictionary is returned.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="timestampsToSearch"/> is null.</exception>
    /// <remarks>
    /// This method uses the Kafka ListOffsets API (API Key 2).
    /// Special timestamp values: -1 (latest offset), -2 (earliest offset).
    /// Use <see cref="TopicPartitionTimestamp.Latest"/> and <see cref="TopicPartitionTimestamp.Earliest"/> constants.
    /// </remarks>
    ValueTask<IReadOnlyDictionary<TopicPartition, long>> GetOffsetsForTimesAsync(
        IEnumerable<TopicPartitionTimestamp> timestampsToSearch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cached watermark offsets (low and high) for a partition.
    /// The watermarks are updated from fetch responses as records are consumed.
    /// Returns null if no watermark data is cached for the partition.
    /// </summary>
    /// <param name="topicPartition">The topic partition to get watermarks for.</param>
    /// <returns>The cached watermark offsets, or null if not available.</returns>
    WatermarkOffsets? GetWatermarkOffsets(TopicPartition topicPartition);

    /// <summary>
    /// Queries the cluster for current watermark offsets (low and high) for a partition.
    /// This makes network requests to fetch the latest offsets from the broker.
    /// </summary>
    /// <param name="topicPartition">The topic partition to query watermarks for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current watermark offsets from the cluster.</returns>
    ValueTask<WatermarkOffsets> QueryWatermarkOffsetsAsync(
        TopicPartition topicPartition,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of consuming a message.
/// This is a struct to avoid heap allocations in the hot path.
/// Key and Value are deserialized eagerly during construction to avoid storing deserializer references.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public readonly struct ConsumeResult<TKey, TValue>
{
    // Thread-local reusable SerializationContext to avoid per-deserialization allocations
    // Since SerializationContext contains reference types (Topic, Headers), copying it
    // involves copying those references. Using ThreadStatic avoids repeated struct creation.
    //
    // ThreadStatic initialization: Default struct initialization (all fields = null/default) is safe.
    // The struct is updated via property setters before each use, so initial null values don't matter.
    // Reference type fields (string Topic, Headers? Headers) start as null and are explicitly set
    // before passing to deserializers, avoiding any uninitialized state issues.
    [ThreadStatic]
    private static SerializationContext t_serializationContext;

    /// <summary>
    /// Creates a new ConsumeResult with eager deserialization.
    /// Deserializes key and value immediately to avoid storing deserializer references in the struct.
    /// </summary>
    public ConsumeResult(
        string topic,
        int partition,
        long offset,
        ReadOnlyMemory<byte> keyData,
        bool isKeyNull,
        ReadOnlyMemory<byte> valueData,
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        DateTimeOffset timestamp,
        TimestampType timestampType,
        int? leaderEpoch,
        IDeserializer<TKey>? keyDeserializer,
        IDeserializer<TValue>? valueDeserializer,
        bool isPartitionEof = false)
    {
        Topic = topic;
        Partition = partition;
        Offset = offset;
        Headers = headers;
        Timestamp = timestamp;
        TimestampType = timestampType;
        LeaderEpoch = leaderEpoch;
        IsPartitionEof = isPartitionEof;

        // Eagerly deserialize key and value to avoid storing deserializer references
        // This eliminates 16 bytes per ConsumeResult (two interface references on 64-bit)
        // and prevents potential closure allocations
        if (isPartitionEof || keyDeserializer is null)
        {
            // Partition EOF or test scenario without deserializer
            Key = default;
        }
        else if (isKeyNull)
        {
            // Null keys return default (null for reference types, 0 for value types).
            // Unlike values (where TValue is non-nullable and requires calling the deserializer),
            // TKey? is nullable so we can return default directly without risking exceptions
            // from deserializers that don't handle empty data (e.g., Int32Serde, GuidSerde).
            Key = default;
        }
        else
        {
            // Reuse thread-local context by updating fields (zero-allocation)
            t_serializationContext.Topic = topic;
            t_serializationContext.Component = SerializationComponent.Key;
            t_serializationContext.Headers = null;
            t_serializationContext.IsNull = false;
            Key = keyDeserializer.Deserialize(new System.Buffers.ReadOnlySequence<byte>(keyData), t_serializationContext);
        }

        if (isPartitionEof || valueDeserializer is null)
        {
            // Partition EOF or test scenario without deserializer
            Value = default!;
        }
        else
        {
            // Reuse thread-local context by updating fields (zero-allocation)
            t_serializationContext.Topic = topic;
            t_serializationContext.Component = SerializationComponent.Value;
            t_serializationContext.Headers = null;
            t_serializationContext.IsNull = isValueNull;

            if (isValueNull)
            {
                Value = valueDeserializer.Deserialize(System.Buffers.ReadOnlySequence<byte>.Empty, t_serializationContext);
            }
            else
            {
                Value = valueDeserializer.Deserialize(new System.Buffers.ReadOnlySequence<byte>(valueData), t_serializationContext);
            }
        }
    }

    /// <summary>
    /// Creates a partition EOF result (no message data).
    /// This is primarily used internally by the consumer when EnablePartitionEof is true.
    /// </summary>
    /// <param name="topic">The topic name.</param>
    /// <param name="partition">The partition index.</param>
    /// <param name="offset">The current offset position.</param>
    /// <returns>A ConsumeResult with IsPartitionEof set to true.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types - factory method pattern
    public static ConsumeResult<TKey, TValue> CreatePartitionEof(string topic, int partition, long offset)
#pragma warning restore CA1000
    {
        return new ConsumeResult<TKey, TValue>(
            topic: topic,
            partition: partition,
            offset: offset,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestamp: default,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null,
            isPartitionEof: true);
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
    /// The deserialized key.
    /// </summary>
    public TKey? Key { get; }

    /// <summary>
    /// The deserialized value.
    /// </summary>
    public TValue Value { get; }

    /// <summary>
    /// The message headers. Returns null if no headers.
    /// </summary>
    public IReadOnlyList<Header>? Headers { get; }

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
    /// Indicates whether this result represents a partition end-of-file (EOF) event.
    /// When true, the consumer has reached the end of the partition (caught up to the high watermark).
    /// Key and Value will be default when this is true.
    /// </summary>
    public bool IsPartitionEof { get; }

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
