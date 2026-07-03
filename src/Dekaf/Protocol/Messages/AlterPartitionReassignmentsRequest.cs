namespace Dekaf.Protocol.Messages;

/// <summary>
/// AlterPartitionReassignments request (API key 45).
/// Alters or cancels partition reassignments.
/// </summary>
public sealed class AlterPartitionReassignmentsRequest : IKafkaRequest<AlterPartitionReassignmentsResponse>
{
    public static ApiKey ApiKey => ApiKey.AlterPartitionReassignments;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 60000;

    /// <summary>
    /// Whether changing replication factor is allowed (v1+).
    /// </summary>
    public bool AllowReplicationFactorChange { get; init; } = true;

    /// <summary>
    /// Topics to reassign.
    /// </summary>
    public required IReadOnlyList<AlterPartitionReassignmentsRequestTopic> Topics { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(TimeoutMs);

        if (version >= 1)
        {
            writer.WriteBoolean(AllowReplicationFactorChange);
        }

        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, AlterPartitionReassignmentsRequestTopic t, short v) => t.Write(ref w, v),
            version);

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic in an AlterPartitionReassignments request.
/// </summary>
public sealed class AlterPartitionReassignmentsRequestTopic
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Partitions to reassign.
    /// </summary>
    public required IReadOnlyList<AlterPartitionReassignmentsRequestPartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(Name);
        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, AlterPartitionReassignmentsRequestPartition p, short v) => p.Write(ref w, v),
            version);
        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Partition in an AlterPartitionReassignments request.
/// </summary>
public sealed class AlterPartitionReassignmentsRequestPartition
{
    /// <summary>
    /// Partition index.
    /// </summary>
    public int PartitionIndex { get; init; }

    /// <summary>
    /// Target replicas, or null to cancel an in-progress reassignment.
    /// </summary>
    public IReadOnlyList<int>? Replicas { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(PartitionIndex);
        writer.WriteCompactNullableArray(
            Replicas,
            static (ref KafkaProtocolWriter w, int replica) => w.WriteInt32(replica));
        writer.WriteEmptyTaggedFields();
    }
}
