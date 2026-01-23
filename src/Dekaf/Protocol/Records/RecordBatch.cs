using System.Buffers;
using Dekaf.Compression;

namespace Dekaf.Protocol.Records;

/// <summary>
/// Kafka RecordBatch v2 format (magic byte 2).
/// This is the modern record format used since Kafka 0.11.
/// Supports lazy record parsing - records are only parsed when enumerated.
/// </summary>
public sealed class RecordBatch
{
    // Thread-local reusable buffers for serialization to avoid per-batch allocations
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_recordsBuffer;
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_crcBuffer;

    private static ArrayBufferWriter<byte> GetRecordsBuffer()
    {
        var buffer = t_recordsBuffer;
        if (buffer is null)
        {
            buffer = new ArrayBufferWriter<byte>(4096);
            t_recordsBuffer = buffer;
        }
        else
        {
            buffer.Clear();
        }
        return buffer;
    }

    private static ArrayBufferWriter<byte> GetCrcBuffer()
    {
        var buffer = t_crcBuffer;
        if (buffer is null)
        {
            buffer = new ArrayBufferWriter<byte>(4096);
            t_crcBuffer = buffer;
        }
        else
        {
            buffer.Clear();
        }
        return buffer;
    }

    public long BaseOffset { get; init; }
    public int BatchLength { get; init; }
    public int PartitionLeaderEpoch { get; init; } = -1;
    public byte Magic { get; init; } = 2;
    public uint Crc { get; init; }
    public RecordBatchAttributes Attributes { get; init; }
    public int LastOffsetDelta { get; init; }
    public long BaseTimestamp { get; init; }
    public long MaxTimestamp { get; init; }
    public long ProducerId { get; init; } = -1;
    public short ProducerEpoch { get; init; } = -1;
    public int BaseSequence { get; init; } = -1;

    /// <summary>
    /// The records in this batch. For batches created via Read(), records are parsed lazily
    /// on first enumeration to avoid allocations for unconsumed records.
    /// </summary>
    public required IReadOnlyList<Record> Records { get; init; }

    /// <summary>
    /// Writes the record batch to the output buffer.
    /// </summary>
    public void Write(IBufferWriter<byte> output, CompressionType compression = CompressionType.None, CompressionCodecRegistry? codecs = null)
    {
        var writer = new KafkaProtocolWriter(output);

        // First, serialize records to calculate length and CRC
        // Use thread-local buffer to avoid per-batch allocation
        var recordsBuffer = GetRecordsBuffer();
        var recordsWriter = new KafkaProtocolWriter(recordsBuffer);

        foreach (var record in Records)
        {
            record.Write(ref recordsWriter);
        }

        var recordsData = recordsBuffer.WrittenSpan;

        // Apply compression if needed
        ReadOnlySpan<byte> compressedRecords;
        ArrayBufferWriter<byte>? compressedBuffer = null;

        if (compression != CompressionType.None)
        {
            var registry = codecs ?? CompressionCodecRegistry.Default;
            var codec = registry.GetCodec(compression);
            compressedBuffer = new ArrayBufferWriter<byte>(recordsData.Length);
            codec.Compress(new ReadOnlySequence<byte>(recordsData.ToArray()), compressedBuffer);
            compressedRecords = compressedBuffer.WrittenSpan;
        }
        else
        {
            compressedRecords = recordsData;
        }

        // Calculate batch length: from partition leader epoch to end
        // 4 (partition leader epoch) + 1 (magic) + 4 (crc) + 2 (attributes) +
        // 4 (last offset delta) + 8 (base timestamp) + 8 (max timestamp) +
        // 8 (producer id) + 2 (producer epoch) + 4 (base sequence) + 4 (records count) + records
        var batchLength = 4 + 1 + 4 + 2 + 4 + 8 + 8 + 8 + 2 + 4 + 4 + compressedRecords.Length;

        // Write base offset and batch length
        writer.WriteInt64(BaseOffset);
        writer.WriteInt32(batchLength);

        // Write partition leader epoch
        writer.WriteInt32(PartitionLeaderEpoch);

        // Write magic byte
        writer.WriteUInt8(Magic);

        // Calculate CRC32C over everything after the CRC field
        // Use thread-local buffer to avoid per-batch allocation
        var crcBuffer = GetCrcBuffer();
        var crcWriter = new KafkaProtocolWriter(crcBuffer);

        var attributes = (short)Attributes;
        if (compression != CompressionType.None)
        {
            attributes = (short)((attributes & ~0x07) | (int)compression);
        }

        crcWriter.WriteInt16(attributes);
        crcWriter.WriteInt32(LastOffsetDelta);
        crcWriter.WriteInt64(BaseTimestamp);
        crcWriter.WriteInt64(MaxTimestamp);
        crcWriter.WriteInt64(ProducerId);
        crcWriter.WriteInt16(ProducerEpoch);
        crcWriter.WriteInt32(BaseSequence);
        crcWriter.WriteInt32(Records.Count);
        crcWriter.WriteRawBytes(compressedRecords);

        var crc = Crc32C.Compute(crcBuffer.WrittenSpan);
        writer.WriteInt32((int)crc);

        // Write the content
        writer.WriteRawBytes(crcBuffer.WrittenSpan);
    }

