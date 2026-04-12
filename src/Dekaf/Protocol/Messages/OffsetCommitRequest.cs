namespace Dekaf.Protocol.Messages;

/// <summary>
/// OffsetCommit request (API key 8).
/// Commits offsets for a consumer group.
/// </summary>
public sealed class OffsetCommitRequest : IKafkaRequest<OffsetCommitResponse>
{
    public static ApiKey ApiKey => ApiKey.OffsetCommit;
    public static short LowestSupportedVersion => 8;
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

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(GroupId);
        writer.WriteInt32(GenerationIdOrMemberEpoch);
        writer.WriteCompactString(MemberId ?? string.Empty);
        writer.WriteCompactNullableString(GroupInstanceId);

        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, OffsetCommitRequestTopic t, short v) => t.Write(ref w, v),
            version);

        writer.WriteEmptyTaggedFields();
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
        writer.WriteCompactString(Name);

        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, OffsetCommitRequestPartition p, short v) => p.Write(ref w, v),
            version);

        writer.WriteEmptyTaggedFields();
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
        writer.WriteInt32(PartitionIndex);
        writer.WriteInt64(CommittedOffset);
        writer.WriteInt32(CommittedLeaderEpoch);
        writer.WriteCompactNullableString(CommittedMetadata);
        writer.WriteEmptyTaggedFields();
    }
}
