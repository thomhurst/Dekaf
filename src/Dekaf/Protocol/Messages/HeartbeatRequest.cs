namespace Dekaf.Protocol.Messages;

/// <summary>
/// Heartbeat request (API key 12).
/// Keeps the consumer group membership alive.
/// </summary>
public sealed class HeartbeatRequest : IKafkaRequest<HeartbeatResponse>
{
    public static ApiKey ApiKey => ApiKey.Heartbeat;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 4;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Generation ID.
    /// </summary>
    public required int GenerationId { get; init; }

    /// <summary>
    /// Member ID.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// Group instance ID for static membership (v3+).
    /// </summary>
    public string? GroupInstanceId { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 4;
    public static short GetRequestHeaderVersion(short version) => version >= 4 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 4 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 4;

        if (isFlexible)
            writer.WriteCompactString(GroupId);
        else
            writer.WriteString(GroupId);

        writer.WriteInt32(GenerationId);

        if (isFlexible)
            writer.WriteCompactString(MemberId);
        else
            writer.WriteString(MemberId);

        if (version >= 3)
        {
            if (isFlexible)
                writer.WriteCompactNullableString(GroupInstanceId);
            else
                writer.WriteString(GroupInstanceId);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
