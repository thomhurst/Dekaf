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
/// <remarks>
/// <para>
/// <b>Ownership Semantics:</b> When passed to <see cref="RecordAccumulator.TryAppendFireAndForgetBatch"/>,
/// ownership of pooled resources (Key.Array, Value.Array, PooledHeaderArray) transfers to the accumulator
/// for successfully appended records. The accumulator will return these arrays to their pools when the
/// batch completes or fails.
/// </para>
/// <para>
/// <b>Partial Failure:</b> If the batch operation fails partway through (e.g., accumulator disposed),
/// the caller is responsible for returning pooled resources for records that were NOT appended.
/// The return value indicates how many records were successfully appended.
/// </para>
/// </remarks>
public readonly struct ProducerRecordData
{
    public long Timestamp { get; init; }
    public PooledMemory Key { get; init; }
    public PooledMemory Value { get; init; }
    public IReadOnlyList<RecordHeader>? Headers { get; init; }
    public RecordHeader[]? PooledHeaderArray { get; init; }
}

/// <summary>
/// Arena allocator for batch message data. Pre-allocates a contiguous buffer
/// and provides slices for direct serialization, eliminating per-message ArrayPool rentals.
/// </summary>
/// <remarks>
/// <para>
/// Instead of renting an array for each key/value, the arena provides a single large buffer.
/// Messages are serialized directly into the arena, and only an offset+length are stored.
/// When the batch completes, the entire arena buffer is returned to the pool at once.
/// </para>
/// <para>
/// This reduces per-message allocations from 2 (key + value) to 0, significantly
/// reducing GC pressure in high-throughput scenarios.
/// </para>
/// </remarks>
internal sealed class BatchArena
{
    private byte[] _buffer;
    private int _position;

    /// <summary>
    /// Creates a new arena with the specified capacity.
    /// The arena does not grow - when full, the batch should be rotated.
    /// </summary>
    /// <param name="capacity">Buffer size (will be rented from ArrayPool).</param>
    public BatchArena(int capacity)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(capacity);
        _position = 0;
    }

    /// <summary>
    /// Gets the current position in the arena (total bytes used).
    /// </summary>
    public int Position => _position;

    /// <summary>
    /// Gets the remaining capacity in the current buffer.
    /// </summary>
    public int RemainingCapacity => _buffer.Length - _position;

    /// <summary>
    /// Tries to allocate space in the arena and returns a span for writing.
    /// </summary>
    /// <param name="size">Number of bytes needed.</param>
    /// <param name="span">Output span to write to.</param>
    /// <param name="offset">Output offset where the allocation starts.</param>
    /// <returns>True if allocation succeeded, false if arena is full.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAllocate(int size, out Span<byte> span, out int offset)
    {
        var currentPos = _position;
        var newPos = currentPos + size;

        if (newPos <= _buffer.Length)
        {
            offset = currentPos;
            span = _buffer.AsSpan(currentPos, size);
            _position = newPos;
            return true;
        }

        // Need to grow - try to accommodate
        return TryAllocateSlow(size, out span, out offset);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryAllocateSlow(int size, out Span<byte> span, out int offset)
    {
        // Don't grow - just return false and let caller rotate batch
        // Growing causes extra allocations; rotating batch is cleaner
        span = default;
        offset = 0;
        return false;
    }

    /// <summary>
    /// Gets a read-only span for data at the specified offset and length.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetSpan(int offset, int length)
    {
        return _buffer.AsSpan(offset, length);
    }

    /// <summary>
    /// Gets the underlying buffer for protocol encoding.
    /// </summary>
    public byte[] Buffer => _buffer;

    /// <summary>
    /// Returns the buffer to the ArrayPool.
    /// </summary>
    public void Return()
    {
        if (_buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
            _buffer = null!;
        }
    }
}

