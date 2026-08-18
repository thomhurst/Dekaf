using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dekaf.SchemaRegistry.Avro.Poco;

/// <summary>Allocation-free Avro binary writer used by generated codecs.</summary>
public ref struct AvroValueWriter
{
    private Span<byte> _destination;
    private int _position;

    internal AvroValueWriter(Span<byte> destination) => _destination = destination;

    /// <summary>Number of encoded bytes.</summary>
    public readonly int WrittenCount => _position;

    internal readonly bool IsComplete => _position != int.MaxValue;

    /// <summary>Writes Avro null.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteNull() { }

    /// <summary>Writes an Avro boolean.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBoolean(bool value)
    {
        if (!Ensure(1))
            return;
        _destination[_position++] = value ? (byte)1 : (byte)0;
    }

    /// <summary>Writes an Avro int.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt32(int value) => WriteInt64(value);

    /// <summary>Writes an Avro long.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt64(long value)
    {
        var encoded = (ulong)((value << 1) ^ (value >> 63));
        if ((encoded & ~0x7FUL) != 0)
        {
            WriteInt64Slow(encoded);
            return;
        }

        if (!Ensure(1))
            return;
        _destination[_position++] = (byte)encoded;
    }

    /// <summary>Writes an Avro float.</summary>
    public void WriteSingle(float value)
    {
        if (!Ensure(sizeof(float)))
            return;
        BinaryPrimitives.WriteSingleLittleEndian(_destination.Slice(_position, sizeof(float)), value);
        _position += sizeof(float);
    }

    /// <summary>Writes an Avro double.</summary>
    public void WriteDouble(double value)
    {
        if (!Ensure(sizeof(double)))
            return;
        BinaryPrimitives.WriteDoubleLittleEndian(_destination.Slice(_position, sizeof(double)), value);
        _position += sizeof(double);
    }

    /// <summary>Writes Avro bytes.</summary>
    public void WriteBytes(scoped ReadOnlySpan<byte> value)
    {
        WriteInt64(value.Length);
        if (!Ensure(value.Length))
            return;
        value.CopyTo(_destination.Slice(_position));
        _position += value.Length;
    }

    /// <summary>Writes an Avro UTF-8 string without an intermediate byte array.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var byteCount = Encoding.UTF8.GetByteCount(value);
        WriteInt64(byteCount);
        if (!Ensure(byteCount))
            return;
        _position += Encoding.UTF8.GetBytes(value, _destination.Slice(_position, byteCount));
    }

    /// <summary>Writes an Avro UUID logical value without allocating a string.</summary>
    public void WriteUuid(Guid value)
    {
        const int formattedLength = 36;
        WriteInt64(formattedLength);
        if (!Ensure(formattedLength))
            return;
        Span<char> characters = stackalloc char[formattedLength];
        if (!value.TryFormat(characters, out var charsWritten, "D") || charsWritten != formattedLength)
        {
            throw new InvalidOperationException("UUID formatting failed.");
        }

        _position += Encoding.UTF8.GetBytes(characters, _destination.Slice(_position, formattedLength));
    }

    /// <summary>Writes an enum or union branch index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteIndex(int value) => WriteInt32(value);

    /// <summary>Starts a single-block array or map.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBlockCount(int count)
    {
        if ((uint)count > AvroValueReader.MaxCollectionItemCount)
            AvroValueReader.ThrowCollectionLimit();
        WriteInt64(count);
    }

    /// <summary>Ends an array or map.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBlockEnd() => WriteInt64(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Ensure(int required)
    {
        if (required <= _destination.Length - _position)
            return true;

        return Overflow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteInt64Slow(ulong encoded)
    {
        while ((encoded & ~0x7FUL) != 0)
        {
            if (!Ensure(1))
                return;
            _destination[_position++] = (byte)((encoded & 0x7F) | 0x80);
            encoded >>= 7;
        }

        if (!Ensure(1))
            return;
        _destination[_position++] = (byte)encoded;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool Overflow()
    {
        _position = int.MaxValue;
        return false;
    }
}
