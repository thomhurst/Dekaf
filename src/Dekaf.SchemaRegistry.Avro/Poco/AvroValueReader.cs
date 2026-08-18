using System.Buffers.Binary;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Allocation-safe Avro binary reader used by generated codecs.</summary>
public ref struct AvroValueReader
{
    internal const int MaxCollectionItemCount = 1_048_576;
    private const int MaxCollectionAllocationBytes = 8 * 1024 * 1024;
    private const int MaxSkipDepth = 256;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly ReadOnlySpan<byte> _source;
    private int _position;
    private int _skipDepth;

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

    /// <summary>Reads and validates an Avro time-micros logical value as ticks since midnight.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadTimeMicrosecondsTicks()
    {
        var microseconds = ReadInt64();
        if ((ulong)microseconds >= TimeSpan.TicksPerDay / 10)
            throw new InvalidDataException("Avro time-micros value must be from zero through less than 24 hours.");
        return microseconds * 10;
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

    /// <summary>Reads validated Avro string bytes into the returned object graph.</summary>
    public byte[] ReadStringBytes()
    {
        var value = ReadBytesSpan();
        ValidateUtf8(value);
        return value.ToArray();
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
        try
        {
            var value = StrictUtf8.GetString(_source.Slice(_position, length));
            _position += length;
            return value;
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Invalid Avro UTF-8 string.", exception);
        }
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadBlockCount()
    {
        var count = ReadInt64();
        int result;
        if (count >= 0)
        {
            result = CheckedLength(count);
        }
        else
        {
            var blockSize = ReadInt64();
            if (blockSize < 0)
                throw new InvalidDataException("Avro collection block byte size cannot be negative.");
            result = CheckedLength(-count);
        }

        if (result > MaxCollectionItemCount)
            ThrowCollectionLimit();
        return result;
    }

    /// <summary>Gets a collection capacity bounded by the remaining encoded payload.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetCollectionCapacity(int count) => Math.Min(count, _source.Length - _position);

    /// <summary>Validates and accumulates collection block counts.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AddCollectionCount(int currentCount, int blockCount)
    {
        var total = (long)currentCount + blockCount;
        if ((ulong)total > MaxCollectionItemCount)
            ThrowCollectionLimit();

        return (int)total;
    }

    /// <summary>Validates that a generated collection's backing storage stays within its byte limit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateCollectionAllocation<T>(int count)
    {
        if ((ulong)(uint)count * (uint)Unsafe.SizeOf<T>() > MaxCollectionAllocationBytes)
            ThrowCollectionAllocationLimit();
    }

    /// <summary>Validates that a generated map's backing storage stays within its byte limit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateMapAllocation<T>(int count)
    {
        const int DictionaryEntryAndBucketMetadataSize = sizeof(int) * 3;
        var entrySize = DictionaryEntryAndBucketMetadataSize + IntPtr.Size + Unsafe.SizeOf<T>();
        if ((ulong)(uint)count * (uint)entrySize > MaxCollectionAllocationBytes)
            ThrowCollectionAllocationLimit();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowCollectionLimit() =>
        throw new InvalidDataException(
            $"Avro collection exceeds the supported limit of {MaxCollectionItemCount} items.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCollectionAllocationLimit() =>
        throw new InvalidDataException(
            $"Avro collection allocation exceeds the supported limit of {MaxCollectionAllocationBytes} bytes.");

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
            case AvroPocoTypeKind.Date:
            case AvroPocoTypeKind.TimeMilliseconds:
                _ = ReadInt32();
                return;
            case AvroPocoTypeKind.Long:
            case AvroPocoTypeKind.TimeMicroseconds:
            case AvroPocoTypeKind.TimestampMilliseconds:
            case AvroPocoTypeKind.TimestampMicroseconds:
                _ = ReadInt64();
                return;
            case AvroPocoTypeKind.Enum:
                _ = ReadIndex(node.EnumSymbolCount);
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
            case AvroPocoTypeKind.Decimal:
                if (node.FixedSize > 0)
                    SkipFixed(node.FixedSize);
                else
                    SkipBytes();
                return;
            case AvroPocoTypeKind.String:
            case AvroPocoTypeKind.Uuid:
                SkipString();
                return;
            case AvroPocoTypeKind.Record:
                SkipNestedRecord(node);
                return;
            case AvroPocoTypeKind.Array:
                SkipNestedCollection(node.Item!);
                return;
            case AvroPocoTypeKind.Map:
                SkipNestedMap(node.Item!);
                return;
            case AvroPocoTypeKind.Union:
                SkipNestedUnion(node.Branches.Span);
                return;
            default:
                throw new InvalidDataException($"Unsupported Avro writer type {node.Kind}.");
        }
    }

    private void SkipNestedRecord(AvroPocoReadNode node)
    {
        EnterSkipNode();
        try
        {
            foreach (var field in node.Fields.Span)
                Skip(field);
        }
        finally
        {
            _skipDepth--;
        }
    }

    private void SkipNestedCollection(AvroPocoReadNode item)
    {
        EnterSkipNode();
        try
        {
            SkipCollection(item);
        }
        finally
        {
            _skipDepth--;
        }
    }

    private void SkipNestedMap(AvroPocoReadNode item)
    {
        EnterSkipNode();
        try
        {
            SkipMap(item);
        }
        finally
        {
            _skipDepth--;
        }
    }

    private void SkipNestedUnion(ReadOnlySpan<AvroPocoReadNode> branches)
    {
        EnterSkipNode();
        try
        {
            var branch = ReadIndex(branches.Length);
            Skip(branches[branch]);
        }
        finally
        {
            _skipDepth--;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterSkipNode()
    {
        if (_skipDepth >= MaxSkipDepth)
            throw new InvalidDataException($"Avro value nesting exceeds the supported limit of {MaxSkipDepth}.");
        _skipDepth++;
    }

    private void SkipBytes()
    {
        var length = ReadLength();
        Ensure(length);
        _position += length;
    }

    private void SkipString()
    {
        var length = ReadLength();
        Ensure(length);
        ValidateUtf8(_source.Slice(_position, length));
        _position += length;
    }

    private void SkipFixed(int length)
    {
        Ensure(length);
        _position += length;
    }

    private void SkipCollection(AvroPocoReadNode item)
    {
        var count = ReadBlockCount();
        var total = count;
        while (count != 0)
        {
            for (var index = 0; index < count; index++)
                Skip(item);

            count = ReadBlockCount();
            if (count != 0)
                total = AddCollectionCount(total, count);
        }
    }

    private void SkipMap(AvroPocoReadNode item)
    {
        var count = ReadBlockCount();
        var total = count;
        while (count != 0)
        {
            for (var index = 0; index < count; index++)
            {
                SkipString();
                Skip(item);
            }

            count = ReadBlockCount();
            if (count != 0)
                total = AddCollectionCount(total, count);
        }
    }

    private static void ValidateUtf8(ReadOnlySpan<byte> value)
    {
        try
        {
            _ = StrictUtf8.GetCharCount(value);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Invalid Avro UTF-8 string.", exception);
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
