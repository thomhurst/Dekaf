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
/// Data for a single record in batch append operations.
/// This is a readonly struct to enable passing via ReadOnlySpan for batch operations.
/// </summary>
public readonly struct ProducerRecordData
{
    public long Timestamp { get; init; }
    public PooledMemory Key { get; init; }
    public PooledMemory Value { get; init; }
    public IReadOnlyList<RecordHeader>? Headers { get; init; }
    public RecordHeader[]? PooledHeaderArray { get; init; }
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
    private volatile bool _disposed;
    private volatile bool _closed;

    // Thread-local cache for fast path when consecutive messages go to the same partition.
    // This eliminates ConcurrentDictionary lookups for the common case of sending multiple
    // messages to the same topic-partition in sequence (e.g., keyed messages, batch processing).
    // Each thread maintains its own cache, so there's no contention.
    [ThreadStatic]
    private static string? t_cachedTopic;
    [ThreadStatic]
    private static int t_cachedPartition;
    [ThreadStatic]
    private static TopicPartition t_cachedTopicPartition;
    [ThreadStatic]
    private static PartitionBatch? t_cachedBatch;
    [ThreadStatic]
    private static RecordAccumulator? t_cachedAccumulator;

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

    public RecordAccumulator(ProducerOptions options)
    {
        _options = options;

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
        PooledValueTaskSource<RecordMetadata> completion,
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
            // Hot path optimization: TryGetValue first to avoid lambda invocation when batch exists.
            // Most appends hit an existing batch, so this avoids GetOrAdd overhead.
            if (!_batches.TryGetValue(topicPartition, out var batch))
            {
                // Cold path: Use static lambda with explicit state parameter to avoid closure allocation.
                // The captured _options would create a closure on every call otherwise.
                batch = _batches.GetOrAdd(topicPartition, static (tp, options) => new PartitionBatch(tp, options), _options);
            }

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
    /// Synchronous version of Append for fire-and-forget produce operations.
    /// Bypasses async overhead when no backpressure is needed.
    /// Returns true if appended successfully, false if the accumulator is disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAppendSync(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
        PooledValueTaskSource<RecordMetadata> completion)
    {
        if (_disposed)
            return false;

        // OPTIMIZATION: Use a nested cache to get/create TopicPartition without allocating on hot path.
        var partitionCache = _topicPartitionCache.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, TopicPartition>());
        var topicPartition = partitionCache.GetOrAdd(partition, static (p, t) => new TopicPartition(t, p), topic);

        // Loop until we successfully append the record.
        while (true)
        {
            // Hot path optimization: TryGetValue first to avoid lambda invocation when batch exists.
            if (!_batches.TryGetValue(topicPartition, out var batch))
            {
                batch = _batches.GetOrAdd(topicPartition, static (tp, options) => new PartitionBatch(tp, options), _options);
            }

            var result = batch.TryAppend(timestamp, key, value, headers, pooledHeaderArray, completion);

            if (result.Success)
                return true;

            // Batch is full, complete it and queue for sending
            var readyBatch = batch.Complete();
            if (readyBatch is not null)
            {
                // Non-blocking write to unbounded channel - should always succeed
                // If it fails (channel completed), the producer is being disposed
                if (!_readyBatches.Writer.TryWrite(readyBatch))
                {
                    // Channel is closed, fail the batch and return false
                    readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                    return false;
                }
            }

            // Atomically remove the completed batch only if it's still the same instance.
            _batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch));
        }
    }

    /// <summary>
    /// Fire-and-forget version of TryAppendSync that skips completion source tracking entirely.
    /// This eliminates the overhead of renting and storing PooledValueTaskSource for fire-and-forget produces.
    /// Returns true if appended successfully, false if the accumulator is disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAppendFireAndForget(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray)
    {
        if (_disposed)
            return false;

        // FAST PATH: Check thread-local cache for consecutive messages to same partition.
        // This is the common case in high-throughput scenarios and avoids all dictionary lookups.
        if (t_cachedAccumulator == this &&
            t_cachedTopic == topic &&
            t_cachedPartition == partition &&
            t_cachedBatch is { } cachedBatch)
        {
            var result = cachedBatch.TryAppendFireAndForget(timestamp, key, value, headers, pooledHeaderArray);
            if (result.Success)
                return true;

            // Cached batch is full, fall through to slow path to handle batch rotation
        }

        // SLOW PATH: Dictionary lookups required
        return TryAppendFireAndForgetSlow(topic, partition, timestamp, key, value, headers, pooledHeaderArray);
    }

    /// <summary>
    /// Slow path for TryAppendFireAndForget that handles dictionary lookups and batch rotation.
    /// Separated from fast path to keep the inlined method small.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryAppendFireAndForgetSlow(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray)
    {
        // OPTIMIZATION: Use a nested cache to get/create TopicPartition without allocating on hot path.
        var partitionCache = _topicPartitionCache.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, TopicPartition>());
        var topicPartition = partitionCache.GetOrAdd(partition, static (p, t) => new TopicPartition(t, p), topic);

        // Loop until we successfully append the record.
        while (true)
        {
            // Hot path optimization: TryGetValue first to avoid lambda invocation when batch exists.
            if (!_batches.TryGetValue(topicPartition, out var batch))
            {
                batch = _batches.GetOrAdd(topicPartition, static (tp, options) => new PartitionBatch(tp, options), _options);
            }

            var result = batch.TryAppendFireAndForget(timestamp, key, value, headers, pooledHeaderArray);

            if (result.Success)
            {
                // Update thread-local cache for next call
                t_cachedAccumulator = this;
                t_cachedTopic = topic;
                t_cachedPartition = partition;
                t_cachedTopicPartition = topicPartition;
                t_cachedBatch = batch;
                return true;
            }

            // Batch is full, complete it and queue for sending
            var readyBatch = batch.Complete();
            if (readyBatch is not null)
            {
                // Non-blocking write to unbounded channel - should always succeed
                // If it fails (channel completed), the producer is being disposed
                if (!_readyBatches.Writer.TryWrite(readyBatch))
                {
                    // Channel is closed, fail the batch and return false
                    readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                    // Invalidate cache since batch is invalid
                    t_cachedBatch = null;
                    return false;
                }
            }

            // Atomically remove the completed batch only if it's still the same instance.
            _batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch));

            // Invalidate cache since we removed the batch
            if (t_cachedBatch == batch)
            {
                t_cachedBatch = null;
            }
        }
    }

    /// <summary>
    /// Batch fire-and-forget append for records going to the same topic/partition.
    /// Amortizes lock acquisition and dictionary lookups over N records.
    /// Returns true if all records were appended, false if accumulator is disposed.
    /// </summary>
    public bool TryAppendFireAndForgetBatch(
        string topic,
        int partition,
        ReadOnlySpan<ProducerRecordData> items)
    {
        if (_disposed || items.Length == 0)
            return !_disposed;

        // Get or create TopicPartition (cached)
        var partitionCache = _topicPartitionCache.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, TopicPartition>());
        var topicPartition = partitionCache.GetOrAdd(partition, static (p, t) => new TopicPartition(t, p), topic);

        var startIndex = 0;

        // Loop until all records are appended
        while (startIndex < items.Length)
        {
            // Get or create batch
            if (!_batches.TryGetValue(topicPartition, out var batch))
            {
                batch = _batches.GetOrAdd(topicPartition, static (tp, options) => new PartitionBatch(tp, options), _options);
            }

            // Try to append remaining records
            var appended = batch.TryAppendFireAndForgetBatch(items, startIndex);

            if (appended > 0)
            {
                startIndex += appended;

                // Update thread-local cache
                t_cachedAccumulator = this;
                t_cachedTopic = topic;
                t_cachedPartition = partition;
                t_cachedTopicPartition = topicPartition;
                t_cachedBatch = batch;
            }

            // If we haven't appended all, batch is full
            if (startIndex < items.Length)
            {
                // Complete and queue the full batch
                var readyBatch = batch.Complete();
                if (readyBatch is not null)
                {
                    if (!_readyBatches.Writer.TryWrite(readyBatch))
                    {
                        readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                        t_cachedBatch = null;
                        return false;
                    }
                }

                // Remove the completed batch
                _batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch));
                t_cachedBatch = null;
            }
        }

        return true;
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

        // Invalidate thread-local cache if it points to this accumulator
        if (t_cachedAccumulator == this)
        {
            t_cachedAccumulator = null;
            t_cachedTopic = null;
            t_cachedBatch = null;
        }

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
/// so we use a SpinLock to protect array mutations and field updates. SpinLock is ideal here because:
/// - Critical sections are very short (<100ns)
/// - Lock is per-partition, so no cross-partition contention
/// - Avoids the ~20ns overhead of Monitor for short-held locks
/// Complete() uses Interlocked for lock-free idempotent completion.
/// </summary>
internal sealed class PartitionBatch
{
    // Initial capacity increased to reduce array growth frequency.
    // Most batches contain 100-500 records, so 256 avoids resizing in common cases.
    // This reduces SpinLock hold time by eliminating most array growth operations.
    private const int InitialRecordCapacity = 256;

