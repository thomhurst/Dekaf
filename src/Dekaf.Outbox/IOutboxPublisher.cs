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
    /// Submits the batch in order and waits for the outcome of every started record.
    /// </summary>
    /// <remarks>
    /// The result's <see cref="OutboxPublishResult.AckedCount"/> is the length of the
    /// contiguous acknowledged prefix: the first failed record stops the count even if later
    /// records were acknowledged. The relay marks exactly that prefix as published, so a
    /// bucket's rows are only ever removed front-to-back. This does not guarantee consumer
    /// order across partial failures: if row 1 fails while row 2 succeeds, retrying both can
    /// deliver 2, 1, 2. Message-id deduplication still leaves 2, 1. Implementations promising
    /// stronger ordering must prevent later rows becoming visible before earlier rows succeed.
    /// <para>
    /// With an IOutboxLeaseRenewalStore, the relay maintains ownership throughout an
    /// asynchronous call without rebalancing its active buckets. Implementations must
    /// yield while waiting and observe cancellation so renewal and shutdown can run.
    /// With a legacy store, OutboxRelayOptions.MaxPublishDuration must bound the entire
    /// call: submission of every row, local/backpressure waits, and every delivery attempt.
    /// It is not one record's delivery timeout and is not enforced by cancelling delivery.
    /// An already-appended Kafka record can still be delivered after cancellation or lease loss.
    /// </para>
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
