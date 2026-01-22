namespace Dekaf.Protocol.Messages;

/// <summary>
/// JoinGroup request (API key 11).
/// Joins a consumer group.
/// </summary>
public sealed class JoinGroupRequest : IKafkaRequest<JoinGroupResponse>
{
    public static ApiKey ApiKey => ApiKey.JoinGroup;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 9;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Session timeout in milliseconds.
    /// </summary>
    public required int SessionTimeoutMs { get; init; }

    /// <summary>
    /// Rebalance timeout in milliseconds (v1+).
    /// </summary>
    public int RebalanceTimeoutMs { get; init; } = -1;

    /// <summary>
    /// Member ID. Empty string for new members.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// Group instance ID for static membership (v5+).
    /// </summary>
    public string? GroupInstanceId { get; init; }

    /// <summary>
    /// Protocol type (e.g., "consumer").
    /// </summary>
    public required string ProtocolType { get; init; }

    /// <summary>
    /// Supported protocols and their metadata.
    /// </summary>
    public required IReadOnlyList<JoinGroupRequestProtocol> Protocols { get; init; }

    /// <summary>
    /// Reason for joining (v8+).
    /// </summary>
    public string? Reason { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 6;
    public static short GetRequestHeaderVersion(short version) => version >= 6 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 6 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 6;

        if (isFlexible)
            writer.WriteCompactString(GroupId);
        else
            writer.WriteString(GroupId);

        writer.WriteInt32(SessionTimeoutMs);

        if (version >= 1)
        {
            writer.WriteInt32(RebalanceTimeoutMs);
        }

        if (isFlexible)
            writer.WriteCompactString(MemberId);
        else
            writer.WriteString(MemberId);

        if (version >= 5)
        {
            if (isFlexible)
                writer.WriteCompactNullableString(GroupInstanceId);
            else
                writer.WriteString(GroupInstanceId);
        }

        if (isFlexible)
            writer.WriteCompactString(ProtocolType);
        else
            writer.WriteString(ProtocolType);

        if (isFlexible)
        {
            writer.WriteCompactArray(
                Protocols,
                (ref KafkaProtocolWriter w, JoinGroupRequestProtocol p) => p.Write(ref w, version));
        }
        else
        {
            writer.WriteArray(
                Protocols,
                (ref KafkaProtocolWriter w, JoinGroupRequestProtocol p) => p.Write(ref w, version));
        }

        if (version >= 8)
        {
            writer.WriteCompactNullableString(Reason);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Protocol in a JoinGroup request.
/// </summary>
public sealed class JoinGroupRequestProtocol
{
    /// <summary>
    /// Protocol name (e.g., "range", "roundrobin").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Protocol metadata (serialized subscription).
    /// </summary>
    public required byte[] Metadata { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 6;

        if (isFlexible)
        {
            writer.WriteCompactString(Name);
            writer.WriteCompactBytes(Metadata);
            writer.WriteEmptyTaggedFields();
        }
        else
        {
            writer.WriteString(Name);
            writer.WriteBytes(Metadata);
        }
    }
}
