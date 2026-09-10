using Dekaf.ShareConsumer;

namespace Dekaf.Extensions.Hosting;

/// <summary>
/// Describes a message processing failure that has no durable successful outcome.
/// </summary>
public readonly struct ShareMessageFailureContext<TKey, TValue>
{
    /// <summary>
    /// Initializes a new failure context.
    /// </summary>
    public ShareMessageFailureContext(
        ShareConsumeResult<TKey, TValue> result,
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
    public ShareConsumeResult<TKey, TValue> Result { get; }

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
