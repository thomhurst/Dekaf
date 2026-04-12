namespace Dekaf.Protocol.Messages;

/// <summary>
/// Metadata request (API key 3).
/// Used to discover cluster topology, topic partitions, and leaders.
/// </summary>
public sealed class MetadataRequest : IKafkaRequest<MetadataResponse>
{
    public static ApiKey ApiKey => ApiKey.Metadata;
    public static short LowestSupportedVersion => 9;
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

    public static bool IsFlexibleVersion(short version) => true;
    public static short GetRequestHeaderVersion(short version) => 2;
    public static short GetResponseHeaderVersion(short version) => 1;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (Topics is null)
        {
            // Null array means fetch all topics
            writer.WriteUnsignedVarInt(0); // 0 means null for compact nullable arrays
        }
        else
        {
            writer.WriteCompactArray(
                Topics,
                static (ref KafkaProtocolWriter w, MetadataRequestTopic t, short v) => t.Write(ref w, v),
                version);
        }

        writer.WriteBoolean(AllowAutoTopicCreation);
        writer.WriteBoolean(IncludeClusterAuthorizedOperations);
        writer.WriteBoolean(IncludeTopicAuthorizedOperations);

        writer.WriteEmptyTaggedFields();
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
        if (version >= 10)
        {
            writer.WriteUuid(TopicId ?? Guid.Empty);
        }

        writer.WriteCompactNullableString(Name);
        writer.WriteEmptyTaggedFields();
    }
}
