namespace Dekaf.Protocol.Messages;

/// <summary>
/// OffsetCommit request (API key 8).
/// Commits offsets for a consumer group.
/// </summary>
public sealed class OffsetCommitRequest : IKafkaRequest<OffsetCommitResponse>
{
    public static ApiKey ApiKey => ApiKey.OffsetCommit;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 9;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Generation ID (v1+).
    /// </summary>
    public int GenerationIdOrMemberEpoch { get; init; } = -1;

    /// <summary>
    /// Member ID (v1+).
    /// </summary>
    public string? MemberId { get; init; }

    /// <summary>
    /// Group instance ID for static membership (v7+).
    /// </summary>
    public string? GroupInstanceId { get; init; }

    /// <summary>
    /// Retention time override (v2-v4). -1 uses broker default.
    /// </summary>
    public long RetentionTimeMs { get; init; } = -1;

    /// <summary>
    /// Topics with offsets to commit.
    /// </summary>
    public required IReadOnlyList<OffsetCommitRequestTopic> Topics { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 8;
    public static short GetRequestHeaderVersion(short version) => version >= 8 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 8 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 8;

        if (isFlexible)
            writer.WriteCompactString(GroupId);
        else
            writer.WriteString(GroupId);

        if (version >= 1)
        {
            writer.WriteInt32(GenerationIdOrMemberEpoch);

            if (isFlexible)
                writer.WriteCompactString(MemberId ?? string.Empty);
            else
                writer.WriteString(MemberId ?? string.Empty);
        }

        if (version >= 7)
        {
            if (isFlexible)
                writer.WriteCompactNullableString(GroupInstanceId);
            else
                writer.WriteString(GroupInstanceId);
        }

        if (version >= 2 && version <= 4)
        {
            writer.WriteInt64(RetentionTimeMs);
        }

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Topics,
                static (ref KafkaProtocolWriter w, OffsetCommitRequestTopic t, short v) => t.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Topics,
                static (ref KafkaProtocolWriter w, OffsetCommitRequestTopic t, short v) => t.Write(ref w, v),
                version);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Topic in an OffsetCommit request.
/// </summary>
public sealed class OffsetCommitRequestTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<OffsetCommitRequestPartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 8;

        if (isFlexible)
            writer.WriteCompactString(Name);
        else
            writer.WriteString(Name);

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Partitions,
                static (ref KafkaProtocolWriter w, OffsetCommitRequestPartition p, short v) => p.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Partitions,
                static (ref KafkaProtocolWriter w, OffsetCommitRequestPartition p, short v) => p.Write(ref w, v),
                version);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Partition in an OffsetCommit request.
/// </summary>
public sealed class OffsetCommitRequestPartition
{
    public required int PartitionIndex { get; init; }
    public required long CommittedOffset { get; init; }
    public int CommittedLeaderEpoch { get; init; } = -1;
    public long CommitTimestamp { get; init; } = -1;
    public string? CommittedMetadata { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 8;

        writer.WriteInt32(PartitionIndex);
        writer.WriteInt64(CommittedOffset);

        if (version >= 6)
        {
            writer.WriteInt32(CommittedLeaderEpoch);
        }

        if (version == 1)
        {
            writer.WriteInt64(CommitTimestamp);
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
