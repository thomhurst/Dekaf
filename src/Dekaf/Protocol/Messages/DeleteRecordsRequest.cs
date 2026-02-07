namespace Dekaf.Protocol.Messages;

/// <summary>
/// DeleteRecords request (API key 21).
/// Deletes records from topic partitions up to a specified offset.
/// </summary>
public sealed class DeleteRecordsRequest : IKafkaRequest<DeleteRecordsResponse>
{
    public static ApiKey ApiKey => ApiKey.DeleteRecords;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The topics with partitions and offsets to delete up to.
    /// </summary>
    public required IReadOnlyList<DeleteRecordsRequestTopic> Topics { get; init; }

    /// <summary>
    /// How long to wait in milliseconds before timing out the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    public static bool IsFlexibleVersion(short version) => version >= 2;
    public static short GetRequestHeaderVersion(short version) => version >= 2 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 2 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Topics,
                static (ref KafkaProtocolWriter w, DeleteRecordsRequestTopic t, short v) => t.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Topics,
                static (ref KafkaProtocolWriter w, DeleteRecordsRequestTopic t, short v) => t.Write(ref w, v),
                version);
        }

        writer.WriteInt32(TimeoutMs);

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Topic in a DeleteRecords request.
/// </summary>
public sealed class DeleteRecordsRequestTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The partitions with offsets to delete up to.
    /// </summary>
    public required IReadOnlyList<DeleteRecordsRequestPartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        if (isFlexible)
            writer.WriteCompactString(Name);
        else
            writer.WriteString(Name);

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Partitions,
                static (ref KafkaProtocolWriter w, DeleteRecordsRequestPartition p, short v) => p.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Partitions,
                static (ref KafkaProtocolWriter w, DeleteRecordsRequestPartition p, short v) => p.Write(ref w, v),
                version);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Partition in a DeleteRecords request.
/// </summary>
public sealed class DeleteRecordsRequestPartition
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public required int PartitionIndex { get; init; }

    /// <summary>
    /// The deletion offset. Records before this offset will be deleted.
    /// </summary>
    public required long Offset { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        writer.WriteInt32(PartitionIndex);
        writer.WriteInt64(Offset);

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