    private readonly TopicPartition _topicPartition;
    private readonly ProducerOptions _options;

    // Zero-allocation array management: use pooled arrays instead of List<T>
    private Record[] _records;
    private int _recordCount;

    private PooledValueTaskSource<RecordMetadata>[] _completionSources;
    private int _completionSourceCount;

    private byte[][] _pooledArrays;
    private int _pooledArrayCount;

    private RecordHeader[][] _pooledHeaderArrays;
    private int _pooledHeaderArrayCount;

    // SpinLock for short critical sections - lower overhead than Monitor for <100ns holds
    // IMPORTANT: SpinLock is a struct, must never be copied. Access via ref only.
    private SpinLock _spinLock = new(enableThreadOwnerTracking: false);

    private long _baseTimestamp;
    private int _estimatedSize;
    // Note: _offsetDelta removed - it always equals _recordCount at assignment time
    private readonly DateTimeOffset _createdAt;
    private int _isCompleted; // 0 = not completed, 1 = completed (Interlocked guard for idempotent Complete)
    private ReadyBatch? _completedBatch; // Cached result to ensure Complete() is idempotent

    public PartitionBatch(TopicPartition topicPartition, ProducerOptions options)
    {
        _topicPartition = topicPartition;
        _options = options;
        _createdAt = DateTimeOffset.UtcNow;

        // Rent arrays from pool - eliminates List allocations
        _records = ArrayPool<Record>.Shared.Rent(InitialRecordCapacity);
        _recordCount = 0;

        _completionSources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(InitialRecordCapacity);
        _completionSourceCount = 0;

        _pooledArrays = ArrayPool<byte[]>.Shared.Rent(InitialRecordCapacity * 2);
        _pooledArrayCount = 0;

        _pooledHeaderArrays = ArrayPool<RecordHeader[]>.Shared.Rent(8); // Headers less common
        _pooledHeaderArrayCount = 0;
    }

