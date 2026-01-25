using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dekaf.Protocol.Records;

namespace Dekaf.Producer;

/// <summary>
/// Represents memory rented from ArrayPool that must be returned when no longer needed.
/// </summary>
public readonly struct PooledMemory
{
    private readonly byte[]? _array;
    private readonly int _length;
    private readonly bool _isNull;

    /// <summary>
    /// Creates a null PooledMemory instance.
    /// </summary>
    public static PooledMemory Null => new(null, 0, isNull: true);

    /// <summary>
    /// Creates a PooledMemory from a rented array.
    /// </summary>
    public PooledMemory(byte[]? array, int length, bool isNull = false)
    {
        _array = array;
        _length = length;
        _isNull = isNull;
    }

    /// <summary>
    /// Whether this represents a null key/value.
    /// </summary>
    public bool IsNull => _isNull;

    /// <summary>
    /// The length of the valid data in the array.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets the underlying array (may be larger than Length).
    /// </summary>
    public byte[]? Array => _array;

    /// <summary>
    /// Gets the data as ReadOnlyMemory.
    /// </summary>
    public ReadOnlyMemory<byte> Memory => _array is null ? ReadOnlyMemory<byte>.Empty : _array.AsMemory(0, _length);

    /// <summary>
    /// Gets the data as ReadOnlySpan.
    /// </summary>
    public ReadOnlySpan<byte> Span => _array is null ? ReadOnlySpan<byte>.Empty : _array.AsSpan(0, _length);

    /// <summary>
    /// Returns the array to the shared pool.
    /// </summary>
    public void Return()
    {
        if (_array is not null)
        {
            ArrayPool<byte>.Shared.Return(_array, clearArray: true);
        }
    }
}

/// <summary>
/// Accumulates records into batches for efficient sending.
/// Provides backpressure through bounded channel capacity (similar to librdkafka's queue.buffering.max.messages).
/// Simple, reliable, and uses modern C# primitives.
/// </summary>
public sealed class RecordAccumulator : IAsyncDisposable
{
    private readonly ProducerOptions _options;
    private readonly ConcurrentDictionary<TopicPartition, PartitionBatch> _batches = new();
    private readonly Channel<ReadyBatch> _readyBatches;
    private readonly Action<TaskCompletionSource<RecordMetadata>> _returnTcsToPool;
    private volatile bool _disposed;
    private volatile bool _closed;

    // Cache for TopicPartition instances to avoid repeated allocations.
    // Using a nested ConcurrentDictionary: outer key is topic (string), inner key is partition (int).
    // This allows O(1) lookup without allocating a TopicPartition struct on the hot path.
    //
    // TRADE-OFF: Unbounded cache growth - the cache grows as new topic-partition pairs are seen.
    // This is acceptable because:
    // 1. Typical workloads have a bounded set of topic-partition pairs (e.g., 100 topics × 10 partitions = 1000 entries)
    // 2. Each entry is small (~50 bytes: string reference + int + dictionary overhead)
    // 3. The memory cost (50KB for 1000 partitions) is negligible compared to batch buffers (MB-GB range)
    // 4. Producers typically write to the same topics throughout their lifetime
    // 5. The cache is cleared on disposal, preventing leaks in producer recreation scenarios
    //
    // For extreme cases with thousands of topics, the memory overhead is still minor and worth the
    // allocation elimination in the critical produce path.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, TopicPartition>> _topicPartitionCache = new();

    public RecordAccumulator(ProducerOptions options, Action<TaskCompletionSource<RecordMetadata>> returnTcsToPool)
    {
        _options = options;
        _returnTcsToPool = returnTcsToPool;

        // Backpressure via channel capacity instead of manual memory tracking
        // MaxQueuedBatches controls how many batches can be in-flight
        // When channel is full, we fail fast (like librdkafka's QUEUE_FULL error)
        var maxQueuedBatches = (int)(options.BufferMemory / (ulong)options.BatchSize);
        if (maxQueuedBatches < 100) maxQueuedBatches = 100; // Minimum queue depth
        if (maxQueuedBatches > 10000) maxQueuedBatches = 10000; // Maximum queue depth

        _readyBatches = Channel.CreateUnbounded<ReadyBatch>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Appends a record to the appropriate batch.
    /// Key and value data are pooled - the batch will return them to the pool when complete.
    /// Header array may also be pooled (if large) and will be returned to pool when batch completes.
    /// The completion source will be completed when the batch is sent.
    /// Backpressure is applied through channel capacity when batches are written.
    /// Optimized to avoid TopicPartition allocation on the hot path by using a nested cache.
    /// </summary>
    public async ValueTask<RecordAppendResult> AppendAsync(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
        TaskCompletionSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RecordAccumulator));

