using Dekaf.Producer;

namespace Dekaf.Consumer;

/// <summary>
/// Interceptor for consumer operations. Allows hooking into the consume pipeline
/// for cross-cutting concerns like tracing, metrics, header inspection, and auditing.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
/// <remarks>
/// <para>Interceptors are called in the order they are added via
/// <see cref="ConsumerBuilder{TKey, TValue}.AddInterceptor"/>.</para>
/// <para>Exceptions thrown by interceptors are caught and logged, not propagated.
/// This ensures interceptor failures do not impact message consumption.</para>
/// <para><b>Warning:</b> Interceptor implementations that accumulate state (e.g., tracking
/// consumed offsets, caching records, or collecting metrics) must manage their own
/// cleanup to avoid unbounded memory growth. The consumer does not manage interceptor
/// lifecycle beyond invoking the callback methods.</para>
/// </remarks>
public interface IConsumerInterceptor<TKey, TValue>
{
    /// <summary>
    /// Called after a message is deserialized, before it is returned to the user.
    /// Can modify the consume result (e.g., inspect headers, transform values).
    /// </summary>
    /// <param name="result">The deserialized consume result.</param>
    /// <returns>The (potentially modified) consume result to return to the user.</returns>
    /// <remarks>
    /// <para>This method is called on the consumer's hot path. Implementations should
    /// be lightweight and avoid blocking operations.</para>
    /// <para>If an interceptor throws, the original result is used and the exception is logged.</para>
    /// </remarks>
    ConsumeResult<TKey, TValue> OnConsume(ConsumeResult<TKey, TValue> result);

    /// <summary>
    /// Called when offsets are successfully committed.
    /// </summary>
    /// <param name="offsets">The offsets that were committed.</param>
    /// <remarks>
    /// <para>This method is called after a successful offset commit.</para>
    /// <para>Exceptions thrown by this method are caught and logged.</para>
    /// </remarks>
    void OnCommit(IReadOnlyList<TopicPartitionOffset> offsets);
}
