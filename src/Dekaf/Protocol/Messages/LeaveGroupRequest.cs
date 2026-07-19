namespace Dekaf.Protocol.Messages;

/// <summary>
/// LeaveGroup request (API key 13). Versions 3+ support removing multiple static members.
/// </summary>
public sealed class LeaveGroupRequest : IKafkaRequest<LeaveGroupResponse>
{
    public static ApiKey ApiKey => ApiKey.LeaveGroup;
    public static short LowestSupportedVersion => 3;
    public static short HighestSupportedVersion => 5;

    public required string GroupId { get; init; }
    public required IReadOnlyList<LeaveGroupRequestMember> Members { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 4;
    public static short GetRequestHeaderVersion(short version) => IsFlexibleVersion(version) ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => IsFlexibleVersion(version) ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (IsFlexibleVersion(version))
        {
            writer.WriteCompactString(GroupId);
            writer.WriteCompactArray(
                Members,
                static (ref KafkaProtocolWriter w, LeaveGroupRequestMember member, short v) => member.Write(ref w, v),
                version);
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteString(GroupId);
        writer.WriteArray(
            Members,
            static (ref KafkaProtocolWriter w, LeaveGroupRequestMember member, short v) => member.Write(ref w, v),
            version);
    }
}

/// <summary>
/// A member to remove from a consumer group.
/// </summary>
public sealed class LeaveGroupRequestMember
{
    public string MemberId { get; init; } = string.Empty;
    public required string GroupInstanceId { get; init; }
    public string? Reason { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        if (LeaveGroupRequest.IsFlexibleVersion(version))
        {
            writer.WriteCompactString(MemberId);
            writer.WriteCompactNullableString(GroupInstanceId);
            if (version >= 5)
            {
                writer.WriteCompactNullableString(Reason);
            }
            writer.WriteEmptyTaggedFields();
            return;
        }

        writer.WriteString(MemberId);
        writer.WriteString(GroupInstanceId);
    }
}