        // OPTIMIZATION: Use a nested cache to get/create TopicPartition without allocating on hot path.
        // First, get or create the partition cache for this topic.
        var partitionCache = _topicPartitionCache.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, TopicPartition>());

        // Then, get or create the TopicPartition for this partition.
        // This is cached, so subsequent calls with same topic/partition reuse the struct.
        var topicPartition = partitionCache.GetOrAdd(partition, static (p, t) => new TopicPartition(t, p), topic);

        // Loop until we successfully append the record.
        // This handles the race condition where multiple threads try to replace a full batch:
        // - GetOrAdd gets existing batch or creates a new one
        // - If append fails (batch full), complete it and atomically remove it
        // - TryRemove(KeyValuePair) only removes if value matches, preventing orphaned batches
        // - Loop retries with the new batch (created by us or another thread)
        while (true)
        {
            var batch = _batches.GetOrAdd(topicPartition, tp => new PartitionBatch(tp, _options, _returnTcsToPool));

            var result = batch.TryAppend(timestamp, key, value, headers, pooledHeaderArray, completion);

            if (result.Success)
                return result;

            // Batch is full, complete it and queue for sending
            var readyBatch = batch.Complete();
            if (readyBatch is not null)
            {
                // Backpressure happens here: WriteAsync blocks when channel is full
                await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
            }

            // Atomically remove the completed batch only if it's still the same instance.
            // If another thread already replaced it, this is a no-op and we'll use their new batch.
            // This prevents the race where two threads both create new batches and one gets orphaned.
            _batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch));

            // Loop will call GetOrAdd again, which will either:
            // 1. Create a new batch (if we successfully removed the old one)
            // 2. Return the batch another thread already created (if they won the race)
        }
    }


    /// <summary>
    /// Gets batches that are ready to send.
    /// </summary>
    public IAsyncEnumerable<ReadyBatch> GetReadyBatchesAsync(CancellationToken cancellationToken)
    {
        return _readyBatches.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Checks for batches that have exceeded linger time.
    /// Uses conditional removal to avoid race conditions where a new batch might be created
    /// between Complete() and TryRemove() calls.
    /// </summary>
    public async ValueTask ExpireLingerAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var kvp in _batches)
        {
            var batch = kvp.Value;
            if (batch.ShouldFlush(now, _options.LingerMs))
            {
                var readyBatch = batch.Complete();
                if (readyBatch is not null)
                {
                    await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
                    // Only remove if the batch is still the same instance.
                    // If AppendAsync already replaced it with a new batch, don't remove the new one.
                    _batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(kvp.Key, batch));
                }
            }
        }
    }

    /// <summary>
    /// Flushes all batches.
    /// </summary>
    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        foreach (var kvp in _batches)
        {
            var batch = kvp.Value;
            var readyBatch = batch.Complete();
            if (readyBatch is not null)
            {
                await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
                // Only remove if the batch is still the same instance.
                // If AppendAsync already replaced it with a new batch, don't remove the new one.
                _batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(kvp.Key, batch));
            }
        }
    }

    /// <summary>
    /// Flushes all pending batches and completes the ready channel for graceful shutdown.
    /// The sender loop will process remaining batches and exit when the channel is empty.
    /// </summary>
    public async ValueTask CloseAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _closed)
            return;

        _closed = true;

        // Flush all pending batches to the ready channel
        await FlushAsync(cancellationToken).ConfigureAwait(false);

        // Complete the channel - sender will drain remaining batches and exit
        _readyBatches.Writer.Complete();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Fail all pending batches that haven't been sent yet
        var disposedException = new ObjectDisposedException(nameof(RecordAccumulator));

        foreach (var kvp in _batches)
        {
            var readyBatch = kvp.Value.Complete();
            readyBatch?.Fail(disposedException);
        }
        _batches.Clear();

        // Clear the TopicPartition cache to release memory
        _topicPartitionCache.Clear();

        // Complete the channel if not already closed by CloseAsync
        if (!_closed)
        {
            _readyBatches.Writer.Complete();
        }

        // Drain any batches that were in the channel but not yet processed
        while (_readyBatches.Reader.TryRead(out var batch))
        {
            batch.Fail(disposedException);
        }
    }
}

