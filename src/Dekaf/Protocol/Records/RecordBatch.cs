using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using Dekaf.Compression;

namespace Dekaf.Protocol.Records;

/// <summary>
/// Reusable buffer writer backed by ArrayPool. Two memory management mechanisms:
/// 1. Hard cap: buffers exceeding MaxRetainedBufferSize (1MB) are returned to the pool
///    and replaced with a fresh initial-size buffer on every Clear().
/// 2. Adaptive shrink: every ShrinkCheckInterval (64) clears, if peak usage was below
///    half the buffer size, the buffer is downsized to 2x peak (minimum _initialCapacity).
/// </summary>
internal sealed class PooledReusableBufferWriter : IBufferWriter<byte>, IDisposable
{
    /// <summary>
    /// Maximum buffer size to retain across reuses (1MB). Buffers that grow beyond this
    /// threshold are replaced with a fresh initial-size buffer on Clear() to prevent
    /// thread-local memory from growing unbounded in long-running producers.
    /// </summary>
    private const int MaxRetainedBufferSize = 1 * 1024 * 1024;

    /// <summary>
    /// Number of Clear() calls between shrink checks. Must be a power of two for fast modulo.
    /// </summary>
    private const int ShrinkCheckInterval = 64;

    private byte[] _buffer;
    private int _written;
    private readonly int _initialCapacity;
    private int _highWaterMark;
    private int _clearCount;

    public PooledReusableBufferWriter(int initialCapacity)
    {
        _initialCapacity = initialCapacity;
        _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        _written = 0;
    }

