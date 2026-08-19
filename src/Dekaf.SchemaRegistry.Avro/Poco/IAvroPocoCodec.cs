namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Generated, strongly typed Avro codec contract.</summary>
/// <typeparam name="T">POCO value type.</typeparam>
public interface IAvroPocoCodec<T>
{
    /// <summary>Deterministic Avro writer schema.</summary>
    static abstract string SchemaJson { get; }

    /// <summary>Deterministic Avro writer schema encoded as UTF-8 at compile time.</summary>
    static abstract ReadOnlySpan<byte> SchemaUtf8 { get; }

    /// <summary>CRC-64-AVRO fingerprint of the schema parsing canonical form.</summary>
    static abstract long ParsingFingerprint64 { get; }

    /// <summary>Fully-qualified Avro record name.</summary>
    static abstract string FullName { get; }

    /// <summary>Generated reader-field metadata used to build cold schema-evolution plans.</summary>
    static abstract ReadOnlyMemory<AvroPocoField> Fields { get; }

    /// <summary>Writes one value without reflection, boxing, or intermediate objects.</summary>
    static abstract void Write(ref AvroValueWriter writer, T value);

    /// <summary>Reads one value using a cached writer-to-reader schema plan.</summary>
    static abstract T Read(ref AvroValueReader reader, AvroPocoReaderPlan plan);
}

/// <summary>Immutable generated field metadata.</summary>
public readonly struct AvroPocoField
{
    /// <summary>Creates generated field metadata.</summary>
    public AvroPocoField(
        string name,
        ReadOnlyMemory<string> aliases,
        string? defaultJson,
        AvroPocoType type)
    {
        Name = name;
        Aliases = aliases;
        DefaultJson = defaultJson;
        Type = type;
    }

    /// <summary>Avro field name.</summary>
    public string Name { get; }

    /// <summary>Avro field aliases.</summary>
    public ReadOnlyMemory<string> Aliases { get; }

    /// <summary>Avro JSON default.</summary>
    public string? DefaultJson { get; }

    /// <summary>Generated field type metadata.</summary>
    public AvroPocoType Type { get; }
}

/// <summary>Compact type description emitted by the source generator.</summary>
public sealed class AvroPocoType
{
    /// <summary>Creates a type description.</summary>
    public AvroPocoType(
        AvroPocoTypeKind kind,
        AvroPocoType? item = null,
        ReadOnlyMemory<AvroPocoType> branches = default,
        string? fullName = null,
        ReadOnlyMemory<AvroPocoField> fields = default,
        ReadOnlyMemory<string> symbols = default,
        int precision = 0,
        int scale = 0)
    {
        Kind = kind;
        Item = item;
        Branches = branches;
        FullName = fullName;
        Fields = fields;
        Symbols = symbols;
        Precision = precision;
        Scale = scale;
    }

    /// <summary>Wire type.</summary>
    public AvroPocoTypeKind Kind { get; }

    /// <summary>Array or map item type.</summary>
    public AvroPocoType? Item { get; }

    /// <summary>Union branch types.</summary>
    public ReadOnlyMemory<AvroPocoType> Branches { get; }

    /// <summary>Named record or enum fullname.</summary>
    public string? FullName { get; }

    /// <summary>Generated fields for a nested record.</summary>
    public ReadOnlyMemory<AvroPocoField> Fields { get; }

    /// <summary>Generated symbols for an enum.</summary>
    public ReadOnlyMemory<string> Symbols { get; }

    /// <summary>Generated decimal precision, or zero for non-decimal types.</summary>
    public int Precision { get; }

    /// <summary>Generated decimal scale.</summary>
    public int Scale { get; }
}

/// <summary>Types supported by generated POCO codecs.</summary>
public enum AvroPocoTypeKind : byte
{
    Null,
    Boolean,
    Int,
    Long,
    Float,
    Double,
    Bytes,
    String,
    Record,
    Enum,
    Array,
    Map,
    Union,
    Date,
    TimeMilliseconds,
    TimeMicroseconds,
    TimestampMilliseconds,
    TimestampMicroseconds,
    Uuid,
    Decimal,

    /// <summary>Internal optimized skip node for an array whose items encode no bytes.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    ZeroWidthArray
}
