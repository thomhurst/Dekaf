namespace Dekaf.Producer;

/// <summary>
/// Interceptor for producer operations. Allows hooking into the produce pipeline
/// for cross-cutting concerns like tracing, metrics, header injection, and auditing.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
/// <remarks>
/// <para>Interceptors are called in the order they are added via
/// <see cref="ProducerBuilder{TKey, TValue}.AddInterceptor"/>.</para>
/// <para>Exceptions thrown by interceptors are caught and logged, not propagated.
/// This ensures interceptor failures do not impact message production.</para>
/// </remarks>
public interface IProducerInterceptor<TKey, TValue>
{
    /// <summary>
    /// Called before a message is serialized and sent to Kafka.
    /// Can modify the message (e.g., add headers, transform key/value).
    /// </summary>
    /// <param name="message">The message about to be produced.</param>
    /// <returns>The (potentially modified) message to produce. Must not return null.</returns>
    /// <remarks>
    /// <para>This method is called on the producer's hot path. Implementations should
    /// be lightweight and avoid blocking operations.</para>
    /// <para>If an interceptor throws, the original message is used and the exception is logged.</para>
    /// </remarks>
    ProducerMessage<TKey, TValue> OnSend(ProducerMessage<TKey, TValue> message);

    /// <summary>
    /// Called when the broker acknowledges (or rejects) a message.
    /// </summary>
    /// <param name="metadata">The metadata of the acknowledged record.</param>
    /// <param name="exception">The exception if the produce failed, or null on success.</param>
    /// <remarks>
    /// <para>This method is called on a background thread. Implementations must be thread-safe.</para>
    /// <para>Exceptions thrown by this method are caught and logged.</para>
    /// </remarks>
    void OnAcknowledgement(RecordMetadata metadata, Exception? exception);
}
