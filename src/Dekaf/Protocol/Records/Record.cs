namespace Dekaf.Protocol.Records;

/// <summary>
/// A single record within a RecordBatch.
/// Uses variable-length encoding for efficiency.
/// Key and Value use ReadOnlyMemory to avoid copying data from the network buffer.
/// </summary>
public sealed class Record
{
    public int Length { get; init; }
    public byte Attributes { get; init; }
    public int TimestampDelta { get; init; }
    public int OffsetDelta { get; init; }
    public ReadOnlyMemory<byte> Key { get; init; }
    public ReadOnlyMemory<byte> Value { get; init; }
    public IReadOnlyList<RecordHeader>? Headers { get; init; }

    /// <summary>
    /// Returns true if the key is null (empty memory with special flag).
    /// </summary>
    public bool IsKeyNull { get; init; }

    /// <summary>
    /// Returns true if the value is null (empty memory with special flag).
    /// </summary>
    public bool IsValueNull { get; init; }

    /// <summary>
    /// Writes the record to the protocol writer.
    /// </summary>
    public void Write(ref KafkaProtocolWriter writer)
    {
        // First calculate the record body size
        var bodySize = CalculateBodySize();

        // Write length as varint
        writer.WriteVarInt(bodySize);

        // Write attributes (always 0 for now)
        writer.WriteInt8((sbyte)Attributes);

        // Write timestamp delta as varint
        writer.WriteVarInt(TimestampDelta);

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
        var headerCount = Headers?.Count ?? 0;
        writer.WriteVarInt(headerCount);

        if (Headers is not null)
        {
            foreach (var header in Headers)
            {
                header.Write(ref writer);
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
        var timestampDelta = reader.ReadVarInt();
        var offsetDelta = reader.ReadVarInt();

        var keyLength = reader.ReadVarInt();
        var isKeyNull = keyLength < 0;
        var key = isKeyNull ? ReadOnlyMemory<byte>.Empty : reader.ReadMemorySlice(keyLength);

        var valueLength = reader.ReadVarInt();
        var isValueNull = valueLength < 0;
        var value = isValueNull ? ReadOnlyMemory<byte>.Empty : reader.ReadMemorySlice(valueLength);

        var headerCount = reader.ReadVarInt();
        List<RecordHeader>? headers = null;

        if (headerCount > 0)
        {
            headers = new List<RecordHeader>(headerCount);
            for (var i = 0; i < headerCount; i++)
            {
                headers.Add(RecordHeader.Read(ref reader));
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
            Headers = headers
        };
    }

    private int CalculateBodySize()
    {
        var size = 1; // attributes

        size += VarIntSize(TimestampDelta);
        size += VarIntSize(OffsetDelta);

        if (IsKeyNull)
        {
            size += VarIntSize(-1);
        }
        else
        {
            size += VarIntSize(Key.Length);
            size += Key.Length;
        }

        if (IsValueNull)
        {
            size += VarIntSize(-1);
        }
        else
        {
            size += VarIntSize(Value.Length);
            size += Value.Length;
        }

        var headerCount = Headers?.Count ?? 0;
        size += VarIntSize(headerCount);

        if (Headers is not null)
        {
            foreach (var header in Headers)
            {
                size += header.CalculateSize();
            }
        }

        return size;
    }

    internal static int VarIntSize(int value)
    {
        var zigzag = (uint)((value << 1) ^ (value >> 31));
        return VarUIntSize(zigzag);
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
}

/// <summary>
/// A header within a record.
/// Uses ReadOnlyMemory for zero-copy value storage.
/// </summary>
public sealed class RecordHeader
{
    public required string Key { get; init; }
    public ReadOnlyMemory<byte> Value { get; init; }

    /// <summary>
    /// Returns true if the value is null.
    /// </summary>
    public bool IsValueNull { get; init; }

    /// <summary>
    /// Writes the header to the protocol writer.
    /// </summary>
    public void Write(ref KafkaProtocolWriter writer)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(Key);
        writer.WriteVarInt(keyBytes.Length);
        writer.WriteRawBytes(keyBytes);

        if (IsValueNull)
        {
            writer.WriteVarInt(-1);
        }
        else
        {
            writer.WriteVarInt(Value.Length);
            writer.WriteRawBytes(Value.Span);
        }
    }

    /// <summary>
    /// Reads a header from the protocol reader.
    /// </summary>
    public static RecordHeader Read(ref KafkaProtocolReader reader)
    {
        var keyLength = reader.ReadVarInt();
        var key = reader.ReadStringContent(keyLength);

        var valueLength = reader.ReadVarInt();
        var isValueNull = valueLength < 0;
        var value = isValueNull ? ReadOnlyMemory<byte>.Empty : reader.ReadMemorySlice(valueLength);

        return new RecordHeader
        {
            Key = key,
            Value = value,
            IsValueNull = isValueNull
        };
    }

    internal int CalculateSize()
    {
        var keyBytes = System.Text.Encoding.UTF8.GetByteCount(Key);
        var size = Record.VarIntSize(keyBytes) + keyBytes;

        if (IsValueNull)
        {
            size += Record.VarIntSize(-1);
        }
        else
        {
            size += Record.VarIntSize(Value.Length) + Value.Length;
        }

        return size;
    }
}
