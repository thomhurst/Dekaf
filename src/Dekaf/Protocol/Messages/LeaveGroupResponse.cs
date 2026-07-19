namespace Dekaf.Protocol.Messages;

/// <summary>
/// LeaveGroup response (API key 13).
/// </summary>
public sealed class LeaveGroupResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.LeaveGroup;
    public static short LowestSupportedVersion => 3;
    public static short HighestSupportedVersion => 5;

    public int ThrottleTimeMs { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public required IReadOnlyList<LeaveGroupResponseMember> Members { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = LeaveGroupRequest.IsFlexibleVersion(version);
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var members = flexible
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => LeaveGroupResponseMember.Read(ref r, v),
                version)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => LeaveGroupResponseMember.Read(ref r, v),
                version);

        if (flexible)
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
/// Per-member LeaveGroup result.
/// </summary>
public sealed class LeaveGroupResponseMember
{
    public required string MemberId { get; init; }
    public string? GroupInstanceId { get; init; }
    public ErrorCode ErrorCode { get; init; }

    public static LeaveGroupResponseMember Read(ref KafkaProtocolReader reader, short version)
    {
        var flexible = LeaveGroupRequest.IsFlexibleVersion(version);
        var memberId = flexible
            ? reader.ReadCompactNonNullableString()
            : reader.ReadString() ?? string.Empty;
        var groupInstanceId = flexible ? reader.ReadCompactString() : reader.ReadString();
        var errorCode = (ErrorCode)reader.ReadInt16();

        if (flexible)
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
