using System.Runtime.CompilerServices;
using AvroSchema = global::Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.SchemaRegistry.Avro;

internal static class AvroValidationConfiguration
{
    internal static AvroInlineRuleValidatorProvider? Create(
        ValidationRulesExecution execution,
        ISchemaRegistryRuleExecutor? ruleExecutor)
    {
        if (!Enum.IsDefined(execution))
        {
            throw new ArgumentOutOfRangeException(
                nameof(execution),
                execution,
                "Unsupported inline validation execution mode.");
        }
        if (execution == ValidationRulesExecution.Disabled)
            return null;
        if (ruleExecutor is not null and not SchemaRegistryRuleExecutor &&
            !ReferenceEquals(ruleExecutor, SchemaRegistryMigrationRunner.MarkerRuleExecutor))
        {
            throw new NotSupportedException(
                "Inline validation rules require SchemaRegistryRuleExecutor so domain and encoding rule boundaries are known.");
        }
        return new AvroInlineRuleValidatorProvider();
    }
}

internal sealed class AvroInlineRuleValidatorProvider : IInlineValidationRuleExecutor
{
    private readonly ConditionalWeakTable<AvroSchema, AvroInlineRuleValidator> _schemas = new();
    private readonly ConditionalWeakTable<RegistrySchema, AvroInlineRuleValidator> _registeredSchemas = new();
    private SchemaValidatorCacheEntry? _lastSchema;

    internal AvroInlineRuleValidator Get(AvroSchema schema) =>
        _schemas.GetValue(schema, static value => new AvroInlineRuleValidator(value));

    internal AvroInlineRuleValidator Register(RegistrySchema registrySchema, AvroSchema avroSchema)
    {
        if (_registeredSchemas.TryGetValue(registrySchema, out var existing))
            return existing;
        return RegisterSlow(registrySchema, avroSchema);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private AvroInlineRuleValidator RegisterSlow(RegistrySchema registrySchema, AvroSchema avroSchema)
    {
        var validator = Get(avroSchema);
        try
        {
            _registeredSchemas.Add(registrySchema, validator);
        }
        catch (ArgumentException)
        {
            validator = _registeredSchemas.GetValue(
                registrySchema,
                static _ => throw new InvalidOperationException("Registered Avro validator cache changed unexpectedly."));
        }
        return validator;
    }

    void IInlineValidationRuleExecutor.Validate(
        ReadOnlyMemory<byte> payload,
        int schemaId,
        string? subject,
        RegistrySchema schema,
        bool failFast)
    {
        var cached = Volatile.Read(ref _lastSchema);
        var validator = cached is { SchemaId: var cachedSchemaId } && cachedSchemaId == schemaId
            ? cached.Validator
            : Resolve(schemaId, schema);
        validator.Validate(payload, schemaId, failFast);
    }

    private AvroInlineRuleValidator Resolve(int schemaId, RegistrySchema schema)
    {
        if (schema.SchemaType != SchemaType.Avro)
        {
            throw new SchemaRegistryRuleException(
                $"Schema {schemaId} is not an Avro schema (type: {schema.SchemaType}).");
        }
        var validator = _registeredSchemas.GetValue(
            schema,
            static value => new AvroInlineRuleValidator(AvroSchema.Parse(value.SchemaString)));
        Volatile.Write(ref _lastSchema, new SchemaValidatorCacheEntry(schemaId, validator));
        return validator;
    }

    private sealed record SchemaValidatorCacheEntry(int SchemaId, AvroInlineRuleValidator Validator);
}
