namespace Dekaf.Protocol.Messages;

/// <summary>
/// ListOffsets request (API key 2).
/// Gets the earliest or latest offset for partitions.
/// </summary>
public sealed class ListOffsetsRequest : IKafkaRequest<ListOffsetsResponse>
{
    public static ApiKey ApiKey => ApiKey.ListOffsets;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 8;

    /// <summary>
    /// Broker ID of the follower, or -1 if this request is from a consumer.
    /// </summary>
    public int ReplicaId { get; init; } = -1;

    /// <summary>
    /// Isolation level for the request (v2+).
    /// </summary>
    public IsolationLevel IsolationLevel { get; init; } = IsolationLevel.ReadUncommitted;

    /// <summary>
    /// Topics with partitions to list offsets for.
    /// </summary>
    public required IReadOnlyList<ListOffsetsRequestTopic> Topics { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 6;
    public static short GetRequestHeaderVersion(short version) => version >= 6 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 6 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 6;

        writer.WriteInt32(ReplicaId);

        if (version >= 2)
        {
            writer.WriteInt8((sbyte)IsolationLevel);
        }

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Topics,
                (ref KafkaProtocolWriter w, ListOffsetsRequestTopic t) => t.Write(ref w, version));
            writer.WriteEmptyTaggedFields();
        }
        else
        {
            writer.WriteArray(
                Topics,
                (ref KafkaProtocolWriter w, ListOffsetsRequestTopic t) => t.Write(ref w, version));
        }
    }
}

/// <summary>
/// Topic in a ListOffsets request.
/// </summary>
public sealed class ListOffsetsRequestTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<ListOffsetsRequestPartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 6;

        if (isFlexible)
            writer.WriteCompactString(Name);
        else
            writer.WriteString(Name);

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Partitions,
                (ref KafkaProtocolWriter w, ListOffsetsRequestPartition p) => p.Write(ref w, version));
            writer.WriteEmptyTaggedFields();
        }
        else
        {
            writer.WriteArray(
                Partitions,
                (ref KafkaProtocolWriter w, ListOffsetsRequestPartition p) => p.Write(ref w, version));
        }
    }
}

/// <summary>
/// Partition in a ListOffsets request.
/// </summary>
public sealed class ListOffsetsRequestPartition
{
    public required int PartitionIndex { get; init; }

    /// <summary>
    /// Current leader epoch (v4+), -1 if unknown.
    /// </summary>
    public int CurrentLeaderEpoch { get; init; } = -1;

    /// <summary>
    /// Timestamp to query. Use -1 for latest, -2 for earliest.
    /// </summary>
    public required long Timestamp { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 6;

        writer.WriteInt32(PartitionIndex);

        if (version >= 4)
        {
            writer.WriteInt32(CurrentLeaderEpoch);
        }

        writer.WriteInt64(Timestamp);

        // v0 has max_num_offsets field
        if (version == 0)
        {
            writer.WriteInt32(1); // max_num_offsets
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
