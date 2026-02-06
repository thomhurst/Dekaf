namespace Dekaf.Protocol.Messages;

/// <summary>
/// CreatePartitions request (API key 37).
/// Increases the number of partitions for existing topics.
/// </summary>
public sealed class CreatePartitionsRequest : IKafkaRequest<CreatePartitionsResponse>
{
    public static ApiKey ApiKey => ApiKey.CreatePartitions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 3;

    /// <summary>
    /// The topics to add partitions to.
    /// </summary>
    public required IReadOnlyList<CreatePartitionsTopic> Topics { get; init; }

    /// <summary>
    /// How long to wait in milliseconds before timing out the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>
    /// If true, check that the partition can be created as specified, but don't create anything.
    /// </summary>
    public bool ValidateOnly { get; init; }

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
                (ref KafkaProtocolWriter w, CreatePartitionsTopic t) => t.Write(ref w, version));
        }
        else
        {
            writer.WriteArray(
                Topics,
                (ref KafkaProtocolWriter w, CreatePartitionsTopic t) => t.Write(ref w, version));
        }

        writer.WriteInt32(TimeoutMs);
        writer.WriteBoolean(ValidateOnly);

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Topic to create partitions for.
/// </summary>
public sealed class CreatePartitionsTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The new partition count.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// The new partition assignments, or null for automatic.
    /// </summary>
    public IReadOnlyList<CreatePartitionsAssignment>? Assignments { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        if (isFlexible)
            writer.WriteCompactString(Name);
        else
            writer.WriteString(Name);

        writer.WriteInt32(Count);

        if (isFlexible)
        {
            writer.WriteCompactNullableArray(
                Assignments,
                (ref KafkaProtocolWriter w, CreatePartitionsAssignment a) => a.Write(ref w, version));
        }
        else
        {
            writer.WriteNullableArray(
                Assignments,
                (ref KafkaProtocolWriter w, CreatePartitionsAssignment a) => a.Write(ref w, version));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Broker assignment for new partitions.
/// </summary>
public sealed class CreatePartitionsAssignment
{
    /// <summary>
    /// The broker IDs for replicas.
    /// </summary>
    public required IReadOnlyList<int> BrokerIds { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

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