/// <summary>
/// A batch of records for a single partition.
/// Tracks pooled arrays that are returned when the batch completes.
/// Uses ArrayPool-backed arrays instead of List to eliminate allocations.
///
/// Thread-safety: Multiple threads can call TryAppend concurrently (via ConcurrentDictionary.AddOrUpdate),
/// so we use a lock to protect array mutations and field updates. The lock is per-partition, so there
/// is no cross-partition contention. Complete() uses Interlocked for lock-free idempotent completion.
/// </summary>
internal sealed class PartitionBatch
{
    // Initial capacity based on typical batch sizes - avoids array resizing
    private const int InitialRecordCapacity = 64;

    private readonly TopicPartition _topicPartition;
    private readonly ProducerOptions _options;
    private readonly Action<TaskCompletionSource<RecordMetadata>> _returnTcsToPool;

    // Zero-allocation array management: use pooled arrays instead of List<T>
    private Record[] _records;
    private int _recordCount;

    private TaskCompletionSource<RecordMetadata>[] _completionSources;
    private int _completionSourceCount;

    private byte[][] _pooledArrays;
    private int _pooledArrayCount;

    private RecordHeader[][] _pooledHeaderArrays;
    private int _pooledHeaderArrayCount;

    private readonly object _lock = new(); // Protects array mutations and field updates

    private long _baseTimestamp;
    private int _offsetDelta;
    private int _estimatedSize;
    private readonly DateTimeOffset _createdAt;
    private int _isCompleted; // 0 = not completed, 1 = completed (Interlocked guard for idempotent Complete)
    private ReadyBatch? _completedBatch; // Cached result to ensure Complete() is idempotent

    public PartitionBatch(TopicPartition topicPartition, ProducerOptions options, Action<TaskCompletionSource<RecordMetadata>> returnTcsToPool)
    {
        _topicPartition = topicPartition;
        _options = options;
        _returnTcsToPool = returnTcsToPool;
        _createdAt = DateTimeOffset.UtcNow;

        // Rent arrays from pool - eliminates List allocations
        _records = ArrayPool<Record>.Shared.Rent(InitialRecordCapacity);
        _recordCount = 0;

        _completionSources = ArrayPool<TaskCompletionSource<RecordMetadata>>.Shared.Rent(InitialRecordCapacity);
        _completionSourceCount = 0;

        _pooledArrays = ArrayPool<byte[]>.Shared.Rent(InitialRecordCapacity * 2);
        _pooledArrayCount = 0;

        _pooledHeaderArrays = ArrayPool<RecordHeader[]>.Shared.Rent(8); // Headers less common
        _pooledHeaderArrayCount = 0;
    }

    public TopicPartition TopicPartition => _topicPartition;
    public int RecordCount => _records.Count;
    public int EstimatedSize => _estimatedSize;

