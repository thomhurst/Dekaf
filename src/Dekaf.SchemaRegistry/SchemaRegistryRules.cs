using Dekaf.Serialization;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Schema Registry payload format exposed to rule executors.
/// </summary>
public enum SchemaRegistryPayloadFormat
{
    /// <summary>
    /// Custom Schema Registry serializer payload.
    /// </summary>
    Custom,

    /// <summary>
    /// JSON Schema payload.
    /// </summary>
    Json,

    /// <summary>
    /// Avro binary payload.
    /// </summary>
    Avro,

    /// <summary>
    /// Protobuf message payload.
    /// </summary>
    Protobuf
}

/// <summary>
/// Context passed to Schema Registry payload rule executors.
/// </summary>
public sealed class SchemaRegistryRuleContext
{
    /// <summary>
    /// Gets the Kafka topic.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Gets whether the payload is for the key or value component.
    /// </summary>
    public required SerializationComponent Component { get; init; }

    /// <summary>
    /// Gets the Schema Registry schema ID from the wire envelope.
    /// </summary>
    public required int SchemaId { get; init; }

    /// <summary>
    /// Gets the Schema Registry subject when known.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Gets the Schema Registry schema when available. This can be <see langword="null" />
    /// when a deserializer skips schema validation or when the subject is unknown.
    /// </summary>
    public Schema? Schema { get; init; }

    /// <summary>
    /// Gets the codec payload format.
    /// </summary>
    public required SchemaRegistryPayloadFormat PayloadFormat { get; init; }
}

/// <summary>
/// Executes Schema Registry payload rules such as encryption or other data-contract transforms.
/// </summary>
/// <remarks>
/// Implementations receive only the codec payload bytes. The Schema Registry magic byte,
/// schema ID, and Protobuf message-index prefix remain owned by the serializer.
/// </remarks>
public interface ISchemaRegistryRuleExecutor
{
    /// <summary>
    /// Transforms the codec payload immediately before it is written to the Schema Registry wire envelope.
    /// </summary>
    /// <remarks>
    /// The <paramref name="payload" /> memory is valid only for the synchronous duration of this call.
    /// Implementations that retain the bytes or use them after returning must copy the payload.
    /// </remarks>
    ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context);

    /// <summary>
    /// Transforms the codec payload immediately after it is read from the Schema Registry wire envelope.
    /// </summary>
    /// <remarks>
    /// The <paramref name="payload" /> memory is valid only for the synchronous duration of this call.
    /// Implementations that retain the bytes or use them after returning must copy the payload.
    /// The <see cref="SchemaRegistryRuleContext.Schema" /> property can be <see langword="null" />
    /// when deserializer schema validation is skipped.
    /// </remarks>
    ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context);
}
