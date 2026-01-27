namespace Dekaf.Protocol.Messages;

/// <summary>
/// Metadata request (API key 3).
/// Used to discover cluster topology, topic partitions, and leaders.
/// </summary>
public sealed class MetadataRequest : IKafkaRequest<MetadataResponse>
{
    public static ApiKey ApiKey => ApiKey.Metadata;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 12;

    /// <summary>
    /// Topics to fetch metadata for. Null means all topics.
    /// </summary>
    public IReadOnlyList<MetadataRequestTopic>? Topics { get; init; }

    /// <summary>
    /// Whether to allow auto topic creation (v4+).
    /// </summary>
    public bool AllowAutoTopicCreation { get; init; } = true;

    /// <summary>
    /// Whether to include cluster authorized operations (v8+).
    /// </summary>
    public bool IncludeClusterAuthorizedOperations { get; init; }

    /// <summary>
    /// Whether to include topic authorized operations (v8+).
    /// </summary>
    public bool IncludeTopicAuthorizedOperations { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 9;
    public static short GetRequestHeaderVersion(short version) => version >= 9 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 9 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 9;

        if (Topics is null)
        {
            // Null array means fetch all topics
            if (isFlexible)
                writer.WriteUnsignedVarInt(0); // 0 means null for compact nullable arrays
            else if (version >= 1)
                writer.WriteInt32(-1);
            else
                writer.WriteInt32(0); // v0 doesn't support null, use empty
        }
        else if (isFlexible)
        {
            writer.WriteCompactArray(
                Topics,
                (ref KafkaProtocolWriter w, MetadataRequestTopic t) => t.Write(ref w, version));
        }
        else
        {
            writer.WriteArray(
                Topics,
                (ref KafkaProtocolWriter w, MetadataRequestTopic t) => t.Write(ref w, version));
        }

        if (version >= 4)
        {
            writer.WriteBoolean(AllowAutoTopicCreation);
        }

        if (version >= 8)
        {
            writer.WriteBoolean(IncludeClusterAuthorizedOperations);
            writer.WriteBoolean(IncludeTopicAuthorizedOperations);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }

    /// <summary>
    /// Creates a request to fetch metadata for all topics.
    /// </summary>
    public static MetadataRequest ForAllTopics() => new() { Topics = null };

    /// <summary>
    /// Creates a request to fetch metadata for specific topics.
    /// </summary>
    public static MetadataRequest ForTopics(params string[] topicNames)
    {
        // Pre-allocate array to avoid intermediate List allocation
        var topics = new MetadataRequestTopic[topicNames.Length];
        for (var i = 0; i < topicNames.Length; i++)
        {
            topics[i] = new MetadataRequestTopic { Name = topicNames[i] };
        }
        return new MetadataRequest { Topics = topics };
    }

    /// <summary>
    /// Creates a request to fetch metadata for specific topics.
    /// </summary>
    public static MetadataRequest ForTopics(IEnumerable<string> topicNames)
    {
        // Optimize for common collection types to avoid multiple enumerations
        if (topicNames is ICollection<string> collection)
        {
            var topics = new MetadataRequestTopic[collection.Count];
            var i = 0;
            foreach (var name in collection)
            {
                topics[i++] = new MetadataRequestTopic { Name = name };
            }
            return new MetadataRequest { Topics = topics };
        }

        // Fallback for arbitrary enumerables - must enumerate twice or use List
        var topicList = topicNames.Select(n => new MetadataRequestTopic { Name = n }).ToArray();
        return new MetadataRequest { Topics = topicList };
    }
}

/// <summary>
/// Topic in a metadata request.
/// </summary>
public sealed class MetadataRequestTopic
{
    /// <summary>
    /// Topic ID (v10+). Null means use name.
    /// </summary>
    public Guid? TopicId { get; init; }

    /// <summary>
    /// Topic name. Null if using topic ID.
    /// </summary>
    public string? Name { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 9;

        if (version >= 10)
        {
            writer.WriteUuid(TopicId ?? Guid.Empty);
        }

        if (isFlexible)
        {
            writer.WriteCompactNullableString(Name);
            writer.WriteEmptyTaggedFields();
        }
        else
        {
            writer.WriteString(Name);
        }
    }
}
