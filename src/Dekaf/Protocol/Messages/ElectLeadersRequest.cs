namespace Dekaf.Protocol.Messages;

/// <summary>
/// ElectLeaders request (API key 43).
/// Triggers leader election for partitions.
/// </summary>
public sealed class ElectLeadersRequest : IKafkaRequest<ElectLeadersResponse>
{
    public static ApiKey ApiKey => ApiKey.ElectLeaders;
    public static short LowestSupportedVersion => 0;
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

    public static bool IsFlexibleVersion(short version) => version >= 2;
    public static short GetRequestHeaderVersion(short version) => version >= 2 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 2 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 2;

        // ElectionType is only in v1+
        if (version >= 1)
        {
            writer.WriteInt8((sbyte)ElectionType);
        }

        // TopicPartitions (nullable array)
        if (TopicPartitions is null)
        {
            if (isFlexible)
                writer.WriteUnsignedVarInt(0); // 0 means null for compact nullable arrays
            else
                writer.WriteInt32(-1);
        }
        else
        {
            if (isFlexible)
            {
                writer.WriteCompactArray(
                    TopicPartitions,
                    (ref KafkaProtocolWriter w, ElectLeadersRequestTopic t) => t.Write(ref w, version));
            }
            else
            {
                writer.WriteArray(
                    TopicPartitions,
                    (ref KafkaProtocolWriter w, ElectLeadersRequestTopic t) => t.Write(ref w, version));
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
        var isFlexible = version >= 2;

        if (isFlexible)
            writer.WriteCompactString(Topic);
        else
            writer.WriteString(Topic);

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Partitions,
                (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
            writer.WriteEmptyTaggedFields();
        }
        else
        {
            writer.WriteArray(
                Partitions,
                (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
        }
    }
}
