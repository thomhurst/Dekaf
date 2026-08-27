using System.Globalization;
using AvroSchema = Avro.Schema;
using AvroSchemaNames = Avro.SchemaNames;
using RegistrySchema = Dekaf.SchemaRegistry.Schema;

namespace Dekaf.SchemaRegistry.Avro.Poco;

internal static class AvroSchemaReferenceResolver
{
    private const int MaxReferenceDepth = 128;

    internal static async Task<AvroSchema> ParseAsync(
        ISchemaRegistryClient schemaRegistry,
        RegistrySchema schema,
        CancellationToken cancellationToken)
    {
        if (schema.References is not { Count: > 0 })
            return AvroSchema.Parse(schema.SchemaString);
        var names = await ResolveAsync(schemaRegistry, schema, cancellationToken).ConfigureAwait(false);
        return AvroSchema.Parse(schema.SchemaString, names);
    }

    internal static async Task<AvroSchemaNames> ResolveAsync(
        ISchemaRegistryClient schemaRegistry,
        RegistrySchema schema,
        CancellationToken cancellationToken)
    {
        var names = new AvroSchemaNames();
        var resolved = new HashSet<ReferenceKey>();
        var visiting = new HashSet<ReferenceKey>();
        await ResolveAsync(
                schemaRegistry,
                schema,
                names,
                resolved,
                visiting,
                depth: 0,
                cancellationToken)
            .ConfigureAwait(false);
        return names;
    }

    private static async Task ResolveAsync(
        ISchemaRegistryClient schemaRegistry,
        RegistrySchema schema,
        AvroSchemaNames names,
        HashSet<ReferenceKey> resolved,
        HashSet<ReferenceKey> visiting,
        int depth,
        CancellationToken cancellationToken)
    {
        if (schema.References is not { Count: > 0 } references)
            return;
        if (depth >= MaxReferenceDepth)
            throw new InvalidOperationException($"Avro schema reference depth exceeds {MaxReferenceDepth}.");

        for (var index = 0; index < references.Count; index++)
        {
            var reference = references[index];
            var key = new ReferenceKey(reference.Subject, reference.Version);
            if (resolved.Contains(key))
                continue;
            if (!visiting.Add(key))
                throw new InvalidOperationException("Cyclic Avro schema references are not supported.");

            var registered = await schemaRegistry.GetSchemaBySubjectAsync(
                    reference.Subject,
                    reference.Version.ToString(CultureInfo.InvariantCulture),
                    cancellationToken)
                .ConfigureAwait(false);
            if (registered.Schema.SchemaType != SchemaType.Avro)
                throw new InvalidOperationException($"Schema {registered.Id} is {registered.Schema.SchemaType}, not Avro.");
            await ResolveAsync(
                    schemaRegistry,
                    registered.Schema,
                    names,
                    resolved,
                    visiting,
                    depth + 1,
                    cancellationToken)
                .ConfigureAwait(false);
            _ = AvroSchema.Parse(registered.Schema.SchemaString, names);
            visiting.Remove(key);
            resolved.Add(key);
        }
    }

    private readonly record struct ReferenceKey(string Subject, int Version);
}
