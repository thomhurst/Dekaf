using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if !NETSTANDARD2_0
using System.Runtime.Intrinsics.X86;
using ArmCrc32 = System.Runtime.Intrinsics.Arm.Crc32;
#endif
using Dekaf.Compression;
using Dekaf.Consumer;
using Dekaf.Internal;
using Dekaf.Producer;
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
        var doubled = Math.Min((long)_buffer.Length * 2, Array.MaxLength);
        var required = Math.Min((long)_written + sizeHint, Array.MaxLength);
        var newSize = (int)Math.Max(doubled, required);

        if (newSize <= _buffer.Length)
            throw new InvalidOperationException("Cannot grow buffer: maximum size reached.");

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
    /// Discards any existing written data. Used when the existing buffer is too small
    /// for the next operation.
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
/// Lightweight <see cref="IBufferWriter{T}"/> backed by a supplied array pool.
/// <see cref="DetachBuffer"/> transfers ownership of the underlying pooled array to
/// the caller, eliminating a second memcpy from reusable scratch storage.
/// </summary>
internal sealed class DetachableBufferWriter : IBufferWriter<byte>, IDisposable
{
    private const int MinimumInitialCapacity = 256;

    private readonly ArrayPool<byte> _pool;
    private byte[] _buffer;
    private int _written;

    public DetachableBufferWriter(ArrayPool<byte> pool, int initialCapacity)
    {
        _pool = pool;
        _buffer = pool.Rent(Math.Max(MinimumInitialCapacity, initialCapacity));
    }

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        if ((uint)count > (uint)(_buffer.Length - _written))
            throw new InvalidOperationException("Advance called with count exceeding available space.");
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

    public byte[] DetachBuffer(out int length)
    {
        length = _written;
        var buf = _buffer;
        _buffer = [];
        _written = 0;
        return buf;
    }

    public void Dispose()
    {
        var buf = _buffer;
        _buffer = [];
        _written = 0;
        if (buf.Length > 0)
            _pool.Return(buf, clearArray: false);
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
        var doubled = Math.Min((long)_buffer.Length * 2, Array.MaxLength);
        var required = Math.Min((long)_written + sizeHint, Array.MaxLength);
        var newSize = (int)Math.Max(doubled, required);

        if (newSize <= _buffer.Length)
            throw new InvalidOperationException("Cannot grow buffer: maximum size reached.");

        var newBuffer = _pool.Rent(newSize);
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);
        _pool.Return(_buffer, clearArray: false);
        _buffer = newBuffer;
    }
}

