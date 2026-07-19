namespace Dekaf.Protocol.Messages;

/// <summary>
/// OffsetCommit response (API key 8).
/// </summary>
public sealed class OffsetCommitResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.OffsetCommit;
    public static short LowestSupportedVersion => 8;
    public static short HighestSupportedVersion => OffsetCommitRequest.TopicIdVersion;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Topics with commit results.
    /// </summary>
    public required IReadOnlyList<OffsetCommitResponseTopic> Topics { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var topics = reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => OffsetCommitResponseTopic.Read(ref r, v), version);

        reader.SkipTaggedFields();

        return new OffsetCommitResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Topics = topics
        };
    }
}

/// <summary>
/// Topic in an OffsetCommit response.
/// </summary>
public sealed class OffsetCommitResponseTopic
{
    public string Name { get; init; } = string.Empty;
    public Guid TopicId { get; init; }
    public required IReadOnlyList<OffsetCommitResponsePartition> Partitions { get; init; }

    public static OffsetCommitResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var name = version >= OffsetCommitRequest.TopicIdVersion
            ? string.Empty
            : reader.ReadCompactNonNullableString();
        var topicId = version >= OffsetCommitRequest.TopicIdVersion ? reader.ReadUuid() : Guid.Empty;
        var partitions = reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => OffsetCommitResponsePartition.Read(ref r, v), version);

        reader.SkipTaggedFields();

        return new OffsetCommitResponseTopic
        {
            Name = name,
            TopicId = topicId,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition in an OffsetCommit response.
/// </summary>
public sealed class OffsetCommitResponsePartition
{
    public required int PartitionIndex { get; init; }
    public required ErrorCode ErrorCode { get; init; }

    public static OffsetCommitResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        reader.SkipTaggedFields();

        return new OffsetCommitResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode
        };
    }
}
