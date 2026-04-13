using Dekaf.Serialization;

namespace Dekaf.ShareConsumer;

/// <summary>
/// A record delivered by the share consumer, with share-group-specific metadata.
/// Unlike the regular consumer's <c>ConsumeResult</c> (a readonly struct), this is a class
/// because the acknowledgement state is mutable between poll and commit.
/// </summary>
public sealed class ShareConsumeResult<TKey, TValue>
{
    /// <summary>
    /// The topic this record was consumed from.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// The partition this record was consumed from.
    /// </summary>
    public required int Partition { get; init; }

    /// <summary>
    /// The offset of this record within the partition.
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// The deserialized key, or default if the record has no key.
    /// </summary>
    public TKey? Key { get; init; }

    /// <summary>
    /// The deserialized value.
    /// </summary>
    public required TValue Value { get; init; }

    /// <summary>
    /// The message headers, or null if no headers.
    /// </summary>
    public IReadOnlyList<Header>? Headers { get; init; }

    /// <summary>
    /// The message timestamp as raw Unix milliseconds since epoch.
    /// </summary>
    public long TimestampMs { get; init; }

    /// <summary>
    /// The message timestamp as a DateTimeOffset.
    /// Computed on demand to avoid per-message construction overhead.
    /// </summary>
    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(TimestampMs);

    /// <summary>
    /// Number of times this record has been delivered (from AcquiredRecords.DeliveryCount).
    /// First delivery = 1.
    /// </summary>
    public required int DeliveryCount { get; init; }

    /// <summary>
    /// The acknowledgement state for this record. Defaults to Accept.
    /// Updated via <see cref="IKafkaShareConsumer{TKey,TValue}.Acknowledge"/>.
    /// </summary>
    internal AcknowledgeType AcknowledgeType { get; set; } = AcknowledgeType.Accept;
}
