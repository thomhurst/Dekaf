using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dekaf.Protocol;

/// <summary>
/// Zero-allocation binary encoder for Kafka protocol messages.
/// This is a ref struct to ensure stack-only allocation and no GC pressure.
/// </summary>
public ref struct KafkaProtocolWriter
{
    private readonly IBufferWriter<byte> _output;
    private int _bytesWritten;

    public KafkaProtocolWriter(IBufferWriter<byte> output)
    {
        _output = output;
        _bytesWritten = 0;
    }

    public readonly int BytesWritten => _bytesWritten;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt8(sbyte value)
    {
        var span = _output.GetSpan(1);
        span[0] = (byte)value;
        _output.Advance(1);
        _bytesWritten += 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt8(byte value)
    {
        var span = _output.GetSpan(1);
        span[0] = value;
        _output.Advance(1);
        _bytesWritten += 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt16(short value)
    {
        var span = _output.GetSpan(2);
        BinaryPrimitives.WriteInt16BigEndian(span, value);
        _output.Advance(2);
        _bytesWritten += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt32(int value)
    {
        var span = _output.GetSpan(4);
        BinaryPrimitives.WriteInt32BigEndian(span, value);
        _output.Advance(4);
        _bytesWritten += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt64(long value)
    {
        var span = _output.GetSpan(8);
        BinaryPrimitives.WriteInt64BigEndian(span, value);
        _output.Advance(8);
        _bytesWritten += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt16(ushort value)
    {
        var span = _output.GetSpan(2);
        BinaryPrimitives.WriteUInt16BigEndian(span, value);
        _output.Advance(2);
        _bytesWritten += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt32(uint value)
    {
        var span = _output.GetSpan(4);
        BinaryPrimitives.WriteUInt32BigEndian(span, value);
        _output.Advance(4);
        _bytesWritten += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUuid(Guid value)
    {
        var span = _output.GetSpan(16);
        value.TryWriteBytes(span, bigEndian: true, out _);
        _output.Advance(16);
        _bytesWritten += 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat64(double value)
    {
        var span = _output.GetSpan(8);
        BinaryPrimitives.WriteDoubleBigEndian(span, value);
        _output.Advance(8);
        _bytesWritten += 8;
    }

    /// <summary>
    /// Writes a variable-length integer using ZigZag encoding.
    /// Used in flexible (compact) protocol versions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVarInt(int value)
    {
        WriteVarUInt(ZigZagEncode(value));
    }

    /// <summary>
    /// Writes a variable-length long using ZigZag encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVarLong(long value)
    {
        WriteVarULong(ZigZagEncode(value));
    }

    /// <summary>
    /// Writes an unsigned variable-length integer.
    /// Used for array lengths and string lengths in compact format.
    /// </summary>
    public void WriteUnsignedVarInt(int value)
    {
        WriteVarUInt((uint)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteVarUInt(uint value)
    {
        // Fast path: single byte (0-127) - most common case
        if (value < 0x80)
        {
            var span = _output.GetSpan(1);
            span[0] = (byte)value;
            _output.Advance(1);
            _bytesWritten += 1;
            return;
        }

        // Two-byte path (128-16383) - second most common
        if (value < 0x4000)
        {
            var span = _output.GetSpan(2);
            span[0] = (byte)(value | 0x80);
            span[1] = (byte)(value >> 7);
            _output.Advance(2);
            _bytesWritten += 2;
            return;
        }

        WriteVarUIntSlow(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteVarUIntSlow(uint value)
    {
        var span = _output.GetSpan(5);
        var pos = 0;
        while (value >= 0x80)
        {
            span[pos++] = (byte)(value | 0x80);
            value >>= 7;
        }
        span[pos++] = (byte)value;
        _output.Advance(pos);
        _bytesWritten += pos;
    }

    private void WriteVarULong(ulong value)
    {
        // Maximum 10 bytes for a 64-bit varint
        // Write directly to output span to avoid stackalloc + copy overhead
        var span = _output.GetSpan(10);
        var pos = 0;

        while (value >= 0x80)
        {
            span[pos++] = (byte)(value | 0x80);
            value >>= 7;
        }
        span[pos++] = (byte)value;

        _output.Advance(pos);
        _bytesWritten += pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ZigZagEncode(int value) => (uint)((value << 1) ^ (value >> 31));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ZigZagEncode(long value) => (ulong)((value << 1) ^ (value >> 63));

    /// <summary>
    /// Writes a boolean as a single byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBoolean(bool value)
    {
        WriteUInt8(value ? (byte)1 : (byte)0);
    }

    /// <summary>
    /// Writes a string with 2-byte length prefix (legacy format).
    /// </summary>
    public void WriteString(string? value)
    {
        if (value is null)
        {
            WriteInt16(-1);
            return;
        }

        // Fast path for short strings (topic names, client IDs, etc.)
        // Avoids double-pass of GetByteCount + GetBytes by using stackalloc buffer
        if (value.Length <= 128)
        {
            // Max UTF-8 expansion is 4 bytes per char (surrogate pairs)
            // For 128 chars, 512 bytes covers all cases including emojis
            Span<byte> buffer = stackalloc byte[512];
            var actualBytes = Encoding.UTF8.GetBytes(value, buffer);
            WriteInt16((short)actualBytes);
            if (actualBytes > 0)
            {
                var outputSpan = _output.GetSpan(actualBytes);
                buffer[..actualBytes].CopyTo(outputSpan);
                _output.Advance(actualBytes);
                _bytesWritten += actualBytes;
            }
            return;
        }

        // Slow path for long strings
        var byteCount = Encoding.UTF8.GetByteCount(value);
        WriteInt16((short)byteCount);

        if (byteCount > 0)
        {
            var span = _output.GetSpan(byteCount);
            Encoding.UTF8.GetBytes(value, span);
            _output.Advance(byteCount);
            _bytesWritten += byteCount;
        }
    }

    /// <summary>
    /// Writes a string's UTF-8 bytes without any length prefix.
    /// Use when length is written separately (e.g., with VarInt).
    /// </summary>
    public void WriteStringContent(string value)
    {
        // Fast path for short strings
        // Avoids double-pass of GetByteCount + GetBytes by using stackalloc buffer
        if (value.Length <= 128)
        {
            // Max UTF-8 expansion is 4 bytes per char (surrogate pairs)
            Span<byte> buffer = stackalloc byte[512];
            var actualBytes = Encoding.UTF8.GetBytes(value, buffer);
            if (actualBytes > 0)
            {
                var outputSpan = _output.GetSpan(actualBytes);
                buffer[..actualBytes].CopyTo(outputSpan);
                _output.Advance(actualBytes);
                _bytesWritten += actualBytes;
            }
            return;
        }

        // Slow path for long strings
        var byteCount = Encoding.UTF8.GetByteCount(value);
        if (byteCount > 0)
        {
            var span = _output.GetSpan(byteCount);
            Encoding.UTF8.GetBytes(value, span);
            _output.Advance(byteCount);
            _bytesWritten += byteCount;
        }
    }

    /// <summary>
    /// Writes a string with 2-byte length prefix (legacy format) from a span.
    /// </summary>
    public void WriteString(ReadOnlySpan<char> value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        WriteInt16((short)byteCount);

        if (byteCount > 0)
        {
            var span = _output.GetSpan(byteCount);
            Encoding.UTF8.GetBytes(value, span);
            _output.Advance(byteCount);
            _bytesWritten += byteCount;
        }
    }

    /// <summary>
    /// Writes a compact string with unsigned varint length prefix (flexible format).
    /// Length is encoded as length + 1 (0 means null).
    /// </summary>
    public void WriteCompactString(string? value)
    {
        if (value is null)
        {
            WriteUnsignedVarInt(0);
            return;
        }

        // Fast path for short strings (topic names, header keys, etc.)
        // Avoids double-pass of GetByteCount + GetBytes by using stackalloc buffer
        if (value.Length <= 128)
        {
            // Max UTF-8 expansion is 4 bytes per char (surrogate pairs)
            // For 128 chars, 512 bytes covers all cases including emojis
            Span<byte> buffer = stackalloc byte[512];
            var actualBytes = Encoding.UTF8.GetBytes(value, buffer);
            WriteUnsignedVarInt(actualBytes + 1);
            if (actualBytes > 0)
            {
                var outputSpan = _output.GetSpan(actualBytes);
                buffer[..actualBytes].CopyTo(outputSpan);
                _output.Advance(actualBytes);
                _bytesWritten += actualBytes;
            }
            return;
        }

        // Slow path for long strings
        var byteCount = Encoding.UTF8.GetByteCount(value);
        WriteUnsignedVarInt(byteCount + 1);

        if (byteCount > 0)
        {
            var span = _output.GetSpan(byteCount);
            Encoding.UTF8.GetBytes(value, span);
            _output.Advance(byteCount);
            _bytesWritten += byteCount;
        }
    }

    /// <summary>
    /// Writes a compact string with unsigned varint length prefix (flexible format) from a span.
    /// </summary>
    public void WriteCompactString(ReadOnlySpan<char> value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        WriteUnsignedVarInt(byteCount + 1);

        if (byteCount > 0)
        {
            var span = _output.GetSpan(byteCount);
            Encoding.UTF8.GetBytes(value, span);
            _output.Advance(byteCount);
            _bytesWritten += byteCount;
        }
    }

    /// <summary>
    /// Writes a compact nullable string.
    /// </summary>
    public void WriteCompactNullableString(string? value)
    {
        WriteCompactString(value);
    }

    /// <summary>
    /// Writes raw bytes with 4-byte length prefix (legacy format).
    /// </summary>
    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        WriteInt32(value.Length);
        WriteRawBytes(value);
    }

    /// <summary>
    /// Writes nullable bytes with 4-byte length prefix.
    /// </summary>
    public void WriteNullableBytes(ReadOnlySpan<byte> value, bool isNull)
    {
        if (isNull)
        {
            WriteInt32(-1);
            return;
        }
        WriteBytes(value);
    }

    /// <summary>
    /// Writes compact bytes with unsigned varint length prefix.
    /// </summary>
    public void WriteCompactBytes(ReadOnlySpan<byte> value)
    {
        WriteUnsignedVarInt(value.Length + 1);
        WriteRawBytes(value);
    }

    /// <summary>
    /// Writes compact nullable bytes.
    /// </summary>
    public void WriteCompactNullableBytes(ReadOnlySpan<byte> value, bool isNull)
    {
        if (isNull)
        {
            WriteUnsignedVarInt(0);
            return;
        }
        WriteCompactBytes(value);
    }

    /// <summary>
    /// Writes raw bytes without a length prefix.
    /// </summary>
    public void WriteRawBytes(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
            return;

        var span = _output.GetSpan(value.Length);
        value.CopyTo(span);
        _output.Advance(value.Length);
        _bytesWritten += value.Length;
    }

    /// <summary>
    /// Writes an array with 4-byte length prefix (legacy format).
    /// </summary>
    public void WriteArray<T>(ReadOnlySpan<T> items, WriteAction<T> writeItem)
    {
        WriteInt32(items.Length);
        foreach (ref readonly var item in items)
        {
            writeItem(ref this, item);
        }
    }

    /// <summary>
    /// Writes an array with 4-byte length prefix (legacy format).
    /// Zero-allocation overload for IReadOnlyList.
    /// </summary>
    public void WriteArray<T>(IReadOnlyList<T> items, WriteAction<T> writeItem)
    {
        WriteInt32(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            writeItem(ref this, items[i]);
        }
    }

    /// <summary>
    /// Writes a nullable array with 4-byte length prefix.
    /// </summary>
    public void WriteNullableArray<T>(ReadOnlySpan<T> items, WriteAction<T> writeItem, bool isNull)
    {
        if (isNull)
        {
            WriteInt32(-1);
            return;
        }
        WriteArray(items, writeItem);
    }

    /// <summary>
    /// Writes a nullable array with 4-byte length prefix.
    /// Zero-allocation overload for IReadOnlyList.
    /// </summary>
    public void WriteNullableArray<T>(IReadOnlyList<T>? items, WriteAction<T> writeItem)
    {
        if (items is null)
        {
            WriteInt32(-1);
            return;
        }
        WriteArray(items, writeItem);
    }

    /// <summary>
    /// Writes a compact array with unsigned varint length prefix (flexible format).
    /// Length is encoded as length + 1 (0 means null).
    /// </summary>
    public void WriteCompactArray<T>(ReadOnlySpan<T> items, WriteAction<T> writeItem)
    {
        WriteUnsignedVarInt(items.Length + 1);
        foreach (ref readonly var item in items)
        {
            writeItem(ref this, item);
        }
    }

    /// <summary>
    /// Writes a compact array with unsigned varint length prefix (flexible format).
    /// Zero-allocation overload for IReadOnlyList.
    /// </summary>
    public void WriteCompactArray<T>(IReadOnlyList<T> items, WriteAction<T> writeItem)
    {
        WriteUnsignedVarInt(items.Count + 1);
        for (var i = 0; i < items.Count; i++)
        {
            writeItem(ref this, items[i]);
        }
    }

    /// <summary>
    /// Writes a compact nullable array.
    /// </summary>
    public void WriteCompactNullableArray<T>(ReadOnlySpan<T> items, WriteAction<T> writeItem, bool isNull)
    {
        if (isNull)
        {
            WriteUnsignedVarInt(0);
            return;
        }
        WriteCompactArray(items, writeItem);
    }

    /// <summary>
    /// Writes a compact nullable array.
    /// Zero-allocation overload for IReadOnlyList.
    /// </summary>
    public void WriteCompactNullableArray<T>(IReadOnlyList<T>? items, WriteAction<T> writeItem)
    {
        if (items is null)
        {
            WriteUnsignedVarInt(0);
            return;
        }
        WriteCompactArray(items, writeItem);
    }

    /// <summary>
    /// Writes an empty tagged fields section (for flexible versions).
    /// </summary>
    public void WriteEmptyTaggedFields()
    {
        WriteUnsignedVarInt(0);
    }

    /// <summary>
    /// Reserves space for a length prefix and returns a position marker.
    /// Call WriteLength after writing the content to fill in the length.
    /// </summary>
    public LengthPlaceholder ReserveLengthPrefix()
    {
        var startPosition = _bytesWritten;
        WriteInt32(0); // Placeholder
        return new LengthPlaceholder(startPosition, _bytesWritten);
    }

    /// <summary>
    /// Delegate for writing a single item in an array.
    /// </summary>
    public delegate void WriteAction<T>(ref KafkaProtocolWriter writer, T item);
}

/// <summary>
/// Represents a placeholder for a length prefix that will be filled in later.
/// </summary>
public readonly struct LengthPlaceholder(int lengthPosition, int contentStartPosition)
{
    public int LengthPosition { get; } = lengthPosition;
    public int ContentStartPosition { get; } = contentStartPosition;

    public int CalculateLength(int currentPosition) => currentPosition - ContentStartPosition;
}
