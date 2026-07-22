namespace Dekaf.Outbox;

/// <summary>
/// Publishes outbox rows to Kafka on behalf of the relay. The default implementation wraps a
/// Dekaf producer; the seam exists so the relay engine can be tested without a broker.
/// </summary>
public interface IOutboxPublisher : IAsyncDisposable
{
    /// <summary>
    /// Initializes the underlying producer (metadata bootstrap, connections).
    /// </summary>
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the batch in order and waits for broker acknowledgment of every record.
    /// </summary>
    /// <remarks>
    /// The result's <see cref="OutboxPublishResult.AckedCount"/> is the length of the
    /// contiguous acknowledged prefix: the first failed record stops the count even if later
    /// records were acknowledged. The relay marks exactly that prefix as published, so a
    /// bucket's rows are only ever removed front-to-back and publish order is preserved
    /// across retries.
    /// </remarks>
    /// <param name="messages">The rows to publish, in ascending id order.</param>
    /// <param name="messageIdHeaderName">Header name to stamp with each row's message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<OutboxPublishResult> PublishAsync(
        IReadOnlyList<OutboxMessage> messages,
        string messageIdHeaderName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a publish attempt.
/// </summary>
/// <param name="AckedCount">Length of the contiguous acknowledged prefix of the batch.</param>
/// <param name="FirstError">The first failure, or null when the whole batch was acknowledged.</param>
public readonly record struct OutboxPublishResult(int AckedCount, Exception? FirstError);
