namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeAcls response (API key 29).
/// Contains the ACLs matching the filter.
/// </summary>
public sealed class DescribeAclsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeAcls;
    public static short LowestSupportedVersion => 2;
    public static short HighestSupportedVersion => 3;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The resources and their associated ACLs.
    /// </summary>
    public required IReadOnlyList<DescribeAclsResource> Resources { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage;
        errorMessage = reader.ReadCompactString();

        IReadOnlyList<DescribeAclsResource> resources;
        resources = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => DescribeAclsResource.Read(ref r, version)) ?? [];

        reader.SkipTaggedFields();

        return new DescribeAclsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Resources = resources
        };
    }
}

/// <summary>
/// A resource with its associated ACLs.
/// </summary>
public sealed class DescribeAclsResource
{
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
    /// The ACLs associated with this resource.
    /// </summary>
    public required IReadOnlyList<AclDescription> Acls { get; init; }

    public static DescribeAclsResource Read(ref KafkaProtocolReader reader, short version)
    {
        var resourceType = reader.ReadInt8();

        string resourceName;
        resourceName = reader.ReadCompactString() ?? string.Empty;

        var patternType = reader.ReadInt8();

        IReadOnlyList<AclDescription> acls;
        acls = reader.ReadCompactArray(
            (ref KafkaProtocolReader r) => AclDescription.Read(ref r, version)) ?? [];

        reader.SkipTaggedFields();

        return new DescribeAclsResource
        {
            ResourceType = resourceType,
            ResourceName = resourceName,
            PatternType = patternType,
            Acls = acls
        };
    }
}

/// <summary>
/// An ACL description.
/// </summary>
public sealed class AclDescription
{
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

    public static AclDescription Read(ref KafkaProtocolReader reader, short version)
    {
        string principal;
        string host;

        principal = reader.ReadCompactString() ?? string.Empty;
        host = reader.ReadCompactString() ?? string.Empty;

        var operation = reader.ReadInt8();
        var permissionType = reader.ReadInt8();

        reader.SkipTaggedFields();

        return new AclDescription
        {
            Principal = principal,
            Host = host,
            Operation = operation,
            PermissionType = permissionType
        };
    }
}
