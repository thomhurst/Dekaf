namespace Dekaf.Protocol.Messages;

/// <summary>
/// DeleteTopics request (API key 20).
/// Deletes one or more topics.
/// </summary>
public sealed class DeleteTopicsRequest : IKafkaRequest<DeleteTopicsResponse>
{
    public static ApiKey ApiKey => ApiKey.DeleteTopics;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 6;

    /// <summary>
    /// The names of the topics to delete (v0-v5).
    /// In v6+, use Topics instead.
    /// </summary>
    public IReadOnlyList<string>? TopicNames { get; init; }

    /// <summary>
    /// The topics to delete (v6+).
    /// </summary>
    public IReadOnlyList<DeleteTopicState>? Topics { get; init; }

    /// <summary>
    /// How long to wait in milliseconds before timing out the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    public static bool IsFlexibleVersion(short version) => version >= 4;
    public static short GetRequestHeaderVersion(short version) => version >= 4 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 4 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 4;

        if (version >= 6)
        {
            // v6+: Use Topics array
            var topics = Topics ?? [];
            writer.WriteCompactArray(
                topics.ToArray().AsSpan(),
                (ref KafkaProtocolWriter w, DeleteTopicState t) => t.Write(ref w, version));
        }
        else
        {
            // v0-v5: Use TopicNames array
            var topicNames = TopicNames ?? [];
            if (isFlexible)
            {
                writer.WriteCompactArray(
                    topicNames.ToArray().AsSpan(),
                    (ref KafkaProtocolWriter w, string name) => w.WriteCompactString(name));
            }
            else
            {
                writer.WriteArray(
                    topicNames.ToArray().AsSpan(),
                    (ref KafkaProtocolWriter w, string name) => w.WriteString(name));
            }
        }

        writer.WriteInt32(TimeoutMs);

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Topic to delete (v6+).
/// </summary>
public sealed class DeleteTopicState
{
    /// <summary>
    /// The topic name (null if deleting by ID).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The topic ID (null UUID if deleting by name).
    /// </summary>
    public Guid TopicId { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactNullableString(Name);
        writer.WriteUuid(TopicId);
        writer.WriteEmptyTaggedFields();
    }
}
