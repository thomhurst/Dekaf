namespace Dekaf.Protocol.Messages;

/// <summary>
/// LeaveGroup response (API key 13).
/// </summary>
public sealed class LeaveGroupResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.LeaveGroup;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// Throttle time in milliseconds (v1+).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Error code.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Member responses (v3+).
    /// </summary>
    public IReadOnlyList<LeaveGroupResponseMember>? Members { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 4;

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;
        var errorCode = (ErrorCode)reader.ReadInt16();

        IReadOnlyList<LeaveGroupResponseMember>? members = null;

        if (version >= 3)
        {
            if (isFlexible)
            {
                members = reader.ReadCompactArray((ref KafkaProtocolReader r) => LeaveGroupResponseMember.Read(ref r, version));
            }
            else
            {
                members = reader.ReadArray((ref KafkaProtocolReader r) => LeaveGroupResponseMember.Read(ref r, version));
            }
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new LeaveGroupResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            Members = members
        };
    }
}

/// <summary>
/// Response for a member that attempted to leave the group.
/// </summary>
public sealed class LeaveGroupResponseMember
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
    /// Error code for this member.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    public static LeaveGroupResponseMember Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 4;

        var memberId = isFlexible ? reader.ReadCompactString()! : reader.ReadString()!;
        var groupInstanceId = isFlexible ? reader.ReadCompactString() : reader.ReadString();
        var errorCode = (ErrorCode)reader.ReadInt16();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new LeaveGroupResponseMember
        {
            MemberId = memberId,
            GroupInstanceId = groupInstanceId,
            ErrorCode = errorCode
        };
    }
}
