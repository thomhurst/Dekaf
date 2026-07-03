using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dekaf.Serialization;

namespace Dekaf.Protocol.Records;

/// <summary>
/// A single record within a RecordBatch.
/// Uses variable-length encoding for efficiency.
/// Key and Value use ReadOnlyMemory to avoid copying data from the network buffer.
/// This is a struct to avoid heap allocations in the hot path.
/// </summary>
public readonly record struct Record
{
    public int Length { get; init; }
    public byte Attributes { get; init; }
    public long TimestampDelta { get; init; }
    public int OffsetDelta { get; init; }
    public ReadOnlyMemory<byte> Key { get; init; }
    public ReadOnlyMemory<byte> Value { get; init; }
    public Header[]? Headers { get; init; }

    /// <summary>
    /// The number of valid headers in the Headers array.
    /// Required because the array may be rented from ArrayPool and oversized.
    /// </summary>
    public int HeaderCount { get; init; }

    /// <summary>
    /// Returns true if the key is null (empty memory with special flag).
    /// </summary>
    public bool IsKeyNull { get; init; }

    /// <summary>
    /// Returns true if the value is null (empty memory with special flag).
    /// </summary>
    public bool IsValueNull { get; init; }

    /// <summary>
    /// Pre-computed body size to avoid redundant calculation during Write().
    /// Set at record creation time; 0 means not pre-computed (use CalculateBodySize()).
    /// </summary>
    internal int CachedBodySize { get; init; }

    /// <summary>
    /// Gets the effective header count, handling both exact-sized and pooled arrays.
    /// </summary>
    /// <remarks>
    /// Invariant: HeaderCount always equals the actual number of valid headers.
    /// When Headers is a pooled array (from Record.Read), HeaderCount &lt; Headers.Length is expected.
    /// When Headers is an exact-sized array (from producer path), HeaderCount == Headers.Length.
    /// </remarks>
    internal int EffectiveHeaderCount => Headers is null ? 0 : HeaderCount;

    /// <summary>
    /// Writes the record to the protocol writer.
    /// </summary>
    public void Write(ref KafkaProtocolWriter writer)
    {
        // First calculate the record body size
        var bodySize = CachedBodySize > 0
            ? CachedBodySize
            : ComputeBodySize(TimestampDelta, OffsetDelta, IsKeyNull, Key.Length, IsValueNull, Value.Length, Headers, EffectiveHeaderCount);

        // Write length as varint
        writer.WriteVarInt(bodySize);

        // Write attributes (always 0 for now)
        writer.WriteInt8((sbyte)Attributes);

        // Write timestamp delta as varlong (per Kafka spec)
        writer.WriteVarLong(TimestampDelta);

        // Write offset delta as varint
        writer.WriteVarInt(OffsetDelta);

        // Write key
        if (IsKeyNull)
        {
            writer.WriteVarInt(-1);
        }
        else
        {
            writer.WriteVarInt(Key.Length);
            writer.WriteRawBytes(Key.Span);
        }

        // Write value
        if (IsValueNull)
        {
            writer.WriteVarInt(-1);
        }
        else
        {
            writer.WriteVarInt(Value.Length);
            writer.WriteRawBytes(Value.Span);
        }

        // Write headers
        var effectiveHeaderCount = EffectiveHeaderCount;
        writer.WriteVarInt(effectiveHeaderCount);

        if (Headers is not null)
        {
            for (var i = 0; i < effectiveHeaderCount; i++)
            {
                Headers[i].Write(ref writer);
            }
        }
    }

    /// <summary>
    /// Reads a record from the protocol reader.
    /// The returned Record's Key and Value reference memory from the reader's buffer.
    /// </summary>
    public static Record Read(ref KafkaProtocolReader reader)
    {
        var length = reader.ReadVarInt();
        if (length < 0)
            throw new MalformedProtocolDataException($"Invalid record length {length}");

        var bodyStart = reader.Consumed;
        var attributes = (byte)reader.ReadInt8();
        var timestampDelta = reader.ReadVarLong();
        var offsetDelta = reader.ReadVarInt();

        var keyLength = reader.ReadVarInt();
        var isKeyNull = keyLength < 0;
        var key = isKeyNull ? ReadOnlyMemory<byte>.Empty : reader.ReadMemorySlice(keyLength);

        var valueLength = reader.ReadVarInt();
        var isValueNull = valueLength < 0;
        var value = isValueNull ? ReadOnlyMemory<byte>.Empty : reader.ReadMemorySlice(valueLength);

        var headerCount = reader.ReadVarInt();
        ValidateHeaderCount(headerCount, length, reader.Consumed - bodyStart, reader.Remaining);

        Header[]? headers = null;

        if (headerCount > 0)
        {
            // Rent from ArrayPool to avoid per-record allocation.
            // The rented array may be oversized; HeaderCount tracks the valid count.
            // The array is returned to the pool when the owning LazyRecordList is disposed.
            headers = ArrayPool<Header>.Shared.Rent(headerCount);
            for (var i = 0; i < headerCount; i++)
            {
                headers[i] = Header.Read(ref reader);
            }
        }

        return new Record
        {
            Length = length,
            Attributes = attributes,
            TimestampDelta = timestampDelta,
            OffsetDelta = offsetDelta,
            Key = key,
            IsKeyNull = isKeyNull,
            Value = value,
            IsValueNull = isValueNull,
            Headers = headers,
            HeaderCount = headerCount
        };
    }

    private static void ValidateHeaderCount(int headerCount, int recordBodyLength, long bodyBytesRead, long readerRemaining)
    {
        var remainingInRecord = recordBodyLength - bodyBytesRead;
        if (remainingInRecord < 0 || headerCount < 0)
            throw new MalformedProtocolDataException($"Invalid record header count {headerCount}");

        // A header needs at least one byte for key length and one byte for value length.
        // Bound the declared record body by the actual readable bytes so corrupt frames
        // cannot force a giant ArrayPool rent before the first header read fails.
        var readableRecordBytes = Math.Min(remainingInRecord, readerRemaining);
        if (headerCount > readableRecordBytes / 2)
            throw new MalformedProtocolDataException($"Invalid record header count {headerCount}");
    }

    /// <summary>
    /// Copies headers from a (potentially pooled/oversized) array into an owned array
    /// that safely outlives the record batch. Returns null if no headers.
    /// </summary>
    internal static IReadOnlyList<Header>? CopyHeaders(Header[]? headers, int headerCount)
    {
        if (headers is null || headerCount == 0)
            return null;

        var result = new Header[headerCount];
        headers.AsSpan(0, headerCount).CopyTo(result);
        return result;
    }

    internal static int ComputeBodySize(long timestampDelta, int offsetDelta, bool isKeyNull, int keyLength, bool isValueNull, int valueLength, Header[]? headers, int headerCount)
    {
        var size = 1; // attributes

        size += VarLongSize(timestampDelta);
        size += VarIntSize(offsetDelta);

        if (isKeyNull)
        {
            size += VarIntSize(-1);
        }
        else
        {
            size += VarIntSize(keyLength);
            size += keyLength;
        }

        if (isValueNull)
        {
            size += VarIntSize(-1);
        }
        else
        {
            size += VarIntSize(valueLength);
            size += valueLength;
        }

        size += VarIntSize(headerCount);

        if (headers is not null)
        {
            for (var i = 0; i < headerCount; i++)
            {
                size += headers[i].CalculateSize();
            }
        }

        return size;
    }

    /// <summary>
    /// Encodes a record directly into a fixed-size destination span using the Kafka record
    /// wire format (length varint + body). The destination length must equal
    /// <c>VarIntSize(bodySize) + bodySize</c> where <paramref name="bodySize"/> was computed by
    /// <see cref="ComputeBodySize"/> with the same arguments. Every byte written here must be
    /// counted there — keep the two methods in sync.
    /// </summary>
    internal static void Encode(
        Span<byte> destination,
        int bodySize,
        long timestampDelta,
        int offsetDelta,
        ReadOnlySpan<byte> keyData,
        bool isKeyNull,
        ReadOnlySpan<byte> valueData,
        bool isValueNull,
        Header[]? headers,
        int headerCount)
    {
        var offset = 0;

        WriteVarInt(destination, ref offset, bodySize);
        destination[offset++] = 0; // record attributes
        WriteVarLong(destination, ref offset, timestampDelta);
        WriteVarInt(destination, ref offset, offsetDelta);

        if (isKeyNull)
        {
            WriteVarInt(destination, ref offset, -1);
        }
        else
        {
            WriteVarInt(destination, ref offset, keyData.Length);
            keyData.CopyTo(destination[offset..]);
            offset += keyData.Length;
        }

        if (isValueNull)
        {
            WriteVarInt(destination, ref offset, -1);
        }
        else
        {
            WriteVarInt(destination, ref offset, valueData.Length);
            valueData.CopyTo(destination[offset..]);
            offset += valueData.Length;
        }

        WriteVarInt(destination, ref offset, headerCount);
        if (headers is not null)
        {
            for (var i = 0; i < headerCount; i++)
            {
                headers[i].Encode(destination, ref offset);
            }
        }

        if (offset != destination.Length)
            ThrowEncodedSizeMismatch(offset, destination.Length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowEncodedSizeMismatch(int bytesWritten, int expectedBytes)
    {
        throw new InvalidOperationException(
            $"Record.Encode wrote {bytesWritten} bytes but destination length is {expectedBytes}. " +
            "Record.ComputeBodySize and Record.Encode are out of sync.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteVarInt(Span<byte> destination, ref int offset, int value)
    {
        var zigzag = (uint)((value << 1) ^ (value >> 31));
        while (zigzag >= 0x80)
        {
            destination[offset++] = (byte)(zigzag | 0x80);
            zigzag >>= 7;
        }

        destination[offset++] = (byte)zigzag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteVarLong(Span<byte> destination, ref int offset, long value)
    {
        var zigzag = (ulong)((value << 1) ^ (value >> 63));
        while (zigzag >= 0x80)
        {
            destination[offset++] = (byte)(zigzag | 0x80);
            zigzag >>= 7;
        }

        destination[offset++] = (byte)zigzag;
    }

    internal static int VarIntSize(int value)
    {
        var zigzag = (uint)((value << 1) ^ (value >> 31));
        return VarUIntSize(zigzag);
    }

    internal static int VarLongSize(long value)
    {
        var zigzag = (ulong)((value << 1) ^ (value >> 63));
        return VarULongSize(zigzag);
    }

    internal static int VarUIntSize(uint value)
    {
        return (BitOperations.Log2(value | 1u) / 7) + 1;
    }

    internal static int VarULongSize(ulong value)
    {
        return (BitOperations.Log2(value | 1ul) / 7) + 1;
    }
}
