namespace Dekaf.SchemaRegistry.Avro;

/// <summary>
/// Configuration options for the Avro Schema Registry serializer.
/// </summary>
public sealed class AvroSerializerConfig
{
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
    /// Whether to use the latest schema version from the registry instead of the schema
    /// derived from the .NET type. This is useful when the writer schema should come
    /// from the registry rather than from code.
    /// Default is false.
    /// </summary>
    public bool UseLatestVersion { get; init; }

}

/// <summary>
/// Configuration options for the Avro Schema Registry deserializer.
/// </summary>
public sealed class AvroDeserializerConfig
{
    /// <summary>
    /// Whether to use a specific reader schema instead of inferring it from the .NET type.
    /// When null, the reader schema is derived from the type T.
    /// </summary>
    public string? ReaderSchema { get; init; }
}
