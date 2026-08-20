namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Configuration for the Protobuf Schema Registry deserializer.
/// </summary>
public sealed class ProtobufDeserializerConfig
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
    /// Whether to read the deprecated unsigned Protobuf message-index encoding.
    /// Default is false.
    /// </summary>
    public bool UseDeprecatedFormat { get; init; }

    /// <summary>
    /// Whether to skip schema validation on deserialization.
    /// When true, the schema ID is read from the message but the schema is not fetched.
    /// Default is false.
    /// </summary>
    public bool SkipSchemaValidation { get; init; }

    /// <summary>
    /// Optional rule executor applied to Protobuf message bytes after the Schema Registry envelope is read.
    /// The Protobuf message-index prefix is not transformed.
    /// </summary>
    public ISchemaRegistryRuleExecutor? RuleExecutor { get; init; }
}
