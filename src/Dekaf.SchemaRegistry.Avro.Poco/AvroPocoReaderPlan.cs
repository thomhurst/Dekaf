namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Cached mapping from one writer schema to a generated POCO reader schema.</summary>
public sealed class AvroPocoReaderPlan
{
    internal AvroPocoReaderPlan(AvroPocoReadOperation[] operations) => Operations = operations;

    internal AvroPocoReadOperation[] Operations { get; }

    /// <summary>Number of fields encoded by the writer schema.</summary>
    public int WriterFieldCount => Operations.Length;

    /// <summary>Gets the cached operation for a writer field.</summary>
    public AvroPocoReadOperation GetOperation(int writerFieldIndex) => Operations[writerFieldIndex];
}

/// <summary>One cached writer-field decoding operation.</summary>
public readonly struct AvroPocoReadOperation
{
    internal AvroPocoReadOperation(int readerFieldIndex, AvroPocoReadNode writerType)
    {
        ReaderFieldIndex = readerFieldIndex;
        WriterType = writerType;
    }

    /// <summary>Generated reader-field index, or -1 when this writer field is skipped.</summary>
    public int ReaderFieldIndex { get; }

    /// <summary>Cached writer type used to decode or skip the field.</summary>
    public AvroPocoReadNode WriterType { get; }
}

/// <summary>Compact cached writer-schema node used by generated readers.</summary>
public sealed class AvroPocoReadNode
{
    internal AvroPocoReadNode(AvroPocoTypeKind kind) => Kind = kind;

    /// <summary>Writer wire type.</summary>
    public AvroPocoTypeKind Kind { get; }

    /// <summary>Array or map writer item type.</summary>
    public AvroPocoReadNode? Item { get; internal init; }

    /// <summary>Writer union branches.</summary>
    public ReadOnlyMemory<AvroPocoReadNode> Branches { get; internal init; }

    /// <summary>Writer record fields in encoded order.</summary>
    public ReadOnlyMemory<AvroPocoReadNode> Fields { get; internal init; }

    /// <summary>Nested writer-to-reader record plan.</summary>
    public AvroPocoReaderPlan? RecordPlan { get; internal init; }

    /// <summary>Writer enum index to generated reader enum index.</summary>
    public ReadOnlyMemory<int> EnumMap { get; internal init; }

    /// <summary>Generated reader-union branch selected for this writer node.</summary>
    public int ReaderUnionBranchIndex { get; internal set; } = -1;
}
