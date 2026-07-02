namespace Dekaf.Protocol.Messages;

/// <summary>
/// AddOffsetsToTxn request (API key 25).
/// Adds a consumer group's offsets to a transaction.
/// </summary>
public sealed class AddOffsetsToTxnRequest : IKafkaRequest<AddOffsetsToTxnResponse>
{
    public static ApiKey ApiKey => ApiKey.AddOffsetsToTxn;
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
    /// The consumer group ID whose offsets should be included in the transaction.
    /// </summary>
    public required string GroupId { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(TransactionalId);

        writer.WriteInt64(ProducerId);
        writer.WriteInt16(ProducerEpoch);

        writer.WriteCompactString(GroupId);

        writer.WriteEmptyTaggedFields();
    }
}
