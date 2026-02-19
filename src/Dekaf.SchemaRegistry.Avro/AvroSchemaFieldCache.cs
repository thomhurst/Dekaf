using System.Collections.Concurrent;
using System.Reflection;

namespace Dekaf.SchemaRegistry.Avro;

/// <summary>
/// Caches reflection lookups for the static <c>_SCHEMA</c> field on Avro-generated types.
/// This avoids repeated reflection calls for the same type across serializer and deserializer instances.
/// </summary>
internal static class AvroSchemaFieldCache
{
    private static readonly ConcurrentDictionary<Type, FieldInfo?> s_schemaFieldCache = new();

    /// <summary>
    /// Gets the <c>_SCHEMA</c> static field for the specified type, using a cached lookup.
    /// Returns <c>null</c> if the type does not have such a field.
    /// </summary>
    internal static FieldInfo? GetSchemaField(Type type)
    {
        return s_schemaFieldCache.GetOrAdd(type, static t =>
            t.GetField("_SCHEMA", BindingFlags.Public | BindingFlags.Static));
    }
}
