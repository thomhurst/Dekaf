namespace Dekaf.SchemaRegistry.Avro;

/// <summary>
/// Configuration options for the Avro Schema Registry serializer.
/// </summary>
public sealed class AvroSerializerConfig
{
    /// <summary>
    /// Maximum number of runtime schemas retained strongly by each serializer cache.
    /// Additional runtime schemas use weak exact-reference entries plus a bounded logical overflow
    /// cache, so repeated schema rotations remain reusable without unbounded serializer retention.
    /// Must be greater than zero. Default is 1000.
    /// </summary>
    public int MaxCachedSchemas { get; init; } = 1000;

    /// <summary>
    /// Whether to automatically register schemas with the Schema Registry.
    /// Default is true.
    /// </summary>
    public bool AutoRegisterSchemas { get; init; } = true;

    /// <summary>
    /// The strategy for determining the subject name for schema registration.
    /// Default is TopicName. This is ignored if <see cref="CustomSubjectNameStrategy"/> is set.
    /// </summary>
    public SubjectNameStrategy SubjectNameStrategy { get; init; } = SubjectNameStrategy.TopicName;

    /// <summary>
    /// A custom subject name strategy implementation. When set, this takes precedence
    /// over the <see cref="SubjectNameStrategy"/> enum value.
    /// Default is null (uses enum-based strategy).
    /// </summary>
    public ISubjectNameStrategy? CustomSubjectNameStrategy { get; init; }

    /// <summary>
    /// Whether <see cref="SubjectNameStrategy.RecordName"/> and
    /// <see cref="SubjectNameStrategy.TopicRecordName"/> should retain Dekaf's legacy
    /// -key/-value suffix. Enable this temporarily while migrating existing subjects.
    /// Default is false.
    /// </summary>
    public bool UseLegacySubjectNames { get; init; }

    /// <summary>
    /// Whether to use the latest schema version from the registry instead of the schema
    /// derived from the .NET type. This is useful when the writer schema should come
    /// from the registry rather than from code.
    /// Default is false.
    /// </summary>
    public bool UseLatestVersion { get; init; }

    /// <summary>
    /// Global Schema Registry ID to use instead of registering, looking up, or selecting the latest schema.
    /// </summary>
    public int? UseSchemaId { get; init; }

    /// <summary>
    /// Strategy used to carry the selected schema identity. The default writes the Confluent payload prefix.
    /// </summary>
    public SchemaIdSerializerStrategy SchemaIdStrategy { get; init; } = SchemaIdSerializerStrategy.Prefix;

    /// <summary>
    /// Whether schema registration and lookup requests should include normalize=true.
    /// Default is false.
    /// </summary>
    public bool NormalizeSchemas { get; init; }

    /// <summary>
    /// Optional rule executor applied to Avro payload bytes before the Schema Registry envelope is written.
    /// </summary>
    public ISchemaRegistryRuleExecutor? RuleExecutor { get; init; }
}

/// <summary>
/// Configuration options for the Avro Schema Registry deserializer.
/// </summary>
public sealed class AvroDeserializerConfig
{
    /// <summary>
    /// Strategy used to read the schema identity. The default accepts a GUID header and falls back to the payload prefix.
    /// </summary>
    public SchemaIdDeserializerStrategy SchemaIdStrategy { get; init; } = SchemaIdDeserializerStrategy.Dual;

    /// <summary>
    /// Whether to use the latest registered subject version as the reader schema and execute
    /// any migration rules between the writer and reader versions.
    /// </summary>
    public bool UseLatestVersion { get; init; }

    /// <summary>
    /// Subject-name strategy used when deserialization rules execute.
    /// </summary>
    public SubjectNameStrategy SubjectNameStrategy { get; init; } = SubjectNameStrategy.TopicName;

    /// <summary>
    /// Custom subject-name strategy used when deserialization rules execute.
    /// </summary>
    public ISubjectNameStrategy? CustomSubjectNameStrategy { get; init; }

    /// <summary>
    /// Whether record-based strategies retain Dekaf's legacy -key/-value suffix.
    /// </summary>
    public bool UseLegacySubjectNames { get; init; }

    /// <summary>
    /// Whether to use a specific reader schema instead of inferring it from the .NET type.
    /// When null, the reader schema is derived from the type T.
    /// </summary>
    public string? ReaderSchema { get; init; }

    /// <summary>
    /// Optional rule executor applied to Avro payload bytes after the Schema Registry envelope is read.
    /// </summary>
    public ISchemaRegistryRuleExecutor? RuleExecutor { get; init; }
}
