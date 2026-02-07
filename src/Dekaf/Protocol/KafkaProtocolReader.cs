using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dekaf.Protocol;

/// <summary>
/// Zero-allocation binary decoder for Kafka protocol messages.
/// This is a ref struct to ensure stack-only allocation and no GC pressure.
///
/// Optimized for contiguous memory (99%+ of cases) by using direct span operations
/// with a simple position index, avoiding SequenceReader overhead entirely.
/// Falls back to SequenceReader only for multi-segment sequences.
/// </summary>
public ref struct KafkaProtocolReader
{
    // Fast path: Direct span access for contiguous memory (99%+ of cases)
    private readonly ReadOnlySpan<byte> _span;
    private int _position;

    // Backing memory for zero-allocation slicing (when available)
    // This allows ReadMemorySlice() and GetRemainingSequence() to return
    // slices without allocating when the reader was constructed from Memory/array
    private readonly ReadOnlyMemory<byte> _memory;
    private readonly bool _hasMemory;

    // Slow path: SequenceReader for multi-segment sequences (rare)
    // This is only allocated/used when data spans multiple segments
    private SequenceReader<byte> _reader;
    private readonly bool _isContiguous;

    /// <summary>
    /// Creates a reader from a byte array. This is the most common case.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KafkaProtocolReader(byte[] buffer)
    {
        _span = buffer;
        _memory = buffer;
        _hasMemory = true;
        _position = 0;
        _isContiguous = true;
        _reader = default;
    }

    /// <summary>
    /// Creates a reader from contiguous memory. This is the fast path used for most protocol parsing.
    /// Note: ReadMemorySlice() and GetRemainingSequence() will allocate when using this constructor
    /// since spans cannot be converted to Memory without copying.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KafkaProtocolReader(ReadOnlySpan<byte> buffer)
    {
        _span = buffer;
        _memory = default;
        _hasMemory = false;
        _position = 0;
        _isContiguous = true;
        _reader = default;
    }

    /// <summary>
    /// Creates a reader from a ReadOnlySequence. Uses fast path if data is in a single segment.
    /// </summary>
    public KafkaProtocolReader(ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsSingleSegment)
        {
            // Fast path: Single segment - use direct span access
            _span = buffer.FirstSpan;
            _memory = buffer.First;
            _hasMemory = true;
            _position = 0;
            _isContiguous = true;
            _reader = default;
        }
        else
        {
            // Slow path: Multi-segment - use SequenceReader
            _span = default;
            _memory = default;
            _hasMemory = false;
            _position = 0;
            _isContiguous = false;
            _reader = new SequenceReader<byte>(buffer);
        }
    }

    /// <summary>
    /// Creates a reader from ReadOnlyMemory. Always uses fast path since Memory is contiguous.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KafkaProtocolReader(ReadOnlyMemory<byte> buffer)
    {
        _span = buffer.Span;
        _memory = buffer;
        _hasMemory = true;
        _position = 0;
        _isContiguous = true;
        _reader = default;
    }

    public readonly long Consumed => _isContiguous ? _position : _reader.Consumed;
    public readonly long Remaining => _isContiguous ? _span.Length - _position : _reader.Remaining;
    public readonly bool End => _isContiguous ? _position >= _span.Length : _reader.End;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadInt8()
    {
        if (_isContiguous)
        {
            if (_position >= _span.Length)
                ThrowInsufficientData();
            return (sbyte)_span[_position++];
        }
        return ReadInt8Slow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private sbyte ReadInt8Slow()
    {
        if (!_reader.TryRead(out var value))
            ThrowInsufficientData();
        return (sbyte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadUInt8()
    {
        if (_isContiguous)
        {
            if (_position >= _span.Length)
                ThrowInsufficientData();
            return _span[_position++];
        }
        return ReadUInt8Slow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private byte ReadUInt8Slow()
    {
        if (!_reader.TryRead(out var value))
            ThrowInsufficientData();
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadInt16()
    {
        if (_isContiguous)
        {
            if (_position + 2 > _span.Length)
                ThrowInsufficientData();
            var result = BinaryPrimitives.ReadInt16BigEndian(_span.Slice(_position));
            _position += 2;
            return result;
        }
        return ReadInt16Slow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private short ReadInt16Slow()
    {
        if (_reader.UnreadSpan.Length >= 2)
        {
            var result = BinaryPrimitives.ReadInt16BigEndian(_reader.UnreadSpan);
            _reader.Advance(2);
            return result;
        }
        Span<byte> buffer = stackalloc byte[2];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(2);
        return BinaryPrimitives.ReadInt16BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        if (_isContiguous)
        {
            if (_position + 4 > _span.Length)
                ThrowInsufficientData();
            var result = BinaryPrimitives.ReadInt32BigEndian(_span.Slice(_position));
            _position += 4;
            return result;
        }
        return ReadInt32Slow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ReadInt32Slow()
    {
        if (_reader.UnreadSpan.Length >= 4)
        {
            var result = BinaryPrimitives.ReadInt32BigEndian(_reader.UnreadSpan);
            _reader.Advance(4);
            return result;
        }
        Span<byte> buffer = stackalloc byte[4];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(4);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64()
    {
        if (_isContiguous)
        {
            if (_position + 8 > _span.Length)
                ThrowInsufficientData();
            var result = BinaryPrimitives.ReadInt64BigEndian(_span.Slice(_position));
            _position += 8;
            return result;
        }
        return ReadInt64Slow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private long ReadInt64Slow()
    {
        if (_reader.UnreadSpan.Length >= 8)
        {
            var result = BinaryPrimitives.ReadInt64BigEndian(_reader.UnreadSpan);
            _reader.Advance(8);
            return result;
        }
        Span<byte> buffer = stackalloc byte[8];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(8);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUInt16()
    {
        if (_isContiguous)
        {
            if (_position + 2 > _span.Length)
                ThrowInsufficientData();
            var result = BinaryPrimitives.ReadUInt16BigEndian(_span.Slice(_position));
            _position += 2;
            return result;
        }
        return ReadUInt16Slow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ushort ReadUInt16Slow()
    {
        if (_reader.UnreadSpan.Length >= 2)
        {
            var result = BinaryPrimitives.ReadUInt16BigEndian(_reader.UnreadSpan);
            _reader.Advance(2);
            return result;
        }
        Span<byte> buffer = stackalloc byte[2];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(2);
        return BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        if (_isContiguous)
        {
            if (_position + 4 > _span.Length)
                ThrowInsufficientData();
            var result = BinaryPrimitives.ReadUInt32BigEndian(_span.Slice(_position));
            _position += 4;
            return result;
        }
        return ReadUInt32Slow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private uint ReadUInt32Slow()
    {
        if (_reader.UnreadSpan.Length >= 4)
        {
            var result = BinaryPrimitives.ReadUInt32BigEndian(_reader.UnreadSpan);
            _reader.Advance(4);
            return result;
        }
        Span<byte> buffer = stackalloc byte[4];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(4);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Guid ReadUuid()
    {
        if (_isContiguous)
        {
            if (_position + 16 > _span.Length)
                ThrowInsufficientData();
            var result = new Guid(_span.Slice(_position, 16), bigEndian: true);
            _position += 16;
            return result;
        }
        return ReadUuidSlow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Guid ReadUuidSlow()
    {
        if (_reader.UnreadSpan.Length >= 16)
        {
            var result = new Guid(_reader.UnreadSpan[..16], bigEndian: true);
            _reader.Advance(16);
            return result;
        }
        Span<byte> buffer = stackalloc byte[16];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(16);
        return new Guid(buffer, bigEndian: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadFloat64()
    {
        if (_isContiguous)
        {
            if (_position + 8 > _span.Length)
                ThrowInsufficientData();
            var result = BinaryPrimitives.ReadDoubleBigEndian(_span.Slice(_position));
            _position += 8;
            return result;
        }
        return ReadFloat64Slow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private double ReadFloat64Slow()
    {
        if (_reader.UnreadSpan.Length >= 8)
        {
            var result = BinaryPrimitives.ReadDoubleBigEndian(_reader.UnreadSpan);
            _reader.Advance(8);
            return result;
        }
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadUnsignedVarInt()
    {
        return (int)ReadVarUInt();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint ReadVarUInt()
    {
        if (_isContiguous)
        {
            return ReadVarUIntFast();
        }
        return ReadVarUIntSlow();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint ReadVarUIntFast()
    {
        if (_position >= _span.Length)
            ThrowInsufficientData();

        // Fast path: single byte (0-127) - most common case
        var b0 = _span[_position];
        if ((b0 & 0x80) == 0)
        {
            _position++;
            return b0;
        }

        // Two-byte path (128-16383) - second most common
        if (_position + 1 >= _span.Length)
            ThrowInsufficientData();

        var b1 = _span[_position + 1];
        if ((b1 & 0x80) == 0)
        {
            _position += 2;
            return (uint)(b0 & 0x7F) | ((uint)b1 << 7);
        }

        return ReadVarUIntSlowContiguous();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private uint ReadVarUIntSlowContiguous()
    {
        uint result = 0;
        var shift = 0;
        while (shift < 35)
        {
            if (_position >= _span.Length)
                ThrowInsufficientData();
            var b = _span[_position++];
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return result;
            shift += 7;
        }
        ThrowMalformedVarInt();
        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private uint ReadVarUIntSlow()
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong ReadVarULong()
    {
        if (_isContiguous)
        {
            return ReadVarULongFast();
        }
        return ReadVarULongSlow();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong ReadVarULongFast()
    {
        // Fast path for contiguous memory - direct span access
        ulong result = 0;
        var shift = 0;

        while (shift < 70)
        {
            if (_position >= _span.Length)
                ThrowInsufficientData();

            var b = _span[_position++];
            result |= (ulong)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return result;

            shift += 7;
        }

        ThrowMalformedVarInt();
        return 0; // Unreachable
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ulong ReadVarULongSlow()
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

    /// <summary>
    /// Reads string content of the specified length without additional allocation for single-segment data.
    /// </summary>
    public string ReadStringContent(int length)
    {
        if (length == 0)
            return string.Empty;

        if (_isContiguous)
        {
            if (_position + length > _span.Length)
                ThrowInsufficientData();
            var result = Encoding.UTF8.GetString(_span.Slice(_position, length));
            _position += length;
            return result;
        }

        return ReadStringContentSlow(length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string ReadStringContentSlow(int length)
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
        if (_isContiguous)
        {
            if (_position + length > _span.Length)
                ThrowInsufficientData();
            _span.Slice(_position, length).CopyTo(result);
            _position += length;
            return result;
        }

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
        if (_isContiguous)
        {
            if (_position + destination.Length > _span.Length)
                ThrowInsufficientData();
            _span.Slice(_position, destination.Length).CopyTo(destination);
            _position += destination.Length;
            return;
        }

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
    /// Reads a slice of the underlying buffer as ReadOnlyMemory without copying,
    /// if the data is in a single contiguous segment. Falls back to copying if data spans segments.
    /// This is optimized for the common case where protocol messages fit in a single buffer segment.
    /// </summary>
    public ReadOnlyMemory<byte> ReadMemorySlice(int count)
    {
        if (count == 0)
            return ReadOnlyMemory<byte>.Empty;

        // Fast path: Return a slice of the backing memory (zero-allocation)
        if (_isContiguous && _hasMemory)
        {
            if (_position + count > _span.Length)
                ThrowInsufficientData();
            var result = _memory.Slice(_position, count);
            _position += count;
            return result;
        }

        // Fallback for span-only mode: must allocate since spans can't become Memory
        if (_isContiguous)
        {
            if (_position + count > _span.Length)
                ThrowInsufficientData();
            var result = _span.Slice(_position, count).ToArray();
            _position += count;
            return result;
        }

        // Multi-segment: try fast path first
        if (_reader.UnreadSpan.Length >= count)
        {
            // Get the underlying memory from the current position
            var sequence = _reader.UnreadSequence;
            var first = sequence.First;
            var result = first.Slice(0, count);
            _reader.Advance(count);
            return result;
        }

        // Slow path: data spans multiple segments, need to copy
        var buffer = new byte[count];
        if (!_reader.TryCopyTo(buffer))
            ThrowInsufficientData();
        _reader.Advance(count);
        return buffer;
    }

    /// <summary>
    /// Reads an array with 4-byte length prefix (legacy format).
    /// </summary>
    public T[] ReadArray<T>(ReadFunc<T> readItem)
    {
        var length = ReadInt32();
        if (length <= 0)
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
        if (length <= 0)
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

        if (_isContiguous)
        {
            if (_position + count > _span.Length)
                ThrowInsufficientData();
            _position += count;
            return;
        }

        if (_reader.Remaining < count)
            ThrowInsufficientData();
        _reader.Advance(count);
    }

    /// <summary>
    /// Gets the remaining unread data as a sequence.
    /// </summary>
    public readonly ReadOnlySequence<byte> GetRemainingSequence()
    {
        // Fast path: Return a slice of the backing memory (zero-allocation)
        if (_isContiguous && _hasMemory)
        {
            return new ReadOnlySequence<byte>(_memory.Slice(_position));
        }

        // Fallback for span-only mode: must allocate since spans can't become Memory
        if (_isContiguous)
        {
            return new ReadOnlySequence<byte>(_span.Slice(_position).ToArray());
        }

        return _reader.UnreadSequence;
    }

    /// <summary>
    /// Reads an array with 4-byte length prefix (legacy format).
    /// State-passing overload to avoid closure allocations.
    /// </summary>
    public T[] ReadArray<T, TState>(ReadFunc<T, TState> readItem, TState state)
    {
        var length = ReadInt32();
        if (length <= 0)
            return [];

        var result = new T[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = readItem(ref this, state);
        }
        return result;
    }

    /// <summary>
    /// Reads a compact array with unsigned varint length prefix (flexible format).
    /// State-passing overload to avoid closure allocations.
    /// </summary>
    public T[] ReadCompactArray<T, TState>(ReadFunc<T, TState> readItem, TState state)
    {
        var length = ReadUnsignedVarInt() - 1;
        if (length <= 0)
            return [];

        var result = new T[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = readItem(ref this, state);
        }
        return result;
    }

    /// <summary>
    /// Delegate for reading a single item in an array.
    /// </summary>
    public delegate T ReadFunc<out T>(ref KafkaProtocolReader reader);

    /// <summary>
    /// Delegate for reading a single item in an array with state to avoid closure allocations.
    /// </summary>
    public delegate T ReadFunc<out T, in TState>(ref KafkaProtocolReader reader, TState state);

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
