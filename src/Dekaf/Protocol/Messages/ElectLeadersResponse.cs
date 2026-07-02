namespace Dekaf.Protocol.Messages;

/// <summary>
/// ElectLeaders response (API key 43).
/// Contains results of leader election requests.
/// </summary>
public sealed class ElectLeadersResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ElectLeaders;
    public static short LowestSupportedVersion => 2;
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
        var throttleTimeMs = reader.ReadInt32();

        // ErrorCode is only in v1+
        var errorCode = (ErrorCode)reader.ReadInt16();

        IReadOnlyList<ElectLeadersResponseTopic> results;
        results = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => ElectLeadersResponseTopic.Read(ref r, version)) ?? [];
        reader.SkipTaggedFields();

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
        var topic = reader.ReadCompactString() ?? string.Empty;

        IReadOnlyList<ElectLeadersResponsePartition> partitions;
        partitions = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => ElectLeadersResponsePartition.Read(ref r, version)) ?? [];
        reader.SkipTaggedFields();

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
        var partitionId = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage = null;
        errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new ElectLeadersResponsePartition
        {
            PartitionId = partitionId,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