/// <summary>
/// Kafka RecordBatch v2 format (magic byte 2).
/// This is the modern record format used since Kafka 0.11.
/// Supports lazy record parsing - records are only parsed when enumerated.
/// </summary>
/// <remarks>
/// When created via Read() during FetchResponse parsing with pooled memory context,
/// the batch itself lazily parses records that reference the pooled network buffer.
/// Call DisposeRecords() to release the pooled memory when done consuming records.
/// </remarks>
public sealed class RecordBatch : IReadOnlyList<Record>, IDisposable
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

    internal const int BatchLengthOffset = sizeof(long);
    internal const int BatchBodyOffset = BatchLengthOffset + sizeof(int);
    internal const int CrcOffset = BatchBodyOffset + sizeof(int) + sizeof(byte);
    internal const int CrcContentOffset = CrcOffset + sizeof(uint);
    internal const int CrcContentFixedSize = 40;
    internal const int BatchHeaderSize = sizeof(int) + sizeof(byte) + sizeof(uint) + sizeof(short) +
                                         sizeof(int) + sizeof(long) + sizeof(long) + sizeof(long) +
                                         sizeof(short) + sizeof(int) + sizeof(int);

    /// <summary>
    /// Total header size including baseOffset(8) + batchLength(4) + BatchHeaderSize(49) = 61 bytes.
    /// </summary>
    internal const int TotalBatchHeaderSize = BatchBodyOffset + BatchHeaderSize;

    // Bounded pool of scratch buffer caches for RecordBatch serialization/deserialization.
    // Previously [ThreadStatic], but ConfigureAwait(false) in BrokerSender send loops causes
    // thread migration — each unique thread that handled serialization retained a permanent
    // ~1MB buffer rented from DekafPools.SerializationBuffers. With 3+ brokers, dozens of
    // threads accumulated caches over time, depleting the pool and driving Gen2 GC pressure.
    private const int MaxPooledCaches = 16;
    private static readonly LockFreeStack<SerializationCache> s_cachePool = new(MaxPooledCaches);

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
        if (!s_cachePool.TryPush(cache))
            cache.Dispose();
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
    private ReadOnlyMemory<byte> _rawRecordData;
    private byte[]? _pooledRecordData;
    private Record[]? _parsedRecords;
    private int _parsedRecordsOffset;
    private bool _ownsParsedRecords;
    private int _recordCount;
    private int _parsedRecordCount;
    private int _nextRecordParseOffset;

    int IReadOnlyCollection<Record>.Count =>
        ReferenceEquals(_records, this) ? GetLazyRecordCount() : Records.Count;

    Record IReadOnlyList<Record>.this[int index] =>
        ReferenceEquals(_records, this) ? GetLazyRecord(index) : Records[index];

    IEnumerator<Record> IEnumerable<Record>.GetEnumerator() =>
        ReferenceEquals(_records, this) ? EnumerateLazyRecords().GetEnumerator() : Records.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        ((IEnumerable<Record>)this).GetEnumerator();

    private int GetLazyRecordCount()
    {
        ThrowIfNotLazyRecordList();
        return _recordCount;
    }

    private Record GetLazyRecord(int index)
    {
        ThrowIfNotLazyRecordList();
        if (index < 0 || index >= _recordCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        EnsureLazyRecordsParsedUpTo(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _recordCount);
        return _parsedRecords![_parsedRecordsOffset + index];
    }

    private IEnumerable<Record> EnumerateLazyRecords()
    {
        var initialRecordCount = _recordCount;
        for (var i = 0; i < initialRecordCount; i++)
        {
            ThrowIfNotLazyRecordList();
            EnsureLazyRecordsParsedUpTo(i);
            if (i >= _recordCount)
                yield break;

            yield return _parsedRecords![_parsedRecordsOffset + i];
        }
    }

    private void ThrowIfNotLazyRecordList()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(RecordBatch));
        if (!ReferenceEquals(_records, this))
            throw new InvalidOperationException("This batch does not own a lazy record list.");
    }

    private void InitializeLazyRecords(ReadOnlyMemory<byte> rawData, byte[]? pooledArray, int count)
    {
        _rawRecordData = rawData;
        _pooledRecordData = pooledArray;
        _recordCount = count;
        _records = this;
    }

    internal int UnparsedLazyRecordCount =>
        ReferenceEquals(_records, this) && _parsedRecords is null ? _recordCount : -1;

    internal void UseParsedRecordSlab(Record[] records, int offset)
    {
        if (!ReferenceEquals(_records, this) || _parsedRecords is not null)
            throw new InvalidOperationException("A parsed-record slab can only be attached to an unparsed lazy batch.");
        if ((uint)offset > (uint)records.Length || _recordCount > records.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _parsedRecords = records;
        _parsedRecordsOffset = offset;
        _ownsParsedRecords = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnsureAllRecordsParsed()
    {
        if (ReferenceEquals(_records, this)
            && _recordCount > 0
            && (_parsedRecords is null || _parsedRecordCount < _recordCount))
        {
            EnsureLazyRecordsParsedUpTo(_recordCount - 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Record[]? GetParsedRecordsArray() =>
        ReferenceEquals(_records, this) ? _parsedRecords : null;

    internal int GetParsedRecordsOffset() =>
        ReferenceEquals(_records, this) ? _parsedRecordsOffset : 0;

    internal const int MaxReasonableLazyRecordCount = 1_000_000;

    internal static bool IsTruncatedRecordTail(
        Exception exception,
        ReadOnlyMemory<byte> availableRecordData,
        int parsedCount,
        int declaredCount)
    {
        if (exception is InsufficientDataException)
        {
            // An incomplete record body has no trusted boundary: opaque key, value, or
            // header bytes can look like a valid record. Preserve tail-truncation behavior
            // rather than infer an interior record from payload contents.
            return true;
        }

        // Issue #2065 intentionally excludes corruption in the final declared record.
        return exception is MalformedProtocolDataException &&
               (availableRecordData.Length < Record.MinimumEncodedSize ||
                parsedCount == declaredCount - 1);
    }

    private void EnsureLazyRecordsParsedUpTo(int index)
    {
        if (_parsedRecords is null)
        {
            var capacity = _recordCount > MaxReasonableLazyRecordCount ? 16 : _recordCount;
            _parsedRecords = ArrayPool<Record>.Shared.Rent(capacity);
            _ownsParsedRecords = true;
        }

        if (_parsedRecordCount > index || _parsedRecordCount >= _recordCount)
            return;

        var reader = new KafkaProtocolReader(_rawRecordData.Slice(_nextRecordParseOffset));
        var readerStartOffset = _nextRecordParseOffset;
        while (_parsedRecordCount <= index && _parsedRecordCount < _recordCount)
        {
            if (_parsedRecordCount >= _parsedRecords.Length - _parsedRecordsOffset)
            {
                if (!_ownsParsedRecords)
                    throw new InvalidOperationException("The shared parsed-record slab is smaller than the declared record count.");

                var newArray = ArrayPool<Record>.Shared.Rent(_parsedRecords.Length * 2);
                _parsedRecords.AsSpan(_parsedRecordsOffset, _parsedRecordCount).CopyTo(newArray);
                ArrayPool<Record>.Shared.Return(_parsedRecords, clearArray: true);
                _parsedRecords = newArray;
                _parsedRecordsOffset = 0;
            }

            var availableRecordData = _rawRecordData.Slice(
                readerStartOffset + (int)reader.Consumed);
            try
            {
                _parsedRecords[_parsedRecordsOffset + _parsedRecordCount] = Record.Read(ref reader);
                _parsedRecordCount++;
                _nextRecordParseOffset = readerStartOffset + (int)reader.Consumed;
            }
            catch (Exception ex) when (IsTruncatedRecordTail(
                ex,
                availableRecordData,
                _parsedRecordCount,
                _recordCount))
            {
                Trace.WriteLine($"Dekaf: Record parsing error ({ex.GetType().Name}) — {_parsedRecordCount} of {_recordCount} records parsed successfully.");
                _recordCount = _parsedRecordCount;
                break;
            }
        }
    }

    private void DisposeLazyRecords()
    {
        var pooledArray = _pooledRecordData;
        _pooledRecordData = null;
        if (pooledArray is not null)
            ArrayPool<byte>.Shared.Return(pooledArray, clearArray: false);

        var parsedRecords = _parsedRecords;
        _parsedRecords = null;
        if (parsedRecords is not null)
        {
            for (var i = 0; i < _parsedRecordCount; i++)
            {
                var recordIndex = _parsedRecordsOffset + i;
                if (parsedRecords[recordIndex].Headers is { } headers)
                    ArrayPool<Header>.Shared.Return(headers, clearArray: true);
                if (!_ownsParsedRecords)
                    parsedRecords[recordIndex] = default;
            }

            if (_ownsParsedRecords)
                ArrayPool<Record>.Shared.Return(parsedRecords, clearArray: true);
        }

        _rawRecordData = default;
        _recordCount = 0;
        _parsedRecordCount = 0;
        _parsedRecordsOffset = 0;
        _ownsParsedRecords = false;
        _nextRecordParseOffset = 0;
    }

    /// <summary>
    /// Pre-compressed records data. When set, <see cref="Write"/> skips compression
    /// and uses this data directly. The array is rented from <see cref="ProducerDataPool"/>
    /// and must be returned by the caller after Write() completes.
    /// Set by <see cref="PreCompress"/> before send so <see cref="Write"/> can emit
    /// the compressed payload directly.
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
    /// CRC32C of the bytes in <see cref="PreCompressedRecords"/>.
    /// Valid only while the pooled buffer remains retained for this batch and unmodified through Write().
    /// </summary>
    internal uint PreCompressedRecordsCrc { get; private set; }

    internal bool HasPreCompressedRecords => PreCompressedRecords is not null;

    private ReadOnlyMemory<byte> _preEncodedRecords;
    private ReadOnlySequence<byte> _segmentedPreEncodedRecords;
    private bool _hasSegmentedPreEncodedRecords;
    private bool _hasPreEncodedRecords;
    // Valid only while _preEncodedRecords remains retained for this batch and unmodified through Write().
    private uint _preEncodedRecordsCrc;

    internal bool HasPreEncodedRecords => _hasPreEncodedRecords;

    internal int PreEncodedRecordsLength => _hasPreEncodedRecords ? GetPreEncodedRecordsLength() : 0;

    internal IReadOnlyList<Record> GetRecordsForSplit()
    {
        if (_records is not ProducerRecordCountList countOnlyRecords)
            return Records;

        if (!_hasPreEncodedRecords)
            throw new InvalidOperationException("Producer records cannot be split without encoded data.");

        var encodedLength = GetPreEncodedRecordsLength();
        var encodedRecords = ArrayPool<byte>.Shared.Rent(encodedLength);
        try
        {
            GetPreEncodedRecordsSequence().CopyTo(encodedRecords);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(encodedRecords, clearArray: false);
            throw;
        }

        LazyRecordList materializedRecords;
        try
        {
            materializedRecords = LazyRecordList.Create(
                new PooledRecordData(encodedRecords, encodedLength),
                countOnlyRecords.Count);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(encodedRecords, clearArray: false);
            throw;
        }

        countOnlyRecords.Dispose();
        _records = materializedRecords;
        return materializedRecords;
    }

    internal void SetPreEncodedRecords(ReadOnlyMemory<byte> records)
    {
        _preEncodedRecords = records;
        _segmentedPreEncodedRecords = default;
        _hasPreEncodedRecords = true;
        _hasSegmentedPreEncodedRecords = false;
        _preEncodedRecordsCrc = Crc32C.Compute(records.Span);
    }

    internal void SetPreEncodedRecords(ReadOnlySequence<byte> records)
    {
        if (records.IsSingleSegment)
        {
            SetPreEncodedRecords(records.First);
            return;
        }

        _preEncodedRecords = default;
        _segmentedPreEncodedRecords = records;
        _hasPreEncodedRecords = true;
        _hasSegmentedPreEncodedRecords = true;
        _preEncodedRecordsCrc = Crc32C.Compute(records);
    }

    /// <summary>
    /// Pre-serializes the records in this batch, compressing when a codec is configured.
    /// Producer send paths call this before request serialization so <see cref="Write"/>
    /// can emit the records with a single copy + CRC instead of re-encoding every record
    /// on the send-loop thread. The data is stored in a pooled array (a per-batch
    /// allocation, acceptable).
    /// </summary>
    /// <param name="compression">The compression type to apply.</param>
    /// <param name="codecs">The codec registry to use.</param>
    internal void PreCompress(CompressionType compression, CompressionCodecRegistry? codecs)
    {
        if (compression == CompressionType.None)
        {
            if (_hasPreEncodedRecords)
            {
                return;
            }

            var uncompressedRecords = Records;
            using var encodedBuffer = new DetachableBufferWriter(
                ProducerDataPool.BytePool, GetEncodedRecordsLength(uncompressedRecords));
            var writer = new KafkaProtocolWriter(encodedBuffer);
            WriteRecords(uncompressedRecords, ref writer);

            PreCompressedRecordsCrc = Crc32C.Compute(encodedBuffer.WrittenSpan);
            PreCompressedRecords = encodedBuffer.DetachBuffer(out var encodedLength);
            PreCompressedLength = encodedLength;
            PreCompressedType = CompressionType.None;
            return;
        }

        var registry = codecs ?? CompressionCodecRegistry.Default;
        var codec = registry.GetCodec(compression);
        if (_hasPreEncodedRecords)
        {
            using var compressedBuffer = new DetachableBufferWriter(
                ProducerDataPool.BytePool,
                GetPreEncodedRecordsLength());
            codec.Compress(GetPreEncodedRecordsSequence(), compressedBuffer);
            PreCompressedRecordsCrc = Crc32C.Compute(compressedBuffer.WrittenSpan);
            PreCompressedRecords = compressedBuffer.DetachBuffer(out var compressedLength);
            PreCompressedLength = compressedLength;
            PreCompressedType = compression;
            return;
        }

        var cache = RentSerializationCache();
        try
        {
            // Compress directly into a detachable producer-pool buffer.
            var recordsBuffer = GetRecordsBuffer(cache);
            var recordsWriter = new KafkaProtocolWriter(recordsBuffer);
            var records = Records;

            WriteRecords(records, ref recordsWriter);
            using var serializedCompressedBuffer = new DetachableBufferWriter(ProducerDataPool.BytePool, recordsBuffer.WrittenCount);
            codec.Compress(new ReadOnlySequence<byte>(recordsBuffer.WrittenMemory), serializedCompressedBuffer);
            PreCompressedRecordsCrc = Crc32C.Compute(serializedCompressedBuffer.WrittenSpan);
            PreCompressedRecords = serializedCompressedBuffer.DetachBuffer(out var serializedCompressedLength);
            PreCompressedLength = serializedCompressedLength;
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
            PreCompressedType = CompressionType.None;
            PreCompressedRecordsCrc = 0;
            Producer.ProducerDataPool.BytePool.Return(array, clearArray: false);
        }
    }

    internal void DisposeRecordList()
    {
        if (ReferenceEquals(_records, this))
        {
            DisposeLazyRecords();
        }
        else if (_records is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _records = null!;
    }

    /// <summary>
    /// Disposes pooled memory associated with this batch's records without returning the
    /// batch object to its pool. Consumer paths must use the owner-aware return helpers;
    /// producer paths call <see cref="ReturnToPool"/> after their ownership ends.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        ReturnPreCompressedBuffer();

        DisposeRecordList();
    }

    private PendingFetchData? _consumerPoolOwner;
    private int _consumerPoolOwnerGeneration;

    internal void AttachConsumerPoolOwner(PendingFetchData owner, int generation)
    {
        _consumerPoolOwner = owner;
        _consumerPoolOwnerGeneration = generation;
    }

    internal void DisposeAndReturnConsumerBatch(PendingFetchData owner, int generation)
    {
        if (!ReferenceEquals(_consumerPoolOwner, owner) || _consumerPoolOwnerGeneration != generation)
            return;

        Dispose();
        ReturnToPool();
    }

    internal void DisposeAndReturnUnownedConsumerBatch()
    {
        if (_consumerPoolOwner is not null)
            return;

        Dispose();
        ReturnToPool();
    }

    // ── Pool for producer and consumer RecordBatch reuse ──
    // Eliminates per-batch class allocation (~120 bytes) that survives Gen0 due to
    // its lifetime spanning producer and consumer pipelines, contributing to Gen1 pressure.
    // Reuses ObjectPool<T> for zero-alloc CAS-based pooling with miss tracking and exception safety.
    private static readonly RecordBatchPool s_pool = new();
    private static int s_trackedReturnThreadId;
    private static int s_trackedReturnCount;

    internal static int MaxPoolSizeValue => s_pool.MaxPoolSize;

    internal static void RatchetPoolSize(int newSize) => s_pool.RatchetMaxPoolSize(newSize);

    /// <summary>
    /// Starts an opt-in return counter scoped to the current thread. Intended for deterministic
    /// failure-path tests where global pool ordering is unobservable under concurrent returns.
    /// </summary>
    internal static void BeginTrackingPoolReturnsForCurrentThread()
    {
        if (Interlocked.CompareExchange(ref s_trackedReturnThreadId, -1, 0) != 0)
            throw new InvalidOperationException("A RecordBatch pool return tracker is already active.");

        Volatile.Write(ref s_trackedReturnCount, 0);
        Volatile.Write(ref s_trackedReturnThreadId, Environment.CurrentManagedThreadId);
    }

    /// <summary>Stops the current-thread return counter and returns its observed count.</summary>
    internal static int EndTrackingPoolReturnsForCurrentThread()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        if (Interlocked.CompareExchange(ref s_trackedReturnThreadId, -1, currentThreadId) != currentThreadId)
            throw new InvalidOperationException("RecordBatch pool return tracking must end on its starting thread.");

        var count = Interlocked.Exchange(ref s_trackedReturnCount, 0);
        Volatile.Write(ref s_trackedReturnThreadId, 0);
        return count;
    }

    private sealed class RecordBatchPool : ObjectPool<RecordBatch>
    {
        public RecordBatchPool() : base(maxPoolSize: 2048) { }
        protected override RecordBatch Create() => new();
        protected override void Reset(RecordBatch item)
        {
            // Clear references to avoid holding onto GC-tracked objects
            item._records = null!;
            item._rawRecordData = default;
            item._pooledRecordData = null;
            item._parsedRecords = null;
            item._parsedRecordsOffset = 0;
            item._ownsParsedRecords = false;
            item._recordCount = 0;
            item._parsedRecordCount = 0;
            item._nextRecordParseOffset = 0;
            item.PreCompressedRecords = null;
            item.PreCompressedLength = 0;
            item.PreCompressedType = CompressionType.None;
            item.PreCompressedRecordsCrc = 0;
            item._preEncodedRecords = default;
            item._segmentedPreEncodedRecords = default;
            item._hasPreEncodedRecords = false;
            item._hasSegmentedPreEncodedRecords = false;
            item._preEncodedRecordsCrc = 0;
            item._consumerPoolOwner = null;
            item._consumerPoolOwnerGeneration = 0;

            // Reset mutable state to defaults
            item.BaseOffset = 0;
            item.BatchLength = 0;
            item.PartitionLeaderEpoch = -1;
            item.Magic = 2;
            item.Crc = 0;
            item.Attributes = RecordBatchAttributes.None;
            item.LastOffsetDelta = 0;
            item.BaseTimestamp = 0;
            item.MaxTimestamp = 0;
            item.ProducerId = -1;
            item.ProducerEpoch = -1;
            item.BaseSequence = -1;
        }
    }

    /// <summary>
    /// Rents a RecordBatch from the pool or creates a new one.
    /// Caller must call <see cref="ReturnToPool"/> after the batch is fully processed.
    /// Consumer-read batches are returned by their owning <c>PendingFetchData</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RecordBatch RentFromPool()
    {
        var batch = s_pool.Rent();
        batch._returnedToPoolFlag = 0;
        Volatile.Write(ref batch._disposed, 0);
        return batch;
    }

    /// <summary>
    /// Returns this RecordBatch to the pool for reuse. Clears all references to avoid
    /// holding onto Records data. Producer callers must call
    /// <see cref="ReturnPreCompressedBuffer"/> first when applicable.
    /// </summary>
    internal void ReturnToPool()
    {
        // Guard against double-return (e.g. multiple Dispose() calls).
        // Interlocked.Exchange ensures exactly one thread passes this gate.
        if (Interlocked.Exchange(ref _returnedToPoolFlag, 1) != 0)
            return;

        s_pool.Return(this);
        var trackedThreadId = Volatile.Read(ref s_trackedReturnThreadId);
        if (trackedThreadId != 0 && trackedThreadId == Environment.CurrentManagedThreadId)
            Interlocked.Increment(ref s_trackedReturnCount);
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
        batch._preEncodedRecords = _preEncodedRecords;
        batch._segmentedPreEncodedRecords = _segmentedPreEncodedRecords;
        batch._hasPreEncodedRecords = _hasPreEncodedRecords;
        batch._hasSegmentedPreEncodedRecords = _hasSegmentedPreEncodedRecords;
        batch._preEncodedRecordsCrc = _preEncodedRecordsCrc;

        // Transfer pre-compressed data — records content is unchanged,
        // only header fields differ (CRC is recomputed in Write()).
        if (PreCompressedRecords is not null)
        {
            batch.PreCompressedRecords = PreCompressedRecords;
            batch.PreCompressedLength = PreCompressedLength;
            batch.PreCompressedType = PreCompressedType;
            batch.PreCompressedRecordsCrc = PreCompressedRecordsCrc;

            // Clear from old batch to prevent double-return to ArrayPool
            PreCompressedRecords = null;
            PreCompressedLength = 0;
            PreCompressedType = CompressionType.None;
            PreCompressedRecordsCrc = 0;
        }

        return batch;
    }

    /// <summary>
    /// Writes the record batch to the output buffer.
    /// </summary>
    /// <remarks>
    /// <para>Current encoding requests one contiguous destination span for the CRC field plus
    /// the CRC-covered batch body. This keeps the array-backed writer path zero-copy and
    /// lets the CRC be backpatched in place, but it means segmented writers must be able
    /// to provide that full span. A streaming writer path would need incremental CRC32C
    /// calculation while emitting fields and records in smaller segments.</para>
    /// <para>Write does not mutate batch state — the CRC is computed into the output only —
    /// and may be invoked repeatedly on the same instance. The epoch-bump rewrite path
    /// (<see cref="WithProducerState"/>) and the benchmarks rely on this.</para>
    /// </remarks>
    /// <returns>
    /// The exact number of bytes written to <paramref name="output"/>. Callers that
    /// pre-compute an encoded size (e.g. the PRODUCE records length prefix) must verify it
    /// against this value: a mismatch means the batch changed between sizing and writing,
    /// and emitting the frame would desync the connection's outgoing byte stream.
    /// </returns>
    public int Write(IBufferWriter<byte> output, CompressionType compression = CompressionType.None, CompressionCodecRegistry? codecs = null)
    {
        var writer = new KafkaProtocolWriter(output);
        var records = Records;

        // Determine the effective compression and records data to write.
        // If pre-compressed at seal time, use that data directly (skip CPU-bound compression
        // on the send loop thread). Otherwise, serialize and compress inline.
        ReadOnlySpan<byte> compressedRecords;
        ReadOnlySequence<byte> segmentedRecords = default;
        var recordsAreSegmented = false;
        uint compressedRecordsCrc;
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
                compressedRecordsCrc = PreCompressedRecordsCrc;
                effectiveCompression = PreCompressedType;
            }
            else if (_hasSegmentedPreEncodedRecords)
            {
                compressedRecords = default;
                segmentedRecords = _segmentedPreEncodedRecords;
                recordsAreSegmented = true;
                compressedRecordsCrc = _preEncodedRecordsCrc;
                effectiveCompression = CompressionType.None;
            }
            else if (_hasPreEncodedRecords)
            {
                compressedRecords = _preEncodedRecords.Span;
                compressedRecordsCrc = _preEncodedRecordsCrc;
                effectiveCompression = CompressionType.None;
            }
            else
            {
                cache = RentSerializationCache();

                // Serialize records to pooled scratch buffer
                var recordsBuffer = GetRecordsBuffer(cache);
                var recordsWriter = new KafkaProtocolWriter(recordsBuffer);

                WriteRecords(records, ref recordsWriter);

                // Apply compression if needed
                if (compression != CompressionType.None)
                {
                    var registry = codecs ?? CompressionCodecRegistry.Default;
                    var codec = registry.GetCodec(compression);
                    var compressedBuffer = GetCompressedBuffer(cache);
                    codec.Compress(new ReadOnlySequence<byte>(recordsBuffer.WrittenMemory), compressedBuffer);
                    compressedRecords = compressedBuffer.WrittenSpan;
                    compressedRecordsCrc = Crc32C.Compute(compressedBuffer.WrittenSpan);
                    effectiveCompression = compression;
                }
                else
                {
                    compressedRecords = recordsBuffer.WrittenSpan;
                    compressedRecordsCrc = Crc32C.Compute(recordsBuffer.WrittenSpan);
                    effectiveCompression = CompressionType.None;
                }
            }

            // Calculate batch length: from partition leader epoch to end
            // 4 (partition leader epoch) + 1 (magic) + 4 (crc) + 2 (attributes) +
            // 4 (last offset delta) + 8 (base timestamp) + 8 (max timestamp) +
            // 8 (producer id) + 2 (producer epoch) + 4 (base sequence) + 4 (records count) + records
            var recordsLength = recordsAreSegmented
                ? checked((int)segmentedRecords.Length)
                : compressedRecords.Length;
            var batchLength = BatchHeaderSize + recordsLength;

            writer.WriteInt64(BaseOffset);
            writer.WriteInt32(batchLength);
            writer.WriteInt32(PartitionLeaderEpoch);
            writer.WriteUInt8(Magic);

            // CRC content size: 2 (attributes) + 4 (lastOffsetDelta) + 8 (baseTimestamp) +
            // 8 (maxTimestamp) + 8 (producerId) + 2 (producerEpoch) + 4 (baseSequence) +
            // 4 (recordsCount) + compressedRecords = 40 + records
            var crcContentSize = CrcContentFixedSize + recordsLength;

            // Get one contiguous span for CRC (4 bytes) + content, write content directly,
            // calculate CRC, and backpatch. This avoids a crcBuffer intermediate copy, but
            // is the current contiguous-destination constraint documented on Write().
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
            BinaryPrimitives.WriteInt32BigEndian(contentSpan[offset..], records.Count);
            offset += 4;
            var headerCrc = Crc32C.Compute(contentSpan[..CrcContentFixedSize]);
            if (recordsAreSegmented)
                segmentedRecords.CopyTo(contentSpan[offset..]);
            else
                compressedRecords.CopyTo(contentSpan[offset..]);

            var crc = Crc32C.Combine(headerCrc, compressedRecordsCrc, recordsLength);

            // Backpatch CRC at the beginning
            BinaryPrimitives.WriteInt32BigEndian(crcAndContentSpan, (int)crc);

            // Advance the output by the full amount written.
            // Must happen before ReturnSerializationCache in the finally block,
            // because compressedRecords may reference the cache's buffer writer.
            output.Advance(4 + crcContentSize);

            return TotalBatchHeaderSize + recordsLength;
        }
        finally
        {
            if (cache is not null)
                ReturnSerializationCache(cache);
        }
    }

    internal bool TryWriteSegmentedHeader(
        IBufferWriter<byte> output,
        CompressionType compression,
        out ReadOnlySequence<byte> encodedRecords)
    {
        uint encodedRecordsCrc;
        CompressionType effectiveCompression;
        if (PreCompressedRecords is { } preCompressedRecords)
        {
            encodedRecords = new ReadOnlySequence<byte>(
                preCompressedRecords.AsMemory(0, PreCompressedLength));
            encodedRecordsCrc = PreCompressedRecordsCrc;
            effectiveCompression = PreCompressedType;
        }
        else if (compression == CompressionType.None && _hasPreEncodedRecords)
        {
            encodedRecords = GetPreEncodedRecordsSequence();
            encodedRecordsCrc = _preEncodedRecordsCrc;
            effectiveCompression = CompressionType.None;
        }
        else
        {
            encodedRecords = default;
            return false;
        }

        var writer = new KafkaProtocolWriter(output);
        writer.WriteInt64(BaseOffset);
        var encodedRecordsLength = checked((int)encodedRecords.Length);
        writer.WriteInt32(BatchHeaderSize + encodedRecordsLength);
        writer.WriteInt32(PartitionLeaderEpoch);
        writer.WriteUInt8(Magic);

        var crcAndContentSpan = output.GetSpan(sizeof(uint) + CrcContentFixedSize);
        var contentSpan = crcAndContentSpan[sizeof(uint)..];
        var attributes = (short)Attributes;
        if (effectiveCompression != CompressionType.None)
            attributes = (short)((attributes & ~0x07) | (int)effectiveCompression);

        BinaryPrimitives.WriteInt16BigEndian(contentSpan, attributes);
        BinaryPrimitives.WriteInt32BigEndian(contentSpan[2..], LastOffsetDelta);
        BinaryPrimitives.WriteInt64BigEndian(contentSpan[6..], BaseTimestamp);
        BinaryPrimitives.WriteInt64BigEndian(contentSpan[14..], MaxTimestamp);
        BinaryPrimitives.WriteInt64BigEndian(contentSpan[22..], ProducerId);
        BinaryPrimitives.WriteInt16BigEndian(contentSpan[30..], ProducerEpoch);
        BinaryPrimitives.WriteInt32BigEndian(contentSpan[32..], BaseSequence);
        BinaryPrimitives.WriteInt32BigEndian(contentSpan[36..], Records.Count);

        var headerCrc = Crc32C.Compute(contentSpan[..CrcContentFixedSize]);
        var crc = Crc32C.Combine(headerCrc, encodedRecordsCrc, encodedRecordsLength);
        BinaryPrimitives.WriteInt32BigEndian(crcAndContentSpan, (int)crc);
        output.Advance(sizeof(uint) + CrcContentFixedSize);
        return true;
    }

    internal bool CanWriteSegmented(CompressionType compression)
        => PreCompressedRecords is not null
            || (compression == CompressionType.None && _hasPreEncodedRecords);

    internal int GetEncodedSize(CompressionType compression = CompressionType.None)
    {
        var recordsLength = GetEncodedRecordsLength(compression);
        return checked(TotalBatchHeaderSize + recordsLength);
    }

    private int GetEncodedRecordsLength(CompressionType compression)
    {
        if (PreCompressedRecords is not null)
            return PreCompressedLength;

        if (compression != CompressionType.None)
            throw new InvalidOperationException("Compressed RecordBatch size requires pre-compressed records.");

        if (_hasPreEncodedRecords)
            return GetPreEncodedRecordsLength();

        return GetEncodedRecordsLength(Records);
    }

    private ReadOnlySequence<byte> GetPreEncodedRecordsSequence() =>
        _hasSegmentedPreEncodedRecords
            ? _segmentedPreEncodedRecords
            : new ReadOnlySequence<byte>(_preEncodedRecords);

    private int GetPreEncodedRecordsLength() =>
        _hasSegmentedPreEncodedRecords
            ? checked((int)_segmentedPreEncodedRecords.Length)
            : _preEncodedRecords.Length;

    private static void WriteRecords(IReadOnlyList<Record> records, ref KafkaProtocolWriter writer)
    {
        switch (records)
        {
            case Record[] array:
                WriteRecordSpan(array, ref writer);
                return;
            case LazyRecordList lazyRecords:
                lazyRecords.EnsureAllParsed();
                var parsedRecords = lazyRecords.GetParsedArray();
                if (parsedRecords is not null)
                    WriteRecordSpan(parsedRecords.AsSpan(0, lazyRecords.Count), ref writer);
                return;
            default:
                WriteRecordList(records, ref writer);
                return;
        }
    }

    private static void WriteRecordSpan(ReadOnlySpan<Record> records, ref KafkaProtocolWriter writer)
    {
        foreach (ref readonly var record in records)
        {
            record.Write(ref writer);
        }
    }

    private static void WriteRecordList(IReadOnlyList<Record> records, ref KafkaProtocolWriter writer)
    {
        var count = records.Count;
        for (var i = 0; i < count; i++)
        {
            var record = records[i];
            record.Write(ref writer);
        }
    }

    private static int GetEncodedRecordsLength(IReadOnlyList<Record> records)
    {
        return records switch
        {
            Record[] array => GetEncodedRecordSpanLength(array),
            LazyRecordList lazyRecords => GetEncodedLazyRecordListLength(lazyRecords),
            _ => GetEncodedRecordListLength(records)
        };
    }

    private static int GetEncodedLazyRecordListLength(LazyRecordList lazyRecords)
    {
        lazyRecords.EnsureAllParsed();
        var parsedRecords = lazyRecords.GetParsedArray();
        return parsedRecords is null ? 0 : GetEncodedRecordSpanLength(parsedRecords.AsSpan(0, lazyRecords.Count));
    }

    private static int GetEncodedRecordSpanLength(ReadOnlySpan<Record> records)
    {
        var recordsLength = 0;
        foreach (ref readonly var record in records)
        {
            checked
            {
                recordsLength += GetEncodedRecordLength(in record);
            }
        }

        return recordsLength;
    }

    private static int GetEncodedRecordListLength(IReadOnlyList<Record> records)
    {
        var recordsLength = 0;
        var count = records.Count;
        for (var i = 0; i < count; i++)
        {
            var record = records[i];
            checked
            {
                recordsLength += GetEncodedRecordLength(in record);
            }
        }

        return recordsLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetEncodedRecordLength(in Record record)
    {
        var bodySize = record.CachedBodySize > 0
            ? record.CachedBodySize
            : Record.ComputeBodySize(
                record.TimestampDelta,
                record.OffsetDelta,
                record.IsKeyNull,
                record.Key.Length,
                record.IsValueNull,
                record.Value.Length,
                record.Headers,
                record.EffectiveHeaderCount);

        return checked(Record.VarIntSize(bodySize) + bodySize);
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
    public static RecordBatch Read(
        ref KafkaProtocolReader reader,
        CompressionCodecRegistry? codecs = null,
        int availableBytes = int.MaxValue) =>
        Read(ref reader, codecs, availableBytes, checkCrcs: false);

    /// <summary>
    /// Reads a RecordBatch and optionally verifies its CRC-32C before decompression.
    /// </summary>
    public static RecordBatch Read(
        ref KafkaProtocolReader reader,
        CompressionCodecRegistry? codecs,
        int availableBytes,
        bool checkCrcs)
    {
        if (availableBytes < TotalBatchHeaderSize)
        {
            if (availableBytes > 0)
            {
                reader.Skip(availableBytes);
            }

            throw new InsufficientDataException();
        }

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

        if (checkCrcs || ResponseParsingContext.CheckCrcs)
        {
            ValidateCrc(
                crc,
                attributes,
                lastOffsetDelta,
                baseTimestamp,
                maxTimestamp,
                producerId,
                producerEpoch,
                baseSequence,
                recordCount,
                rawRecordData.Span);
        }

        // Determine how to handle the record data based on compression and pooled memory availability
        ReadOnlyMemory<byte> lazyRecordData;
        byte[]? pooledRecordData;

        if (compression != CompressionType.None)
        {
            // Decompress directly into an ArrayPool<byte>.Shared-backed writer, then
            // detach the array for zero-copy handoff to the pooled RecordBatch.
            // This eliminates the previous decompress-to-scratch-then-copy pattern.
            var registry = codecs ?? CompressionCodecRegistry.Default;
            var codec = registry.GetCodec(compression);
            var estimatedSize = recordsLength * 4; // Estimate 4x expansion
            using var decompressWriter = new DetachableBufferWriter(ArrayPool<byte>.Shared, estimatedSize);
            codec.Decompress(new ReadOnlySequence<byte>(rawRecordData), decompressWriter);

            // Transfer ownership of the pooled array — Dispose is a no-op after DetachBuffer.
            var pooledArray = decompressWriter.DetachBuffer(out var writtenLength);
            lazyRecordData = pooledArray.AsMemory(0, writtenLength);
            pooledRecordData = pooledArray;
        }
        else if (ResponseParsingContext.HasPooledMemory)
        {
            // Zero-copy: use the raw data directly from the pooled buffer
            // Mark that at least one batch used the pooled memory, so ownership
            // will be transferred to PendingFetchData after parsing completes
            ResponseParsingContext.MarkMemoryUsed();
            lazyRecordData = rawRecordData;
            pooledRecordData = null;
        }
        else
        {
            // No pooled memory available (legacy path or not from network)
            // Use pooled array to avoid GC allocation from ToArray()
            var length = rawRecordData.Length;
            var pooledArray = ArrayPool<byte>.Shared.Rent(length);
            rawRecordData.Span.CopyTo(pooledArray);
            lazyRecordData = pooledArray.AsMemory(0, length);
            pooledRecordData = pooledArray;
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
        batch.InitializeLazyRecords(lazyRecordData, pooledRecordData, recordCount);
        return batch;
    }

    private static void ValidateCrc(
        uint expectedCrc,
        RecordBatchAttributes attributes,
        int lastOffsetDelta,
        long baseTimestamp,
        long maxTimestamp,
        long producerId,
        short producerEpoch,
        int baseSequence,
        int recordCount,
        ReadOnlySpan<byte> recordData)
    {
        Span<byte> header = stackalloc byte[CrcContentFixedSize];
        BinaryPrimitives.WriteInt16BigEndian(header, (short)attributes);
        BinaryPrimitives.WriteInt32BigEndian(header[2..], lastOffsetDelta);
        BinaryPrimitives.WriteInt64BigEndian(header[6..], baseTimestamp);
        BinaryPrimitives.WriteInt64BigEndian(header[14..], maxTimestamp);
        BinaryPrimitives.WriteInt64BigEndian(header[22..], producerId);
        BinaryPrimitives.WriteInt16BigEndian(header[30..], producerEpoch);
        BinaryPrimitives.WriteInt32BigEndian(header[32..], baseSequence);
        BinaryPrimitives.WriteInt32BigEndian(header[36..], recordCount);

        var headerCrc = Crc32C.Compute(header);
        var actualCrc = Crc32C.Combine(headerCrc, Crc32C.Compute(recordData), recordData.Length);
        if (actualCrc != expectedCrc)
        {
            throw new InvalidDataException(
                $"Record batch CRC mismatch: expected 0x{expectedCrc:X8}, computed 0x{actualCrc:X8}.");
        }
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

/// <summary>
/// Count-only producer record view. Producer batches already contain encoded records, so send
/// paths need only the count and never materialize <see cref="Record"/> instances.
/// </summary>
internal sealed class ProducerRecordCountList : IReadOnlyList<Record>, IDisposable
{
    private static readonly ProducerRecordCountListPool s_pool = new();
    private int _count;
    private int _disposed;

    private sealed class ProducerRecordCountListPool()
        : ObjectPool<ProducerRecordCountList>(2048)
    {
        protected override ProducerRecordCountList Create() => new();
        protected override void Reset(ProducerRecordCountList item) => item._count = 0;
    }

    private ProducerRecordCountList() { }

    internal static ProducerRecordCountList Rent(int count)
    {
        var list = s_pool.Rent();
        list._count = count;
        Volatile.Write(ref list._disposed, 0);
        return list;
    }

    public int Count => Volatile.Read(ref _disposed) == 0
        ? _count
        : throw new ObjectDisposedException(nameof(ProducerRecordCountList));

    public Record this[int index] => throw new NotSupportedException(
        "Producer record data is retained in encoded form and cannot be indexed.");

    public IEnumerator<Record> GetEnumerator() => throw new NotSupportedException(
        "Producer record data is retained in encoded form and cannot be enumerated.");

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            s_pool.Return(this);
    }
}

internal sealed class LazyRecordList : IReadOnlyList<Record>, IDisposable
{
    // Pool for reusing LazyRecordList instances to eliminate per-batch class allocation.
    private const int DefaultMaxPooledInstances = 256;
    private static readonly LazyRecordListPool s_instancePool = new();

    private sealed class LazyRecordListPool() : ObjectPool<LazyRecordList>(DefaultMaxPooledInstances)
    {
        protected override LazyRecordList Create() => new();
        protected override void Reset(LazyRecordList item) { }
    }

    internal static int MaxPoolSizeValue => s_instancePool.MaxPoolSize;

    internal static void RatchetPoolSize(int newSize) => s_instancePool.RatchetMaxPoolSize(newSize);

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
        var instance = s_instancePool.Rent();
        Volatile.Write(ref instance._disposed, 0);
        return instance;
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

    /// <summary>
    /// Returns the underlying parsed records array for direct access,
    /// bypassing the IReadOnlyList indexer overhead (Volatile.Read + disposed check +
    /// EnsureParsedUpTo call per access). Only valid after <see cref="EnsureAllParsed"/>.
    /// The array may be oversized (rented from ArrayPool); use <see cref="Count"/> for bounds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Record[]? GetParsedArray() => _parsedRecords;

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

            var availableRecordData = _rawData.Slice(
                readerStartOffset + (int)reader.Consumed);
            try
            {
                var record = Record.Read(ref reader);
                _parsedRecords[_parsedCount++] = record;
                _nextParseOffset = readerStartOffset + (int)reader.Consumed;
            }
            catch (Exception ex) when (RecordBatch.IsTruncatedRecordTail(
                ex,
                availableRecordData,
                _parsedCount,
                _count))
            {
                // The raw record data ended before the declared record count. Cap the count to
                // the successfully parsed records. Unambiguous interior corruption propagates.
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

        // Return to pool. If full, let GC handle this instance.
        s_instancePool.Return(this);
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
/// Uses the Castagnoli polynomial (0x1EDC6F41) with hardware acceleration (SSE4.2 or Arm CRC32).
/// </summary>
internal static class Crc32C
{
    private const int TableSize = 256;
    private const int SliceCount = 8;
    private const int PositiveIntBitCount = 31;
#if !NETSTANDARD2_0
    private const int HardwareParallelChunkSize = 512;
    private const int HardwareParallelBlockSize = HardwareParallelChunkSize * 3;
#endif

    private static readonly uint[] Table = GenerateTable();
    // Element n applies 2^n zero bytes to a CRC, so arbitrary lengths compose from set bits.
    private static readonly uint[][] ByteShiftPowerOperators = CreateByteShiftPowerOperators();
#if !NETSTANDARD2_0
    private static readonly uint[] HardwareShiftChunk = CreateShiftOperator(HardwareParallelChunkSize);
    private static readonly uint[] HardwareShiftTwoChunks = CreateShiftOperator(HardwareParallelChunkSize * 2);
#endif

    private static uint[] GenerateTable()
    {
        var table = new uint[SliceCount * TableSize];
        const uint polynomial = 0x82F63B78; // Reversed Castagnoli polynomial

        for (var i = 0; i < TableSize; i++)
        {
            var crc = (uint)i;
            for (var j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
            table[i] = crc;
        }

        for (var slice = 1; slice < SliceCount; slice++)
        {
            var previousOffset = (slice - 1) * TableSize;
            var currentOffset = slice * TableSize;

            for (var i = 0; i < TableSize; i++)
            {
                var crc = table[previousOffset + i];
                table[currentOffset + i] = table[(int)(crc & 0xFF)] ^ (crc >> 8);
            }
        }

        return table;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(ReadOnlySpan<byte> data)
    {
#if !NETSTANDARD2_0
        if (Sse42.IsSupported)
        {
            return ComputeHardwareX86(data);
        }

        if (ArmCrc32.IsSupported)
        {
            return ComputeHardwareArm(data);
        }
#endif

        return ComputeSoftware(data);
    }

    public static uint Compute(ReadOnlySequence<byte> data)
    {
        if (data.IsSingleSegment)
        {
            return Compute(data.First.Span);
        }

        var crc = 0u;
        foreach (var segment in data)
        {
            crc = Combine(crc, Compute(segment.Span), segment.Length);
        }

        return crc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Combine(uint prefixCrc, uint suffixCrc, int suffixByteCount)
        => ShiftCrc32C(prefixCrc, suffixByteCount) ^ suffixCrc;

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ComputeHardwareX86(ReadOnlySpan<byte> data)
    {
        if (Sse42.X64.IsSupported && data.Length >= HardwareParallelBlockSize)
        {
            return ComputeHardwareX86Optimized(data);
        }

        return ComputeHardwareX86Scalar(data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ComputeHardwareX86Scalar(ReadOnlySpan<byte> data)
        => UpdateHardwareX86(data, 0xFFFFFFFFu) ^ 0xFFFFFFFFu;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static uint ComputeHardwareX86Optimized(ReadOnlySpan<byte> data)
    {
        if (!Sse42.X64.IsSupported || data.Length < HardwareParallelBlockSize)
        {
            return ComputeHardwareX86Scalar(data);
        }

        var crc = 0xFFFFFFFFu;
        var offset = 0;

        while (offset + HardwareParallelBlockSize <= data.Length)
        {
            var crc0 = crc;
            var crc1 = 0u;
            var crc2 = 0u;

            for (var i = 0; i < HardwareParallelChunkSize; i += 8)
            {
                crc0 = (uint)Sse42.X64.Crc32(
                    crc0,
                    BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + i, 8)));
                crc1 = (uint)Sse42.X64.Crc32(
                    crc1,
                    BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + HardwareParallelChunkSize + i, 8)));
                crc2 = (uint)Sse42.X64.Crc32(
                    crc2,
                    BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + (HardwareParallelChunkSize * 2) + i, 8)));
            }

            // crc1/crc2 start from zero, so combine the three independent chains by
            // shifting each raw CRC state as though its following chunks were zero bytes.
            crc = ShiftCrc32C(crc0, HardwareShiftTwoChunks) ^ ShiftCrc32C(crc1, HardwareShiftChunk) ^ crc2;
            offset += HardwareParallelBlockSize;
        }

        return UpdateHardwareX86(data[offset..], crc) ^ 0xFFFFFFFFu;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint UpdateHardwareX86(ReadOnlySpan<byte> data, uint crc)
    {
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

        return crc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ComputeHardwareArm(ReadOnlySpan<byte> data)
    {
        if (ArmCrc32.Arm64.IsSupported && data.Length >= HardwareParallelBlockSize)
        {
            return ComputeHardwareArmOptimized(data);
        }

        return ComputeHardwareArmScalar(data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ComputeHardwareArmScalar(ReadOnlySpan<byte> data)
        => UpdateHardwareArm(data, 0xFFFFFFFFu) ^ 0xFFFFFFFFu;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static uint ComputeHardwareArmOptimized(ReadOnlySpan<byte> data)
    {
        if (!ArmCrc32.Arm64.IsSupported || data.Length < HardwareParallelBlockSize)
        {
            return ComputeHardwareArmScalar(data);
        }

        var crc = 0xFFFFFFFFu;
        var offset = 0;

        while (offset + HardwareParallelBlockSize <= data.Length)
        {
            var crc0 = crc;
            var crc1 = 0u;
            var crc2 = 0u;

            for (var i = 0; i < HardwareParallelChunkSize; i += 8)
            {
                crc0 = ArmCrc32.Arm64.ComputeCrc32C(
                    crc0,
                    BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + i, 8)));
                crc1 = ArmCrc32.Arm64.ComputeCrc32C(
                    crc1,
                    BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + HardwareParallelChunkSize + i, 8)));
                crc2 = ArmCrc32.Arm64.ComputeCrc32C(
                    crc2,
                    BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + (HardwareParallelChunkSize * 2) + i, 8)));
            }

            crc = ShiftCrc32C(crc0, HardwareShiftTwoChunks) ^ ShiftCrc32C(crc1, HardwareShiftChunk) ^ crc2;
            offset += HardwareParallelBlockSize;
        }

        return UpdateHardwareArm(data[offset..], crc) ^ 0xFFFFFFFFu;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint UpdateHardwareArm(ReadOnlySpan<byte> data, uint crc)
    {
        var i = 0;

        // Process 8 bytes at a time on Arm64 when available.
        if (ArmCrc32.Arm64.IsSupported)
        {
            while (i + 8 <= data.Length)
            {
                var value = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(i, 8));
                crc = ArmCrc32.Arm64.ComputeCrc32C(crc, value);
                i += 8;
            }
        }

        // Process 4 bytes at a time.
        while (i + 4 <= data.Length)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(i, 4));
            crc = ArmCrc32.ComputeCrc32C(crc, value);
            i += 4;
        }

        // Process remaining bytes.
        while (i < data.Length)
        {
            crc = ArmCrc32.ComputeCrc32C(crc, data[i]);
            i++;
        }

        return crc;
    }
#endif

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static uint ComputeSoftware(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        var i = 0;

        while (i + 8 <= data.Length)
        {
            crc ^= BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(i, 4));
            var next = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(i + 4, 4));

            crc =
                Table[(7 * TableSize) + (int)(crc & 0xFF)] ^
                Table[(6 * TableSize) + (int)((crc >> 8) & 0xFF)] ^
                Table[(5 * TableSize) + (int)((crc >> 16) & 0xFF)] ^
                Table[(4 * TableSize) + (int)(crc >> 24)] ^
                Table[(3 * TableSize) + (int)(next & 0xFF)] ^
                Table[(2 * TableSize) + (int)((next >> 8) & 0xFF)] ^
                Table[TableSize + (int)((next >> 16) & 0xFF)] ^
                Table[(int)(next >> 24)];

            i += 8;
        }

        while (i < data.Length)
        {
            crc = Table[(int)((crc ^ data[i]) & 0xFF)] ^ (crc >> 8);
            i++;
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] CreateShiftOperator(int byteCount)
    {
        var shift = new uint[32];

        for (var bit = 0; bit < 32; bit++)
        {
            shift[bit] = ShiftCrc32CSlow(1u << bit, byteCount);
        }

        return shift;
    }

    private static uint[][] CreateByteShiftPowerOperators()
    {
        var operators = new uint[PositiveIntBitCount][];
        operators[0] = CreateShiftOperator(1);
        for (var bit = 1; bit < operators.Length; bit++)
        {
            var next = new uint[32];
            SquareMatrix(next, operators[bit - 1]);
            operators[bit] = next;
        }

        return operators;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ShiftCrc32C(uint crc, uint[] shiftOperator)
    {
        var shifted = 0u;
        var bit = 0;

        while (crc != 0)
        {
            if ((crc & 1) != 0)
            {
                shifted ^= shiftOperator[bit];
            }

            crc >>= 1;
            bit++;
        }

        return shifted;
    }

    private static uint ShiftCrc32C(uint crc, int byteCount)
    {
        if (byteCount <= 0)
        {
            return crc;
        }

        var bit = 0;
        while (byteCount != 0)
        {
            if ((byteCount & 1) != 0)
            {
                crc = ShiftCrc32C(crc, ByteShiftPowerOperators[bit]);
            }

            byteCount >>= 1;
            bit++;
        }

        return crc;
    }

    private static uint ShiftCrc32CSlow(uint crc, int byteCount)
    {
        if (byteCount <= 0)
        {
            return crc;
        }

        Span<uint> odd = stackalloc uint[32];
        Span<uint> even = stackalloc uint[32];

        odd[0] = 0x82F63B78u;
        var row = 1u;
        for (var i = 1; i < 32; i++)
        {
            odd[i] = row;
            row <<= 1;
        }

        SquareMatrix(even, odd);
        SquareMatrix(odd, even);

        var length = byteCount;
        do
        {
            SquareMatrix(even, odd);
            if ((length & 1) != 0)
            {
                crc = MatrixTimes(even, crc);
            }

            length >>= 1;
            if (length == 0)
            {
                break;
            }

            SquareMatrix(odd, even);
            if ((length & 1) != 0)
            {
                crc = MatrixTimes(odd, crc);
            }

            length >>= 1;
        }
        while (length != 0);

        return crc;
    }

    private static void SquareMatrix(Span<uint> square, ReadOnlySpan<uint> matrix)
    {
        for (var i = 0; i < 32; i++)
        {
            square[i] = MatrixTimes(matrix, matrix[i]);
        }
    }

    private static uint MatrixTimes(ReadOnlySpan<uint> matrix, uint vector)
    {
        var sum = 0u;
        var index = 0;

        while (vector != 0)
        {
            if ((vector & 1) != 0)
            {
                sum ^= matrix[index];
            }

            vector >>= 1;
            index++;
        }

        return sum;
    }
}
