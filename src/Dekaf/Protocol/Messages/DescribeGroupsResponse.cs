namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeGroups response (API key 15).
/// Contains the descriptions of consumer groups.
/// </summary>
public sealed class DescribeGroupsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeGroups;
    public static short LowestSupportedVersion => 5;
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
        var throttleTimeMs = reader.ReadInt32();

        IReadOnlyList<DescribeGroupsResponseGroup> groups;
        groups = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => DescribeGroupsResponseGroup.Read(ref r, version)) ?? [];

        reader.SkipTaggedFields();

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
        var errorCode = (ErrorCode)reader.ReadInt16();

        var groupId = reader.ReadCompactString() ?? string.Empty;

        var groupState = reader.ReadCompactString() ?? string.Empty;

        var protocolType = reader.ReadCompactString();

        var protocolData = reader.ReadCompactString();

        IReadOnlyList<DescribeGroupsResponseMember> members;
        members = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => DescribeGroupsResponseMember.Read(ref r, version)) ?? [];

        var authorizedOperations = reader.ReadInt32();

        reader.SkipTaggedFields();

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
        var memberId = reader.ReadCompactString() ?? string.Empty;

        string? groupInstanceId = null;
        groupInstanceId = reader.ReadCompactString();

        var clientId = reader.ReadCompactString();

        var clientHost = reader.ReadCompactString();

        var memberMetadata = reader.ReadCompactBytes();

        var memberAssignment = reader.ReadCompactBytes();

        reader.SkipTaggedFields();

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
