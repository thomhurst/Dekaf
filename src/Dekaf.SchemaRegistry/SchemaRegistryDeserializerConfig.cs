namespace Dekaf.SchemaRegistry;

/// <summary>
/// Subject-name configuration used when deserialization rules execute.
/// </summary>
public sealed class SchemaRegistryDeserializerConfig
{
    /// <summary>
    /// Whether to use the latest registered subject version as the reader schema and execute
    /// any migration rules between the writer and reader versions.
    /// </summary>
    public bool UseLatestVersion { get; init; }

    /// <summary>
    /// Strategy used to reconstruct the subject associated with the consumed schema.
    /// </summary>
    public SubjectNameStrategy SubjectNameStrategy { get; init; } = SubjectNameStrategy.TopicName;

    /// <summary>
    /// Custom subject-name strategy. When set, this takes precedence over
    /// <see cref="SubjectNameStrategy" />.
    /// </summary>
    public ISubjectNameStrategy? CustomSubjectNameStrategy { get; init; }

    /// <summary>
    /// Whether record-based strategies retain Dekaf's legacy -key/-value suffix.
    /// </summary>
    public bool UseLegacySubjectNames { get; init; }
}
