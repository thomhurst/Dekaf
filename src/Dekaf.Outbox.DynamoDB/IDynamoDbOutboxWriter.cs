using Amazon.DynamoDBv2.Model;

namespace Dekaf.Outbox.DynamoDB;

/// <summary>
/// Enqueue side of the DynamoDB outbox: turns <see cref="OutboxMessage"/> instances into
/// items that commit atomically with the business write.
/// </summary>
/// <remarks>
/// <para><b>Ordering:</b> DynamoDB has no auto-increment, so each message takes the next
/// number of its bucket's atomic counter before the business transaction commits. The order
/// of two messages is the order of their reservations, independent of host clocks. A
/// reservation that is never committed leaves a gap, which is harmless.</para>
/// <para><b>Retries:</b> create the items inside the retry loop. A transaction that lost an
/// optimistic concurrency race and is rebuilt from fresh state must reserve again, otherwise
/// its message keeps a number older than the write that won the race.</para>
/// </remarks>
public interface IDynamoDbOutboxWriter
{
    /// <summary>
    /// Reserves a sequence number and returns the <c>Put</c> to add to the
    /// <c>TransactWriteItems</c> request that carries the business write.
    /// </summary>
    ValueTask<TransactWriteItem> CreateTransactWriteItemAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserves sequence numbers, one request per distinct bucket, and returns one <c>Put</c>
    /// per message in the same order. Messages sharing a bucket keep their list order.
    /// </summary>
    ValueTask<IReadOnlyList<TransactWriteItem>> CreateTransactWriteItemsAsync(
        IReadOnlyList<OutboxMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Wakes the local relay after the transaction holding <paramref name="message"/>
    /// committed. Never call before the commit. Optional: polling finds the message anyway.
    /// </summary>
    void NotifyCommitted(OutboxMessage message);

    /// <summary>
    /// Wakes the local relay after the transaction holding <paramref name="messages"/>
    /// committed. Never call before the commit. Optional: polling finds the messages anyway.
    /// </summary>
    void NotifyCommitted(IReadOnlyList<OutboxMessage> messages);

    /// <summary>
    /// Writes one message without a business write and wakes the local relay.
    /// </summary>
    /// <remarks>
    /// Safe under the AWS SDK's own retries: a retried request whose first attempt was applied
    /// is a success. Calling this method again for the same message is a new write under a new
    /// sequence number: the message is then stored and published twice under one
    /// <see cref="OutboxMessage.MessageId"/>, which consumers deduplicate like any other
    /// at-least-once redelivery.
    /// </remarks>
    ValueTask EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the messages in one transaction, all or nothing, and wakes the local relay.
    /// At most <see cref="DynamoDbOutboxWriter.MaxTransactionItems"/> messages.
    /// </summary>
    /// <remarks>
    /// The same retry contract as <see cref="EnqueueAsync(OutboxMessage, CancellationToken)"/>;
    /// the transaction carries an idempotency token for the SDK's retries.
    /// </remarks>
    ValueTask EnqueueAsync(IReadOnlyList<OutboxMessage> messages, CancellationToken cancellationToken = default);
}
