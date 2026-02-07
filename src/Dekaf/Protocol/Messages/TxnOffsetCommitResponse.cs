namespace Dekaf.Protocol.Messages;

/// <summary>
/// TxnOffsetCommit response (API key 28).
/// </summary>
public sealed class TxnOffsetCommitResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.TxnOffsetCommit;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 4;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The results for each topic.
    /// </summary>
    public required IReadOnlyList<TxnOffsetCommitResponseTopic> Topics { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;

        var throttleTimeMs = reader.ReadInt32();

        var topics = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => TxnOffsetCommitResponseTopic.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => TxnOffsetCommitResponseTopic.Read(ref r, version));

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new TxnOffsetCommitResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Topics = topics
        };
    }
}

/// <summary>
/// Per-topic result in a TxnOffsetCommit response.
/// </summary>
public sealed class TxnOffsetCommitResponseTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<TxnOffsetCommitResponsePartition> Partitions { get; init; }

    public static TxnOffsetCommitResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;

        var name = isFlexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;

        var partitions = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => TxnOffsetCommitResponsePartition.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => TxnOffsetCommitResponsePartition.Read(ref r, version));

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new TxnOffsetCommitResponseTopic
        {
            Name = name,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Per-partition result in a TxnOffsetCommit response.
/// </summary>
public sealed class TxnOffsetCommitResponsePartition
{
    public required int PartitionIndex { get; init; }
    public required ErrorCode ErrorCode { get; init; }

    public static TxnOffsetCommitResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;

        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new TxnOffsetCommitResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode
        };
    }
}
