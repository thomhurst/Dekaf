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

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(TransactionalId);

        writer.WriteInt64(ProducerId);
        writer.WriteInt16(ProducerEpoch);
        writer.WriteInt8(Committed ? (sbyte)1 : (sbyte)0);

        writer.WriteEmptyTaggedFields();
    }
}
