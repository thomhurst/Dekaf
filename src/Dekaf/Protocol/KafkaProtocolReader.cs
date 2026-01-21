using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dekaf.Protocol;

/// <summary>
/// Zero-allocation binary decoder for Kafka protocol messages.
/// This is a ref struct to ensure stack-only allocation and no GC pressure.
/// </summary>
public ref struct KafkaProtocolReader
{
    private SequenceReader<byte> _reader;

    public KafkaProtocolReader(ReadOnlySequence<byte> buffer)
    {
        _reader = new SequenceReader<byte>(buffer);
    }

    public KafkaProtocolReader(ReadOnlyMemory<byte> buffer) : this(new ReadOnlySequence<byte>(buffer))
    {
    }

    public readonly long Consumed => _reader.Consumed;
    public readonly long Remaining => _reader.Remaining;
    public readonly bool End => _reader.End;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadInt8()
    {
        if (!_reader.TryRead(out var value))
            ThrowInsufficientData();
        return (sbyte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadUInt8()
    {
        if (!_reader.TryRead(out var value))
            ThrowInsufficientData();
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadInt16()
    {
        Span<byte> buffer = stackalloc byte[2];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(2);
        return BinaryPrimitives.ReadInt16BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        Span<byte> buffer = stackalloc byte[4];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(4);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64()
    {
        Span<byte> buffer = stackalloc byte[8];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(8);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Guid ReadUuid()
    {
        Span<byte> buffer = stackalloc byte[16];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(16);
        return new Guid(buffer, bigEndian: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadFloat64()
    {
        Span<byte> buffer = stackalloc byte[8];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(8);
        return BinaryPrimitives.ReadDoubleBigEndian(buffer);
    }

    /// <summary>
    /// Reads a variable-length integer using ZigZag decoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadVarInt()
    {
        var value = ReadVarUInt();
        return ZigZagDecode(value);
    }

    /// <summary>
    /// Reads a variable-length long using ZigZag decoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadVarLong()
    {
        var value = ReadVarULong();
        return ZigZagDecode(value);
    }

    /// <summary>
    /// Reads an unsigned variable-length integer.
    /// </summary>
    public int ReadUnsignedVarInt()
    {
        return (int)ReadVarUInt();
    }

    private uint ReadVarUInt()
    {
        uint result = 0;
        var shift = 0;

        while (shift < 35)
        {
            if (!_reader.TryRead(out var b))
                ThrowInsufficientData();

            result |= (uint)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return result;

            shift += 7;
        }

        ThrowMalformedVarInt();
        return 0; // Unreachable
    }

    private ulong ReadVarULong()
    {
        ulong result = 0;
        var shift = 0;

        while (shift < 70)
        {
            if (!_reader.TryRead(out var b))
                ThrowInsufficientData();

            result |= (ulong)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return result;

            shift += 7;
        }

        ThrowMalformedVarInt();
        return 0; // Unreachable
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ZigZagDecode(uint value) => (int)(value >> 1) ^ -(int)(value & 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ZigZagDecode(ulong value) => (long)(value >> 1) ^ -(long)(value & 1);

    /// <summary>
    /// Reads a boolean as a single byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBoolean()
    {
        return ReadUInt8() != 0;
    }

    /// <summary>
    /// Reads a string with 2-byte length prefix (legacy format).
    /// </summary>
    public string? ReadString()
    {
        var length = ReadInt16();
        if (length < 0)
            return null;
        if (length == 0)
            return string.Empty;

        return ReadStringContent(length);
    }

    /// <summary>
    /// Reads a compact string with unsigned varint length prefix (flexible format).
    /// Length is encoded as length + 1 (0 means null).
    /// </summary>
    public string? ReadCompactString()
    {
        var length = ReadUnsignedVarInt() - 1;
        if (length < 0)
            return null;
        if (length == 0)
            return string.Empty;

        return ReadStringContent(length);
    }

    /// <summary>
    /// Reads a compact non-nullable string.
    /// </summary>
    public string ReadCompactNonNullableString()
    {
        return ReadCompactString() ?? string.Empty;
    }

    private string ReadStringContent(int length)
    {
        if (_reader.UnreadSpan.Length >= length)
        {
            var result = Encoding.UTF8.GetString(_reader.UnreadSpan[..length]);
            _reader.Advance(length);
            return result;
        }

        // Slow path: data spans multiple segments
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            if (!_reader.TryCopyTo(buffer.AsSpan(0, length)))
                ThrowInsufficientData();
            _reader.Advance(length);
            return Encoding.UTF8.GetString(buffer.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Reads bytes with 4-byte length prefix (legacy format).
    /// </summary>
    public byte[]? ReadBytes()
    {
        var length = ReadInt32();
        if (length < 0)
            return null;
        if (length == 0)
            return [];

        return ReadBytesContent(length);
    }

    /// <summary>
    /// Reads compact bytes with unsigned varint length prefix.
    /// </summary>
    public byte[]? ReadCompactBytes()
    {
        var length = ReadUnsignedVarInt() - 1;
        if (length < 0)
            return null;
        if (length == 0)
            return [];

        return ReadBytesContent(length);
    }

    private byte[] ReadBytesContent(int length)
    {
        var result = new byte[length];
        if (!_reader.TryCopyTo(result))
            ThrowInsufficientData();
        _reader.Advance(length);
        return result;
    }

    /// <summary>
    /// Reads raw bytes without a length prefix into the provided span.
    /// </summary>
    public void ReadRawBytes(Span<byte> destination)
    {
        if (!_reader.TryCopyTo(destination))
            ThrowInsufficientData();
        _reader.Advance(destination.Length);
    }

    /// <summary>
    /// Reads raw bytes and returns them as an array.
    /// </summary>
    public byte[] ReadRawBytes(int count)
    {
        if (count == 0)
            return [];
        var result = new byte[count];
        ReadRawBytes(result);
        return result;
    }

    /// <summary>
    /// Reads an array with 4-byte length prefix (legacy format).
    /// </summary>
    public T[] ReadArray<T>(ReadFunc<T> readItem)
    {
        var length = ReadInt32();
        if (length < 0)
            return [];

        var result = new T[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = readItem(ref this);
        }
        return result;
    }

    /// <summary>
    /// Reads a compact array with unsigned varint length prefix (flexible format).
    /// </summary>
    public T[] ReadCompactArray<T>(ReadFunc<T> readItem)
    {
        var length = ReadUnsignedVarInt() - 1;
        if (length < 0)
            return [];

        var result = new T[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = readItem(ref this);
        }
        return result;
    }

    /// <summary>
    /// Skips tagged fields (for flexible versions).
    /// </summary>
    public void SkipTaggedFields()
    {
        var numFields = ReadUnsignedVarInt();
        for (var i = 0; i < numFields; i++)
        {
            _ = ReadUnsignedVarInt(); // tag
            var size = ReadUnsignedVarInt();
            Skip(size);
        }
    }

    /// <summary>
    /// Skips a specified number of bytes.
    /// </summary>
    public void Skip(int count)
    {
        if (count <= 0)
            return;
        if (_reader.Remaining < count)
            ThrowInsufficientData();
        _reader.Advance(count);
    }

    /// <summary>
    /// Gets the remaining unread data as a sequence.
    /// </summary>
    public readonly ReadOnlySequence<byte> GetRemainingSequence()
    {
        return _reader.UnreadSequence;
    }

    /// <summary>
    /// Delegate for reading a single item in an array.
    /// </summary>
    public delegate T ReadFunc<out T>(ref KafkaProtocolReader reader);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInsufficientData()
    {
        throw new InvalidOperationException("Insufficient data in buffer");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowMalformedVarInt()
    {
        throw new InvalidOperationException("Malformed variable-length integer");
    }
}
