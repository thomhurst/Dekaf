namespace Dekaf.SchemaRegistry;

/// <summary>
/// Schema Registry compatibility policy.
/// </summary>
public enum SchemaCompatibilityLevel
{
    /// <summary>No compatibility checks.</summary>
    None,

    /// <summary>New schemas can read data written with the latest schema.</summary>
    Backward,

    /// <summary>New schemas can read data written with every previous schema.</summary>
    BackwardTransitive,

    /// <summary>The latest schema can read data written with new schemas.</summary>
    Forward,

    /// <summary>Every previous schema can read data written with new schemas.</summary>
    ForwardTransitive,

    /// <summary>Backward and forward compatibility with the latest schema.</summary>
    Full,

    /// <summary>Backward and forward compatibility with every previous schema.</summary>
    FullTransitive
}
