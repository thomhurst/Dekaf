namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeShareGroupOffsets response (API key 90).
/// Contains the offsets for share groups (KIP-932).
/// </summary>
public sealed class DescribeShareGroupOffsetsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeShareGroupOffsets;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The described groups.
    /// </summary>
    public required IReadOnlyList<DescribeShareGroupOffsetsResponseGroup> Groups { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();

        var groups = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => DescribeShareGroupOffsetsResponseGroup.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new DescribeShareGroupOffsetsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Groups = groups
        };
    }
}

/// <summary>
/// Group in a DescribeShareGroupOffsets response.
/// </summary>
public sealed class DescribeShareGroupOffsetsResponseGroup
{
    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The topics with offsets.
    /// </summary>
    public required IReadOnlyList<DescribeShareGroupOffsetsResponseTopic> Topics { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(GroupId);

        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, DescribeShareGroupOffsetsResponseTopic topic, short v) => topic.Write(ref w, v),
            version);

        writer.WriteInt16((short)ErrorCode);
        writer.WriteCompactNullableString(ErrorMessage);

        writer.WriteEmptyTaggedFields();
    }

    public static DescribeShareGroupOffsetsResponseGroup Read(ref KafkaProtocolReader reader, short version)
    {
        var groupId = reader.ReadCompactNonNullableString();

        var topics = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => DescribeShareGroupOffsetsResponseTopic.Read(ref r, v),
            version);

        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new DescribeShareGroupOffsetsResponseGroup
        {
            GroupId = groupId,
            Topics = topics,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Topic in a DescribeShareGroupOffsets response.
/// </summary>
public sealed class DescribeShareGroupOffsetsResponseTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    /// The topic ID (UUID).
    /// </summary>
    public Guid TopicId { get; init; }

    /// <summary>
    /// The partitions with offsets.
    /// </summary>
    public required IReadOnlyList<DescribeShareGroupOffsetsResponsePartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(TopicName);
        writer.WriteUuid(TopicId);

        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, DescribeShareGroupOffsetsResponsePartition partition, short v) => partition.Write(ref w, v),
            version);

        writer.WriteEmptyTaggedFields();
    }

    public static DescribeShareGroupOffsetsResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var topicName = reader.ReadCompactNonNullableString();
        var topicId = reader.ReadUuid();

        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => DescribeShareGroupOffsetsResponsePartition.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new DescribeShareGroupOffsetsResponseTopic
        {
            TopicName = topicName,
            TopicId = topicId,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition in a DescribeShareGroupOffsets response.
/// </summary>
public sealed class DescribeShareGroupOffsetsResponsePartition
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public int PartitionIndex { get; init; }

    /// <summary>
    /// The share group start offset.
    /// </summary>
    public long StartOffset { get; init; }

    /// <summary>
    /// The leader epoch.
    /// </summary>
    public int LeaderEpoch { get; init; }

    /// <summary>
    /// The lag of the share group on this partition (v1+). -1 if not available.
    /// </summary>
    public long Lag { get; init; } = -1;

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(PartitionIndex);
        writer.WriteInt64(StartOffset);
        writer.WriteInt32(LeaderEpoch);

        if (version >= 1)
        {
            writer.WriteInt64(Lag);
        }

        writer.WriteInt16((short)ErrorCode);
        writer.WriteCompactNullableString(ErrorMessage);

        writer.WriteEmptyTaggedFields();
    }

    public static DescribeShareGroupOffsetsResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var partitionIndex = reader.ReadInt32();
        var startOffset = reader.ReadInt64();
        var leaderEpoch = reader.ReadInt32();

        long lag = -1;
        if (version >= 1)
        {
            lag = reader.ReadInt64();
        }

        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new DescribeShareGroupOffsetsResponsePartition
        {
            PartitionIndex = partitionIndex,
            StartOffset = startOffset,
            LeaderEpoch = leaderEpoch,
            Lag = lag,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
