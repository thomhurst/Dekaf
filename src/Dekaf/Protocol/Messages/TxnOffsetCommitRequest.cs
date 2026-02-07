namespace Dekaf.Protocol.Messages;

/// <summary>
/// TxnOffsetCommit request (API key 28).
/// Commits offsets within a transaction.
/// </summary>
public sealed class TxnOffsetCommitRequest : IKafkaRequest<TxnOffsetCommitResponse>
{
    public static ApiKey ApiKey => ApiKey.TxnOffsetCommit;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 4;

    /// <summary>
    /// The transactional ID.
    /// </summary>
    public required string TransactionalId { get; init; }

    /// <summary>
    /// The consumer group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The producer ID.
    /// </summary>
    public long ProducerId { get; init; }

    /// <summary>
    /// The producer epoch.
    /// </summary>
    public short ProducerEpoch { get; init; }

    /// <summary>
    /// The generation ID of the consumer group (v3+). Default -1.
    /// </summary>
    public int GenerationId { get; init; } = -1;

    /// <summary>
    /// The member ID of the consumer (v3+).
    /// </summary>
    public string MemberId { get; init; } = string.Empty;

    /// <summary>
    /// The group instance ID for static membership (v3+).
    /// </summary>
    public string? GroupInstanceId { get; init; }

    /// <summary>
    /// The topics and partitions with offsets to commit.
    /// </summary>
    public required IReadOnlyList<TxnOffsetCommitRequestTopic> Topics { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 3;
    public static short GetRequestHeaderVersion(short version) => version >= 3 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 3 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 3;

        if (isFlexible)
            writer.WriteCompactString(TransactionalId);
        else
            writer.WriteString(TransactionalId);

        if (isFlexible)
            writer.WriteCompactString(GroupId);
        else
            writer.WriteString(GroupId);

        writer.WriteInt64(ProducerId);
        writer.WriteInt16(ProducerEpoch);

        if (version >= 3)
        {
            writer.WriteInt32(GenerationId);
            writer.WriteCompactString(MemberId);
            writer.WriteCompactNullableString(GroupInstanceId);
        }

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Topics,
                static (ref KafkaProtocolWriter w, TxnOffsetCommitRequestTopic t, short v) => t.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Topics,
                static (ref KafkaProtocolWriter w, TxnOffsetCommitRequestTopic t, short v) => t.Write(ref w, v),
                version);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Topic in a TxnOffsetCommit request.
/// </summary>
public sealed class TxnOffsetCommitRequestTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<TxnOffsetCommitRequestPartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 3;

        if (isFlexible)
            writer.WriteCompactString(Name);
        else
            writer.WriteString(Name);

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Partitions,
                static (ref KafkaProtocolWriter w, TxnOffsetCommitRequestPartition p, short v) => p.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Partitions,
                static (ref KafkaProtocolWriter w, TxnOffsetCommitRequestPartition p, short v) => p.Write(ref w, v),
                version);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Partition in a TxnOffsetCommit request.
/// </summary>
public sealed class TxnOffsetCommitRequestPartition
{
    public required int PartitionIndex { get; init; }
    public required long CommittedOffset { get; init; }

    /// <summary>
    /// The committed leader epoch (v2+). Default -1.
    /// </summary>
    public int CommittedLeaderEpoch { get; init; } = -1;

    /// <summary>
    /// Optional metadata for the committed offset.
    /// </summary>
    public string? CommittedMetadata { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 3;

        writer.WriteInt32(PartitionIndex);
        writer.WriteInt64(CommittedOffset);

        if (version >= 2)
        {
            writer.WriteInt32(CommittedLeaderEpoch);
        }

        if (isFlexible)
            writer.WriteCompactNullableString(CommittedMetadata);
        else
            writer.WriteString(CommittedMetadata);

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
