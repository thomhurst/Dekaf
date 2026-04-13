namespace Dekaf.Protocol.Messages;

/// <summary>
/// ShareAcknowledge request (API key 79).
/// Acknowledges delivery of records fetched from a share group,
/// indicating whether each batch was accepted, released, or rejected (KIP-932).
/// </summary>
public sealed class ShareAcknowledgeRequest : IKafkaRequest<ShareAcknowledgeResponse>
{
    public static ApiKey ApiKey => ApiKey.ShareAcknowledge;
    // v0 was removed after Kafka 4.0 early access; v1 is the minimum GA version (KIP-932).
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The member ID.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// The current share session epoch.
    /// </summary>
    public int ShareSessionEpoch { get; init; }

    /// <summary>
    /// Whether this is a renew acknowledgement (v2+ only).
    /// When true, the broker treats the acknowledgement as a renewal of previously
    /// sent acknowledgements rather than new ones.
    /// </summary>
    public bool IsRenewAck { get; init; }

    /// <summary>
    /// The topics containing the partitions to acknowledge.
    /// </summary>
    public required IReadOnlyList<ShareAcknowledgeTopic> Topics { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(GroupId);
        writer.WriteCompactString(MemberId);
        writer.WriteInt32(ShareSessionEpoch);

        if (version >= 2)
        {
            writer.WriteInt8((sbyte)(IsRenewAck ? 1 : 0));
        }

        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, ShareAcknowledgeTopic tp) => tp.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic data in a ShareAcknowledge request.
/// </summary>
public sealed class ShareAcknowledgeTopic
{
    /// <summary>
    /// The topic ID (UUID).
    /// </summary>
    public required Guid TopicId { get; init; }

    /// <summary>
    /// The partitions containing acknowledgement batches.
    /// </summary>
    public required IReadOnlyList<ShareAcknowledgePartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteUuid(TopicId);
        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, ShareAcknowledgePartition p) => p.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }

    public static ShareAcknowledgeTopic Read(ref KafkaProtocolReader reader)
    {
        var topicId = reader.ReadUuid();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ShareAcknowledgePartition.Read(ref r));

        reader.SkipTaggedFields();

        return new ShareAcknowledgeTopic
        {
            TopicId = topicId,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition data in a ShareAcknowledge request.
/// </summary>
public sealed class ShareAcknowledgePartition
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public int PartitionIndex { get; init; }

    /// <summary>
    /// The acknowledgement batches for this partition.
    /// </summary>
    public required IReadOnlyList<ShareAcknowledgeBatch> AcknowledgementBatches { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(PartitionIndex);
        writer.WriteCompactArray(
            AcknowledgementBatches,
            static (ref KafkaProtocolWriter w, ShareAcknowledgeBatch b) => b.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }

    public static ShareAcknowledgePartition Read(ref KafkaProtocolReader reader)
    {
        var partitionIndex = reader.ReadInt32();
        var acknowledgementBatches = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => ShareAcknowledgeBatch.Read(ref r));

        reader.SkipTaggedFields();

        return new ShareAcknowledgePartition
        {
            PartitionIndex = partitionIndex,
            AcknowledgementBatches = acknowledgementBatches
        };
    }
}

/// <summary>
/// An acknowledgement batch in a ShareAcknowledge request.
/// Specifies an offset range and the acknowledge type for each offset within that range.
/// </summary>
public sealed class ShareAcknowledgeBatch
{
    /// <summary>
    /// The first offset in this acknowledgement batch.
    /// </summary>
    public long FirstOffset { get; init; }

    /// <summary>
    /// The last offset in this acknowledgement batch.
    /// </summary>
    public long LastOffset { get; init; }

    /// <summary>
    /// The acknowledge types for each offset in the range.
    /// Each byte represents the acknowledge type for the corresponding offset.
    /// </summary>
    public required IReadOnlyList<byte> AcknowledgeTypes { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt64(FirstOffset);
        writer.WriteInt64(LastOffset);
        writer.WriteCompactArray(
            AcknowledgeTypes,
            static (ref KafkaProtocolWriter w, byte ackType) => w.WriteInt8((sbyte)ackType));
        writer.WriteEmptyTaggedFields();
    }

    public static ShareAcknowledgeBatch Read(ref KafkaProtocolReader reader)
    {
        var firstOffset = reader.ReadInt64();
        var lastOffset = reader.ReadInt64();
        var acknowledgeTypes = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => (byte)r.ReadInt8());

        reader.SkipTaggedFields();

        return new ShareAcknowledgeBatch
        {
            FirstOffset = firstOffset,
            LastOffset = lastOffset,
            AcknowledgeTypes = acknowledgeTypes
        };
    }
}
