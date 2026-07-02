namespace Dekaf.Protocol.Messages;

/// <summary>
/// CreateTopics request (API key 19).
/// Creates one or more topics.
/// </summary>
public sealed class CreateTopicsRequest : IKafkaRequest<CreateTopicsResponse>
{
    public static ApiKey ApiKey => ApiKey.CreateTopics;
    public static short LowestSupportedVersion => 5;
    public static short HighestSupportedVersion => 7;

    /// <summary>
    /// The topics to create.
    /// </summary>
    public required IReadOnlyList<CreateTopicData> Topics { get; init; }

    /// <summary>
    /// How long to wait in milliseconds before timing out the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>
    /// If true, check that the topics can be created as specified, but don't create anything.
    /// </summary>
    public bool ValidateOnly { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, CreateTopicData t, short v) => t.Write(ref w, v),
            version);

        writer.WriteInt32(TimeoutMs);

        writer.WriteBoolean(ValidateOnly);

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic creation data.
/// </summary>
public sealed class CreateTopicData
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The number of partitions to create, or -1 to use the broker default.
    /// </summary>
    public int NumPartitions { get; init; } = -1;

    /// <summary>
    /// The replication factor, or -1 to use the broker default.
    /// </summary>
    public short ReplicationFactor { get; init; } = -1;

    /// <summary>
    /// Manual partition-to-broker assignments.
    /// </summary>
    public IReadOnlyList<CreateTopicAssignment>? Assignments { get; init; }

    /// <summary>
    /// Topic-level configuration overrides.
    /// </summary>
    public IReadOnlyList<CreateTopicConfig>? Configs { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(Name);

        writer.WriteInt32(NumPartitions);
        writer.WriteInt16(ReplicationFactor);

        // Assignments
        var assignments = Assignments ?? [];
        writer.WriteCompactArray(
            assignments,
            static (ref KafkaProtocolWriter w, CreateTopicAssignment a, short v) => a.Write(ref w, v),
            version);

        // Configs
        var configs = Configs ?? [];
        writer.WriteCompactArray(
            configs,
            static (ref KafkaProtocolWriter w, CreateTopicConfig c, short v) => c.Write(ref w, v),
            version);

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Partition-to-broker assignment for topic creation.
/// </summary>
public sealed class CreateTopicAssignment
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public required int PartitionIndex { get; init; }

    /// <summary>
    /// The broker IDs for replicas.
    /// </summary>
    public required IReadOnlyList<int> BrokerIds { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(PartitionIndex);

        writer.WriteCompactArray(
            BrokerIds,
            (ref KafkaProtocolWriter w, int id) => w.WriteInt32(id));

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic configuration for topic creation.
/// </summary>
public sealed class CreateTopicConfig
{
    /// <summary>
    /// The configuration name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The configuration value.
    /// </summary>
    public string? Value { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(Name);
        writer.WriteCompactNullableString(Value);

        writer.WriteEmptyTaggedFields();
    }
}
