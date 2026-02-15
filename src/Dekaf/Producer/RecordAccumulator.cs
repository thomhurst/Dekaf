using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading.Tasks.Sources;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging;

namespace Dekaf.Producer;

/// <summary>
/// Debug-only tracking for message flow through the producer pipeline.
/// Tracks messages at each stage to identify where messages are lost.
/// All recording methods use [Conditional("DEBUG")] so calls are stripped in Release builds.
/// </summary>
internal static class ProducerDebugCounters
{
    // Stage 1: Messages appended to batches
    private static int _messagesAppended;
    private static int _messagesAppendedWithCompletion;
    private static int _messagesAppendedFireAndForget;
    private static int _completionSourcesStoredInBatch;

    // Stage 2: Batches completed and queued
    private static int _batchesCompleted;
    private static int _completionSourcesInCompletedBatches;
    private static int _batchesQueuedToReadyChannel;
    private static int _batchesFailedToQueue;

    // Stage 3: Batches processed by completion loop
    private static int _batchesProcessedByCompletionLoop;
    private static int _batchesForwardedToSendable;

    // Stage 4: Batches sent by sender loop
    private static int _batchesSentSuccessfully;
    private static int _batchesFailed;
    private static int _completionSourcesCompleted;
    private static int _completionSourcesFailed;

    // Stage 5: Flush and disposal
    private static int _flushCalls;
    private static int _batchesFlushedFromDictionary;

    [Conditional("DEBUG")]
    public static void RecordMessageAppended(bool hasCompletionSource)
    {
        Interlocked.Increment(ref _messagesAppended);
        if (hasCompletionSource)
            Interlocked.Increment(ref _messagesAppendedWithCompletion);
        else
            Interlocked.Increment(ref _messagesAppendedFireAndForget);
    }

    [Conditional("DEBUG")]
    public static void RecordCompletionSourceStoredInBatch() =>
        Interlocked.Increment(ref _completionSourcesStoredInBatch);

    [Conditional("DEBUG")]
    public static void RecordBatchCompleted(int completionSourceCount)
    {
        Interlocked.Increment(ref _batchesCompleted);
        Interlocked.Add(ref _completionSourcesInCompletedBatches, completionSourceCount);
    }

    [Conditional("DEBUG")]
    public static void RecordBatchQueuedToReady() => Interlocked.Increment(ref _batchesQueuedToReadyChannel);

    [Conditional("DEBUG")]
    public static void RecordBatchFailedToQueue() => Interlocked.Increment(ref _batchesFailedToQueue);

    [Conditional("DEBUG")]
    public static void RecordBatchProcessedByCompletionLoop() => Interlocked.Increment(ref _batchesProcessedByCompletionLoop);

    [Conditional("DEBUG")]
    public static void RecordBatchForwardedToSendable() => Interlocked.Increment(ref _batchesForwardedToSendable);

    [Conditional("DEBUG")]
    public static void RecordBatchSentSuccessfully() => Interlocked.Increment(ref _batchesSentSuccessfully);

    [Conditional("DEBUG")]
    public static void RecordBatchFailed() => Interlocked.Increment(ref _batchesFailed);

    [Conditional("DEBUG")]
    public static void RecordCompletionSourceCompleted(int count = 1) =>
        Interlocked.Add(ref _completionSourcesCompleted, count);

    [Conditional("DEBUG")]
    public static void RecordCompletionSourceFailed(int count = 1) =>
        Interlocked.Add(ref _completionSourcesFailed, count);

    [Conditional("DEBUG")]
    public static void RecordFlushCall() => Interlocked.Increment(ref _flushCalls);

    [Conditional("DEBUG")]
    public static void RecordBatchFlushedFromDictionary() => Interlocked.Increment(ref _batchesFlushedFromDictionary);

    [Conditional("DEBUG")]
    public static void Reset()
    {
        _messagesAppended = 0;
        _messagesAppendedWithCompletion = 0;
        _messagesAppendedFireAndForget = 0;
        _completionSourcesStoredInBatch = 0;
        _batchesCompleted = 0;
        _completionSourcesInCompletedBatches = 0;
        _batchesQueuedToReadyChannel = 0;
        _batchesFailedToQueue = 0;
        _batchesProcessedByCompletionLoop = 0;
        _batchesForwardedToSendable = 0;
        _batchesSentSuccessfully = 0;
        _batchesFailed = 0;
        _completionSourcesCompleted = 0;
        _completionSourcesFailed = 0;
        _flushCalls = 0;
        _batchesFlushedFromDictionary = 0;
    }

    public static string GetSummary()
    {
#if DEBUG
        return $"""
            [ProducerDebugCounters Summary]
            Stage 1 - Append:
              Messages appended: {_messagesAppended}
              - With completion source: {_messagesAppendedWithCompletion}
              - Fire-and-forget: {_messagesAppendedFireAndForget}
              Completion sources stored (inside lock): {_completionSourcesStoredInBatch}
              LOSS AT APPEND: {_messagesAppendedWithCompletion - _completionSourcesStoredInBatch}
            Stage 2 - Batch Complete:
              Batches completed: {_batchesCompleted}
              Completion sources in completed batches: {_completionSourcesInCompletedBatches}
              LOSS AT COMPLETE: {_completionSourcesStoredInBatch - _completionSourcesInCompletedBatches}
              Batches queued to ready channel: {_batchesQueuedToReadyChannel}
              Batches failed to queue: {_batchesFailedToQueue}
            Stage 3 - Completion Loop:
              Batches processed: {_batchesProcessedByCompletionLoop}
              Batches forwarded to sendable: {_batchesForwardedToSendable}
            Stage 4 - Sender Loop:
              Batches sent successfully: {_batchesSentSuccessfully}
              Batches failed: {_batchesFailed}
              Completion sources completed: {_completionSourcesCompleted}
              Completion sources failed: {_completionSourcesFailed}
            Stage 5 - Flush/Dispose:
              Flush calls: {_flushCalls}
              Batches flushed from dictionary: {_batchesFlushedFromDictionary}
            DISCREPANCY CHECK:
              Expected callbacks: {_messagesAppendedWithCompletion}
              Actual callbacks: {_completionSourcesCompleted + _completionSourcesFailed}
              Missing: {_messagesAppendedWithCompletion - _completionSourcesCompleted - _completionSourcesFailed}
            """;
#else
        return "[ProducerDebugCounters disabled in Release build]";
#endif
    }

    [Conditional("DEBUG")]
    public static void DumpToConsole() => Console.WriteLine(GetSummary());
}

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
    public IReadOnlyList<Header>? Headers { get; init; }
    public Header[]? PooledHeaderArray { get; init; }
}

