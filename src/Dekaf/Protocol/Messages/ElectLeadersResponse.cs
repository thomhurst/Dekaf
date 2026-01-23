namespace Dekaf.Protocol.Messages;

/// <summary>
/// ElectLeaders response (API key 43).
/// Contains results of leader election requests.
/// </summary>
public sealed class ElectLeadersResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ElectLeaders;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Top-level error code.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Results for each topic.
    /// </summary>
    public required IReadOnlyList<ElectLeadersResponseTopic> ReplicaElectionResults { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var throttleTimeMs = reader.ReadInt32();

        // ErrorCode is only in v1+
        var errorCode = version >= 1 ? (ErrorCode)reader.ReadInt16() : ErrorCode.None;

        IReadOnlyList<ElectLeadersResponseTopic> results;
        if (isFlexible)
        {
            results = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => ElectLeadersResponseTopic.Read(ref r, version)) ?? [];
            reader.SkipTaggedFields();
        }
        else
        {
            results = reader.ReadArray(
                (ref KafkaProtocolReader r) => ElectLeadersResponseTopic.Read(ref r, version)) ?? [];
        }

        return new ElectLeadersResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ReplicaElectionResults = results
        };
    }
}

/// <summary>
/// Per-topic results in an ElectLeaders response.
/// </summary>
public sealed class ElectLeadersResponseTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Results for each partition.
    /// </summary>
    public required IReadOnlyList<ElectLeadersResponsePartition> PartitionResult { get; init; }

    public static ElectLeadersResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var topic = isFlexible
            ? reader.ReadCompactString() ?? string.Empty
            : reader.ReadString() ?? string.Empty;

        IReadOnlyList<ElectLeadersResponsePartition> partitions;
        if (isFlexible)
        {
            partitions = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => ElectLeadersResponsePartition.Read(ref r, version)) ?? [];
            reader.SkipTaggedFields();
        }
        else
        {
            partitions = reader.ReadArray(
                (ref KafkaProtocolReader r) => ElectLeadersResponsePartition.Read(ref r, version)) ?? [];
        }

        return new ElectLeadersResponseTopic
        {
            Topic = topic,
            PartitionResult = partitions
        };
    }
}

/// <summary>
/// Per-partition results in an ElectLeaders response.
/// </summary>
public sealed class ElectLeadersResponsePartition
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public int PartitionId { get; init; }

    /// <summary>
    /// Error code for this partition.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Error message (v1+).
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static ElectLeadersResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var partitionId = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage = null;
        if (version >= 1)
        {
            errorMessage = isFlexible
                ? reader.ReadCompactString()
                : reader.ReadString();
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new ElectLeadersResponsePartition
        {
            PartitionId = partitionId,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