/// <summary>
/// Lightweight reference to data within a BatchArena.
/// Replaces PooledMemory for arena-managed data, eliminating per-message allocations.
/// </summary>
internal readonly struct ArenaSlice
{
    public readonly int Offset;
    public readonly int Length;

    public ArenaSlice(int offset, int length)
    {
        Offset = offset;
        Length = length;
    }

    public bool IsEmpty => Length == 0;

    /// <summary>
    /// Gets the data from the specified arena.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetSpan(BatchArena arena) => arena.GetSpan(Offset, Length);
}

/// <summary>
/// Buffer writer that writes directly to a BatchArena.
/// Implements IBufferWriter&lt;byte&gt; for use with existing serializers.
/// After writing, call Complete() to get an ArenaSlice referencing the written data.
/// </summary>
internal ref struct ArenaBufferWriter : IBufferWriter<byte>
{
    private readonly BatchArena _arena;
    private readonly int _startOffset;
    private int _written;
    private bool _failed;

    public ArenaBufferWriter(BatchArena arena)
    {
        _arena = arena;
        _startOffset = arena.Position;
        _written = 0;
        _failed = false;
    }

    /// <summary>
    /// Gets whether the write operation failed due to arena capacity.
    /// </summary>
    public readonly bool Failed => _failed;

    /// <summary>
    /// Gets the number of bytes written.
    /// </summary>
    public readonly int WrittenCount => _written;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        _written += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (sizeHint < 1) sizeHint = 256;

        if (!_arena.TryAllocate(sizeHint, out var span, out _))
        {
            _failed = true;
            return Memory<byte>.Empty;
        }

        // Return as Memory - arena buffer is stable until batch completes
        return _arena.Buffer.AsMemory(_arena.Position - sizeHint, sizeHint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        if (sizeHint < 1) sizeHint = 256;

        if (!_arena.TryAllocate(sizeHint, out var span, out _))
        {
            _failed = true;
            return Span<byte>.Empty;
        }

        return span;
    }

    /// <summary>
    /// Completes the write and returns an ArenaSlice referencing the written data.
    /// </summary>
    public readonly ArenaSlice Complete()
    {
        return new ArenaSlice(_startOffset, _written);
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

    // Multi-partition thread-local cache for scenarios where messages go to multiple partitions.
    // Uses a small fixed-size array indexed by partition modulo cache size.
    // This handles common scenarios (3-16 partitions) with near-100% cache hit rate.
    // Cache size of 16 covers most production scenarios while keeping memory footprint small.
    private const int MultiPartitionCacheSize = 16;

    [ThreadStatic]
    private static PartitionBatchCacheEntry[]? t_partitionBatchCache;
    [ThreadStatic]
    private static RecordAccumulator? t_partitionBatchCacheOwner;

    /// <summary>
    /// Entry in the multi-partition batch cache.
    /// Stores the topic, partition, and batch reference for quick lookup.
    /// </summary>
    private struct PartitionBatchCacheEntry
    {
        public string? Topic;
        public int Partition;
        public PartitionBatch? Batch;
    }

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

        // Use unbounded channel for ready batches to avoid artificial backpressure.
        // Natural backpressure occurs through:
        // 1. TCP flow control when the network can't keep up
        // 2. Broker responses being slow
        // 3. Batch size limits in PartitionBatch
        // This matches Confluent.Kafka's approach where queue.buffering.max.messages
        // defaults to 100,000 and rarely causes backpressure in practice.
        _readyBatches = Channel.CreateUnbounded<ReadyBatch>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Gets the partition cache for a topic (for TopicPartition allocation avoidance).
    /// Used by KafkaProducer for arena-based serialization path.
    /// </summary>
    internal ConcurrentDictionary<int, TopicPartition> GetTopicPartitionCache(string topic)
    {
        return _topicPartitionCache.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, TopicPartition>());
    }

    /// <summary>
    /// Tries to get an existing batch for the given topic-partition.
    /// Used by KafkaProducer to access the batch's arena for direct serialization.
    /// </summary>
    /// <param name="topicPartition">The topic-partition to look up.</param>
    /// <param name="batch">The batch if found, null otherwise.</param>
    /// <returns>True if a batch exists, false otherwise.</returns>
    internal bool TryGetBatch(TopicPartition topicPartition, out PartitionBatch? batch)
    {
        if (_disposed)
        {
            batch = null;
            return false;
        }

        return _batches.TryGetValue(topicPartition, out batch);
    }

    /// <summary>
    /// Tries to get an existing batch by topic and partition using thread-local cache.
    /// This is the fast path that avoids TopicPartition allocation.
    /// </summary>
    /// <param name="topic">The topic name.</param>
    /// <param name="partition">The partition number.</param>
    /// <param name="batch">The batch if found, null otherwise.</param>
    /// <returns>True if a batch exists, false otherwise.</returns>
    internal bool TryGetBatch(string topic, int partition, out PartitionBatch? batch)
    {
        if (_disposed)
        {
            batch = null;
            return false;
        }

        // Use thread-local multi-partition cache for fast lookup
        var cache = t_partitionBatchCache;
        var cacheOwner = t_partitionBatchCacheOwner;

        if (cache is not null && ReferenceEquals(cacheOwner, this))
        {
            var index = partition & (MultiPartitionCacheSize - 1);
            ref var entry = ref cache[index];

            if (entry.Topic == topic && entry.Partition == partition && entry.Batch is not null)
            {
                // Cache hit - verify batch is still valid (not completed)
                var cachedBatch = entry.Batch;
                if (cachedBatch.Arena is not null) // Arena is null when batch is completed
                {
                    batch = cachedBatch;
                    return true;
                }
                // Batch was completed, clear cache entry
                entry.Batch = null;
            }
        }

        // Cache miss - look up in dictionary
        if (!_topicPartitionCache.TryGetValue(topic, out var partitionCache))
        {
            batch = null;
            return false;
        }

        if (!partitionCache.TryGetValue(partition, out var topicPartition))
        {
            batch = null;
            return false;
        }

        if (!_batches.TryGetValue(topicPartition, out batch))
        {
            return false;
        }

        // Update thread-local cache for next time
        UpdatePartitionCache(topic, partition, batch);
        return true;
    }

    /// <summary>
    /// Updates the thread-local partition cache.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdatePartitionCache(string topic, int partition, PartitionBatch? batch)
    {
        var cache = t_partitionBatchCache;
        if (cache is null || !ReferenceEquals(t_partitionBatchCacheOwner, this))
        {
            cache = new PartitionBatchCacheEntry[MultiPartitionCacheSize];
            t_partitionBatchCache = cache;
            t_partitionBatchCacheOwner = this;
        }

        var index = partition & (MultiPartitionCacheSize - 1);
        cache[index] = new PartitionBatchCacheEntry
        {
            Topic = topic,
            Partition = partition,
            Batch = batch
        };
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
                // Try synchronous write first to avoid async state machine allocation
                if (!_readyBatches.Writer.TryWrite(readyBatch))
                {
                    // Backpressure happens here: WriteAsync blocks when channel is full
                    await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
                }
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

        // FAST PATH 1: Check single-partition cache for consecutive messages to same partition.
        // This is the fastest path for single-partition or sticky-partitioner scenarios.
        if (t_cachedAccumulator == this &&
            t_cachedTopic == topic &&
            t_cachedPartition == partition &&
            t_cachedBatch is { } cachedBatch)
        {
            var result = cachedBatch.TryAppendFireAndForget(timestamp, key, value, headers, pooledHeaderArray);
            if (result.Success)
                return true;

            // Cached batch is full - try fast-path rotation using cached TopicPartition
            if (t_cachedTopicPartition.Topic == topic && t_cachedTopicPartition.Partition == partition)
            {
                return TryRotateBatchFastPath(cachedBatch, t_cachedTopicPartition, topic, partition,
                    timestamp, key, value, headers, pooledHeaderArray);
            }
        }

        // FAST PATH 2: Check multi-partition cache for scenarios with multiple partitions.
        // This handles round-robin/default partitioning across multiple partitions efficiently.
        if (t_partitionBatchCacheOwner == this && t_partitionBatchCache is { } cache)
        {
            var cacheIndex = partition & (MultiPartitionCacheSize - 1); // Fast modulo for power of 2
            ref var entry = ref cache[cacheIndex];
            if (entry.Topic == topic && entry.Partition == partition && entry.Batch is { } mpCachedBatch)
            {
                var result = mpCachedBatch.TryAppendFireAndForget(timestamp, key, value, headers, pooledHeaderArray);
                if (result.Success)
                {
                    // Also update single-partition cache for potential consecutive hits
                    t_cachedAccumulator = this;
                    t_cachedTopic = topic;
                    t_cachedPartition = partition;
                    t_cachedBatch = mpCachedBatch;
                    return true;
                }
                // Batch is full, invalidate this cache entry and fall through
                entry.Batch = null;
            }
        }

        // SLOW PATH: Dictionary lookups required
        return TryAppendFireAndForgetSlow(topic, partition, timestamp, key, value, headers, pooledHeaderArray);
    }

    /// <summary>
    /// Fast-path batch rotation when we have cached TopicPartition.
    /// Avoids dictionary lookups for TopicPartition cache.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryRotateBatchFastPath(
        PartitionBatch oldBatch,
        TopicPartition topicPartition,
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray)
    {
        // Complete the old batch and queue it for sending
        var readyBatch = oldBatch.Complete();
        if (readyBatch is not null)
        {
            if (!_readyBatches.Writer.TryWrite(readyBatch))
            {
                readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                t_cachedBatch = null;
                return false;
            }
        }

        // Remove the old batch atomically
        _batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, oldBatch));

        // Create new batch and add to dictionary
        var newBatch = new PartitionBatch(topicPartition, _options);

        // Try to add the new batch - another thread might have added one already
        if (!_batches.TryAdd(topicPartition, newBatch))
        {
            // Another thread added a batch, get it and use it instead
            // Return our batch's arrays to pool
            newBatch.Complete(); // This will return arrays since batch is empty
            newBatch = _batches.GetOrAdd(topicPartition, static (tp, options) => new PartitionBatch(tp, options), _options);
        }

        // Append to the new batch
        var result = newBatch.TryAppendFireAndForget(timestamp, key, value, headers, pooledHeaderArray);

        if (result.Success)
        {
            // Update caches
            t_cachedBatch = newBatch;

            // Update multi-partition cache if initialized
            if (t_partitionBatchCacheOwner == this && t_partitionBatchCache is { } mpCache)
            {
                var cacheIndex = partition & (MultiPartitionCacheSize - 1);
                ref var entry = ref mpCache[cacheIndex];
                entry.Topic = topic;
                entry.Partition = partition;
                entry.Batch = newBatch;
            }

            return true;
        }

        // Batch rejected the append (shouldn't happen for fresh batch)
        t_cachedBatch = null;
        return false;
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

        // Initialize multi-partition cache if needed (one-time allocation per thread)
        var mpCache = t_partitionBatchCache;
        if (mpCache is null || t_partitionBatchCacheOwner != this)
        {
            mpCache = t_partitionBatchCache ??= new PartitionBatchCacheEntry[MultiPartitionCacheSize];
            t_partitionBatchCacheOwner = this;
            // Clear cache entries when switching accumulators
            Array.Clear(mpCache);
        }
        var cacheIndex = partition & (MultiPartitionCacheSize - 1);

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
                // Update single-partition cache for consecutive hits
                t_cachedAccumulator = this;
                t_cachedTopic = topic;
                t_cachedPartition = partition;
                t_cachedTopicPartition = topicPartition;
                t_cachedBatch = batch;

                // Update multi-partition cache for round-robin scenarios
                ref var entry = ref mpCache[cacheIndex];
                entry.Topic = topic;
                entry.Partition = partition;
                entry.Batch = batch;

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
                    // Invalidate caches
                    t_cachedBatch = null;
                    mpCache[cacheIndex].Batch = null;
                    return false;
                }
            }

            // Atomically remove the completed batch only if it's still the same instance.
            _batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch));

            // Invalidate caches since we removed the batch
            if (t_cachedBatch == batch)
            {
                t_cachedBatch = null;
            }
            if (mpCache[cacheIndex].Batch == batch)
            {
                mpCache[cacheIndex].Batch = null;
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
    internal IAsyncEnumerable<ReadyBatch> GetReadyBatchesAsync(CancellationToken cancellationToken)
    {
        return _readyBatches.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Checks for batches that have exceeded linger time.
    /// Uses conditional removal to avoid race conditions where a new batch might be created
    /// between Complete() and TryRemove() calls.
    /// </summary>
    /// <remarks>
    /// Optimized to avoid async state machine allocation when there are no batches to process.
    /// Also uses synchronous TryWrite when possible to avoid async overhead.
    /// </remarks>
    public ValueTask ExpireLingerAsync(CancellationToken cancellationToken)
    {
        // Fast path: no batches to check - avoid enumeration and async overhead entirely
        if (_batches.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        return ExpireLingerAsyncCore(cancellationToken);
    }

    private async ValueTask ExpireLingerAsyncCore(CancellationToken cancellationToken)
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
                    // Try synchronous write first to avoid async state machine allocation
                    if (!_readyBatches.Writer.TryWrite(readyBatch))
                    {
                        // Channel is bounded or busy, fall back to async
                        await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
                    }
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
    /// <remarks>
    /// Optimized to avoid async state machine allocation when there are no batches to flush.
    /// </remarks>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        // Fast path: no batches to flush - avoid enumeration and async overhead entirely
        if (_batches.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        return FlushAsyncCore(cancellationToken);
    }

    private async ValueTask FlushAsyncCore(CancellationToken cancellationToken)
    {
        foreach (var kvp in _batches)
        {
            var batch = kvp.Value;
            var readyBatch = batch.Complete();
            if (readyBatch is not null)
            {
                // Try synchronous write first to avoid async state machine allocation
                if (!_readyBatches.Writer.TryWrite(readyBatch))
                {
                    // Channel is bounded or busy, fall back to async
                    await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
                }
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

        // Invalidate thread-local caches if they point to this accumulator
        if (t_cachedAccumulator == this)
        {
            t_cachedAccumulator = null;
            t_cachedTopic = null;
            t_cachedBatch = null;
        }
        if (t_partitionBatchCacheOwner == this)
        {
            t_partitionBatchCacheOwner = null;
            if (t_partitionBatchCache is { } cache)
            {
                Array.Clear(cache);
            }
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

    // Arena for zero-copy serialization - all message data in one contiguous buffer
    private BatchArena? _arena;

    // Zero-allocation array management: use pooled arrays instead of List<T>
    private Record[] _records;
    private int _recordCount;

    private PooledValueTaskSource<RecordMetadata>[] _completionSources;
    private int _completionSourceCount;

    // Legacy: pooled arrays for non-arena path (completion-tracked messages)
    private byte[][] _pooledArrays;
    private int _pooledArrayCount;

    private RecordHeader[][] _pooledHeaderArrays;
    private int _pooledHeaderArrayCount;

    // SpinLock for short critical sections - lower overhead than Monitor for <100ns holds
    // IMPORTANT: SpinLock is a struct, must never be copied. Access via ref only.
    private SpinLock _spinLock = new(enableThreadOwnerTracking: false);

    // Exclusive access flag for lock-free single-producer optimization.
    // Uses Interlocked.CompareExchange for atomic claim/release:
    // - 0 = no one is currently appending (available)
    // - Non-zero = a thread is currently appending (busy)
    //
    // This replaces the previous ownership-based optimization which had a race condition
    // where the fast path (checking ownership) could run concurrently with the slow path
    // (acquiring SpinLock and claiming ownership).
    //
    // The new approach uses CAS to atomically claim exclusive access:
    // 1. CAS(0 -> 1): If success, we have exclusive access, proceed without SpinLock
    // 2. CAS fails: Someone else is appending, fall back to SpinWait loop
    // 3. After append, set back to 0 to release
    //
    // This is correct because:
    // - Only one thread can win the CAS at a time
    // - The winner has exclusive access until it releases
    // - Losers spin until the winner releases
    private int _exclusiveAccess;

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

        // Create arena for zero-copy serialization
        // Size matches batch size; when full, batch rotates
        _arena = new BatchArena(options.BatchSize);

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

    /// <summary>
    /// Gets the batch's arena for direct serialization.
    /// Returns null if arena is not available (batch completed or arena full).
    /// </summary>
    public BatchArena? Arena => Volatile.Read(ref _isCompleted) == 0 ? _arena : null;

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

            // Check if batch was completed while we were waiting for the lock.
            // Complete() sets _isCompleted and nulls out arrays without holding the lock,
            // so we must check this before accessing any arrays.
            if (Volatile.Read(ref _isCompleted) != 0)
            {
                return new RecordAppendResult(false);
            }

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
    ///
    /// Uses lock-free CAS-based synchronization for exclusive access, which is faster
    /// than SpinLock for the common single-producer case while remaining correct under
    /// multi-producer contention.
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

        // FAST PATH: Try to atomically claim exclusive access via CAS.
        // If we win (exchange 0 -> 1), we have exclusive access and can proceed without spinning.
        // This is the common case for single-producer patterns.
        if (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) == 0)
        {
            try
            {
                return TryAppendFireAndForgetCore(timestamp, key, value, headers, pooledHeaderArray, recordSize);
            }
            finally
            {
                // Release exclusive access
                Volatile.Write(ref _exclusiveAccess, 0);
            }
        }

        // SLOW PATH: CAS failed - another thread is appending. Spin until we can claim access.
        return TryAppendFireAndForgetWithSpinWait(timestamp, key, value, headers, pooledHeaderArray, recordSize);
    }

    /// <summary>
    /// Core append logic without locking. Called when we've verified single-producer pattern
    /// or when we already hold the lock.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RecordAppendResult TryAppendFireAndForgetCore(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
        int recordSize)
    {
        // Check if batch was completed - Complete() nulls out arrays without synchronization,
        // so we must check this before accessing any arrays.
        if (Volatile.Read(ref _isCompleted) != 0)
        {
            return new RecordAppendResult(false);
        }

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

        return new RecordAppendResult(true);
    }

    /// <summary>
    /// Arena-based fire-and-forget append. Key/value data is already in the batch's arena.
    /// This is the zero-allocation path - no per-message ArrayPool rentals.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RecordAppendResult TryAppendFromArena(
        long timestamp,
        ArenaSlice keySlice,
        bool isKeyNull,
        ArenaSlice valueSlice,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray)
    {
        var recordSize = EstimateRecordSize(keySlice.Length, valueSlice.Length, headers);

        // FAST PATH: Try to atomically claim exclusive access via CAS.
        if (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) == 0)
        {
            try
            {
                return TryAppendFromArenaCore(timestamp, keySlice, isKeyNull, valueSlice, headers, pooledHeaderArray, recordSize);
            }
            finally
            {
                Volatile.Write(ref _exclusiveAccess, 0);
            }
        }

        // SLOW PATH: Spin until we can claim access.
        return TryAppendFromArenaWithSpinWait(timestamp, keySlice, isKeyNull, valueSlice, headers, pooledHeaderArray, recordSize);
    }

    /// <summary>
    /// Core append logic for arena-based data. No per-message array tracking needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RecordAppendResult TryAppendFromArenaCore(
        long timestamp,
        ArenaSlice keySlice,
        bool isKeyNull,
        ArenaSlice valueSlice,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
        int recordSize)
    {
        if (Volatile.Read(ref _isCompleted) != 0)
        {
            return new RecordAppendResult(false);
        }

        if (_recordCount == 0)
        {
            _baseTimestamp = timestamp;
        }

        // Check size limit
        if (_estimatedSize + recordSize > _options.BatchSize && _recordCount > 0)
        {
            return new RecordAppendResult(false);
        }

        // Grow records array if needed
        if (_recordCount >= _records.Length)
        {
            GrowArray(ref _records, ref _recordCount, ArrayPool<Record>.Shared);
        }
        // Track pooled header arrays (rare)
        if (pooledHeaderArray is not null && _pooledHeaderArrayCount >= _pooledHeaderArrays.Length)
        {
            GrowArray(ref _pooledHeaderArrays, ref _pooledHeaderArrayCount, ArrayPool<RecordHeader[]>.Shared);
        }
        if (pooledHeaderArray is not null)
        {
            _pooledHeaderArrays[_pooledHeaderArrayCount++] = pooledHeaderArray;
        }

        // Create record with Memory referencing the arena buffer
        // Key and value data is already in the arena, we just store the slice info
        var arena = _arena!;
        var timestampDelta = (int)(timestamp - _baseTimestamp);
        var record = new Record
        {
            TimestampDelta = timestampDelta,
            OffsetDelta = _recordCount,
            Key = isKeyNull ? ReadOnlyMemory<byte>.Empty : arena.Buffer.AsMemory(keySlice.Offset, keySlice.Length),
            IsKeyNull = isKeyNull,
            Value = arena.Buffer.AsMemory(valueSlice.Offset, valueSlice.Length),
            IsValueNull = false,
            Headers = headers
        };

        _records[_recordCount++] = record;
        _estimatedSize += recordSize;

        return new RecordAppendResult(true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private RecordAppendResult TryAppendFromArenaWithSpinWait(
        long timestamp,
        ArenaSlice keySlice,
        bool isKeyNull,
        ArenaSlice valueSlice,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
        int recordSize)
    {
        var spin = new SpinWait();
        while (true)
        {
            if (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) == 0)
            {
                try
                {
                    return TryAppendFromArenaCore(timestamp, keySlice, isKeyNull, valueSlice, headers, pooledHeaderArray, recordSize);
                }
                finally
                {
                    Volatile.Write(ref _exclusiveAccess, 0);
                }
            }
            spin.SpinOnce();
        }
    }

    /// <summary>
    /// Slow path: spins until exclusive access is available, then appends.
    /// Called when CAS failed because another thread is currently appending.
    /// Uses SpinWait for efficient spinning that adapts to contention level.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private RecordAppendResult TryAppendFireAndForgetWithSpinWait(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
        int recordSize)
    {
        var spin = new SpinWait();

        while (true)
        {
            // Try to claim exclusive access
            if (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) == 0)
            {
                try
                {
                    return TryAppendFireAndForgetCore(timestamp, key, value, headers, pooledHeaderArray, recordSize);
                }
                finally
                {
                    // Release exclusive access
                    Volatile.Write(ref _exclusiveAccess, 0);
                }
            }

            // Someone else has access, spin and retry
            spin.SpinOnce();
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

            // Check if batch was completed while we were waiting for the lock.
            if (Volatile.Read(ref _isCompleted) != 0)
            {
                return 0;
            }

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

    /// <summary>
    /// Checks if this batch should be flushed based on linger time.
    /// Uses volatile read instead of locking since this is a read-only check.
    /// The worst case of a stale read is harmless - we'll catch it on the next check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShouldFlush(DateTimeOffset now, int lingerMs)
    {
        // Volatile read for thread-safe access without locking.
        // A stale read that returns 0 when there are records just delays flush to next cycle.
        // A stale read that returns non-zero for an empty batch results in a no-op Complete().
        if (Volatile.Read(ref _recordCount) == 0)
            return false;

        return (now - _createdAt).TotalMilliseconds >= lingerMs;
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

        // Use pooled records array directly with wrapper to avoid allocation
        // ReadyBatch will return the array to pool in Cleanup()
        var pooledRecordsArray = _records;
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = _baseTimestamp,
            MaxTimestamp = _baseTimestamp + (_recordCount > 0 ? pooledRecordsArray[_recordCount - 1].TimestampDelta : 0),
            LastOffsetDelta = _recordCount - 1,
            Records = new RecordListWrapper(pooledRecordsArray, _recordCount)
        };
        _records = null!;

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
            _pooledHeaderArrayCount,
            pooledRecordsArray,
            _arena);

        // Null out references - ownership transferred to ReadyBatch
        _completionSources = null!;
        _pooledArrays = null!;
        _pooledHeaderArrays = null!;
        _arena = null;

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

        // Return arena buffer if present
        _arena?.Return();

        // Null out references to prevent accidental reuse
        _records = null!;
        _completionSources = null!;
        _pooledArrays = null!;
        _pooledHeaderArrays = null!;
        _arena = null;
    }

    private static int EstimateRecordSize(int keyLength, int valueLength, IReadOnlyList<RecordHeader>? headers)
    {
        var size = 20; // Base overhead for varint lengths, timestamp delta, offset delta, etc.
        size += keyLength;
        size += valueLength;

        if (headers is not null)
        {
            // Use index-based iteration to guarantee zero-allocation (avoids enumerator)
            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
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
internal sealed class ReadyBatch
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
    private readonly Record[]? _pooledRecordsArray; // Pooled records array from RecordBatch
    private readonly BatchArena? _arena; // Arena for zero-copy serialization data

    private int _cleanedUp; // 0 = not cleaned, 1 = cleaned (prevents double-cleanup)

    public ReadyBatch(
        TopicPartition topicPartition,
        RecordBatch recordBatch,
        PooledValueTaskSource<RecordMetadata>[] completionSourcesArray,
        int completionSourcesCount,
        byte[][] pooledDataArrays,
        int pooledDataArraysCount,
        RecordHeader[][] pooledHeaderArrays,
        int pooledHeaderArraysCount,
        Record[]? pooledRecordsArray = null,
        BatchArena? arena = null)
    {
        TopicPartition = topicPartition;
        RecordBatch = recordBatch;
        _completionSourcesArray = completionSourcesArray;
        _completionSourcesCount = completionSourcesCount;
        _pooledDataArrays = pooledDataArrays;
        _pooledDataArraysCount = pooledDataArraysCount;
        _pooledHeaderArrays = pooledHeaderArrays;
        _pooledHeaderArraysCount = pooledHeaderArraysCount;
        _pooledRecordsArray = pooledRecordsArray;
        _arena = arena;
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

        // Return pooled byte arrays (key/value data) - only for non-arena path
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

        // Return pooled records array if present
        if (_pooledRecordsArray is not null)
        {
            ArrayPool<Record>.Shared.Return(_pooledRecordsArray, clearArray: false);
        }

        // Return arena buffer if present (arena-based path)
        // This is a single pool return instead of N individual array returns
        _arena?.Return();
    }
}

/// <summary>
/// Zero-allocation wrapper around a pooled Record array that implements IReadOnlyList.
/// Used to present only the valid portion of a pooled array without copying.
/// </summary>
internal readonly struct RecordListWrapper : IReadOnlyList<Record>
{
    private readonly Record[] _array;
    private readonly int _count;

    public RecordListWrapper(Record[] array, int count)
    {
        _array = array;
        _count = count;
    }

    public Record this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _array[index];
        }
    }

    public int Count => _count;

    public Enumerator GetEnumerator() => new(_array, _count);

    IEnumerator<Record> IEnumerable<Record>.GetEnumerator() => GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<Record>
    {
        private readonly Record[] _array;
        private readonly int _count;
        private int _index;

        public Enumerator(Record[] array, int count)
        {
            _array = array;
            _count = count;
            _index = -1;
        }

        public Record Current => _array[_index];

        object System.Collections.IEnumerator.Current => Current;

        public bool MoveNext() => ++_index < _count;

        public void Reset() => _index = -1;

        public void Dispose() { }
    }
}
