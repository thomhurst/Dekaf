namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Configuration for the Protobuf Schema Registry serializer.
/// </summary>
public sealed class ProtobufSerializerConfig
{
    /// <summary>
    /// Strategy for determining the subject name.
    /// Default is <see cref="SubjectNameStrategy.TopicName"/>.
    /// This is ignored if <see cref="CustomSubjectNameStrategy"/> is set.
    /// </summary>
    public SubjectNameStrategy SubjectNameStrategy { get; init; } = SubjectNameStrategy.TopicName;

    /// <summary>
    /// A custom subject name strategy implementation. When set, this takes precedence
    /// over the <see cref="SubjectNameStrategy"/> enum value.
    /// Default is null (uses enum-based strategy).
    /// </summary>
    public ISubjectNameStrategy? CustomSubjectNameStrategy { get; init; }

    /// <summary>
    /// Whether to auto-register schemas when producing messages.
    /// Default is true.
    /// </summary>
    public bool AutoRegisterSchemas { get; init; } = true;

    /// <summary>
    /// Whether to use the latest schema version from the registry instead of the schema
    /// derived from the .NET type. This is useful when the writer schema should come
    /// from the registry rather than from code.
    /// Default is false.
    /// </summary>
    public bool UseLatestVersion { get; init; }

    /// <summary>
    /// Whether to use the deprecated subject naming format (without -key/-value suffix for RecordName strategy).
    /// Default is false.
    /// </summary>
    public bool UseDeprecatedFormat { get; init; }

    /// <summary>
    /// Whether to skip known types when serializing.
    /// Default is false.
    /// </summary>
    public bool SkipKnownTypes { get; init; }

    /// <summary>
    /// Whether to include references to dependent schemas when registering.
    /// Default is true.
    /// </summary>
    public bool UseSchemaReferences { get; init; } = true;

    /// <summary>
    /// Reference subject name strategy for dependent schemas.
    /// Default is <see cref="ReferenceSubjectNameStrategy.ReferenceName"/>.
    /// </summary>
    public ReferenceSubjectNameStrategy ReferenceSubjectNameStrategy { get; init; } = ReferenceSubjectNameStrategy.ReferenceName;
}

/// <summary>
/// Strategy for determining the subject name for schema references.
/// </summary>
public enum ReferenceSubjectNameStrategy
{
    /// <summary>
    /// Use the reference name as the subject name.
    /// </summary>
    ReferenceName,

    /// <summary>
    /// Use the qualified record name as the subject name.
    /// </summary>
    QualifiedRecordName
}
