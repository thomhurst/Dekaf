namespace Dekaf.SchemaRegistry;

/// <summary>
/// Immutable Schema Registry resolution selected for one serialization context.
/// </summary>
/// <param name="Subject">The resolved Schema Registry subject.</param>
/// <param name="SchemaId">The global Schema Registry ID.</param>
/// <param name="Schema">The schema associated with the serialized payload.</param>
public readonly record struct ResolvedSchemaContext(
    string Subject,
    int SchemaId,
    Schema Schema)
{
    internal byte[]? SchemaGuidFrame { get; init; }
}
