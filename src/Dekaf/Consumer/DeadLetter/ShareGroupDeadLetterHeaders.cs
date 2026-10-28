using System.Buffers.Text;
using Dekaf.Serialization;

namespace Dekaf.Consumer.DeadLetter;

/// <summary>
/// Reads the context headers that a Kafka 4.4+ broker writes on share-group dead-letter records (KIP-1191).
/// </summary>
/// <remarks>
/// The broker, not the client, writes these records when a share consumer rejects a record or the record
/// reaches the group's delivery limit. Numeric values are UTF-8 decimal strings. With
/// errors.deadletterqueue.copy.record.enable, Kafka 4.4.0 copies the original key and value but not the original
/// headers, so a dead-letter record carries only these headers. The DLQ write is not atomic with the share-state write,
/// so a source record can occasionally produce more than one dead-letter record.
/// </remarks>
public static class ShareGroupDeadLetterHeaders
{
    /// <summary>Header key for the source topic name.</summary>
    public const string TopicKey = "__dlq.errors.topic";
    /// <summary>Header key for the source partition.</summary>
    public const string PartitionKey = "__dlq.errors.partition";
    /// <summary>Header key for the source offset.</summary>
    public const string OffsetKey = "__dlq.errors.offset";
    /// <summary>Header key for the share group that dead-lettered the record.</summary>
    public const string GroupKey = "__dlq.errors.group";
    /// <summary>Header key for the delivery count when the record was dead-lettered.</summary>
    public const string DeliveryCountKey = "__dlq.errors.delivery.count";
    /// <summary>Header key for the broker's reason, such as a client reject or an exceeded delivery limit.</summary>
    public const string MessageKey = "__dlq.errors.message";

    /// <summary>
    /// Parses every dead-letter context header. Numeric headers parse without allocating; the topic,
    /// group, and message headers allocate their decoded strings.
    /// </summary>
    /// <returns>
    /// False when the topic, partition, offset, group, or delivery-count header is missing or malformed.
    /// The message header is optional.
    /// </returns>
    public static bool TryParse(IReadOnlyList<Header>? headers, out ShareGroupDeadLetterContext context)
    {
        if (TryGetPartition(headers, out var partition)
            && TryGetOffset(headers, out var offset)
            && TryGetDeliveryCount(headers, out var deliveryCount)
            && GetTopic(headers) is { Length: > 0 } topic
            && GetGroup(headers) is { Length: > 0 } group)
        {
            context = new ShareGroupDeadLetterContext(topic, partition, offset, group, deliveryCount, GetMessage(headers));
            return true;
        }

        context = default;
        return false;
    }

    /// <summary>Gets the source topic, or null when the header is missing.</summary>
    public static string? GetTopic(IReadOnlyList<Header>? headers)
        => TryFind(headers, TopicKey, out var header) ? header.GetValueAsString() : null;

    /// <summary>Gets the share group, or null when the header is missing.</summary>
    public static string? GetGroup(IReadOnlyList<Header>? headers)
        => TryFind(headers, GroupKey, out var header) ? header.GetValueAsString() : null;

    /// <summary>Gets the broker's dead-letter reason, or null when the header is missing.</summary>
    public static string? GetMessage(IReadOnlyList<Header>? headers)
        => TryFind(headers, MessageKey, out var header) ? header.GetValueAsString() : null;

    /// <summary>Gets the source partition without allocating.</summary>
    /// <returns>False when the header is missing, malformed, or negative.</returns>
    public static bool TryGetPartition(IReadOnlyList<Header>? headers, out int partition)
    {
        var parsed = TryGetNumber(headers, PartitionKey, minimum: 0, int.MaxValue, out var value);
        partition = (int)value;
        return parsed;
    }

    /// <summary>Gets the source offset without allocating.</summary>
    /// <returns>False when the header is missing, malformed, or negative.</returns>
    public static bool TryGetOffset(IReadOnlyList<Header>? headers, out long offset)
        => TryGetNumber(headers, OffsetKey, minimum: 0, long.MaxValue, out offset);

    /// <summary>Gets the delivery count without allocating.</summary>
    /// <returns>False when the header is missing, malformed, or not positive.</returns>
    public static bool TryGetDeliveryCount(IReadOnlyList<Header>? headers, out int deliveryCount)
    {
        var parsed = TryGetNumber(headers, DeliveryCountKey, minimum: 1, int.MaxValue, out var value);
        deliveryCount = (int)value;
        return parsed;
    }

    private static bool TryGetNumber(IReadOnlyList<Header>? headers, string key, long minimum, long maximum, out long value)
    {
        if (TryFind(headers, key, out var header))
        {
            var bytes = header.Value.Span;
            if (Utf8Parser.TryParse(bytes, out value, out var consumed)
                && consumed == bytes.Length
                && value >= minimum
                && value <= maximum)
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    // Indexed lookup avoids an interface-enumerator allocation. A null value counts as missing.
    private static bool TryFind(IReadOnlyList<Header>? headers, string key, out Header header)
    {
        if (headers is not null)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                header = headers[i];
                if (string.Equals(header.Key, key, StringComparison.Ordinal))
                    return !header.IsValueNull;
            }
        }

        header = default;
        return false;
    }
}

/// <summary>
/// The source record and reason that a broker recorded on a share-group dead-letter record (KIP-1191).
/// </summary>
/// <param name="Topic">The source topic.</param>
/// <param name="Partition">The source partition.</param>
/// <param name="Offset">The source offset.</param>
/// <param name="Group">The share group that dead-lettered the record.</param>
/// <param name="DeliveryCount">The delivery count when the record was dead-lettered.</param>
/// <param name="Message">The broker's reason, or null when the header is missing.</param>
public readonly record struct ShareGroupDeadLetterContext(
    string Topic,
    int Partition,
    long Offset,
    string Group,
    int DeliveryCount,
    string? Message);
