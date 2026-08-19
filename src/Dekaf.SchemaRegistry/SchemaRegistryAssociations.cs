namespace Dekaf.SchemaRegistry;

/// <summary>
/// Describes an association between a Schema Registry subject and an external resource.
/// </summary>
public sealed class Association
{
    /// <summary>The associated subject name.</summary>
    public required string Subject { get; init; }

    /// <summary>The globally unique association identifier.</summary>
    public required string Guid { get; init; }

    /// <summary>The external resource name, such as a Kafka topic name.</summary>
    public required string ResourceName { get; init; }

    /// <summary>The resource namespace, such as a Kafka cluster ID.</summary>
    public required string ResourceNamespace { get; init; }

    /// <summary>The resource identifier.</summary>
    public required string ResourceId { get; init; }

    /// <summary>The resource type, such as <c>topic</c>.</summary>
    public required string ResourceType { get; init; }

    /// <summary>The association type, such as <c>key</c> or <c>value</c>.</summary>
    public required string AssociationType { get; init; }

    /// <summary>The association lifecycle policy.</summary>
    public required string Lifecycle { get; init; }

    /// <summary>Whether the association is frozen.</summary>
    public bool Frozen { get; init; }
}

/// <summary>
/// Describes one subject association to create or update.
/// </summary>
public sealed class AssociationCreateOrUpdateInfo
{
    /// <summary>The subject name.</summary>
    public required string Subject { get; init; }

    /// <summary>The association type, such as <c>key</c> or <c>value</c>.</summary>
    public required string AssociationType { get; init; }

    /// <summary>The lifecycle policy.</summary>
    public required string Lifecycle { get; init; }

    /// <summary>Whether to freeze the association, or <see langword="null" /> to retain the server default.</summary>
    public bool? Frozen { get; init; }

    /// <summary>An optional schema to register with the association.</summary>
    public Schema? Schema { get; init; }

    /// <summary>Whether Schema Registry should normalize <see cref="Schema" />.</summary>
    public bool? Normalize { get; init; }
}

/// <summary>
/// Requests creation or update of subject associations for a resource.
/// </summary>
public sealed class AssociationCreateOrUpdateRequest
{
    /// <summary>The external resource name, such as a Kafka topic name.</summary>
    public required string ResourceName { get; init; }

    /// <summary>The resource namespace, such as a Kafka cluster ID.</summary>
    public required string ResourceNamespace { get; init; }

    /// <summary>The resource identifier.</summary>
    public required string ResourceId { get; init; }

    /// <summary>The resource type, such as <c>topic</c>.</summary>
    public required string ResourceType { get; init; }

    /// <summary>The subject associations to create or update.</summary>
    public required IReadOnlyList<AssociationCreateOrUpdateInfo> Associations { get; init; }
}

/// <summary>
/// Describes an association returned after a create or update operation.
/// </summary>
public sealed class AssociationInfo
{
    /// <summary>The associated subject name.</summary>
    public required string Subject { get; init; }

    /// <summary>The association type, such as <c>key</c> or <c>value</c>.</summary>
    public required string AssociationType { get; init; }

    /// <summary>The lifecycle policy.</summary>
    public required string Lifecycle { get; init; }

    /// <summary>Whether the association is frozen.</summary>
    public bool Frozen { get; init; }

    /// <summary>The schema registered with the association, when returned by Schema Registry.</summary>
    public Schema? Schema { get; init; }
}

/// <summary>
/// Result of creating or updating associations for a resource.
/// </summary>
public sealed class AssociationResponse
{
    /// <summary>The external resource name.</summary>
    public required string ResourceName { get; init; }

    /// <summary>The resource namespace.</summary>
    public required string ResourceNamespace { get; init; }

    /// <summary>The resource identifier.</summary>
    public required string ResourceId { get; init; }

    /// <summary>The resource type.</summary>
    public required string ResourceType { get; init; }

    /// <summary>The associations created or updated by Schema Registry.</summary>
    public required IReadOnlyList<AssociationInfo> Associations { get; init; }
}
