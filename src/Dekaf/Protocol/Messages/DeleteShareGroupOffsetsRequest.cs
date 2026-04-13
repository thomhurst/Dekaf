namespace Dekaf.Protocol.Messages;

/// <summary>
/// DeleteShareGroupOffsets request (API key 92).
/// Deletes the offsets for a share group (KIP-932).
/// </summary>
public sealed class DeleteShareGroupOffsetsRequest : IKafkaRequest<DeleteShareGroupOffsetsResponse>
{
    public static ApiKey ApiKey => ApiKey.DeleteShareGroupOffsets;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The topics to delete offsets for.
    /// </summary>
    public required IReadOnlyList<DeleteShareGroupOffsetsRequestTopic> Topics { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(GroupId);

        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, DeleteShareGroupOffsetsRequestTopic topic) => topic.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic in a DeleteShareGroupOffsets request.
/// No partition list — deletes all partitions for each topic.
/// </summary>
public sealed class DeleteShareGroupOffsetsRequestTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string TopicName { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(TopicName);

        writer.WriteEmptyTaggedFields();
    }

    public static DeleteShareGroupOffsetsRequestTopic Read(ref KafkaProtocolReader reader)
    {
        var topicName = reader.ReadCompactNonNullableString();

        reader.SkipTaggedFields();

        return new DeleteShareGroupOffsetsRequestTopic
        {
            TopicName = topicName
        };
    }
}
