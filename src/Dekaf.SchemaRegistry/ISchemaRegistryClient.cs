namespace Dekaf.SchemaRegistry;

/// <summary>
/// Client interface for interacting with Confluent Schema Registry.
/// </summary>
public interface ISchemaRegistryClient : IDisposable
{
    /// <summary>
    /// Registers a schema under a subject. If the schema is already registered,
    /// returns the existing schema ID.
    /// </summary>
    /// <param name="subject">The subject name (typically topic-key or topic-value).</param>
    /// <param name="schema">The schema to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema ID.</returns>
    Task<int> RegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a schema by its global ID.
    /// </summary>
    /// <param name="id">The schema ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema.</returns>
    Task<Schema> GetSchemaAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the schema registered under a subject at a specific version.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="version">The version number, or "latest" for latest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registered schema with metadata.</returns>
    Task<RegisteredSchema> GetSchemaBySubjectAsync(string subject, string version = "latest", CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or registers a schema, returning its ID.
    /// This is a convenience method that first checks if the schema exists.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="schema">The schema.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema ID.</returns>
    Task<int> GetOrRegisterSchemaAsync(string subject, Schema schema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all subjects.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of subject names.</returns>
    Task<IReadOnlyList<string>> GetAllSubjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all versions registered under a subject.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of version numbers.</returns>
    Task<IReadOnlyList<int>> GetVersionsAsync(string subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a schema is compatible with the subject's compatibility settings.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="schema">The schema to check.</param>
    /// <param name="version">The version to check against (default: latest).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if compatible.</returns>
    Task<bool> IsCompatibleAsync(string subject, Schema schema, string version = "latest", CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a subject and all associated schemas.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="permanent">If true, permanently delete (hard delete).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of deleted version numbers.</returns>
    Task<IReadOnlyList<int>> DeleteSubjectAsync(string subject, bool permanent = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional Schema Registry client cache access used by hot deserialization paths.
/// </summary>
public interface ISchemaRegistryCache
{
    /// <summary>
    /// Attempts to get a schema by global ID from the local cache without allocating.
    /// </summary>
    /// <param name="id">The schema ID.</param>
    /// <param name="schema">The cached schema, when found.</param>
    /// <returns>True when the schema is already cached.</returns>
    bool TryGetCachedSchema(int id, out Schema schema);
}

/// <summary>
/// Represents a schema.
/// </summary>
public sealed class Schema
{
    /// <summary>
    /// The schema type (AVRO, JSON, PROTOBUF).
    /// </summary>
    public SchemaType SchemaType { get; init; } = SchemaType.Avro;

    /// <summary>
    /// The schema definition string.
    /// </summary>
    public required string SchemaString { get; init; }

    /// <summary>
    /// Schema references for schemas that depend on other schemas.
    /// </summary>
    public IReadOnlyList<SchemaReference>? References { get; init; }

    /// <summary>
    /// Optional Schema Registry data-contract metadata, including field tags used by CSFLE policies.
    /// </summary>
    public SchemaMetadata? Metadata { get; init; }

    /// <summary>
    /// Optional Schema Registry rule set for data-contract validation, migration, and encryption rules.
    /// </summary>
    public SchemaRuleSet? RuleSet { get; init; }
}

/// <summary>
/// A schema that has been registered in the registry.
/// </summary>
public sealed class RegisteredSchema
{
    /// <summary>
    /// The global schema ID.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The subject this schema is registered under.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// The version number within the subject.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// The schema.
    /// </summary>
    public required Schema Schema { get; init; }
}

/// <summary>
/// Schema Registry data-contract metadata.
/// </summary>
public sealed class SchemaMetadata
{
    /// <summary>
    /// Field or schema paths mapped to tag names, for example PII or encrypted.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>>? Tags { get; init; }

    /// <summary>
    /// Arbitrary schema metadata properties.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Properties { get; init; }

    /// <summary>
    /// Names of metadata properties that should be treated as sensitive.
    /// </summary>
    public IReadOnlySet<string>? Sensitive { get; init; }
}

/// <summary>
/// Schema Registry data-contract rule set.
/// </summary>
public sealed class SchemaRuleSet
{
    /// <summary>
    /// Rules used when migrating between schema versions.
    /// </summary>
    public IReadOnlyList<SchemaRule>? MigrationRules { get; init; }

    /// <summary>
    /// Rules used for validation or transforms on the current schema.
    /// </summary>
    public IReadOnlyList<SchemaRule>? DomainRules { get; init; }

    /// <summary>
    /// Rules used for encoding transforms such as field-level encryption.
    /// </summary>
    public IReadOnlyList<SchemaRule>? EncodingRules { get; init; }

    /// <summary>
    /// Optional Schema Registry activation marker.
    /// </summary>
    public string? EnableAt { get; init; }
}

/// <summary>
/// Schema Registry data-contract rule.
/// </summary>
public sealed class SchemaRule
{
    /// <summary>
    /// Rule name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional rule documentation.
    /// </summary>
    public string? Doc { get; init; }

    /// <summary>
    /// Rule kind.
    /// </summary>
    public SchemaRuleKind Kind { get; init; }

    /// <summary>
    /// Rule mode.
    /// </summary>
    public SchemaRuleMode Mode { get; init; }

    /// <summary>
    /// Rule executor type, for example ENCRYPT or CEL.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Tags this rule applies to.
    /// </summary>
    public IReadOnlySet<string>? Tags { get; init; }

    /// <summary>
    /// Rule parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }

    /// <summary>
    /// Optional rule expression.
    /// </summary>
    public string? Expr { get; init; }

    /// <summary>
    /// Optional action on rule success.
    /// </summary>
    public string? OnSuccess { get; init; }

    /// <summary>
    /// Optional action on rule failure.
    /// </summary>
    public string? OnFailure { get; init; }

    /// <summary>
    /// Whether this rule is disabled.
    /// </summary>
    public bool Disabled { get; init; }
}

/// <summary>
/// Schema Registry rule kind.
/// </summary>
public enum SchemaRuleKind
{
    /// <summary>
    /// A transform rule.
    /// </summary>
    Transform,

    /// <summary>
    /// A validation or condition rule.
    /// </summary>
    Condition
}

/// <summary>
/// Schema Registry rule mode.
/// </summary>
public enum SchemaRuleMode
{
    /// <summary>
    /// Upgrade migration rule.
    /// </summary>
    Upgrade,

    /// <summary>
    /// Downgrade migration rule.
    /// </summary>
    Downgrade,

    /// <summary>
    /// Upgrade and downgrade migration rule.
    /// </summary>
    UpDown,

    /// <summary>
    /// Read-side rule.
    /// </summary>
    Read,

    /// <summary>
    /// Write-side rule.
    /// </summary>
    Write,

    /// <summary>
    /// Write and read rule.
    /// </summary>
    WriteRead
}

/// <summary>
/// Schema type enumeration.
/// </summary>
public enum SchemaType
{
    /// <summary>
    /// Apache Avro schema.
    /// </summary>
    Avro,

    /// <summary>
    /// JSON Schema.
    /// </summary>
    Json,

    /// <summary>
    /// Protocol Buffers schema.
    /// </summary>
    Protobuf
}

/// <summary>
/// Reference to another schema.
/// </summary>
public sealed class SchemaReference
{
    /// <summary>
    /// The name of the reference.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The subject the reference points to.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// The version of the referenced schema.
    /// </summary>
    public required int Version { get; init; }
}
