namespace Dekaf.Protocol.Messages;

/// <summary>
/// OffsetDelete request (API key 47).
/// Deletes committed offsets for specific partitions in a consumer group.
/// </summary>
public sealed class OffsetDeleteRequest : IKafkaRequest<OffsetDeleteResponse>
{
    public static ApiKey ApiKey => ApiKey.OffsetDelete;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The unique group identifier.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The topics to delete offsets for.
    /// </summary>
    public required IReadOnlyList<OffsetDeleteRequestTopic> Topics { get; init; }

    public static bool IsFlexibleVersion(short version) => false;
    public static short GetRequestHeaderVersion(short version) => 1;
    public static short GetResponseHeaderVersion(short version) => 0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteString(GroupId);
        writer.WriteArray(
            Topics,
            (ref KafkaProtocolWriter w, OffsetDeleteRequestTopic t) => t.Write(ref w, version));
    }
}

/// <summary>
/// Topic in an OffsetDelete request.
/// </summary>
public sealed class OffsetDeleteRequestTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The partitions to delete offsets for.
    /// </summary>
    public required IReadOnlyList<OffsetDeleteRequestPartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteString(Name);
        writer.WriteArray(
            Partitions,
            (ref KafkaProtocolWriter w, OffsetDeleteRequestPartition p) => p.Write(ref w, version));
    }
}

/// <summary>
/// Partition in an OffsetDelete request.
/// </summary>
public sealed class OffsetDeleteRequestPartition
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public required int PartitionIndex { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(PartitionIndex);
    }
}
