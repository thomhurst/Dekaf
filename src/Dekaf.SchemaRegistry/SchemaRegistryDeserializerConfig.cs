namespace Dekaf.SchemaRegistry;

/// <summary>
/// Subject-name configuration used when deserialization rules execute.
/// </summary>
public sealed class SchemaRegistryDeserializerConfig
{
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
