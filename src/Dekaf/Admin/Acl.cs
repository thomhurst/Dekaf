namespace Dekaf.Admin;

/// <summary>
/// Represents a Kafka ACL resource type.
/// </summary>
public enum ResourceType : sbyte
{
    /// <summary>
    /// Resource type is not known.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Matches any resource type.
    /// </summary>
    Any = 1,

    /// <summary>
    /// A topic resource.
    /// </summary>
    Topic = 2,

    /// <summary>
    /// A consumer group resource.
    /// </summary>
    Group = 3,

    /// <summary>
    /// The cluster resource.
    /// </summary>
    Cluster = 4,

    /// <summary>
    /// A transactional ID resource.
    /// </summary>
    TransactionalId = 5,

    /// <summary>
    /// A delegation token resource.
    /// </summary>
    DelegationToken = 6
}

/// <summary>
/// Represents a Kafka ACL resource pattern type.
/// </summary>
public enum PatternType : sbyte
{
    /// <summary>
    /// Pattern type is not known.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Matches any pattern type.
    /// </summary>
    Any = 1,

    /// <summary>
    /// Match patterns to find ACLs that would affect the resource.
    /// </summary>
    Match = 2,

    /// <summary>
    /// The resource name must match exactly.
    /// </summary>
    Literal = 3,

    /// <summary>
    /// The resource name is a prefix.
    /// </summary>
    Prefixed = 4
}

/// <summary>
/// Represents a Kafka ACL operation.
/// </summary>
public enum AclOperation : sbyte
{
    /// <summary>
    /// Operation is not known.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Matches any operation.
    /// </summary>
    Any = 1,

    /// <summary>
    /// Matches all operations.
    /// </summary>
    All = 2,

    /// <summary>
    /// Read operation.
    /// </summary>
    Read = 3,

    /// <summary>
    /// Write operation.
    /// </summary>
    Write = 4,

    /// <summary>
    /// Create operation.
    /// </summary>
    Create = 5,

    /// <summary>
    /// Delete operation.
    /// </summary>
    Delete = 6,

    /// <summary>
    /// Alter operation.
    /// </summary>
    Alter = 7,

    /// <summary>
    /// Describe operation.
    /// </summary>
    Describe = 8,

    /// <summary>
    /// Cluster action operation.
    /// </summary>
    ClusterAction = 9,

    /// <summary>
    /// Describe configs operation.
    /// </summary>
    DescribeConfigs = 10,

    /// <summary>
    /// Alter configs operation.
    /// </summary>
    AlterConfigs = 11,

    /// <summary>
    /// Idempotent write operation.
    /// </summary>
    IdempotentWrite = 12
}

/// <summary>
/// Represents a Kafka ACL permission type.
/// </summary>
public enum AclPermissionType : sbyte
{
    /// <summary>
    /// Permission type is not known.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Matches any permission type.
    /// </summary>
    Any = 1,

    /// <summary>
    /// Deny permission.
    /// </summary>
    Deny = 2,

    /// <summary>
    /// Allow permission.
    /// </summary>
    Allow = 3
}

/// <summary>
/// Represents a Kafka resource pattern for ACL bindings.
/// </summary>
public sealed class ResourcePattern
{
    /// <summary>
    /// The resource type.
    /// </summary>
    public required ResourceType Type { get; init; }

    /// <summary>
    /// The resource name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The pattern type.
    /// </summary>
    public PatternType PatternType { get; init; } = PatternType.Literal;

    /// <summary>
    /// Creates a resource pattern for a topic.
    /// </summary>
    public static ResourcePattern Topic(string name, PatternType patternType = PatternType.Literal) =>
        new() { Type = ResourceType.Topic, Name = name, PatternType = patternType };

    /// <summary>
    /// Creates a resource pattern for a consumer group.
    /// </summary>
    public static ResourcePattern Group(string name, PatternType patternType = PatternType.Literal) =>
        new() { Type = ResourceType.Group, Name = name, PatternType = patternType };

    /// <summary>
    /// Creates a resource pattern for the cluster.
    /// </summary>
    public static ResourcePattern Cluster() =>
        new() { Type = ResourceType.Cluster, Name = "kafka-cluster", PatternType = PatternType.Literal };

