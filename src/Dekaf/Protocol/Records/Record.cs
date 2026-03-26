using System.Buffers;
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
    /// Gets the effective header count.
    /// </summary>
    private int EffectiveHeaderCount => HeaderCount;

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
        Header[]? headers = null;

        if (headerCount > 0)
        {
            // Rent from ArrayPool to eliminate per-message heap allocation.
            // The rented array may be oversized; HeaderCount tracks valid elements.
            // Arrays are returned to the pool by LazyRecordList.Dispose().
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
        var size = 1;
        while (value >= 0x80)
        {
            size++;
            value >>= 7;
        }
        return size;
    }

    internal static int VarULongSize(ulong value)
    {
        var size = 1;
        while (value >= 0x80)
        {
            size++;
            value >>= 7;
        }
        return size;
    }
}
