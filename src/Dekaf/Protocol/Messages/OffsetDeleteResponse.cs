namespace Dekaf.Protocol.Messages;

/// <summary>
/// OffsetDelete response (API key 47).
/// Contains the results of offset deletion requests.
/// </summary>
public sealed class OffsetDeleteResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.OffsetDelete;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The top-level error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Results for each topic.
    /// </summary>
    public required IReadOnlyList<OffsetDeleteResponseTopic> Topics { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var throttleTimeMs = reader.ReadInt32();

        var topics = reader.ReadArray(
            (ref KafkaProtocolReader r) => OffsetDeleteResponseTopic.Read(ref r, version)) ?? [];

        return new OffsetDeleteResponse
        {
            ErrorCode = errorCode,
            ThrottleTimeMs = throttleTimeMs,
            Topics = topics
        };
    }
}

/// <summary>
/// Per-topic response for OffsetDelete.
/// </summary>
public sealed class OffsetDeleteResponseTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The responses for each partition in the topic.
    /// </summary>
    public required IReadOnlyList<OffsetDeleteResponsePartition> Partitions { get; init; }

    public static OffsetDeleteResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var name = reader.ReadString() ?? string.Empty;

        var partitions = reader.ReadArray(
            (ref KafkaProtocolReader r) => OffsetDeleteResponsePartition.Read(ref r, version)) ?? [];

        return new OffsetDeleteResponseTopic
        {
            Name = name,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Per-partition response for OffsetDelete.
/// </summary>
public sealed class OffsetDeleteResponsePartition
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public required int PartitionIndex { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    public static OffsetDeleteResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        return new OffsetDeleteResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode
        };
    }
}
