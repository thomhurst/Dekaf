namespace Dekaf.SchemaRegistry;

/// <summary>Configuration for JSON Schema Registry serialization.</summary>
public sealed class JsonSchemaSerializerConfig
{
    /// <summary>Whether schemas are registered when an exact match is absent.</summary>
    public bool AutoRegisterSchemas { get; init; } = true;

    /// <summary>Whether to select the latest subject version.</summary>
    public bool UseLatestVersion { get; init; }

    /// <summary>Global Schema Registry ID to select explicitly.</summary>
    public int? UseSchemaId { get; init; }

    /// <summary>Strategy used to carry the selected schema identity.</summary>
    public SchemaIdSerializerStrategy SchemaIdStrategy { get; init; } = SchemaIdSerializerStrategy.Prefix;

    /// <summary>Strategy used to construct the Schema Registry subject.</summary>
    public SubjectNameStrategy SubjectNameStrategy { get; init; } = SubjectNameStrategy.TopicName;

    /// <summary>Custom subject strategy, which takes precedence over <see cref="SubjectNameStrategy"/>.</summary>
    public ISubjectNameStrategy? CustomSubjectNameStrategy { get; init; }

    /// <summary>Whether record-based subject strategies retain Dekaf's legacy suffix.</summary>
    public bool UseLegacySubjectNames { get; init; }

    /// <summary>Whether Schema Registry requests use schema normalization.</summary>
    public bool NormalizeSchemas { get; init; }

    /// <summary>Optional rule executor applied before framing.</summary>
    public ISchemaRegistryRuleExecutor? RuleExecutor { get; init; }
}
