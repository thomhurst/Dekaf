namespace Dekaf.Protocol.Messages;

/// <summary>
/// DeleteAcls response (API key 31).
/// Contains the results of ACL deletion requests.
/// </summary>
public sealed class DeleteAclsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DeleteAcls;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 3;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The results for each filter.
    /// </summary>
    public required IReadOnlyList<DeleteAclsFilterResult> FilterResults { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var throttleTimeMs = reader.ReadInt32();

        IReadOnlyList<DeleteAclsFilterResult> filterResults;
        if (isFlexible)
        {
            filterResults = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => DeleteAclsFilterResult.Read(ref r, version)) ?? [];
        }
        else
        {
            filterResults = reader.ReadArray(
                (ref KafkaProtocolReader r) => DeleteAclsFilterResult.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DeleteAclsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            FilterResults = filterResults
        };
    }
}

/// <summary>
/// The result of a delete ACL filter.
/// </summary>
public sealed class DeleteAclsFilterResult
{
    /// <summary>
    /// The error code, or 0 if the filter was a success.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if the filter was a success.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The ACLs that were deleted.
    /// </summary>
    public required IReadOnlyList<DeleteAclsMatchingAcl> MatchingAcls { get; init; }

    public static DeleteAclsFilterResult Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage;
        if (isFlexible)
            errorMessage = reader.ReadCompactString();
        else
            errorMessage = reader.ReadString();

        IReadOnlyList<DeleteAclsMatchingAcl> matchingAcls;
        if (isFlexible)
        {
            matchingAcls = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => DeleteAclsMatchingAcl.Read(ref r, version)) ?? [];
        }
        else
        {
            matchingAcls = reader.ReadArray(
                (ref KafkaProtocolReader r) => DeleteAclsMatchingAcl.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DeleteAclsFilterResult
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            MatchingAcls = matchingAcls
        };
    }
}

/// <summary>
/// An ACL that was deleted by a filter.
/// </summary>
public sealed class DeleteAclsMatchingAcl
{
    /// <summary>
    /// The deletion error code, or 0 if the deletion succeeded.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The deletion error message, or null if the deletion succeeded.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The resource type.
    /// </summary>
    public sbyte ResourceType { get; init; }

    /// <summary>
    /// The resource name.
    /// </summary>
    public required string ResourceName { get; init; }

    /// <summary>
    /// The resource pattern type (v1+).
    /// </summary>
    public sbyte PatternType { get; init; } = 3; // Literal

    /// <summary>
    /// The principal.
    /// </summary>
    public required string Principal { get; init; }

    /// <summary>
    /// The host.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// The operation.
    /// </summary>
    public sbyte Operation { get; init; }

    /// <summary>
    /// The permission type.
    /// </summary>
    public sbyte PermissionType { get; init; }

    public static DeleteAclsMatchingAcl Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 2;

        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage;
        if (isFlexible)
            errorMessage = reader.ReadCompactString();
        else
            errorMessage = reader.ReadString();

        var resourceType = reader.ReadInt8();

        string resourceName;
        if (isFlexible)
            resourceName = reader.ReadCompactString() ?? string.Empty;
        else
            resourceName = reader.ReadString() ?? string.Empty;

        var patternType = version >= 1 ? reader.ReadInt8() : (sbyte)3;

        string principal;
        string host;
        if (isFlexible)
        {
            principal = reader.ReadCompactString() ?? string.Empty;
            host = reader.ReadCompactString() ?? string.Empty;
        }
        else
        {
            principal = reader.ReadString() ?? string.Empty;
            host = reader.ReadString() ?? string.Empty;
        }

        var operation = reader.ReadInt8();
        var permissionType = reader.ReadInt8();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DeleteAclsMatchingAcl
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ResourceType = resourceType,
            ResourceName = resourceName,
            PatternType = patternType,
            Principal = principal,
            Host = host,
            Operation = operation,
            PermissionType = permissionType
        };
    }
}
