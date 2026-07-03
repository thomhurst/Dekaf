namespace Dekaf.Protocol.Messages;

/// <summary>
/// AlterPartitionReassignments response (API key 45).
/// Contains top-level and per-partition reassignment results.
/// </summary>
public sealed class AlterPartitionReassignmentsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.AlterPartitionReassignments;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Whether replication-factor change was allowed (v1+).
    /// </summary>
    public bool AllowReplicationFactorChange { get; init; } = true;

    /// <summary>
    /// Top-level error code.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Top-level error message.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Per-topic results.
    /// </summary>
    public required IReadOnlyList<AlterPartitionReassignmentsResponseTopic> Responses { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var allowReplicationFactorChange = version >= 1 && reader.ReadBoolean();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();
        var responses = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => AlterPartitionReassignmentsResponseTopic.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new AlterPartitionReassignmentsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            AllowReplicationFactorChange = version >= 1 ? allowReplicationFactorChange : true,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Responses = responses
        };
    }
}

/// <summary>
/// Per-topic result for AlterPartitionReassignments.
/// </summary>
public sealed class AlterPartitionReassignmentsResponseTopic
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Per-partition results.
    /// </summary>
    public required IReadOnlyList<AlterPartitionReassignmentsResponsePartition> Partitions { get; init; }

    public static AlterPartitionReassignmentsResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var name = reader.ReadCompactString() ?? string.Empty;
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => AlterPartitionReassignmentsResponsePartition.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new AlterPartitionReassignmentsResponseTopic
        {
            Name = name,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Per-partition result for AlterPartitionReassignments.
/// </summary>
public sealed class AlterPartitionReassignmentsResponsePartition
{
    /// <summary>
    /// Partition index.
    /// </summary>
    public int PartitionIndex { get; init; }

    /// <summary>
    /// Partition error code.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Partition error message.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static AlterPartitionReassignmentsResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new AlterPartitionReassignmentsResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
