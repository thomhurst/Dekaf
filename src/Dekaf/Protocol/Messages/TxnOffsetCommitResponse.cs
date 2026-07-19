namespace Dekaf.Protocol.Messages;

/// <summary>
/// TxnOffsetCommit response (API key 28).
/// </summary>
public sealed class TxnOffsetCommitResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.TxnOffsetCommit;
    public static short LowestSupportedVersion => 3;
    public static short HighestSupportedVersion => TxnOffsetCommitRequest.TopicIdVersion;

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
        var throttleTimeMs = reader.ReadInt32();

        var topics = reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => TxnOffsetCommitResponseTopic.Read(ref r, v), version);

        reader.SkipTaggedFields();

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
    public string Name { get; init; } = string.Empty;
    public Guid TopicId { get; init; }
    public required IReadOnlyList<TxnOffsetCommitResponsePartition> Partitions { get; init; }

    public static TxnOffsetCommitResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var name = version >= TxnOffsetCommitRequest.TopicIdVersion
            ? string.Empty
            : reader.ReadCompactNonNullableString();
        var topicId = version >= TxnOffsetCommitRequest.TopicIdVersion
            ? reader.ReadUuid()
            : Guid.Empty;

        var partitions = reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => TxnOffsetCommitResponsePartition.Read(ref r, v), version);

        reader.SkipTaggedFields();

        return new TxnOffsetCommitResponseTopic
        {
            Name = name,
            TopicId = topicId,
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
        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        reader.SkipTaggedFields();

        return new TxnOffsetCommitResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode
        };
    }
}
