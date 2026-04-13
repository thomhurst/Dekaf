namespace Dekaf.Protocol.Messages;

/// <summary>
/// AlterShareGroupOffsets request (API key 91).
/// Alters the offsets for a share group (KIP-932).
/// </summary>
public sealed class AlterShareGroupOffsetsRequest : IKafkaRequest<AlterShareGroupOffsetsResponse>
{
    public static ApiKey ApiKey => ApiKey.AlterShareGroupOffsets;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The topics to alter offsets for.
    /// </summary>
    public required IReadOnlyList<AlterShareGroupOffsetsRequestTopic> Topics { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(GroupId);

        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, AlterShareGroupOffsetsRequestTopic topic) => topic.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic in an AlterShareGroupOffsets request.
/// </summary>
public sealed class AlterShareGroupOffsetsRequestTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    /// The partitions to alter offsets for.
    /// </summary>
    public required IReadOnlyList<AlterShareGroupOffsetsRequestPartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(TopicName);

        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, AlterShareGroupOffsetsRequestPartition partition) => partition.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }

    public static AlterShareGroupOffsetsRequestTopic Read(ref KafkaProtocolReader reader)
    {
        var topicName = reader.ReadCompactNonNullableString();

        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => AlterShareGroupOffsetsRequestPartition.Read(ref r));

        reader.SkipTaggedFields();

        return new AlterShareGroupOffsetsRequestTopic
        {
            TopicName = topicName,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition in an AlterShareGroupOffsets request.
/// </summary>
public sealed class AlterShareGroupOffsetsRequestPartition
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public int PartitionIndex { get; init; }

    /// <summary>
    /// The start offset to set for the share group on this partition.
    /// </summary>
    public long StartOffset { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(PartitionIndex);
        writer.WriteInt64(StartOffset);

        writer.WriteEmptyTaggedFields();
    }

    public static AlterShareGroupOffsetsRequestPartition Read(ref KafkaProtocolReader reader)
    {
        var partitionIndex = reader.ReadInt32();
        var startOffset = reader.ReadInt64();

        reader.SkipTaggedFields();

        return new AlterShareGroupOffsetsRequestPartition
        {
            PartitionIndex = partitionIndex,
            StartOffset = startOffset
        };
    }
}
