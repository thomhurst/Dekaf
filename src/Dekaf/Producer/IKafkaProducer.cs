using Dekaf.Serialization;

namespace Dekaf.Producer;

/// <summary>
/// Interface for Kafka producer.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public interface IKafkaProducer<TKey, TValue> : IInitializableKafkaClient, IAsyncDisposable
{
    /// <summary>
    /// Produces a message to Kafka.
    /// </summary>
    /// <remarks>
    /// <para><b>IMPORTANT:</b> This method returns <see cref="ValueTask{TResult}"/> which MUST be awaited
    /// immediately or converted to <see cref="Task{TResult}"/> via <c>.AsTask()</c>. Do NOT store
    /// <see cref="ValueTask{TResult}"/> instances in collections or await them later - this violates
    /// ValueTask semantics and can cause deadlocks or undefined behavior.</para>
    ///
    /// <para><b>Correct usage examples:</b></para>
    /// <code>
    /// // Single message - await immediately (recommended)
    /// var metadata = await producer.ProduceAsync(message);
    ///
    /// // Multiple messages in parallel - convert to Task (recommended)
    /// var tasks = new List&lt;Task&lt;RecordMetadata&gt;&gt;();
    /// for (int i = 0; i &lt; count; i++)
    ///     tasks.Add(producer.ProduceAsync(message).AsTask());
    /// await Task.WhenAll(tasks);
    /// </code>
    ///
    /// <para><b>INCORRECT usage - DO NOT DO THIS:</b></para>
    /// <code>
    /// // WRONG: Storing ValueTasks in a list
    /// var valueTasks = new List&lt;ValueTask&lt;RecordMetadata&gt;&gt;();
    /// for (int i = 0; i &lt; count; i++)
    ///     valueTasks.Add(producer.ProduceAsync(message));  // DEADLOCK RISK!
    /// foreach (var vt in valueTasks)
    ///     await vt;  // Undefined behavior - may deadlock
    /// </code>
    /// </remarks>
    ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a message to the specified topic.
    /// </summary>
    /// <remarks>
    /// <para>See <see cref="ProduceAsync(ProducerMessage{TKey, TValue}, CancellationToken)"/> for
    /// important information about <see cref="ValueTask{TResult}"/> usage rules.</para>
    /// </remarks>
    ValueTask<RecordMetadata> ProduceAsync(
        string topic,
        TKey? key,
        TValue value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message without waiting for acknowledgment (fire-and-forget).
    /// </summary>
    /// <remarks>
    /// <para>This method queues the message for delivery without waiting for broker acknowledgment.
    /// It provides lower latency than <see cref="ProduceAsync"/> but offers no delivery guarantees.</para>
    ///
    /// <para>To ensure all messages are delivered, call <see cref="FlushAsync"/> before disposing the producer.</para>
    ///
    /// <para>Errors during delivery will be logged but not thrown. For reliable delivery with error handling,
    /// use the callback overload or <see cref="ProduceAsync"/>.</para>
    /// </remarks>
    void Send(ProducerMessage<TKey, TValue> message);

    /// <summary>
    /// Sends a message to the specified topic without waiting for acknowledgment (fire-and-forget).
    /// </summary>
    /// <remarks>
    /// <para>This is an optimized overload that avoids allocating a <see cref="ProducerMessage{TKey, TValue}"/>
    /// object, making it ideal for high-throughput fire-and-forget scenarios.</para>
    ///
    /// <para>To ensure all messages are delivered, call <see cref="FlushAsync"/> before disposing the producer.</para>
    ///
    /// <para>Errors during delivery will be logged but not thrown. For reliable delivery with error handling,
    /// use the callback overload or <see cref="ProduceAsync"/>.</para>
    /// </remarks>
    /// <param name="topic">The topic to produce to.</param>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    void Send(string topic, TKey? key, TValue value);

    /// <summary>
    /// Sends a message without waiting for acknowledgment, with a delivery callback.
    /// </summary>
    /// <remarks>
    /// <para>This method queues the message for delivery and invokes the callback when delivery completes
    /// (either successfully or with an error).</para>
    ///
    /// <para>The callback is invoked on a background thread. Do not perform blocking operations in the callback.</para>
    /// </remarks>
    /// <param name="message">The message to produce.</param>
    /// <param name="deliveryHandler">Callback invoked when delivery completes. The exception parameter is null on success.</param>
    void Send(ProducerMessage<TKey, TValue> message, Action<RecordMetadata, Exception?> deliveryHandler);

    /// <summary>
    /// Produces multiple messages and waits for all acknowledgments.
    /// </summary>
    /// <remarks>
    /// <para>This method is the recommended way to produce multiple messages in parallel.
    /// It handles the <see cref="ValueTask{TResult}"/> to <see cref="Task{TResult}"/> conversion
    /// internally, avoiding the error-prone pattern of storing ValueTask instances in collections.</para>
    /// </remarks>
    /// <param name="messages">The messages to produce.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of record metadata for each produced message, in the same order as the input.</returns>
    Task<RecordMetadata[]> ProduceAllAsync(
        IEnumerable<ProducerMessage<TKey, TValue>> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces multiple messages to a single topic and waits for all acknowledgments.
    /// </summary>
    /// <remarks>
    /// <para>This is a convenience overload for producing multiple messages to the same topic.
    /// It avoids allocating <see cref="ProducerMessage{TKey, TValue}"/> objects for simple key-value pairs.</para>
    /// </remarks>
    /// <param name="topic">The topic to produce to.</param>
    /// <param name="messages">The key-value pairs to produce.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of record metadata for each produced message, in the same order as the input.</returns>
    Task<RecordMetadata[]> ProduceAllAsync(
        string topic,
        IEnumerable<(TKey? Key, TValue Value)> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any pending messages.
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a transaction (if transactional).
    /// </summary>
    ITransaction<TKey, TValue> BeginTransaction();

    /// <summary>
    /// Initializes transactions (must be called before BeginTransaction).
    /// </summary>
    ValueTask InitTransactionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a topic-specific producer bound to the specified topic.
    /// </summary>
    /// <remarks>
    /// <para>The returned topic producer shares connections, worker threads, and metadata cache
    /// with this producer. Multiple topic producers from the same base producer operate efficiently
    /// without duplicating resources.</para>
    /// <para>Disposing the topic producer does NOT dispose this producer. You must dispose this
    /// producer separately when done.</para>
    /// </remarks>
    /// <param name="topic">The topic to bind the producer to.</param>
    /// <returns>A producer bound to the specified topic.</returns>
    ITopicProducer<TKey, TValue> ForTopic(string topic);
}

/// <summary>
/// Message to produce.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed record ProducerMessage<TKey, TValue>
{
    /// <summary>
    /// The topic to produce to.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// The message key.
    /// </summary>
    public TKey? Key { get; init; }

    /// <summary>
    /// The message value.
    /// </summary>
    public required TValue Value { get; init; }

    /// <summary>
    /// Optional headers.
    /// </summary>
    public Headers? Headers { get; init; }

    /// <summary>
    /// Optional partition. If not set, partitioner will choose.
    /// </summary>
    public int? Partition { get; init; }

    /// <summary>
    /// Optional timestamp. If not set, current time will be used.
    /// </summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>
    /// Creates a new producer message with a null key.
    /// </summary>
    /// <param name="topic">The topic to produce to.</param>
    /// <param name="value">The message value.</param>
    /// <returns>A new producer message with a null key.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types - factory method pattern
    public static ProducerMessage<TKey, TValue> Create(string topic, TValue value)
#pragma warning restore CA1000
        => new() { Topic = topic, Value = value };

    /// <summary>
    /// Creates a new producer message.
    /// </summary>
    /// <param name="topic">The topic to produce to.</param>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <returns>A new producer message.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types - factory method pattern
    public static ProducerMessage<TKey, TValue> Create(string topic, TKey? key, TValue value)
#pragma warning restore CA1000
        => new() { Topic = topic, Key = key, Value = value };

    /// <summary>
    /// Creates a new producer message with headers.
    /// </summary>
    /// <param name="topic">The topic to produce to.</param>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="headers">The message headers.</param>
    /// <returns>A new producer message.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types - factory method pattern
    public static ProducerMessage<TKey, TValue> Create(string topic, TKey? key, TValue value, Headers headers)
#pragma warning restore CA1000
        => new() { Topic = topic, Key = key, Value = value, Headers = headers };

    /// <summary>
    /// Creates a new producer message with a specific partition.
    /// </summary>
    /// <param name="topic">The topic to produce to.</param>
    /// <param name="partition">The partition to produce to.</param>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <returns>A new producer message.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types - factory method pattern
    public static ProducerMessage<TKey, TValue> Create(string topic, int partition, TKey? key, TValue value)
#pragma warning restore CA1000
        => new() { Topic = topic, Partition = partition, Key = key, Value = value };
}

/// <summary>
/// Metadata about a produced record.
/// This is a readonly struct to eliminate per-message heap allocations.
/// </summary>
public readonly record struct RecordMetadata
{
    /// <summary>
    /// The topic the record was produced to.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// The partition the record was produced to.
    /// </summary>
    public required int Partition { get; init; }

    /// <summary>
    /// The offset of the record.
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// The timestamp of the record.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Size of the serialized key in bytes.
    /// </summary>
    public int KeySize { get; init; }

    /// <summary>
    /// Size of the serialized value in bytes.
    /// </summary>
    public int ValueSize { get; init; }
}

/// <summary>
/// Interface for a transaction.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public interface ITransaction<TKey, TValue> : IAsyncDisposable
{
    /// <summary>
    /// Produces a message within the transaction.
    /// </summary>
    ValueTask<RecordMetadata> ProduceAsync(
        ProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Aborts the transaction.
    /// </summary>
    ValueTask AbortAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends offsets to transaction for exactly-once semantics.
    /// </summary>
    ValueTask SendOffsetsToTransactionAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        string consumerGroupId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a topic, partition, and offset.
/// </summary>
public readonly record struct TopicPartitionOffset(string Topic, int Partition, long Offset);

/// <summary>
/// Represents a topic and partition.
/// </summary>
public readonly record struct TopicPartition(string Topic, int Partition);

/// <summary>
/// Represents a topic, partition, and timestamp for offset lookup.
/// </summary>
/// <param name="Topic">The topic name.</param>
/// <param name="Partition">The partition index.</param>
/// <param name="Timestamp">The timestamp to search for. Use -1 for latest offset, -2 for earliest offset.</param>
public readonly record struct TopicPartitionTimestamp(string Topic, int Partition, long Timestamp)
{
    /// <summary>
    /// Special timestamp value to get the latest offset.
    /// </summary>
    public const long Latest = -1;

    /// <summary>
    /// Special timestamp value to get the earliest offset.
    /// </summary>
    public const long Earliest = -2;

    /// <summary>
    /// Creates a TopicPartitionTimestamp from a TopicPartition and timestamp.
    /// </summary>
    public TopicPartitionTimestamp(TopicPartition topicPartition, long timestamp)
        : this(topicPartition.Topic, topicPartition.Partition, timestamp)
    {
    }

    /// <summary>
    /// Creates a TopicPartitionTimestamp from a TopicPartition and DateTimeOffset.
    /// </summary>
    public TopicPartitionTimestamp(TopicPartition topicPartition, DateTimeOffset timestamp)
        : this(topicPartition.Topic, topicPartition.Partition, timestamp.ToUnixTimeMilliseconds())
    {
    }

    /// <summary>
    /// Gets the TopicPartition for this timestamp lookup.
    /// </summary>
    public TopicPartition TopicPartition => new(Topic, Partition);
}

/// <summary>
/// Represents the low and high watermark offsets for a partition.
/// Low watermark is the earliest available offset (log start offset).
/// High watermark is the next offset to be written (end of log).
/// </summary>
public readonly record struct WatermarkOffsets(long Low, long High);
