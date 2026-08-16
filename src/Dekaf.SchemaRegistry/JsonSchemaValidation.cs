using Dekaf.Errors;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// Selects where JSON Schema validation is applied.
/// </summary>
[Flags]
public enum JsonSchemaValidationMode
{
    /// <summary>
    /// Do not validate JSON payloads.
    /// </summary>
    None = 0,

    /// <summary>
    /// Validate serialized JSON before Schema Registry rules are applied.
    /// </summary>
    Serialize = 1,

    /// <summary>
    /// Validate deserialized JSON after Schema Registry rules are applied.
    /// </summary>
    Deserialize = 2,

    /// <summary>
    /// Validate both serialized and deserialized JSON.
    /// </summary>
    Both = Serialize | Deserialize
}

/// <summary>
/// Creates compiled JSON Schema validators.
/// </summary>
public interface IJsonSchemaValidatorFactory
{
    /// <summary>
    /// Gets or creates a compiled validator for a schema.
    /// </summary>
    /// <param name="schema">The Schema Registry JSON schema.</param>
    /// <returns>A reusable validator.</returns>
    IJsonSchemaValidator GetOrCreate(Schema schema);
}

/// <summary>
/// Validates JSON payloads against one compiled schema.
/// </summary>
public interface IJsonSchemaValidator
{
    /// <summary>
    /// Validates a UTF-8 JSON payload.
    /// </summary>
    /// <param name="payload">The plaintext JSON payload without Schema Registry framing.</param>
    /// <param name="schemaId">The Schema Registry schema ID carried by the message.</param>
    void Validate(ReadOnlySpan<byte> payload, int schemaId);
}

/// <summary>
/// Configures optional JSON Schema validation.
/// </summary>
public sealed class JsonSchemaValidationOptions
{
    /// <summary>
    /// Validation locations. Default is both serialization and deserialization.
    /// </summary>
    public JsonSchemaValidationMode Mode { get; init; } = JsonSchemaValidationMode.Both;

    /// <summary>
    /// Factory that compiles and caches JSON Schema validators.
    /// </summary>
    public required IJsonSchemaValidatorFactory ValidatorFactory { get; init; }

    internal IJsonSchemaValidatorFactory? GetSerializerFactory()
    {
        ValidateMode();
        return (Mode & JsonSchemaValidationMode.Serialize) != 0
            ? GetValidatorFactory()
            : null;
    }

    internal IJsonSchemaValidatorFactory? GetDeserializerFactory()
    {
        ValidateMode();
        return (Mode & JsonSchemaValidationMode.Deserialize) != 0
            ? GetValidatorFactory()
            : null;
    }

    private IJsonSchemaValidatorFactory GetValidatorFactory() => ValidatorFactory
        ?? throw new InvalidOperationException("A JSON Schema validator factory is required.");

    private void ValidateMode()
    {
        if ((Mode & ~JsonSchemaValidationMode.Both) != 0)
            throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unsupported JSON Schema validation mode.");
    }
}

/// <summary>
/// Exception thrown when a JSON payload does not satisfy its registered schema.
/// </summary>
public sealed class JsonSchemaValidationException : KafkaException
{
    /// <summary>
    /// Creates a JSON Schema validation exception without including payload contents.
    /// </summary>
    public JsonSchemaValidationException(
        int schemaId,
        string keyword,
        string jsonPath,
        string message,
        Exception? innerException = null)
        : base(message, innerException!)
    {
        SchemaId = schemaId;
        Keyword = keyword ?? throw new ArgumentNullException(nameof(keyword));
        JsonPath = jsonPath ?? throw new ArgumentNullException(nameof(jsonPath));
    }

    /// <summary>
    /// Schema Registry schema ID used for validation.
    /// </summary>
    public int SchemaId { get; }

    /// <summary>
    /// JSON Schema keyword that rejected the payload.
    /// </summary>
    public string Keyword { get; }

    /// <summary>
    /// JSON path of the rejected value.
    /// </summary>
    public string JsonPath { get; }
}
