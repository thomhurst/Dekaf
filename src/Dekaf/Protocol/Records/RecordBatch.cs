using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using Dekaf.Compression;
using Dekaf.Internal;
using Dekaf.Serialization;

namespace Dekaf.Protocol.Records;

/// <summary>
/// Reusable buffer writer backed by ArrayPool. Two memory management mechanisms:
/// 1. Hard cap: buffers exceeding the configured max retained size are returned to the pool
///    and replaced with a fresh initial-size buffer on every Clear().
/// 2. Adaptive shrink: every ShrinkCheckInterval (64) clears, if peak usage was below
///    half the buffer size, the buffer is downsized to 2x peak (minimum _initialCapacity).
/// </summary>
internal sealed class PooledReusableBufferWriter : IBufferWriter<byte>, IDisposable
{
    /// <summary>
    /// Default maximum buffer size to retain across reuses (1MB).
    /// </summary>
    private const int DefaultMaxRetainedBufferSize = 1 * 1024 * 1024;

    /// <summary>
    /// Number of Clear() calls between shrink checks. Must be a power of two for fast modulo.
    /// </summary>
    private const int ShrinkCheckInterval = 64;

    private byte[] _buffer;
    private int _written;
    private readonly int _initialCapacity;
    private readonly int _maxRetainedBufferSize;
    private int _highWaterMark;
    private int _clearCount;

    public PooledReusableBufferWriter(int initialCapacity, int maxRetainedBufferSize = DefaultMaxRetainedBufferSize)
    {
        _initialCapacity = initialCapacity;
        _maxRetainedBufferSize = maxRetainedBufferSize;
        _buffer = DekafPools.SerializationBuffers.Rent(initialCapacity);
        _written = 0;
    }