    public int WrittenCount => _written;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _written);

    public int Capacity => _buffer.Length;

    /// <summary>
    /// Resets the write position without zeroing the buffer.
    /// If the buffer has grown beyond <see cref="MaxRetainedBufferSize"/>, it is returned
    /// to the pool and replaced with a fresh initial-size buffer to prevent unbounded growth.
    /// Additionally, every <see cref="ShrinkCheckInterval"/> clears, if the high-water mark
    /// is less than half the buffer size, the buffer is shrunk to 2x the high-water mark
    /// to reclaim memory from transient spikes.
    /// </summary>
    public void Clear()
    {
        // Track peak usage since last shrink check
        if (_written > _highWaterMark)
            _highWaterMark = _written;

        _written = 0;

        if (_buffer.Length > MaxRetainedBufferSize)
        {
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: false);
            _buffer = ArrayPool<byte>.Shared.Rent(_initialCapacity);
            _highWaterMark = 0;
            _clearCount = 0;
            return;
        }

        // Generation-based shrink: every 64 clears, check if buffer is oversized for actual usage
        if ((++_clearCount & (ShrinkCheckInterval - 1)) == 0)
        {
            var bufferLength = _buffer.Length;
            if (_highWaterMark < bufferLength / 2 && bufferLength > _initialCapacity)
            {
                // Target size floors at _initialCapacity when _highWaterMark is 0
            // (buffer had no writes in the last ShrinkCheckInterval clears).
            var targetSize = Math.Max(_initialCapacity, _highWaterMark * 2);
                ArrayPool<byte>.Shared.Return(_buffer, clearArray: false);
                _buffer = ArrayPool<byte>.Shared.Rent(targetSize);
            }

            // Always reset to start a fresh tracking window, regardless of whether shrink occurred.
            _highWaterMark = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        _written += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 1)
            sizeHint = 1;

        if (_buffer.Length - _written < sizeHint)
        {
            Grow(sizeHint);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int sizeHint)
    {
        var required = checked(_written + sizeHint);
        var newSize = Math.Max(_buffer.Length * 2, required);

        // Rent from pool - not zero-initialized, avoiding Buffer.ZeroMemoryInternal overhead.
        var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);

        // Return old buffer without clearing - avoids zero-fill overhead.
        // No security requirement: buffer holds serialized Kafka protocol bytes, not credentials.
        ArrayPool<byte>.Shared.Return(_buffer, clearArray: false);
        _buffer = newBuffer;
    }

    /// <summary>
    /// Resets the write position and ensures the buffer has at least the specified capacity.
    /// Discards any existing written data. Used by GetDecompressedBuffer when the existing
    /// buffer is too small for the next decompression operation.
    /// </summary>
    public void ResetAndEnsureCapacity(int minimumCapacity)
    {
        _written = 0;

        if (_buffer.Length >= minimumCapacity)
            return;

        // Return the old buffer and rent a larger one
        ArrayPool<byte>.Shared.Return(_buffer, clearArray: false);
        _buffer = ArrayPool<byte>.Shared.Rent(minimumCapacity);
    }

    /// <summary>
    /// Returns the pooled buffer. After disposal, the writer must not be used.
    /// </summary>
    public void Dispose()
    {
        var buf = _buffer;
        _buffer = [];
        _written = 0;
        if (buf.Length > 0)
            ArrayPool<byte>.Shared.Return(buf, clearArray: false);
    }
}

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
    // Single thread-local cache consolidating all per-thread buffer state.
    // Reduces 3 separate [ThreadStatic] lookups to 1.
    [ThreadStatic]
    private static RecordBatchThreadCache? t_cache;

    /// <summary>
    /// Holds all per-thread cached buffer state for RecordBatch serialization/deserialization.
    /// Consolidating into a single class reduces thread-static lookup overhead from 3 to 1.
    /// Uses PooledReusableBufferWriter (ArrayPool-backed) instead of ArrayBufferWriter
    /// to avoid Buffer.ZeroMemoryInternal overhead from new byte[] allocations on growth.
    /// </summary>
    private sealed class RecordBatchThreadCache
    {
        public PooledReusableBufferWriter? RecordsBuffer;
        public PooledReusableBufferWriter? CompressedBuffer;
        public PooledReusableBufferWriter? DecompressedBuffer;
    }

    private static PooledReusableBufferWriter GetRecordsBuffer()
    {
        var cache = t_cache ??= new RecordBatchThreadCache();
        var buffer = cache.RecordsBuffer;
        if (buffer is null)
        {
            buffer = new PooledReusableBufferWriter(4096);
            cache.RecordsBuffer = buffer;
        }
        else
        {
            buffer.Clear();
        }
        return buffer;
    }

    private static PooledReusableBufferWriter GetCompressedBuffer()
    {
        var cache = t_cache ??= new RecordBatchThreadCache();
        var buffer = cache.CompressedBuffer;
        if (buffer is null)
        {
            buffer = new PooledReusableBufferWriter(4096);
            cache.CompressedBuffer = buffer;
        }
        else
        {
            buffer.Clear();
        }
        return buffer;
    }

    private static PooledReusableBufferWriter GetDecompressedBuffer(int estimatedSize)
    {
        var cache = t_cache ??= new RecordBatchThreadCache();
        var buffer = cache.DecompressedBuffer;
        if (buffer is null)
        {
            buffer = new PooledReusableBufferWriter(Math.Max(4096, estimatedSize));
            cache.DecompressedBuffer = buffer;
        }
        else
        {
            // Reset write position and ensure capacity for decompressed data
            buffer.ResetAndEnsureCapacity(estimatedSize);
        }
        return buffer;
    }

    /// <summary>
    /// Guards against double-return to pool (e.g. multiple Dispose() calls).
    /// Uses int + Interlocked.Exchange for thread-safe atomic check-and-set.
    /// Reset to 0 when rented from pool.
    /// </summary>
    private int _returnedToPoolFlag;

    /// <summary>
    /// Tracks whether this batch has been disposed. Used to throw
    /// <see cref="ObjectDisposedException"/> on post-dispose access.
    /// </summary>
    private volatile bool _disposed;

    public long BaseOffset { get; internal set; }
    public int BatchLength { get; internal set; }
    public int PartitionLeaderEpoch { get; internal set; } = -1;
    public byte Magic { get; internal set; } = 2;
    public uint Crc { get; internal set; }
    public RecordBatchAttributes Attributes { get; internal set; }
    public int LastOffsetDelta { get; internal set; }
    public long BaseTimestamp { get; internal set; }
    public long MaxTimestamp { get; internal set; }
    public long ProducerId { get; set; } = -1;
    public short ProducerEpoch { get; set; } = -1;
    public int BaseSequence { get; set; } = -1;

    /// <summary>
    /// The records in this batch. For batches created via Read(), records are parsed lazily
    /// on first enumeration to avoid allocations for unconsumed records.
    /// Throws <see cref="ObjectDisposedException"/> if the batch has been disposed.
    /// </summary>
    public IReadOnlyList<Record> Records
    {
        get
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RecordBatch));
            return _records;
        }
        internal set => _records = value;
    }

    private IReadOnlyList<Record> _records = null!;

    /// <summary>
    /// Pre-compressed records data. When set, <see cref="Write"/> skips compression
    /// and uses this data directly. The array is rented from <see cref="ArrayPool{T}"/>
    /// and must be returned by the caller after Write() completes.
    /// Set by <see cref="PreCompress"/> at batch seal time so compression happens on
    /// partition-affine append workers instead of the single-threaded send loop.
    /// </summary>
    internal byte[]? PreCompressedRecords { get; private set; }

    /// <summary>
    /// The valid length within <see cref="PreCompressedRecords"/>.
    /// </summary>
    internal int PreCompressedLength { get; private set; }

    /// <summary>
    /// The compression type applied to <see cref="PreCompressedRecords"/>.
    /// </summary>
    internal CompressionType PreCompressedType { get; private set; }

    /// <summary>
    /// Pre-compresses the records in this batch. Called at seal time on partition-affine
    /// append workers to move compression work off the single-threaded send loop.
    /// The compressed data is stored in a pooled array and used by <see cref="Write"/>
    /// to skip re-compression. This is a per-batch allocation (acceptable).
    /// </summary>
    /// <param name="compression">The compression type to apply.</param>
    /// <param name="codecs">The codec registry to use.</param>
    internal void PreCompress(CompressionType compression, CompressionCodecRegistry? codecs)
    {
        if (compression == CompressionType.None)
            return;

        // Serialize records to thread-local buffer
        var recordsBuffer = GetRecordsBuffer();
        var recordsWriter = new KafkaProtocolWriter(recordsBuffer);

        for (var i = 0; i < Records.Count; i++)
        {
            Records[i].Write(ref recordsWriter);
        }

        // Compress to thread-local buffer
        var registry = codecs ?? CompressionCodecRegistry.Default;
        var codec = registry.GetCodec(compression);
        var compressedBuffer = GetCompressedBuffer();
        codec.Compress(new ReadOnlySequence<byte>(recordsBuffer.WrittenMemory), compressedBuffer);

        // Copy to a pooled array for storage (thread-local buffer will be reused)
        var compressedLength = compressedBuffer.WrittenCount;
        var pooledArray = ArrayPool<byte>.Shared.Rent(compressedLength);
        compressedBuffer.WrittenSpan.CopyTo(pooledArray);

        PreCompressedRecords = pooledArray;
        PreCompressedLength = compressedLength;
        PreCompressedType = compression;
    }

    /// <summary>
    /// Returns the pre-compressed pooled array to ArrayPool.
    /// Must be called after the batch has been written to the network.
    /// </summary>
    internal void ReturnPreCompressedBuffer()
    {
        var array = PreCompressedRecords;
        if (array is not null)
        {
            PreCompressedRecords = null;
            PreCompressedLength = 0;
            ArrayPool<byte>.Shared.Return(array, clearArray: false);
        }
    }

    /// <summary>
    /// Disposes any pooled memory associated with this batch's records
    /// and returns the batch instance to the pool for reuse.
    /// Call this after consuming all records from this batch.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;

        ReturnPreCompressedBuffer();

        if (_records is IDisposable disposable)
        {
            disposable.Dispose();
        }

        ReturnToPool();
    }

    // ── Pool for producer-path RecordBatch reuse ──
    // Eliminates per-batch class allocation (~120 bytes) that survives Gen0 due to
    // its lifetime spanning the send pipeline (1-10ms), contributing to high Gen1/Gen0 ratio.
    private static readonly ConcurrentStack<RecordBatch> s_pool = new();
    private static int s_poolCount;
    private const int MaxPoolSize = 2048;

    /// <summary>
    /// Rents a RecordBatch from the pool or creates a new one.
    /// Caller must call <see cref="ReturnToPool"/> after the batch is fully processed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RecordBatch RentFromPool()
    {
        if (s_pool.TryPop(out var batch))
        {
            Interlocked.Decrement(ref s_poolCount);
            batch._returnedToPoolFlag = 0;
            batch._disposed = false;
            return batch;
        }
        return new RecordBatch();
    }

    /// <summary>
    /// Returns this RecordBatch to the pool for reuse. Clears all references to avoid
    /// holding onto Records data. Caller must call <see cref="ReturnPreCompressedBuffer"/>
    /// before this method to return any pooled compression buffers.
    /// </summary>
    internal void ReturnToPool()
    {
        // Guard against double-return (e.g. multiple Dispose() calls).
        // Interlocked.Exchange ensures exactly one thread passes this gate.
        if (Interlocked.Exchange(ref _returnedToPoolFlag, 1) != 0)
            return;

        // Clear references to avoid holding onto GC-tracked objects
        _records = null!;
        PreCompressedRecords = null;
        PreCompressedLength = 0;
        PreCompressedType = CompressionType.None;

        // Reset mutable state to defaults
        BaseOffset = 0;
        BatchLength = 0;
        PartitionLeaderEpoch = -1;
        Magic = 2;
        Crc = 0;
        Attributes = RecordBatchAttributes.None;
        LastOffsetDelta = 0;
        BaseTimestamp = 0;
        MaxTimestamp = 0;
        ProducerId = -1;
        ProducerEpoch = -1;
        BaseSequence = -1;

        // Soft limit: the check-then-act is intentionally non-atomic.
        // Under high concurrency, the pool may briefly exceed MaxPoolSize by a few items.
        // This is acceptable — avoiding a CAS loop keeps the return path lock-free.
        if (Volatile.Read(ref s_poolCount) < MaxPoolSize)
        {
            s_pool.Push(this);
            Interlocked.Increment(ref s_poolCount);
        }
    }

    /// <summary>
    /// Creates a new RecordBatch with updated producer state (PID, epoch, base sequence).
    /// All other fields are copied from this instance. Records reference is shared (immutable).
    /// CRC is recomputed automatically during Write().
    /// Used during epoch bump recovery to rewrite stale batches with new sequence numbers.
    /// </summary>
    /// <remarks>
    /// The caller must call <see cref="ReturnToPool"/> on the old batch (this instance)
    /// after calling this method, since ownership of pooled resources (e.g. PreCompressedRecords)
    /// is transferred to the new batch.
    /// </remarks>
    internal RecordBatch WithProducerState(long producerId, short producerEpoch, int baseSequence)
    {
        var batch = RentFromPool();
        batch.BaseOffset = BaseOffset;
        batch.BatchLength = BatchLength;
        batch.PartitionLeaderEpoch = PartitionLeaderEpoch;
        batch.Magic = Magic;
        batch.Crc = 0; // Will be recomputed during Write()
        batch.Attributes = Attributes;
        batch.LastOffsetDelta = LastOffsetDelta;
        batch.BaseTimestamp = BaseTimestamp;
        batch.MaxTimestamp = MaxTimestamp;
        batch.ProducerId = producerId;
        batch.ProducerEpoch = producerEpoch;
        batch.BaseSequence = baseSequence;
        batch.Records = Records;

        // Transfer pre-compressed data — records content is unchanged,
        // only header fields differ (CRC is recomputed in Write()).
        if (PreCompressedRecords is not null)
        {
            batch.PreCompressedRecords = PreCompressedRecords;
            batch.PreCompressedLength = PreCompressedLength;
            batch.PreCompressedType = PreCompressedType;

            // Clear from old batch to prevent double-return to ArrayPool
            PreCompressedRecords = null;
            PreCompressedLength = 0;
            PreCompressedType = CompressionType.None;
        }

        return batch;
    }

    /// <summary>
    /// Writes the record batch to the output buffer.
    /// </summary>
    public void Write(IBufferWriter<byte> output, CompressionType compression = CompressionType.None, CompressionCodecRegistry? codecs = null)
    {
        var writer = new KafkaProtocolWriter(output);

        // Determine the effective compression and records data to write.
        // If pre-compressed at seal time, use that data directly (skip CPU-bound compression
        // on the send loop thread). Otherwise, serialize and compress inline (legacy path).
        ReadOnlySpan<byte> compressedRecords;
        CompressionType effectiveCompression;

        if (PreCompressedRecords is not null)
        {
            // Pre-compressed at seal time — use the stored data directly
            compressedRecords = PreCompressedRecords.AsSpan(0, PreCompressedLength);
            effectiveCompression = PreCompressedType;
        }
        else
        {
            // Serialize records to thread-local buffer
            var recordsBuffer = GetRecordsBuffer();
            var recordsWriter = new KafkaProtocolWriter(recordsBuffer);

            for (var i = 0; i < Records.Count; i++)
            {
                Records[i].Write(ref recordsWriter);
            }

            // Apply compression if needed
            if (compression != CompressionType.None)
            {
                var registry = codecs ?? CompressionCodecRegistry.Default;
                var codec = registry.GetCodec(compression);
                var compressedBuffer = GetCompressedBuffer();
                codec.Compress(new ReadOnlySequence<byte>(recordsBuffer.WrittenMemory), compressedBuffer);
                compressedRecords = compressedBuffer.WrittenSpan;
                effectiveCompression = compression;
            }
            else
            {
                compressedRecords = recordsBuffer.WrittenSpan;
                effectiveCompression = CompressionType.None;
            }
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
        if (effectiveCompression != CompressionType.None)
        {
            attributes = (short)((attributes & ~0x07) | (int)effectiveCompression);
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
        LazyRecordList lazyRecords;

        if (compression != CompressionType.None)
        {
            // Compressed data must be decompressed to a new buffer.
            // Use thread-local buffer for decompression, then copy to a pooled array.
            // The pooled array will be managed by LazyRecordList for proper cleanup.
            var registry = codecs ?? CompressionCodecRegistry.Default;
            var codec = registry.GetCodec(compression);
            var estimatedSize = recordsLength * 4; // Estimate 4x expansion
            var decompressedBuffer = GetDecompressedBuffer(estimatedSize);
            codec.Decompress(new ReadOnlySequence<byte>(rawRecordData), decompressedBuffer);

            // Rent a pooled array and copy the decompressed data
            var writtenLength = decompressedBuffer.WrittenCount;
            var pooledArray = ArrayPool<byte>.Shared.Rent(writtenLength);
            decompressedBuffer.WrittenSpan.CopyTo(pooledArray);
            var pooledData = new PooledRecordData(pooledArray, writtenLength);
            lazyRecords = new LazyRecordList(pooledData, recordCount);
        }
        else if (ResponseParsingContext.HasPooledMemory)
        {
            // Zero-copy: use the raw data directly from the pooled buffer
            // Mark that at least one batch used the pooled memory, so ownership
            // will be transferred to PendingFetchData after parsing completes
            ResponseParsingContext.MarkMemoryUsed();
            lazyRecords = new LazyRecordList(rawRecordData, recordCount);
        }
        else
        {
            // No pooled memory available (legacy path or not from network)
            // Use pooled array to avoid GC allocation from ToArray()
            var length = rawRecordData.Length;
            var pooledArray = ArrayPool<byte>.Shared.Rent(length);
            rawRecordData.Span.CopyTo(pooledArray);
            var pooledData = new PooledRecordData(pooledArray, length);
            lazyRecords = new LazyRecordList(pooledData, recordCount);
        }

        var batch = RentFromPool();
        batch.BaseOffset = baseOffset;
        batch.BatchLength = batchLength;
        batch.PartitionLeaderEpoch = partitionLeaderEpoch;
        batch.Magic = magic;
        batch.Crc = crc;
        batch.Attributes = attributes;
        batch.LastOffsetDelta = lastOffsetDelta;
        batch.BaseTimestamp = baseTimestamp;
        batch.MaxTimestamp = maxTimestamp;
        batch.ProducerId = producerId;
        batch.ProducerEpoch = producerEpoch;
        batch.BaseSequence = baseSequence;
        batch.Records = lazyRecords;
        return batch;
    }
}

/// <summary>
/// A lazy list that parses records on-demand from raw byte data.
/// Records are only parsed when accessed, avoiding allocations for unconsumed records.
/// Uses pooled lists to avoid per-batch allocations.
/// </summary>
/// <remarks>
/// When used with zero-copy parsing, the raw data references the pooled network buffer.
/// The memory ownership is managed at the PendingFetchData level, not here.
/// Dispose marks the list as disposed and returns the pooled list for reuse.
/// </remarks>
/// <summary>
/// Wraps a pooled byte array as ReadOnlyMemory with proper cleanup semantics.
/// When the owning LazyRecordList is disposed, the array is returned to the pool.
/// </summary>
internal readonly struct PooledRecordData
{
    private readonly byte[] _pooledArray;
    private readonly int _length;

    public PooledRecordData(byte[] pooledArray, int length)
    {
        _pooledArray = pooledArray;
        _length = length;
    }

    public ReadOnlyMemory<byte> Memory => _pooledArray.AsMemory(0, _length);

    public byte[]? PooledArray => _pooledArray;

    public static implicit operator ReadOnlyMemory<byte>(PooledRecordData data) => data.Memory;
}

internal sealed class LazyRecordList : IReadOnlyList<Record>, IDisposable
{
    // Pool for reusing List<Record> instances to reduce GC pressure.
    // Using ConcurrentBag for thread-safe pooling with good performance.
    // Note: MaxPooledLists is a soft limit because ConcurrentBag.Count is not atomic with Add().
    // Under high concurrency, the pool may temporarily exceed MaxPooledLists, but this is acceptable
    // as it only affects memory usage slightly and avoids the overhead of stricter synchronization.
    private static readonly System.Collections.Concurrent.ConcurrentBag<List<Record>> s_listPool = new();
    private const int MaxPooledLists = 256;

    private readonly ReadOnlyMemory<byte> _rawData;
    private byte[]? _pooledArray; // Track pooled array for cleanup (mutable for idempotent dispose)
    private readonly int _count;
    private List<Record>? _parsedRecords;
    private int _nextParseOffset;
    private bool _disposed;

    public LazyRecordList(ReadOnlyMemory<byte> rawData, int count)
    {
        _rawData = rawData;
        _pooledArray = null;
        _count = count;
    }

    public LazyRecordList(PooledRecordData pooledData, int count)
    {
        _rawData = pooledData.Memory;
        _pooledArray = pooledData.PooledArray;
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

    // Maximum reasonable record count per batch to prevent OOM from malformed data.
    // Kafka's max.message.bytes default is 1MB, with typical records being 1KB+,
    // so 1M records per batch is extremely conservative.
    private const int MaxReasonableRecordCount = 1_000_000;

    private void EnsureParsedUpTo(int index)
    {
        if (_parsedRecords is null)
        {
            // Try to get a pooled list, otherwise create new
            if (!s_listPool.TryTake(out _parsedRecords))
            {
                // Pre-allocate with known count to avoid growth allocations
                var capacityToAllocate = _count > MaxReasonableRecordCount ? 0 : _count;
                _parsedRecords = new List<Record>(capacityToAllocate);
            }
            else
            {
                // Ensure pooled list has enough capacity
                var capacityNeeded = _count > MaxReasonableRecordCount ? 0 : _count;
                if (_parsedRecords.Capacity < capacityNeeded)
                {
                    _parsedRecords.Capacity = capacityNeeded;
                }
            }
        }

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
    /// Marks the list as disposed and returns the pooled list for reuse.
    /// After disposal, accessing records will throw ObjectDisposedException.
    /// Note: Memory ownership is managed at PendingFetchData level for non-compressed data,
    /// but pooled decompression buffers are returned here.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Return pooled array if we own one, and null out to prevent double-return.
        // Double-return would allow ArrayPool to hand the same array to two different
        // renters simultaneously, causing data corruption.
        var pooledArray = _pooledArray;
        _pooledArray = null;
        if (pooledArray is not null)
        {
            ArrayPool<byte>.Shared.Return(pooledArray, clearArray: false);
        }

        // Return list to pool for reuse (soft limit - see MaxPooledLists comment)
        if (_parsedRecords is not null && s_listPool.Count < MaxPooledLists)
        {
            _parsedRecords.Clear();
            s_listPool.Add(_parsedRecords);
        }
        _parsedRecords = null;
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
    Zstd = 4,

    /// <summary>
    /// Brotli compression. This is a Dekaf-specific extension and is NOT part of the official
    /// Apache Kafka protocol specification. Value 5 is not assigned by the Kafka protocol;
    /// Kafka brokers will not understand this codec natively and will reject produce requests
    /// that use it unless a custom broker plugin is installed.
    /// Both producer and consumer must have the <c>Dekaf.Compression.Brotli</c> codec installed.
    /// Standard Kafka clients (Java, librdkafka, Confluent.Kafka) cannot produce or consume
    /// messages compressed with this type.
    /// </summary>
    Brotli = 5
}

/// <summary>
/// CRC32C implementation for Kafka record batch checksums.
/// Uses the Castagnoli polynomial (0x1EDC6F41) with hardware acceleration (SSE4.2).
/// </summary>
internal static class Crc32C
{
    private static readonly bool IsHardwareAccelerated = Sse42.IsSupported;

    // Software fallback table
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        return IsHardwareAccelerated ? ComputeHardware(data) : ComputeSoftware(data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ComputeHardware(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        var i = 0;

        // Process 8 bytes at a time using 64-bit CRC instruction
        // Use BinaryPrimitives for consistent endianness (CRC32C operates on little-endian data)
        if (Sse42.X64.IsSupported)
        {
            while (i + 8 <= data.Length)
            {
                var value = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(i, 8));
                crc = (uint)Sse42.X64.Crc32(crc, value);
                i += 8;
            }
        }

        // Process 4 bytes at a time
        while (i + 4 <= data.Length)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(i, 4));
            crc = Sse42.Crc32(crc, value);
            i += 4;
        }

        // Process remaining bytes
        while (i < data.Length)
        {
            crc = Sse42.Crc32(crc, data[i]);
            i++;
        }

        return crc ^ 0xFFFFFFFFu;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint ComputeSoftware(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