/// <summary>
/// Work item for per-partition-affine append workers.
/// Encapsulates all data needed to append a record to the accumulator via a worker channel.
/// </summary>
internal readonly struct AppendWorkItem
{
    public readonly string Topic;
    public readonly int Partition;
    public readonly long Timestamp;
    public readonly PooledMemory Key;
    public readonly PooledMemory Value;
    public readonly IReadOnlyList<Header>? Headers;
    public readonly Header[]? PooledHeaderArray;
    public readonly PooledValueTaskSource<RecordMetadata> Completion;
    public readonly CancellationToken CancellationToken;

    public AppendWorkItem(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        Topic = topic;
        Partition = partition;
        Timestamp = timestamp;
        Key = key;
        Value = value;
        Headers = headers;
        PooledHeaderArray = pooledHeaderArray;
        Completion = completion;
        CancellationToken = cancellationToken;
    }
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
    private static readonly ConcurrentQueue<BatchArena> s_pool = new();
    private const int MaxPoolSize = 64;
    private static int s_poolCount;

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
    /// Rents an arena from the pool or creates a new one.
    /// </summary>
    public static BatchArena RentOrCreate(int capacity)
    {
        if (s_pool.TryDequeue(out var arena))
        {
            Interlocked.Decrement(ref s_poolCount);
            arena.Reset(capacity);
            return arena;
        }
        return new BatchArena(capacity);
    }

    /// <summary>
    /// Returns an arena to the pool for reuse, or disposes it if the pool is full.
    /// </summary>
    public static void ReturnToPool(BatchArena arena)
    {
        arena._position = 0;

        if (Interlocked.Increment(ref s_poolCount) <= MaxPoolSize)
        {
            s_pool.Enqueue(arena);
        }
        else
        {
            Interlocked.Decrement(ref s_poolCount);
            // Only return buffer to ArrayPool when arena is discarded
            var buffer = Interlocked.Exchange(ref arena._buffer, null!);
            if (buffer is not null)
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>
    /// Resets the arena for reuse with a new buffer.
    /// </summary>
    private void Reset(int capacity)
    {
        if (_buffer is not null && _buffer.Length >= capacity)
        {
            _buffer.AsSpan(0, _position).Clear();
            _position = 0;
            return;
        }

        if (_buffer is not null)
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);

        _buffer = ArrayPool<byte>.Shared.Rent(capacity);
        _position = 0;
    }

    /// <summary>
    /// Gets the current position in the arena (total bytes used).
    /// Uses volatile read for thread-safe access.
    /// </summary>
    public int Position => Volatile.Read(ref _position);

    /// <summary>
    /// Gets the remaining capacity in the current buffer.
    /// Uses volatile read for thread-safe access.
    /// </summary>
    public int RemainingCapacity => _buffer.Length - Volatile.Read(ref _position);

    /// <summary>
    /// Tries to allocate space in the arena and returns a span for writing.
    /// Thread-safe: uses CAS to atomically claim space in the arena.
    /// </summary>
    /// <param name="size">Number of bytes needed.</param>
    /// <param name="span">Output span to write to.</param>
    /// <param name="offset">Output offset where the allocation starts.</param>
    /// <returns>True if allocation succeeded, false if arena is full.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAllocate(int size, out Span<byte> span, out int offset)
    {
        // CAS loop for thread-safe allocation.
        // Multiple threads may race to allocate space, but each will get a unique region.
        while (true)
        {
            var currentPos = Volatile.Read(ref _position);
            var newPos = currentPos + size;

            if (newPos > _buffer.Length)
            {
                // Not enough space - don't grow, let caller rotate batch
                span = default;
                offset = 0;
                return false;
            }

            // Atomically claim this region
            if (Interlocked.CompareExchange(ref _position, newPos, currentPos) == currentPos)
            {
                offset = currentPos;
                span = _buffer.AsSpan(currentPos, size);
                return true;
            }
            // CAS failed - another thread allocated, retry with new position
        }
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
    /// Thread-safe: uses Interlocked.Exchange to prevent double-return.
    /// </summary>
    public void Return()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null!);
        if (buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
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

        if (!_arena.TryAllocate(sizeHint, out _, out var offset))
        {
            _failed = true;
            return Memory<byte>.Empty;
        }

        // Use offset returned by TryAllocate - don't compute from Position
        // which could race with concurrent allocations
        return _arena.Buffer.AsMemory(offset, sizeHint);
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
public sealed partial class RecordAccumulator : IAsyncDisposable
{
    private readonly ProducerOptions _options;
    private readonly ConcurrentDictionary<TopicPartition, PartitionBatch> _batches = new();
    private readonly ConcurrentDictionary<TopicPartition, object> _partitionSealLocks = new();
    private readonly Channel<ReadyBatch> _readyBatches;

    // Per-partition-affine append workers: each worker owns a channel and processes
    // appends for a subset of partitions (partition % workerCount). This eliminates
    // contention on CAS spin, ConcurrentDictionary lookups, and per-partition seal locks
    // when many threads miss the fast path and fall through to the slow (async) append path.
    private readonly Channel<AppendWorkItem>[] _appendWorkerChannels;
    private readonly int _appendWorkerCount;
    private Task[]? _appendWorkerTasks;

    private readonly PartitionBatchPool _batchPool;
    private readonly ReadyBatchPool _readyBatchPool; // Pool for ReadyBatch objects to eliminate per-batch allocations

    // Track in-flight batches for FlushAsync using O(1) counter instead of dictionary
    // This eliminates dictionary resizing issues under high throughput
    private long _inFlightBatchCount;
    // TCS for async waiting - created on-demand, completed when counter reaches 0
    // Using TCS instead of ManualResetEventSlim avoids polling and ThreadPool starvation
    // Not volatile - use Volatile.Read/Interlocked for thread-safe access
    private TaskCompletionSource<bool>? _flushTcs;

    // Optimization: Track the oldest batch creation time to skip unnecessary enumeration.
    // With LingerMs=5ms and 1ms timer, we'd enumerate 5x per batch without this optimization.
    // By tracking the oldest batch, we can skip enumeration when no batch is old enough to flush.
    // Uses ticks (100ns units) for precision. long.MaxValue means no batches exist.
    private long _oldestBatchCreatedTicks = long.MaxValue;

    // Track whether there are pending awaited produces (ProduceAsync with completion sources).
    // These need immediate flushing via ShouldFlush() regardless of LingerMs, so we can't
    // skip enumeration when this counter is non-zero.
    private int _pendingAwaitedProduceCount;

    // Coordination lock between FlushAsyncCore and ExpireLingerAsyncCore.
    // Prevents a race where the linger timer removes batch-0 from _batches (TryRemove succeeds)
    // but before it writes to _readyBatches, FlushAsync snapshots _batches (missing batch-0),
    // seals batch-1, and writes it to _readyBatches first — reversing batch ordering.
    private readonly SemaphoreSlim _flushLingerLock = new(1, 1);

    private readonly ILogger _logger;

    private volatile bool _disposed;
    private volatile bool _closed;

    // Transaction support: ProducerId, ProducerEpoch, and transactional flag
    // Set by KafkaProducer.InitTransactionsAsync after successful InitProducerId
    internal long ProducerId { get; set; } = -1;
    internal short ProducerEpoch { get; set; } = -1;
    internal bool IsTransactional { get; set; }

    // Per-partition sequence numbers for idempotent/transactional producing.
    // The broker requires monotonically increasing BaseSequence per partition.
    // Uses StrongBox<int> so GetOrAdd returns a mutable reference on the fast path
    // (lock-free hash lookup only), avoiding AddOrUpdate's per-call bucket locking.
    // Each partition is accessed by exactly one BrokerSender thread, so direct
    // mutation of StrongBox.Value is safe without additional synchronization.
    private readonly ConcurrentDictionary<TopicPartition, StrongBox<int>> _sequenceNumbers = new();

    /// <summary>
    /// Gets the next base sequence number for a partition and increments by the record count.
    /// Fast path: lock-free ConcurrentDictionary.GetOrAdd (existing key) + direct mutation.
    /// Each partition is assigned to exactly one BrokerSender, so no contention on the value.
    /// </summary>
    internal int GetAndIncrementSequence(TopicPartition topicPartition, int recordCount)
    {
        var box = _sequenceNumbers.GetOrAdd(topicPartition, static _ => new StrongBox<int>(0));
        var baseSequence = box.Value;
        box.Value = baseSequence + recordCount;
        return baseSequence;
    }

    /// <summary>
    /// Resets all sequence numbers. Called after InitTransactionsAsync when epoch changes.
    /// </summary>
    internal void ResetSequenceNumbers()
    {
        _sequenceNumbers.Clear();
    }

    // Buffer memory tracking for backpressure
    private readonly ulong _maxBufferMemory;
    private long _bufferedBytes;
    // Use ManualResetEventSlim instead of SemaphoreSlim for buffer space signaling.
    // When memory is released, Set() wakes ALL waiting threads so they can race to reserve
    // via lock-free TryReserveMemory(). This eliminates the serialized wakeup bottleneck
    // that occurred with SemaphoreSlim(1,1) which only woke one thread per Release().
    private readonly ManualResetEventSlim _bufferSpaceAvailable = new(true); // Initially signaled (space available)
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly ManualResetEventSlim _disposalEvent = new(false);
    // Pre-allocated WaitHandle array for ReserveMemorySync to avoid per-wait allocation.
    // Index 0 = buffer space available, Index 1 = disposal signal.
    private readonly WaitHandle[] _syncWaitHandles;

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

    public RecordAccumulator(ProducerOptions options, ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _options = options;
        _readyBatchPool = new ReadyBatchPool();
        _batchPool = new PartitionBatchPool(options);
        _batchPool.SetReadyBatchPool(_readyBatchPool); // Wire up pools
        _maxBufferMemory = options.BufferMemory;

        // Pre-allocate WaitHandle array for sync wait path to avoid per-iteration allocation
        _syncWaitHandles = [_bufferSpaceAvailable.WaitHandle, _disposalEvent.WaitHandle];

        // Use unbounded channel for ready batches - backpressure is now handled by buffer memory tracking
        // Single channel design: batches go directly to sender loop (no intermediate CompletionLoop)
        _readyBatches = Channel.CreateUnbounded<ReadyBatch>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Create per-partition-affine append worker channels.
        // Each channel is SingleReader (one worker) but allows multiple writers (caller threads).
        _appendWorkerCount = Math.Clamp(Environment.ProcessorCount, 1, 8);
        _appendWorkerChannels = new Channel<AppendWorkItem>[_appendWorkerCount];
        for (var i = 0; i < _appendWorkerCount; i++)
        {
            _appendWorkerChannels[i] = Channel.CreateUnbounded<AppendWorkItem>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        }
    }

    /// <summary>
    /// Exposes the ready batches channel reader for KafkaProducer.SenderLoop.
    /// Simplified architecture: batches go directly from append → ready channel → sender loop.
    /// </summary>
    internal ChannelReader<ReadyBatch> ReadyBatches => _readyBatches.Reader;

    /// <summary>
    /// Returns a ReadyBatch to the pool for reuse.
    /// Called by KafkaProducer after batch is processed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReturnReadyBatch(ReadyBatch batch)
    {
        _readyBatchPool.Return(batch);
    }

    /// <summary>
    /// Starts per-partition-affine append worker tasks.
    /// Each worker processes appends for partitions where (partition % workerCount == workerIndex),
    /// enabling cross-partition parallelism while preserving per-partition ordering.
    /// </summary>
    internal void StartAppendWorkers(CancellationToken cancellationToken)
    {
        _appendWorkerTasks = new Task[_appendWorkerCount];
        for (var i = 0; i < _appendWorkerCount; i++)
        {
            var workerIndex = i;
            _appendWorkerTasks[i] = Task.Run(
                () => ProcessAppendWorkerAsync(workerIndex, cancellationToken),
                CancellationToken.None);
        }
    }

    /// <summary>
    /// Worker loop that processes append work items from its dedicated channel.
    /// Each worker handles a subset of partitions, so AppendAsync calls for the same
    /// partition are always serialized through a single worker — no contention.
    /// </summary>
    private async Task ProcessAppendWorkerAsync(int workerIndex, CancellationToken cancellationToken)
    {
        var reader = _appendWorkerChannels[workerIndex].Reader;
        await foreach (var workItem in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await AppendAsync(
                    workItem.Topic,
                    workItem.Partition,
                    workItem.Timestamp,
                    workItem.Key,
                    workItem.Value,
                    workItem.Headers,
                    workItem.PooledHeaderArray,
                    workItem.Completion,
                    workItem.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (workItem.CancellationToken.IsCancellationRequested)
            {
                CleanupWorkItemResources(in workItem);
                workItem.Completion.TrySetCanceled(workItem.CancellationToken);
            }
            catch (Exception ex)
            {
                CleanupWorkItemResources(in workItem);
                workItem.Completion.TrySetException(ex);
            }
        }
    }

    /// <summary>
    /// Returns pooled resources owned by a work item back to their respective ArrayPools.
    /// Called when the work item will NOT be processed by AppendAsync (exception, cancellation, disposal).
    /// On the success path, AppendAsync/TryAppend takes ownership of these resources.
    /// </summary>
    private static void CleanupWorkItemResources(in AppendWorkItem workItem)
    {
        workItem.Key.Return();
        workItem.Value.Return();
        if (workItem.PooledHeaderArray is not null)
        {
            ArrayPool<Header>.Shared.Return(workItem.PooledHeaderArray);
        }
    }

    /// <summary>
    /// Enqueues a record for append by a per-partition-affine worker.
    /// Partition is used to route the work item to a specific worker channel,
    /// ensuring all appends for the same partition are processed sequentially.
    /// </summary>
    internal void EnqueueAppend(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        var workerIndex = (int)((uint)partition % (uint)_appendWorkerCount);
        var workItem = new AppendWorkItem(topic, partition, timestamp, key, value,
            headers, pooledHeaderArray, completion, cancellationToken);

        if (!_appendWorkerChannels[workerIndex].Writer.TryWrite(workItem))
        {
            CleanupWorkItemResources(in workItem);
            completion.TrySetException(new ObjectDisposedException(nameof(RecordAccumulator)));
        }
    }

    /// <summary>
    /// Rents a new PartitionBatch from the pool and configures it with current transaction state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PartitionBatch RentBatch(TopicPartition topicPartition)
    {
        var batch = _batchPool.Rent(topicPartition);
        batch.SetTransactionState(ProducerId, ProducerEpoch, IsTransactional, ProducerId >= 0 ? this : null);
        return batch;
    }

    /// <summary>
    /// Gets the current buffered memory usage in bytes.
    /// </summary>
    public long BufferedBytes => Volatile.Read(ref _bufferedBytes);

    /// <summary>
    /// Gets the maximum buffer memory limit in bytes.
    /// </summary>
    public ulong MaxBufferMemory => _maxBufferMemory;

    /// <summary>
    /// Attempts to reserve buffer memory for a record without blocking.
    /// Uses lock-free CAS loop for thread-safe reservation.
    /// </summary>
    /// <param name="recordSize">Size in bytes to reserve</param>
    /// <returns>True if memory was reserved; false if BufferMemory limit would be exceeded</returns>
    /// <remarks>
    /// This method must be internal so KafkaProducer can check BufferMemory before arena allocation.
    /// If this returns false, the caller should fall back to the slow path with blocking.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryReserveMemory(int recordSize)
    {
        while (true)
        {
            var current = Volatile.Read(ref _bufferedBytes);
            var newValue = current + recordSize;

            // Check if adding this record would exceed buffer limit
            if ((ulong)newValue > _maxBufferMemory)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _bufferedBytes, newValue, current) == current)
            {
                return true;
            }
            // CAS failed, retry
        }
    }

    /// <summary>
    /// Waits until buffer space is available, then reserves memory for a record.
    /// Throws TimeoutException if buffer space doesn't become available within DeliveryTimeoutMs.
    /// </summary>
    internal async ValueTask ReserveMemoryAsync(int recordSize, CancellationToken cancellationToken)
    {
        // Fast path: try to reserve immediately
        if (TryReserveMemory(recordSize))
        {
            return;
        }

        // Slow path: wait for space to become available with timeout protection
        // Use MaxBlockMs to limit how long we block waiting for buffer space (equivalent to Kafka's max.block.ms)
        // Protect against overflow if MaxBlockMs is configured to a very large value
        var currentBufferedBytes = Volatile.Read(ref _bufferedBytes);
        LogBufferMemoryWaiting(recordSize, currentBufferedBytes, _maxBufferMemory);
        var currentTicks = Environment.TickCount64;
        var deadline = (long.MaxValue - currentTicks > _options.MaxBlockMs)
            ? currentTicks + _options.MaxBlockMs
            : long.MaxValue;

        while (!TryReserveMemory(recordSize))
        {
            // Check disposal first
            if (_disposed)
                throw new ObjectDisposedException(nameof(RecordAccumulator));

            cancellationToken.ThrowIfCancellationRequested();

            // Check if we've exceeded max.block.ms
            var remainingMs = deadline - Environment.TickCount64;
            if (remainingMs <= 0)
            {
                throw new TimeoutException(
                    $"Failed to allocate buffer within max.block.ms ({_options.MaxBlockMs}ms). " +
                    $"Requested {recordSize} bytes, current usage: {Volatile.Read(ref _bufferedBytes)}/{_maxBufferMemory} bytes. " +
                    $"Producer is generating messages faster than the network can send them. " +
                    $"Consider: increasing BufferMemory, increasing MaxBlockMs, reducing production rate, or checking network connectivity.");
            }

            // Reset event before waiting - ReleaseMemory will Set() it when space becomes available.
            // RACE CONDITION MITIGATION: There's an inherent race between Reset() and the signal from
            // ReleaseMemory(). If Set() happens after Reset() but before our wait, we need to detect it.
            _bufferSpaceAvailable.Reset();

            // Check disposal after reset
            if (_disposed)
                throw new ObjectDisposedException(nameof(RecordAccumulator));

            // Check if event was signaled between Reset() and here (race condition mitigation).
            // If already set, skip the delay and immediately retry TryReserveMemory().
            if (_bufferSpaceAvailable.IsSet)
                continue;

            // ManualResetEventSlim doesn't have native async wait, so use short polling.
            // Use up to 5ms poll interval to minimize latency when memory becomes available.
            // The trade-off is slightly more CPU usage, but memory pressure scenarios are
            // transient and this path is only hit under backpressure.
            var waitMs = (int)Math.Min(5, remainingMs);
            await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private void ReserveMemorySync(int recordSize)
    {
        // Fast path: try to reserve immediately
        if (TryReserveMemory(recordSize))
        {
            return;
        }

        // Slow path: wait for space with timeout and cancellation support
        // Use MaxBlockMs to limit how long we block waiting for buffer space (equivalent to Kafka's max.block.ms)
        var currentTicks = Environment.TickCount64;
        var deadline = (long.MaxValue - currentTicks > _options.MaxBlockMs)
            ? currentTicks + _options.MaxBlockMs
            : long.MaxValue;

        var spinWait = new SpinWait();

        while (!TryReserveMemory(recordSize))
        {
            // Spin briefly before waiting (hot path optimization)
            if (spinWait.Count < 10)
            {
                spinWait.SpinOnce();
                // Check for disposal even during spin phase for prompt detection
                if (_disposed)
                {
                    throw new OperationCanceledException(_disposalCts.Token);
                }
                continue;
            }

            // Check for disposal before sleeping
            if (_disposed)
            {
                throw new OperationCanceledException(_disposalCts.Token);
            }

            // Check timeout
            var remainingMs = deadline - Environment.TickCount64;
            if (remainingMs <= 0)
            {
                throw new TimeoutException(
                    $"Failed to allocate buffer within max.block.ms ({_options.MaxBlockMs}ms). " +
                    $"Requested {recordSize} bytes, current usage: {Volatile.Read(ref _bufferedBytes)}/{_maxBufferMemory} bytes. " +
                    $"Producer is generating messages faster than the network can send them. " +
                    $"Consider: increasing BufferMemory, increasing MaxBlockMs, reducing production rate, or checking network connectivity.");
            }

            // Reset event before waiting - ReleaseMemory will Set() it when space becomes available.
            // This ensures we don't miss signals: if Set() happens between our TryReserveMemory fail
            // and Wait(), the Wait() returns immediately. If Set() happens during Wait(), we wake up.
            _bufferSpaceAvailable.Reset();

            // Double-check disposal after reset but before wait
            if (_disposed)
            {
                throw new OperationCanceledException(_disposalCts.Token);
            }

            // Wait for either: buffer space available, disposal, or timeout.
            // Use WaitHandle.WaitAny with pre-allocated array to avoid per-iteration allocation.
            // Sync path uses 10ms (vs 5ms async) because: (1) WaitAny provides true signal-based
            // wake-up so this is max sleep not actual latency, (2) shorter interval reduces
            // idle time after backpressure relief, improving throughput under sustained load.
            var waitMs = Math.Min(10, (int)remainingMs);
            var signaled = WaitHandle.WaitAny(_syncWaitHandles, waitMs);

            // Check if disposal was signaled (index 1)
            if (signaled == 1 || _disposed)
            {
                throw new OperationCanceledException(_disposalCts.Token);
            }
        }
    }

    /// <summary>
    /// Releases reserved buffer memory when a batch is completed/sent.
    /// </summary>
    internal void ReleaseMemory(int batchSize)
    {
        var newValue = Interlocked.Add(ref _bufferedBytes, -batchSize);
        LogBufferMemoryReleased(batchSize, newValue);

        // DEFENSIVE: Detect accounting bugs early
        if (newValue < 0)
        {
#if DEBUG
            throw new InvalidOperationException(
                $"BufferMemory accounting bug: released {batchSize} bytes " +
                $"but resulted in negative value {newValue}. This indicates a " +
                $"reservation/release mismatch bug.");
#else
            // In release builds, log to System.Diagnostics but don't crash
            System.Diagnostics.Debug.Fail(
                $"BufferMemory accounting error: released {batchSize} bytes " +
                $"but resulted in negative value {newValue}. This indicates a " +
                $"reservation/release mismatch bug.");
#endif
        }

        // Signal that space is available - wakes ALL waiting threads so they can race
        // to reserve via lock-free TryReserveMemory(). This is more efficient than
        // SemaphoreSlim which only wakes one thread per Release().
        try
        {
            _bufferSpaceAvailable.Set();
        }
        catch (ObjectDisposedException)
        {
            // Accumulator is disposed, event no longer valid - ignore
        }
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
                // Cache hit - verify batch is still valid (not completed) AND still belongs
                // to the correct partition. The batch object may have been recycled via the pool
                // and Reset() for a different partition while this thread-local cache entry is stale.
                // The linger timer (different thread) can complete + pool a batch, then the produce
                // thread can rent it for another partition, making the stale cache entry dangerous.
                var cachedBatch = entry.Batch;
                if (cachedBatch.Arena is not null && cachedBatch.TopicPartition.Partition == partition)
                {
                    batch = cachedBatch;
                    return true;
                }
                // Batch was completed or recycled for a different partition, clear cache entry
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
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RecordAccumulator));

        // Calculate record size for buffer memory tracking
        var recordSize = PartitionBatch.EstimateRecordSize(key.Length, value.Length, headers);

        // Reserve memory before appending - blocks if buffer is full
        // This provides backpressure when producers are faster than the network can drain
        await ReserveMemoryAsync(recordSize, cancellationToken).ConfigureAwait(false);

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
            // Check disposal and cancellation at top of loop
            if (_disposed)
                throw new ObjectDisposedException(nameof(RecordAccumulator));

            cancellationToken.ThrowIfCancellationRequested();

            // Hot path optimization: TryGetValue first to avoid factory invocation when batch exists.
            // Most appends hit an existing batch, so this avoids GetOrAdd overhead.
            if (!_batches.TryGetValue(topicPartition, out var batch))
            {
                // Cold path: Rent from pool
                var newBatch = RentBatch(topicPartition);
                if (!_batches.TryAdd(topicPartition, newBatch))
                {
                    // Another thread added a batch, return ours to pool
                    _batchPool.Return(newBatch);
                    // Use TryGetValue since the batch might have been removed already
                    if (!_batches.TryGetValue(topicPartition, out batch))
                    {
                        continue; // Retry the loop
                    }
                }
                else
                {
                    batch = newBatch;
                    // Update oldest batch tracking for linger timer optimization
                    UpdateOldestBatchTracking(newBatch);
                }
            }

            // Should never be null at this point - defensive check
            if (batch is null)
            {
                continue; // Retry the loop
            }

            // Track pending awaited produce BEFORE append to prevent race condition:
            // Without this, ExpireLingerAsync could see _pendingAwaitedProduceCount == 0
            // after the message is in the batch but before the counter is incremented.
            Interlocked.Increment(ref _pendingAwaitedProduceCount);

            var result = batch.TryAppend(timestamp, key, value, headers, pooledHeaderArray, completion);

            if (result.Success)
            {
                ProducerDebugCounters.RecordMessageAppended(hasCompletionSource: true);
                // Release the difference between estimated and actual size to prevent memory leak
                // The actual batch memory will be released when the batch is sent via SendBatchAsync
                var overestimate = recordSize - result.ActualSizeAdded;
                if (overestimate > 0)
                    ReleaseMemory(overestimate);
                return result;
            }

            // Append failed (batch full) - decrement counter since message wasn't added.
            // The loop will increment again before the next TryAppend attempt.
            Interlocked.Decrement(ref _pendingAwaitedProduceCount);

            // Batch is full - seal it under a per-partition lock to preserve ordering.
            // The lock ensures that if the linger timer is concurrently sealing an older batch
            // for this partition, the older batch is written to the channel first.
            lock (GetPartitionSealLock(topicPartition))
            {
                if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch)))
                {
                    // Reset oldest batch tracking if dictionary is now empty
                    ResetOldestBatchTrackingIfEmpty();

                    var readyBatch = batch.Complete();
                    if (readyBatch is not null)
                    {
                        // Decrement pending awaited produce count by the number of completion sources in this batch
                        if (readyBatch.CompletionSourcesCount > 0)
                            Interlocked.Add(ref _pendingAwaitedProduceCount, -readyBatch.CompletionSourcesCount);
                        ProducerDebugCounters.RecordBatchCompleted(readyBatch.CompletionSourcesCount);
                        // Track delivery task for FlushAsync
                        OnBatchEntersPipeline();

                        // Write to unbounded channel — TryWrite always succeeds unless writer is completed (disposal)
                        if (!_readyBatches.Writer.TryWrite(readyBatch))
                        {
                            ProducerDebugCounters.RecordBatchFailedToQueue();
                            readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                            OnBatchExitsPipeline();
                            ReleaseMemory(readyBatch.DataSize);
                            _batchPool.Return(batch);
                            throw new ObjectDisposedException(nameof(RecordAccumulator));
                        }

                        ProducerDebugCounters.RecordBatchQueuedToReady();
                        // Return the completed batch shell to the pool for reuse
                        _batchPool.Return(batch);
                    }
                }
            }

            // Loop will rent from pool again, which will either reuse a pooled batch or create new
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
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        PooledValueTaskSource<RecordMetadata> completion)
    {
        if (_disposed)
            return false;

        // Calculate record size for buffer memory tracking
        var recordSize = PartitionBatch.EstimateRecordSize(key.Length, value.Length, headers);

        // Try non-blocking memory reservation. If buffer is full, return false so the
        // caller (ProduceAsync fast path) falls back to the async path which uses
        // ReserveMemoryAsync and yields instead of blocking a thread. This prevents
        // thread pool starvation when BufferMemory is held until batch completion.
        if (!TryReserveMemory(recordSize))
            return false;

        // OPTIMIZATION: Use a nested cache to get/create TopicPartition without allocating on hot path.
        var partitionCache = _topicPartitionCache.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, TopicPartition>());
        var topicPartition = partitionCache.GetOrAdd(partition, static (p, t) => new TopicPartition(t, p), topic);

        // Loop until we successfully append the record.
        while (true)
        {
            // Check disposal at top of loop
            if (_disposed)
            {
                // Release memory before returning
                ReleaseMemory(recordSize);
                return false;
            }

            // Hot path optimization: TryGetValue first to avoid factory invocation when batch exists.
            if (!_batches.TryGetValue(topicPartition, out var batch))
            {
                // Rent from pool
                var newBatch = RentBatch(topicPartition);
                if (!_batches.TryAdd(topicPartition, newBatch))
                {
                    // Another thread added a batch, return ours to pool
                    _batchPool.Return(newBatch);
                    // Use TryGetValue since the batch might have been removed already
                    if (!_batches.TryGetValue(topicPartition, out batch))
                    {
                        continue; // Retry the loop
                    }
                }
                else
                {
                    batch = newBatch;
                    // Update oldest batch tracking for linger timer optimization
                    UpdateOldestBatchTracking(newBatch);
                }
            }

            // Track pending awaited produce BEFORE append to prevent race condition:
            // Without this, ExpireLingerAsync could see _pendingAwaitedProduceCount == 0
            // after the message is in the batch but before the counter is incremented.
            Interlocked.Increment(ref _pendingAwaitedProduceCount);

            var result = batch.TryAppend(timestamp, key, value, headers, pooledHeaderArray, completion);

            if (result.Success)
            {
                ProducerDebugCounters.RecordMessageAppended(hasCompletionSource: true);
                // Release the difference between estimated and actual size to prevent memory leak
                // The actual batch memory will be released when the batch is sent via SendBatchAsync
                var overestimate = recordSize - result.ActualSizeAdded;
                if (overestimate > 0)
                    ReleaseMemory(overestimate);

                return true;
            }

            // Append failed (batch full) - decrement counter since message wasn't added.
            // The loop will increment again before the next TryAppend attempt.
            Interlocked.Decrement(ref _pendingAwaitedProduceCount);

            // Batch is full - atomically remove it from dictionary BEFORE completing.
            // Only the thread that wins the TryRemove race will complete the batch.
            if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch)))
            {
                // Reset oldest batch tracking if dictionary is now empty
                ResetOldestBatchTrackingIfEmpty();

                var readyBatch = batch.Complete();
                if (readyBatch is not null)
                {
                    // Decrement pending awaited produce count by the number of completion sources in this batch
                    if (readyBatch.CompletionSourcesCount > 0)
                        Interlocked.Add(ref _pendingAwaitedProduceCount, -readyBatch.CompletionSourcesCount);
                    ProducerDebugCounters.RecordBatchCompleted(readyBatch.CompletionSourcesCount);
                    // Track delivery task for FlushAsync
                    OnBatchEntersPipeline();

                    // Non-blocking write to unbounded channel - should always succeed
                    // If it fails (channel completed), the producer is being disposed
                    if (!_readyBatches.Writer.TryWrite(readyBatch))
                    {
                        ProducerDebugCounters.RecordBatchFailedToQueue();
                        // Channel is closed, fail the batch and return false
                        readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                        OnBatchExitsPipeline(); // Decrement counter on failure
                        // Release the batch's buffer memory since it won't go through producer
                        ReleaseMemory(readyBatch.DataSize);
                        return false;
                    }

                    ProducerDebugCounters.RecordBatchQueuedToReady();
                    // Return the completed batch shell to the pool for reuse
                    _batchPool.Return(batch);
                }
            }
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
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray)
    {
        if (_disposed)
            return false;

        // Calculate record size for buffer memory tracking
        var recordSize = PartitionBatch.EstimateRecordSize(key.Length, value.Length, headers);

        // Reserve memory before appending - blocks synchronously if buffer is full
        // This provides backpressure when producers are faster than the network can drain
        ReserveMemorySync(recordSize);

        // FAST PATH 1: Check single-partition cache for consecutive messages to same partition.
        // This is the fastest path for single-partition or sticky-partitioner scenarios.
        // CRITICAL: Also verify the batch still belongs to the correct partition.
        // The batch object may have been recycled via the pool and Reset() for a different
        // partition while this thread-local cache entry is stale (e.g., linger timer completed
        // and pooled the batch on another thread, then it was rented for a different partition).
        if (t_cachedAccumulator == this &&
            t_cachedTopic == topic &&
            t_cachedPartition == partition &&
            t_cachedBatch is { } cachedBatch &&
            cachedBatch.TopicPartition.Partition == partition)
        {
            var result = cachedBatch.TryAppendFireAndForget(timestamp, key, value, headers, pooledHeaderArray);
            if (result.Success)
            {
                // Release the difference between estimated and actual size to prevent memory leak
                var overestimate = recordSize - result.ActualSizeAdded;
                if (overestimate > 0)
                    ReleaseMemory(overestimate);

                return true;
            }

            // Cached batch is full - try fast-path rotation using cached TopicPartition
            if (t_cachedTopicPartition.Topic == topic && t_cachedTopicPartition.Partition == partition)
            {
                var rotated = TryRotateBatchFastPath(cachedBatch, t_cachedTopicPartition, topic, partition,
                    timestamp, key, value, headers, pooledHeaderArray, recordSize);

                // If rotation failed, release reserved memory before returning
                if (!rotated)
                    ReleaseMemory(recordSize);

                return rotated;
            }
        }

        // FAST PATH 2: Check multi-partition cache for scenarios with multiple partitions.
        // This handles round-robin/default partitioning across multiple partitions efficiently.
        if (t_partitionBatchCacheOwner == this && t_partitionBatchCache is { } cache)
        {
            var cacheIndex = partition & (MultiPartitionCacheSize - 1); // Fast modulo for power of 2
            ref var entry = ref cache[cacheIndex];
            // CRITICAL: Verify batch still belongs to the correct partition (same recycling concern).
            if (entry.Topic == topic && entry.Partition == partition && entry.Batch is { } mpCachedBatch &&
                mpCachedBatch.TopicPartition.Partition == partition)
            {
                var result = mpCachedBatch.TryAppendFireAndForget(timestamp, key, value, headers, pooledHeaderArray);
                if (result.Success)
                {
                    // Release the difference between estimated and actual size to prevent memory leak
                    var overestimate = recordSize - result.ActualSizeAdded;
                    if (overestimate > 0)
                        ReleaseMemory(overestimate);

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
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        int recordSize)
    {
        // Seal under per-partition lock to preserve ordering across concurrent sealers.
        lock (GetPartitionSealLock(topicPartition))
        {
            if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, oldBatch)))
            {
                // Reset oldest batch tracking if dictionary is now empty
                ResetOldestBatchTrackingIfEmpty();

                var readyBatch = oldBatch.Complete();
                if (readyBatch is not null)
                {
                    // Track delivery task for FlushAsync to wait on
                    OnBatchEntersPipeline();

                    if (!_readyBatches.Writer.TryWrite(readyBatch))
                    {
                        readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                        OnBatchExitsPipeline(); // Decrement counter on failure
                        // Release the batch's buffer memory since it won't go through producer
                        ReleaseMemory(readyBatch.DataSize);
                        t_cachedBatch = null;
                        return false;
                    }

                    // Return the completed batch shell to the pool for reuse
                    _batchPool.Return(oldBatch);
                }
            }
        }

        // Rent a new batch from the pool
        var newBatch = RentBatch(topicPartition);

        // Try to add the new batch - another thread might have added one already
        if (!_batches.TryAdd(topicPartition, newBatch))
        {
            // Another thread added a batch, return ours to pool
            _batchPool.Return(newBatch);
            // Use TryGetValue since the batch might have been removed already
            if (!_batches.TryGetValue(topicPartition, out newBatch!))
            {
                // Batch was removed, retry by falling through to slow path
                t_cachedBatch = null;
                return TryAppendFireAndForgetSlow(topic, partition, timestamp, key, value, headers, pooledHeaderArray);
            }
        }
        else
        {
            // Update oldest batch tracking for linger timer optimization
            UpdateOldestBatchTracking(newBatch);
        }

        // Append to the new batch
        var result = newBatch.TryAppendFireAndForget(timestamp, key, value, headers, pooledHeaderArray);

        if (result.Success)
        {
            // Release the difference between estimated and actual size to prevent memory leak
            var overestimate = recordSize - result.ActualSizeAdded;
            if (overestimate > 0)
                ReleaseMemory(overestimate);

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
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray)
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

        // Calculate record size for memory release if needed
        var recordSize = PartitionBatch.EstimateRecordSize(key.Length, value.Length, headers);

        // Loop until we successfully append the record.
        while (true)
        {
            // Check disposal at top of loop
            if (_disposed)
            {
                // Release memory before returning
                ReleaseMemory(recordSize);
                return false;
            }

            // Hot path optimization: TryGetValue first to avoid factory invocation when batch exists.
            if (!_batches.TryGetValue(topicPartition, out var batch))
            {
                // Rent from pool
                var newBatch = RentBatch(topicPartition);
                if (!_batches.TryAdd(topicPartition, newBatch))
                {
                    // Another thread added a batch, return ours to pool
                    _batchPool.Return(newBatch);
                    // Use TryGetValue since the batch might have been removed already
                    if (!_batches.TryGetValue(topicPartition, out batch))
                    {
                        continue; // Retry the loop
                    }
                }
                else
                {
                    batch = newBatch;
                    // Update oldest batch tracking for linger timer optimization
                    UpdateOldestBatchTracking(newBatch);
                }
            }

            var result = batch.TryAppendFireAndForget(timestamp, key, value, headers, pooledHeaderArray);

            if (result.Success)
            {
                // Release the difference between estimated and actual size to prevent memory leak
                var overestimate = recordSize - result.ActualSizeAdded;
                if (overestimate > 0)
                    ReleaseMemory(overestimate);

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

            // Seal under per-partition lock to preserve ordering across concurrent sealers.
            lock (GetPartitionSealLock(topicPartition))
            {
                if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch)))
                {
                    // Reset oldest batch tracking if dictionary is now empty
                    ResetOldestBatchTrackingIfEmpty();

                    var readyBatch = batch.Complete();
                    if (readyBatch is not null)
                    {
                        // Track delivery task for FlushAsync
                        OnBatchEntersPipeline();

                        if (!_readyBatches.Writer.TryWrite(readyBatch))
                        {
                            readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                            OnBatchExitsPipeline();
                            ReleaseMemory(readyBatch.DataSize);
                            t_cachedBatch = null;
                            mpCache[cacheIndex].Batch = null;
                            _batchPool.Return(batch);
                            return false;
                        }

                        // Return the completed batch shell to the pool for reuse
                        _batchPool.Return(batch);
                    }
                }
            }

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
    /// Appends a record with a delivery callback stored directly in the batch.
    /// This is the slow-path equivalent of TryAppendFireAndForget but with callbacks.
    /// Returns true if appended successfully, false if the accumulator is disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAppendWithCallback(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        Action<RecordMetadata, Exception?> callback)
    {
        if (_disposed)
            return false;

        // Calculate record size for buffer memory tracking
        var recordSize = PartitionBatch.EstimateRecordSize(key.Length, value.Length, headers);

        // Reserve memory before appending - blocks synchronously if buffer is full
        ReserveMemorySync(recordSize);

        // Get or create TopicPartition (cached for performance)
        var partitionCache = _topicPartitionCache.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, TopicPartition>());
        var topicPartition = partitionCache.GetOrAdd(partition, static (p, t) => new TopicPartition(t, p), topic);

        // Loop until we successfully append the record
        while (true)
        {
            if (_disposed)
            {
                ReleaseMemory(recordSize);
                return false;
            }

            if (!_batches.TryGetValue(topicPartition, out var batch))
            {
                var newBatch = RentBatch(topicPartition);
                if (!_batches.TryAdd(topicPartition, newBatch))
                {
                    _batchPool.Return(newBatch);
                    if (!_batches.TryGetValue(topicPartition, out batch))
                    {
                        continue; // Retry
                    }
                }
                else
                {
                    batch = newBatch;
                    // Update oldest batch tracking for linger timer optimization
                    UpdateOldestBatchTracking(newBatch);
                }
            }

            var result = batch.TryAppendWithCallback(timestamp, key, value, headers, pooledHeaderArray, callback);

            if (result.Success)
            {
                var overestimate = recordSize - result.ActualSizeAdded;
                if (overestimate > 0)
                    ReleaseMemory(overestimate);

                return true;
            }

            // Seal under per-partition lock to preserve ordering across concurrent sealers.
            lock (GetPartitionSealLock(topicPartition))
            {
                if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch)))
                {
                    // Reset oldest batch tracking if dictionary is now empty
                    ResetOldestBatchTrackingIfEmpty();

                    var readyBatch = batch.Complete();
                    if (readyBatch is not null)
                    {
                        OnBatchEntersPipeline();

                        if (!_readyBatches.Writer.TryWrite(readyBatch))
                        {
                            readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                            OnBatchExitsPipeline();
                            ReleaseMemory(readyBatch.DataSize);
                            _batchPool.Return(batch);
                            return false;
                        }

                        _batchPool.Return(batch);
                    }
                }
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
        // BUG FIX: Check empty BEFORE reservation to avoid unnecessary memory allocation
        if (items.Length == 0)
            return true;

        // CRITICAL: Reserve BufferMemory for all records upfront
        // Calculate total estimated size for all records in the batch
        var totalEstimatedSize = 0;
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            totalEstimatedSize += PartitionBatch.EstimateRecordSize(item.Key.Length, item.Value.Length, item.Headers);
        }

        // Reserve memory before appending any records
        // This ensures we respect the BufferMemory limit and apply backpressure
        ReserveMemorySync(totalEstimatedSize);

        // BUG FIX: Check disposal AFTER reservation to ensure cleanup in finally block
        // If disposed, release the reserved memory and return
        if (_disposed)
        {
            ReleaseMemory(totalEstimatedSize);
            return false;
        }

        var startIndex = 0;

        try
        {
            // Get or create TopicPartition (cached)
            var partitionCache = _topicPartitionCache.GetOrAdd(topic, static _ => new ConcurrentDictionary<int, TopicPartition>());
            var topicPartition = partitionCache.GetOrAdd(partition, static (p, t) => new TopicPartition(t, p), topic);

            // Loop until all records are appended
            while (startIndex < items.Length)
            {
                // Get or create batch
                if (!_batches.TryGetValue(topicPartition, out var batch))
                {
                    // Rent from pool
                    var newBatch = RentBatch(topicPartition);
                    if (!_batches.TryAdd(topicPartition, newBatch))
                    {
                        // Another thread added a batch, return ours to pool
                        _batchPool.Return(newBatch);
                        // Use TryGetValue since the batch might have been removed already
                        if (!_batches.TryGetValue(topicPartition, out batch))
                        {
                            continue; // Retry the loop
                        }
                    }
                    else
                    {
                        batch = newBatch;
                        // Update oldest batch tracking for linger timer optimization
                        UpdateOldestBatchTracking(newBatch);
                    }
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
                    // Seal under per-partition lock to preserve ordering across concurrent sealers.
                    lock (GetPartitionSealLock(topicPartition))
                    {
                        if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch)))
                        {
                            // Reset oldest batch tracking if dictionary is now empty
                            ResetOldestBatchTrackingIfEmpty();

                            var readyBatch = batch.Complete();
                            if (readyBatch is not null)
                            {
                                // Track delivery task for FlushAsync
                                OnBatchEntersPipeline();

                                try
                                {
                                    if (!_readyBatches.Writer.TryWrite(readyBatch))
                                    {
                                        readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                                        OnBatchExitsPipeline();
                                        ReleaseMemory(readyBatch.DataSize);
                                        t_cachedBatch = null;
                                        _batchPool.Return(batch);
                                        return false;
                                    }

                                    // Return the completed batch shell to the pool for reuse
                                    _batchPool.Return(batch);
                                }
                                catch
                                {
                                    readyBatch.Fail(new InvalidOperationException("Batch append failed"));
                                    OnBatchExitsPipeline();
                                    ReleaseMemory(readyBatch.DataSize);
                                    throw;
                                }
                            }
                        }
                    }
                    t_cachedBatch = null;
                }
            }

            return true;
        }
        finally
        {
            // Release memory only for records that were NOT appended to any batch (error case).
            // Memory for successfully appended records stays reserved in _bufferedBytes and will
            // be released when their containing batch is completed by the sender or during disposal.
            if (startIndex < items.Length)
            {
                var unappendedMemory = 0;
                for (var i = startIndex; i < items.Length; i++)
                {
                    ref readonly var item = ref items[i];
                    unappendedMemory += PartitionBatch.EstimateRecordSize(item.Key.Length, item.Value.Length, item.Headers);
                }
                if (unappendedMemory > 0)
                    ReleaseMemory(unappendedMemory);
            }
        }
    }

    /// <summary>
    /// Checks for batches that have exceeded linger time.
    /// Uses conditional removal to avoid race conditions where a new batch might be created
    /// between Complete() and TryRemove() calls.
    /// </summary>
    /// <remarks>
    /// Optimized with multiple fast paths:
    /// 1. Empty dictionary check - avoids enumeration overhead
    /// 2. Oldest batch age check - skips enumeration if no batch can possibly be ready
    /// With LingerMs=5ms and 1ms timer, this reduces enumeration from 5x to 1x per batch lifetime.
    /// Also uses synchronous TryWrite when possible to avoid async overhead.
    /// </remarks>
    public ValueTask ExpireLingerAsync(CancellationToken cancellationToken)
    {
        // Fast path 1: no batches to check - avoid enumeration and async overhead entirely
        if (_batches.IsEmpty)
        {
            // Reset oldest batch tracking since there are no batches
            Volatile.Write(ref _oldestBatchCreatedTicks, long.MaxValue);
            return ValueTask.CompletedTask;
        }

        // Fast path 2: if the oldest batch hasn't reached linger time yet AND there are no
        // pending awaited produces, skip enumeration. Awaited produces (ProduceAsync) use a
        // micro-linger (min(1ms, LingerMs/10)) so they need more frequent enumeration checks.
        // This is the key optimization: with LingerMs=5 and 1ms timer, we'd enumerate 5x per batch.
        // By tracking the oldest batch, we skip 4 out of 5 enumerations for fire-and-forget workloads.
        if (Volatile.Read(ref _pendingAwaitedProduceCount) == 0)
        {
            var oldestTicks = Volatile.Read(ref _oldestBatchCreatedTicks);
            if (oldestTicks != long.MaxValue)
            {
                var nowTicks = DateTimeOffset.UtcNow.Ticks;
                var millisSinceOldest = (nowTicks - oldestTicks) / TimeSpan.TicksPerMillisecond;
                if (millisSinceOldest < _options.LingerMs)
                {
                    // No batch is old enough to flush yet - skip the O(n) enumeration
                    return ValueTask.CompletedTask;
                }
            }
        }

        return ExpireLingerAsyncCore(cancellationToken);
    }

    /// <summary>
    /// Seals a single batch and writes it to the ready channel for delivery.
    /// Handles Complete → decrement pending count → track delivery → channel write → error handling → pool return.
    /// </summary>
    /// <returns>True if the batch was sealed and written; false if Complete() returned null.</returns>
    private async ValueTask<bool> SealBatchToChannelAsync(
        PartitionBatch batch,
        CancellationToken cancellationToken)
    {
        var readyBatch = batch.Complete();
        if (readyBatch is null)
            return false;

        LogBatchSealed(batch.TopicPartition.Topic, batch.TopicPartition.Partition, batch.RecordCount, readyBatch.DataSize);

        // Decrement pending awaited produce count by the number of completion sources in this batch
        if (readyBatch.CompletionSourcesCount > 0)
            Interlocked.Add(ref _pendingAwaitedProduceCount, -readyBatch.CompletionSourcesCount);
        ProducerDebugCounters.RecordBatchCompleted(readyBatch.CompletionSourcesCount);
        // Track delivery task for FlushAsync
        OnBatchEntersPipeline();

        // Try synchronous write first to avoid async state machine allocation
        if (!_readyBatches.Writer.TryWrite(readyBatch))
        {
            try
            {
                // Channel is bounded or busy, fall back to async
                await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
                ProducerDebugCounters.RecordBatchQueuedToReady();
            }
            catch
            {
                ProducerDebugCounters.RecordBatchFailedToQueue();
                // CRITICAL: Use nested try-finally to ensure memory is ALWAYS released
                // even if readyBatch.Fail() itself throws an exception
                try
                {
                    readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                    OnBatchExitsPipeline(); // Decrement counter on failure
                }
                finally
                {
                    // ALWAYS release memory to prevent permanent leak
                    ReleaseMemory(readyBatch.DataSize);
                }
                throw;
            }
        }
#if DEBUG
        else
        {
            ProducerDebugCounters.RecordBatchQueuedToReady();
        }
#endif

        // Return the completed batch shell to the pool for reuse
        _batchPool.Return(batch);
        return true;
    }

    /// <summary>
    /// Synchronous version of <see cref="SealBatchToChannelAsync"/> for use inside per-partition locks.
    /// Since the ready channel is unbounded, TryWrite always succeeds unless the writer is completed (disposal).
    /// This avoids the need for async WriteAsync inside a lock block.
    /// </summary>
    private void SealBatchToChannelSync(PartitionBatch batch)
    {
        var readyBatch = batch.Complete();
        if (readyBatch is null)
            return;

        LogBatchSealed(batch.TopicPartition.Topic, batch.TopicPartition.Partition, batch.RecordCount, readyBatch.DataSize);

        if (readyBatch.CompletionSourcesCount > 0)
            Interlocked.Add(ref _pendingAwaitedProduceCount, -readyBatch.CompletionSourcesCount);
        ProducerDebugCounters.RecordBatchCompleted(readyBatch.CompletionSourcesCount);
        OnBatchEntersPipeline();

        if (!_readyBatches.Writer.TryWrite(readyBatch))
        {
            ProducerDebugCounters.RecordBatchFailedToQueue();
            try
            {
                readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                OnBatchExitsPipeline();
            }
            finally
            {
                ReleaseMemory(readyBatch.DataSize);
            }
            _batchPool.Return(batch);
            return;
        }

        ProducerDebugCounters.RecordBatchQueuedToReady();
        _batchPool.Return(batch);
    }

    /// <summary>
    /// Unified batch-sealing method used by both linger timer and flush.
    /// </summary>
    /// <param name="sealAll">
    /// true = flush mode: seals ALL batches (Keys.ToArray snapshot, blocking lock).
    /// false = linger mode: seals only expired batches (foreach enumeration, non-blocking lock).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async ValueTask SealBatchesAsync(bool sealAll, CancellationToken cancellationToken)
    {
        if (sealAll)
        {
            // Flush mode: blocking acquire — wait for any in-progress linger iteration to complete
            await _flushLingerLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Linger mode: non-blocking acquire — skip this tick if FlushAsync holds the lock
            // FlushAsync will seal everything, so there's no work lost.
            if (!_flushLingerLock.Wait(0, CancellationToken.None))
                return;
        }

        try
        {
            if (sealAll)
            {
                // Flush mode: snapshot keys to avoid infinite loop if messages are continuously added
                var keysToFlush = _batches.Keys.ToArray();

                foreach (var key in keysToFlush)
                {
                    if (_batches.TryGetValue(key, out var batch))
                    {
                        // Seal under per-partition lock to preserve ordering across concurrent sealers.
                        lock (GetPartitionSealLock(key))
                        {
                            if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(key, batch)))
                            {
                                ResetOldestBatchTrackingIfEmpty();
                                ProducerDebugCounters.RecordBatchFlushedFromDictionary();
                                SealBatchToChannelSync(batch);
                            }
                        }
                    }
                }
            }
            else
            {
                // Linger mode: enumerate and seal only expired batches, track oldest remaining
                var now = DateTimeOffset.UtcNow;
                var newOldestTicks = long.MaxValue;

                foreach (var kvp in _batches)
                {
                    var batch = kvp.Value;
                    if (batch.ShouldFlush(now, _options.LingerMs))
                    {
                        // Seal under per-partition lock to preserve ordering across concurrent sealers.
                        lock (GetPartitionSealLock(kvp.Key))
                        {
                            if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(kvp.Key, batch)))
                            {
                                // Note: We don't call ResetOldestBatchTrackingIfEmpty() here because
                                // linger mode already recalculates _oldestBatchCreatedTicks
                                // at the end of enumeration based on remaining batches.
                                SealBatchToChannelSync(batch);
                            }
                        }
                    }
                    else
                    {
                        // Batch not ready for flush - track its creation time for oldest batch calculation
                        var batchCreatedTicks = batch.CreatedAtTicks;
                        if (batchCreatedTicks < newOldestTicks)
                        {
                            newOldestTicks = batchCreatedTicks;
                        }
                    }
                }

                // Update the oldest batch tracking for next check using CAS to prevent race condition.
                // A concurrent batch addition via UpdateOldestBatchTracking() may have set a valid
                // timestamp after we started enumeration. Only update if we found an older batch
                // than currently tracked, preserving timestamps from concurrent additions.
                var current = Volatile.Read(ref _oldestBatchCreatedTicks);
                while (newOldestTicks < current)
                {
                    var original = Interlocked.CompareExchange(ref _oldestBatchCreatedTicks, newOldestTicks, current);
                    if (original == current)
                        break;
                    current = original;
                }
            }
        }
        finally
        {
            _flushLingerLock.Release();
        }
    }

    private ValueTask ExpireLingerAsyncCore(CancellationToken cancellationToken)
        => SealBatchesAsync(sealAll: false, cancellationToken);

    /// <summary>
    /// Increments the in-flight batch counter when a batch enters the pipeline.
    /// Called when a batch is completed and queued to _readyBatches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnBatchEntersPipeline()
    {
        Interlocked.Increment(ref _inFlightBatchCount);
    }

    /// <summary>
    /// Decrements the in-flight batch counter when a batch exits the pipeline.
    /// Called after a batch is sent (CompleteSend) or failed (Fail).
    /// Must be called by KafkaProducer's SenderLoopAsync after processing each batch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void OnBatchExitsPipeline()
    {
        if (Interlocked.Decrement(ref _inFlightBatchCount) == 0)
        {
            // All batches processed - complete any waiting flush and clear the TCS
            Interlocked.Exchange(ref _flushTcs, null)?.TrySetResult(true);
        }
    }

    /// <summary>
    /// Updates the oldest batch tracking when a new batch is added to the dictionary.
    /// Uses lock-free CAS loop to atomically set to min(current, newBatchTicks).
    /// This is called when TryAdd succeeds for a new batch.
    /// </summary>
    /// <remarks>
    /// Since new batches are always created with current time, they're newer than existing batches.
    /// This only updates when the dictionary was empty (_oldestBatchCreatedTicks == MaxValue)
    /// or in rare race conditions.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateOldestBatchTracking(PartitionBatch newBatch)
    {
        var newBatchTicks = newBatch.CreatedAtTicks;
        var current = Volatile.Read(ref _oldestBatchCreatedTicks);

        // CAS loop to atomically set to min(current, newBatchTicks)
        while (newBatchTicks < current)
        {
            var original = Interlocked.CompareExchange(ref _oldestBatchCreatedTicks, newBatchTicks, current);
            if (original == current)
                break;
            current = original;
        }
    }

    /// <summary>
    /// Resets the oldest batch tracking when a batch is removed from the dictionary.
    /// This prevents stale timestamps from blocking the fast path optimization.
    /// </summary>
    /// <remarks>
    /// Called after TryRemove succeeds. If the dictionary is now empty, resets to MaxValue.
    /// This fixes a race condition where:
    /// 1. ExpireLingerAsyncCore reads batch X's timestamp during enumeration
    /// 2. Another thread removes batch X
    /// 3. ExpireLingerAsyncCore writes a stale oldest timestamp
    /// 4. New batches fail to update oldest (CAS fails since new > stale)
    ///
    /// By resetting when empty, we ensure the next batch addition will correctly set oldest.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetOldestBatchTrackingIfEmpty()
    {
        if (_batches.IsEmpty)
        {
            Volatile.Write(ref _oldestBatchCreatedTicks, long.MaxValue);
        }
    }

    /// <summary>
    /// Gets the per-partition seal lock. This lock ensures that when multiple threads
    /// seal batches for the same partition concurrently (e.g., linger timer and produce thread),
    /// batches are written to the ready channel in creation order.
    /// Without this lock, the following race can cause ordering violations:
    /// 1. Linger timer: TryRemove(B1) — B1 removed from dictionary
    /// 2. Produce thread: creates B2, fills it, TryRemove(B2), Complete(B2), TryWrite(B2)
    /// 3. Linger timer: Complete(B1), TryWrite(B1) — B1 arrives AFTER B2 in channel
    /// The lock prevents step 2 from completing before step 3.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object GetPartitionSealLock(TopicPartition topicPartition) =>
        _partitionSealLocks.GetOrAdd(topicPartition, static _ => new object());

    /// <summary>
    /// Flushes all batches and waits for them to be delivered to Kafka.
    /// </summary>
    /// <remarks>
    /// Optimized to avoid async state machine allocation when there are no batches to flush.
    /// Respects caller's cancellation token - if no timeout is provided, will wait indefinitely.
    /// Uses O(1) counter-based tracking instead of dictionary for zero-allocation.
    /// </remarks>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        // Check cancellation upfront - must throw immediately if already cancelled
        cancellationToken.ThrowIfCancellationRequested();

        // Fast path: no batches to flush AND no in-flight batches - avoid async overhead entirely
        if (_batches.IsEmpty && Volatile.Read(ref _inFlightBatchCount) == 0)
        {
            return ValueTask.CompletedTask;
        }

        return FlushAsyncCore(cancellationToken);
    }

    private async ValueTask FlushAsyncCore(CancellationToken cancellationToken)
    {
        var pendingBatchCount = _batches.Count;
        var inFlightCount = Volatile.Read(ref _inFlightBatchCount);
        LogFlushStarted(pendingBatchCount, inFlightCount);

        ProducerDebugCounters.RecordFlushCall();
        await SealBatchesAsync(sealAll: true, cancellationToken).ConfigureAwait(false);

        // Wait for all in-flight batches to complete using counter-based tracking
        // O(1) operation instead of dictionary enumeration and Task.WhenAll
        // Note: Lock is released before waiting — linger timer can resume for new batches
        if (Volatile.Read(ref _inFlightBatchCount) > 0)
        {
            await WaitForAllBatchesCompleteAsync(cancellationToken).ConfigureAwait(false);
        }

        LogFlushCompleted();
    }

    /// <summary>
    /// Waits for all in-flight batches to complete.
    /// Uses TaskCompletionSource for true async waiting without polling or ThreadPool starvation.
    /// </summary>
    private async ValueTask WaitForAllBatchesCompleteAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            // Fast path: already complete
            if (Volatile.Read(ref _inFlightBatchCount) == 0)
                return;

            // Get or create TCS for waiting
            var tcs = Volatile.Read(ref _flushTcs);
            if (tcs == null)
            {
                // Create new TCS - RunContinuationsAsynchronously prevents stack dives
                var newTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                tcs = Interlocked.CompareExchange(ref _flushTcs, newTcs, null) ?? newTcs;
            }

            // Double-check after TCS setup (counter may have hit 0 while we were setting up)
            if (Volatile.Read(ref _inFlightBatchCount) == 0)
                return;

            // Wait on the TCS with cancellation support
            await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Completes all append worker channels. Safe to call multiple times.
    /// </summary>
    private void CompleteAppendWorkerChannels()
    {
        foreach (var channel in _appendWorkerChannels)
            channel.Writer.TryComplete();
    }

    /// <summary>
    /// Flushes all pending batches and completes the ready channel for graceful shutdown.
    /// The sender loop will process remaining batches and exit when the channel is empty.
    /// </summary>
    public async ValueTask CloseAsync(CancellationToken cancellationToken)
    {
        if (_disposed || _closed)
            return;

        LogCloseStarted(_batches.Count);
        _closed = true;

        // Complete append worker channels so workers drain remaining items and exit.
        // Don't await workers here — they may be blocked in ReserveMemoryAsync which
        // needs the disposal event (set later in DisposeAsync) to unblock.
        CompleteAppendWorkerChannels();

        // Flush all pending batches to the ready channel
        await FlushAsync(cancellationToken).ConfigureAwait(false);

        // Complete the channel - sender will drain remaining batches and exit
        _readyBatches.Writer.Complete();
        LogClosedChannelCompleted();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        var pendingBatches = _batches.Count;
        var inFlightBatches = Volatile.Read(ref _inFlightBatchCount);
        LogDisposeStarted(pendingBatches, inFlightBatches);
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

        // Clear the TopicPartition cache to release memory
        _topicPartitionCache.Clear();

        // FIRST: Try graceful shutdown (send remaining batches) with timeout
        // This matches Confluent.Kafka behavior and prevents data loss
        // We do this BEFORE failing batches to give them a chance to be sent
        if (!_closed)
        {
            try
            {
                // 5-second grace period to flush and send remaining batches
                using var cts = new CancellationTokenSource(5000);
                await CloseAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Timeout or cancellation - proceed with immediate shutdown
                CompleteAppendWorkerChannels();
                _readyBatches.Writer.Complete();
            }
            catch
            {
                // Other exceptions (e.g., no connection) - proceed with immediate shutdown
                CompleteAppendWorkerChannels();
                _readyBatches.Writer.Complete();
            }
        }

        // Ensure append worker channels are completed even if CloseAsync early-returned
        // (CloseAsync checks _disposed and returns immediately, so channels may not be completed)
        CompleteAppendWorkerChannels();

        // Cancel the disposal token and signal the disposal event to interrupt any blocked operations
        // (e.g., workers stuck in ReserveMemoryAsync). Do this AFTER graceful shutdown attempt
        // so FlushAsync can complete normally.
        try
        {
            _disposalCts.Cancel();
            _disposalEvent.Set();
        }
        catch
        {
            // Ignore exceptions during cancellation
        }

        // Wait for append workers to exit now that disposal event has been set.
        // Workers blocked in ReserveMemoryAsync will be interrupted by the disposal event above.
        if (_appendWorkerTasks is not null)
        {
            try
            {
                await Task.WhenAll(_appendWorkerTasks)
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
            }
            catch
            {
                // Timeout or cancellation — proceed with disposal
            }
        }

        // NOW fail any remaining batches that couldn't be sent during graceful shutdown
        var remainingBatches = _batches.Count;
        if (remainingBatches > 0)
            LogDisposalFailingRemainingBatches(remainingBatches);

        var disposedException = new ObjectDisposedException(nameof(RecordAccumulator));

        // Drain append worker channels and fail any unprocessed work items
        foreach (var channel in _appendWorkerChannels)
        {
            while (channel.Reader.TryRead(out var workItem))
            {
                CleanupWorkItemResources(in workItem);
                workItem.Completion.TrySetException(disposedException);
            }
        }

        // Fail incomplete batches still in the dictionary (weren't flushed in time)
        foreach (var kvp in _batches)
        {
            // Use TryRemove to ensure only one thread handles each batch during disposal.
            // This prevents races where a batch is completed and recycled while we're disposing.
            if (_batches.TryRemove(kvp))
            {
                // Reset oldest batch tracking (not strictly needed during disposal, but consistent)
                ResetOldestBatchTrackingIfEmpty();

                var readyBatch = kvp.Value.Complete();
                if (readyBatch is not null)
                {
                    // Decrement pending awaited produce count by the number of completion sources in this batch
                    if (readyBatch.CompletionSourcesCount > 0)
                        Interlocked.Add(ref _pendingAwaitedProduceCount, -readyBatch.CompletionSourcesCount);

                    readyBatch.Fail(disposedException);
                    // Release the batch's buffer memory
                    ReleaseMemory(readyBatch.DataSize);
                }
            }
        }

        // Drain and fail any remaining batches still in the ready channel
        // After graceful close above, this handles:
        // - Unit tests with no sender loop
        // - Timeout scenarios where batches didn't send in time
        // - Batches that were added after close but before disposal completed
        while (_readyBatches.Reader.TryRead(out var readyBatch))
        {
            readyBatch.Fail(disposedException);
            if (!readyBatch.MemoryReleased)
            {
                ReleaseMemory(readyBatch.DataSize);
                readyBatch.MemoryReleased = true;
            }
            OnBatchExitsPipeline(); // Decrement counter for batches drained during disposal
        }

        // Wait for all in-flight batches to complete with a timeout using counter-based tracking
        // After failing all batches above, their DoneTasks should complete quickly
        if (Volatile.Read(ref _inFlightBatchCount) > 0)
        {
            try
            {
                // Wait for all batches with a 5-second timeout to prevent hanging during disposal
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await WaitForAllBatchesCompleteAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Some batches didn't complete in time - proceed with disposal anyway
            }
            catch (TimeoutException)
            {
                // Some batches didn't complete in time - proceed with disposal anyway
            }
        }

        // Final drain: catch any batches that may have been added during our cleanup
        while (_readyBatches.Reader.TryRead(out var batch))
        {
            if (batch is not null)
            {
                batch.Fail(disposedException);
                if (!batch.MemoryReleased)
                {
                    ReleaseMemory(batch.DataSize);
                    batch.MemoryReleased = true;
                }
                OnBatchExitsPipeline(); // Decrement counter
            }
        }

        // Clear the batch pool
        _batchPool.Clear();

        // Dispose resources to prevent leaks
        _bufferSpaceAvailable?.Dispose();
        _disposalCts?.Dispose();
        _disposalEvent?.Dispose();
        _flushLingerLock.Dispose();
        // _flushTcs doesn't need disposal - it's a TaskCompletionSource
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Buffer memory backpressure: waiting for {RequestedBytes} bytes (current: {CurrentBytes}/{MaxBytes})")]
    private partial void LogBufferMemoryWaiting(int requestedBytes, long currentBytes, ulong maxBytes);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Released {ReleasedBytes} bytes of buffer memory (remaining: {RemainingBytes})")]
    private partial void LogBufferMemoryReleased(int releasedBytes, long remainingBytes);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Batch sealed for {Topic}-{Partition}: {RecordCount} records, {DataSize} bytes")]
    private partial void LogBatchSealed(string topic, int partition, int recordCount, int dataSize);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flush started: {PendingBatchCount} pending batches, {InFlightCount} in-flight")]
    private partial void LogFlushStarted(int pendingBatchCount, long inFlightCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Flush completed: all batches delivered")]
    private partial void LogFlushCompleted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Closing accumulator: {PendingBatchCount} pending batches")]
    private partial void LogCloseStarted(int pendingBatchCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Accumulator closed: ready channel completed")]
    private partial void LogClosedChannelCompleted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Disposing accumulator: {PendingBatchCount} pending batches, {InFlightCount} in-flight")]
    private partial void LogDisposeStarted(int pendingBatchCount, long inFlightCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failing {RemainingBatchCount} batches during disposal")]
    private partial void LogDisposalFailingRemainingBatches(int remainingBatchCount);

    #endregion
}

/// <summary>
/// Pool for reusing PartitionBatch instances to avoid ~40KB allocation per batch rotation.
/// Uses a ConcurrentStack for lock-free push/pop operations.
/// </summary>
internal sealed class PartitionBatchPool
{
    private readonly ConcurrentStack<PartitionBatch> _pool = new();
    private readonly ProducerOptions _options;
    private readonly int _maxPoolSize;
    private ReadyBatchPool? _readyBatchPool;

    /// <summary>
    /// Creates a new PartitionBatchPool.
    /// </summary>
    /// <param name="options">Producer options for configuring new batches.</param>
    /// <param name="maxPoolSize">Maximum number of batches to keep pooled. Default is 64.</param>
    public PartitionBatchPool(ProducerOptions options, int maxPoolSize = 64)
    {
        _options = options;
        _maxPoolSize = maxPoolSize;
    }

    /// <summary>
    /// Sets the ReadyBatchPool to use for PartitionBatch.Complete() calls.
    /// Must be called after construction.
    /// </summary>
    public void SetReadyBatchPool(ReadyBatchPool pool)
    {
        _readyBatchPool = pool;
    }

    /// <summary>
    /// Gets a batch from the pool or creates a new one.
    /// </summary>
    public PartitionBatch Rent(TopicPartition topicPartition)
    {
        if (_pool.TryPop(out var batch))
        {
            batch.Reset(topicPartition);
            batch.SetReadyBatchPool(_readyBatchPool);
            return batch;
        }

        var newBatch = new PartitionBatch(topicPartition, _options);
        newBatch.SetReadyBatchPool(_readyBatchPool);
        return newBatch;
    }

    /// <summary>
    /// Returns a batch to the pool for reuse.
    /// The batch must have been prepared for pooling (arrays transferred to ReadyBatch).
    /// </summary>
    public void Return(PartitionBatch batch)
    {
        // Only pool if we haven't exceeded the limit
        if (_pool.Count < _maxPoolSize)
        {
            batch.PrepareForPooling(_options);
            _pool.Push(batch);
        }
        // If pool is full, the batch will be garbage collected
    }

    /// <summary>
    /// Clears all pooled batches (for disposal).
    /// </summary>
    public void Clear()
    {
        _pool.Clear();
    }
}

/// <summary>
/// A batch of records for a single partition.
/// Tracks pooled arrays that are returned when the batch completes.
/// Uses ArrayPool-backed arrays instead of List to eliminate allocations.
///
/// Thread-safety: Multiple threads can call TryAppend concurrently (via ConcurrentDictionary.AddOrUpdate),
/// so we use CAS-based exclusive access to protect array mutations and field updates. This is ideal because:
/// - Critical sections are very short (<100ns)
/// - Lock is per-partition, so no cross-partition contention
/// - CAS is faster than SpinLock/Monitor for the common single-producer case
/// Complete() coordinates with TryAppend via the same _exclusiveAccess flag.
/// </summary>
internal sealed class PartitionBatch
{
    private TopicPartition _topicPartition;
    private ProducerOptions _options;
    private readonly int _initialRecordCapacity;
    private ReadyBatchPool? _readyBatchPool; // Pool for renting ReadyBatch objects

    // Arena for zero-copy serialization - all message data in one contiguous buffer
    private BatchArena? _arena;

    // Zero-allocation array management: use pooled arrays instead of List<T>
    private Record[] _records;
    private int _recordCount;

    private PooledValueTaskSource<RecordMetadata>[] _completionSources;
    private int _completionSourceCount;

    // Callbacks for Send(message, callback) - stored directly in batch for inline invocation
    private Action<RecordMetadata, Exception?>?[]? _callbacks;
    private int _callbackCount;

    // Legacy: pooled arrays for non-arena path (completion-tracked messages)
    private byte[][] _pooledArrays;
    private int _pooledArrayCount;

    private Header[][] _pooledHeaderArrays;
    private int _pooledHeaderArrayCount;

    // Exclusive access flag for CAS-based synchronization.
    // Uses Interlocked.CompareExchange for atomic claim/release:
    // - 0 = no one is currently appending (available)
    // - 1 = a thread is currently appending (busy)
    //
    // All append methods and Complete() coordinate via this flag:
    // 1. CAS(0 -> 1): If success, we have exclusive access
    // 2. CAS fails: Someone else has access, spin wait until available
    // 3. After work completes, set back to 0 to release
    //
    // This is correct because:
    // - Only one thread can win the CAS at a time
    // - The winner has exclusive access until it releases
    // - Losers spin until the winner releases
    private int _exclusiveAccess;

    private long _baseTimestamp;
    private int _estimatedSize;
    // Note: _offsetDelta removed - it always equals _recordCount at assignment time
    private DateTimeOffset _createdAt;
    private int _isCompleted; // 0 = not completed, 1 = completed (Interlocked guard for idempotent Complete)
    private ReadyBatch? _completedBatch; // Cached result to ensure Complete() is idempotent

    // Transaction support: set by RecordAccumulator when batch is rented
    private long _producerId = -1;
    private short _producerEpoch = -1;
    private bool _isTransactional;
    private RecordAccumulator? _accumulator;

    public PartitionBatch(TopicPartition topicPartition, ProducerOptions options)
    {
        _topicPartition = topicPartition;
        _options = options;
        _createdAt = DateTimeOffset.UtcNow;

        _initialRecordCapacity = options.InitialBatchRecordCapacity > 0
            ? Math.Clamp(options.InitialBatchRecordCapacity, 16, 16384)
            : ComputeInitialRecordCapacity(options.BatchSize);

        // Create arena for zero-copy serialization
        // Use ArenaCapacity if set, otherwise fall back to BatchSize
        var arenaCapacity = options.ArenaCapacity > 0 ? options.ArenaCapacity : options.BatchSize;
        _arena = new BatchArena(arenaCapacity);

        // Rent arrays from pool - eliminates List allocations
        _records = ArrayPool<Record>.Shared.Rent(_initialRecordCapacity);
        _recordCount = 0;

        _completionSources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(_initialRecordCapacity);
        _completionSourceCount = 0;

        _pooledArrays = ArrayPool<byte[]>.Shared.Rent(_initialRecordCapacity * 2);
        _pooledArrayCount = 0;

        _pooledHeaderArrays = ArrayPool<Header[]>.Shared.Rent(8); // Headers less common
        _pooledHeaderArrayCount = 0;
    }

    private static int ComputeInitialRecordCapacity(int batchSize)
    {
        // Minimum record wire overhead ~64 bytes (conservative for small messages)
        const int minRecordOverhead = 64;
        var estimated = (uint)Math.Max(batchSize / minRecordOverhead, 64);
        return (int)Math.Min(BitOperations.RoundUpToPowerOf2(estimated), 16384);
    }

    /// <summary>
    /// Sets the ReadyBatchPool to use for renting ReadyBatch objects in Complete().
    /// Must be called after construction or when renting from pool.
    /// </summary>
    internal void SetReadyBatchPool(ReadyBatchPool? pool)
    {
        _readyBatchPool = pool;
    }

    /// <summary>
    /// Sets the transaction state for this batch. Called by RecordAccumulator after renting.
    /// </summary>
    internal void SetTransactionState(long producerId, short producerEpoch, bool isTransactional, RecordAccumulator? accumulator)
    {
        _producerId = producerId;
        _producerEpoch = producerEpoch;
        _isTransactional = isTransactional;
        _accumulator = accumulator;
    }

    /// <summary>
    /// Resets the batch for reuse with a new topic-partition.
    /// Called when renting from the pool.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: _isCompleted and _exclusiveAccess must ONLY be reset here, NOT in PrepareForPooling().
    /// Resetting them in PrepareForPooling() creates a race condition where a stale reference from
    /// another thread could successfully append to the pooled batch (since _isCompleted would be 0),
    /// and those messages would be lost when Reset() is later called. By only resetting these flags
    /// at rent time, we ensure that any stale references fail the _isCompleted check in TryAppend().
    /// </remarks>
    internal void Reset(TopicPartition topicPartition)
    {
        _topicPartition = topicPartition;
        _createdAt = DateTimeOffset.UtcNow;
        _recordCount = 0;
        _completionSourceCount = 0;
        _callbackCount = 0;
        _pooledArrayCount = 0;
        _pooledHeaderArrayCount = 0;
        _baseTimestamp = 0;
        _estimatedSize = 0;
        _isCompleted = 0;  // Only reset here - see remarks
        _completedBatch = null;
        _exclusiveAccess = 0;  // Only reset here - see remarks
    }

    /// <summary>
    /// Prepares the batch for returning to the pool.
    /// Allocates new arrays (the old ones were transferred to ReadyBatch).
    /// IMPORTANT: This must only be called after Complete() which transfers arrays to ReadyBatch.
    /// </summary>
    internal void PrepareForPooling(ProducerOptions options)
    {
        _options = options;

        // Arena was transferred to ReadyBatch by Complete(), so _arena should be null.
        // This is a no-op but kept for safety.
        _arena?.Return();

        // Rent or create arena for the pooled batch
        var arenaCapacity = options.ArenaCapacity > 0 ? options.ArenaCapacity : options.BatchSize;
        _arena = BatchArena.RentOrCreate(arenaCapacity);

        // Arrays were transferred to ReadyBatch by Complete() and are now null.
        // Allocate fresh arrays for the pooled batch.
        _records = ArrayPool<Record>.Shared.Rent(_initialRecordCapacity);
        _completionSources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(_initialRecordCapacity);
        _pooledArrays = ArrayPool<byte[]>.Shared.Rent(_initialRecordCapacity * 2);
        _pooledHeaderArrays = ArrayPool<Header[]>.Shared.Rent(8);

        // Reset counters and state for reuse.
        // IMPORTANT: Do NOT reset _isCompleted or _exclusiveAccess here!
        // These must only be reset in Reset() when the batch is actually rented.
        // If we reset _isCompleted here, a stale reference from another thread could
        // successfully append to this pooled batch (since _isCompleted would be 0),
        // and those messages would be lost when Reset() is later called.
        _recordCount = 0;
        _completionSourceCount = 0;
        _pooledArrayCount = 0;
        _pooledHeaderArrayCount = 0;
        _baseTimestamp = 0;
        _estimatedSize = 0;
        // _isCompleted stays at 1 - batch is "completed" while in pool
        _completedBatch = null;
        // _exclusiveAccess stays at 0 (should already be 0 from Complete())
    }

    /// <summary>
    /// Gets the batch's arena for direct serialization.
    /// Returns null if arena is not available (batch completed or arena full).
    /// </summary>
    public BatchArena? Arena => Volatile.Read(ref _isCompleted) == 0 ? _arena : null;

    public TopicPartition TopicPartition => _topicPartition;
    public int RecordCount => _recordCount;
    public int EstimatedSize => _estimatedSize;

    /// <summary>
    /// Returns the batch creation time in ticks for efficient age comparisons.
    /// Used by ExpireLingerAsync to track the oldest batch without enumeration.
    /// </summary>
    public long CreatedAtTicks => _createdAt.Ticks;

    /// <summary>
    /// Appends a record to the batch with completion tracking.
    /// Uses lock-free CAS-based synchronization for the common single-producer case,
    /// falling back to spin-wait under contention.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RecordAppendResult TryAppend(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        PooledValueTaskSource<RecordMetadata> completion)
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
                return TryAppendCore(timestamp, key, value, headers, pooledHeaderArray, completion, recordSize);
            }
            finally
            {
                // Release exclusive access
                Volatile.Write(ref _exclusiveAccess, 0);
            }
        }

        // SLOW PATH: CAS failed - another thread is appending. Spin until we can claim access.
        return TryAppendWithSpinWait(timestamp, key, value, headers, pooledHeaderArray, completion, recordSize);
    }

    /// <summary>
    /// Core append logic with completion tracking. Called when we have exclusive access.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RecordAppendResult TryAppendCore(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        PooledValueTaskSource<RecordMetadata> completion,
        int recordSize)
    {
        // Check if batch was completed - Complete() nulls out arrays without synchronization,
        // so we must check this before accessing any arrays.
        if (Volatile.Read(ref _isCompleted) != 0)
        {
            return new RecordAppendResult(false);
        }

        // Defensive check: if arrays are null, batch is in inconsistent state (being pooled)
        if (_records is null || _completionSources is null || _pooledArrays is null)
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
            GrowArray(ref _pooledHeaderArrays, ref _pooledHeaderArrayCount, ArrayPool<Header[]>.Shared);
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

        var timestampDelta = timestamp - _baseTimestamp;
        var record = new Record
        {
            TimestampDelta = timestampDelta,
            OffsetDelta = _recordCount,
            Key = key.Memory,
            IsKeyNull = key.IsNull,
            Value = value.Memory,
            IsValueNull = value.IsNull,
            Headers = headers
        };

        _records[_recordCount++] = record;
        _estimatedSize += recordSize;

        // Use the passed-in completion source - no allocation here
        _completionSources[_completionSourceCount++] = completion;
        // Track inside exclusive lock - this MUST match batch completion count
        ProducerDebugCounters.RecordCompletionSourceStoredInBatch();

        return new RecordAppendResult(Success: true, ActualSizeAdded: recordSize);
    }

    /// <summary>
    /// Slow path for TryAppend when CAS fails due to contention.
    /// Spins until exclusive access is available.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private RecordAppendResult TryAppendWithSpinWait(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        PooledValueTaskSource<RecordMetadata> completion,
        int recordSize)
    {
        var spinner = new SpinWait();
        while (true)
        {
            // Early exit if already completed
            if (_isCompleted != 0)
                return new RecordAppendResult(false);

            spinner.SpinOnce();

            if (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) == 0)
            {
                try
                {
                    return TryAppendCore(timestamp, key, value, headers, pooledHeaderArray, completion, recordSize);
                }
                finally
                {
                    Volatile.Write(ref _exclusiveAccess, 0);
                }
            }
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
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray)
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
    /// Appends a record with a delivery callback stored directly in the batch.
    /// This is the zero-allocation path for Send(message, callback) - no PooledValueTaskSource needed.
    /// Callbacks are invoked inline on the sender thread when the batch completes.
    ///
    /// Uses lock-free CAS-based synchronization for exclusive access.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RecordAppendResult TryAppendWithCallback(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        Action<RecordMetadata, Exception?> callback)
    {
        // Pre-compute record size outside the lock
        var recordSize = EstimateRecordSize(key.Length, value.Length, headers);

        // FAST PATH: Try to atomically claim exclusive access via CAS.
        if (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) == 0)
        {
            try
            {
                return TryAppendWithCallbackCore(timestamp, key, value, headers, pooledHeaderArray, callback, recordSize);
            }
            finally
            {
                Volatile.Write(ref _exclusiveAccess, 0);
            }
        }

        // SLOW PATH: Spin until we can claim access.
        return TryAppendWithCallbackSpinWait(timestamp, key, value, headers, pooledHeaderArray, callback, recordSize);
    }

    /// <summary>
    /// Core append logic with callback storage. Called when we hold exclusive access.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RecordAppendResult TryAppendWithCallbackCore(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        Action<RecordMetadata, Exception?> callback,
        int recordSize)
    {
        // Check if batch was completed
        if (Volatile.Read(ref _isCompleted) != 0)
        {
            return new RecordAppendResult(false);
        }

        // Defensive check: if arrays are null, batch is in inconsistent state
        if (_records is null || _pooledArrays is null)
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

        // Grow arrays if needed
        if (_recordCount >= _records.Length)
        {
            GrowArray(ref _records, ref _recordCount, ArrayPool<Record>.Shared);
        }
        if (_pooledArrayCount + 2 >= _pooledArrays.Length)
        {
            GrowArray(ref _pooledArrays, ref _pooledArrayCount, ArrayPool<byte[]>.Shared);
        }
        if (pooledHeaderArray is not null && _pooledHeaderArrayCount >= _pooledHeaderArrays.Length)
        {
            GrowArray(ref _pooledHeaderArrays, ref _pooledHeaderArrayCount, ArrayPool<Header[]>.Shared);
        }

        // Ensure callback array exists and has space
        _callbacks ??= ArrayPool<Action<RecordMetadata, Exception?>?>.Shared.Rent(_initialRecordCapacity);
        if (_callbackCount >= _callbacks.Length)
        {
            GrowArray(ref _callbacks!, ref _callbackCount, ArrayPool<Action<RecordMetadata, Exception?>?>.Shared);
        }

        // Track pooled arrays
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

        var timestampDelta = timestamp - _baseTimestamp;
        var record = new Record
        {
            TimestampDelta = timestampDelta,
            OffsetDelta = _recordCount,
            Key = key.Memory,
            IsKeyNull = key.IsNull,
            Value = value.Memory,
            IsValueNull = value.IsNull,
            Headers = headers
        };

        _records[_recordCount] = record;
        _callbacks[_callbackCount++] = callback;
        _recordCount++;
        _estimatedSize += recordSize;

        return new RecordAppendResult(Success: true, ActualSizeAdded: recordSize);
    }

    /// <summary>
    /// Slow path for TryAppendWithCallback - spins until exclusive access is available.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private RecordAppendResult TryAppendWithCallbackSpinWait(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        Action<RecordMetadata, Exception?> callback,
        int recordSize)
    {
        var spin = new SpinWait();

        while (true)
        {
            if (_isCompleted != 0)
                return new RecordAppendResult(false);

            if (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) == 0)
            {
                try
                {
                    return TryAppendWithCallbackCore(timestamp, key, value, headers, pooledHeaderArray, callback, recordSize);
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
    /// Core append logic without locking. Called when we've verified single-producer pattern
    /// or when we already hold the lock.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RecordAppendResult TryAppendFireAndForgetCore(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        int recordSize)
    {
        // Check if batch was completed - Complete() nulls out arrays without synchronization,
        // so we must check this before accessing any arrays.
        if (Volatile.Read(ref _isCompleted) != 0)
        {
            return new RecordAppendResult(false);
        }

        // Defensive check: if arrays are null, batch is in inconsistent state (being pooled)
        if (_records is null || _pooledArrays is null)
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
            GrowArray(ref _pooledHeaderArrays, ref _pooledHeaderArrayCount, ArrayPool<Header[]>.Shared);
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

        var timestampDelta = timestamp - _baseTimestamp;
        var record = new Record
        {
            TimestampDelta = timestampDelta,
            OffsetDelta = _recordCount,
            Key = key.Memory,
            IsKeyNull = key.IsNull,
            Value = value.Memory,
            IsValueNull = value.IsNull,
            Headers = headers
        };

        _records[_recordCount++] = record;
        _estimatedSize += recordSize;

        return new RecordAppendResult(Success: true, ActualSizeAdded: recordSize);
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
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray)
    {
        var recordSize = EstimateRecordSize(keySlice.Length, valueSlice.Length, headers);

        // FAST PATH: Try to atomically claim exclusive access via CAS.
        if (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) == 0)
        {
            try
            {
                return TryAppendFromArenaCore(timestamp, keySlice, isKeyNull, valueSlice, isValueNull, headers, pooledHeaderArray, recordSize);
            }
            finally
            {
                Volatile.Write(ref _exclusiveAccess, 0);
            }
        }

        // SLOW PATH: Spin until we can claim access.
        return TryAppendFromArenaWithSpinWait(timestamp, keySlice, isKeyNull, valueSlice, isValueNull, headers, pooledHeaderArray, recordSize);
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
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
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
            GrowArray(ref _pooledHeaderArrays, ref _pooledHeaderArrayCount, ArrayPool<Header[]>.Shared);
        }
        if (pooledHeaderArray is not null)
        {
            _pooledHeaderArrays[_pooledHeaderArrayCount++] = pooledHeaderArray;
        }

        // Create record with Memory referencing the arena buffer
        // Key and value data is already in the arena, we just store the slice info
        var arena = _arena!;
        var timestampDelta = timestamp - _baseTimestamp;
        var record = new Record
        {
            TimestampDelta = timestampDelta,
            OffsetDelta = _recordCount,
            Key = isKeyNull ? ReadOnlyMemory<byte>.Empty : arena.Buffer.AsMemory(keySlice.Offset, keySlice.Length),
            IsKeyNull = isKeyNull,
            Value = isValueNull ? ReadOnlyMemory<byte>.Empty : arena.Buffer.AsMemory(valueSlice.Offset, valueSlice.Length),
            IsValueNull = isValueNull,
            Headers = headers
        };

        _records[_recordCount++] = record;
        _estimatedSize += recordSize;

        return new RecordAppendResult(Success: true, ActualSizeAdded: recordSize);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private RecordAppendResult TryAppendFromArenaWithSpinWait(
        long timestamp,
        ArenaSlice keySlice,
        bool isKeyNull,
        ArenaSlice valueSlice,
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        int recordSize)
    {
        var spin = new SpinWait();
        while (true)
        {
            // Early exit if already completed
            if (_isCompleted != 0)
                return new RecordAppendResult(false);

            if (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) == 0)
            {
                try
                {
                    return TryAppendFromArenaCore(timestamp, keySlice, isKeyNull, valueSlice, isValueNull, headers, pooledHeaderArray, recordSize);
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
    /// Arena-based append with delivery callback stored directly in the batch.
    /// This is the zero-allocation path for Send(message, callback) with arena serialization.
    /// Callbacks are invoked inline on the sender thread when the batch completes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RecordAppendResult TryAppendFromArenaWithCallback(
        long timestamp,
        ArenaSlice keySlice,
        bool isKeyNull,
        ArenaSlice valueSlice,
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        Action<RecordMetadata, Exception?> callback)
    {
        var recordSize = EstimateRecordSize(keySlice.Length, valueSlice.Length, headers);

        // FAST PATH: Try to atomically claim exclusive access via CAS.
        if (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) == 0)
        {
            try
            {
                return TryAppendFromArenaWithCallbackCore(timestamp, keySlice, isKeyNull, valueSlice, isValueNull, headers, pooledHeaderArray, callback, recordSize);
            }
            finally
            {
                Volatile.Write(ref _exclusiveAccess, 0);
            }
        }

        // SLOW PATH: Spin until we can claim access.
        return TryAppendFromArenaWithCallbackSpinWait(timestamp, keySlice, isKeyNull, valueSlice, isValueNull, headers, pooledHeaderArray, callback, recordSize);
    }

    /// <summary>
    /// Core append logic for arena-based data with callback.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private RecordAppendResult TryAppendFromArenaWithCallbackCore(
        long timestamp,
        ArenaSlice keySlice,
        bool isKeyNull,
        ArenaSlice valueSlice,
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        Action<RecordMetadata, Exception?> callback,
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
            GrowArray(ref _pooledHeaderArrays, ref _pooledHeaderArrayCount, ArrayPool<Header[]>.Shared);
        }
        if (pooledHeaderArray is not null)
        {
            _pooledHeaderArrays[_pooledHeaderArrayCount++] = pooledHeaderArray;
        }

        // Ensure callback array exists and has space
        _callbacks ??= ArrayPool<Action<RecordMetadata, Exception?>?>.Shared.Rent(_initialRecordCapacity);
        if (_callbackCount >= _callbacks.Length)
        {
            GrowArray(ref _callbacks!, ref _callbackCount, ArrayPool<Action<RecordMetadata, Exception?>?>.Shared);
        }

        // Create record with Memory referencing the arena buffer
        var arena = _arena!;
        var timestampDelta = timestamp - _baseTimestamp;
        var record = new Record
        {
            TimestampDelta = timestampDelta,
            OffsetDelta = _recordCount,
            Key = isKeyNull ? ReadOnlyMemory<byte>.Empty : arena.Buffer.AsMemory(keySlice.Offset, keySlice.Length),
            IsKeyNull = isKeyNull,
            Value = isValueNull ? ReadOnlyMemory<byte>.Empty : arena.Buffer.AsMemory(valueSlice.Offset, valueSlice.Length),
            IsValueNull = isValueNull,
            Headers = headers
        };

        _records[_recordCount] = record;
        _callbacks[_callbackCount++] = callback;
        _recordCount++;
        _estimatedSize += recordSize;

        return new RecordAppendResult(Success: true, ActualSizeAdded: recordSize);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private RecordAppendResult TryAppendFromArenaWithCallbackSpinWait(
        long timestamp,
        ArenaSlice keySlice,
        bool isKeyNull,
        ArenaSlice valueSlice,
        bool isValueNull,
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        Action<RecordMetadata, Exception?> callback,
        int recordSize)
    {
        var spin = new SpinWait();
        while (true)
        {
            if (_isCompleted != 0)
                return new RecordAppendResult(false);

            if (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) == 0)
            {
                try
                {
                    return TryAppendFromArenaWithCallbackCore(timestamp, keySlice, isKeyNull, valueSlice, isValueNull, headers, pooledHeaderArray, callback, recordSize);
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
        IReadOnlyList<Header>? headers,
        Header[]? pooledHeaderArray,
        int recordSize)
    {
        var spin = new SpinWait();

        while (true)
        {
            // Early exit if already completed
            if (_isCompleted != 0)
                return new RecordAppendResult(false);

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

        // Use CAS-based locking to coordinate with Complete() which also uses _exclusiveAccess
        var spinner = new SpinWait();
        while (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) != 0)
        {
            // Early exit if already completed
            if (_isCompleted != 0)
                return 0;

            spinner.SpinOnce();
        }

        try
        {
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
                    GrowArray(ref _pooledHeaderArrays, ref _pooledHeaderArrayCount, ArrayPool<Header[]>.Shared);
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

                var timestampDelta = item.Timestamp - _baseTimestamp;
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
            Volatile.Write(ref _exclusiveAccess, 0);
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
    /// Checks if this batch should be flushed based on linger time and pending completions.
    /// Uses volatile read instead of locking since this is a read-only check.
    /// The worst case of a stale read is harmless - we'll catch it on the next check.
    ///
    /// Smart batching strategy:
    /// - If there are completion sources waiting (awaited produces), use a micro-linger
    ///   of min(1ms, LingerMs/10) to let co-temporal messages batch together
    /// - When LingerMs == 0 (default), awaited produces still flush immediately
    /// - Fire-and-forget messages wait for full linger time
    /// This balances low latency for awaited produces with efficient batching.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShouldFlush(DateTimeOffset now, int lingerMs)
    {
        // Volatile read for thread-safe access without locking.
        // A stale read that returns 0 when there are records just delays flush to next cycle.
        // A stale read that returns non-zero for an empty batch results in a no-op Complete().
        if (Volatile.Read(ref _recordCount) == 0)
            return false;

        var elapsedMs = (now - _createdAt).TotalMilliseconds;

        // Awaited produces: use micro-linger instead of immediate flush.
        // When LingerMs > 0, wait min(1ms, LingerMs/10) to let co-temporal messages batch.
        // When LingerMs == 0, flush immediately (preserves current default behavior).
        // Fire-and-forget messages (Send) don't add completion sources, so they
        // still benefit from full linger time batching.
        if (Volatile.Read(ref _completionSourceCount) > 0)
            return lingerMs == 0 || elapsedMs >= Math.Min(1.0, lingerMs / 10.0);

        return elapsedMs >= lingerMs;
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

        // Wait for any in-progress appends to complete before modifying arrays.
        // This coordinates with the CAS-based exclusive access used by TryAppend/TryAppendFireAndForget.
        // NOTE: We MUST spin until we get exclusive access. The Interlocked.Exchange above ensures
        // only one thread proceeds past this point, so there's no need for an early exit check.
        // A previous buggy early exit check (`if (_isCompleted != 0)`) would always trigger since
        // WE just set _isCompleted = 1, causing premature return with null and orphaning completion sources.
        var spinner = new SpinWait();
        while (Interlocked.CompareExchange(ref _exclusiveAccess, 1, 0) != 0)
        {
            spinner.SpinOnce();
        }

        // Now we hold exclusive access - safe to modify arrays.
        // Use try/finally to ensure lock is always released, even if an exception occurs
        // (e.g., OutOfMemoryException during ReadyBatch allocation).
        try
        {
            if (_recordCount == 0)
            {
                // Empty batch - return arrays to pool immediately
                ReturnBatchArraysToPool();
                return null;
            }

            // Use pooled records array directly with wrapper to avoid allocation
            // ReadyBatch will return the array to pool in Cleanup()
            var pooledRecordsArray = _records;
            var attributes = RecordBatchAttributes.None;
            if (_isTransactional)
            {
                attributes |= RecordBatchAttributes.IsTransactional;
            }

            // Sequences are assigned by BrokerSender.SendCoalescedAsync at send time.
            // This eliminates a race between the accumulator's seal thread and the
            // send loop's epoch bump recovery (ResetSequenceNumbers) that caused
            // OutOfOrderSequenceNumber errors when both threads called
            // GetAndIncrementSequence on the same shared counter.
            var baseSequence = -1;

            var batch = new RecordBatch
            {
                BaseOffset = 0,
                BaseTimestamp = _baseTimestamp,
                MaxTimestamp = _baseTimestamp + (_recordCount > 0 ? pooledRecordsArray[_recordCount - 1].TimestampDelta : 0),
                LastOffsetDelta = _recordCount - 1,
                ProducerId = _producerId,
                ProducerEpoch = _producerEpoch,
                BaseSequence = baseSequence,
                Attributes = attributes,
                Records = new RecordListWrapper(pooledRecordsArray, _recordCount)
            };
            _records = null!;

            // Rent ReadyBatch from pool or create new if no pool available
            // This eliminates per-batch class allocations at high throughput
            var readyBatch = _readyBatchPool?.Rent() ?? new ReadyBatch();

            // Initialize with batch data - ownership of arrays transfers to ReadyBatch
            // PooledValueTaskSource auto-returns to its pool when GetResult() is called
            readyBatch.Initialize(
                _topicPartition,
                batch,
                _completionSources,
                _completionSourceCount,
                _pooledArrays,
                _pooledArrayCount,
                _pooledHeaderArrays,
                _pooledHeaderArrayCount,
                _estimatedSize,
                pooledRecordsArray,
                _arena,
                _callbacks,
                _callbackCount);

            _completedBatch = readyBatch;

            // Null out references - ownership transferred to ReadyBatch
            _completionSources = null!;
            _pooledArrays = null!;
            _pooledHeaderArrays = null!;
            _arena = null;
            _callbacks = null;

            return _completedBatch;
        }
        finally
        {
            // Release exclusive access - always release even if exception occurred
            Volatile.Write(ref _exclusiveAccess, 0);
        }
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
            ArrayPool<Header[]>.Shared.Return(_pooledHeaderArrays, clearArray: false);

        // Return arena buffer if present
        _arena?.Return();

        // Null out references to prevent accidental reuse
        _records = null!;
        _completionSources = null!;
        _pooledArrays = null!;
        _pooledHeaderArrays = null!;
        _arena = null;
    }

    /// <summary>
    /// Estimates the size of a record in the batch for buffer memory accounting.
    /// This is an upper-bound estimate that includes:
    /// - 20 bytes base overhead (1 byte attributes + varint overhead for timestamp/offset/key-length/value-length/header-count)
    /// - Key and value payload lengths
    /// - Header sizes (10 bytes overhead per header + key/value lengths)
    /// This must be internal so KafkaProducer can calculate size before reserving memory.
    /// </summary>
    /// <param name="keyLength">Length of the serialized key in bytes (0 if null)</param>
    /// <param name="valueLength">Length of the serialized value in bytes (0 if null)</param>
    /// <param name="headers">Optional collection of record headers</param>
    /// <returns>Estimated size in bytes for buffer memory reservation (upper bound)</returns>
    /// <remarks>
    /// The actual size may be smaller due to varint compression, but this conservative estimate
    /// ensures we never under-allocate BufferMemory.
    /// </remarks>
    internal static int EstimateRecordSize(int keyLength, int valueLength, IReadOnlyList<Header>? headers)
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
                // OPTIMIZATION: For ASCII-only keys (99%+ of cases), string.Length == byte count.
                // Only fall back to expensive UTF8.GetByteCount for non-ASCII keys.
                var headerKeyByteCount = GetHeaderKeyByteCount(header.Key);
                size += headerKeyByteCount + (header.IsValueNull ? 0 : header.Value.Length) + 10;
            }
        }

        return size;
    }

    /// <summary>
    /// Fast path for header key byte count. ASCII keys (the common case) use string length directly.
    /// Uses SIMD-optimized Ascii.IsValid for efficient detection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetHeaderKeyByteCount(string key)
    {
        // SIMD-optimized ASCII check: if all chars are ASCII, byte count == char count
        // This is much faster than UTF8.GetByteCount for the common ASCII case
        return System.Text.Ascii.IsValid(key)
            ? key.Length
            : System.Text.Encoding.UTF8.GetByteCount(key);
    }
}

/// <summary>
/// Result of a record append operation.
/// </summary>
/// <param name="Success">Whether the append succeeded.</param>
/// <param name="ActualSizeAdded">Actual size added to batch (for memory accounting). Only valid when Success=true.</param>
public readonly record struct RecordAppendResult(bool Success, int ActualSizeAdded = 0);

/// <summary>
/// Pool for ReadyBatch objects to eliminate per-batch class allocations.
/// Uses ConcurrentStack for thread-safe lock-free operations.
/// </summary>
internal sealed class ReadyBatchPool
{
    private readonly ConcurrentStack<ReadyBatch> _pool = new();
    private readonly int _maxPoolSize;

    public ReadyBatchPool(int maxPoolSize = 128)
    {
        _maxPoolSize = maxPoolSize;
    }

    /// <summary>
    /// Rents a ReadyBatch from the pool or creates a new one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadyBatch Rent()
    {
        if (_pool.TryPop(out var batch))
        {
            return batch;
        }
        return new ReadyBatch();
    }

    /// <summary>
    /// Returns a ReadyBatch to the pool after resetting it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(ReadyBatch batch)
    {
        batch.Reset();
        if (_pool.Count < _maxPoolSize)
        {
            _pool.Push(batch);
        }
    }
}

/// <summary>
/// A batch ready to be sent. Poolable to eliminate per-batch allocations.
/// Returns pooled arrays to ArrayPool when complete.
/// PooledValueTaskSource instances auto-return to their pool when GetResult() is called.
/// </summary>
internal sealed class ReadyBatch : IValueTaskSource<bool>
{
    private TopicPartition _topicPartition;
    private RecordBatch _recordBatch = null!;

    /// <summary>
    /// The topic-partition this batch is for.
    /// </summary>
    public TopicPartition TopicPartition => _topicPartition;

    /// <summary>
    /// The record batch to send.
    /// </summary>
    public RecordBatch RecordBatch => _recordBatch;

    /// <summary>
    /// Number of completion sources (messages) in this batch.
    /// </summary>
    public int CompletionSourcesCount => _completionSourcesCount;

    /// <summary>
    /// Estimated size of all data in this batch (for buffer memory tracking).
    /// </summary>
    public int DataSize { get; private set; }

    /// <summary>
    /// Gets a ValueTask that completes when this batch is done (either sent successfully or failed).
    /// Used by FlushAsync to wait for batch completion.
    /// IMPORTANT: This task never faults - it completes with true (success) or false (failure).
    /// This design eliminates UnobservedTaskException issues for fire-and-forget scenarios.
    /// Per-message exceptions are handled via the completion sources array, not this task.
    /// </summary>
    public ValueTask<bool> DoneTask => new(this, _doneCore.Version);

    // Working arrays from accumulator (pooled) - mutable for pooling
    private PooledValueTaskSource<RecordMetadata>[]? _completionSourcesArray;
    private int _completionSourcesCount;
    private byte[][]? _pooledDataArrays;
    private int _pooledDataArraysCount;
    private Header[][]? _pooledHeaderArrays;
    private int _pooledHeaderArraysCount;
    private Record[]? _pooledRecordsArray; // Pooled records array from RecordBatch
    private BatchArena? _arena; // Arena for zero-copy serialization data

    // Callbacks for Send(message, callback) - inline invocation without ThreadPool
    private Action<RecordMetadata, Exception?>?[]? _callbacks;
    private int _callbackCount;

    // In-flight tracker entry for coordinated retry with multiple in-flight batches per partition.
    // Set by KafkaProducer when registering with PartitionInflightTracker, cleared in Reset().
    internal InflightEntry? InflightEntry { get; set; }

    /// <summary>
    /// Replaces the record batch with a rewritten one (updated PID/epoch/sequence).
    /// Only called during epoch bump recovery — not in the hot path.
    /// </summary>
    internal void RewriteRecordBatch(RecordBatch newRecordBatch) => _recordBatch = newRecordBatch;

    /// <summary>
    /// Whether BufferMemory has already been released for this batch.
    /// Set to true when ReleaseMemory is called (at TCP send time or in error paths).
    /// Prevents double-release across send and cleanup paths.
    /// </summary>
    internal bool MemoryReleased { get; set; }

    /// <summary>
    /// When true, this batch is a same-broker retry. The send loop unmutes the partition
    /// when coalescing a retry batch, ensuring it is sent before newer batches for the
    /// same partition. Set by ProcessCompletedResponses, cleared during coalescing or in Reset().
    /// </summary>
    internal bool IsRetry { get; set; }

    /// <summary>
    /// Stopwatch timestamp before which this retry batch should not be sent (backoff).
    /// Set by ProcessCompletedResponses when a retriable error occurs. The send loop
    /// skips batches where the backoff hasn't elapsed. 0 means no backoff.
    /// </summary>
    internal long RetryNotBefore { get; set; }

    /// <summary>
    /// Stopwatch timestamp when this batch was initialized. Used for absolute delivery deadline
    /// computation in ProcessCompletedResponses (prevents infinite retries with relative deadlines).
    /// </summary>
    internal long StopwatchCreatedTicks { get; private set; }

    // Batch-level completion tracking using resettable ManualResetValueTaskSourceCore
    // Never faults - uses SetResult(true) for success, SetResult(false) for failure
    private ManualResetValueTaskSourceCore<bool> _doneCore;

    private int _cleanedUp; // 0 = not cleaned, 1 = cleaned (prevents double-cleanup)
    private int _completed; // 0 = not completed, 1 = completed (prevents double-completion)

    /// <summary>
    /// Creates an uninitialized ReadyBatch. Call Initialize() before use.
    /// </summary>
    public ReadyBatch()
    {
        // Default constructor for pooling - Initialize() must be called before use
    }

    /// <summary>
    /// Initializes the batch with data. Must be called after Rent() from pool.
    /// </summary>
    public void Initialize(
        TopicPartition topicPartition,
        RecordBatch recordBatch,
        PooledValueTaskSource<RecordMetadata>[]? completionSourcesArray,
        int completionSourcesCount,
        byte[][]? pooledDataArrays,
        int pooledDataArraysCount,
        Header[][]? pooledHeaderArrays,
        int pooledHeaderArraysCount,
        int dataSize,
        Record[]? pooledRecordsArray = null,
        BatchArena? arena = null,
        Action<RecordMetadata, Exception?>?[]? callbacks = null,
        int callbackCount = 0)
    {
        _topicPartition = topicPartition;
        _recordBatch = recordBatch;
        _completionSourcesArray = completionSourcesArray;
        _completionSourcesCount = completionSourcesCount;
        _pooledDataArrays = pooledDataArrays;
        _pooledDataArraysCount = pooledDataArraysCount;
        _pooledHeaderArrays = pooledHeaderArrays;
        _pooledHeaderArraysCount = pooledHeaderArraysCount;
        DataSize = dataSize;
        _pooledRecordsArray = pooledRecordsArray;
        _arena = arena;
        _callbacks = callbacks;
        _callbackCount = callbackCount;
        StopwatchCreatedTicks = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Resets the batch for reuse. Called by ReadyBatchPool.Return().
    /// </summary>
    public void Reset()
    {
        // Defensive cleanup: ensure pooled arrays are returned even if Cleanup() wasn't called.
        // This handles edge cases like exceptions between CompleteSend and ReturnReadyBatch.
        // Cleanup() is idempotent (uses Interlocked.Exchange), so double-call is safe.
        if (Volatile.Read(ref _cleanedUp) == 0)
        {
            Cleanup();
        }

        // Clear all references to allow GC
        _topicPartition = default;
        _recordBatch = null!;
        _completionSourcesArray = null;
        _completionSourcesCount = 0;
        _pooledDataArrays = null;
        _pooledDataArraysCount = 0;
        _pooledHeaderArrays = null;
        _pooledHeaderArraysCount = 0;
        DataSize = 0;
        _pooledRecordsArray = null;
        _arena = null;
        _callbacks = null;
        _callbackCount = 0;
        InflightEntry = null;
        MemoryReleased = false;
        IsRetry = false;
        RetryNotBefore = 0;

        // Reset state flags
        Volatile.Write(ref _cleanedUp, 0);
        Volatile.Write(ref _completed, 0);

        // Reset the ValueTaskSourceCore for reuse
        _doneCore.Reset();
    }

    // IValueTaskSource<bool> implementation for DoneTask
    bool IValueTaskSource<bool>.GetResult(short token) => _doneCore.GetResult(token);
    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => _doneCore.GetStatus(token);
    void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _doneCore.OnCompleted(continuation, state, token, flags);

    /// <summary>
    /// Marks batch as "ready" (processed by completion loop).
    /// Called by RecordAccumulator.CompletionLoopAsync or unified SenderLoopAsync.
    /// This unblocks FlushAsync for fire-and-forget scenarios.
    /// For unit tests without a sender loop, this is the final completion.
    /// </summary>
    public void CompleteDelivery()
    {
        // Atomically claim completion - only one thread wins
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return;

        // Check _cleanedUp AFTER winning the CAS to avoid TOCTOU race.
        // If Cleanup() ran between our CAS and this check, _doneCore may be reset.
        // In that case, skip SetResult to avoid calling it on a reset core.
        if (Volatile.Read(ref _cleanedUp) != 0)
            return;

        // Signal batch is done (ready for fire-and-forget semantic)
        _doneCore.SetResult(true);
    }

    /// <summary>
    /// Marks batch as successfully sent to Kafka.
    /// Called by KafkaProducer.SenderLoopAsync after network send.
    /// This completes per-message ProduceAsync operations with success metadata.
    /// Also invokes any registered callbacks inline (no ThreadPool scheduling).
    /// </summary>
    public void CompleteSend(long baseOffset, DateTimeOffset timestamp)
    {
        // Guard against calling after Cleanup
        if (Volatile.Read(ref _cleanedUp) != 0)
            return;

        try
        {
            ProducerDebugCounters.RecordBatchSentSuccessfully();

            // Complete per-message completion sources with metadata
            if (_completionSourcesArray is not null)
            {
#if DEBUG
                var completedCount = 0;
#endif
                for (var i = 0; i < _completionSourcesCount; i++)
                {
                    var source = _completionSourcesArray[i];
                    if (source?.TrySetResult(new RecordMetadata
                    {
                        Topic = _topicPartition.Topic,
                        Partition = _topicPartition.Partition,
                        Offset = baseOffset + i,
                        Timestamp = timestamp
                    }) == true)
                    {
#if DEBUG
                        completedCount++;
#endif
                    }
                }
#if DEBUG
                ProducerDebugCounters.RecordCompletionSourceCompleted(completedCount);
#endif
            }

            // Invoke callbacks inline - NO ThreadPool scheduling for zero-allocation
            // Callbacks are invoked on the sender thread, so they must be non-blocking
            if (_callbacks is not null)
            {
                for (var i = 0; i < _callbackCount; i++)
                {
                    var callback = _callbacks[i];
                    if (callback is not null)
                    {
                        try
                        {
                            callback.Invoke(new RecordMetadata
                            {
                                Topic = _topicPartition.Topic,
                                Partition = _topicPartition.Partition,
                                Offset = baseOffset + i,
                                Timestamp = timestamp
                            }, null);
                        }
                        catch
                        {
                            // Swallow callback exceptions - don't crash sender loop
                            // User callbacks are responsible for their own error handling
                        }
                        _callbacks[i] = null; // Clear for pool reuse
                    }
                }
            }

            // Signal batch is done (successfully) if not already completed
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
            {
                _doneCore.SetResult(true);
            }
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// Marks batch as failed with an exception.
    /// Called when batch cannot be sent (disposal, errors, etc).
    /// Per-message completion sources receive the exception for ProduceAsync callers.
    /// Callbacks receive the exception as the second parameter.
    /// DoneTask completes with false (no exception) to avoid UnobservedTaskException.
    /// </summary>
    public void Fail(Exception exception)
    {
        // Guard against calling Fail after Cleanup has been performed
        if (Volatile.Read(ref _cleanedUp) != 0)
            return;

        try
        {
            ProducerDebugCounters.RecordBatchFailed();

            // Fail per-message completion sources - these throw for ProduceAsync callers
            if (_completionSourcesArray is not null)
            {
#if DEBUG
                var failedCount = 0;
#endif
                for (var i = 0; i < _completionSourcesCount; i++)
                {
                    var source = _completionSourcesArray[i];
                    if (source?.TrySetException(exception) == true)
                    {
#if DEBUG
                        failedCount++;
#endif
                    }
                }
#if DEBUG
                ProducerDebugCounters.RecordCompletionSourceFailed(failedCount);
#endif
            }

            // Invoke callbacks with exception - NO ThreadPool scheduling
            if (_callbacks is not null)
            {
                for (var i = 0; i < _callbackCount; i++)
                {
                    var callback = _callbacks[i];
                    if (callback is not null)
                    {
                        try
                        {
                            callback.Invoke(default, exception);
                        }
                        catch
                        {
                            // Swallow callback exceptions - don't crash during failure handling
                        }
                        _callbacks[i] = null; // Clear for pool reuse
                    }
                }
            }

            // Signal batch is done (failed) - NO EXCEPTION to avoid UnobservedTaskException
            // For fire-and-forget, no one awaits this, so exception would go unobserved
            // For FlushAsync, it just needs to know "done", not success/failure details
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
            {
                _doneCore.SetResult(false);
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
        // Null check defensive against partially constructed batches
        if (_pooledDataArrays is not null)
        {
            for (var i = 0; i < _pooledDataArraysCount; i++)
            {
                ArrayPool<byte>.Shared.Return(_pooledDataArrays[i], clearArray: true);
            }
        }

        // Return pooled header arrays (large header counts)
        // clearArray: false - header data is not sensitive
        if (_pooledHeaderArrays is not null)
        {
            for (var i = 0; i < _pooledHeaderArraysCount; i++)
            {
                ArrayPool<Header>.Shared.Return(_pooledHeaderArrays[i], clearArray: false);
            }
        }

        // Return the working arrays to pool
        // Note: PooledValueTaskSource instances auto-return to their pool when awaited
        // clearArray: false for tracking arrays - they only hold references, not actual data
        // Null checks are defensive against partially constructed batches during race conditions
        if (_completionSourcesArray is not null)
            ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Return(_completionSourcesArray, clearArray: false);
        if (_pooledDataArrays is not null)
            ArrayPool<byte[]>.Shared.Return(_pooledDataArrays, clearArray: false);
        if (_pooledHeaderArrays is not null)
            ArrayPool<Header[]>.Shared.Return(_pooledHeaderArrays, clearArray: false);

        // Return pooled records array if present
        if (_pooledRecordsArray is not null)
        {
            ArrayPool<Record>.Shared.Return(_pooledRecordsArray, clearArray: false);
        }

        // Return arena to pool for reuse (arena-based path)
        // This avoids allocating a new BatchArena object on each batch recycle
        if (_arena is not null)
        {
            BatchArena.ReturnToPool(_arena);
        }

        // Return callback array to pool if present
        if (_callbacks is not null)
        {
            ArrayPool<Action<RecordMetadata, Exception?>?>.Shared.Return(_callbacks, clearArray: true);
        }
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
