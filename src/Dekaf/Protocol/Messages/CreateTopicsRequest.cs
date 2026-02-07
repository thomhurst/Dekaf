namespace Dekaf.Protocol.Messages;

/// <summary>
/// CreateTopics request (API key 19).
/// Creates one or more topics.
/// </summary>
public sealed class CreateTopicsRequest : IKafkaRequest<CreateTopicsResponse>
{
    public static ApiKey ApiKey => ApiKey.CreateTopics;
    public static short LowestSupportedVersion => 0;
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

    public static bool IsFlexibleVersion(short version) => version >= 5;
    public static short GetRequestHeaderVersion(short version) => version >= 5 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 5 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 5;

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Topics,
                static (ref KafkaProtocolWriter w, CreateTopicData t, short v) => t.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                Topics,
                static (ref KafkaProtocolWriter w, CreateTopicData t, short v) => t.Write(ref w, v),
                version);
        }

        writer.WriteInt32(TimeoutMs);

        if (version >= 1)
        {
            writer.WriteBoolean(ValidateOnly);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
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
        var isFlexible = version >= 5;

        if (isFlexible)
            writer.WriteCompactString(Name);
        else
            writer.WriteString(Name);

        writer.WriteInt32(NumPartitions);
        writer.WriteInt16(ReplicationFactor);

        // Assignments
        var assignments = Assignments ?? [];
        if (isFlexible)
        {
            writer.WriteCompactArray(
                assignments,
                static (ref KafkaProtocolWriter w, CreateTopicAssignment a, short v) => a.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                assignments,
                static (ref KafkaProtocolWriter w, CreateTopicAssignment a, short v) => a.Write(ref w, v),
                version);
        }

        // Configs
        var configs = Configs ?? [];
        if (isFlexible)
        {
            writer.WriteCompactArray(
                configs,
                static (ref KafkaProtocolWriter w, CreateTopicConfig c, short v) => c.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteArray(
                configs,
                static (ref KafkaProtocolWriter w, CreateTopicConfig c, short v) => c.Write(ref w, v),
                version);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
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
        var isFlexible = version >= 5;

        writer.WriteInt32(PartitionIndex);

        if (isFlexible)
        {
            writer.WriteCompactArray(
                BrokerIds,
                (ref KafkaProtocolWriter w, int id) => w.WriteInt32(id));
        }
        else
        {
            writer.WriteArray(
                BrokerIds,
                (ref KafkaProtocolWriter w, int id) => w.WriteInt32(id));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
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
        var isFlexible = version >= 5;

        if (isFlexible)
        {
            writer.WriteCompactString(Name);
            writer.WriteCompactNullableString(Value);
        }
        else
        {
            writer.WriteString(Name);
            writer.WriteString(Value);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
