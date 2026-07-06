namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Configuration for the Protobuf Schema Registry deserializer.
/// </summary>
public sealed class ProtobufDeserializerConfig
{
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