    public RecordAppendResult TryAppend(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
        TaskCompletionSource<RecordMetadata> completion)
    {
        // Lock required: Multiple threads can call TryAppend concurrently via ConcurrentDictionary.AddOrUpdate.
        // List<T> is not thread-safe, so we must synchronize access to _records, _completionSources, etc.
        // This lock is per-partition, so there is no cross-partition contention.
        lock (_lock)
        {
            if (_recordCount == 0)
            {
                _baseTimestamp = timestamp;
            }

            // Estimate size
            var recordSize = EstimateRecordSize(key.Length, value.Length, headers);
            if (_estimatedSize + recordSize > _options.BatchSize && _recordCount > 0)
            {
                return new RecordAppendResult(false);
            }

            // Grow arrays if needed (rare - only happens if batch fills beyond initial capacity)
            if (_recordCount >= _records.Length)
            {
                GrowArray(ref _records, ref _recordCount, ArrayPool<Record>.Shared);
            }
            if (_completionSourceCount >= _completionSources.Length)
            {
                GrowArray(ref _completionSources, ref _completionSourceCount, ArrayPool<TaskCompletionSource<RecordMetadata>>.Shared);
            }
            if (_pooledArrayCount + 2 >= _pooledArrays.Length) // +2 for key and value
            {
                GrowArray(ref _pooledArrays, ref _pooledArrayCount, ArrayPool<byte[]>.Shared);
            }
            if (pooledHeaderArray is not null && _pooledHeaderArrayCount >= _pooledHeaderArrays.Length)
            {
                GrowArray(ref _pooledHeaderArrays, ref _pooledHeaderArrayCount, ArrayPool<RecordHeader[]>.Shared);
            }

            // Track pooled arrays for returning to pool later
            if (key.Array is not null)
            {
                _pooledArrays[_pooledArrayCount++] = key.Array;
            }
            if (value.Array is not null)
            {
                _pooledArrays[_pooledArrayCount++] = value.Array;
            }
            if (pooledHeaderArray is not null)
            {
                _pooledHeaderArrays[_pooledHeaderArrayCount++] = pooledHeaderArray;
            }

            var timestampDelta = (int)(timestamp - _baseTimestamp);
            var record = new Record
            {
                TimestampDelta = timestampDelta,
                OffsetDelta = _offsetDelta,
                Key = key.Memory,
                IsKeyNull = key.IsNull,
                Value = value.Memory,
                IsValueNull = false,
                Headers = headers
            };

            _records[_recordCount++] = record;
            _estimatedSize += recordSize;

            // Use the passed-in completion source - no allocation here
            _completionSources[_completionSourceCount++] = completion;

            _offsetDelta++;

            return new RecordAppendResult(true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GrowArray<T>(ref T[] array, ref int count, ArrayPool<T> pool)
    {
        var newSize = array.Length * 2;
        var newArray = pool.Rent(newSize);
        Array.Copy(array, newArray, count);
        pool.Return(array, clearArray: true);
        array = newArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShouldFlush(DateTimeOffset now, int lingerMs)
    {
        lock (_lock)
        {
            if (_recordCount == 0)
                return false;

            return (now - _createdAt).TotalMilliseconds >= lingerMs;
        }
    }

    public ReadyBatch? Complete()
    {
        // Atomically mark as completed - only first caller proceeds
        if (Interlocked.Exchange(ref _isCompleted, 1) != 0)
        {
            // Idempotency: If already completed, return the cached batch
            // This prevents creating multiple ReadyBatch objects with duplicate pooled arrays.
            return _completedBatch;
        }

        if (_recordCount == 0)
        {
            // Empty batch - return arrays to pool immediately
            ReturnBatchArraysToPool();
            return null;
        }

        // Create a copy of records array with exact size for RecordBatch
        // RecordBatch needs IReadOnlyList<Record> but we need to return our pooled array
        var recordsCopy = new Record[_recordCount];
        Array.Copy(_records, recordsCopy, _recordCount);

        // Create copies of completion sources and pooled arrays with exact sizes
        // ReadyBatch will own these and manage their lifecycle
        var completionSourcesCopy = new TaskCompletionSource<RecordMetadata>[_completionSourceCount];
        Array.Copy(_completionSources, completionSourcesCopy, _completionSourceCount);

        var pooledArraysCopy = new byte[_pooledArrayCount][];
        Array.Copy(_pooledArrays, pooledArraysCopy, _pooledArrayCount);

        var pooledHeaderArraysCopy = new RecordHeader[_pooledHeaderArrayCount][];
        Array.Copy(_pooledHeaderArrays, pooledHeaderArraysCopy, _pooledHeaderArrayCount);

        // Return our working arrays to pool - we've copied the data we need
        ReturnBatchArraysToPool();

        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = _baseTimestamp,
            MaxTimestamp = _baseTimestamp + (_recordCount > 0 ? recordsCopy[_recordCount - 1].TimestampDelta : 0),
            LastOffsetDelta = _recordCount - 1,
            Records = recordsCopy
        };

        // Pass the copied arrays - ReadyBatch now owns them
        _completedBatch = new ReadyBatch(
            _topicPartition,
            batch,
            completionSourcesCopy,
            pooledArraysCopy,
            pooledHeaderArraysCopy,
            _returnTcsToPool);

        return _completedBatch;
    }

    private void ReturnBatchArraysToPool()
    {
        // Return the working arrays to pool
        ArrayPool<Record>.Shared.Return(_records, clearArray: true);
        ArrayPool<TaskCompletionSource<RecordMetadata>>.Shared.Return(_completionSources, clearArray: true);
        ArrayPool<byte[]>.Shared.Return(_pooledArrays, clearArray: true);
        ArrayPool<RecordHeader[]>.Shared.Return(_pooledHeaderArrays, clearArray: true);

        // Null out references to prevent accidental reuse
        _records = null!;
        _completionSources = null!;
        _pooledArrays = null!;
        _pooledHeaderArrays = null!;
    }

    private static int EstimateRecordSize(int keyLength, int valueLength, IReadOnlyList<RecordHeader>? headers)
    {
        var size = 20; // Base overhead for varint lengths, timestamp delta, offset delta, etc.
        size += keyLength;
        size += valueLength;

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                size += header.Key.Length + (header.IsValueNull ? 0 : header.Value.Length) + 10;
            }
        }

        return size;
    }
}

/// <summary>
/// Result of appending a record.
/// </summary>
public readonly record struct RecordAppendResult(bool Success);

/// <summary>
/// A batch ready to be sent.
/// Returns pooled arrays to ArrayPool when complete.
/// </summary>
public sealed class ReadyBatch
{
    public TopicPartition TopicPartition { get; }
    public RecordBatch RecordBatch { get; }
    public IReadOnlyList<TaskCompletionSource<RecordMetadata>> CompletionSources { get; }
    private readonly IReadOnlyList<byte[]> _pooledArrays;
    private readonly IReadOnlyList<RecordHeader[]> _pooledHeaderArrays;
    private readonly Action<TaskCompletionSource<RecordMetadata>> _returnTcsToPool;
    private int _cleanedUp; // 0 = not cleaned, 1 = cleaned (prevents double-cleanup)

