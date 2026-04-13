namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeShareGroupOffsets request (API key 90).
/// Describes the offsets for share groups (KIP-932).
/// </summary>
public sealed class DescribeShareGroupOffsetsRequest : IKafkaRequest<DescribeShareGroupOffsetsResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeShareGroupOffsets;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 1;

    /// <summary>
    /// The groups to describe offsets for.
    /// </summary>
    public required IReadOnlyList<DescribeShareGroupOffsetsRequestGroup> Groups { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            Groups,
            static (ref KafkaProtocolWriter w, DescribeShareGroupOffsetsRequestGroup group) => group.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Group in a DescribeShareGroupOffsets request.
/// </summary>
public sealed class DescribeShareGroupOffsetsRequestGroup
{
    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The topics to describe offsets for. Null means all topics.
    /// </summary>
    public IReadOnlyList<DescribeShareGroupOffsetsRequestTopic>? Topics { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(GroupId);

        writer.WriteCompactNullableArray(
            Topics,
            static (ref KafkaProtocolWriter w, DescribeShareGroupOffsetsRequestTopic topic) => topic.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }

    public static DescribeShareGroupOffsetsRequestGroup Read(ref KafkaProtocolReader reader)
    {
        var groupId = reader.ReadCompactNonNullableString();

        // Topics is nullable: null = describe all topics, empty = no topics.
        // ReadCompactArray cannot distinguish null from empty, so read the length manually.
        var topicsLength = reader.ReadUnsignedVarInt() - 1;
        IReadOnlyList<DescribeShareGroupOffsetsRequestTopic>? topics = null;
        if (topicsLength >= 0)
        {
            var topicsList = new DescribeShareGroupOffsetsRequestTopic[topicsLength];
            for (int i = 0; i < topicsLength; i++)
            {
                topicsList[i] = DescribeShareGroupOffsetsRequestTopic.Read(ref reader);
            }
            topics = topicsList;
        }

        reader.SkipTaggedFields();

        return new DescribeShareGroupOffsetsRequestGroup
        {
            GroupId = groupId,
            Topics = topics
        };
    }
}

/// <summary>
/// Topic in a DescribeShareGroupOffsets request.
/// </summary>
public sealed class DescribeShareGroupOffsetsRequestTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    /// The partition indexes.
    /// </summary>
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(TopicName);

        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, int partition) => w.WriteInt32(partition));

        writer.WriteEmptyTaggedFields();
    }

    public static DescribeShareGroupOffsetsRequestTopic Read(ref KafkaProtocolReader reader)
    {
        var topicName = reader.ReadCompactNonNullableString();

        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32());

        reader.SkipTaggedFields();

        return new DescribeShareGroupOffsetsRequestTopic
        {
            TopicName = topicName,
            Partitions = partitions
        };
    }
}
