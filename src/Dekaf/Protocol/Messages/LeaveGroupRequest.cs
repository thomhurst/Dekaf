namespace Dekaf.Protocol.Messages;

/// <summary>
/// LeaveGroup request (API key 13).
/// Sent by a consumer to leave a consumer group.
/// </summary>
public sealed class LeaveGroupRequest : IKafkaRequest<LeaveGroupResponse>
{
    public static ApiKey ApiKey => ApiKey.LeaveGroup;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Member ID (v0-v2).
    /// </summary>
    public string? MemberId { get; init; }

    /// <summary>
    /// List of members leaving the group (v3+).
    /// </summary>
    public IReadOnlyList<LeaveGroupRequestMember>? Members { get; init; }

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

        if (version <= 2)
        {
            // v0-v2: single member ID
            if (isFlexible)
                writer.WriteCompactString(MemberId ?? string.Empty);
            else
                writer.WriteString(MemberId ?? string.Empty);
        }
        else
        {
            // v3+: members array
            if (isFlexible)
            {
                writer.WriteCompactArray(
                    Members ?? [],
                    static (ref KafkaProtocolWriter w, LeaveGroupRequestMember m, short v) => m.Write(ref w, v),
                    version);
            }
            else
            {
                writer.WriteArray(
                    Members ?? [],
                    static (ref KafkaProtocolWriter w, LeaveGroupRequestMember m, short v) => m.Write(ref w, v),
                    version);
            }
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// A member leaving the group.
/// </summary>
public sealed class LeaveGroupRequestMember
{
    /// <summary>
    /// The member ID.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// Group instance ID for static membership.
    /// </summary>
    public string? GroupInstanceId { get; init; }

    /// <summary>
    /// The reason for leaving the group (v5+).
    /// </summary>
    public string? Reason { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 4;

        if (isFlexible)
            writer.WriteCompactString(MemberId);
        else
            writer.WriteString(MemberId);

        if (isFlexible)
            writer.WriteCompactNullableString(GroupInstanceId);
        else
            writer.WriteString(GroupInstanceId);

        if (version >= 5)
        {
            if (isFlexible)
                writer.WriteCompactNullableString(Reason);
            else
                writer.WriteString(Reason);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
