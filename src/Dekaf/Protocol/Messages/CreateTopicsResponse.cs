namespace Dekaf.Protocol.Messages;

/// <summary>
/// CreateTopics response (API key 19).
/// Contains the results of topic creation requests.
/// </summary>
public sealed class CreateTopicsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.CreateTopics;
    public static short LowestSupportedVersion => 5;
    public static short HighestSupportedVersion => 7;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (v2+, zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Results for each topic.
    /// </summary>
    public required IReadOnlyList<CreateTopicsResponseTopic> Topics { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();

        IReadOnlyList<CreateTopicsResponseTopic> topics;
        topics = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => CreateTopicsResponseTopic.Read(ref r, version)) ?? [];

        reader.SkipTaggedFields();

        return new CreateTopicsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Topics = topics
        };
    }
}

/// <summary>
/// Per-topic response for CreateTopics.
/// </summary>
public sealed class CreateTopicsResponseTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The topic ID (v7+).
    /// </summary>
    public Guid TopicId { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error (v1+).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of partitions of the topic (v5+).
    /// </summary>
    public int NumPartitions { get; init; } = -1;

    /// <summary>
    /// Replication factor of the topic (v5+).
    /// </summary>
    public short ReplicationFactor { get; init; } = -1;

    /// <summary>
    /// Configuration entries (v5+).
    /// </summary>
    public IReadOnlyList<CreateTopicsResponseConfig>? Configs { get; init; }

    public static CreateTopicsResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var name = reader.ReadCompactString() ?? string.Empty;

        var topicId = version >= 7 ? reader.ReadUuid() : Guid.Empty;

        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage = null;
        errorMessage = reader.ReadCompactString();

        var numPartitions = reader.ReadInt32();
        var replicationFactor = reader.ReadInt16();

        IReadOnlyList<CreateTopicsResponseConfig>? configs = null;
        configs = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => CreateTopicsResponseConfig.Read(ref r, version));

        reader.SkipTaggedFields();

        return new CreateTopicsResponseTopic
        {
            Name = name,
            TopicId = topicId,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            NumPartitions = numPartitions,
            ReplicationFactor = replicationFactor,
            Configs = configs
        };
    }
}

/// <summary>
/// Configuration entry in CreateTopics response.
/// </summary>
public sealed class CreateTopicsResponseConfig
{
    /// <summary>
    /// The configuration name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The configuration value.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// True if the configuration is read-only.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// The configuration source (v5+).
    /// </summary>
    public sbyte ConfigSource { get; init; } = -1;

    /// <summary>
    /// True if the config is sensitive.
    /// </summary>
    public bool IsSensitive { get; init; }

    public static CreateTopicsResponseConfig Read(ref KafkaProtocolReader reader, short version)
    {
        var name = reader.ReadCompactString() ?? string.Empty;

        var value = reader.ReadCompactString();

        var readOnly = reader.ReadBoolean();
        var configSource = reader.ReadInt8();
        var isSensitive = reader.ReadBoolean();

        reader.SkipTaggedFields();

        return new CreateTopicsResponseConfig
        {
            Name = name,
            Value = value,
            ReadOnly = readOnly,
            ConfigSource = configSource,
            IsSensitive = isSensitive
        };
    }
}
