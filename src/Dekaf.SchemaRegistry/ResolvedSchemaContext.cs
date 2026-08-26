using System.Runtime.CompilerServices;

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

    /// <inheritdoc />
    // Preserve the CompilerGenerated attribute emitted by the previously synthesized record member.
    [CompilerGenerated]
    public bool Equals(ResolvedSchemaContext other) =>
        EqualityComparer<string>.Default.Equals(Subject, other.Subject)
        && SchemaId == other.SchemaId
        && EqualityComparer<Schema>.Default.Equals(Schema, other.Schema);

    /// <inheritdoc />
    [CompilerGenerated]
    public override int GetHashCode() => HashCode.Combine(Subject, SchemaId, Schema);
}
