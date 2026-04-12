namespace Dekaf.Protocol.Messages;

/// <summary>
/// EndTxn request (API key 26).
/// Commits or aborts a transaction.
/// </summary>
public sealed class EndTxnRequest : IKafkaRequest<EndTxnResponse>
{
    public static ApiKey ApiKey => ApiKey.EndTxn;
    public static short LowestSupportedVersion => 3;
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
    /// True if the transaction should be committed, false to abort.
    /// </summary>
    public bool Committed { get; init; }

    public static bool IsFlexibleVersion(short version) => true;
    public static short GetRequestHeaderVersion(short version) => 2;
    public static short GetResponseHeaderVersion(short version) => 1;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 3;

        if (isFlexible)
            writer.WriteCompactString(TransactionalId);
        else
            writer.WriteString(TransactionalId);

        writer.WriteInt64(ProducerId);
        writer.WriteInt16(ProducerEpoch);
        writer.WriteInt8(Committed ? (sbyte)1 : (sbyte)0);

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
