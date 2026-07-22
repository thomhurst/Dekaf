using Dekaf.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Dekaf.Outbox.EntityFrameworkCore;

/// <summary>
/// Enqueue-side helpers: add outbox rows inside the same transaction as the business write.
/// </summary>
public static class OutboxDbContextExtensions
{
    /// <summary>
    /// Adds a pre-built outbox message to the context. It is inserted when the surrounding
    /// <c>SaveChanges</c> commits, atomically with the business entities in the same
    /// transaction.
    /// </summary>
    public static void AddOutboxMessage(this DbContext context, OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);
        context.Set<OutboxMessage>().Add(message);
    }

    /// <summary>
    /// Serializes and adds an outbox message in one call. See
    /// <see cref="OutboxMessage.Create{TKey, TValue}"/> for parameter semantics.
    /// </summary>
    /// <returns>The created row, e.g. to read its <see cref="OutboxMessage.MessageId"/>.</returns>
    public static OutboxMessage AddOutboxMessage<TKey, TValue>(
        this DbContext context,
        string topic,
        TKey? key,
        TValue? value,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        Headers? headers = null,
        int bucketCount = OutboxRelayOptions.DefaultBucketCount,
        int? partition = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = OutboxMessage.Create(
            topic, key, value, keySerializer, valueSerializer, headers, bucketCount, partition);
        context.Set<OutboxMessage>().Add(message);
        return message;
    }
}
