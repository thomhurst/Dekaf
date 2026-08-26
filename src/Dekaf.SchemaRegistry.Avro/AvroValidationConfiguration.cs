using System.Runtime.CompilerServices;
using AvroSchema = global::Avro.Schema;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.SchemaRegistry.Avro;

internal static class AvroValidationConfiguration
{
    internal static AvroInlineRuleValidatorProvider? Create(
        ISchemaRegistryClient schemaRegistry,
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
        return new AvroInlineRuleValidatorProvider(schemaRegistry);
    }
}

internal sealed class AvroInlineRuleValidatorProvider : IInlineValidationRuleExecutor
{
    private readonly ConditionalWeakTable<AvroSchema, AvroInlineRuleValidator> _schemas = new();
    private readonly ConditionalWeakTable<RegistrySchema, AvroInlineRuleValidator> _registeredSchemas = new();
    private readonly ConditionalWeakTable<RegistrySchema, AvroSchema> _resolvedSchemas = new();
    private readonly ISchemaRegistryClient? _schemaRegistry;
    private SchemaValidatorCacheEntry? _lastSchema;

    internal AvroInlineRuleValidatorProvider(ISchemaRegistryClient? schemaRegistry = null) =>
        _schemaRegistry = schemaRegistry;

    internal AvroInlineRuleValidator Get(AvroSchema schema) =>
        _schemas.GetValue(schema, static value => new AvroInlineRuleValidator(value));

    internal AvroInlineRuleValidator Register(RegistrySchema registrySchema, AvroSchema resolvedSchema)
    {
        if (_registeredSchemas.TryGetValue(registrySchema, out var existing))
            return existing;
        CacheResolvedSchema(registrySchema, resolvedSchema);
        return RegisterSlow(registrySchema, resolvedSchema);
    }

    internal AvroSchema GetResolvedSchema(RegistrySchema registrySchema)
    {
        if (_resolvedSchemas.TryGetValue(registrySchema, out var resolved))
            return resolved;
        if (_schemaRegistry is null)
        {
            throw new SchemaRegistryRuleException(
                "Could not resolve registered Avro schema references without a Schema Registry client.");
        }

        resolved = Poco.AvroSchemaReferenceResolver.Parse(
            _schemaRegistry,
            registrySchema,
            TimeSpan.FromSeconds(30));
        _ = Register(registrySchema, resolved);
        return _resolvedSchemas.GetValue(
            registrySchema,
            static _ => throw new InvalidOperationException("Resolved Avro schema cache changed unexpectedly."));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private AvroInlineRuleValidator RegisterSlow(
        RegistrySchema registrySchema,
        AvroSchema resolvedSchema)
    {
        var validator = new AvroInlineRuleValidator(ParseRegisteredSchema(registrySchema, resolvedSchema));
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
            : ResolveValidator(schemaId, schema);
        validator.Validate(payload, schemaId, failFast);
    }

    private AvroInlineRuleValidator ResolveValidator(int schemaId, RegistrySchema schema)
    {
        if (schema.SchemaType != SchemaType.Avro)
        {
            throw new SchemaRegistryRuleException(
                $"Schema {schemaId} is not an Avro schema (type: {schema.SchemaType}).");
        }
        if (!_registeredSchemas.TryGetValue(schema, out var validator))
            validator = Register(schema, GetResolvedSchema(schema));
        Volatile.Write(ref _lastSchema, new SchemaValidatorCacheEntry(schemaId, validator));
        return validator;
    }

    private void CacheResolvedSchema(RegistrySchema registrySchema, AvroSchema resolvedSchema)
    {
        if (_resolvedSchemas.TryGetValue(registrySchema, out _))
            return;
        try
        {
            _resolvedSchemas.Add(registrySchema, resolvedSchema);
        }
        catch (ArgumentException)
        {
            _ = _resolvedSchemas.GetValue(
                registrySchema,
                static _ => throw new InvalidOperationException("Resolved Avro schema cache changed unexpectedly."));
        }
    }

    private static AvroSchema ParseRegisteredSchema(
        RegistrySchema registrySchema,
        AvroSchema resolvedSchema)
    {
        if (registrySchema.References is not { Count: > 0 } references)
            return AvroSchema.Parse(registrySchema.SchemaString);

        var names = new global::Avro.SchemaNames();
        for (var index = 0; index < references.Count; index++)
        {
            var visited = new HashSet<AvroSchema>(AvroSchemaReferenceComparer.Instance);
            var referenced = FindNamedSchema(resolvedSchema, references[index].Name, visited);
            if (referenced is not null)
                _ = names.Add(referenced);
        }
        return AvroSchema.Parse(registrySchema.SchemaString, names);
    }

    private static global::Avro.NamedSchema? FindNamedSchema(
        AvroSchema schema,
        string fullName,
        HashSet<AvroSchema> visited)
    {
        if (!visited.Add(schema))
            return null;
        if (schema is global::Avro.NamedSchema named &&
            string.Equals(named.Fullname, fullName, StringComparison.Ordinal))
        {
            return named;
        }

        return schema switch
        {
            global::Avro.LogicalSchema logical => FindNamedSchema(logical.BaseSchema, fullName, visited),
            global::Avro.RecordSchema record => FindNamedFieldSchema(record, fullName, visited),
            global::Avro.ArraySchema array => FindNamedSchema(array.ItemSchema, fullName, visited),
            global::Avro.MapSchema map => FindNamedSchema(map.ValueSchema, fullName, visited),
            global::Avro.UnionSchema union => FindNamedUnionSchema(union, fullName, visited),
            _ => null
        };
    }

    private static global::Avro.NamedSchema? FindNamedFieldSchema(
        global::Avro.RecordSchema record,
        string fullName,
        HashSet<AvroSchema> visited)
    {
        for (var index = 0; index < record.Fields.Count; index++)
        {
            var found = FindNamedSchema(record.Fields[index].Schema, fullName, visited);
            if (found is not null)
                return found;
        }
        return null;
    }

    private static global::Avro.NamedSchema? FindNamedUnionSchema(
        global::Avro.UnionSchema union,
        string fullName,
        HashSet<AvroSchema> visited)
    {
        for (var index = 0; index < union.Count; index++)
        {
            var found = FindNamedSchema(union[index], fullName, visited);
            if (found is not null)
                return found;
        }
        return null;
    }

    private sealed record SchemaValidatorCacheEntry(int SchemaId, AvroInlineRuleValidator Validator);
}