    public ReadyBatch(
        TopicPartition topicPartition,
        RecordBatch recordBatch,
        IReadOnlyList<TaskCompletionSource<RecordMetadata>> completionSources,
        IReadOnlyList<byte[]> pooledArrays,
        IReadOnlyList<RecordHeader[]> pooledHeaderArrays,
        Action<TaskCompletionSource<RecordMetadata>> returnTcsToPool)
    {
        TopicPartition = topicPartition;
        RecordBatch = recordBatch;
        CompletionSources = completionSources;
        _pooledArrays = pooledArrays;
        _pooledHeaderArrays = pooledHeaderArrays;
        _returnTcsToPool = returnTcsToPool;
    }

    public void Complete(long baseOffset, DateTimeOffset timestamp)
    {
        try
        {
            for (var i = 0; i < CompletionSources.Count; i++)
            {
                var tcs = CompletionSources[i];
                tcs.TrySetResult(new RecordMetadata
                {
                    Topic = TopicPartition.Topic,
                    Partition = TopicPartition.Partition,
                    Offset = baseOffset + i,
                    Timestamp = timestamp
                });

                // Return TCS to pool after completion
                _returnTcsToPool(tcs);
            }
        }
        finally
        {
            Cleanup();
        }
    }

    public void Fail(Exception exception)
    {
        try
        {
            foreach (var tcs in CompletionSources)
            {
                tcs.TrySetException(exception);

                // Return TCS to pool after failure
                _returnTcsToPool(tcs);
            }
        }
        finally
        {
            Cleanup();
        }
    }

    private void Cleanup()
    {
        // Guard against double-cleanup: If cleanup has already been performed, return immediately.
        // This prevents double-return of pooled arrays which would corrupt ArrayPool.
        if (Interlocked.Exchange(ref _cleanedUp, 1) != 0)
            return;

        // Return pooled byte arrays (key/value data)
        foreach (var array in _pooledArrays)
        {
            ArrayPool<byte>.Shared.Return(array, clearArray: true);
        }

        // Return pooled header arrays (large header counts)
        foreach (var array in _pooledHeaderArrays)
        {
            ArrayPool<RecordHeader>.Shared.Return(array, clearArray: true);
        }
    }
}
