namespace Dekaf.Consumer.DeadLetter;

/// <summary>
/// Default dead letter policy that routes messages to DLQ after a configured number of failures.
/// </summary>
/// <typeparam name="TKey">The message key type.</typeparam>
/// <typeparam name="TValue">The message value type.</typeparam>
public sealed class DefaultDeadLetterPolicy<TKey, TValue> : IDeadLetterPolicy<TKey, TValue>
{
    private readonly DeadLetterOptions _options;

    /// <summary>
    /// Creates a new default dead letter policy with the specified options.
    /// </summary>
    /// <param name="options">The dead letter queue options.</param>
    public DefaultDeadLetterPolicy(DeadLetterOptions options) => _options = options;

    /// <inheritdoc/>
    public bool ShouldDeadLetter(ConsumeResult<TKey, TValue> result, Exception exception, int failureCount)
        => failureCount >= _options.MaxFailures;

    /// <inheritdoc/>
    public string GetDeadLetterTopic(string sourceTopic)
        => sourceTopic + _options.TopicSuffix;
}
