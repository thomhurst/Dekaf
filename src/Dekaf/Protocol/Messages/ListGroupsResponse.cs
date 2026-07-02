namespace Dekaf.Protocol.Messages;

/// <summary>
/// ListGroups response (API key 16).
/// Contains the groups on a broker.
/// </summary>
public sealed class ListGroupsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ListGroups;
    public static short LowestSupportedVersion => 3;
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
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        IReadOnlyList<ListGroupsResponseGroup> groups;
        groups = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => ListGroupsResponseGroup.Read(ref r, version)) ?? [];

        reader.SkipTaggedFields();

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
        var groupId = reader.ReadCompactString() ?? string.Empty;

        var protocolType = reader.ReadCompactString();

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

        reader.SkipTaggedFields();

        return new ListGroupsResponseGroup
        {
            GroupId = groupId,
            ProtocolType = protocolType,
            GroupState = groupState,
            GroupType = groupType
        };
    }
}