    /// <summary>
    /// Creates a resource pattern for a transactional ID.
    /// </summary>
    public static ResourcePattern TransactionalId(string name, PatternType patternType = PatternType.Literal) =>
        new() { Type = ResourceType.TransactionalId, Name = name, PatternType = patternType };
}

/// <summary>
/// Represents an access control entry for ACL bindings.
/// </summary>
public sealed class AccessControlEntry
{
    /// <summary>
    /// The principal (e.g., "User:alice").
    /// </summary>
    public required string Principal { get; init; }

    /// <summary>
    /// The host from which the principal is allowed/denied access.
    /// Use "*" for all hosts.
    /// </summary>
    public string Host { get; init; } = "*";

    /// <summary>
    /// The operation being allowed or denied.
    /// </summary>
    public required AclOperation Operation { get; init; }

    /// <summary>
    /// The permission type (Allow or Deny).
    /// </summary>
    public required AclPermissionType Permission { get; init; }
}

/// <summary>
/// Represents a binding of an ACL to a resource pattern.
/// </summary>
public sealed class AclBinding
{
    /// <summary>
    /// The resource pattern this ACL applies to.
    /// </summary>
    public required ResourcePattern Pattern { get; init; }

    /// <summary>
    /// The access control entry.
    /// </summary>
    public required AccessControlEntry Entry { get; init; }

    /// <summary>
    /// Creates an ACL binding that allows the specified operation.
    /// </summary>
    public static AclBinding Allow(ResourcePattern pattern, string principal, AclOperation operation, string host = "*") =>
        new()
        {
            Pattern = pattern,
            Entry = new AccessControlEntry
            {
                Principal = principal,
                Host = host,
                Operation = operation,
                Permission = AclPermissionType.Allow
            }
        };

    /// <summary>
    /// Creates an ACL binding that denies the specified operation.
    /// </summary>
    public static AclBinding Deny(ResourcePattern pattern, string principal, AclOperation operation, string host = "*") =>
        new()
        {
            Pattern = pattern,
            Entry = new AccessControlEntry
            {
                Principal = principal,
                Host = host,
                Operation = operation,
                Permission = AclPermissionType.Deny
            }
        };
}

/// <summary>
/// Filter for matching ACL bindings.
/// </summary>
public sealed class AclBindingFilter
{
    /// <summary>
    /// The resource type to filter on, or Any to match all.
    /// </summary>
    public ResourceType ResourceType { get; init; } = ResourceType.Any;

    /// <summary>
    /// The resource name to filter on, or null to match all.
    /// </summary>
    public string? ResourceName { get; init; }

    /// <summary>
    /// The pattern type to filter on, or Any to match all.
    /// </summary>
    public PatternType PatternType { get; init; } = PatternType.Any;

    /// <summary>
    /// The principal to filter on, or null to match all.
    /// </summary>
    public string? Principal { get; init; }

    /// <summary>
    /// The host to filter on, or null to match all.
    /// </summary>
    public string? Host { get; init; }

    /// <summary>
    /// The operation to filter on, or Any to match all.
    /// </summary>
    public AclOperation Operation { get; init; } = AclOperation.Any;

    /// <summary>
    /// The permission type to filter on, or Any to match all.
    /// </summary>
    public AclPermissionType Permission { get; init; } = AclPermissionType.Any;

    /// <summary>
    /// Creates a filter that matches all ACLs.
    /// </summary>
    public static AclBindingFilter MatchAll() => new();

    /// <summary>
    /// Creates a filter for ACLs on a specific resource.
    /// </summary>
    public static AclBindingFilter ForResource(ResourceType resourceType, string resourceName, PatternType patternType = PatternType.Any) =>
        new()
        {
            ResourceType = resourceType,
            ResourceName = resourceName,
            PatternType = patternType
        };

    /// <summary>
    /// Creates a filter for ACLs for a specific principal.
    /// </summary>
    public static AclBindingFilter ForPrincipal(string principal) =>
        new() { Principal = principal };
}

/// <summary>
/// Options for CreateAcls operation.
/// </summary>
public sealed class CreateAclsOptions
{
    /// <summary>
    /// The timeout in milliseconds for the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Options for DeleteAcls operation.
/// </summary>
public sealed class DeleteAclsOptions
{
    /// <summary>
    /// The timeout in milliseconds for the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Options for DescribeAcls operation.
/// </summary>
public sealed class DescribeAclsOptions
{
    /// <summary>
    /// The timeout in milliseconds for the request.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;
}
