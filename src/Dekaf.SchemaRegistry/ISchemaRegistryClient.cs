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
    Task<int> RegisterSchemaAsync(
        string subject,
        Schema schema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a schema under a subject, optionally requesting Schema Registry normalization.
    /// </summary>
    /// <param name="subject">The subject name (typically topic-key or topic-value).</param>
    /// <param name="schema">The schema to register.</param>
    /// <param name="normalize">Whether to request Schema Registry normalization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema ID.</returns>
    Task<int> RegisterSchemaAsync(
        string subject,
        Schema schema,
        bool normalize,
        CancellationToken cancellationToken = default)
        => RegisterSchemaAsync(subject, schema, cancellationToken);

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
    Task<int> GetOrRegisterSchemaAsync(
        string subject,
        Schema schema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or registers a schema, optionally requesting Schema Registry normalization.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="schema">The schema.</param>
    /// <param name="normalize">Whether to request Schema Registry normalization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schema ID.</returns>
    Task<int> GetOrRegisterSchemaAsync(
        string subject,
        Schema schema,
        bool normalize,
        CancellationToken cancellationToken = default)
        => GetOrRegisterSchemaAsync(subject, schema, cancellationToken);

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
    Task<bool> IsCompatibleAsync(
        string subject,
        Schema schema,
        string version = "latest",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks schema compatibility, optionally requesting Schema Registry normalization.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="schema">The schema to check.</param>
    /// <param name="version">The version to check against.</param>
    /// <param name="normalize">Whether to request Schema Registry normalization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if compatible.</returns>
    Task<bool> IsCompatibleAsync(
        string subject,
        Schema schema,
        string version,
        bool normalize,
        CancellationToken cancellationToken = default)
        => IsCompatibleAsync(subject, schema, version, cancellationToken);

    /// <summary>
    /// Deletes a subject and all associated schemas.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="permanent">If true, permanently delete (hard delete).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of deleted version numbers.</returns>
    Task<IReadOnlyList<int>> DeleteSubjectAsync(string subject, bool permanent = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Schema Registry Key Encryption Key (KEK) names.
    /// </summary>
    /// <param name="deleted">Whether to include soft-deleted KEKs.</param>
    /// <param name="offset">Pagination offset.</param>
    /// <param name="limit">Pagination size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of KEK names.</returns>
    Task<IReadOnlyList<string>> GetKekNamesAsync(
        bool deleted = false,
        int? offset = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Schema Registry client does not support DEK Registry KEK operations.");

    /// <summary>
    /// Registers a Schema Registry Key Encryption Key (KEK).
    /// </summary>
    /// <param name="request">KEK registration request.</param>
    /// <param name="testSharing">Whether Schema Registry should test KEK sharing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registered KEK.</returns>
    Task<Kek> RegisterKekAsync(
        RegisterKekRequest request,
        bool testSharing = false,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Schema Registry client does not support DEK Registry KEK operations.");

    /// <summary>
    /// Gets a Schema Registry Key Encryption Key (KEK) by name.
    /// </summary>
    /// <param name="name">KEK name.</param>
    /// <param name="deleted">Whether to include soft-deleted KEKs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The KEK.</returns>
    Task<Kek> GetKekAsync(
        string name,
        bool deleted = false,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Schema Registry client does not support DEK Registry KEK operations.");

    /// <summary>
    /// Deletes a Schema Registry Key Encryption Key (KEK).
    /// </summary>
    /// <param name="name">KEK name.</param>
    /// <param name="permanent">Whether to permanently delete the KEK.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteKekAsync(
        string name,
        bool permanent = false,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Schema Registry client does not support DEK Registry KEK operations.");

    /// <summary>
    /// Lists Data Encryption Key (DEK) subjects for a KEK.
    /// </summary>
    /// <param name="kekName">KEK name.</param>
    /// <param name="deleted">Whether to include soft-deleted DEKs.</param>
    /// <param name="offset">Pagination offset.</param>
    /// <param name="limit">Pagination size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of DEK subjects.</returns>
    Task<IReadOnlyList<string>> GetDekSubjectsAsync(
        string kekName,
        bool deleted = false,
        int? offset = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Schema Registry client does not support DEK Registry DEK operations.");

    /// <summary>
    /// Registers a Data Encryption Key (DEK) under a KEK.
    /// </summary>
    /// <param name="kekName">KEK name.</param>
    /// <param name="request">DEK registration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registered DEK.</returns>
    Task<Dek> RegisterDekAsync(
        string kekName,
        RegisterDekRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Schema Registry client does not support DEK Registry DEK operations.");

    /// <summary>
    /// Gets the latest Data Encryption Key (DEK) for a KEK and subject.
    /// </summary>
    /// <param name="kekName">KEK name.</param>
    /// <param name="subject">DEK subject.</param>
    /// <param name="algorithm">Optional DEK algorithm filter.</param>
    /// <param name="deleted">Whether to include soft-deleted DEKs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The DEK.</returns>
    Task<Dek> GetDekAsync(
        string kekName,
        string subject,
        DekAlgorithm? algorithm = null,
        bool deleted = false,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Schema Registry client does not support DEK Registry DEK operations.");

    /// <summary>
    /// Gets a Data Encryption Key (DEK) by version.
    /// </summary>
    /// <param name="kekName">KEK name.</param>
    /// <param name="subject">DEK subject.</param>
    /// <param name="version">DEK version.</param>
    /// <param name="deleted">Whether to include soft-deleted DEKs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The DEK.</returns>
    Task<Dek> GetDekAsync(
        string kekName,
        string subject,
        int version,
        bool deleted = false,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Schema Registry client does not support DEK Registry DEK operations.");

    /// <summary>
    /// Lists Data Encryption Key (DEK) versions for a KEK and subject.
    /// </summary>
    /// <param name="kekName">KEK name.</param>
    /// <param name="subject">DEK subject.</param>
    /// <param name="algorithm">Optional DEK algorithm filter.</param>
    /// <param name="deleted">Whether to include soft-deleted DEKs.</param>
    /// <param name="offset">Pagination offset.</param>
    /// <param name="limit">Pagination size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of DEK versions.</returns>
    Task<IReadOnlyList<int>> GetDekVersionsAsync(
        string kekName,
        string subject,
        DekAlgorithm? algorithm = null,
        bool deleted = false,
        int? offset = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Schema Registry client does not support DEK Registry DEK operations.");

    /// <summary>
    /// Deletes all versions of a Data Encryption Key (DEK).
    /// </summary>
    /// <param name="kekName">KEK name.</param>
    /// <param name="subject">DEK subject.</param>
    /// <param name="algorithm">Optional DEK algorithm filter.</param>
    /// <param name="permanent">Whether to permanently delete the DEK.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteDekAsync(
        string kekName,
        string subject,
        DekAlgorithm? algorithm = null,
        bool permanent = false,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Schema Registry client does not support DEK Registry DEK operations.");

    /// <summary>
    /// Deletes one version of a Data Encryption Key (DEK).
    /// </summary>
    /// <param name="kekName">KEK name.</param>
    /// <param name="subject">DEK subject.</param>
    /// <param name="version">DEK version.</param>
    /// <param name="algorithm">Optional DEK algorithm filter.</param>
    /// <param name="permanent">Whether to permanently delete the DEK.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteDekVersionAsync(
        string kekName,
        string subject,
        int version,
        DekAlgorithm? algorithm = null,
        bool permanent = false,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This Schema Registry client does not support DEK Registry DEK operations.");
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
/// Schema Registry Key Encryption Key (KEK).
/// </summary>
public sealed class Kek
{
    /// <summary>
    /// KEK name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// KMS provider type, for example aws-kms, azure-kv, or gcp-kms.
    /// </summary>
    public required string KmsType { get; init; }

    /// <summary>
    /// Provider-specific KMS key identifier.
    /// </summary>
    public required string KmsKeyId { get; init; }

    /// <summary>
    /// Provider-specific KMS properties.
    /// </summary>
    public IReadOnlyDictionary<string, string>? KmsProps { get; init; }

    /// <summary>
    /// Optional KEK documentation.
    /// </summary>
    public string? Doc { get; init; }

    /// <summary>
    /// Whether this KEK is shared across multiple subjects.
    /// </summary>
    public bool Shared { get; init; }

    /// <summary>
    /// Whether this KEK has been soft-deleted.
    /// </summary>
    public bool Deleted { get; init; }

    /// <summary>
    /// Schema Registry timestamp for this KEK, when returned by the server.
    /// </summary>
    public long? Timestamp { get; init; }
}

/// <summary>
/// Request used to register a Schema Registry Key Encryption Key (KEK).
/// </summary>
public sealed class RegisterKekRequest
{
    /// <summary>
    /// KEK name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// KMS provider type, for example aws-kms, azure-kv, or gcp-kms.
    /// </summary>
    public required string KmsType { get; init; }

    /// <summary>
    /// Provider-specific KMS key identifier.
    /// </summary>
    public required string KmsKeyId { get; init; }

    /// <summary>
    /// Provider-specific KMS properties.
    /// </summary>
    public IReadOnlyDictionary<string, string>? KmsProps { get; init; }

    /// <summary>
    /// Optional KEK documentation.
    /// </summary>
    public string? Doc { get; init; }

    /// <summary>
    /// Whether this KEK is shared across multiple subjects.
    /// </summary>
    public bool? Shared { get; init; }

    /// <summary>
    /// Whether this KEK should be registered as deleted.
    /// </summary>
    public bool? Deleted { get; init; }
}

/// <summary>
/// Schema Registry Data Encryption Key (DEK) algorithm.
/// </summary>
public enum DekAlgorithm
{
    /// <summary>
    /// Unknown or server-specific algorithm.
    /// </summary>
    Unknown,

    /// <summary>
    /// AES-128 GCM.
    /// </summary>
    Aes128Gcm,

    /// <summary>
    /// AES-256 GCM.
    /// </summary>
    Aes256Gcm,

    /// <summary>
    /// AES-256 SIV.
    /// </summary>
    Aes256Siv
}

/// <summary>
/// Schema Registry Data Encryption Key (DEK).
/// </summary>
public sealed class Dek
{
    /// <summary>
    /// KEK name associated with this DEK.
    /// </summary>
    public required string KekName { get; init; }

    /// <summary>
    /// Subject associated with this DEK.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// DEK version.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// Encryption algorithm used by this DEK.
    /// </summary>
    public DekAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Encrypted DEK material, base64 encoded by Schema Registry.
    /// </summary>
    public string? EncryptedKeyMaterial { get; init; }

    /// <summary>
    /// Raw DEK material when the server returns it.
    /// </summary>
    public string? KeyMaterial { get; init; }

    /// <summary>
    /// Whether this DEK has been soft-deleted.
    /// </summary>
    public bool Deleted { get; init; }

    /// <summary>
    /// Schema Registry timestamp for this DEK, when returned by the server.
    /// </summary>
    public long? Timestamp { get; init; }
}

/// <summary>
/// Request used to register a Schema Registry Data Encryption Key (DEK).
/// </summary>
public sealed class RegisterDekRequest
{
    /// <summary>
    /// Subject associated with this DEK.
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Optional explicit DEK version.
    /// </summary>
    public int? Version { get; init; }

    /// <summary>
    /// Optional DEK algorithm. When omitted, Schema Registry chooses its default.
    /// </summary>
    public DekAlgorithm? Algorithm { get; init; }

    /// <summary>
    /// Optional encrypted DEK material.
    /// </summary>
    public string? EncryptedKeyMaterial { get; init; }

    /// <summary>
    /// Whether this DEK should be registered as deleted.
    /// </summary>
    public bool? Deleted { get; init; }
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
