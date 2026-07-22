using System.Buffers;
using Dekaf.Serialization;

namespace Dekaf.Outbox;

/// <summary>
/// A single outbox row: one Kafka record captured in the application's database transaction,
/// awaiting publication by the outbox relay.
/// </summary>
/// <remarks>
/// <para>Key and value are stored pre-serialized. Serialization happens once, on the enqueue
/// side inside the application's own transaction; the relay is a byte pass-through and never
/// re-serializes.</para>
/// <para>The class is not sealed so stores can attach a native storage identifier
/// (a document id, a sort key, an etag) on a subclass: the relay hands the same instances
/// returned by <see cref="IOutboxStore.GetNextBatchAsync"/> back to
/// <see cref="IOutboxStore.MarkPublishedAsync"/>, so a subclassing store can downcast to
/// recover its identifier.</para>
/// </remarks>
public class OutboxMessage
{
    /// <summary>
    /// Ordering value used by relational stores (typically a database identity column):
    /// unique and ascending within the outbox table, determining publish order within a
    /// bucket. Stores that maintain enqueue order by other means (a time-ordered document
    /// id, a sequence field on a subclass) may leave this zero - the relay never reads it.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Stable unique identifier assigned at enqueue time. Stamped onto the published record
    /// as a header so consumers can deduplicate redeliveries (at-least-once semantics).
    /// </summary>
    public required Guid MessageId { get; init; }

    /// <summary>
    /// Ordering bucket computed from the record key via <see cref="OutboxBucket.Compute(byte[]?, Guid, int)"/>.
    /// Each bucket has a single publishing relay at any time, so records that share a key
    /// (and therefore a bucket) are published in enqueue order.
    /// </summary>
    public required int Bucket { get; init; }

    /// <summary>
    /// The topic to publish to.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Pre-serialized record key, or null for a keyless record.
    /// </summary>
    public byte[]? Key { get; init; }

    /// <summary>
    /// Pre-serialized record value, or null for a tombstone.
    /// </summary>
    public byte[]? Value { get; init; }

    /// <summary>
    /// Record headers serialized with <see cref="OutboxHeaderCodec"/>, or null for none.
    /// </summary>
    public byte[]? Headers { get; init; }

    /// <summary>
    /// Optional explicit partition. When null the producer's partitioner chooses from the key.
    /// </summary>
    public int? Partition { get; init; }

    /// <summary>
    /// Enqueue time. Deliberately not used as the published record's timestamp: a record
    /// stamped with an old enqueue time after a long backlog could land already past a
    /// CreateTime topic's retention and be deleted before consumers read it. Enqueuers
    /// that need the enqueue time downstream should carry it as a record header.
    /// </summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// Creates an outbox message by serializing the key and value with Dekaf serializers.
    /// Call this inside the same database transaction as the business write, then hand the
    /// result to the outbox store (e.g. <c>DbContext.AddOutboxMessage</c>).
    /// </summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <param name="topic">The topic to publish to.</param>
    /// <param name="key">The record key; null produces a keyless record.</param>
    /// <param name="value">The record value; null produces a tombstone.</param>
    /// <param name="keySerializer">Serializer for the key.</param>
    /// <param name="valueSerializer">Serializer for the value.</param>
    /// <param name="headers">Optional record headers.</param>
    /// <param name="bucketCount">Bucket count; must match <see cref="OutboxRelayOptions.BucketCount"/> on the relay.</param>
    /// <param name="partition">Optional explicit partition.</param>
    public static OutboxMessage Create<TKey, TValue>(
        string topic,
        TKey? key,
        TValue? value,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        Headers? headers = null,
        int bucketCount = OutboxRelayOptions.DefaultBucketCount,
        int? partition = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(keySerializer);
        ArgumentNullException.ThrowIfNull(valueSerializer);
        ArgumentOutOfRangeException.ThrowIfLessThan(bucketCount, 1);

        var messageId = Guid.NewGuid();
        var keyBytes = SerializeComponent(topic, key, keySerializer, headers, SerializationComponent.Key);
        var valueBytes = SerializeComponent(topic, value, valueSerializer, headers, SerializationComponent.Value);

        return new OutboxMessage
        {
            MessageId = messageId,
            Bucket = OutboxBucket.Compute(keyBytes, messageId, bucketCount, partition),
            Topic = topic,
            Key = keyBytes,
            Value = valueBytes,
            Headers = OutboxHeaderCodec.Encode(headers),
            Partition = partition,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static byte[]? SerializeComponent<T>(
        string topic, T? value, ISerializer<T> serializer, Headers? headers, SerializationComponent component)
    {
        if (value is null)
            return null;

        var buffer = new ArrayBufferWriter<byte>();
        var context = new SerializationContext
        {
            Topic = topic,
            Component = component,
            Headers = headers
        };
        serializer.Serialize(value, ref buffer, context);
        return buffer.WrittenSpan.ToArray();
    }
}
