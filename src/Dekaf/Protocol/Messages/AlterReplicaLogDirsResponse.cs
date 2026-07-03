namespace Dekaf.Protocol.Messages;

/// <summary>
/// AlterReplicaLogDirs response (API key 34).
/// </summary>
public sealed class AlterReplicaLogDirsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.AlterReplicaLogDirs;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Results for each topic.
    /// </summary>
    public required IReadOnlyList<AlterReplicaLogDirsResponseTopic> Results { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var results = version >= 2
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => AlterReplicaLogDirsResponseTopic.Read(ref r, v),
                version)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => AlterReplicaLogDirsResponseTopic.Read(ref r, v),
                version);

        if (version >= 2)
        {
            reader.SkipTaggedFields();
        }

        return new AlterReplicaLogDirsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Results = results
        };
    }
}

/// <summary>
/// Topic result in an AlterReplicaLogDirs response.
/// </summary>
public sealed class AlterReplicaLogDirsResponseTopic
{
    public required string TopicName { get; init; }
    public required IReadOnlyList<AlterReplicaLogDirsResponsePartition> Partitions { get; init; }

    public static AlterReplicaLogDirsResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var topicName = version >= 2
            ? reader.ReadCompactString() ?? string.Empty
            : reader.ReadString() ?? string.Empty;

        var partitions = version >= 2
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => AlterReplicaLogDirsResponsePartition.Read(ref r, v),
                version)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => AlterReplicaLogDirsResponsePartition.Read(ref r, v),
                version);

        if (version >= 2)
        {
            reader.SkipTaggedFields();
        }

        return new AlterReplicaLogDirsResponseTopic
        {
            TopicName = topicName,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition result in an AlterReplicaLogDirs response.
/// </summary>
public sealed class AlterReplicaLogDirsResponsePartition
{
    public required int PartitionIndex { get; init; }
    public required ErrorCode ErrorCode { get; init; }

    public static AlterReplicaLogDirsResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        if (version >= 2)
        {
            reader.SkipTaggedFields();
        }

        return new AlterReplicaLogDirsResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode
        };
    }
}
