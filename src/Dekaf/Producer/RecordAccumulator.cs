using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading.Tasks.Sources;
using Dekaf.Compression;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Microsoft.Extensions.Logging;

namespace Dekaf.Producer;

/// <summary>
/// RAII guard for SpinLock that ensures Exit() is called on dispose.
/// Must be used with <c>using var guard = new SpinLockGuard(ref spinLock);</c>.
/// </summary>
internal ref struct SpinLockGuard
{
    private ref SpinLock _lock;
    private bool _taken;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpinLockGuard(ref SpinLock spinLock)
    {
        _lock = ref spinLock;
        _taken = false;
        _lock.Enter(ref _taken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_taken) _lock.Exit();
    }
}

/// <summary>
/// Per-waiter node for FIFO sync waiter queue. Each blocked thread in ReserveMemorySync or
/// WaitForBufferSpace gets its own node so ReleaseMemory can wake waiters one-at-a-time
/// instead of broadcasting (thundering herd). Nodes are NOT pooled — a fresh node is allocated
/// per slow-path entry to avoid a race where a recycled node is signaled for the wrong thread.
/// </summary>
internal sealed class SyncWaiterNode
{
    /// <summary>
    /// The event this waiter blocks on. Starts unsignaled; signaled by ReleaseMemory
    /// when this node reaches the front of the queue.
    /// </summary>
    public readonly ManualResetEventSlim Event = new(false);

    /// <summary>
    /// Set to true when the owning thread is done with this node.
    /// WakeNextSyncWaiter skips cancelled nodes to avoid consuming a signal
    /// that no thread is waiting on.
    /// </summary>
    public volatile bool Cancelled;
}

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
            ArrayPool<byte>.Shared.Return(_array, clearArray: false);
        }
    }
}

