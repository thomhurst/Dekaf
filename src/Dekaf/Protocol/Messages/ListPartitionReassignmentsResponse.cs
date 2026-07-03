namespace Dekaf.Protocol.Messages;

/// <summary>
/// ListPartitionReassignments response (API key 46).
/// Contains in-progress partition reassignments.
/// </summary>
public sealed class ListPartitionReassignmentsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ListPartitionReassignments;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Top-level error code.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Top-level error message.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// In-progress reassignments by topic.
    /// </summary>
    public required IReadOnlyList<ListPartitionReassignmentsResponseTopic> Topics { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();
        var topics = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => ListPartitionReassignmentsResponseTopic.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new ListPartitionReassignmentsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Topics = topics
        };
    }
}

/// <summary>
/// Topic with in-progress partition reassignments.
/// </summary>
public sealed class ListPartitionReassignmentsResponseTopic
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// In-progress partition reassignments.
    /// </summary>
    public required IReadOnlyList<OngoingPartitionReassignmentData> Partitions { get; init; }

    public static ListPartitionReassignmentsResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var name = reader.ReadCompactString() ?? string.Empty;
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => OngoingPartitionReassignmentData.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new ListPartitionReassignmentsResponseTopic
        {
            Name = name,
            Partitions = partitions
        };
    }
}

/// <summary>
/// In-progress partition reassignment data.
/// </summary>
public sealed class OngoingPartitionReassignmentData
{
    /// <summary>
    /// Partition index.
    /// </summary>
    public int PartitionIndex { get; init; }

    /// <summary>
    /// Current replica set.
    /// </summary>
    public required IReadOnlyList<int> Replicas { get; init; }

    /// <summary>
    /// Replicas being added.
    /// </summary>
    public required IReadOnlyList<int> AddingReplicas { get; init; }

    /// <summary>
    /// Replicas being removed.
    /// </summary>
    public required IReadOnlyList<int> RemovingReplicas { get; init; }

    public static OngoingPartitionReassignmentData Read(ref KafkaProtocolReader reader, short version)
    {
        var partitionIndex = reader.ReadInt32();
        var replicas = reader.ReadCompactArray(static (ref KafkaProtocolReader r) => r.ReadInt32());
        var addingReplicas = reader.ReadCompactArray(static (ref KafkaProtocolReader r) => r.ReadInt32());
        var removingReplicas = reader.ReadCompactArray(static (ref KafkaProtocolReader r) => r.ReadInt32());

        reader.SkipTaggedFields();

        return new OngoingPartitionReassignmentData
        {
            PartitionIndex = partitionIndex,
            Replicas = replicas,
            AddingReplicas = addingReplicas,
            RemovingReplicas = removingReplicas
        };
    }
}
