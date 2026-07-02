namespace Dekaf.Protocol.Messages;

/// <summary>
/// DeleteGroups response (API key 42).
/// Contains the results of group deletion requests.
/// </summary>
public sealed class DeleteGroupsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DeleteGroups;
    public static short LowestSupportedVersion => 2;
    public static short HighestSupportedVersion => 2;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Results for each group.
    /// </summary>
    public required IReadOnlyList<DeleteGroupsResponseResult> Results { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();

        IReadOnlyList<DeleteGroupsResponseResult> results;
        results = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => DeleteGroupsResponseResult.Read(ref r, version)) ?? [];

        reader.SkipTaggedFields();

        return new DeleteGroupsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Results = results
        };
    }
}

/// <summary>
/// Per-group result for DeleteGroups.
/// </summary>
public sealed class DeleteGroupsResponseResult
{
    /// <summary>
    /// The group ID.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    public static DeleteGroupsResponseResult Read(ref KafkaProtocolReader reader, short version)
    {
        var groupId = reader.ReadCompactString() ?? string.Empty;

        var errorCode = (ErrorCode)reader.ReadInt16();

        reader.SkipTaggedFields();

        return new DeleteGroupsResponseResult
        {
            GroupId = groupId,
            ErrorCode = errorCode
        };
    }
}
