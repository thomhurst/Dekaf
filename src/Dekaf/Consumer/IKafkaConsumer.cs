using Dekaf.Producer;
using Dekaf.Serialization;
using Dekaf.Telemetry;
#if NETSTANDARD2_0
using StringSet = System.Collections.Generic.IReadOnlyCollection<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using StringSet = System.Collections.Generic.IReadOnlySet<string>;
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

namespace Dekaf.Consumer;

/// <summary>
/// Interface for Kafka consumer.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public interface IKafkaConsumer<TKey, TValue> : IInitializableKafkaClient, IAsyncDisposable
{
    /// <summary>
    /// Gets the current subscription.
    /// </summary>
    StringSet Subscription { get; }

    /// <summary>
    /// Gets the current server-side topic regex subscription, if any.
    /// </summary>
    string? SubscriptionPattern { get; }

    /// <summary>
    /// Gets the current assignment.
    /// </summary>
    TopicPartitionSet Assignment { get; }

    /// <summary>
    /// Gets the paused partitions.
    /// </summary>
    TopicPartitionSet Paused { get; }

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
    /// Gets position and seek operations.
    /// </summary>
    IConsumerPositions Positions { get; }

    /// <summary>
    /// Gets assignment and pause/resume operations.
    /// </summary>
    IConsumerPartitions Partitions { get; }

    /// <summary>
    /// Gets offset lookup operations.
    /// </summary>
    IConsumerOffsets Offsets { get; }

    /// <summary>
    /// Subscribes to topics.
    /// </summary>
    void Subscribe(params string[] topics);

    /// <summary>
    /// Subscribes to topics matching a pattern.
    /// </summary>
    void Subscribe(Func<string, bool> topicFilter);

    /// <summary>
    /// Subscribes to topics matching a broker-side RE2/J regular expression.
    /// </summary>
    /// <remarks>
    /// The pattern is sent to Kafka as-is. .NET regular expression syntax is not translated to RE2/J.
    /// Requires ConsumerGroupHeartbeat v1, available on Kafka 4.1+ brokers.
    /// </remarks>
    /// <param name="pattern">The RE2/J topic regex pattern.</param>
    void SubscribePattern(string pattern);

    /// <summary>
    /// Unsubscribes from all topics.
    /// </summary>
    void Unsubscribe();

    /// <summary>
    /// Consumes messages as an async enumerable.
    /// </summary>
    IAsyncEnumerable<ConsumeResult<TKey, TValue>> ConsumeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes a single message.
    /// </summary>
    ValueTask<ConsumeResult<TKey, TValue>?> ConsumeOneAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes messages in batches for maximum throughput.
    /// Each batch contains all records from a single partition fetch response.
    /// Records within a batch are iterated synchronously (no async overhead per message).
    /// Position tracking is deferred to batch completion.
    /// Partition EOF events are not surfaced by this method; use <see cref="ConsumeAsync"/> for EOF notification.
    /// </summary>
    IAsyncEnumerable<ConsumeBatch<TKey, TValue>> ConsumeBatchAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Consumes raw (undeserialized) messages in batches for maximum throughput.
    /// Records provide zero-copy <see cref="ReadOnlyMemory{T}"/> access to key/value data.
    /// No deserialization, header copying, interceptors, or tracing overhead.
    /// Partition EOF events are not surfaced by this method; use <see cref="ConsumeAsync"/> for EOF notification.
    /// </summary>
    IAsyncEnumerable<ConsumeRawBatch> ConsumeRawBatchAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers or replaces an application metric for broker telemetry subscriptions.
    /// </summary>
    /// <param name="metric">The application metric to register.</param>
    void RegisterMetricForSubscription(ApplicationTelemetryMetric metric);

    /// <summary>
    /// Unregisters an application metric from broker telemetry subscriptions.
    /// Missing names are ignored.
    /// </summary>
    /// <param name="name">The application metric name.</param>
    void UnregisterMetricFromSubscription(string name);

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
    /// Stores the consumed message's next offset for the automatic commit loop.
    /// Use with <see cref="ConsumerOptions.EnableAutoOffsetStore"/> disabled to store offsets only
    /// after message processing succeeds.
    /// </summary>
    void StoreOffset(ConsumeResult<TKey, TValue> result);

    /// <summary>
    /// Stores an offset for the automatic commit loop.
    /// </summary>
    void StoreOffset(TopicPartitionOffset offset);

    /// <summary>
    /// Gracefully closes the consumer: stops background tasks (heartbeat, auto-commit, prefetch),
    /// notifies <see cref="IPartitionStopListener"/> when configured, commits pending offsets,
    /// leaves the consumer group, and releases resources.
    /// </summary>
    /// <remarks>
    /// <para><b>Optional:</b> Calling <c>CloseAsync</c> explicitly is not required.
    /// <see cref="IAsyncDisposable.DisposeAsync"/> calls <c>CloseAsync</c> automatically if it
    /// has not already been called. Use <c>CloseAsync</c> when you need explicit control over
    /// close timing before disposal — for example, to observe close errors, to ensure a final
    /// commit completes before the <c>await using</c> block exits, or to close with a specific
    /// cancellation token.</para>
    /// <para><b>Idempotent:</b> Safe to call multiple times. Subsequent calls after the first
    /// return immediately with no side effects. It is also safe to call <c>DisposeAsync</c>
    /// after <c>CloseAsync</c> — disposal will detect that close has already completed and
    /// skip the close step.</para>
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous close operation.</returns>
    ValueTask CloseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Position and seek operations for a Kafka consumer.
/// </summary>
public interface IConsumerPositions
{
    /// <summary>
    /// Gets the committed offset for a partition.
    /// </summary>
    ValueTask<long?> GetCommittedOffsetAsync(TopicPartition partition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current position (next offset to consume) for a partition.
    /// In auto-commit mode, reflects the last fully-processed batch boundary;
    /// for per-message accuracy, use manual commit mode.
    /// </summary>
    long? GetPosition(TopicPartition partition);

    /// <summary>
    /// Seeks to a specific offset.
    /// </summary>
    void Seek(TopicPartitionOffset offset);

    /// <summary>
    /// Seeks to the beginning of partitions.
    /// </summary>
    void SeekToBeginning(params TopicPartition[] partitions);

    /// <summary>
    /// Seeks to the end of partitions.
    /// </summary>
    void SeekToEnd(params TopicPartition[] partitions);
}

/// <summary>
/// Assignment and pause/resume operations for a Kafka consumer.
/// </summary>
public interface IConsumerPartitions
{
    /// <summary>
    /// Gets the current assignment.
    /// </summary>
    TopicPartitionSet Assignment { get; }

    /// <summary>
    /// Gets the paused partitions.
    /// </summary>
    TopicPartitionSet Paused { get; }

    /// <summary>
    /// Manually assigns partitions.
    /// </summary>
    void Assign(params TopicPartition[] partitions);

    /// <summary>
    /// Unassigns all partitions.
    /// </summary>
    void Unassign();

    /// <summary>
    /// Incrementally adds partitions to the current assignment.
    /// Used with cooperative rebalancing (CooperativeSticky assignor).
    /// Unlike <see cref="Assign"/>, does not replace the entire assignment.
    /// </summary>
    /// <param name="partitions">The partitions to add with optional starting offsets.</param>
    void IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions);

    /// <summary>
    /// Incrementally removes partitions from the current assignment.
    /// Used with cooperative rebalancing (CooperativeSticky assignor).
    /// Unlike <see cref="Unassign"/>, only removes the specified partitions.
    /// </summary>
    /// <param name="partitions">The partitions to remove.</param>
    void IncrementalUnassign(IEnumerable<TopicPartition> partitions);

    /// <summary>
    /// Pauses consumption from partitions.
    /// </summary>
    void Pause(params TopicPartition[] partitions);

    /// <summary>
    /// Resumes consumption from partitions.
    /// </summary>
    void Resume(params TopicPartition[] partitions);
}

/// <summary>
/// Offset lookup operations for a Kafka consumer.
/// </summary>
public interface IConsumerOffsets
{
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
/// Timestamp is computed on demand from stored Unix milliseconds to avoid per-message DateTimeOffset construction.
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

    // Store raw Unix milliseconds instead of DateTimeOffset to avoid per-message
    // DateTimeOffset.FromUnixTimeMilliseconds() construction in the consume loop.
    // The DateTimeOffset is computed on demand via the Timestamp property.
    private readonly long _timestampMs;
    private readonly IReadOnlyList<Header>? _headers;
    private readonly Header[]? _pooledHeaders;
    private readonly int _pooledHeaderCount;
    private readonly PendingFetchData? _headerOwner;
    private readonly int _headerGeneration;

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
        long timestampMs,
        TimestampType timestampType,
        int? leaderEpoch,
        IDeserializer<TKey>? keyDeserializer,
        IDeserializer<TValue>? valueDeserializer,
        bool isPartitionEof = false)
        : this(
            topic,
            partition,
            offset,
            keyData,
            isKeyNull,
            valueData,
            isValueNull,
            headers,
            pooledHeaders: null,
            pooledHeaderCount: 0,
            headerOwner: null,
            headerGeneration: 0,
            timestampMs,
            timestampType,
            leaderEpoch,
            keyDeserializer,
            valueDeserializer,
            isPartitionEof)
    {
    }

    internal ConsumeResult(
        string topic,
        int partition,
        long offset,
        ReadOnlyMemory<byte> keyData,
        bool isKeyNull,
        ReadOnlyMemory<byte> valueData,
        bool isValueNull,
        Header[]? pooledHeaders,
        int pooledHeaderCount,
        PendingFetchData headerOwner,
        long timestampMs,
        TimestampType timestampType,
        int? leaderEpoch,
        IDeserializer<TKey>? keyDeserializer,
        IDeserializer<TValue>? valueDeserializer)
        : this(
            topic,
            partition,
            offset,
            keyData,
            isKeyNull,
            valueData,
            isValueNull,
            headers: null,
            pooledHeaders,
            pooledHeaderCount,
            headerOwner,
            headerOwner.HeaderGeneration,
            timestampMs,
            timestampType,
            leaderEpoch,
            keyDeserializer,
            valueDeserializer,
            isPartitionEof: false)
    {
    }

    private ConsumeResult(
        string topic,
        int partition,
        long offset,
        ReadOnlyMemory<byte> keyData,
        bool isKeyNull,
        ReadOnlyMemory<byte> valueData,
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaders,
        int pooledHeaderCount,
        PendingFetchData? headerOwner,
        int headerGeneration,
        long timestampMs,
        TimestampType timestampType,
        int? leaderEpoch,
        IDeserializer<TKey>? keyDeserializer,
        IDeserializer<TValue>? valueDeserializer,
        bool isPartitionEof)
    {
        Topic = topic;
        Partition = partition;
        Offset = offset;
        _headers = headers;
        _pooledHeaders = pooledHeaders;
        _pooledHeaderCount = pooledHeaderCount;
        _headerOwner = headerOwner;
        _headerGeneration = headerGeneration;
        _timestampMs = timestampMs;
        TimestampType = timestampType;
        LeaderEpoch = leaderEpoch;
        IsPartitionEof = isPartitionEof;

        // Eagerly deserialize to avoid storing deserializer references (saves 16 bytes per struct)
        if (isPartitionEof || keyDeserializer is null)
        {
            Key = default;
        }
        else if (isKeyNull)
        {
            // Null keys return default directly — TKey? is nullable so no deserializer call needed
            Key = default;
        }
        else
        {
            t_serializationContext.Topic = topic;
            t_serializationContext.Component = SerializationComponent.Key;
            t_serializationContext.Headers = null;
            t_serializationContext.IsNull = false;

            Key = keyDeserializer.Deserialize(keyData, t_serializationContext);
        }

        if (isPartitionEof || valueDeserializer is null)
        {
            Value = default!;
        }
        else
        {
            t_serializationContext.Topic = topic;
            t_serializationContext.Component = SerializationComponent.Value;
            t_serializationContext.Headers = null;
            t_serializationContext.IsNull = isValueNull;

            Value = isValueNull
                ? valueDeserializer.Deserialize(ReadOnlyMemory<byte>.Empty, t_serializationContext)
                : valueDeserializer.Deserialize(valueData, t_serializationContext);
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
            timestampMs: 0,
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
    /// Header arrays from consumed record batches are copied lazily on first access; if a
    /// result is retained beyond the consume loop, access this property before the owning
    /// fetch batch is disposed to keep a safe snapshot.
    /// </summary>
    public IReadOnlyList<Header>? Headers => _headers ?? LazyConsumeHeaders.Create(
        _pooledHeaders,
        _pooledHeaderCount,
        _headerOwner,
        _headerGeneration);

    /// <summary>
    /// The message timestamp as a DateTimeOffset.
    /// Computed on demand from the stored Unix milliseconds to avoid per-message construction overhead.
    /// </summary>
    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(_timestampMs);

    /// <summary>
    /// The message timestamp as raw Unix milliseconds since epoch.
    /// Prefer this over <see cref="Timestamp"/> in hot paths to avoid DateTimeOffset construction.
    /// </summary>
    public long TimestampMs => _timestampMs;

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
    public TopicPartitionOffset TopicPartitionOffset => new(Topic, Partition, Offset, LeaderEpoch ?? -1);

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
