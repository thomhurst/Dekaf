namespace Dekaf.Protocol.Messages;

/// <summary>
/// ListPartitionReassignments request (API key 46).
/// Lists in-progress partition reassignments.
/// </summary>
public sealed class ListPartitionReassignmentsRequest : IKafkaRequest<ListPartitionReassignmentsResponse>
{
    public static ApiKey ApiKey => ApiKey.ListPartitionReassignments;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 60000;

    /// <summary>
    /// Topics and partitions to list, or null to list all reassignments.
    /// </summary>
    public IReadOnlyList<ListPartitionReassignmentsRequestTopic>? Topics { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(TimeoutMs);
        writer.WriteCompactNullableArray(
            Topics,
            static (ref KafkaProtocolWriter w, ListPartitionReassignmentsRequestTopic t, short v) => t.Write(ref w, v),
            version);
        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic in a ListPartitionReassignments request.
/// </summary>
public sealed class ListPartitionReassignmentsRequestTopic
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Partition indexes to list.
    /// </summary>
    public required IReadOnlyList<int> PartitionIndexes { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(Name);
        writer.WriteCompactArray(
            PartitionIndexes,
            static (ref KafkaProtocolWriter w, int partition) => w.WriteInt32(partition));
        writer.WriteEmptyTaggedFields();
    }
}
