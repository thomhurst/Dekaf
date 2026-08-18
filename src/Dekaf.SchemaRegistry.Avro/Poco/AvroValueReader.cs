using System.Buffers.Binary;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Allocation-safe Avro binary reader used by generated codecs.</summary>
public ref struct AvroValueReader
{
    private readonly ReadOnlySpan<byte> _source;
    private int _position;

    internal AvroValueReader(ReadOnlySpan<byte> source) => _source = source;

    /// <summary>Reads Avro null.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadNull() { }

    /// <summary>Reads an Avro boolean.</summary>
    public bool ReadBoolean()
    {
        Ensure(1);
        return _source[_position++] switch
        {
            0 => false,
            1 => true,
            var value => throw new InvalidDataException($"Invalid Avro boolean byte {value}.")
        };
    }

    /// <summary>Reads an Avro int.</summary>
    public int ReadInt32()
    {
        var value = ReadInt64();
        if (value is < int.MinValue or > int.MaxValue)
            throw new InvalidDataException("Avro integer exceeds Int32 range.");
        return (int)value;
    }

    /// <summary>Reads an Avro long.</summary>
    public long ReadInt64()
    {
        ulong encoded = 0;
        for (var shift = 0; shift < 70; shift += 7)
        {
            Ensure(1);
            var current = _source[_position++];
            if (shift == 63 && (current & 0xFE) != 0)
                throw new InvalidDataException("Avro variable-length integer exceeds Int64 range.");
            encoded |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return (long)(encoded >> 1) ^ -((long)encoded & 1);
        }

        throw new InvalidDataException("Invalid Avro variable-length integer.");
    }

    /// <summary>Reads an Avro float.</summary>
    public float ReadSingle()
    {
        Ensure(sizeof(float));
        var value = BinaryPrimitives.ReadSingleLittleEndian(_source.Slice(_position, sizeof(float)));
        _position += sizeof(float);
        return value;
    }

    /// <summary>Reads an Avro double.</summary>
    public double ReadDouble()
    {
        Ensure(sizeof(double));
        var value = BinaryPrimitives.ReadDoubleLittleEndian(_source.Slice(_position, sizeof(double)));
        _position += sizeof(double);
        return value;
    }

    /// <summary>Reads Avro bytes into the returned object graph.</summary>
    public byte[] ReadBytes()
    {
        return ReadBytesSpan().ToArray();
    }

    /// <summary>Reads Avro bytes as a view over the input payload.</summary>
    public ReadOnlySpan<byte> ReadBytesSpan()
    {
        var length = ReadLength();
        Ensure(length);
        var value = _source.Slice(_position, length);
        _position += length;
        return value;
    }

    /// <summary>Reads an Avro UTF-8 string into the returned object graph.</summary>
    public string ReadString()
    {
        var length = ReadLength();
        Ensure(length);
        var value = Encoding.UTF8.GetString(_source.Slice(_position, length));
        _position += length;
        return value;
    }

    /// <summary>Reads an Avro UUID logical value directly from UTF-8.</summary>
    public Guid ReadUuid()
    {
        var value = ReadBytesSpan();
        if (!Utf8Parser.TryParse(value, out Guid result, out var consumed, 'D') || consumed != value.Length)
            throw new InvalidDataException("Invalid Avro UUID value.");
        return result;
    }

    /// <summary>Reads an enum, union, array, or map block index/count.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadIndex() => ReadLength();

    /// <summary>Reads and validates an enum, union, array, or map block index/count.</summary>
    public int ReadIndex(int exclusiveUpperBound)
    {
        var value = ReadLength();
        if (exclusiveUpperBound <= 0 || (uint)value >= (uint)exclusiveUpperBound)
            throw new InvalidDataException("Avro index is out of range.");
        return value;
    }

    /// <summary>Reads the next array or map block count.</summary>
    public int ReadBlockCount()
    {
        var count = ReadInt64();
        if (count >= 0)
            return CheckedLength(count);

        var blockSize = ReadInt64();
        if (blockSize < 0)
            throw new InvalidDataException("Avro collection block byte size cannot be negative.");
        return CheckedLength(-count);
    }

    /// <summary>Gets a collection capacity bounded by the remaining encoded payload.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetCollectionCapacity(int count) => Math.Min(count, _source.Length - _position);

    /// <summary>Skips a value described by a cached writer-schema node.</summary>
    public void Skip(AvroPocoReadNode node)
    {
        switch (node.Kind)
        {
            case AvroPocoTypeKind.Null:
                return;
            case AvroPocoTypeKind.Boolean:
                _ = ReadBoolean();
                return;
            case AvroPocoTypeKind.Int:
            case AvroPocoTypeKind.Long:
            case AvroPocoTypeKind.Date:
            case AvroPocoTypeKind.TimeMilliseconds:
            case AvroPocoTypeKind.TimeMicroseconds:
            case AvroPocoTypeKind.TimestampMilliseconds:
            case AvroPocoTypeKind.TimestampMicroseconds:
            case AvroPocoTypeKind.Enum:
                _ = ReadInt64();
                return;
            case AvroPocoTypeKind.Float:
                Ensure(sizeof(float));
                _position += sizeof(float);
                return;
            case AvroPocoTypeKind.Double:
                Ensure(sizeof(double));
                _position += sizeof(double);
                return;
            case AvroPocoTypeKind.Bytes:
            case AvroPocoTypeKind.String:
            case AvroPocoTypeKind.Decimal:
            case AvroPocoTypeKind.Uuid:
                if (node.FixedSize > 0)
                    SkipFixed(node.FixedSize);
                else
                    SkipBytes();
                return;
            case AvroPocoTypeKind.Record:
                foreach (var field in node.Fields.Span)
                    Skip(field);
                return;
            case AvroPocoTypeKind.Array:
                SkipCollection(node.Item!);
                return;
            case AvroPocoTypeKind.Map:
                SkipMap(node.Item!);
                return;
            case AvroPocoTypeKind.Union:
                var branches = node.Branches.Span;
                var branch = ReadIndex(branches.Length);
                Skip(branches[branch]);
                return;
            default:
                throw new InvalidDataException($"Unsupported Avro writer type {node.Kind}.");
        }
    }

    private void SkipBytes()
    {
        var length = ReadLength();
        Ensure(length);
        _position += length;
    }

    private void SkipFixed(int length)
    {
        Ensure(length);
        _position += length;
    }

    private void SkipCollection(AvroPocoReadNode item)
    {
        while (true)
        {
            var count = ReadBlockCount();
            if (count == 0)
                return;
            for (var index = 0; index < count; index++)
                Skip(item);
        }
    }

    private void SkipMap(AvroPocoReadNode item)
    {
        while (true)
        {
            var count = ReadBlockCount();
            if (count == 0)
                return;
            for (var index = 0; index < count; index++)
            {
                SkipBytes();
                Skip(item);
            }
        }
    }

    internal ReadOnlySpan<byte> ReadFixed(int length)
    {
        Ensure(length);
        var value = _source.Slice(_position, length);
        _position += length;
        return value;
    }

    private int ReadLength()
    {
        var value = ReadInt64();
        if (value < 0)
            throw new InvalidDataException("Invalid Avro length.");
        return CheckedLength(value);
    }

    private static int CheckedLength(long value)
    {
        if ((ulong)value > int.MaxValue)
            throw new InvalidDataException("Avro length exceeds supported range.");
        return (int)value;
    }

    private readonly void Ensure(int required)
    {
        if (required < 0 || required > _source.Length - _position)
            throw new EndOfStreamException("Avro payload ended before the value was complete.");
    }
}