    /// <summary>
    /// Reads a record batch from the input buffer.
    /// Records are parsed lazily to avoid allocations for unconsumed records.
    /// </summary>
    public static RecordBatch Read(ref KafkaProtocolReader reader, CompressionCodecRegistry? codecs = null)
    {
        var baseOffset = reader.ReadInt64();
        var batchLength = reader.ReadInt32();
        var partitionLeaderEpoch = reader.ReadInt32();
        var magic = reader.ReadUInt8();

        if (magic != 2)
        {
            throw new NotSupportedException($"Unsupported record batch magic byte: {magic}. Only v2 (magic=2) is supported.");
        }

        var crc = (uint)reader.ReadInt32();
        var attributes = (RecordBatchAttributes)reader.ReadInt16();
        var lastOffsetDelta = reader.ReadInt32();
        var baseTimestamp = reader.ReadInt64();
        var maxTimestamp = reader.ReadInt64();
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt16();
        var baseSequence = reader.ReadInt32();
        var recordCount = reader.ReadInt32();

        // Calculate remaining bytes for records
        var recordsLength = batchLength - (4 + 1 + 4 + 2 + 4 + 8 + 8 + 8 + 2 + 4 + 4);

        // Check compression type from attributes
        var compression = (CompressionType)((int)attributes & 0x07);

        // Capture the raw record data
        var rawRecordData = reader.ReadMemorySlice(recordsLength);

        // Decompress if needed
        ReadOnlyMemory<byte> recordData;
        if (compression != CompressionType.None)
        {
            var registry = codecs ?? CompressionCodecRegistry.Default;
            var codec = registry.GetCodec(compression);
            var decompressedBuffer = new ArrayBufferWriter<byte>(recordsLength * 4); // Estimate 4x expansion
            codec.Decompress(new ReadOnlySequence<byte>(rawRecordData), decompressedBuffer);
            recordData = decompressedBuffer.WrittenMemory.ToArray(); // Copy to owned memory
        }
        else
        {
            recordData = rawRecordData;
        }

        // Create a lazy record list that parses on-demand
        var lazyRecords = new LazyRecordList(recordData, recordCount);

        return new RecordBatch
        {
            BaseOffset = baseOffset,
            BatchLength = batchLength,
            PartitionLeaderEpoch = partitionLeaderEpoch,
            Magic = magic,
            Crc = crc,
            Attributes = attributes,
            LastOffsetDelta = lastOffsetDelta,
            BaseTimestamp = baseTimestamp,
            MaxTimestamp = maxTimestamp,
            ProducerId = producerId,
            ProducerEpoch = producerEpoch,
            BaseSequence = baseSequence,
            Records = lazyRecords
        };
    }
}

/// <summary>
/// A lazy list that parses records on-demand from raw byte data.
/// Records are only parsed when accessed, avoiding allocations for unconsumed records.
/// Uses incremental list growth instead of pre-allocating full array.
/// </summary>
internal sealed class LazyRecordList : IReadOnlyList<Record>
{
    private readonly ReadOnlyMemory<byte> _rawData;
    private readonly int _count;
    private List<Record>? _parsedRecords;
    private int _nextParseOffset;

    public LazyRecordList(ReadOnlyMemory<byte> rawData, int count)
    {
        _rawData = rawData;
        _count = count;
    }

    public int Count => _count;

    public Record this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            EnsureParsedUpTo(index);
            return _parsedRecords![index];
        }
    }

    public IEnumerator<Record> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
        {
            yield return this[i];
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private void EnsureParsedUpTo(int index)
    {
        // Initialize list on first access - don't pre-allocate full capacity
        // This avoids allocating space for all records when only a few are needed
        _parsedRecords ??= new List<Record>();

        while (_parsedRecords.Count <= index && _parsedRecords.Count < _count)
        {
            var slice = _rawData.Slice(_nextParseOffset);
            var reader = new KafkaProtocolReader(slice);
            var record = Record.Read(ref reader);
            _parsedRecords.Add(record);
            _nextParseOffset += (int)reader.Consumed;
        }
    }
}

/// <summary>
/// Record batch attributes bit flags.
/// </summary>
[Flags]
public enum RecordBatchAttributes : short
{
    None = 0,

    // Compression type (bits 0-2)
    CompressionNone = 0,
    CompressionGzip = 1,
    CompressionSnappy = 2,
    CompressionLz4 = 3,
    CompressionZstd = 4,

    // Timestamp type (bit 3)
    TimestampTypeCreateTime = 0,
    TimestampTypeLogAppendTime = 0x08,

    // Is transactional (bit 4)
    IsTransactional = 0x10,

    // Is control batch (bit 5)
    IsControlBatch = 0x20,

    // Has delete horizon (bit 6)
    HasDeleteHorizon = 0x40
}

/// <summary>
/// Compression types supported by Kafka.
/// </summary>
public enum CompressionType
{
    None = 0,
    Gzip = 1,
    Snappy = 2,
    Lz4 = 3,
    Zstd = 4
}

/// <summary>
/// CRC32C implementation for Kafka record batch checksums.
/// Uses the Castagnoli polynomial (0x1EDC6F41).
/// </summary>
internal static class Crc32C
{
    private static readonly uint[] Table = GenerateTable();

    private static uint[] GenerateTable()
    {
        var table = new uint[256];
        const uint polynomial = 0x82F63B78; // Reversed Castagnoli polynomial

        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
            table[i] = crc;
        }

        return table;
    }

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFF;

        foreach (var b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }
}
