namespace Dekaf.Protocol.Messages;

/// <summary>
/// OffsetCommit response (API key 8).
/// </summary>
public sealed class OffsetCommitResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.OffsetCommit;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 9;

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
        var isFlexible = version >= 8;

        var throttleTimeMs = version >= 3 ? reader.ReadInt32() : 0;

        var topics = isFlexible
            ? reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => OffsetCommitResponseTopic.Read(ref r, v), version)
            : reader.ReadArray(static (ref KafkaProtocolReader r, short v) => OffsetCommitResponseTopic.Read(ref r, v), version);

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

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
    public required string Name { get; init; }
    public required IReadOnlyList<OffsetCommitResponsePartition> Partitions { get; init; }

    public static OffsetCommitResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 8;

        var name = isFlexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;

        var partitions = isFlexible
            ? reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => OffsetCommitResponsePartition.Read(ref r, v), version)
            : reader.ReadArray(static (ref KafkaProtocolReader r, short v) => OffsetCommitResponsePartition.Read(ref r, v), version);

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new OffsetCommitResponseTopic
        {
            Name = name,
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
        var isFlexible = version >= 8;

        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new OffsetCommitResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode
        };
    }
}