    public int WrittenCount => _written;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _written);

    public int Capacity => _buffer.Length;

    /// <summary>
    /// Resets the write position without zeroing the buffer.
    /// If the buffer has grown beyond the configured max retained size, it is returned
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

        if (_buffer.Length > _maxRetainedBufferSize)
        {
            DekafPools.SerializationBuffers.Return(_buffer, clearArray: false);
            _buffer = DekafPools.SerializationBuffers.Rent(_initialCapacity);
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
                DekafPools.SerializationBuffers.Return(_buffer, clearArray: false);
                _buffer = DekafPools.SerializationBuffers.Rent(targetSize);
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
        var newBuffer = DekafPools.SerializationBuffers.Rent(newSize);
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);

        // Return old buffer without clearing - avoids zero-fill overhead.
        // No security requirement: buffer holds serialized Kafka protocol bytes, not credentials.
        DekafPools.SerializationBuffers.Return(_buffer, clearArray: false);
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
        DekafPools.SerializationBuffers.Return(_buffer, clearArray: false);
        _buffer = DekafPools.SerializationBuffers.Rent(minimumCapacity);
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
            DekafPools.SerializationBuffers.Return(buf, clearArray: false);
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
    /// <summary>
    /// Size of the batch header fields after batchLength: partitionLeaderEpoch(4) + magic(1) +
    /// crc(4) + attributes(2) + lastOffsetDelta(4) + baseTimestamp(8) + maxTimestamp(8) +
    /// producerId(8) + producerEpoch(2) + baseSequence(4) + recordCount(4) = 49 bytes.
    /// </summary>
    private static int s_maxRetainedBufferSize = 1 * 1024 * 1024; // default 1MB

    /// <summary>
    /// Ratchets up the max retained buffer size for thread-local buffers.
    /// Called by producer initialization when BatchSize exceeds the current value.
    /// Thread-local buffers created before this call retain their original (smaller) limit,
    /// which is acceptable since the ratchet only increases — existing buffers are more
    /// conservative, not less. New buffers on any thread pick up the updated value.
    /// Note: this is process-global — if multiple producers coexist with different batch sizes,
    /// the largest wins. Over-retention wastes some memory but under-retention would cause
    /// frequent ArrayPool churn on the large-batch producer.
    /// </summary>
    internal static void RatchetMaxRetainedBufferSize(int newSize) =>
        InterlockedHelper.RatchetUp(ref s_maxRetainedBufferSize, newSize);

    internal const int BatchHeaderSize = 4 + 1 + 4 + 2 + 4 + 8 + 8 + 8 + 2 + 4 + 4;

    /// <summary>
    /// Total header size including baseOffset(8) + batchLength(4) + BatchHeaderSize(49) = 61 bytes.
    /// </summary>
    internal const int TotalBatchHeaderSize = 8 + 4 + BatchHeaderSize;

    // Bounded pool of scratch buffer caches for RecordBatch serialization/deserialization.
    // Previously [ThreadStatic], but ConfigureAwait(false) in BrokerSender send loops causes
    // thread migration — each unique thread that handled serialization retained a permanent
    // ~1MB buffer rented from DekafPools.SerializationBuffers. With 3+ brokers, dozens of
    // threads accumulated caches over time, depleting the pool and driving Gen2 GC pressure.
    // A bounded ConcurrentStack pool ensures at most MaxPooledCaches buffers are retained,
    // regardless of how many threads participate in serialization.
    private static readonly ConcurrentStack<SerializationCache> s_cachePool = new();
    private const int MaxPooledCaches = 16;

    /// <summary>
    /// Holds scratch buffer state for a single RecordBatch serialization/deserialization operation.
    /// Rented from <see cref="s_cachePool"/> at operation start, returned at operation end.
    /// Uses PooledReusableBufferWriter (ArrayPool-backed) instead of ArrayBufferWriter
    /// to avoid Buffer.ZeroMemoryInternal overhead from new byte[] allocations on growth.
    /// </summary>
    private sealed class SerializationCache : IDisposable
    {
        public PooledReusableBufferWriter? RecordsBuffer;
        public PooledReusableBufferWriter? CompressedBuffer;
        public PooledReusableBufferWriter? DecompressedBuffer;

        /// <summary>
        /// Disposes all buffers, returning their arrays to DekafPools.SerializationBuffers.
        /// Called when the cache is evicted from the pool (pool full) to prevent buffer leaks.
        /// </summary>
        public void Dispose()
        {
            RecordsBuffer?.Dispose();
            RecordsBuffer = null;
            CompressedBuffer?.Dispose();
            CompressedBuffer = null;
            DecompressedBuffer?.Dispose();
            DecompressedBuffer = null;
        }
    }

    private static SerializationCache RentSerializationCache()
    {
        if (s_cachePool.TryPop(out var cache))
            return cache;

        return new SerializationCache();
    }

    private static void ReturnSerializationCache(SerializationCache cache)
    {
        if (s_cachePool.Count < MaxPooledCaches)
        {
            s_cachePool.Push(cache);
        }
        else
        {
            cache.Dispose();
        }
    }

    private static PooledReusableBufferWriter GetRecordsBuffer(SerializationCache cache)
    {
        var buffer = cache.RecordsBuffer;
        if (buffer is null)
        {
            buffer = new PooledReusableBufferWriter(4096, Volatile.Read(ref s_maxRetainedBufferSize));
            cache.RecordsBuffer = buffer;
        }
        else
        {
            buffer.Clear();
        }
        return buffer;
    }

    private static PooledReusableBufferWriter GetCompressedBuffer(SerializationCache cache)
    {
        var buffer = cache.CompressedBuffer;
        if (buffer is null)
        {
            buffer = new PooledReusableBufferWriter(4096, Volatile.Read(ref s_maxRetainedBufferSize));
            cache.CompressedBuffer = buffer;
        }
        else
        {
            buffer.Clear();
        }
        return buffer;
    }

    private static PooledReusableBufferWriter GetDecompressedBuffer(SerializationCache cache, int estimatedSize)
    {
        var buffer = cache.DecompressedBuffer;
        if (buffer is null)
        {
            buffer = new PooledReusableBufferWriter(Math.Max(4096, estimatedSize), Volatile.Read(ref s_maxRetainedBufferSize));
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
    private int _disposed;

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
            if (Volatile.Read(ref _disposed) != 0)
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

        var cache = RentSerializationCache();
        try
        {
            // Serialize records to pooled scratch buffer
            var recordsBuffer = GetRecordsBuffer(cache);
            var recordsWriter = new KafkaProtocolWriter(recordsBuffer);

            for (var i = 0; i < Records.Count; i++)
            {
                Records[i].Write(ref recordsWriter);
            }

            // Compress to pooled scratch buffer
            var registry = codecs ?? CompressionCodecRegistry.Default;
            var codec = registry.GetCodec(compression);
            var compressedBuffer = GetCompressedBuffer(cache);
            codec.Compress(new ReadOnlySequence<byte>(recordsBuffer.WrittenMemory), compressedBuffer);

            // Copy to a pooled array for storage (scratch buffer will be reused).
            // Uses ProducerDataPool (not ArrayPool<byte>.Shared) because PreCompress runs on
            // the producer/accumulator thread but ReturnPreCompressedBuffer runs on the
            // BrokerSender thread — cross-thread ArrayPool<byte>.Shared causes TLS accumulation.
            var compressedLength = compressedBuffer.WrittenCount;
            var pooledArray = Producer.ProducerDataPool.BytePool.Rent(compressedLength);
            compressedBuffer.WrittenSpan.CopyTo(pooledArray);

            PreCompressedRecords = pooledArray;
            PreCompressedLength = compressedLength;
            PreCompressedType = compression;
        }
        finally
        {
            ReturnSerializationCache(cache);
        }
    }

    /// <summary>
    /// Returns the pre-compressed pooled array to the dedicated producer pool.
    /// Must be called after the batch has been written to the network.
    /// </summary>
    internal void ReturnPreCompressedBuffer()
    {
        var array = PreCompressedRecords;
        if (array is not null)
        {
            PreCompressedRecords = null;
            PreCompressedLength = 0;
            Producer.ProducerDataPool.BytePool.Return(array, clearArray: false);
        }
    }

    /// <summary>
    /// Disposes any pooled memory associated with this batch's records
    /// and returns the batch instance to the pool for reuse.
    /// Call this after consuming all records from this batch.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

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
    // Uses an array-based LIFO stack instead of ConcurrentStack to avoid ~32-byte Node
    // allocation per Push — at 400 batches/sec this eliminates ~12.8 KB/sec of Gen0 pressure
    // that can seed GC feedback loops on low-core machines.
    private static readonly RecordBatch?[] s_pool = new RecordBatch?[MaxPoolSize];
    private static int s_top;
    private const int MaxPoolSize = 2048;

    /// <summary>
    /// Rents a RecordBatch from the pool or creates a new one.
    /// Caller must call <see cref="ReturnToPool"/> after the batch is fully processed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RecordBatch RentFromPool()
    {
        while (true)
        {
            var top = Volatile.Read(ref s_top);
            if (top <= 0)
                break;

            if (Interlocked.CompareExchange(ref s_top, top - 1, top) == top)
            {
                var batch = Interlocked.Exchange(ref s_pool[top - 1], null);
                if (batch is not null)
                {
                    batch._returnedToPoolFlag = 0;
                    Volatile.Write(ref batch._disposed, 0);
                    return batch;
                }
                break;
            }
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

        while (true)
        {
            var top = Volatile.Read(ref s_top);
            if (top >= MaxPoolSize)
                return; // Pool full — discard for GC

            if (Interlocked.CompareExchange(ref s_top, top + 1, top) == top)
            {
                Volatile.Write(ref s_pool[top], this);
                return;
            }
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
        // on the send loop thread). Otherwise, serialize and compress inline.
        ReadOnlySpan<byte> compressedRecords;
        CompressionType effectiveCompression;

        // Rent a scratch cache only when needed (no pre-compressed data available).
        // The cache is returned in the finally block to bound total retained buffers.
        SerializationCache? cache = null;
        try
        {
            if (PreCompressedRecords is not null)
            {
                // Pre-compressed at seal time — use the stored data directly
                compressedRecords = PreCompressedRecords.AsSpan(0, PreCompressedLength);
                effectiveCompression = PreCompressedType;
            }
            else
            {
                cache = RentSerializationCache();

                // Serialize records to pooled scratch buffer
                var recordsBuffer = GetRecordsBuffer(cache);
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
                    var compressedBuffer = GetCompressedBuffer(cache);
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
            var batchLength = BatchHeaderSize + compressedRecords.Length;

            writer.WriteInt64(BaseOffset);
            writer.WriteInt32(batchLength);
            writer.WriteInt32(PartitionLeaderEpoch);
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

            var crc = Crc32C.Compute(contentSpan[..crcContentSize]);

            // Backpatch CRC at the beginning
            BinaryPrimitives.WriteInt32BigEndian(crcAndContentSpan, (int)crc);

            // Advance the output by the full amount written.
            // Must happen before ReturnSerializationCache in the finally block,
            // because compressedRecords may reference the cache's buffer writer.
            output.Advance(4 + crcContentSize);
        }
        finally
        {
            if (cache is not null)
                ReturnSerializationCache(cache);
        }
    }

    /// <summary>
    /// Reads a RecordBatch from the protocol reader.
    /// Records are parsed lazily to avoid allocations for unconsumed records.
    /// </summary>
    /// <param name="reader">The protocol reader.</param>
    /// <param name="codecs">Optional compression codec registry.</param>
    /// <param name="availableBytes">
    /// Maximum bytes available for this batch in the partition records section.
    /// When a fetch response is truncated by max_bytes limits, the last batch's
    /// batchLength may exceed the actual data available. If so, the reader is
    /// advanced to the partition boundary and <see cref="InsufficientDataException"/>
    /// is thrown. Defaults to <see cref="int.MaxValue"/> (no boundary enforcement).
    /// </param>
    /// <remarks>
    /// If a ResponseParsingContext with pooled memory is active, the records will reference
    /// the pooled buffer directly (zero-copy). The first batch to be parsed will take ownership
    /// of the pooled memory, and subsequent batches will share it.
    /// Call Dispose() on the batch to release the pooled memory when done.
    /// </remarks>
    public static RecordBatch Read(ref KafkaProtocolReader reader, CompressionCodecRegistry? codecs = null, int availableBytes = int.MaxValue)
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

        var recordsLength = batchLength - BatchHeaderSize;

        // Batch truncated by fetch size limit — skip remaining data and signal to caller.
        var maxRecordsLength = Math.Max(0, availableBytes - TotalBatchHeaderSize);
        if (recordsLength > maxRecordsLength)
        {
            reader.Skip(maxRecordsLength);
            throw new InsufficientDataException();
        }

        // Check compression type from attributes
        var compression = (CompressionType)((int)attributes & 0x07);

        // Capture the raw record data
        var rawRecordData = reader.ReadMemorySlice(recordsLength);

        // Determine how to handle the record data based on compression and pooled memory availability
        LazyRecordList lazyRecords;

        if (compression != CompressionType.None)
        {
            // Compressed data must be decompressed to a new buffer.
            // Use pooled scratch buffer for decompression, then copy to a pooled array.
            // The pooled array will be managed by LazyRecordList for proper cleanup.
            var registry = codecs ?? CompressionCodecRegistry.Default;
            var codec = registry.GetCodec(compression);
            var estimatedSize = recordsLength * 4; // Estimate 4x expansion
            var cache = RentSerializationCache();
            try
            {
                var decompressedBuffer = GetDecompressedBuffer(cache, estimatedSize);
                codec.Decompress(new ReadOnlySequence<byte>(rawRecordData), decompressedBuffer);

                // Rent a pooled array and copy the decompressed data
                var writtenLength = decompressedBuffer.WrittenCount;
                var pooledArray = ArrayPool<byte>.Shared.Rent(writtenLength);
                decompressedBuffer.WrittenSpan.CopyTo(pooledArray);
                var pooledData = new PooledRecordData(pooledArray, writtenLength);
                lazyRecords = LazyRecordList.Create(pooledData, recordCount);
            }
            finally
            {
                ReturnSerializationCache(cache);
            }
        }
        else if (ResponseParsingContext.HasPooledMemory)
        {
            // Zero-copy: use the raw data directly from the pooled buffer
            // Mark that at least one batch used the pooled memory, so ownership
            // will be transferred to PendingFetchData after parsing completes
            ResponseParsingContext.MarkMemoryUsed();
            lazyRecords = LazyRecordList.Create(rawRecordData, recordCount);
        }
        else
        {
            // No pooled memory available (legacy path or not from network)
            // Use pooled array to avoid GC allocation from ToArray()
            var length = rawRecordData.Length;
            var pooledArray = ArrayPool<byte>.Shared.Rent(length);
            rawRecordData.Span.CopyTo(pooledArray);
            var pooledData = new PooledRecordData(pooledArray, length);
            lazyRecords = LazyRecordList.Create(pooledData, recordCount);
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
    // Pool for reusing LazyRecordList instances to eliminate per-batch class allocation.
    // Soft limit via Volatile.Read avoids ConcurrentStack.Count overhead.
    private static readonly ConcurrentStack<LazyRecordList> s_instancePool = new();
    private static int s_instancePoolCount;
    private const int MaxPooledInstances = 256;

    private ReadOnlyMemory<byte> _rawData;
    private byte[]? _pooledArray; // Track pooled array for cleanup (mutable for idempotent dispose)
    private int _count;
    private Record[]? _parsedRecords; // Rented from ArrayPool<Record>.Shared
    private int _parsedCount;         // Number of records parsed into _parsedRecords
    private int _nextParseOffset;
    private int _disposed;

    private LazyRecordList() { }

    /// <summary>
    /// Rents a LazyRecordList from the pool or creates a new one.
    /// </summary>
    internal static LazyRecordList Create(ReadOnlyMemory<byte> rawData, int count)
    {
        var instance = Rent();
        instance._rawData = rawData;
        instance._pooledArray = null;
        instance._count = count;
        return instance;
    }

    /// <summary>
    /// Rents a LazyRecordList from the pool or creates a new one, with pooled byte array ownership.
    /// </summary>
    internal static LazyRecordList Create(PooledRecordData pooledData, int count)
    {
        var instance = Rent();
        instance._rawData = pooledData.Memory;
        instance._pooledArray = pooledData.PooledArray;
        instance._count = count;
        return instance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LazyRecordList Rent()
    {
        if (s_instancePool.TryPop(out var instance))
        {
            Interlocked.Decrement(ref s_instancePoolCount);
            Volatile.Write(ref instance._disposed, 0);
            return instance;
        }
        return new LazyRecordList();
    }

    public int Count => _count;

    /// <summary>
    /// Eagerly parses all records in the batch at once.
    /// Call this before sequential access to avoid per-record lazy parse overhead
    /// (disposed check + bounds check + EnsureParsedUpTo per indexer access).
    /// After this call, indexer access is a simple array lookup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnsureAllParsed()
    {
        if (_count > 0 && (_parsedRecords is null || _parsedCount < _count))
        {
            EnsureParsedUpTo(_count - 1);
        }
    }

    public Record this[int index]
    {
        get
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(LazyRecordList));
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            EnsureParsedUpTo(index);

            // Re-check after parsing — EnsureParsedUpTo may have reduced _count
            // due to truncated data, making this index out of range.
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);

            return _parsedRecords![index];
        }
    }

    public IEnumerator<Record> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(LazyRecordList));

            EnsureParsedUpTo(i);

            // Re-check _count — EnsureParsedUpTo may have reduced it due to truncation.
            if (i >= _count)
                yield break;

            yield return _parsedRecords![i];
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
            // Rent from ArrayPool instead of allocating a List<Record>.
            // Eliminates per-batch List object allocation (~32 bytes) and uses pooled arrays
            // that are returned to the shared pool on dispose.
            var capacityToAllocate = _count > MaxReasonableRecordCount ? 16 : _count;
            _parsedRecords = ArrayPool<Record>.Shared.Rent(capacityToAllocate);
            _parsedCount = 0;
        }

        if (_parsedCount > index || _parsedCount >= _count)
            return;

        // Create reader once for all records to parse in this call.
        // Reusing the reader across iterations avoids redundant Slice() + ReadOnlyMemory construction per record.
        var reader = new KafkaProtocolReader(_rawData.Slice(_nextParseOffset));
        var readerStartOffset = _nextParseOffset;

        while (_parsedCount <= index && _parsedCount < _count)
        {
            // Grow the array if needed (rare: only when _count was capped by MaxReasonableRecordCount)
            if (_parsedCount >= _parsedRecords.Length)
            {
                var newArray = ArrayPool<Record>.Shared.Rent(_parsedRecords.Length * 2);
                _parsedRecords.AsSpan(0, _parsedCount).CopyTo(newArray);
                ArrayPool<Record>.Shared.Return(_parsedRecords, clearArray: true);
                _parsedRecords = newArray;
            }

            try
            {
                var record = Record.Read(ref reader);
                _parsedRecords[_parsedCount++] = record;
                _nextParseOffset = readerStartOffset + (int)reader.Consumed;
            }
            catch (Exception ex) when (ex is InsufficientDataException or MalformedProtocolDataException)
            {
                // Truncated fetch response or malformed varint — no more complete records
                // can be parsed. Cap the count to what we've successfully parsed so far to
                // prevent further attempts. This mirrors the partial batch handling in
                // FetchResponse. MalformedProtocolDataException covers malformed variable-length
                // integers that throw "Malformed variable-length integer".
                Trace.WriteLine($"Dekaf: Record parsing error ({ex.GetType().Name}) — {_parsedCount} of {_count} records parsed successfully.");
                _count = _parsedCount;
                break;
            }
        }
    }

    /// <summary>
    /// Marks the list as disposed, returns the pooled Record[] array, and returns
    /// this instance to the instance pool for reuse.
    /// After disposal, accessing records will throw ObjectDisposedException.
    /// Note: Memory ownership is managed at PendingFetchData level for non-compressed data,
    /// but pooled decompression buffers are returned here.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // Return pooled byte array if we own one, and null out to prevent double-return.
        // Double-return would allow ArrayPool to hand the same array to two different
        // renters simultaneously, causing data corruption.
        var pooledArray = _pooledArray;
        _pooledArray = null;
        if (pooledArray is not null)
        {
            ArrayPool<byte>.Shared.Return(pooledArray, clearArray: false);
        }

        // Return pooled Header[] arrays from parsed records before returning Record[] to pool.
        // Records with headers rent Header[] from ArrayPool<Header> in Record.Read().
        var parsedRecords = _parsedRecords;
        _parsedRecords = null;
        if (parsedRecords is not null)
        {
            for (var i = 0; i < _parsedCount; i++)
            {
                var headers = parsedRecords[i].Headers;
                if (headers is not null)
                {
                    // clearArray: true is intentional — Header.Value is ReadOnlyMemory<byte>
                    // referencing the network buffer. Clearing releases those references back
                    // to the GC, preventing the pool from holding onto network buffer memory.
                    ArrayPool<Header>.Shared.Return(headers, clearArray: true);
                }
            }

            ArrayPool<Record>.Shared.Return(parsedRecords, clearArray: true);
        }

        // Reset state for reuse
        _rawData = default;
        _count = 0;
        _parsedCount = 0;
        _nextParseOffset = 0;

        // Soft limit: the check-then-act is intentionally non-atomic.
        // Under high concurrency, the pool may briefly exceed MaxPooledInstances by a few items.
        // This is acceptable — avoiding a CAS loop keeps the return path lock-free.
        if (Volatile.Read(ref s_instancePoolCount) < MaxPooledInstances)
        {
            s_instancePool.Push(this);
            Interlocked.Increment(ref s_instancePoolCount);
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
