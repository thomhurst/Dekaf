namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeGroups response (API key 15).
/// Contains the descriptions of consumer groups.
/// </summary>
public sealed class DescribeGroupsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeGroups;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (v1+, zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The described groups.
    /// </summary>
    public required IReadOnlyList<DescribeGroupsResponseGroup> Groups { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 5;

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;

        IReadOnlyList<DescribeGroupsResponseGroup> groups;
        if (isFlexible)
        {
            groups = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => DescribeGroupsResponseGroup.Read(ref r, version)) ?? [];
        }
        else
        {
            groups = reader.ReadArray(
                (ref KafkaProtocolReader r) => DescribeGroupsResponseGroup.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeGroupsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Groups = groups
        };
    }
}

/// <summary>
/// Described group in a DescribeGroups response.
/// </summary>
public sealed class DescribeGroupsResponseGroup
{
    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The group state.
    /// </summary>
    public required string GroupState { get; init; }

    /// <summary>
    /// The group protocol type.
    /// </summary>
    public string? ProtocolType { get; init; }

    /// <summary>
    /// The group protocol data (the selected assignor for consumer groups).
    /// </summary>
    public string? ProtocolData { get; init; }

    /// <summary>
    /// The group members.
    /// </summary>
    public required IReadOnlyList<DescribeGroupsResponseMember> Members { get; init; }

    /// <summary>
    /// Authorized operations for this group (v3+), or int.MinValue if not provided.
    /// </summary>
    public int AuthorizedOperations { get; init; } = int.MinValue;

    public static DescribeGroupsResponseGroup Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 5;

        var errorCode = (ErrorCode)reader.ReadInt16();

        var groupId = isFlexible
            ? reader.ReadCompactString() ?? string.Empty
            : reader.ReadString() ?? string.Empty;

        var groupState = isFlexible
            ? reader.ReadCompactString() ?? string.Empty
            : reader.ReadString() ?? string.Empty;

        var protocolType = isFlexible
            ? reader.ReadCompactString()
            : reader.ReadString();

        var protocolData = isFlexible
            ? reader.ReadCompactString()
            : reader.ReadString();

        IReadOnlyList<DescribeGroupsResponseMember> members;
        if (isFlexible)
        {
            members = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => DescribeGroupsResponseMember.Read(ref r, version)) ?? [];
        }
        else
        {
            members = reader.ReadArray(
                (ref KafkaProtocolReader r) => DescribeGroupsResponseMember.Read(ref r, version)) ?? [];
        }

        var authorizedOperations = version >= 3 ? reader.ReadInt32() : int.MinValue;

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeGroupsResponseGroup
        {
            ErrorCode = errorCode,
            GroupId = groupId,
            GroupState = groupState,
            ProtocolType = protocolType,
            ProtocolData = protocolData,
            Members = members,
            AuthorizedOperations = authorizedOperations
        };
    }
}

/// <summary>
/// Member in a DescribeGroups response.
/// </summary>
public sealed class DescribeGroupsResponseMember
{
    /// <summary>
    /// The member ID.
    /// </summary>
    public required string MemberId { get; init; }

    /// <summary>
    /// The group instance ID (v4+).
    /// </summary>
    public string? GroupInstanceId { get; init; }

    /// <summary>
    /// The client ID.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// The client host.
    /// </summary>
    public string? ClientHost { get; init; }

    /// <summary>
    /// The member metadata (subscription info).
    /// </summary>
    public byte[]? MemberMetadata { get; init; }

    /// <summary>
    /// The member assignment (topic-partition assignments).
    /// </summary>
    public byte[]? MemberAssignment { get; init; }

    public static DescribeGroupsResponseMember Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 5;

        var memberId = isFlexible
            ? reader.ReadCompactString() ?? string.Empty
            : reader.ReadString() ?? string.Empty;

        string? groupInstanceId = null;
        if (version >= 4)
        {
            groupInstanceId = isFlexible
                ? reader.ReadCompactString()
                : reader.ReadString();
        }

        var clientId = isFlexible
            ? reader.ReadCompactString()
            : reader.ReadString();

        var clientHost = isFlexible
            ? reader.ReadCompactString()
            : reader.ReadString();

        var memberMetadata = isFlexible
            ? reader.ReadCompactBytes()
            : reader.ReadBytes();

        var memberAssignment = isFlexible
            ? reader.ReadCompactBytes()
            : reader.ReadBytes();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DescribeGroupsResponseMember
        {
            MemberId = memberId,
            GroupInstanceId = groupInstanceId,
            ClientId = clientId,
            ClientHost = clientHost,
            MemberMetadata = memberMetadata,
            MemberAssignment = memberAssignment
        };
    }
}
