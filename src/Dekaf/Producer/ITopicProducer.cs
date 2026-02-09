using Dekaf.Serialization;

namespace Dekaf.Producer;

/// <summary>
/// A producer bound to a specific topic, providing a simplified API without topic parameters.
/// </summary>
/// <remarks>
/// <para>Topic producers can be created in several ways:</para>
/// <list type="bullet">
/// <item><description>From the builder: <c>Dekaf.CreateProducer&lt;K,V&gt;().WithBootstrapServers(...).BuildForTopic("my-topic")</c></description></item>
/// <item><description>From an existing producer: <c>producer.ForTopic("my-topic")</c></description></item>
/// <item><description>Direct factory: <c>Kafka.CreateTopicProducer&lt;K,V&gt;("localhost:9092", "my-topic")</c></description></item>
/// </list>
/// <para>When created from an existing producer via <see cref="IKafkaProducer{TKey,TValue}.ForTopic"/>,
/// multiple topic producers can share the same underlying connections and worker threads.</para>
/// </remarks>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public interface ITopicProducer<TKey, TValue> : IAsyncDisposable
{
    /// <summary>
    /// Gets the topic this producer is bound to.
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// Initializes the underlying producer by fetching cluster metadata and setting up idempotent producer state.
    /// Must be called before producing messages, unless the producer was created via <c>BuildForTopicAsync()</c>
    /// which calls this automatically.
    /// </summary>
    /// <remarks>
    /// <para>This method is idempotent and thread-safe. Calling it multiple times has no effect
    /// after the first successful initialization.</para>
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a message to the bound topic.
    /// </summary>
    /// <remarks>
    /// <para><b>IMPORTANT:</b> This method returns <see cref="ValueTask{TResult}"/> which MUST be awaited
    /// immediately or converted to <see cref="Task{TResult}"/> via <c>.AsTask()</c>. Do NOT store
    /// <see cref="ValueTask{TResult}"/> instances in collections or await them later.</para>
    /// </remarks>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata about the produced record.</returns>
    ValueTask<RecordMetadata> ProduceAsync(
        TKey? key,
        TValue value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a message with headers to the bound topic.
    /// </summary>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="headers">The message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata about the produced record.</returns>
    ValueTask<RecordMetadata> ProduceAsync(
        TKey? key,
        TValue value,
        Headers headers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a message to a specific partition of the bound topic.
    /// </summary>
    /// <param name="partition">The partition to produce to.</param>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata about the produced record.</returns>
    ValueTask<RecordMetadata> ProduceAsync(
        int partition,
        TKey? key,
        TValue value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a message with full control over all properties.
    /// </summary>
    /// <param name="message">The message to produce.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata about the produced record.</returns>
    ValueTask<RecordMetadata> ProduceAsync(
        TopicProducerMessage<TKey, TValue> message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message without waiting for acknowledgment (fire-and-forget).
    /// </summary>
    /// <remarks>
    /// <para>To ensure all messages are delivered, call <see cref="FlushAsync"/> before disposing.</para>
    /// </remarks>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    void Send(TKey? key, TValue value);

    /// <summary>
    /// Sends a message with headers without waiting for acknowledgment (fire-and-forget).
    /// </summary>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="headers">The message headers.</param>
    void Send(TKey? key, TValue value, Headers headers);

    /// <summary>
    /// Sends a message with a delivery callback (fire-and-forget with notification).
    /// </summary>
    /// <param name="key">The message key (can be null).</param>
    /// <param name="value">The message value.</param>
    /// <param name="deliveryHandler">Callback invoked when delivery completes. The exception parameter is null on success.</param>
    void Send(TKey? key, TValue value, Action<RecordMetadata, Exception?> deliveryHandler);

    /// <summary>
    /// Produces multiple messages and waits for all acknowledgments.
    /// </summary>
    /// <param name="messages">The key-value pairs to produce.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of record metadata for each produced message, in the same order as the input.</returns>
    Task<RecordMetadata[]> ProduceAllAsync(
        IEnumerable<(TKey? Key, TValue Value)> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces multiple messages with full control and waits for all acknowledgments.
    /// </summary>
    /// <param name="messages">The messages to produce.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An array of record metadata for each produced message, in the same order as the input.</returns>
    Task<RecordMetadata[]> ProduceAllAsync(
        IEnumerable<TopicProducerMessage<TKey, TValue>> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any pending messages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}
