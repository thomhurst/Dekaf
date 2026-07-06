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

/// <summary>
/// Direction for a Schema Registry payload rule transform.
/// </summary>
public enum SchemaRegistryRuleDirection
{
    /// <summary>
    /// Rule is running before bytes are written to Kafka.
    /// </summary>
    Write,

    /// <summary>
    /// Rule is running after bytes are read from Kafka.
    /// </summary>
    Read
}

/// <summary>
/// Context passed to a typed Schema Registry rule handler.
/// </summary>
public sealed class SchemaRegistryRuleHandlerContext
{
    /// <summary>
    /// Gets the payload context shared by all rule executors.
    /// </summary>
    public required SchemaRegistryRuleContext PayloadContext { get; init; }

    /// <summary>
    /// Gets the Schema Registry rule being executed.
    /// </summary>
    public required SchemaRule Rule { get; init; }

    /// <summary>
    /// Gets the transform direction.
    /// </summary>
    public required SchemaRegistryRuleDirection Direction { get; init; }
}

/// <summary>
/// Handles one Schema Registry rule type, such as ENCRYPT or CEL.
/// </summary>
public interface ISchemaRegistryRuleHandler
{
    /// <summary>
    /// Gets the Schema Registry rule type this handler supports.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Applies a write-side transform for the rule.
    /// </summary>
    ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context);

    /// <summary>
    /// Applies a read-side transform for the rule.
    /// </summary>
    ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleHandlerContext context);
}

/// <summary>
/// Exception thrown when Schema Registry rule execution fails.
/// </summary>
public sealed class SchemaRegistryRuleException : Exception
{
    /// <summary>
    /// Creates a rule exception.
    /// </summary>
    public SchemaRegistryRuleException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a rule exception with an inner exception.
    /// </summary>
    public SchemaRegistryRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Executes Schema Registry encoding rules by dispatching each active rule to a typed handler.
/// </summary>
/// <remarks>
/// This executor operates on codec payload bytes. It intentionally fails closed when a schema
/// contains an active encoding rule with no registered handler, so protected data is not
/// accidentally produced or consumed without the configured transform.
/// </remarks>
public sealed class SchemaRegistryRuleExecutor : ISchemaRegistryRuleExecutor
{
    private readonly IReadOnlyDictionary<string, ISchemaRegistryRuleHandler> _handlers;

    /// <summary>
    /// Creates a Schema Registry rule executor.
    /// </summary>
    /// <param name="handlers">Rule handlers keyed by <see cref="ISchemaRegistryRuleHandler.Type" />.</param>
    public SchemaRegistryRuleExecutor(IEnumerable<ISchemaRegistryRuleHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var dictionary = new Dictionary<string, ISchemaRegistryRuleHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            if (string.IsNullOrWhiteSpace(handler.Type))
                throw new ArgumentException("Rule handler type cannot be null or whitespace.", nameof(handlers));

            if (!dictionary.TryAdd(handler.Type, handler))
                throw new ArgumentException($"A rule handler for type '{handler.Type}' is already registered.", nameof(handlers));
        }

        _handlers = dictionary;
    }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformSerializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context)
        => ApplyRules(payload, context, SchemaRegistryRuleDirection.Write);

    /// <inheritdoc />
    public ReadOnlyMemory<byte> TransformDeserializedPayload(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context)
        => ApplyRules(payload, context, SchemaRegistryRuleDirection.Read);

    private ReadOnlyMemory<byte> ApplyRules(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext context,
        SchemaRegistryRuleDirection direction)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rules = context.Schema?.RuleSet?.EncodingRules;
        if (rules is null || rules.Count == 0)
            return payload;

        var isWrite = direction == SchemaRegistryRuleDirection.Write;
        var index = isWrite ? 0 : rules.Count - 1;
        var end = isWrite ? rules.Count : -1;
        var step = isWrite ? 1 : -1;

        for (; index != end; index += step)
        {
            var rule = rules[index];
            if (!IsActiveTransformRule(rule, direction))
                continue;

            if (!_handlers.TryGetValue(rule.Type, out var handler))
                throw new SchemaRegistryRuleException(
                    $"No Schema Registry rule handler is registered for rule type '{rule.Type}' (rule '{rule.Name}').");

            payload = ApplyRule(payload, context, rule, handler, direction);
        }

        return payload;
    }

    private static ReadOnlyMemory<byte> ApplyRule(
        ReadOnlyMemory<byte> payload,
        SchemaRegistryRuleContext payloadContext,
        SchemaRule rule,
        ISchemaRegistryRuleHandler handler,
        SchemaRegistryRuleDirection direction)
    {
        var handlerContext = new SchemaRegistryRuleHandlerContext
        {
            PayloadContext = payloadContext,
            Rule = rule,
            Direction = direction
        };

        try
        {
            return direction == SchemaRegistryRuleDirection.Write
                ? handler.TransformSerializedPayload(payload, handlerContext)
                : handler.TransformDeserializedPayload(payload, handlerContext);
        }
        catch (Exception ex) when (ex is not SchemaRegistryRuleException)
        {
            throw new SchemaRegistryRuleException(
                $"Schema Registry rule '{rule.Name}' of type '{rule.Type}' failed during {direction.ToString().ToLowerInvariant()} transform.",
                ex);
        }
    }
    private static bool IsActiveTransformRule(SchemaRule rule, SchemaRegistryRuleDirection direction)
    {
        if (rule.Disabled || rule.Kind != SchemaRuleKind.Transform || string.IsNullOrWhiteSpace(rule.Type))
            return false;

        return direction == SchemaRegistryRuleDirection.Write
            ? rule.Mode is SchemaRuleMode.Write or SchemaRuleMode.WriteRead
            : rule.Mode is SchemaRuleMode.Read or SchemaRuleMode.WriteRead;
    }
}
