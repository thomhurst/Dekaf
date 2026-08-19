namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>
/// Opts a partial class, record, or struct into source-generated Avro serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class AvroRecordAttribute : Attribute
{
    /// <summary>Overrides the Avro record name.</summary>
    public string? Name { get; init; }

    /// <summary>Overrides the Avro record namespace.</summary>
    public string? Namespace { get; init; }
}

/// <summary>Configures one generated Avro field.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class AvroFieldAttribute : Attribute
{
    /// <summary>Sets deterministic field ordering. Unset fields use source declaration order.</summary>
    public int Order { get; init; } = -1;

    /// <summary>Overrides the Avro field name.</summary>
    public string? Name { get; init; }

    /// <summary>Names used to resolve fields written by older schemas.</summary>
    public string[]? Aliases { get; init; }

    /// <summary>Avro JSON default used when a writer schema omits this field.</summary>
    public string? DefaultJson { get; init; }

    /// <summary>Explicit non-null union branches for interface or object members.</summary>
    public Type[]? UnionTypes { get; init; }

    /// <summary>Requires this member's standard logical-type mapping to match the specified name.</summary>
    public string? LogicalType { get; init; }

    /// <summary>Decimal precision for a decimal member.</summary>
    public int Precision { get; init; }

    /// <summary>Decimal scale for a decimal member.</summary>
    public int Scale { get; init; }
}

/// <summary>Excludes a property or field from its generated Avro record.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class AvroIgnoreAttribute : Attribute;