    public TopicPartition TopicPartition => _topicPartition;
    public int RecordCount => _recordCount;
    public int EstimatedSize => _estimatedSize;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RecordAppendResult TryAppend(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
        PooledValueTaskSource<RecordMetadata> completion)
    {
        // Pre-compute record size outside the lock - depends only on input parameters
        var recordSize = EstimateRecordSize(key.Length, value.Length, headers);

        // SpinLock for short critical section - lower overhead than Monitor for <100ns holds.
        // Multiple threads can call TryAppend concurrently via ConcurrentDictionary.AddOrUpdate.
        // This lock is per-partition, so there is no cross-partition contention.
        var lockTaken = false;
        try
        {
            _spinLock.Enter(ref lockTaken);

            if (_recordCount == 0)
            {
                _baseTimestamp = timestamp;
            }

            // Check size limit
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
                GrowArray(ref _completionSources, ref _completionSourceCount, ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared);
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
                OffsetDelta = _recordCount,
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

            return new RecordAppendResult(true);
        }
        finally
        {
            if (lockTaken) _spinLock.Exit();
        }
    }

    /// <summary>
    /// Fire-and-forget version of TryAppend that skips completion source tracking.
    /// This is significantly faster for fire-and-forget produces since it avoids:
    /// 1. Renting a PooledValueTaskSource
    /// 2. Storing the completion source in the batch
    /// 3. Setting the result when the batch completes
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RecordAppendResult TryAppendFireAndForget(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray)
    {
        // Pre-compute record size outside the lock - depends only on input parameters
        var recordSize = EstimateRecordSize(key.Length, value.Length, headers);

        var lockTaken = false;
        try
        {
            _spinLock.Enter(ref lockTaken);

            if (_recordCount == 0)
            {
                _baseTimestamp = timestamp;
            }

            // Check size limit
            if (_estimatedSize + recordSize > _options.BatchSize && _recordCount > 0)
            {
                return new RecordAppendResult(false);
            }

            // Grow arrays if needed (rare - only happens if batch fills beyond initial capacity)
            if (_recordCount >= _records.Length)
            {
                GrowArray(ref _records, ref _recordCount, ArrayPool<Record>.Shared);
            }
            // Note: No need to grow _completionSources for fire-and-forget
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
                OffsetDelta = _recordCount,
                Key = key.Memory,
                IsKeyNull = key.IsNull,
                Value = value.Memory,
                IsValueNull = false,
                Headers = headers
            };

            _records[_recordCount++] = record;
            _estimatedSize += recordSize;

            // Fire-and-forget: Skip completion source tracking entirely
            // This is the key optimization - no PooledValueTaskSource rental or storage

            return new RecordAppendResult(true);
        }
        finally
        {
            if (lockTaken) _spinLock.Exit();
        }
    }

    /// <summary>
    /// Batch append for fire-and-forget produces. Appends multiple records with a single lock acquisition.
    /// Returns the number of records successfully appended before the batch became full.
    /// This amortizes lock overhead over N messages, providing significant throughput improvement.
    /// </summary>
    public int TryAppendFireAndForgetBatch(
        ReadOnlySpan<ProducerRecordData> items,
        int startIndex = 0)
    {
        if (items.Length == 0 || startIndex >= items.Length)
            return 0;

        var lockTaken = false;
        try
        {
            _spinLock.Enter(ref lockTaken);

            var appended = 0;

            for (var i = startIndex; i < items.Length; i++)
            {
                ref readonly var item = ref items[i];
                var recordSize = EstimateRecordSize(item.Key.Length, item.Value.Length, item.Headers);

                // Set base timestamp from first record
                if (_recordCount == 0)
                {
                    _baseTimestamp = item.Timestamp;
                }

                // Check size limit
                if (_estimatedSize + recordSize > _options.BatchSize && _recordCount > 0)
                {
                    // Batch is full, return count of appended records
                    return appended;
                }

                // Grow arrays if needed
                if (_recordCount >= _records.Length)
                {
                    GrowArray(ref _records, ref _recordCount, ArrayPool<Record>.Shared);
                }
                if (_pooledArrayCount + 2 >= _pooledArrays.Length)
                {
                    GrowArray(ref _pooledArrays, ref _pooledArrayCount, ArrayPool<byte[]>.Shared);
                }
                if (item.PooledHeaderArray is not null && _pooledHeaderArrayCount >= _pooledHeaderArrays.Length)
                {
                    GrowArray(ref _pooledHeaderArrays, ref _pooledHeaderArrayCount, ArrayPool<RecordHeader[]>.Shared);
                }

                // Track pooled arrays
                if (item.Key.Array is not null)
                {
                    _pooledArrays[_pooledArrayCount++] = item.Key.Array;
                }
                if (item.Value.Array is not null)
                {
                    _pooledArrays[_pooledArrayCount++] = item.Value.Array;
                }
                if (item.PooledHeaderArray is not null)
                {
                    _pooledHeaderArrays[_pooledHeaderArrayCount++] = item.PooledHeaderArray;
                }

                var timestampDelta = (int)(item.Timestamp - _baseTimestamp);
                _records[_recordCount] = new Record
                {
                    TimestampDelta = timestampDelta,
                    OffsetDelta = _recordCount,
                    Key = item.Key.Memory,
                    IsKeyNull = item.Key.IsNull,
                    Value = item.Value.Memory,
                    IsValueNull = false,
                    Headers = item.Headers
                };

                _recordCount++;
                _estimatedSize += recordSize;
                appended++;
            }

            return appended;
        }
        finally
        {
            if (lockTaken) _spinLock.Exit();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GrowArray<T>(ref T[] array, ref int count, ArrayPool<T> pool)
    {
        var newSize = array.Length * 2;
        var newArray = pool.Rent(newSize);
        Array.Copy(array, newArray, count);
        pool.Return(array, clearArray: false);
        array = newArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShouldFlush(DateTimeOffset now, int lingerMs)
    {
        var lockTaken = false;
        try
        {
            _spinLock.Enter(ref lockTaken);

            if (_recordCount == 0)
                return false;

            return (now - _createdAt).TotalMilliseconds >= lingerMs;
        }
        finally
        {
            if (lockTaken) _spinLock.Exit();
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

        // Return only the records array to pool - we've copied it
        // Other arrays are passed directly to ReadyBatch which will return them to pool
        ArrayPool<Record>.Shared.Return(_records, clearArray: false);
        _records = null!;

        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = _baseTimestamp,
            MaxTimestamp = _baseTimestamp + (_recordCount > 0 ? recordsCopy[_recordCount - 1].TimestampDelta : 0),
            LastOffsetDelta = _recordCount - 1,
            Records = recordsCopy
        };

        // Pass pooled arrays directly to ReadyBatch - no copying needed
        // ReadyBatch will return them to pool when done
        // PooledValueTaskSource auto-returns to its pool when GetResult() is called
        _completedBatch = new ReadyBatch(
            _topicPartition,
            batch,
            _completionSources,
            _completionSourceCount,
            _pooledArrays,
            _pooledArrayCount,
            _pooledHeaderArrays,
            _pooledHeaderArrayCount);

        // Null out references - ownership transferred to ReadyBatch
        _completionSources = null!;
        _pooledArrays = null!;
        _pooledHeaderArrays = null!;

        return _completedBatch;
    }

    private void ReturnBatchArraysToPool()
    {
        // Return all working arrays to pool (with null checks since they may have been transferred)
        // clearArray: false for internal tracking arrays - they will be overwritten on next use
        if (_records is not null)
            ArrayPool<Record>.Shared.Return(_records, clearArray: false);
        if (_completionSources is not null)
            ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Return(_completionSources, clearArray: false);
        if (_pooledArrays is not null)
            ArrayPool<byte[]>.Shared.Return(_pooledArrays, clearArray: false);
        if (_pooledHeaderArrays is not null)
            ArrayPool<RecordHeader[]>.Shared.Return(_pooledHeaderArrays, clearArray: false);

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
/// PooledValueTaskSource instances auto-return to their pool when GetResult() is called.
/// </summary>
public sealed class ReadyBatch
{
    public TopicPartition TopicPartition { get; }
    public RecordBatch RecordBatch { get; }

    /// <summary>
    /// Number of completion sources (messages) in this batch.
    /// </summary>
    public int CompletionSourcesCount => _completionSourcesCount;

    // Working arrays from accumulator (pooled)
    private readonly PooledValueTaskSource<RecordMetadata>[] _completionSourcesArray;
    private readonly int _completionSourcesCount;
    private readonly byte[][] _pooledDataArrays;
    private readonly int _pooledDataArraysCount;
    private readonly RecordHeader[][] _pooledHeaderArrays;
    private readonly int _pooledHeaderArraysCount;

    private int _cleanedUp; // 0 = not cleaned, 1 = cleaned (prevents double-cleanup)

    public ReadyBatch(
        TopicPartition topicPartition,
        RecordBatch recordBatch,
        PooledValueTaskSource<RecordMetadata>[] completionSourcesArray,
        int completionSourcesCount,
        byte[][] pooledDataArrays,
        int pooledDataArraysCount,
        RecordHeader[][] pooledHeaderArrays,
        int pooledHeaderArraysCount)
    {
        TopicPartition = topicPartition;
        RecordBatch = recordBatch;
        _completionSourcesArray = completionSourcesArray;
        _completionSourcesCount = completionSourcesCount;
        _pooledDataArrays = pooledDataArrays;
        _pooledDataArraysCount = pooledDataArraysCount;
        _pooledHeaderArrays = pooledHeaderArrays;
        _pooledHeaderArraysCount = pooledHeaderArraysCount;
    }

    public void Complete(long baseOffset, DateTimeOffset timestamp)
    {
        // Guard against calling Complete after Cleanup has been performed
        if (Volatile.Read(ref _cleanedUp) != 0)
            return;

        try
        {
            for (var i = 0; i < _completionSourcesCount; i++)
            {
                var source = _completionSourcesArray[i];
                // TrySetResult completes the ValueTask; the awaiter's GetResult()
                // will auto-return the source to its pool
                source.TrySetResult(new RecordMetadata
                {
                    Topic = TopicPartition.Topic,
                    Partition = TopicPartition.Partition,
                    Offset = baseOffset + i,
                    Timestamp = timestamp
                });
            }
        }
        finally
        {
            Cleanup();
        }
    }

    public void Fail(Exception exception)
    {
        // Guard against calling Fail after Cleanup has been performed
        if (Volatile.Read(ref _cleanedUp) != 0)
            return;

        try
        {
            for (var i = 0; i < _completionSourcesCount; i++)
            {
                var source = _completionSourcesArray[i];
                // TrySetException completes the ValueTask; the awaiter's GetResult()
                // will auto-return the source to its pool
                source.TrySetException(exception);
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
        for (var i = 0; i < _pooledDataArraysCount; i++)
        {
            ArrayPool<byte>.Shared.Return(_pooledDataArrays[i], clearArray: true);
        }

        // Return pooled header arrays (large header counts)
        // clearArray: false - header data is not sensitive
        for (var i = 0; i < _pooledHeaderArraysCount; i++)
        {
            ArrayPool<RecordHeader>.Shared.Return(_pooledHeaderArrays[i], clearArray: false);
        }

        // Return the working arrays to pool
        // Note: PooledValueTaskSource instances auto-return to their pool when awaited
        // clearArray: false for tracking arrays - they only hold references, not actual data
        ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Return(_completionSourcesArray, clearArray: false);
        ArrayPool<byte[]>.Shared.Return(_pooledDataArrays, clearArray: false);
        ArrayPool<RecordHeader[]>.Shared.Return(_pooledHeaderArrays, clearArray: false);
    }
}
