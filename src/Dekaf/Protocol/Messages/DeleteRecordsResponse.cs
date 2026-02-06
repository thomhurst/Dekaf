namespace Dekaf.Protocol.Messages;

/// <summary>
/// DeleteRecords response (API key 21).
/// Contains the results of record deletion requests.
/// </summary>
public sealed class DeleteRecordsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DeleteRecords;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Results for each topic.
    /// </summary>
    public required IReadOnlyList<DeleteRecordsResponseTopic> Topics { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var throttleTimeMs = reader.ReadInt32();

        IReadOnlyList<DeleteRecordsResponseTopic> topics;
        if (isFlexible)
        {
            topics = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => DeleteRecordsResponseTopic.Read(ref r, version)) ?? [];
        }
        else
        {
            topics = reader.ReadArray(
                (ref KafkaProtocolReader r) => DeleteRecordsResponseTopic.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DeleteRecordsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Topics = topics
        };
    }
}

/// <summary>
/// Per-topic result for DeleteRecords.
/// </summary>
public sealed class DeleteRecordsResponseTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Results for each partition.
    /// </summary>
    public required IReadOnlyList<DeleteRecordsResponsePartition> Partitions { get; init; }

    public static DeleteRecordsResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var name = isFlexible
            ? reader.ReadCompactString() ?? string.Empty
            : reader.ReadString() ?? string.Empty;

        IReadOnlyList<DeleteRecordsResponsePartition> partitions;
        if (isFlexible)
        {
            partitions = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => DeleteRecordsResponsePartition.Read(ref r, version)) ?? [];
        }
        else
        {
            partitions = reader.ReadArray(
                (ref KafkaProtocolReader r) => DeleteRecordsResponsePartition.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DeleteRecordsResponseTopic
        {
            Name = name,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Per-partition result for DeleteRecords.
/// </summary>
public sealed class DeleteRecordsResponsePartition
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public required int PartitionIndex { get; init; }

    /// <summary>
    /// The partition low watermark after the delete.
    /// </summary>
    public long LowWatermark { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    public static DeleteRecordsResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var partitionIndex = reader.ReadInt32();
        var lowWatermark = reader.ReadInt64();
        var errorCode = (ErrorCode)reader.ReadInt16();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DeleteRecordsResponsePartition
        {
            PartitionIndex = partitionIndex,
            LowWatermark = lowWatermark,
            ErrorCode = errorCode
        };
    }
}
