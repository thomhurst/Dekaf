using System.Buffers;
using System.Buffers.Binary;
using Dekaf.Compression;

namespace Dekaf.Protocol.Records;

/// <summary>
/// Kafka RecordBatch v2 format (magic byte 2).
/// This is the modern record format used since Kafka 0.11.
/// Supports lazy record parsing - records are only parsed when enumerated.
/// </summary>
/// <remarks>
/// When created via Read() during FetchResponse parsing with pooled memory context,
/// the Records property returns a LazyRecordList that references the pooled network buffer.
/// Call DisposeRecords() to release the pooled memory when done consuming records.
/// </remarks>
public sealed class RecordBatch : IDisposable
{
    // Thread-local reusable buffers for serialization to avoid per-batch allocations
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_recordsBuffer;
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_compressedBuffer;

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

    private static ArrayBufferWriter<byte> GetCompressedBuffer()
    {
        var buffer = t_compressedBuffer;
        if (buffer is null)
        {
            buffer = new ArrayBufferWriter<byte>(4096);
            t_compressedBuffer = buffer;
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
    /// Disposes any pooled memory associated with this batch's records.
    /// Call this after consuming all records from this batch.
    /// </summary>
    public void Dispose()
    {
        if (Records is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

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
            // Use thread-local buffer to avoid per-batch allocation
            compressedBuffer = GetCompressedBuffer();
            // Use WrittenMemory instead of ToArray() to avoid heap allocation
            codec.Compress(new ReadOnlySequence<byte>(recordsBuffer.WrittenMemory), compressedBuffer);
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

        // CRC content size: 2 (attributes) + 4 (lastOffsetDelta) + 8 (baseTimestamp) +
        // 8 (maxTimestamp) + 8 (producerId) + 2 (producerEpoch) + 4 (baseSequence) +
        // 4 (recordsCount) + compressedRecords = 40 + records
        const int crcContentFixedSize = 40;
        var crcContentSize = crcContentFixedSize + compressedRecords.Length;

        // Get a span for CRC (4 bytes) + content, write content directly, calculate CRC, backpatch
        // This eliminates the need for crcBuffer intermediate copy
        var crcAndContentSpan = output.GetSpan(4 + crcContentSize);
        var contentSpan = crcAndContentSpan.Slice(4); // Content starts after CRC

        var attributes = (short)Attributes;
        if (compression != CompressionType.None)
        {
            attributes = (short)((attributes & ~0x07) | (int)compression);
        }

        // Write content directly using BinaryPrimitives
        var offset = 0;
        BinaryPrimitives.WriteInt16BigEndian(contentSpan[offset..], attributes);
        offset += 2;
        BinaryPrimitives.WriteInt32BigEndian(contentSpan[offset..], LastOffsetDelta);
        offset += 4;
        BinaryPrimitives.WriteInt64BigEndian(contentSpan[offset..], BaseTimestamp);
        offset += 8;
        BinaryPrimitives.WriteInt64BigEndian(contentSpan[offset..], MaxTimestamp);
        offset += 8;
        BinaryPrimitives.WriteInt64BigEndian(contentSpan[offset..], ProducerId);
        offset += 8;
        BinaryPrimitives.WriteInt16BigEndian(contentSpan[offset..], ProducerEpoch);
        offset += 2;
        BinaryPrimitives.WriteInt32BigEndian(contentSpan[offset..], BaseSequence);
        offset += 4;
        BinaryPrimitives.WriteInt32BigEndian(contentSpan[offset..], Records.Count);
        offset += 4;
        compressedRecords.CopyTo(contentSpan[offset..]);

        // Calculate CRC over the content we just wrote
        var crc = Crc32C.Compute(contentSpan[..crcContentSize]);

        // Backpatch CRC at the beginning
        BinaryPrimitives.WriteInt32BigEndian(crcAndContentSpan, (int)crc);

        // Advance the output by the full amount written
        output.Advance(4 + crcContentSize);
    }

    /// <summary>
    /// Reads a record batch from the input buffer.
    /// Records are parsed lazily to avoid allocations for unconsumed records.
    /// </summary>
    /// <remarks>
    /// If a ResponseParsingContext with pooled memory is active, the records will reference
    /// the pooled buffer directly (zero-copy). The first batch to be parsed will take ownership
    /// of the pooled memory, and subsequent batches will share it.
    /// Call Dispose() on the batch to release the pooled memory when done.
    /// </remarks>
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

        // Determine how to handle the record data based on compression and pooled memory availability
        ReadOnlyMemory<byte> recordData;

        if (compression != CompressionType.None)
        {
            // Compressed data must be decompressed to a new buffer.
            // We still need to copy because decompression creates new data.
            var registry = codecs ?? CompressionCodecRegistry.Default;
            var codec = registry.GetCodec(compression);
            var decompressedBuffer = new ArrayBufferWriter<byte>(recordsLength * 4); // Estimate 4x expansion
            codec.Decompress(new ReadOnlySequence<byte>(rawRecordData), decompressedBuffer);
            recordData = decompressedBuffer.WrittenMemory.ToArray();
        }
        else if (ResponseParsingContext.HasPooledMemory)
        {
            // Zero-copy: use the raw data directly from the pooled buffer
            // Mark that at least one batch used the pooled memory, so ownership
            // will be transferred to PendingFetchData after parsing completes
            ResponseParsingContext.MarkMemoryUsed();
            recordData = rawRecordData;
        }
        else
        {
            // No pooled memory available (legacy path or not from network)
            // Copy to owned memory to avoid use-after-free
            recordData = rawRecordData.ToArray();
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
/// <remarks>
/// When used with zero-copy parsing, the raw data references the pooled network buffer.
/// The memory ownership is managed at the PendingFetchData level, not here.
/// Dispose marks the list as disposed to prevent access after the buffer is released.
/// </remarks>
internal sealed class LazyRecordList : IReadOnlyList<Record>, IDisposable
{
    private readonly ReadOnlyMemory<byte> _rawData;
    private readonly int _count;
    private List<Record>? _parsedRecords;
    private int _nextParseOffset;
    private bool _disposed;

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
            if (_disposed)
                throw new ObjectDisposedException(nameof(LazyRecordList));
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
        // Pre-allocate list with known count to avoid repeated growth allocations.
        // We know exactly how many records are in the batch from the record count field.
        // This eliminates List capacity growth overhead during iteration.
        _parsedRecords ??= new List<Record>(_count);

        while (_parsedRecords.Count <= index && _parsedRecords.Count < _count)
        {
            var slice = _rawData.Slice(_nextParseOffset);
            var reader = new KafkaProtocolReader(slice);
            var record = Record.Read(ref reader);
            _parsedRecords.Add(record);
            _nextParseOffset += (int)reader.Consumed;
        }
    }

    /// <summary>
    /// Marks the list as disposed. After disposal, accessing records will throw ObjectDisposedException.
    /// Note: Memory ownership is managed at PendingFetchData level, not here.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
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
