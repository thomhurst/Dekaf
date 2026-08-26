using System.Runtime.CompilerServices;
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
    /// Validate serialized JSON after domain rules and before encoding rules are applied.
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

    /// <summary>
    /// Validates a memory-backed UTF-8 JSON payload. The default implementation forwards to the span overload.
    /// </summary>
    /// <param name="payload">The plaintext JSON payload without Schema Registry framing.</param>
    /// <param name="schemaId">The Schema Registry schema ID carried by the message.</param>
    void Validate(ReadOnlyMemory<byte> payload, int schemaId) => Validate(payload.Span, schemaId);

    /// <summary>
    /// Evaluates inline <c>confluent:rules</c> CHECK constraints.
    /// </summary>
    /// <param name="payload">The plaintext UTF-8 JSON payload.</param>
    /// <param name="schemaId">The Schema Registry schema ID.</param>
    /// <param name="failFast">Whether to stop after the first violation.</param>
    void ValidateRules(ReadOnlyMemory<byte> payload, int schemaId, bool failFast) =>
        throw new NotSupportedException("This JSON Schema validator does not support inline validation rules.");
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

    /// <summary>
    /// Selects when inline <c>confluent:rules</c> constraints run. Default is disabled.
    /// </summary>
    public ValidationRulesExecution ValidationRulesExecution { get; init; }

    /// <summary>
    /// Stops the inline schema walk after its first violation when enabled.
    /// </summary>
    public bool ValidationRulesFailFast { get; init; }

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

    internal IJsonSchemaValidatorFactory? GetValidationRulesFactory(
        ISchemaRegistryRuleExecutor? ruleExecutor = null)
    {
        ValidateMode();
        ValidateRulesExecution();

        if (ValidationRulesExecution == ValidationRulesExecution.Disabled)
            return null;
        if (ruleExecutor is not null and not SchemaRegistryRuleExecutor &&
            !ReferenceEquals(ruleExecutor, SchemaRegistryMigrationRunner.MarkerRuleExecutor))
        {
            throw new NotSupportedException(
                "Inline validation rules require SchemaRegistryRuleExecutor so domain and encoding rule boundaries are known.");
        }
        return GetValidatorFactory();
    }

    private void ValidateRulesExecution()
    {
        if (!Enum.IsDefined(ValidationRulesExecution))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ValidationRulesExecution),
                ValidationRulesExecution,
                "Unsupported inline validation execution mode.");
        }
    }

    private IJsonSchemaValidatorFactory GetValidatorFactory() => ValidatorFactory
        ?? throw new InvalidOperationException("A JSON Schema validator factory is required.");

    private void ValidateMode()
    {
        if ((Mode & ~JsonSchemaValidationMode.Both) != 0)
            throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unsupported JSON Schema validation mode.");
    }
}

internal sealed class JsonSchemaValidationPipelineFactory : IJsonSchemaValidatorFactory
{
    private readonly IJsonSchemaValidatorFactory? _schemaValidationFactory;
    private readonly IJsonSchemaValidatorFactory _rulesFactory;
    private readonly ValidationRulesExecution _execution;
    private readonly bool _failFast;
    private readonly ConditionalWeakTable<Schema, IJsonSchemaValidator> _validators = new();
    private readonly ConditionalWeakTable<Schema, IJsonSchemaValidator>.CreateValueCallback _createValidator;

    internal JsonSchemaValidationPipelineFactory(
        IJsonSchemaValidatorFactory? schemaValidationFactory,
        IJsonSchemaValidatorFactory rulesFactory,
        ValidationRulesExecution execution,
        bool failFast)
    {
        _schemaValidationFactory = schemaValidationFactory;
        _rulesFactory = rulesFactory;
        _execution = execution;
        _failFast = failFast;
        _createValidator = CreateValidator;
    }

    public IJsonSchemaValidator GetOrCreate(Schema schema) =>
        _validators.GetValue(schema, _createValidator);

    private IJsonSchemaValidator CreateValidator(Schema schema) =>
        new JsonSchemaValidationPipelineValidator(
            _schemaValidationFactory?.GetOrCreate(schema),
            _rulesFactory.GetOrCreate(schema),
            _execution,
            _failFast);
}

internal readonly struct JsonInlineValidationRuleExecutor(IJsonSchemaValidatorFactory factory)
    : IInlineValidationRuleExecutor
{
    public void Validate(ReadOnlyMemory<byte> payload, int schemaId, Schema schema, bool failFast) =>
        factory.GetOrCreate(schema).ValidateRules(payload, schemaId, failFast);
}

internal sealed class JsonSchemaValidationPipelineValidator(
    IJsonSchemaValidator? schemaValidator,
    IJsonSchemaValidator rulesValidator,
    ValidationRulesExecution execution,
    bool failFast) : IJsonSchemaValidator
{
    public void Validate(ReadOnlySpan<byte> payload, int schemaId) =>
        throw new NotSupportedException("The validation pipeline requires a memory-backed JSON payload.");

    public void ValidateRules(ReadOnlyMemory<byte> payload, int schemaId, bool validationFailFast) =>
        rulesValidator.ValidateRules(payload, schemaId, validationFailFast);

    public void Validate(ReadOnlyMemory<byte> payload, int schemaId)
    {
        if (execution == ValidationRulesExecution.BeforeDomainRules)
            rulesValidator.ValidateRules(payload, schemaId, failFast);
        schemaValidator?.Validate(payload.Span, schemaId);
        if (execution == ValidationRulesExecution.AfterDomainRules)
            rulesValidator.ValidateRules(payload, schemaId, failFast);
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
