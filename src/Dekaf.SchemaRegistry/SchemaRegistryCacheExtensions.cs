namespace Dekaf.SchemaRegistry;

internal static class SchemaRegistryCacheExtensions
{
    internal static Schema GetSchemaSync(this ISchemaRegistryClient schemaRegistry, int schemaId, TimeSpan timeout)
    {
        if (schemaRegistry is ISchemaRegistryCache cache
            && cache.TryGetCachedSchema(schemaId, out var cached))
            return cached;

        return schemaRegistry.GetSchemaAsync(schemaId)
            .WaitAsync(timeout)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    internal static Schema GetSchemaSync(
        this ISchemaRegistryClient schemaRegistry,
        int schemaId,
        string subject,
        TimeSpan timeout)
    {
        if (schemaRegistry is ISchemaRegistryCache cache
            && cache.TryGetCachedSchema(schemaId, subject, out var cached))
            return cached;

        return schemaRegistry.GetSchemaAsync(schemaId, subject)
            .WaitAsync(timeout)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }
}
