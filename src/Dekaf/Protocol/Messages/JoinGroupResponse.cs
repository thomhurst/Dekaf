namespace Dekaf.Protocol.Messages;

/// <summary>
/// JoinGroup response (API key 11).
/// Contains group membership and leader information.
/// </summary>
public sealed class JoinGroupResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.JoinGroup;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 9;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Error code.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Generation ID.
    /// </summary>
    public required int GenerationId { get; init; }

    /// <summary>
    /// Selected protocol.
    /// </summary>
    public string? ProtocolType { get; init; }

    /// <summary>
    /// Selected protocol name.
    /// </summary>
    public string? ProtocolName { get; init; }

    /// <summary>
    /// Group leader's member ID.
    /// </summary>
    public required string Leader { get; init; }

    /// <summary>
    /// Whether to skip assignment (v9+).
    /// </summary>
    public bool SkipAssignment { get; init; }

    /// <summary>
    /// This member's ID.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// Group members (only for leader).
    /// </summary>
    public required IReadOnlyList<JoinGroupResponseMember> Members { get; init; }

    /// <summary>
    /// Returns true if this member is the group leader.
    /// </summary>
    public bool IsLeader => Leader == MemberId;

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 6;

        var throttleTimeMs = version >= 2 ? reader.ReadInt32() : 0;
        var errorCode = (ErrorCode)reader.ReadInt16();
        var generationId = reader.ReadInt32();

        string? protocolType = null;
        if (version >= 7)
        {
            protocolType = reader.ReadCompactString();
        }

        var protocolName = isFlexible ? reader.ReadCompactString() : reader.ReadString();
        var leader = isFlexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;

        var skipAssignment = false;
        if (version >= 9)
        {
            skipAssignment = reader.ReadBoolean();
        }

        var memberId = isFlexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;

        var members = isFlexible
            ? reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => JoinGroupResponseMember.Read(ref r, v), version)
            : reader.ReadArray(static (ref KafkaProtocolReader r, short v) => JoinGroupResponseMember.Read(ref r, v), version);

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new JoinGroupResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            GenerationId = generationId,
            ProtocolType = protocolType,
            ProtocolName = protocolName,
            Leader = leader,
            SkipAssignment = skipAssignment,
            MemberId = memberId,
            Members = members
        };
    }
}

/// <summary>
/// Member information in a JoinGroup response.
/// </summary>
public sealed class JoinGroupResponseMember
{
    /// <summary>
    /// Member ID.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// Group instance ID for static membership.
    /// </summary>
    public string? GroupInstanceId { get; init; }

    /// <summary>
    /// Member metadata (subscription).
    /// </summary>
    public required byte[] Metadata { get; init; }

    public static JoinGroupResponseMember Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 6;

        var memberId = isFlexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;

        string? groupInstanceId = null;
        if (version >= 5)
        {
            groupInstanceId = isFlexible ? reader.ReadCompactString() : reader.ReadString();
        }

        var metadata = isFlexible ? reader.ReadCompactBytes() ?? [] : reader.ReadBytes() ?? [];

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new JoinGroupResponseMember
        {
            MemberId = memberId,
            GroupInstanceId = groupInstanceId,
            Metadata = metadata
        };
    }
}
