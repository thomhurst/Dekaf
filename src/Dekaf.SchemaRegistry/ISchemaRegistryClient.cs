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
