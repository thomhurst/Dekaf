namespace Dekaf.Protocol.Messages;

/// <summary>
/// AddPartitionsToTxn request (API key 24).
/// Adds partitions to an ongoing transaction.
/// v0-v3: Flat format with TransactionalId, ProducerId, ProducerEpoch, Topics.
/// v4+: Wrapped in a Transactions array (supports batching, adds VerifyOnly field).
/// </summary>
public sealed class AddPartitionsToTxnRequest : IKafkaRequest<AddPartitionsToTxnResponse>
{
    public static ApiKey ApiKey => ApiKey.AddPartitionsToTxn;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 4;

    /// <summary>
    /// The transactional ID.
    /// </summary>
    public required string TransactionalId { get; init; }

    /// <summary>
    /// The producer ID.
    /// </summary>
    public long ProducerId { get; init; }

    /// <summary>
    /// The producer epoch.
    /// </summary>
    public short ProducerEpoch { get; init; }

    /// <summary>
    /// Whether this is a verify-only request (v4+). Defaults to false.
    /// </summary>
    public bool VerifyOnly { get; init; }

    /// <summary>
    /// The topics and partitions to add to the transaction.
    /// </summary>
    public required IReadOnlyList<AddPartitionsToTxnTopic> Topics { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 3;
    public static short GetRequestHeaderVersion(short version) => version >= 3 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 3 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (version >= 4)
        {
            // v4+: Transactions compact array containing a single transaction element
            writer.WriteUnsignedVarInt(2); // compact array length = numElements + 1 = 1 + 1 = 2

            // Transaction element
            writer.WriteCompactString(TransactionalId);
            writer.WriteInt64(ProducerId);
            writer.WriteInt16(ProducerEpoch);
            writer.WriteUInt8(VerifyOnly ? (byte)1 : (byte)0);

            // Topics compact array
            writer.WriteCompactArray(
                Topics,
                (ref KafkaProtocolWriter w, AddPartitionsToTxnTopic t) => t.Write(ref w, version));

            // Transaction element tagged fields
            writer.WriteEmptyTaggedFields();

            // Request-level tagged fields
            writer.WriteEmptyTaggedFields();
        }
        else
        {
            var isFlexible = version >= 3;

            if (isFlexible)
                writer.WriteCompactString(TransactionalId);
            else
                writer.WriteString(TransactionalId);

            writer.WriteInt64(ProducerId);
            writer.WriteInt16(ProducerEpoch);

            if (isFlexible)
            {
                writer.WriteCompactArray(
                    Topics,
                    (ref KafkaProtocolWriter w, AddPartitionsToTxnTopic t) => t.Write(ref w, version));
            }
            else
            {
                writer.WriteArray(
                    Topics,
                    (ref KafkaProtocolWriter w, AddPartitionsToTxnTopic t) => t.Write(ref w, version));
            }

            if (isFlexible)
            {
                writer.WriteEmptyTaggedFields();
            }
        }
    }
}

/// <summary>
/// Topic in an AddPartitionsToTxn request.
/// </summary>
public sealed class AddPartitionsToTxnTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        // v3+ and v4+ are both flexible
        var isFlexible = version >= 3;

        if (isFlexible)
            writer.WriteCompactString(Name);
        else
            writer.WriteString(Name);

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Partitions,
                static (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
        }
        else
        {
            writer.WriteArray(
                Partitions,
                static (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