/// <summary>
/// Data for a single record in batch append operations.
/// This is a readonly struct to enable passing via ReadOnlySpan for batch operations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ownership Semantics:</b> When passed to batch append methods,
/// ownership of pooled resources (Key.Array, Value.Array, Headers) transfers to the accumulator
/// for successfully appended records. The accumulator will return these arrays to their pools when the
/// batch completes or fails.
/// </para>
/// </remarks>
public readonly struct ProducerRecordData
{
    public long Timestamp { get; init; }
    public PooledMemory Key { get; init; }
    public PooledMemory Value { get; init; }
    public Header[]? Headers { get; init; }
    public int HeaderCount { get; init; }
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
    public readonly Header[]? Headers;
    public readonly int HeaderCount;
    public readonly PooledValueTaskSource<RecordMetadata> Completion;
    public readonly CancellationToken CancellationToken;

    public AppendWorkItem(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        Topic = topic;
        Partition = partition;
        Timestamp = timestamp;
        Key = key;
        Value = value;
        Headers = headers;
        HeaderCount = headerCount;
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
/// When the batch completes, the entire arena is returned to the pool for reuse.
/// </para>
/// <para>
/// This reduces per-message allocations from 2 (key + value) to 0, significantly
/// reducing GC pressure in high-throughput scenarios.
/// </para>
/// <para>
/// Arena buffers are allocated on the Pinned Object Heap (POH) via
/// <c>GC.AllocateUninitializedArray(pinned: true)</c>, avoiding the Large Object Heap
/// entirely. POH buffers are not subject to Gen0/1/2 collection or compaction, eliminating
/// the GC pressure that LOH-allocated buffers would cause during batch rotation churn.
/// Buffers are reclaimable by the GC once all references are dropped (e.g. pool eviction).
/// </para>
/// </remarks>
internal sealed class BatchArena
{
    private static readonly ConcurrentQueue<BatchArena> s_pool = new();
    // Memory tradeoff: pooling arenas retains POH memory for the pool's lifetime.
    // This is a static/process-wide pool shared across all RecordAccumulator instances.
    // POH buffers are reclaimable by the GC when evicted from the pool (references dropped).
    // Each pooled PartitionBatch retains a BatchArena, so the arena pool should be at least
    // as large as PartitionBatchPool.
    //
    // Pool size scales with BufferMemory/BatchSize to handle high batch churn rates
    // (e.g., 16KB batches create ~6,250 batches/sec at 100K msg/sec vs ~100 for 1MB batches).
    // The default (1024) covers sustained load with default 2GB buffer and 1MB batches;
    // smaller batches ratchet up automatically.
    // Profiling showed 256 caused pool overflow and ~3.9GB of POH allocation churn in 2-min stress tests.
    internal const int DefaultPoolSize = 1024;
    // Upper bound on pool size. Worst-case POH retention: MaxPoolSizeCap × arena capacity.
    // With 16KB batches (the smallest that triggers scaling): 2048 × ~18KB ≈ 36MB.
    // With 256KB batches and 2GB buffer: 2048 × ~280KB ≈ 560MB POH retention.
    // With 1MB batches: ComputePoolSize returns 1024 (not 2048), so 1024 × ~1.1MB ≈ ~1.1GB.
    internal const int MaxPoolSizeCap = 2048;
    private static int s_maxPoolSize = DefaultPoolSize;
    private static int s_poolCount;
    private static long s_misses;

    /// <summary>
    /// Increases the static pool size limit if the new value is larger.
    /// Called when a new RecordAccumulator is created with a higher pool size requirement.
    /// Thread-safe via CAS ratchet — the pool size only ever increases because arenas are
    /// expensive POH allocations; shrinking would discard them only to re-allocate later.
    /// Note: in multi-producer scenarios, a disposed small-batch producer leaves the raised
    /// cap in place. This is acceptable because re-creating POH buffers on demand is costlier
    /// than retaining the pool headroom, and most applications use a single producer config.
    /// Worst-case amplification: if a transient small-batch producer (e.g., 256KB batches)
    /// ratchets the cap to 2048, then a 1MB-batch producer can retain up to
    /// 2048 × ~1.1MB ≈ 2.2GB of POH memory instead of the normal 1024 × ~1.1MB ≈ ~1.1GB.
    /// </summary>
    internal static void RatchetPoolSize(int newSize)
    {
        int current;
        do
        {
            current = Volatile.Read(ref s_maxPoolSize);
            if (newSize <= current) return;
        }
        while (Interlocked.CompareExchange(ref s_maxPoolSize, newSize, current) != current);
    }

    /// <summary>
    /// Number of times <see cref="RentOrCreate"/> found the pool empty and had to allocate a new arena.
    /// Use this to diagnose pool sizing — sustained misses under load indicate the pool is too small.
    /// </summary>
    internal static long Misses => Volatile.Read(ref s_misses);

    /// <summary>
    /// Pre-allocates arenas into the static pool to eliminate ramp-up allocation bursts.
    /// Call during producer initialization.
    /// </summary>
    /// <param name="count">Number of arenas to pre-allocate.</param>
    /// <param name="capacity">Buffer capacity for each arena.</param>
    internal static void PreWarm(int count, int capacity)
    {
        var maxPool = Volatile.Read(ref s_maxPoolSize);
        for (var i = 0; i < count; i++)
        {
            if (Volatile.Read(ref s_poolCount) >= maxPool)
                break;

            var arena = new BatchArena(capacity);

            if (Interlocked.Increment(ref s_poolCount) <= maxPool)
            {
                s_pool.Enqueue(arena);
            }
            else
            {
                Interlocked.Decrement(ref s_poolCount);
                break;
            }
        }
    }

    private byte[] _buffer;
    private int _position;

    /// <summary>
    /// Creates a new arena with the specified capacity.
    /// The arena does not grow - when full, the batch should be rotated.
    /// </summary>
    /// <param name="capacity">Buffer size. Pinned on the POH permanently (not from ArrayPool) to avoid LOH fragmentation and Gen2 GC pressure.</param>
    public BatchArena(int capacity)
    {
        _buffer = GC.AllocateUninitializedArray<byte>(capacity, pinned: true);
        _position = 0;
    }

    /// <summary>
    /// Rents an arena from the pool or creates a new one.
    /// Pooled arenas retain their POH buffers for the pool's lifetime - no ArrayPool rent/return overhead.
    /// </summary>
    public static BatchArena RentOrCreate(int capacity)
    {
        if (s_pool.TryDequeue(out var arena))
        {
            Interlocked.Decrement(ref s_poolCount);
            arena.Reset(capacity);
            return arena;
        }
        Interlocked.Increment(ref s_misses);
        return new BatchArena(capacity);
    }

    /// <summary>
    /// Returns an arena to the pool for reuse, or drops the reference for GC collection if the pool is full.
    /// The POH buffer is never returned to ArrayPool.
    /// </summary>
    public static void ReturnToPool(BatchArena arena)
    {
        arena._position = 0;

        if (Interlocked.Increment(ref s_poolCount) <= Volatile.Read(ref s_maxPoolSize))
        {
            s_pool.Enqueue(arena);
        }
        else
        {
            Interlocked.Decrement(ref s_poolCount);
            // POH buffer — drop the reference so the GC can reclaim the POH segment
            // once all objects on it are dead. No ArrayPool return needed.
            arena._buffer = null!;
        }
    }

    /// <summary>
    /// Resets the arena for reuse. Keeps the existing buffer if it's large enough,
    /// otherwise allocates a new permanent buffer.
    /// </summary>
    private void Reset(int capacity)
    {
        if (_buffer is not null && _buffer.Length >= capacity)
        {
            // Skip clearing — _position reset to 0 means stale bytes are never read,
            // and the next batch overwrites from position 0.
            _position = 0;
            return;
        }

        // Buffer too small (or null) - allocate a new permanent buffer.
        // The old buffer (if any) will be collected by GC.
        _buffer = GC.AllocateUninitializedArray<byte>(capacity, pinned: true);
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
    /// Releases the arena's buffer reference.
    /// Defensive nulling to prevent use-after-return — the arena is single-owner
    /// at this point so a plain write is sufficient (no atomic exchange needed).
    /// The buffer is permanently allocated (not from ArrayPool), so it is simply
    /// released for GC collection.
    /// </summary>
    public void Return()
    {
        _buffer = null!;
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
    // ReadyBatch lifecycle (seal→send→response→cleanup) is longer than PartitionBatch
    // (create→fill→seal), so its pool needs proportionally more capacity.
    private const int ReadyBatchPoolSizeRatio = 2;

    private readonly ProducerOptions _options;
    private readonly CompressionCodecRegistry? _compressionCodecs;

    /// <summary>
    /// Per-partition deques of sealed ReadyBatches, matching Java's
    /// ConcurrentMap&lt;TopicPartition, Deque&lt;ProducerBatch&gt;&gt;.
    /// Accessed by producer threads (AddLast under lock) and sender thread (drain).
    /// </summary>
    private readonly ConcurrentDictionary<TopicPartition, PartitionDeque> _partitionDeques = new();

    /// <summary>
    /// Muted partitions — skipped by Ready() and Drain().
    /// ConcurrentDictionary for thread safety: BrokerSender threads call MutePartition/UnmutePartition
    /// while the sender thread reads via Contains in Ready/Drain.
    /// </summary>
    private readonly ConcurrentDictionary<TopicPartition, byte> _mutedPartitions = new();

    /// <summary>
    /// Per-broker drain index for fair round-robin partition ordering.
    /// Matches Java's nodesDrainIndex HashMap.
    /// </summary>
    private readonly Dictionary<int, int> _drainIndex = new();

    /// <summary>
    /// Signaled when new data is available for the sender loop to drain.
    /// Set by seal paths and reenqueue. Sender loop resets after wake.
    /// Zero-allocation after warmup (reuses timer, one-time shutdown registration).
    /// </summary>
    private readonly AsyncAutoResetSignal _wakeupSignal = new();

    // Per-partition-affine append workers: each worker owns a channel and processes
    // appends for a subset of partitions (partition % workerCount). This reduces
    // contention on ConcurrentDictionary lookups when many threads fall through
    // to the slow (async) append path.
    // Workers are started lazily on first EnqueueAppend call to avoid creating
    // dedicated threads for fire-and-forget producers that never use the async path.
    private readonly Channel<AppendWorkItem>[] _appendWorkerChannels;
    private readonly int _appendWorkerCount;
    private volatile Task[]? _appendWorkerTasks;
    private CancellationToken _appendWorkerCancellationToken;
    private int _appendWorkersReady;
    private int _appendWorkersStarted;

    private readonly PartitionBatchPool _batchPool;
    private readonly ReadyBatchPool _readyBatchPool; // Pool for ReadyBatch objects to eliminate per-batch allocations

    // O(1) counter for fast flush-check (is anything in-flight?).
    // The separate _inFlightBatches dictionary provides reference-tracking for orphan sweep.
    private long _inFlightBatchCount;

    // O(1) counter tracking the number of partition deques with an unsealed CurrentBatch.
    // Replaces O(n) enumeration of _partitionDeques in HasUnsealedBatches().
    // Incremented when pd.CurrentBatch is set to a new batch, decremented when set to null.
    private int _unsealedBatchCount;
    // TCS for async waiting - created on-demand, completed when counter reaches 0
    // Using TCS instead of ManualResetEventSlim avoids polling and ThreadPool starvation
    // Not volatile - use Volatile.Read/Interlocked for thread-safe access
    private TaskCompletionSource<bool>? _flushTcs;

    // Reference-tracking for in-flight batches. Enables forceful cleanup of orphaned batches
    // during disposal — catches batches whose references were lost from BrokerSender data structures.
    // Per-batch cost (not per-message): one TryAdd/TryRemove per batch, amortized over ~1000 messages.
    private readonly ConcurrentDictionary<ReadyBatch, byte> _inFlightBatches = new(concurrencyLevel: Environment.ProcessorCount, capacity: 16);

    // Optimization: Track the oldest batch creation time to skip unnecessary enumeration.
    // With LingerMs=5ms and 1ms timer, we'd enumerate 5x per batch without this optimization.
    // By tracking the oldest batch, we can skip enumeration when no batch is old enough to flush.
    // Uses Stopwatch ticks for high-resolution timing. long.MaxValue means no batches exist.
    private long _oldestBatchCreatedTicks = long.MaxValue;

    // Track whether there are pending awaited produces (ProduceAsync with completion sources).
    // These need immediate flushing via ShouldFlush() regardless of LingerMs, so we can't
    // skip enumeration when this counter is non-zero.
    private int _pendingAwaitedProduceCount;

    // Push-based notification queue for partitions with sealed batches ready to send.
    // Populated by SealCurrentBatchUnderLock, SealBatchesAsync, and Reenqueue when a batch
    // enters a partition deque. Drained by Ready() instead of scanning all _partitionDeques,
    // converting O(n_partitions) to O(n_ready_partitions) per sender cycle.
    // ConcurrentQueue is lock-free and safe for multi-producer (append workers, linger timer)
    // single-consumer (sender thread) usage.
    //
    // Duplicate entries for the same partition are harmless: Ready() dequeues and checks the
    // partition deque, so extra notifications for an already-drained partition are no-ops.
    // The queue is bounded: each partition appears at most once per seal event, muted
    // partitions are dropped (not re-enqueued), and UnmutePartition re-enqueues only if
    // sealed batches exist. Total size <= number of sealed batches <= BufferMemory / BatchSize.
    private readonly ConcurrentQueue<TopicPartition> _readyPartitions = new();

    // Coordination lock between FlushAsyncCore and ExpireLingerAsyncCore.
    // Ensures linger and flush don't both seal batches simultaneously,
    // which could cause ordering or double-seal issues.
    private readonly SemaphoreSlim _flushLingerLock = new(1, 1);

    private readonly ILogger _logger;

    private volatile bool _disposed;
    private volatile bool _closed;

    /// <summary>
    /// True after CloseAsync has been called. Used by the sender loop to know
    /// when to exit after draining remaining batches.
    /// </summary>
    internal bool Closed => _closed;

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

    /// <summary>
    /// Resets sequence numbers for specific partitions only (Java-style per-partition reset).
    /// Called during client-side epoch bump for idempotent producers — only the partitions
    /// that triggered OOSN/InvalidProducerEpoch need their sequences reset to 0.
    /// Unaffected partitions keep their current sequence counters; the broker carries
    /// forward per-partition sequence state across epoch bumps (KIP-360).
    /// </summary>
    internal void ResetSequencesForPartitions(IReadOnlyCollection<TopicPartition> partitions)
    {
        foreach (var tp in partitions)
            _sequenceNumbers.TryRemove(tp, out _);
    }

    // Buffer memory tracking for backpressure
    private readonly ulong _maxBufferMemory;
    private long _bufferedBytes;
    // Adaptive connection scaling: counts slow-path entries in ReserveMemorySync/Async
    private long _bufferPressureEvents;
    // FIFO waiter queue: each blocked thread gets its own SyncWaiterNode so
    // ReleaseMemory wakes waiters one-at-a-time instead of broadcasting (thundering herd).
    private readonly ConcurrentQueue<SyncWaiterNode> _syncWaiterQueue = new();
    private readonly CancellationTokenSource _disposalCts = new();
    // Async signal for ReserveMemoryAsync — SemaphoreSlim(0,1) used as async auto-reset event.
    // ReleaseMemory signals this so async waiters wake instantly instead of polling with Task.Delay.
    // Note: SemaphoreSlim is retained here (not AsyncAutoResetSignal) because ReserveMemoryAsync
    // can have multiple concurrent async waiters from parallel ProduceAsync calls.
    // AsyncAutoResetSignal is single-waiter only. The per-call allocation from WaitAsync is
    // acceptable: it only occurs on the backpressure slow path (buffer full), not per-message.
    private readonly SemaphoreSlim _asyncBufferSpaceSignal = new(0, 1);

    /// <summary>
    /// Per-partition state matching Java's Deque&lt;ProducerBatch&gt; design.
    /// The deque holds sealed ReadyBatches waiting to be drained by the sender loop.
    /// CurrentBatch is the unsealed batch accepting new records.
    /// Thread-safety: all access to Deque and CurrentBatch must be under Lock.
    /// Uses a ring-buffer backing array instead of LinkedList to eliminate per-batch
    /// LinkedListNode and HashSet allocations. Typical deques hold 1-3 batches.
    /// </summary>
    private sealed class PartitionDeque
    {
        private ReadyBatch?[] _items = new ReadyBatch?[4];
        private int _head;
        private int _count;

        /// <summary>Per-partition lock for deque access (matches Java's synchronized(deque)).
        /// SpinLock avoids kernel transitions for the brief critical sections in append/drain paths.</summary>
        public SpinLock Lock = new(enableThreadOwnerTracking: false);

        /// <summary>Current unsealed batch accepting new records. Null if no active batch.</summary>
        public PartitionBatch? CurrentBatch;

        /// <summary>Number of batches in the deque.</summary>
        public int Count => _count;

        private const int MinCapacity = 4;

        /// <summary>Remove and return the first batch (oldest). Returns null if empty.</summary>
        public ReadyBatch? PollFirst()
        {
            if (_count == 0) return null;
            var item = _items[_head]!;
            _items[_head] = null;
            _head = (_head + 1) % _items.Length;
            _count--;

            // Shrink if utilization drops below 25% and array is above minimum capacity.
            // Prevents sticky peak allocation from temporary bursts (e.g., network partition).
            if (_items.Length > MinCapacity && _count <= _items.Length / 4)
                Shrink();

            return item;
        }

        /// <summary>Return the first batch without removing. Returns null if empty.</summary>
        public ReadyBatch? PeekFirst()
        {
            return _count == 0 ? null : _items[_head];
        }

        /// <summary>Add to back of deque (normal seal path).</summary>
        public void AddLast(ReadyBatch batch)
        {
            EnsureCapacity();
            _items[(_head + _count) % _items.Length] = batch;
            _count++;
        }

        /// <summary>Add to front of deque (retry/reenqueue — Java's addFirst).</summary>
        public void AddFirst(ReadyBatch batch)
        {
            EnsureCapacity();
            _head = (_head - 1 + _items.Length) % _items.Length;
            _items[_head] = batch;
            _count++;
        }

        /// <summary>Whether the deque contains the specified batch. O(n) linear scan.
        /// Only used in diagnostic/orphan-sweep paths, not hot append/drain.</summary>
        public bool Contains(ReadyBatch batch)
        {
            for (var i = 0; i < _count; i++)
            {
                if (ReferenceEquals(_items[(_head + i) % _items.Length], batch))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Insert a batch in ascending sequence order for idempotent reenqueue.
        /// Walks to the first position that is unsequenced (&lt; 0) or &gt;= batch's sequence,
        /// then inserts before it to maintain ascending sequence order.
        /// </summary>
        public void InsertInSequenceOrder(ReadyBatch batch)
        {
            if (batch.RecordBatch.BaseSequence < 0)
            {
                AddFirst(batch);
                return;
            }

            // Find insertion position
            var insertAt = _count;
            for (var i = 0; i < _count; i++)
            {
                var existing = _items[(_head + i) % _items.Length]!;
                if (existing.RecordBatch.BaseSequence < 0 ||
                    existing.RecordBatch.BaseSequence >= batch.RecordBatch.BaseSequence)
                {
                    insertAt = i;
                    break;
                }
            }

            if (insertAt == _count)
            {
                AddLast(batch);
                return;
            }

            // Shift elements right to make room at insertAt.
            // O(n) shift is acceptable — this path is only taken on idempotent retry reenqueue,
            // and typical deque depth is 1-3 batches.
            EnsureCapacity();
            for (var i = _count; i > insertAt; i--)
            {
                _items[(_head + i) % _items.Length] = _items[(_head + i - 1) % _items.Length];
            }
            _items[(_head + insertAt) % _items.Length] = batch;
            _count++;
        }

        private void EnsureCapacity()
        {
            if (_count < _items.Length) return;
            var newItems = new ReadyBatch?[_items.Length * 2];
            for (var i = 0; i < _count; i++)
            {
                newItems[i] = _items[(_head + i) % _items.Length];
            }
            _items = newItems;
            _head = 0;
        }

        private void Shrink()
        {
            var newCapacity = Math.Max(MinCapacity, _items.Length / 2);
            if (newCapacity >= _items.Length) return;
            var newItems = new ReadyBatch?[newCapacity];
            for (var i = 0; i < _count; i++)
            {
                newItems[i] = _items[(_head + i) % _items.Length];
            }
            _items = newItems;
            _head = 0;
        }
    }

    private PartitionDeque GetOrCreateDeque(TopicPartition tp)
        => _partitionDeques.GetOrAdd(tp, static _ => new PartitionDeque());

    /// <summary>
    /// Drains the push-based notification queue to find partitions with sendable data.
    /// Populates the caller-provided readyNodes set with broker IDs that have at least one
    /// partition whose head batch is sendable (sealed by append overflow, linger expiry, or flush).
    /// Only called from the sender thread.
    ///
    /// Complexity is O(n_ready_partitions) instead of O(n_all_partitions) because only partitions
    /// that had a batch sealed or reenqueued are in the notification queue.
    /// </summary>
    internal (int NextCheckDelayMs, bool UnknownLeadersExist) Ready(
        MetadataManager metadataManager, HashSet<int> readyNodes)
    {
        var unknownLeadersExist = false;

        // Snapshot the current queue length to avoid infinite loop: partitions that need
        // re-enqueue (backoff, unknown leader) are added back during the loop, but we only
        // process items that were present at the start of this call.
        var count = _readyPartitions.Count;

        for (var i = 0; i < count; i++)
        {
            var dequeued = _readyPartitions.TryDequeue(out var tp);
            Debug.Assert(dequeued, "TryDequeue failed despite being the sole consumer of _readyPartitions");

            if (_mutedPartitions.ContainsKey(tp))
            {
                // Drop the notification; UnmutePartition() will re-enqueue if the
                // partition still has sealed batches. This avoids unbounded queue
                // growth while a partition stays muted across many sender cycles.
                continue;
            }

            var pd = _partitionDeques.GetValueOrDefault(tp);
            if (pd is null)
                continue;

            ReadyBatch? head;
            {
                using var guard = new SpinLockGuard(ref pd.Lock);
                head = pd.PeekFirst();
            }

            if (head is null)
                continue;

            // Check retry backoff
            if (head.IsRetry && head.RetryNotBefore > 0)
            {
                var backoffRemaining = head.RetryNotBefore - Stopwatch.GetTimestamp();
                if (backoffRemaining > 0)
                {
                    var backoffMs = (int)(backoffRemaining * 1000 / Stopwatch.Frequency);

                    // Defer re-enqueue until backoff expires to avoid per-cycle churn.
                    // The timer fires once, re-enqueues the partition, and wakes the sender.
                    // One Task.Delay allocation per retry batch (not per message) is acceptable.
                    DeferReenqueue(tp, backoffMs);
                    continue;
                }
            }

            // Find leader for this partition
            var leader = metadataManager.TryGetCachedPartitionLeader(tp.Topic, tp.Partition);
            if (leader is null)
            {
                // Sealed batch exists but leader is unknown (e.g., after partition expansion).
                // Signal the sender to trigger a metadata refresh, matching Java's
                // RecordAccumulator.ready() unknownLeadersExist behavior.
                unknownLeadersExist = true;

                // Re-enqueue so the sender loop retries after metadata refresh.
                _readyPartitions.Enqueue(tp);
                continue;
            }

            // Sealed batches in the deque are always sendable. The linger/micro-linger
            // timer already determined readiness when it sealed the batch, so re-checking
            // linger here would double-count the wait (e.g., a batch sealed by micro-linger
            // after 1ms would wait another 5000ms with lingerMs=5000).
            // Only retry backoff (handled above) can delay a sealed batch.
            readyNodes.Add(leader.NodeId);
        }

        // With push-based notifications, the sender wakes on SignalWakeup() from batch
        // sealing or DeferReenqueue timer expiry. The 100ms fallback is a safety-net poll
        // in case a notification is missed (e.g., during disposal races).
        return (100, unknownLeadersExist);
    }

    /// <summary>
    /// Drains one batch per partition for each ready broker, matching Java's RecordAccumulator.drain().
    /// Populates caller-owned <paramref name="result"/> with per-broker batch lists.
    /// Only called from the sender thread.
    /// </summary>
    /// <param name="metadataManager">Metadata manager for partition-to-broker mapping.</param>
    /// <param name="readyNodes">Broker IDs with sendable data (from <see cref="Ready"/>).</param>
    /// <param name="maxRequestSize">Maximum request size in bytes.</param>
    /// <param name="result">Caller-owned dictionary to populate. Must be empty on entry.</param>
    /// <param name="batchListPool">LIFO pool of reusable batch lists to avoid per-call allocations.</param>
    internal void Drain(
        MetadataManager metadataManager,
        HashSet<int> readyNodes,
        int maxRequestSize,
        Dictionary<int, List<ReadyBatch>> result,
        Stack<List<ReadyBatch>> batchListPool)
    {
        foreach (var nodeId in readyNodes)
        {
            // Get a list from the pool or create a new one
            var batches = batchListPool.Count > 0
                ? batchListPool.Pop()
                : new List<ReadyBatch>();

            DrainBatchesForOneNode(metadataManager, nodeId, maxRequestSize, batches);
            if (batches.Count > 0)
            {
                result[nodeId] = batches;
            }
            else
            {
                // Return unused list to pool
                batchListPool.Push(batches);
            }
        }
    }

    private void DrainBatchesForOneNode(
        MetadataManager metadataManager,
        int nodeId,
        int maxRequestSize,
        List<ReadyBatch> ready)
    {
        var partitions = metadataManager.GetPartitionsForNode(nodeId);
        if (partitions.Count == 0)
            return;

        if (!_drainIndex.TryGetValue(nodeId, out var startIndex))
            startIndex = 0;

        var size = 0;
        var count = partitions.Count;
        var now = Stopwatch.GetTimestamp();
        var lastDrainIndex = startIndex;

        for (var i = 0; i < count; i++)
        {
            var idx = (startIndex + i) % count;
            var tp = partitions[idx];

            lastDrainIndex = (startIndex + i + 1) % count;

            if (_mutedPartitions.ContainsKey(tp))
                continue;

            var pd = _partitionDeques.GetValueOrDefault(tp);
            if (pd is null)
                continue;

            ReadyBatch? batch;
            {
                using var guard = new SpinLockGuard(ref pd.Lock);
                batch = pd.PeekFirst();
                if (batch is null)
                    continue;

                if (batch.IsRetry && batch.RetryNotBefore > 0
                    && now < batch.RetryNotBefore)
                    continue;

                if (ready.Count > 0 && size + batch.DataSize > maxRequestSize)
                {
                    // Ready() already consumed notifications for all partitions on this node.
                    // Re-enqueue this and remaining partitions so they aren't orphaned.
                    // No filtering needed: Ready()/Drain will re-validate on the next cycle,
                    // and duplicate entries in _readyPartitions are harmless (documented invariant).
                    ReenqueueSkippedPartitions(partitions, startIndex, i, count);
                    break;
                }

                batch = pd.PollFirst();

                // If more sealed batches remain, re-enqueue so they're drained on
                // the next cycle. Ready() already consumed the notification for this
                // partition. The mute/unmute cycle would eventually re-enqueue via
                // UnmutePartition, but there is a timing gap between drain and mute
                // where the sender could sleep with no pending notifications.
                if (pd.PeekFirst() is not null)
                    _readyPartitions.Enqueue(tp);
            }

            if (batch is not null)
            {
                batch.AppendDiag('D');
                size += batch.DataSize;
                ready.Add(batch);
            }
        }

        _drainIndex[nodeId] = lastDrainIndex;
    }

    /// <summary>
    /// Re-enqueues notifications for partitions skipped by <see cref="DrainBatchesForOneNode"/>
    /// when the maxRequestSize limit is hit. Ready() already consumed their notifications, so
    /// without re-enqueue these partitions' sealed batches would be orphaned — never drained
    /// and never re-notified (they aren't muted, so UnmutePartition won't fire for them).
    /// Blind re-enqueue without locking: Ready()/Drain re-validate all conditions on the next
    /// cycle, and duplicate entries are harmless (documented ConcurrentQueue invariant).
    /// </summary>
    private void ReenqueueSkippedPartitions(
        IReadOnlyList<TopicPartition> partitions, int startIndex, int fromOffset, int count)
    {
        for (var j = fromOffset; j < count; j++)
            _readyPartitions.Enqueue(partitions[(startIndex + j) % count]);
    }

    /// <summary>
    /// Puts a failed batch back at the front of its partition deque for retry.
    /// Matches Java's RecordAccumulator.reenqueue() with addFirst.
    /// For idempotent producers, uses insertInSequenceOrder to maintain sequence ordering.
    /// </summary>
    internal void Reenqueue(ReadyBatch batch, long nowMs)
    {
        batch.Reenqueued(nowMs);
        var pd = GetOrCreateDeque(batch.TopicPartition);
        {
            using var guard = new SpinLockGuard(ref pd.Lock);
            if (ProducerId >= 0)
                pd.InsertInSequenceOrder(batch);
            else
                pd.AddFirst(batch);
        }

        // Notify Ready() that this partition has a sendable batch.
        _readyPartitions.Enqueue(batch.TopicPartition);
        SignalWakeup();
    }

    internal void MutePartition(TopicPartition tp) => _mutedPartitions.TryAdd(tp, 0);

    internal void UnmutePartition(TopicPartition tp)
    {
        // TryRemove must precede Enqueue to ensure Ready() does not observe the notification
        // but then find the partition still muted (which would cause a wasted re-enqueue cycle).
        // This ordering is safe because Ready() is single-consumer (sender thread) and
        // UnmutePartition is also called from the sender thread, so there is no concurrent
        // race between the remove and the enqueue.
        _mutedPartitions.TryRemove(tp, out _);

        // Re-notify so Ready() picks up any sealed batches that were skipped while muted.
        if (_partitionDeques.TryGetValue(tp, out var pd))
        {
            bool hasBatches;
            {
                using var guard = new SpinLockGuard(ref pd.Lock);
                hasBatches = pd.PeekFirst() is not null;
            }
            if (hasBatches)
                _readyPartitions.Enqueue(tp);
        }

        SignalWakeup(); // Wake sender loop so it can drain the newly-unmuted partition
    }
    internal bool IsMuted(TopicPartition tp) => _mutedPartitions.ContainsKey(tp);

    internal void SignalWakeup()
    {
        _wakeupSignal.Signal();
    }

    /// <summary>
    /// Schedules a partition to be re-enqueued into <see cref="_readyPartitions"/> after
    /// <paramref name="delayMs"/> milliseconds. Uses a fire-and-forget <see cref="Task.Delay"/>
    /// so the sender loop is not churning on partitions still in retry backoff.
    /// One allocation per retry batch (not per message) — acceptable per allocation guidelines.
    /// Respects <see cref="_disposalCts"/> so the timer cancels promptly on disposal,
    /// preventing reference leaks from captured state keeping the accumulator alive.
    /// </summary>
    private void DeferReenqueue(TopicPartition tp, int delayMs)
    {
        _ = Task.Delay(Math.Max(delayMs, 1), _disposalCts.Token).ContinueWith(static (t, state) =>
        {
            if (t.IsCanceled) return;
            var (self, partition) = ((RecordAccumulator, TopicPartition))state!;
            self._readyPartitions.Enqueue(partition);
            self.SignalWakeup();
        }, (this, tp), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>
    /// Releases a semaphore if not already signaled, ignoring disposal and max-count races.
    /// The CurrentCount check avoids throwing SemaphoreFullException on every call under
    /// normal (no-waiter) operation. The rare TOCTOU race (another thread releases between
    /// check and Release) is harmless — it just means a redundant signal, caught by SFE.
    /// </summary>
    private static void TryReleaseSemaphore(SemaphoreSlim semaphore)
    {
        try
        {
            if (semaphore.CurrentCount == 0)
                semaphore.Release();
        }
        catch (ObjectDisposedException) { }
        catch (SemaphoreFullException) { }
    }

    internal ValueTask<bool> WaitForWakeupAsync(int timeoutMs)
    {
        return _wakeupSignal.WaitAsync(timeoutMs);
    }

    /// <summary>
    /// Registers a shutdown token with the wakeup signal so cancellation wakes the sender loop
    /// without per-wait allocation. Must be called once before the sender loop starts waiting.
    /// </summary>
    internal void RegisterWakeupShutdownToken(CancellationToken cancellationToken)
    {
        _wakeupSignal.RegisterShutdownToken(cancellationToken);
    }

    public RecordAccumulator(ProducerOptions options, CompressionCodecRegistry? compressionCodecs = null, ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _options = options;
        _compressionCodecs = compressionCodecs;

        // Scale pool sizes with BufferMemory/BatchSize to prevent pool exhaustion.
        // Small batches (e.g., 16KB) create high batch churn (~6,250/sec at 100K msg/sec)
        // and need larger pools than the default 256 designed for 1MB batches.
        var poolSize = ComputePoolSize(options);
        BatchArena.RatchetPoolSize(poolSize);
        // ReadyBatch lifecycle spans seal→send→response→cleanup (longer than PartitionBatch),
        // so its pool needs to be larger to avoid exhaustion under sustained throughput.
        _readyBatchPool = new ReadyBatchPool(maxPoolSize: poolSize * ReadyBatchPoolSizeRatio);
        _batchPool = new PartitionBatchPool(options, maxPoolSize: poolSize);
        _batchPool.SetReadyBatchPool(_readyBatchPool); // Wire up pools
        _maxBufferMemory = options.BufferMemory;

        // Pre-warm pools to eliminate ramp-up allocation bursts.
        // ReadyBatch is lightweight (no arena), so fully warm it.
        // PartitionBatch and BatchArena each allocate a ~BatchSize POH buffer,
        // so pre-warm only a fraction (1/4) to avoid excessive upfront memory usage
        // (e.g., poolSize=512 × 1MB = 512MB if fully warmed).
        _readyBatchPool.PreWarm(poolSize * ReadyBatchPoolSizeRatio);
        _batchPool.PreWarm(poolSize / 4);
        BatchArena.PreWarm(poolSize / 4, options.BatchSize);

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
    /// Computes the recommended pool size based on producer options.
    /// Scales with BufferMemory/BatchSize to prevent pool exhaustion under high batch churn.
    /// At high throughput, batches cycle through: create → fill → seal → send → response → cleanup → pool.
    /// The pool must cover peak in-flight batch count to avoid heap allocations.
    /// </summary>
    internal static int ComputePoolSize(ProducerOptions options)
    {
        // BufferMemory / BatchSize gives the max batch count the buffer can hold.
        // Divide by 2: under sustained load, batches span multiple lifecycle phases
        // (filling, sealed/queued, in-flight, awaiting ack, cleanup). Profiling showed
        // that /4 caused pool overflow with default settings (2GB/1MB = 512), leading to
        // ~3.9GB POH allocation churn and 15 Gen2 GCs in 2-minute stress tests.
        // /2 provides sufficient headroom for peak in-flight counts without excessive retention.
        var batchCapacity = (int)Math.Min(options.BufferMemory / (ulong)Math.Max(options.BatchSize, 1), int.MaxValue);
        return Math.Clamp(batchCapacity / 2, BatchArena.DefaultPoolSize, BatchArena.MaxPoolSizeCap);
    }

    /// <summary>
    /// Drains one ReadyBatch from any non-empty partition deque.
    /// Used by tests to simulate sender loop draining without MetadataManager.
    /// </summary>
    internal bool TryDrainBatch([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ReadyBatch? batch)
    {
        foreach (var kvp in _partitionDeques)
        {
            var pd = kvp.Value;
            {
                using var guard = new SpinLockGuard(ref pd.Lock);
                var b = pd.PollFirst();
                if (b is not null)
                {
                    batch = b;
                    return true;
                }
            }
        }

        batch = null;
        return false;
    }

    /// <summary>
    /// Returns a ReadyBatch to the pool for reuse.
    /// Called by KafkaProducer after batch is processed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReturnReadyBatch(ReadyBatch batch)
    {
        // Atomic guard: only the first caller returns the batch to the pool.
        // Multiple paths may attempt to return the same batch (e.g., BrokerSender.CleanupBatch
        // racing with ForceFailAllInFlightBatches during disposal). Double-return to the pool
        // causes two renters to share the same object — silent data corruption.
        if (Interlocked.Exchange(ref batch._returnedToPool, 1) != 0)
            return;

        _readyBatchPool.Return(batch);
    }

    /// <summary>
    /// Starts per-partition-affine append worker tasks.
    /// Each worker processes appends for partitions where (partition % workerCount == workerIndex),
    /// enabling cross-partition parallelism while preserving per-partition ordering.
    /// </summary>
    internal void StartAppendWorkers(CancellationToken cancellationToken)
    {
        // Store the token for lazy start. Workers are created on first EnqueueAppend call
        // to avoid allocating dedicated threads for fire-and-forget producers.
        // CancellationToken is a struct containing a single CancellationTokenSource reference;
        // its copy is a pointer-width write, atomic on all .NET platforms.
        // Volatile.Write on _appendWorkersReady provides a release fence, ensuring the token
        // store is visible to threads that read _appendWorkersReady via Volatile.Read (acquire).
        _appendWorkerCancellationToken = cancellationToken;
        Volatile.Write(ref _appendWorkersReady, 1);
    }

    private void EnsureAppendWorkersStarted()
    {
        if (Volatile.Read(ref _appendWorkersStarted) != 0)
            return;

        // Ensure StartAppendWorkers has been called (token is stored).
        // Without workers, items sit in the channel forever and the caller's
        // completion source never resolves, causing ProduceAsync to hang.
        // Skip check if disposed — the channel is closed and TryWrite will fail
        // with ObjectDisposedException, which is the expected post-disposal behavior.
        if (Volatile.Read(ref _appendWorkersReady) == 0)
        {
            if (_disposed)
                return; // Let TryWrite fail with ObjectDisposedException
            throw new InvalidOperationException("EnqueueAppend called before StartAppendWorkers");
        }

        if (Interlocked.CompareExchange(ref _appendWorkersStarted, 1, 0) != 0)
            return;

        // Build the array locally, then publish atomically so DisposeAsync
        // never observes a partially-populated array with null Task entries.
        // The field is volatile, so the assignment is a release-fence store.
        var tasks = new Task[_appendWorkerCount];
        for (var i = 0; i < _appendWorkerCount; i++)
        {
            var workerIndex = i;
            tasks[i] = Task.Factory.StartNew(
                () => ProcessAppendWorkerAsync(workerIndex, _appendWorkerCancellationToken),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
        }
        _appendWorkerTasks = tasks;
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
                workItem.CancellationToken.ThrowIfCancellationRequested();

                if (!Append(
                    workItem.Topic,
                    workItem.Partition,
                    workItem.Timestamp,
                    workItem.Key,
                    workItem.Value,
                    workItem.Headers,
                    workItem.HeaderCount,
                    workItem.Completion,
                    null))
                {
                    CleanupWorkItemResources(in workItem);
                    workItem.Completion.TrySetException(new ObjectDisposedException(nameof(RecordAccumulator)));
                }
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
        if (workItem.Headers is not null)
        {
            ArrayPool<Header>.Shared.Return(workItem.Headers);
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
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata> completion,
        CancellationToken cancellationToken)
    {
        EnsureAppendWorkersStarted();
        var workerIndex = (int)((uint)partition % (uint)_appendWorkerCount);
        var workItem = new AppendWorkItem(topic, partition, timestamp, key, value,
            headers, headerCount, completion, cancellationToken);

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

    // Single thread-local cache consolidating all per-thread deque lookup state.
    // Reduces 3 separate [ThreadStatic] lookups to 1.
    // Per-thread one-slot cache for GetOrCreateDeque to avoid ConcurrentDictionary hash+lookup
    // on every message. The cache stores (accumulator instance, TopicPartition, PartitionDeque).
    // Hit rate is high in partition-affine append workers and single-partition scenarios.
    // Note: holds a strong reference to the accumulator until the thread reuses the cache slot.
    // This is acceptable because producers are typically singleton-lifetime objects.
    // IMPORTANT: This cache is only valid because _partitionDeques never removes entries.
    // If partition eviction is ever added, the cache must be invalidated on removal.
    [ThreadStatic]
    private static AccumulatorThreadCache? t_cache;

    /// <summary>
    /// Holds per-thread cached state for partition deque lookups.
    /// Consolidating into a single class reduces thread-static lookup overhead from 3 to 1.
    /// </summary>
    private sealed class AccumulatorThreadCache
    {
        public RecordAccumulator? CachedAccumulator;
        public TopicPartition CachedTopicPartition;
        public PartitionDeque? CachedDeque;
    }

    /// <summary>
    /// Gets or creates the PartitionDeque for a topic-partition pair.
    /// Uses a per-thread one-slot cache to avoid ConcurrentDictionary lookup when the
    /// same thread repeatedly accesses the same partition (common in append workers).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PartitionDeque GetOrCreateDeque(string topic, int partition)
    {
        var tp = new TopicPartition(topic, partition);

        // Fast path: check thread-local cache (skip if disposed to avoid serving stale data)
        var cache = t_cache ??= new AccumulatorThreadCache();
        if (cache.CachedAccumulator == this && !_disposed && cache.CachedTopicPartition == tp && cache.CachedDeque is { } cached)
            return cached;

        return GetOrCreateDequeSlow(tp, cache);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private PartitionDeque GetOrCreateDequeSlow(TopicPartition tp, AccumulatorThreadCache cache)
    {
        var deque = _partitionDeques.GetOrAdd(tp, static _ => new PartitionDeque());

        // Update thread-local cache
        cache.CachedAccumulator = this;
        cache.CachedTopicPartition = tp;
        cache.CachedDeque = deque;

        return deque;
    }

    /// <summary>
    /// Unified append method matching Java's RecordAccumulator.append().
    /// All produce paths (ProduceAsync, Send, Send+callback) go through this single method.
    /// Synchronized on the per-partition deque lock to guarantee ordering.
    /// </summary>
    /// <returns>true if appended successfully, false if the accumulator is disposed.</returns>
    internal bool Append(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback)
    {
        if (_disposed)
            return false;

        var recordSize = PartitionBatch.EstimateRecordSize(key.Length, value.Length, headers, headerCount);
        ReserveMemorySync(recordSize);

        // Track pending awaited produce BEFORE append to prevent race condition:
        // Without this, ExpireLingerAsync could see _pendingAwaitedProduceCount == 0
        // after the message is in the batch but before the counter is incremented.
        if (completionSource is not null)
            Interlocked.Increment(ref _pendingAwaitedProduceCount);

        var pd = GetOrCreateDeque(topic, partition);
        ReadyBatch? sealedBatch = null;

        {
            using var guard = new SpinLockGuard(ref pd.Lock);

            // Check disposal under lock
            if (_disposed)
            {
                if (completionSource is not null)
                    Interlocked.Decrement(ref _pendingAwaitedProduceCount);
                ReleaseMemory(recordSize);
                return false;
            }

            // Try append to current batch
            if (pd.CurrentBatch is { } currentBatch)
            {
                if (TryAppendToBatch(currentBatch, timestamp, key, value, headers, headerCount,
                    completionSource, callback, recordSize))
                    return true;

                // Current batch is full — seal it (compress + enqueue under lock)
                sealedBatch = SealCurrentBatchUnderLock(pd, currentBatch);
            }

            // Create new batch
            var newBatch = RentBatch(new TopicPartition(topic, partition));
            pd.CurrentBatch = newBatch;
            Interlocked.Increment(ref _unsealedBatchCount);

            // Append to new batch — must succeed since batch is empty
            if (!TryAppendToBatch(newBatch, timestamp, key, value, headers, headerCount,
                completionSource, callback, recordSize))
            {
                // Record too large for a single batch
                pd.CurrentBatch = null;
                Interlocked.Decrement(ref _unsealedBatchCount);
                _batchPool.Return(newBatch);
                if (completionSource is not null)
                    Interlocked.Decrement(ref _pendingAwaitedProduceCount);
                ReleaseMemory(recordSize);
                throw new KafkaException(ErrorCode.MessageTooLarge,
                    $"Record of size {recordSize} exceeds maximum batch size of {_options.BatchSize}");
            }
        }

        if (sealedBatch is not null)
        {
            SignalWakeup();
        }

        return true;
    }

    /// <summary>
    /// Non-blocking variant of Append for the ProduceAsync fast path.
    /// Returns false when buffer is full (caller falls back to async slow path)
    /// OR when the accumulator is disposed.
    /// </summary>
    internal bool TryAppendWithCompletion(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata> completionSource)
    {
        if (_disposed)
            return false;

        var recordSize = PartitionBatch.EstimateRecordSize(key.Length, value.Length, headers, headerCount);

        // Try non-blocking memory reservation. If buffer is full, return false so the
        // caller (ProduceAsync fast path) falls back to the async path.
        if (!TryReserveMemory(recordSize))
            return false;

        // Track pending awaited produce BEFORE append
        Interlocked.Increment(ref _pendingAwaitedProduceCount);

        var pd = GetOrCreateDeque(topic, partition);
        ReadyBatch? sealedBatch = null;

        {
            using var guard = new SpinLockGuard(ref pd.Lock);

            if (_disposed)
            {
                Interlocked.Decrement(ref _pendingAwaitedProduceCount);
                ReleaseMemory(recordSize);
                return false;
            }

            // Try append to current batch
            if (pd.CurrentBatch is { } currentBatch)
            {
                if (TryAppendToBatch(currentBatch, timestamp, key, value, headers, headerCount,
                    completionSource, null, recordSize))
                    return true;

                // Current batch is full — seal it (compress + enqueue under lock)
                sealedBatch = SealCurrentBatchUnderLock(pd, currentBatch);
            }

            // Create new batch
            var newBatch = RentBatch(new TopicPartition(topic, partition));
            pd.CurrentBatch = newBatch;
            Interlocked.Increment(ref _unsealedBatchCount);

            // Append to new batch — must succeed since batch is empty
            if (!TryAppendToBatch(newBatch, timestamp, key, value, headers, headerCount,
                completionSource, null, recordSize))
            {
                pd.CurrentBatch = null;
                Interlocked.Decrement(ref _unsealedBatchCount);
                _batchPool.Return(newBatch);
                Interlocked.Decrement(ref _pendingAwaitedProduceCount);
                ReleaseMemory(recordSize);
                throw new KafkaException(ErrorCode.MessageTooLarge,
                    $"Record of size {recordSize} exceeds maximum batch size of {_options.BatchSize}");
            }
        }

        if (sealedBatch is not null)
        {
            SignalWakeup();
        }

        return true;
    }

    /// <summary>
    /// Helper: tries to append a record to the given batch.
    /// Called under the deque lock.
    /// </summary>
    private bool TryAppendToBatch(
        PartitionBatch batch,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int estimatedSize)
    {
        var result = batch.TryAppend(timestamp, key, value, headers, headerCount, completionSource, callback, estimatedSize);

        if (result.Success)
        {
            ProducerDebugCounters.RecordMessageAppended(hasCompletionSource: completionSource is not null);
            var overestimate = estimatedSize - result.ActualSizeAdded;
            if (overestimate > 0)
                ReleaseMemory(overestimate);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Appends a record from raw span data, using arena-based zero-copy when possible.
    /// This avoids per-message ArrayPool rentals on the fire-and-forget slow path.
    /// </summary>
    /// <returns>true if appended successfully, false if the accumulator is disposed.</returns>
    internal bool AppendFromSpans(
        string topic,
        int partition,
        long timestamp,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        Header[]? headers,
        int headerCount,
        Action<RecordMetadata, Exception?>? callback)
    {
        if (_disposed)
            return false;

        var keyLength = keyIsNull ? 0 : keyData.Length;
        var valueLength = valueIsNull ? 0 : valueData.Length;
        var recordSize = PartitionBatch.EstimateRecordSize(keyLength, valueLength, headers, headerCount);
        ReserveMemorySync(recordSize);

        var pd = GetOrCreateDeque(topic, partition);
        ReadyBatch? sealedBatch = null;

        {
            using var guard = new SpinLockGuard(ref pd.Lock);

            // Check disposal under lock
            if (_disposed)
            {
                ReleaseMemory(recordSize);
                return false;
            }

            // Try append to current batch
            if (pd.CurrentBatch is { } currentBatch)
            {
                if (TryAppendFromSpansToBatch(currentBatch, timestamp, keyData, keyIsNull, valueData, valueIsNull,
                    headers, headerCount, callback, recordSize))
                    return true;

                // Current batch is full — seal it (compress + enqueue under lock)
                sealedBatch = SealCurrentBatchUnderLock(pd, currentBatch);
            }

            // Create new batch
            var newBatch = RentBatch(new TopicPartition(topic, partition));
            pd.CurrentBatch = newBatch;
            Interlocked.Increment(ref _unsealedBatchCount);

            // Append to new batch — must succeed since batch is empty
            if (!TryAppendFromSpansToBatch(newBatch, timestamp, keyData, keyIsNull, valueData, valueIsNull,
                headers, headerCount, callback, recordSize))
            {
                // Record too large for a single batch
                pd.CurrentBatch = null;
                Interlocked.Decrement(ref _unsealedBatchCount);
                _batchPool.Return(newBatch);
                ReleaseMemory(recordSize);
                throw new KafkaException(ErrorCode.MessageTooLarge,
                    $"Record of size {recordSize} exceeds maximum batch size of {_options.BatchSize}");
            }
        }

        if (sealedBatch is not null)
        {
            SignalWakeup();
        }

        return true;
    }

    /// <summary>
    /// Helper: tries to append a record from span data to the given batch using arena zero-copy.
    /// Called under the deque lock.
    /// </summary>
    private bool TryAppendFromSpansToBatch(
        PartitionBatch batch,
        long timestamp,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        Header[]? headers,
        int headerCount,
        Action<RecordMetadata, Exception?>? callback,
        int estimatedSize)
    {
        var result = batch.TryAppendFromSpans(timestamp, keyData, keyIsNull, valueData, valueIsNull,
            headers, headerCount, callback, estimatedSize);

        if (result.Success)
        {
            ProducerDebugCounters.RecordMessageAppended(hasCompletionSource: false);
            var overestimate = estimatedSize - result.ActualSizeAdded;
            if (overestimate > 0)
                ReleaseMemory(overestimate);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Seals the current batch in a partition deque and returns the ready batch.
    /// Seals, pre-compresses, tracks, and enqueues the batch — all under the caller's lock.
    ///
    /// Compression is performed under the partition SpinLock to preserve per-partition FIFO ordering.
    /// If two batches for the same partition are sealed in rapid succession (e.g., one by the append
    /// worker and one by the linger timer), releasing the lock between seal and enqueue would allow
    /// concurrent compression to complete out of order — Batch B could finish before Batch A and
    /// get enqueued first, violating ordering guarantees.
    ///
    /// The SpinLock contention from compression is acceptable because:
    /// 1. Partition-affine routing (partition % workerCount) means typically only one append thread
    ///    per partition, so lock contention is minimal.
    /// 2. The real performance win of pre-compression is moving it OFF the send loop thread —
    ///    whether it runs under a partition lock or not doesn't change that benefit.
    ///
    /// MUST be called under pd.Lock.
    /// </summary>
    private ReadyBatch? SealCurrentBatchUnderLock(PartitionDeque pd, PartitionBatch currentBatch)
    {
        var readyBatch = currentBatch.Complete();
        if (readyBatch is not null)
        {
            if (readyBatch.CompletionSourcesCount > 0)
                Interlocked.Add(ref _pendingAwaitedProduceCount, -readyBatch.CompletionSourcesCount);
            ProducerDebugCounters.RecordBatchCompleted(readyBatch.CompletionSourcesCount);

            // Pre-compress under the lock to preserve per-partition ordering.
            if (_options.CompressionType != CompressionType.None)
            {
                readyBatch.RecordBatch.PreCompress(_options.CompressionType, _compressionCodecs);
            }

            OnBatchEntersPipeline(readyBatch);
            pd.AddLast(readyBatch);
            ProducerDebugCounters.RecordBatchQueuedToReady();

            // Notify Ready() that this partition has a sendable batch.
            _readyPartitions.Enqueue(readyBatch.TopicPartition);
        }
        _batchPool.Return(currentBatch);
        pd.CurrentBatch = null;
        Interlocked.Decrement(ref _unsealedBatchCount);
        return readyBatch;
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
    /// Gets the cumulative count of buffer pressure events (slow-path entries in memory reservation).
    /// Used by adaptive connection scaling to detect sustained backpressure.
    /// </summary>
    internal long BufferPressureEvents => Volatile.Read(ref _bufferPressureEvents);

    /// <summary>
    /// Gets the current buffer utilization as a ratio (0.0 to 1.0+).
    /// Used by adaptive connection scaling to confirm buffer is actually full.
    /// </summary>
    internal double BufferUtilization => (double)Volatile.Read(ref _bufferedBytes) / (double)_maxBufferMemory;

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
    /// Throws KafkaTimeoutException if buffer space doesn't become available within MaxBlockMs.
    /// </summary>
    internal async ValueTask ReserveMemoryAsync(int recordSize, CancellationToken cancellationToken)
    {
        // Fast path: try to reserve immediately
        if (TryReserveMemory(recordSize))
        {
            return;
        }

        // Track for adaptive connection scaling
        Interlocked.Increment(ref _bufferPressureEvents);

        // Slow path: wait for space to become available with timeout protection
        // Use MaxBlockMs to limit how long we block waiting for buffer space (equivalent to Kafka's max.block.ms)
        // Protect against overflow if MaxBlockMs is configured to a very large value
        var currentBufferedBytes = Volatile.Read(ref _bufferedBytes);
        LogBufferMemoryWaiting(recordSize, currentBufferedBytes, _maxBufferMemory);
        var currentTicks = Environment.TickCount64;
        var deadline = (long.MaxValue - currentTicks > _options.MaxBlockMs)
            ? currentTicks + _options.MaxBlockMs
            : long.MaxValue;

        // Avoid CreateLinkedTokenSource allocation by using the caller's token directly
        // and checking disposal manually on each wake-up. The caller's token handles user
        // cancellation; disposal is checked explicitly at the top of each iteration.
        while (!TryReserveMemory(recordSize))
        {
            // Check disposal first
            if (_disposed)
                throw new ObjectDisposedException(nameof(RecordAccumulator));

            cancellationToken.ThrowIfCancellationRequested();

            // Check if we've exceeded max.block.ms
            var remainingMs = deadline - Environment.TickCount64;
            if (remainingMs <= 0)
                ThrowBufferMemoryTimeout(recordSize, currentTicks);

            // Wait for signal from ReleaseMemory, with timeout and caller's cancellation.
            // SemaphoreSlim.WaitAsync provides true async signal-based wake-up — no polling needed.
            // The semaphore acts as an async auto-reset event: ReleaseMemory releases, we acquire.
            // Disposal responsiveness: DisposeAsync signals _asyncBufferSpaceSignal (via
            // ReleaseMemory or direct Release) so waiters wake up and hit the _disposed check above.
            try
            {
                await _asyncBufferSpaceSignal.WaitAsync(
                    (int)Math.Min(remainingMs, int.MaxValue),
                    cancellationToken
                ).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposed)
            {
                // Caller's token was cancelled while _disposed is true — convert to
                // ObjectDisposedException for a clearer signal. Without the linked CTS,
                // this only fires on the narrow window of simultaneous user-cancel + disposal;
                // the primary disposal path is the _disposed check at loop top.
                throw new ObjectDisposedException(nameof(RecordAccumulator));
            }
            // OperationCanceledException from caller's token propagates naturally
            // WaitAsync returning false (timeout) just means we loop and check deadline above
        }
    }

    internal void ReserveMemorySync(int recordSize)
    {
        // Fast path: try to reserve immediately
        if (TryReserveMemory(recordSize))
        {
            return;
        }

        // Track for adaptive connection scaling
        Interlocked.Increment(ref _bufferPressureEvents);

        // Enqueue a FIFO waiter node and block on its individual event.
        // Only one waiter is woken per ReleaseMemory call, eliminating thundering herd.
        var currentTicks = Environment.TickCount64;
        var deadline = (long.MaxValue - currentTicks > _options.MaxBlockMs)
            ? currentTicks + _options.MaxBlockMs
            : long.MaxValue;

        while (true)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RecordAccumulator));

            var remainingMs = deadline - Environment.TickCount64;
            if (remainingMs <= 0)
                ThrowBufferMemoryTimeout(recordSize, currentTicks);

            if (TryReserveMemory(recordSize))
                break;

            // Fresh node per wait. Re-using across iterations causes double-enqueue on timeout.
            var waiter = new SyncWaiterNode();
            _syncWaiterQueue.Enqueue(waiter);

            // Re-check after enqueue: ReleaseMemory may have fired between the check
            // above and the enqueue, finding an empty queue. Without this, the thread
            // sleeps through available space until the next ReleaseMemory or timeout.
            if (TryReserveMemory(recordSize))
            {
                waiter.Cancelled = true;
                break;
            }

            waiter.Event.Wait((int)Math.Min(remainingMs, int.MaxValue));
            waiter.Cancelled = true;
        }

        // Chain-wake: if space still remains after our reservation, wake the next
        // waiter in FIFO order so it can attempt its CAS without waiting for ReleaseMemory.
        if ((ulong)Volatile.Read(ref _bufferedBytes) < _maxBufferMemory)
            WakeNextSyncWaiter();
    }

    private void ThrowBufferMemoryTimeout(int recordSize, long startTicks)
    {
        var configured = TimeSpan.FromMilliseconds(_options.MaxBlockMs);
        var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - startTicks);
        throw new KafkaTimeoutException(
            TimeoutKind.MaxBlock,
            elapsed,
            configured,
            $"Failed to allocate buffer within max.block.ms ({_options.MaxBlockMs}ms). " +
            $"Requested {recordSize} bytes, current usage: {Volatile.Read(ref _bufferedBytes)}/{_maxBufferMemory} bytes. " +
            $"Producer is generating messages faster than the network can send them. " +
            $"Consider: increasing BufferMemory, increasing MaxBlockMs, reducing production rate, or checking network connectivity.");
    }

    /// <summary>
    /// Blocks the caller while the buffer is at capacity.
    /// Unlike <see cref="ReserveMemorySync"/>, this does NOT reserve any bytes — it is a pure
    /// gate that prevents unbounded work from being queued when the buffer is full.
    /// Used by the fire-and-forget async fallback path in <c>Send()</c>, which otherwise
    /// bypasses synchronous backpressure entirely (messages are dispatched as fire-and-forget
    /// Tasks that each call <see cref="ReserveMemoryAsync"/> independently). Without this gate,
    /// a tight <c>Send()</c> loop during a metadata cache miss can queue hundreds of thousands
    /// of Tasks, saturating the thread pool and preventing the sender loop from draining batches.
    /// </summary>
    /// <remarks>
    /// <see cref="DisposeAsync"/> promptly unblocks all waiting threads via
    /// <see cref="WakeAllSyncWaiters"/>; each waiter then hits the <c>_disposed</c> check.
    /// <para/>
    /// Both this method and <see cref="ReserveMemorySync"/> use the shared FIFO waiter queue.
    /// <see cref="ReleaseMemory"/> wakes waiters one-at-a-time in FIFO order via
    /// <see cref="WakeNextSyncWaiter"/>, with chain-wake propagation when space remains.
    /// </remarks>
    internal void WaitForBufferSpace()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RecordAccumulator));

        // Fast path: buffer has space — the common case.
        if ((ulong)Volatile.Read(ref _bufferedBytes) < _maxBufferMemory)
            return;

        // Track for adaptive connection scaling
        Interlocked.Increment(ref _bufferPressureEvents);

        var startTicks = Environment.TickCount64;

        while ((ulong)Volatile.Read(ref _bufferedBytes) >= _maxBufferMemory)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RecordAccumulator));

            var elapsed = Environment.TickCount64 - startTicks;
            if (elapsed >= _options.MaxBlockMs)
                ThrowBufferFullTimeout(startTicks);

            var remainingMs = _options.MaxBlockMs - elapsed;

            var waiter = new SyncWaiterNode();
            _syncWaiterQueue.Enqueue(waiter);

            // Re-check after enqueue to avoid sleeping through freed space
            if ((ulong)Volatile.Read(ref _bufferedBytes) < _maxBufferMemory)
            {
                waiter.Cancelled = true;
                break;
            }

            waiter.Event.Wait((int)Math.Min(remainingMs, int.MaxValue));
            waiter.Cancelled = true;
        }

        // Chain-wake: if space still remains, wake the next waiter.
        if ((ulong)Volatile.Read(ref _bufferedBytes) < _maxBufferMemory)
            WakeNextSyncWaiter();
    }

    private void ThrowBufferFullTimeout(long startTicks)
    {
        var configured = TimeSpan.FromMilliseconds(_options.MaxBlockMs);
        var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - startTicks);
        throw new KafkaTimeoutException(
            TimeoutKind.MaxBlock,
            elapsed,
            configured,
            $"Buffer is full ({Volatile.Read(ref _bufferedBytes)}/{_maxBufferMemory} bytes) and did not " +
            $"drain within max.block.ms ({_options.MaxBlockMs}ms). " +
            $"Producer is generating messages faster than the network can send them. " +
            $"Consider: increasing BufferMemory, increasing MaxBlockMs, reducing production rate, or checking network connectivity.");
    }


    /// <summary>
    /// Dequeues and signals the next non-cancelled waiter in FIFO order (if any).
    /// Skips stale entries from threads that succeeded before waiting.
    /// Called from ReleaseMemory and chain-wake paths.
    /// </summary>
    private void WakeNextSyncWaiter()
    {
        while (_syncWaiterQueue.TryDequeue(out var waiter))
        {
            if (!waiter.Cancelled)
            {
                waiter.Event.Set();
                return;
            }
        }
    }

    /// <summary>
    /// Wakes ALL queued sync waiters. Used during disposal to unblock all waiting threads
    /// so they can observe <c>_disposed</c> and throw <see cref="ObjectDisposedException"/>.
    /// </summary>
    private void WakeAllSyncWaiters()
    {
        // Signal only — disposal is handled by each owning thread after Wait() returns.
        while (_syncWaiterQueue.TryDequeue(out var waiter))
            waiter.Event.Set();
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

        // Signal that space is available — wake sync waiters one-at-a-time (FIFO)
        // and async waiters via semaphore.
        WakeNextSyncWaiter();
        TryReleaseSemaphore(_asyncBufferSpaceSignal);
    }

    /// <summary>
    /// Tries to get an existing batch for the given topic-partition.
    /// Used by KafkaProducer to access the batch's arena for direct serialization.
    /// </summary>
    /// <param name="topicPartition">The topic-partition to look up.</param>
    /// <param name="batch">The batch if found, null otherwise.</param>
    /// <summary>
    /// Checks for batches that have exceeded linger time.
    /// Uses conditional removal to avoid race conditions where a new batch might be created
    /// between Complete() and TryRemove() calls.
    /// </summary>
    /// <summary>
    /// Tries to get an existing batch for the given topic-partition.
    /// Used by tests for batch introspection.
    /// </summary>
    internal bool TryGetBatch(string topic, int partition, out PartitionBatch? batch)
    {
        if (_disposed)
        {
            batch = null;
            return false;
        }

        if (_partitionDeques.TryGetValue(new TopicPartition(topic, partition), out var pd))
        {
            batch = pd.CurrentBatch;
            return batch is not null;
        }

        batch = null;
        return false;
    }

    /// <summary>
    /// Clears the current unsealed batch for the given partition, preventing double-release
    /// during disposal. Used by tests that manually call <see cref="ReleaseMemory"/>.
    /// </summary>
    internal void ClearCurrentBatch(string topic, int partition)
    {
        if (_partitionDeques.TryGetValue(new TopicPartition(topic, partition), out var pd))
        {
            using var guard = new SpinLockGuard(ref pd.Lock);
            if (pd.CurrentBatch is not null)
            {
                Interlocked.Decrement(ref _unsealedBatchCount);
                pd.CurrentBatch = null;
            }
        }
    }

    /// <remarks>
    /// Optimized with multiple fast paths:
    /// 1. Empty dictionary check - avoids enumeration overhead
    /// 2. Oldest batch age check - skips enumeration if no batch can possibly be ready
    /// With LingerMs=5ms and 1ms timer, this reduces enumeration from 5x to 1x per batch lifetime.
    /// Also uses synchronous TryWrite when possible to avoid async overhead.
    /// </remarks>
    public ValueTask ExpireLingerAsync(CancellationToken cancellationToken)
    {
        // Fast path 1: no unsealed batches to check - avoid enumeration and async overhead entirely
        if (!HasUnsealedBatches())
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
                var millisSinceOldest = (long)Stopwatch.GetElapsedTime(oldestTicks).TotalMilliseconds;
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
    /// Checks if any work remains: unsealed batches, sealed batches in deques, or
    /// in-flight batches still being sent by BrokerSenders.
    /// Used by the sender loop to decide when to exit after close.
    /// </summary>
    internal bool HasPendingWork()
    {
        if (Volatile.Read(ref _inFlightBatchCount) > 0)
            return true;

        if (Volatile.Read(ref _unsealedBatchCount) > 0)
            return true;

        // Fast path: if the ready notification queue is non-empty, there are sealed batches
        // waiting to be drained — skip the O(n) partition scan below.
        if (!_readyPartitions.IsEmpty)
            return true;

        // Still need O(n) scan for sealed-but-not-yet-sent batches (pd.Count > 0)
        foreach (var kvp in _partitionDeques)
        {
            if (kvp.Value.Count > 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if any partition deque has an unsealed current batch.
    /// O(1) via atomic counter instead of O(n) enumeration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasUnsealedBatches()
        => Volatile.Read(ref _unsealedBatchCount) > 0;

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
            var now = Stopwatch.GetTimestamp();
            var newOldestTicks = long.MaxValue;
            bool anySealed = false;

            foreach (var kvp in _partitionDeques)
            {
                var pd = kvp.Value;
                ReadyBatch? sealedBatch = null;

                {
                    using var guard = new SpinLockGuard(ref pd.Lock);

                    if (pd.CurrentBatch is null)
                        continue;

                    if (sealAll || pd.CurrentBatch.ShouldFlush(now, _options.LingerMs))
                    {
                        ProducerDebugCounters.RecordBatchFlushedFromDictionary();
                        // Seal under lock (includes compression + enqueue)
                        sealedBatch = SealCurrentBatchUnderLock(pd, pd.CurrentBatch);
                    }
                    else
                    {
                        // Batch not ready for flush - track its creation time for oldest batch calculation
                        var batchCreatedTicks = pd.CurrentBatch.CreatedAtStopwatchTimestamp;
                        if (batchCreatedTicks < newOldestTicks)
                            newOldestTicks = batchCreatedTicks;
                    }
                }

                if (sealedBatch is not null)
                {
                    anySealed = true;
                }
            }

            if (!sealAll)
            {
                // Update the oldest batch tracking for next check using CAS to prevent race condition.
                var current = Volatile.Read(ref _oldestBatchCreatedTicks);
                while (newOldestTicks < current)
                {
                    var original = Interlocked.CompareExchange(ref _oldestBatchCreatedTicks, newOldestTicks, current);
                    if (original == current)
                        break;
                    current = original;
                }
            }
            else
            {
                // After sealing all, reset oldest batch tracking
                Volatile.Write(ref _oldestBatchCreatedTicks, long.MaxValue);
            }

            if (anySealed)
                SignalWakeup();
        }
        finally
        {
            _flushLingerLock.Release();
        }
    }

    private ValueTask ExpireLingerAsyncCore(CancellationToken cancellationToken)
        => SealBatchesAsync(sealAll: false, cancellationToken);

    /// <summary>
    /// Tracks a batch entering the pipeline: increments counter and adds to reference-tracking dictionary.
    /// Called when a batch is completed and enqueued to a partition deque.
    /// </summary>
    private void OnBatchEntersPipeline(ReadyBatch batch)
    {
        // Counter first, dictionary second. If the orphan sweep runs between these two
        // operations, the counter is already incremented so the sweep's Exchange(0) resets it.
        // Reversed order could leave a permanently positive counter (dictionary swept but
        // counter not yet incremented → orphan sweep misses it → counter incremented after).
        Interlocked.Increment(ref _inFlightBatchCount);
        _inFlightBatches.TryAdd(batch, 0);
        batch.AppendDiag('E');
    }

    /// <summary>
    /// Removes a batch from the pipeline: decrements counter and removes from reference-tracking dictionary.
    /// Called after a batch is sent (CompleteSend) or failed (Fail).
    /// Must be called by KafkaProducer's SenderLoopAsync after processing each batch.
    /// </summary>
    /// <returns>true if the batch was successfully removed from tracking; false if it was already removed by another thread.</returns>
    internal bool OnBatchExitsPipeline(ReadyBatch batch)
    {
        // TryRemove acts as a natural atomic guard: only the first thread to remove the batch
        // proceeds with the decrement. This prevents double-decrement from concurrent cleanup
        // paths (e.g., DisposeAsync racing with SendLoopAsync's finally block).
        if (!_inFlightBatches.TryRemove(batch, out _))
            return false;

        batch.AppendDiag('X');
        var count = Interlocked.Decrement(ref _inFlightBatchCount);
        Debug.Assert(count >= 0, $"In-flight batch count went negative ({count}) — mismatched Enter/Exit calls");
        if (count <= 0)
        {
            // All batches processed - complete any waiting flush and clear the TCS
            Interlocked.Exchange(ref _flushTcs, null)?.TrySetResult(true);

            // Wake the sender loop after the last in-flight batch exits, so it doesn't
            // re-enter WaitForWakeupAsync and sleep up to 100ms before discovering all
            // work is done.
            if (_closed)
                SignalWakeup();
        }

        return true;
    }

    /// <summary>
    /// Sweeps <see cref="_inFlightBatches"/> for batches whose delivery timeout has expired
    /// and fails them. Called periodically from the linger loop as defense-in-depth against
    /// batches whose references are lost from BrokerSender data structures (orphans).
    /// Without this sweep, orphaned batches cause ProduceAsync to hang indefinitely because
    /// their completion sources are never signaled.
    /// Uses 3x delivery timeout to avoid interfering with normal delivery timeout handling
    /// in BrokerSender.ProcessCompletedResponses (which runs at 1x). The sweep is only
    /// for truly orphaned batches that fell out of all BrokerSender data structures.
    /// </summary>
    /// <returns>The number of expired batches that were failed.</returns>
    internal int SweepExpiredInFlightBatches()
    {
        if (_inFlightBatches.IsEmpty)
            return 0;

        var now = Stopwatch.GetTimestamp();
        // Use 3x delivery timeout for the sweep. BrokerSender.ProcessCompletedResponses
        // handles normal delivery timeout (1x) for batches still tracked in _pendingResponses.
        // The sweep only catches truly orphaned batches — those not in any data structure.
        var deliveryTimeoutTicks = _options.DeliveryTimeoutTicks * 3;
        var configured = TimeSpan.FromMilliseconds(_options.DeliveryTimeoutMs * 3);
        var expiredCount = 0;

        foreach (var (batch, _) in _inFlightBatches)
        {
            var deadlineTicks = batch.StopwatchCreatedTicks + deliveryTimeoutTicks;
            if (now < deadlineTicks)
                continue; // Not yet expired

            // Use OnBatchExitsPipeline as the atomic guard: only the first thread to
            // remove the batch proceeds. This uses the same cleanup path as BrokerSender,
            // ensuring consistent counter decrement and flush signaling per-batch.
            if (!OnBatchExitsPipeline(batch))
                continue; // Another thread already handled this batch

            expiredCount++;
            var elapsed = Stopwatch.GetElapsedTime(batch.StopwatchCreatedTicks);

            // Diagnostic: capture partition state to help identify why batch was orphaned
            var isMuted = _mutedPartitions.ContainsKey(batch.TopicPartition);
            var inDeque = false;
            var dequeCount = 0;
            if (_partitionDeques.TryGetValue(batch.TopicPartition, out var pd))
            {
                using var guard = new SpinLockGuard(ref pd.Lock);
                dequeCount = pd.Count;
                inDeque = pd.Contains(batch);
            }

            FailAndRelease(batch, new KafkaTimeoutException(
                TimeoutKind.Delivery,
                elapsed,
                configured,
                $"Delivery timeout exceeded for orphaned batch {batch.TopicPartition} " +
                $"(elapsed: {elapsed.TotalSeconds:F1}s/{configured.TotalSeconds:F0}s, " +
                $"muted={isMuted}, inDeque={inDeque}, dequeCount={dequeCount}, " +
                $"trace={batch.DiagTrace})"));
            // Do NOT return to pool here. BrokerSender may still reference this batch
            // in _pendingResponses. BrokerSender.CleanupBatch will return it when it
            // processes the response. For truly orphaned batches (no BrokerSender reference),
            // ForceFailAllInFlightBatches during disposal handles pool return.
        }

        if (expiredCount > 0)
            LogOrphanedBatchesSweep(expiredCount);

        return expiredCount;
    }

    /// <summary>
    /// Fails all remaining in-flight batches tracked in <see cref="_inFlightBatches"/>.
    /// Used as a defense-in-depth sweep after BrokerSender disposal to catch batches
    /// whose references were lost from BrokerSender data structures.
    /// Safe to call multiple times — idempotent due to dictionary removal and IsEmpty check.
    /// </summary>
    internal void ForceFailAllInFlightBatches()
    {
        if (_inFlightBatches.IsEmpty)
            return;

        LogOrphanedBatchesDuringDisposal(_inFlightBatches.Count);
        var disposedException = new ObjectDisposedException(nameof(RecordAccumulator));

        foreach (var (orphanedBatch, _) in _inFlightBatches)
        {
            // OnBatchExitsPipeline uses TryRemove as atomic guard and decrements counter.
            if (!OnBatchExitsPipeline(orphanedBatch))
                continue; // Another thread already handled this batch

            FailAndRelease(orphanedBatch, disposedException);
            // During disposal, BrokerSenders are already stopped — safe to return to pool.
            try { ReturnReadyBatch(orphanedBatch); }
            catch (Exception returnEx) { LogBatchCleanupStepFailed(returnEx); }
        }
    }

    /// <summary>
    /// Fails a batch and releases its memory. Does NOT return the batch to the pool.
    /// For sweep: BrokerSender still holds a reference in _pendingResponses — returning
    /// to pool would cause use-after-free when the response arrives and BrokerSender
    /// operates on a batch that has been reused for a different partition.
    /// For disposal: caller adds explicit ReturnReadyBatch after this method.
    /// Each step is individually guarded to ensure subsequent steps always run.
    /// </summary>
    private void FailAndRelease(ReadyBatch batch, Exception exception)
    {
        try { batch.Fail(exception); }
        catch (Exception failEx) { LogBatchCleanupStepFailed(failEx); }
        try
        {
            if (!batch.MemoryReleased)
            {
                ReleaseMemory(batch.DataSize);
                batch.MemoryReleased = true;
            }
        }
        catch (Exception memEx) { LogBatchCleanupStepFailed(memEx); }
    }

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

        // Fast path: no unsealed batches AND no in-flight batches - avoid async overhead entirely
        if (!HasUnsealedBatches() && Volatile.Read(ref _inFlightBatchCount) == 0)
        {
            return ValueTask.CompletedTask;
        }

        return FlushAsyncCore(cancellationToken);
    }

    private async ValueTask FlushAsyncCore(CancellationToken cancellationToken)
    {
        var inFlightCount = Volatile.Read(ref _inFlightBatchCount);
        LogFlushStarted(0, inFlightCount);

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

        LogCloseStarted(_partitionDeques.Count);
        _closed = true;

        // Complete append worker channels so workers drain remaining items and exit.
        // Don't await workers here — they may be blocked in ReserveMemoryAsync which
        // needs the disposal event (set later in DisposeAsync) to unblock.
        CompleteAppendWorkerChannels();

        // Flush all pending batches to the partition deques
        await FlushAsync(cancellationToken).ConfigureAwait(false);

        // Signal the sender loop to wake up and exit
        SignalWakeup();
        LogClosedChannelCompleted();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        var inFlightBatches = Volatile.Read(ref _inFlightBatchCount);
        LogDisposeStarted(_partitionDeques.Count, inFlightBatches);
        // Note: BatchArena.Misses is process-scoped (shared across all producers),
        // while _batchPool and _readyBatchPool misses are per-producer-instance.
        LogPoolMisses(_batchPool.Misses, _readyBatchPool.Misses, BatchArena.Misses);
        _disposed = true;


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
            }
            catch
            {
                // Other exceptions (e.g., no connection) - proceed with immediate shutdown
                CompleteAppendWorkerChannels();
            }
        }

        // Ensure append worker channels are completed even if CloseAsync early-returned
        // (CloseAsync checks _disposed and returns immediately, so channels may not be completed)
        CompleteAppendWorkerChannels();

        // Wake ALL threads blocked in ReserveMemorySync/WaitForBufferSpace so they
        // recheck _disposed promptly instead of waiting for the timeout.
        WakeAllSyncWaiters();
        TryReleaseSemaphore(_asyncBufferSpaceSignal);

        // Cancel the disposal token to interrupt any remaining blocked operations
        // (e.g., append workers, metadata waits). Do this AFTER graceful shutdown attempt
        // so FlushAsync can complete normally.
        try
        {
            _disposalCts.Cancel();
        }
        catch
        {
            // Ignore exceptions during cancellation
        }

        // Wait for append workers to exit now that disposal token has been cancelled.
        // Workers blocked in ReserveMemorySync/Async will be woken by the semaphore
        // releases above and exit via the _disposed check.
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

        var disposedException = new ObjectDisposedException(nameof(RecordAccumulator));

        // Drain append worker channels and fail any unprocessed work items
        foreach (var channel in _appendWorkerChannels)
        {
            while (channel.Reader.TryRead(out var workItem))
            {
                CleanupWorkItemResources(in workItem);
                workItem.Completion?.TrySetException(disposedException);
            }
        }

        // Fail unsealed current batches and drain sealed batches from partition deques
        foreach (var kvp in _partitionDeques)
        {
            var pd = kvp.Value;
            {
                using var guard = new SpinLockGuard(ref pd.Lock);

                // Fail current unsealed batch
                if (pd.CurrentBatch is { } current)
                {
                    var readyBatch = current.Complete();
                    if (readyBatch is not null)
                    {
                        if (readyBatch.CompletionSourcesCount > 0)
                            Interlocked.Add(ref _pendingAwaitedProduceCount, -readyBatch.CompletionSourcesCount);

                        readyBatch.Fail(disposedException);
                        ReleaseMemory(readyBatch.DataSize);
                    }
                    _batchPool.Return(current);
                    pd.CurrentBatch = null;
                    Interlocked.Decrement(ref _unsealedBatchCount);
                }

                // Drain sealed batches
                while (pd.Count > 0)
                {
                    var readyBatch = pd.PollFirst()!;
                    readyBatch.Fail(disposedException);
                    if (!readyBatch.MemoryReleased)
                    {
                        ReleaseMemory(readyBatch.DataSize);
                        readyBatch.MemoryReleased = true;
                    }
                    OnBatchExitsPipeline(readyBatch);
                }
            }
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
        foreach (var kvp in _partitionDeques)
        {
            var pd = kvp.Value;
            {
                using var guard = new SpinLockGuard(ref pd.Lock);
                while (pd.Count > 0)
                {
                    var batch = pd.PollFirst()!;
                    batch.Fail(disposedException);
                    if (!batch.MemoryReleased)
                    {
                        ReleaseMemory(batch.DataSize);
                        batch.MemoryReleased = true;
                    }
                    OnBatchExitsPipeline(batch); // Decrement counter
                }
            }
        }

        // Signal wakeup so the sender loop can exit if still waiting
        SignalWakeup();

        // Sweep for orphaned batches whose references were lost from BrokerSender data structures.
        // This is the last line of defense: if a batch was tracked via OnBatchEntersPipeline but
        // never cleaned up via OnBatchExitsPipeline (due to a dropped reference), fail it here.
        ForceFailAllInFlightBatches();

        // Clear the batch pool
        _batchPool.Clear();

        // Dispose resources to prevent leaks
        while (_syncWaiterQueue.TryDequeue(out var queuedNode))
            queuedNode.Event.Dispose();
        _wakeupSignal?.Dispose();
        _disposalCts?.Dispose();
        _asyncBufferSpaceSignal?.Dispose();
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Disposal sweep found {Count} orphaned in-flight batches — failing them to prevent hangs")]
    private partial void LogOrphanedBatchesDuringDisposal(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Periodic sweep found {Count} orphaned in-flight batches past delivery timeout — failing them to prevent hangs")]
    private partial void LogOrphanedBatchesSweep(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Closing accumulator: {PendingBatchCount} pending batches")]
    private partial void LogCloseStarted(int pendingBatchCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Accumulator closed: ready channel completed")]
    private partial void LogClosedChannelCompleted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Disposing accumulator: {PendingBatchCount} pending batches, {InFlightCount} in-flight")]
    private partial void LogDisposeStarted(int pendingBatchCount, long inFlightCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pool misses during lifetime: PartitionBatch={PartitionBatchMisses}, ReadyBatch={ReadyBatchMisses}, BatchArena={ArenaMisses}")]
    private partial void LogPoolMisses(long partitionBatchMisses, long readyBatchMisses, long arenaMisses);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failing {RemainingBatchCount} batches during disposal")]
    private partial void LogDisposalFailingRemainingBatches(int remainingBatchCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Non-fatal exception during batch cleanup step (suppressed)")]
    private partial void LogBatchCleanupStepFailed(Exception exception);

    #endregion
}

/// <summary>
/// Pool for reusing PartitionBatch instances to avoid ~40KB allocation per batch rotation.
/// Extends <see cref="ObjectPool{T}"/> for pre-warm support and miss tracking.
/// </summary>
internal sealed class PartitionBatchPool : ObjectPool<PartitionBatch>
{
    private readonly ProducerOptions _options;
    private ReadyBatchPool? _readyBatchPool;
    private readonly BatchArrayReuseQueue _arrayReuseQueue;

    /// <summary>
    /// Creates a new PartitionBatchPool.
    /// </summary>
    /// <param name="options">Producer options for configuring new batches.</param>
    /// <param name="maxPoolSize">Maximum number of batches to keep pooled.
    /// Defaults to <see cref="BatchArena.DefaultPoolSize"/> since each pooled batch retains a BatchArena.</param>
    public PartitionBatchPool(ProducerOptions options, int maxPoolSize = BatchArena.DefaultPoolSize)
        : base(maxPoolSize)
    {
        _options = options;
        _arrayReuseQueue = new BatchArrayReuseQueue(maxSize: maxPoolSize);
    }

    /// <summary>
    /// Sets the ReadyBatchPool to use for PartitionBatch.Complete() calls.
    /// Must be called after construction.
    /// </summary>
    public void SetReadyBatchPool(ReadyBatchPool pool)
    {
        _readyBatchPool = pool;
    }

    protected override PartitionBatch Create()
    {
        var batch = new PartitionBatch(default, _options);
        batch.SetReadyBatchPool(_readyBatchPool);
        batch.SetArrayReuseQueue(_arrayReuseQueue);
        return batch;
    }

    protected override void Reset(PartitionBatch item)
    {
        item.PrepareForPooling(_options, _arrayReuseQueue);
    }

    /// <summary>
    /// Gets a batch from the pool or creates a new one, configured for the given partition.
    /// </summary>
    public PartitionBatch Rent(TopicPartition topicPartition)
    {
        var batch = Rent();
        batch.Reset(topicPartition);
        return batch;
    }
}

/// <summary>
/// A batch of records for a single partition.
/// Tracks pooled arrays that are returned when the batch completes.
/// Uses ArrayPool-backed arrays instead of List to eliminate allocations.
///
/// Thread-safety: All access is serialized by the per-partition deque lock (Partition_deque.Lock).
/// No internal synchronization is needed.
/// </summary>
internal sealed class PartitionBatch
{
    private TopicPartition _topicPartition;
    private ProducerOptions _options;
    private readonly int _initialRecordCapacity;
    private ReadyBatchPool? _readyBatchPool; // Pool for renting ReadyBatch objects
    private BatchArrayReuseQueue? _arrayReuseQueue; // Reuse queue for working arrays

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

    private long _baseTimestamp;
    private int _estimatedSize;
    // Note: _offsetDelta removed - it always equals _recordCount at assignment time
    private long _createdStopwatchTimestamp;
    private int _isCompleted; // 0 = not completed, 1 = completed (Interlocked guard for idempotent Complete)
    private ReadyBatch? _completedBatch; // Cached result to ensure Complete() is idempotent

    // Transaction support: set by RecordAccumulator when batch is rented
    private long _producerId = -1;
    private short _producerEpoch = -1;
    private bool _isTransactional;
    private RecordAccumulator? _accumulator;

    /// <summary>
    /// Divisor for computing the arena overflow margin from BatchSize.
    /// A divisor of 8 gives a 12.5% margin (BatchSize / 8) above BatchSize,
    /// reducing per-message ArrayPool fallback when batches near capacity.
    /// </summary>
    private const int ArenaOverflowMarginDivisor = 8;

    /// <summary>
    /// Returns the effective arena capacity: explicit ArenaCapacity if set,
    /// otherwise BatchSize + 12.5% margin to reduce per-message ArrayPool fallback.
    /// Uses long arithmetic to avoid integer overflow when BatchSize is large.
    /// </summary>
    private static int GetEffectiveArenaCapacity(ProducerOptions options) =>
        options.ArenaCapacity > 0
            ? options.ArenaCapacity
            : (int)Math.Min((long)options.BatchSize + options.BatchSize / ArenaOverflowMarginDivisor, int.MaxValue);

    public PartitionBatch(TopicPartition topicPartition, ProducerOptions options)
    {
        _topicPartition = topicPartition;
        _options = options;
        _createdStopwatchTimestamp = Stopwatch.GetTimestamp();

        _initialRecordCapacity = options.InitialBatchRecordCapacity > 0
            ? Math.Clamp(options.InitialBatchRecordCapacity, 16, 16384)
            : ComputeInitialRecordCapacity(options.BatchSize);

        // Create arena for zero-copy serialization
        _arena = new BatchArena(GetEffectiveArenaCapacity(options));

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
    /// Sets the array reuse queue for recycling working arrays through ReadyBatch.
    /// Called when constructing a new batch outside the pool path.
    /// </summary>
    internal void SetArrayReuseQueue(BatchArrayReuseQueue? queue)
    {
        _arrayReuseQueue = queue;
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
    /// IMPORTANT: _isCompleted must ONLY be reset here, NOT in PrepareForPooling().
    /// Resetting it in PrepareForPooling() creates a race condition where a stale reference from
    /// another thread could successfully append to the pooled batch (since _isCompleted would be 0),
    /// and those messages would be lost when Reset() is later called. By only resetting this flag
    /// at rent time, we ensure that any stale references fail the _isCompleted check in TryAppend().
    /// </remarks>
    internal void Reset(TopicPartition topicPartition)
    {
        _topicPartition = topicPartition;
        _createdStopwatchTimestamp = Stopwatch.GetTimestamp();
        _recordCount = 0;
        _completionSourceCount = 0;
        _callbackCount = 0;
        _pooledArrayCount = 0;
        _pooledHeaderArrayCount = 0;
        _baseTimestamp = 0;
        _estimatedSize = 0;
        _isCompleted = 0;  // Only reset here - see remarks
        _completedBatch = null;
    }

    /// <summary>
    /// Prepares the batch for returning to the pool.
    /// Allocates new arrays (the old ones were transferred to ReadyBatch).
    /// IMPORTANT: This must only be called after Complete() which transfers arrays to ReadyBatch.
    /// </summary>
    internal void PrepareForPooling(ProducerOptions options, BatchArrayReuseQueue? arrayReuseQueue = null)
    {
        _options = options;
        _arrayReuseQueue = arrayReuseQueue;

        // Arena was transferred to ReadyBatch by Complete(), so _arena should be null.
        // This is a no-op but kept for safety.
        _arena?.Return();

        // Rent or create arena for the pooled batch
        _arena = BatchArena.RentOrCreate(GetEffectiveArenaCapacity(options));

        // Arrays were transferred to ReadyBatch by Complete() and are now null.
        // Try to reclaim arrays from the reuse queue first (returned by ReadyBatch.Cleanup()),
        // avoiding 4 ArrayPool Rent operations per batch cycle.
        if (_arrayReuseQueue is not null && _arrayReuseQueue.TryDequeue(out var reusable))
        {
            _records = reusable.Records;
            _completionSources = reusable.CompletionSources;
            _pooledArrays = reusable.PooledDataArrays;
            _pooledHeaderArrays = reusable.PooledHeaderArrays;
        }
        else
        {
            // Fallback: rent fresh arrays from ArrayPool
            _records = ArrayPool<Record>.Shared.Rent(_initialRecordCapacity);
            _completionSources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(_initialRecordCapacity);
            _pooledArrays = ArrayPool<byte[]>.Shared.Rent(_initialRecordCapacity * 2);
            _pooledHeaderArrays = ArrayPool<Header[]>.Shared.Rent(8);
        }

        // Reset counters and state for reuse.
        // IMPORTANT: Do NOT reset _isCompleted here!
        // It must only be reset in Reset() when the batch is actually rented.
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
    /// Returns the batch creation timestamp from Stopwatch for efficient age comparisons.
    /// Used by ExpireLingerAsync to track the oldest batch without enumeration.
    /// </summary>
    public long CreatedAtStopwatchTimestamp => _createdStopwatchTimestamp;

    /// <summary>
    /// Appends a record to the batch. Handles all three record types:
    /// completion-tracked (ProduceAsync), callback (Send with handler), and fire-and-forget (Send).
    /// Caller must hold the per-partition deque lock.
    /// </summary>
    public RecordAppendResult TryAppend(
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int estimatedRecordSize)
    {
        // Check if batch was completed
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
        if (_estimatedSize + estimatedRecordSize > _options.BatchSize && _recordCount > 0)
        {
            return new RecordAppendResult(false);
        }

        // Grow arrays if needed (rare - only happens if batch fills beyond initial capacity)
        if (_recordCount >= _records.Length)
        {
            GrowArray(ref _records, ref _recordCount, ArrayPool<Record>.Shared);
        }
        if (completionSource is not null && _completionSourceCount >= _completionSources.Length)
        {
            GrowArray(ref _completionSources, ref _completionSourceCount, ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared);
        }
        if (_pooledArrayCount + 2 >= _pooledArrays.Length) // +2 for key and value
        {
            GrowArray(ref _pooledArrays, ref _pooledArrayCount, ArrayPool<byte[]>.Shared);
        }
        if (headers is not null && _pooledHeaderArrayCount >= _pooledHeaderArrays.Length)
        {
            GrowArray(ref _pooledHeaderArrays, ref _pooledHeaderArrayCount, ArrayPool<Header[]>.Shared);
        }
        if (callback is not null)
        {
            _callbacks ??= ArrayPool<Action<RecordMetadata, Exception?>?>.Shared.Rent(_initialRecordCapacity);
            if (_callbackCount >= _callbacks.Length)
            {
                GrowArray(ref _callbacks!, ref _callbackCount, ArrayPool<Action<RecordMetadata, Exception?>?>.Shared);
            }
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
        if (headers is not null)
        {
            _pooledHeaderArrays[_pooledHeaderArrayCount++] = headers;
        }

        var timestampDelta = timestamp - _baseTimestamp;
        _records[_recordCount] = new Record
        {
            TimestampDelta = timestampDelta,
            OffsetDelta = _recordCount,
            Key = key.Memory,
            IsKeyNull = key.IsNull,
            Value = value.Memory,
            IsValueNull = value.IsNull,
            Headers = headers,
            HeaderCount = headerCount,
            CachedBodySize = Record.ComputeBodySize(timestampDelta, _recordCount, key.IsNull, key.Length, value.IsNull, value.Length, headers, headerCount)
        };

        if (completionSource is not null)
        {
            _completionSources[_completionSourceCount++] = completionSource;
            ProducerDebugCounters.RecordCompletionSourceStoredInBatch();
        }

        if (callback is not null)
        {
            _callbacks![_callbackCount++] = callback;
        }

        _recordCount++;
        _estimatedSize += estimatedRecordSize;

        return new RecordAppendResult(Success: true, ActualSizeAdded: estimatedRecordSize);
    }

    /// <summary>
    /// Appends a record from raw span data, using the arena for zero-copy when possible.
    /// Falls back to ArrayPool rental when the arena is full.
    /// Caller must hold the per-partition deque lock.
    /// </summary>
    public RecordAppendResult TryAppendFromSpans(
        long timestamp,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        Header[]? headers,
        int headerCount,
        Action<RecordMetadata, Exception?>? callback,
        int estimatedRecordSize)
    {
        var keyLength = keyIsNull ? 0 : keyData.Length;
        var valueLength = valueIsNull ? 0 : valueData.Length;

        // Check if batch was completed
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
        if (_estimatedSize + estimatedRecordSize > _options.BatchSize && _recordCount > 0)
        {
            return new RecordAppendResult(false);
        }

        // Grow arrays if needed (rare - only happens if batch fills beyond initial capacity)
        if (_recordCount >= _records.Length)
        {
            GrowArray(ref _records, ref _recordCount, ArrayPool<Record>.Shared);
        }
        if (headers is not null && _pooledHeaderArrayCount >= _pooledHeaderArrays.Length)
        {
            GrowArray(ref _pooledHeaderArrays, ref _pooledHeaderArrayCount, ArrayPool<Header[]>.Shared);
        }
        if (callback is not null)
        {
            _callbacks ??= ArrayPool<Action<RecordMetadata, Exception?>?>.Shared.Rent(_initialRecordCapacity);
            if (_callbackCount >= _callbacks.Length)
            {
                GrowArray(ref _callbacks!, ref _callbackCount, ArrayPool<Action<RecordMetadata, Exception?>?>.Shared);
            }
        }

        // Pre-grow _pooledArrays for worst case (key + value fallback to ArrayPool)
        if (_pooledArrayCount + 2 >= _pooledArrays.Length)
        {
            GrowArray(ref _pooledArrays, ref _pooledArrayCount, ArrayPool<byte[]>.Shared);
        }

        // Try to use arena for zero-copy serialization
        ReadOnlyMemory<byte> keyMemory = ReadOnlyMemory<byte>.Empty;
        ReadOnlyMemory<byte> valueMemory = ReadOnlyMemory<byte>.Empty;
        bool usedArenaForKey = false;
        bool usedArenaForValue = false;

        if (!keyIsNull && keyLength > 0 && _arena is not null)
        {
            if (_arena.TryAllocate(keyLength, out var keySpan, out var keyOffset))
            {
                keyData.CopyTo(keySpan);
                keyMemory = _arena.Buffer.AsMemory(keyOffset, keyLength);
                usedArenaForKey = true;
            }
        }

        if (!valueIsNull && valueLength > 0 && _arena is not null)
        {
            if (_arena.TryAllocate(valueLength, out var valueSpan, out var valueOffset))
            {
                valueData.CopyTo(valueSpan);
                valueMemory = _arena.Buffer.AsMemory(valueOffset, valueLength);
                usedArenaForValue = true;
            }
        }

        // Fall back to ArrayPool for data that didn't fit in the arena
        if (!keyIsNull && keyLength > 0 && !usedArenaForKey)
        {
            var keyArray = ArrayPool<byte>.Shared.Rent(keyLength);
            keyData.CopyTo(keyArray);
            keyMemory = keyArray.AsMemory(0, keyLength);
            _pooledArrays[_pooledArrayCount++] = keyArray;
        }

        if (!valueIsNull && valueLength > 0 && !usedArenaForValue)
        {
            var valueArray = ArrayPool<byte>.Shared.Rent(valueLength);
            valueData.CopyTo(valueArray);
            valueMemory = valueArray.AsMemory(0, valueLength);
            _pooledArrays[_pooledArrayCount++] = valueArray;
        }

        if (headers is not null)
        {
            _pooledHeaderArrays[_pooledHeaderArrayCount++] = headers;
        }

        var timestampDelta = timestamp - _baseTimestamp;
        _records[_recordCount] = new Record
        {
            TimestampDelta = timestampDelta,
            OffsetDelta = _recordCount,
            Key = keyMemory,
            IsKeyNull = keyIsNull,
            Value = valueMemory,
            IsValueNull = valueIsNull,
            Headers = headers,
            HeaderCount = headerCount,
            CachedBodySize = Record.ComputeBodySize(timestampDelta, _recordCount, keyIsNull, keyLength, valueIsNull, valueLength, headers, headerCount)
        };

        if (callback is not null)
        {
            _callbacks![_callbackCount++] = callback;
        }

        _recordCount++;
        _estimatedSize += estimatedRecordSize;

        return new RecordAppendResult(Success: true, ActualSizeAdded: estimatedRecordSize);
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
    /// - When LingerMs == 0, awaited produces still flush immediately
    /// - Fire-and-forget messages wait for full linger time
    /// This balances low latency for awaited produces with efficient batching.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShouldFlush(long nowStopwatchTimestamp, int lingerMs)
    {
        // Volatile read for thread-safe access without locking.
        // A stale read that returns 0 when there are records just delays flush to next cycle.
        // A stale read that returns non-zero for an empty batch results in a no-op Complete().
        if (Volatile.Read(ref _recordCount) == 0)
            return false;

        var elapsedMs = Stopwatch.GetElapsedTime(_createdStopwatchTimestamp, nowStopwatchTimestamp).TotalMilliseconds;

        // Awaited produces: use micro-linger instead of immediate flush.
        // When LingerMs > 0 (default is 5), wait min(1ms, LingerMs/10) to let co-temporal messages batch.
        // When LingerMs == 0, flush immediately.
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

        // Called under deque lock - no concurrent access is possible.

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

        // Rent from pool to eliminate per-batch RecordBatch class allocation.
        // The batch lives through the send pipeline (1-10ms) and would otherwise
        // survive Gen0 collection, contributing to high Gen1/Gen0 promotion rate.
        var batch = RecordBatch.RentFromPool();
        batch.BaseOffset = 0;
        batch.BaseTimestamp = _baseTimestamp;
        batch.MaxTimestamp = _baseTimestamp + (_recordCount > 0 ? pooledRecordsArray[_recordCount - 1].TimestampDelta : 0);
        batch.LastOffsetDelta = _recordCount - 1;
        batch.ProducerId = _producerId;
        batch.ProducerEpoch = _producerEpoch;
        batch.BaseSequence = baseSequence;
        batch.Attributes = attributes;
        batch.Records = new RecordListWrapper(pooledRecordsArray, _recordCount);
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
            _callbackCount,
            _arrayReuseQueue);

        _completedBatch = readyBatch;

        // Null out references - ownership transferred to ReadyBatch
        _completionSources = null!;
        _pooledArrays = null!;
        _pooledHeaderArrays = null!;
        _arena = null;
        _callbacks = null;

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
    internal static int EstimateRecordSize(int keyLength, int valueLength, Header[]? headers, int headerCount)
    {
        var size = 20; // Base overhead for varint lengths, timestamp delta, offset delta, etc.
        size += keyLength;
        size += valueLength;

        if (headers is not null)
        {
            for (var i = 0; i < headerCount; i++)
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
/// Extends <see cref="ObjectPool{T}"/> for pre-warm support and miss tracking.
/// </summary>
internal sealed class ReadyBatchPool(int maxPoolSize = BatchArena.DefaultPoolSize * 2)
    : ObjectPool<ReadyBatch>(maxPoolSize)
{
    protected override ReadyBatch Create() => new();
    protected override void Reset(ReadyBatch item) => item.Reset();
}

/// <summary>
/// Reuse queue for the 4 working arrays that PartitionBatch rents from ArrayPool.
/// When ReadyBatch finishes cleanup, it pushes the container arrays here instead of returning
/// them to ArrayPool. PartitionBatch.PrepareForPooling() dequeues from here first, falling back
/// to ArrayPool on miss. This eliminates ~4 ArrayPool Rent/Return pairs per batch cycle.
/// Thread-safe via ConcurrentQueue.
/// </summary>
internal sealed class BatchArrayReuseQueue
{
    private readonly ConcurrentQueue<ReusableArrays> _queue = new();
    private readonly int _maxSize;

    public BatchArrayReuseQueue(int maxSize = 128)
    {
        _maxSize = maxSize;
    }

    internal readonly record struct ReusableArrays(
        Record[] Records,
        PooledValueTaskSource<RecordMetadata>[] CompletionSources,
        byte[][] PooledDataArrays,
        Header[][] PooledHeaderArrays);

    /// <summary>
    /// Enqueues arrays for reuse, or returns them to ArrayPool if the queue is full.
    /// </summary>
    public void EnqueueOrReturn(
        Record[] records,
        PooledValueTaskSource<RecordMetadata>[] completionSources,
        byte[][] pooledDataArrays,
        Header[][] pooledHeaderArrays)
    {
        if (_queue.Count < _maxSize)
        {
            _queue.Enqueue(new ReusableArrays(records, completionSources, pooledDataArrays, pooledHeaderArrays));
        }
        else
        {
            // Queue is full - fall back to returning arrays to ArrayPool
            ArrayPool<Record>.Shared.Return(records, clearArray: false);
            ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Return(completionSources, clearArray: false);
            ArrayPool<byte[]>.Shared.Return(pooledDataArrays, clearArray: false);
            ArrayPool<Header[]>.Shared.Return(pooledHeaderArrays, clearArray: false);
        }
    }

    /// <summary>
    /// Tries to dequeue a set of reusable arrays.
    /// </summary>
    public bool TryDequeue(out ReusableArrays arrays)
    {
        return _queue.TryDequeue(out arrays);
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

    // Reuse queue for returning working arrays back to PartitionBatch without ArrayPool round-trip
    private BatchArrayReuseQueue? _arrayReuseQueue;
    private BatchArena? _arena; // Arena for zero-copy serialization data

    // Callbacks for Send(message, callback) - inline invocation without ThreadPool
    private Action<RecordMetadata, Exception?>?[]? _callbacks;
    private int _callbackCount;

    // In-flight tracker entry for coordinated retry with multiple in-flight batches per partition.
    // Set by KafkaProducer when registering with PartitionInflightTracker, cleared in Reset().
    internal InflightEntry? InflightEntry { get; set; }

    /// <summary>
    /// Lightweight lifecycle trace for diagnosing orphaned batches in Release builds.
    /// Tracks the last few transitions as single-char codes to keep overhead minimal.
    /// Codes: E=EntersPipeline, D=Drained, Q=EnqueuedToBrokerSender, C=Coalesced,
    /// O=CarriedOver, S=Sent, R=ResponseReceived, X=ExitsPipeline, F=Failed
    /// </summary>
    internal string DiagTrace
    {
        get
        {
            var len = Volatile.Read(ref _diagTraceLen);
            return len > 0 ? new string(_diagTrace, 0, Math.Min(len, _diagTrace.Length)) : "";
        }
    }
    private readonly char[] _diagTrace = new char[32];
    private int _diagTraceLen;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AppendDiag(char code)
    {
        var idx = Interlocked.Increment(ref _diagTraceLen) - 1;
        if (idx < _diagTrace.Length)
            _diagTrace[idx] = code;
    }

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

    /// <summary>
    /// Age of this batch in milliseconds since creation (or since last reenqueue).
    /// Used by Ready() to determine if linger time has expired.
    /// </summary>
    internal int AgeMs => (int)((Stopwatch.GetTimestamp() - _createdTimestamp) * 1000 / Stopwatch.Frequency);
    private long _createdTimestamp;

    /// <summary>
    /// Called when this batch is reenqueued for retry. Resets the age timer
    /// and marks the batch as a retry so Ready() knows to apply backoff.
    /// </summary>
    internal void Reenqueued(long nowMs)
    {
        _createdTimestamp = Stopwatch.GetTimestamp();
        IsRetry = true;
    }

    // Batch-level completion tracking using resettable ManualResetValueTaskSourceCore
    // Never faults - uses SetResult(true) for success, SetResult(false) for failure
    private ManualResetValueTaskSourceCore<bool> _doneCore;

    private int _cleanedUp; // 0 = not cleaned, 1 = cleaned (prevents double-cleanup in Cleanup())
    private int _completed; // 0 = not completed, 1 = completed (prevents double-signal of _doneCore)
    private int _sendCompleted; // 0 = not done, 1 = done (prevents concurrent CompleteSend/Fail)
    internal int _returnedToPool; // 0 = not returned, 1 = returned (prevents double pool return)

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
        int callbackCount = 0,
        BatchArrayReuseQueue? arrayReuseQueue = null)
    {
        // Reset lifecycle flags at the START of a new lifecycle (not in Reset()).
        // This ensures stale references from a previous lifecycle see _cleanedUp=1
        // and return early from CompleteSend/Fail, preventing pool corruption.
        Interlocked.Exchange(ref _cleanedUp, 0);
        Interlocked.Exchange(ref _completed, 0);
        Interlocked.Exchange(ref _sendCompleted, 0);
        Interlocked.Exchange(ref _returnedToPool, 0);

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
        _arrayReuseQueue = arrayReuseQueue;
        StopwatchCreatedTicks = Stopwatch.GetTimestamp();
        _createdTimestamp = StopwatchCreatedTicks;
    }

    /// <summary>
    /// Resets the batch for reuse. Called by ReadyBatchPool.Return().
    /// </summary>
    public void Reset()
    {
        // SAFETY NET: If CompleteSend/Fail wasn't called (_cleanedUp still 0), resolve
        // orphaned completion sources and return pooled arrays before the batch goes back
        // to the pool. Without this, ProduceAsync callers hang forever on unresolved sources.
        // Single read: _cleanedUp cannot change during Reset (pool return is single-threaded per batch).
        if (Volatile.Read(ref _cleanedUp) == 0)
        {
            if (_completionSourcesArray is not null)
            {
                var orphanedException = new InvalidOperationException("Batch recycled without completing delivery — this indicates a bug in the producer pipeline");
                for (var i = 0; i < _completionSourcesCount; i++)
                {
                    _completionSourcesArray[i]?.TrySetException(orphanedException);
                }
            }

            // Cleanup() is idempotent (uses Interlocked.Exchange), so double-call is safe.
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
        _arrayReuseQueue = null;
        InflightEntry = null;
        MemoryReleased = false;
        IsRetry = false;
        RetryNotBefore = 0;
        _diagTraceLen = 0;

        // NOTE: _cleanedUp, _completed, and _sendCompleted are NOT reset here. They stay
        // at 1 (armed) while the batch is in the pool, so that stale references from a
        // previous lifecycle calling CompleteSend/Fail will hit the guard and return early.
        // These flags are reset in Initialize() when the batch starts a new lifecycle.

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
        // Atomic entry guard: only one thread can execute CompleteSend/Fail.
        // Separate from _cleanedUp so Cleanup() in the finally block still runs.
        if (Interlocked.Exchange(ref _sendCompleted, 1) != 0)
            return;

        try
        {
            ProducerDebugCounters.RecordBatchSentSuccessfully();

            // Complete per-message completion sources with metadata
            if (_completionSourcesCount > 0 && _completionSourcesArray is not null)
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
            if (_callbackCount > 0 && _callbacks is not null)
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

            // Signal batch is done (successfully) if not already signaled by CompleteDelivery
            if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
                _doneCore.SetResult(true);
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
        // Atomic entry guard: only one thread can execute Fail/CompleteSend.
        // Separate from _cleanedUp so Cleanup() in the finally block still runs.
        if (Interlocked.Exchange(ref _sendCompleted, 1) != 0)
            return;

        try
        {
            ProducerDebugCounters.RecordBatchFailed();

            // Fail per-message completion sources - these throw for ProduceAsync callers
            if (_completionSourcesCount > 0 && _completionSourcesArray is not null)
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
            if (_callbackCount > 0 && _callbacks is not null)
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
                _doneCore.SetResult(false);
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
                ArrayPool<byte>.Shared.Return(_pooledDataArrays[i], clearArray: false);
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

        // Return the working (container) arrays: either to the reuse queue for fast recycling
        // back to PartitionBatch, or to ArrayPool as fallback.
        // Note: PooledValueTaskSource instances auto-return to their pool when awaited.
        if (_arrayReuseQueue is not null
            && _pooledRecordsArray is not null
            && _completionSourcesArray is not null
            && _pooledDataArrays is not null
            && _pooledHeaderArrays is not null)
        {
            // Enqueue all 4 working arrays for reuse by PartitionBatch.PrepareForPooling().
            // Safety: clearArray: false is intentional. Array slots past _recordCount may contain stale
            // PooledValueTaskSource references, but these are never accessed because counters reset to 0
            // on reuse. PooledValueTaskSource instances auto-return to their pool via GetResult(),
            // so stale references don't prevent pool recycling. This matches ArrayPool behavior.
            _arrayReuseQueue.EnqueueOrReturn(_pooledRecordsArray, _completionSourcesArray, _pooledDataArrays, _pooledHeaderArrays);
        }
        else
        {
            // Fallback: return individually to ArrayPool (partial batch or no reuse queue)
            if (_completionSourcesArray is not null)
                ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Return(_completionSourcesArray, clearArray: false);
            if (_pooledDataArrays is not null)
                ArrayPool<byte[]>.Shared.Return(_pooledDataArrays, clearArray: false);
            if (_pooledHeaderArrays is not null)
                ArrayPool<Header[]>.Shared.Return(_pooledHeaderArrays, clearArray: false);
            if (_pooledRecordsArray is not null)
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

        // Return pre-compressed buffer and RecordBatch to pool.
        // ReturnPreCompressedBuffer() releases the ArrayPool buffer, then ReturnToPool()
        // clears all references and returns the RecordBatch object for reuse.
        if (_recordBatch is not null)
        {
            _recordBatch.ReturnPreCompressedBuffer();
            _recordBatch.ReturnToPool();
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
