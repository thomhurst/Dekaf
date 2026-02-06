namespace Dekaf.Protocol.Messages;

/// <summary>
/// ListGroups response (API key 16).
/// Contains the groups on a broker.
/// </summary>
public sealed class ListGroupsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ListGroups;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 5;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (v1+, zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The groups on this broker.
    /// </summary>
    public required IReadOnlyList<ListGroupsResponseGroup> Groups { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;
        var errorCode = (ErrorCode)reader.ReadInt16();

        IReadOnlyList<ListGroupsResponseGroup> groups;
        if (isFlexible)
        {
            groups = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => ListGroupsResponseGroup.Read(ref r, version)) ?? [];
        }
        else
        {
            groups = reader.ReadArray(
                (ref KafkaProtocolReader r) => ListGroupsResponseGroup.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new ListGroupsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            Groups = groups
        };
    }
}

/// <summary>
/// Group listing in a ListGroups response.
/// </summary>
public sealed class ListGroupsResponseGroup
{
    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The group protocol type.
    /// </summary>
    public string? ProtocolType { get; init; }

    /// <summary>
    /// The group state (v4+).
    /// </summary>
    public string? GroupState { get; init; }

    /// <summary>
    /// The group type (v5+).
    /// </summary>
    public string? GroupType { get; init; }

    public static ListGroupsResponseGroup Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 3;

        var groupId = isFlexible
            ? reader.ReadCompactString() ?? string.Empty
            : reader.ReadString() ?? string.Empty;

        var protocolType = isFlexible
            ? reader.ReadCompactString()
            : reader.ReadString();

        string? groupState = null;
        if (version >= 4)
        {
            groupState = reader.ReadCompactString();
        }

        string? groupType = null;
        if (version >= 5)
        {
            groupType = reader.ReadCompactString();
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new ListGroupsResponseGroup
        {
            GroupId = groupId,
            ProtocolType = protocolType,
            GroupState = groupState,
            GroupType = groupType
        };
    }
}
