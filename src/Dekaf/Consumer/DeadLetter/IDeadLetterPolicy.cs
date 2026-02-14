namespace Dekaf.Consumer.DeadLetter;

/// <summary>
/// Determines whether failed messages should be routed to a dead letter queue.
/// </summary>
/// <typeparam name="TKey">The message key type.</typeparam>
/// <typeparam name="TValue">The message value type.</typeparam>
public interface IDeadLetterPolicy<TKey, TValue>
{
    /// <summary>
    /// Determines whether a failed message should be sent to the DLQ.
    /// Called after OnErrorAsync completes in the consumer service.
    /// </summary>
    /// <param name="result">The consume result that failed processing.</param>
    /// <param name="exception">The exception thrown during processing.</param>
    /// <param name="failureCount">The number of times this message has failed.</param>
    /// <returns>True if the message should be routed to the DLQ.</returns>
    bool ShouldDeadLetter(ConsumeResult<TKey, TValue> result, Exception exception, int failureCount);

    /// <summary>
    /// Returns the DLQ topic name for the given source topic.
    /// </summary>
    /// <param name="sourceTopic">The original topic name.</param>
    /// <returns>The DLQ topic name.</returns>
    string GetDeadLetterTopic(string sourceTopic);
}
