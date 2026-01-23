namespace Dekaf.SchemaRegistry.Protobuf;

/// <summary>
/// Configuration for the Protobuf Schema Registry deserializer.
/// </summary>
public sealed class ProtobufDeserializerConfig
{
    /// <summary>
    /// Whether to use the deprecated subject naming format.
    /// Default is false.
    /// </summary>
    public bool UseDeprecatedFormat { get; init; }

    /// <summary>
    /// Whether to skip schema validation on deserialization.
    /// When true, the schema ID is read from the message but the schema is not fetched.
    /// Default is false.
    /// </summary>
    public bool SkipSchemaValidation { get; init; }
}
