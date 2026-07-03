namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeLogDirs response (API key 35).
/// </summary>
public sealed class DescribeLogDirsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeLogDirs;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Top-level error code for v3+ responses.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The log directories reported by the broker.
    /// </summary>
    public required IReadOnlyList<DescribeLogDirsResponseDir> Results { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = version >= 3 ? (ErrorCode)reader.ReadInt16() : ErrorCode.None;

        var results = version >= 2
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => DescribeLogDirsResponseDir.Read(ref r, v),
                version)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => DescribeLogDirsResponseDir.Read(ref r, v),
                version);

        if (version >= 2)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeLogDirsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            Results = results
        };
    }
}

/// <summary>
/// Log directory result in a DescribeLogDirs response.
/// </summary>
public sealed class DescribeLogDirsResponseDir
{
    public required ErrorCode ErrorCode { get; init; }
    public required string LogDir { get; init; }
    public required IReadOnlyList<DescribeLogDirsResponseTopic> Topics { get; init; }
    public long? TotalBytes { get; init; }
    public long? UsableBytes { get; init; }
    public bool? IsCordoned { get; init; }

    public static DescribeLogDirsResponseDir Read(ref KafkaProtocolReader reader, short version)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var logDir = version >= 2
            ? reader.ReadCompactString() ?? string.Empty
            : reader.ReadString() ?? string.Empty;

        var topics = version >= 2
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => DescribeLogDirsResponseTopic.Read(ref r, v),
                version)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => DescribeLogDirsResponseTopic.Read(ref r, v),
                version);

        long? totalBytes = null;
        long? usableBytes = null;
        bool? isCordoned = null;

        if (version >= 4)
        {
            totalBytes = reader.ReadInt64();
            usableBytes = reader.ReadInt64();
        }

        if (version >= 5)
        {
            isCordoned = reader.ReadBoolean();
        }

        if (version >= 2)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeLogDirsResponseDir
        {
            ErrorCode = errorCode,
            LogDir = logDir,
            Topics = topics,
            TotalBytes = totalBytes,
            UsableBytes = usableBytes,
            IsCordoned = isCordoned
        };
    }
}

/// <summary>
/// Topic in a DescribeLogDirs response.
/// </summary>
public sealed class DescribeLogDirsResponseTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<DescribeLogDirsResponsePartition> Partitions { get; init; }

    public static DescribeLogDirsResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var name = version >= 2
            ? reader.ReadCompactString() ?? string.Empty
            : reader.ReadString() ?? string.Empty;

        var partitions = version >= 2
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => DescribeLogDirsResponsePartition.Read(ref r, v),
                version)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => DescribeLogDirsResponsePartition.Read(ref r, v),
                version);

        if (version >= 2)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeLogDirsResponseTopic
        {
            Name = name,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition information in a DescribeLogDirs response.
/// </summary>
public sealed class DescribeLogDirsResponsePartition
{
    public required int PartitionIndex { get; init; }
    public required long PartitionSize { get; init; }
    public required long OffsetLag { get; init; }
    public required bool IsFutureKey { get; init; }

    public static DescribeLogDirsResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var partitionIndex = reader.ReadInt32();
        var partitionSize = reader.ReadInt64();
        var offsetLag = reader.ReadInt64();
        var isFutureKey = reader.ReadBoolean();

        if (version >= 2)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeLogDirsResponsePartition
        {
            PartitionIndex = partitionIndex,
            PartitionSize = partitionSize,
            OffsetLag = offsetLag,
            IsFutureKey = isFutureKey
        };
    }
}
