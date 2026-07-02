namespace Dekaf.Protocol.Messages;

/// <summary>
/// ElectLeaders request (API key 43).
/// Triggers leader election for partitions.
/// </summary>
public sealed class ElectLeadersRequest : IKafkaRequest<ElectLeadersResponse>
{
    public static ApiKey ApiKey => ApiKey.ElectLeaders;
    public static short LowestSupportedVersion => 2;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// Type of election: 0 = Preferred, 1 = Unclean.
    /// Only supported in v1+.
    /// </summary>
    public byte ElectionType { get; init; }

    /// <summary>
    /// Topics with partitions to elect leaders for.
    /// Null means elect leaders for all partitions.
    /// </summary>
    public IReadOnlyList<ElectLeadersRequestTopic>? TopicPartitions { get; init; }

    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        // ElectionType is only in v1+
        writer.WriteInt8((sbyte)ElectionType);

        // TopicPartitions (nullable array)
        if (TopicPartitions is null)
        {
            writer.WriteUnsignedVarInt(0); // 0 means null for compact nullable arrays
        }
        else
        {
            writer.WriteCompactArray(
            TopicPartitions,
            static (ref KafkaProtocolWriter w, ElectLeadersRequestTopic t, short v) => t.Write(ref w, v),
            version);
        }

        writer.WriteInt32(TimeoutMs);

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic in an ElectLeaders request.
/// </summary>
public sealed class ElectLeadersRequestTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// The partition indexes.
    /// </summary>
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(Topic);

        writer.WriteCompactArray(
            Partitions,
            (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
        writer.WriteEmptyTaggedFields();
    }
}
