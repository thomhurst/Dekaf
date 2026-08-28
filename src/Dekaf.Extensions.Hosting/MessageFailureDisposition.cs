using Dekaf.Consumer;

namespace Dekaf.Extensions.Hosting;

/// <summary>
/// Determines what a hosted consumer does when message processing cannot complete successfully.
/// </summary>
public enum MessageFailureDisposition
{
    /// <summary>
    /// Preserves the message for redelivery by leaving its offset uncommitted.
    /// </summary>
    Retry,

    /// <summary>
    /// Acknowledges the failed message and allows consumption to continue.
    /// </summary>
    Discard
}

/// <summary>
/// Identifies the operation that left a failed message without a durable outcome.
/// </summary>
public enum MessageFailureStage
{
    /// <summary>
    /// Message processing failed and no configured retry or routing operation handled it.
    /// </summary>
    Processing,

    /// <summary>
    /// Producing the message to a retry topic failed.
    /// </summary>
    RetryTopicRouting,

    /// <summary>
    /// Producing the message to a dead letter topic failed or was unavailable.
    /// </summary>
    DeadLetterRouting
}

/// <summary>
/// Describes a message processing failure that has no durable successful outcome.
/// </summary>
public readonly struct MessageFailureContext<TKey, TValue>
{
    /// <summary>
    /// Initializes a new failure context.
    /// </summary>
    public MessageFailureContext(
        ConsumeResult<TKey, TValue> result,
        Exception processingException,
        int attemptNumber,
        int failureCount,
        MessageFailureStage stage,
        Exception? routingException = null)
    {
        Result = result;
        ProcessingException = processingException;
        AttemptNumber = attemptNumber;
        FailureCount = failureCount;
        Stage = stage;
        RoutingException = routingException;
    }

    /// <summary>
    /// Gets the failed record.
    /// </summary>
    public ConsumeResult<TKey, TValue> Result { get; }

    /// <summary>
    /// Gets the exception thrown by message processing.
    /// </summary>
    public Exception ProcessingException { get; }

    /// <summary>
    /// Gets the one-based processing attempt number for this delivery.
    /// </summary>
    public int AttemptNumber { get; }

    /// <summary>
    /// Gets the cumulative failure count, including retry-topic deliveries.
    /// </summary>
    public int FailureCount { get; }

    /// <summary>
    /// Gets the operation that left the message without a durable outcome.
    /// </summary>
    public MessageFailureStage Stage { get; }

    /// <summary>
    /// Gets the retry-topic or dead-letter routing exception, when routing failed.
    /// </summary>
    public Exception? RoutingException { get; }
}
