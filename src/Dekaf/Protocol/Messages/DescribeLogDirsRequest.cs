namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeLogDirs request (API key 35).
/// Describes replica placement and size by log directory on a broker.
/// </summary>
public sealed class DescribeLogDirsRequest : IKafkaRequest<DescribeLogDirsResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeLogDirs;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// Topics to describe, or null for all topics.
    /// </summary>
    public IReadOnlyList<DescribeLogDirsRequestTopic>? Topics { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 2;

    public static short GetRequestHeaderVersion(short version) => version >= 2 ? (short)2 : (short)1;

    public static short GetResponseHeaderVersion(short version) => version >= 2 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (version >= 2)
        {
            writer.WriteCompactNullableArray(
                Topics,
                static (ref KafkaProtocolWriter w, DescribeLogDirsRequestTopic t, short v) => t.Write(ref w, v),
                version);
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteNullableArray(
            Topics,
            static (ref KafkaProtocolWriter w, DescribeLogDirsRequestTopic t, short v) => t.Write(ref w, v),
            version);
    }
}

/// <summary>
/// Topic filter in a DescribeLogDirs request.
/// </summary>
public sealed class DescribeLogDirsRequestTopic
{
    public required string Topic { get; init; }
    public required IReadOnlyList<int> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (version >= 2)
        {
            writer.WriteCompactString(Topic);
            writer.WriteCompactArray(
                Partitions,
                static (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteString(Topic);
        writer.WriteArray(
            Partitions,
            static (ref KafkaProtocolWriter w, int p) => w.WriteInt32(p));
    }
}
