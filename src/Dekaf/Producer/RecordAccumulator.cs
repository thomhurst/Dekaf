using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Dekaf.Protocol.Records;

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
public sealed class RecordAccumulator : IAsyncDisposable
{
    private readonly ProducerOptions _options;
    private readonly ConcurrentDictionary<TopicPartition, PartitionBatch> _batches = new();
    private readonly Channel<ReadyBatch> _readyBatches;
    private readonly Channel<ReadyBatch> _sendableBatches; // Batches with completed delivery tasks, ready for network send
    private readonly PartitionBatchPool _batchPool;

    // Completion loop that drains _readyBatches, completes delivery tasks, and forwards to _sendableBatches
    private readonly Task _completionTask;
    private readonly CancellationTokenSource _completionCts;

    // Track all in-flight delivery tasks so FlushAsync can wait for them
    // This ensures FlushAsync waits for batches already in _readyBatches, not just batches in _batches
    // Initialize with large capacity (1024) to avoid resizing under load
    // Completed tasks automatically remove themselves via ContinueWith to prevent unbounded growth
    private readonly ConcurrentDictionary<Task, byte> _inFlightDeliveryTasks = new(concurrencyLevel: Environment.ProcessorCount, capacity: 1024);

    private volatile bool _disposed;
    private volatile bool _closed;
    private volatile bool _disposing; // Set during DisposeAsync to prevent continuations from removing tasks

    // Buffer memory tracking for backpressure
    private readonly ulong _maxBufferMemory;
    private long _bufferedBytes;
    private readonly SemaphoreSlim _bufferSpaceAvailable = new(1, 1);
    private readonly CancellationTokenSource _disposalCts = new();
    private readonly ManualResetEventSlim _disposalEvent = new(false);

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
        _batchPool = new PartitionBatchPool(options);
        _maxBufferMemory = options.BufferMemory;

        // Use unbounded channel for ready batches - backpressure is now handled by buffer memory tracking
        _readyBatches = Channel.CreateUnbounded<ReadyBatch>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Channel for batches that are ready for network send (delivery tasks completed)
        _sendableBatches = Channel.CreateUnbounded<ReadyBatch>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        // Start the completion loop
        _completionCts = new CancellationTokenSource();
        _completionTask = CompletionLoopAsync(_completionCts.Token);
    }

    /// <summary>
    /// Completion loop that processes ready batches:
    /// 1. Reads from _readyBatches channel
    /// 2. Completes the delivery task (fire-and-forget semantic: batch is "ready")
    /// 3. Forwards to _sendableBatches for KafkaProducer.SenderLoop
    ///
    /// DoneTask is completed here for RecordAccumulator-only tests without a SenderLoop.
    /// KafkaProducer.FlushAsync uses _inFlightDeliveryTasks to wait for batch completion.
    /// </summary>
    private async Task CompletionLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var readyBatch in _readyBatches.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
#if DEBUG
                ProducerDebugCounters.RecordBatchProcessedByCompletionLoop();
#endif
                // Complete the delivery task (fire-and-forget semantic: "ready" = done)
                // This makes FlushAsync return for RecordAccumulator-only tests
                readyBatch.CompleteDelivery();

                // Forward to sendable channel for KafkaProducer's sender loop
                await _sendableBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
#if DEBUG
                ProducerDebugCounters.RecordBatchForwardedToSendable();
#endif
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            // Complete the sendable channel when completion loop exits
            _sendableBatches.Writer.Complete();
        }
    }

    /// <summary>
    /// Exposes the sendable batches channel reader for KafkaProducer.SenderLoop.
    /// </summary>
    internal ChannelReader<ReadyBatch> SendableBatches => _sendableBatches.Reader;

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
    private async ValueTask ReserveMemoryAsync(int recordSize, CancellationToken cancellationToken)
    {
        // Fast path: try to reserve immediately
        if (TryReserveMemory(recordSize))
        {
            return;
        }

        // Slow path: wait for space to become available with timeout protection
        // Use semaphore to avoid busy spinning - released when batches are completed
        // Protect against overflow if DeliveryTimeoutMs is configured to a very large value
        var currentTicks = Environment.TickCount64;
        var deadline = (long.MaxValue - currentTicks > _options.DeliveryTimeoutMs)
            ? currentTicks + _options.DeliveryTimeoutMs
            : long.MaxValue;

        while (!TryReserveMemory(recordSize))
        {
            // Check disposal first
            if (_disposed)
                throw new ObjectDisposedException(nameof(RecordAccumulator));

            cancellationToken.ThrowIfCancellationRequested();

            // Check if we've exceeded the delivery timeout
            var remainingMs = deadline - Environment.TickCount64;
            if (remainingMs <= 0)
            {
                throw new TimeoutException(
                    $"Timeout waiting for buffer memory after {_options.DeliveryTimeoutMs}ms. " +
                    $"Requested {recordSize} bytes, current usage: {Volatile.Read(ref _bufferedBytes)}/{_maxBufferMemory} bytes. " +
                    $"Producer is generating messages faster than the network can send them. " +
                    $"Consider: increasing BufferMemory, reducing production rate, or checking network connectivity.");
            }

            // Wait with timeout to periodically retry in case we missed a signal
            // Use the smaller of 100ms or remaining time
            var waitMs = (int)Math.Min(100, remainingMs);
            await _bufferSpaceAvailable.WaitAsync(waitMs, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reserves buffer memory synchronously for backpressure purposes.
    /// Used by Send() to enforce backpressure before queueing to the work channel.
    /// Blocks if buffer is full until space is available or timeout is reached.
    /// Throws TimeoutException if buffer space doesn't become available within DeliveryTimeoutMs.
    /// Throws OperationCanceledException if the producer is disposed while waiting.
    /// </summary>
    internal void ReserveMemorySyncForBackpressure(int recordSize) => ReserveMemorySync(recordSize);

    /// <summary>
    /// Reserves buffer memory asynchronously for backpressure purposes.
    /// Used by ProduceAsync() to enforce backpressure before queueing to the work channel.
    /// Awaits if buffer is full until space is available or timeout is reached.
    /// </summary>
    internal ValueTask ReserveMemoryAsyncForBackpressure(int recordSize, CancellationToken cancellationToken)
        => ReserveMemoryAsync(recordSize, cancellationToken);

    private void ReserveMemorySync(int recordSize)
    {
        // Fast path: try to reserve immediately
        if (TryReserveMemory(recordSize))
        {
            return;
        }

        // Slow path: wait for space with timeout and cancellation support
        var currentTicks = Environment.TickCount64;
        var deadline = (long.MaxValue - currentTicks > _options.DeliveryTimeoutMs)
            ? currentTicks + _options.DeliveryTimeoutMs
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
                    $"Timeout waiting for buffer memory after {_options.DeliveryTimeoutMs}ms. " +
                    $"Requested {recordSize} bytes, current usage: {Volatile.Read(ref _bufferedBytes)}/{_maxBufferMemory} bytes. " +
                    $"Producer is generating messages faster than the network can send them. " +
                    $"Consider: increasing BufferMemory, reducing production rate, or checking network connectivity.");
            }

            // Wait on disposal event with timeout - this allows immediate wake-up when disposed
            // Use 10ms timeout to balance CPU usage with prompt memory availability detection
            if (_disposalEvent.Wait(Math.Min(10, (int)remainingMs)))
            {
                // Disposal was signaled - throw cancellation exception
                throw new OperationCanceledException(_disposalCts.Token);
            }

            // Check again after waking up in case disposal happened during sleep
            if (_disposed)
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

        // Signal that space may be available
        // Use TryRelease to handle case where semaphore is already at max count
        try
        {
            _bufferSpaceAvailable.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already at max count, ignore
        }
        catch (ObjectDisposedException)
        {
            // Accumulator is disposed, semaphore no longer valid - ignore
        }
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
                var newBatch = _batchPool.Rent(topicPartition);
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
                }
            }

            // Should never be null at this point - defensive check
            if (batch is null)
            {
                continue; // Retry the loop
            }

            var result = batch.TryAppend(timestamp, key, value, headers, pooledHeaderArray, completion);

            if (result.Success)
            {
#if DEBUG
                ProducerDebugCounters.RecordMessageAppended(hasCompletionSource: true);
#endif
                // Release the difference between estimated and actual size to prevent memory leak
                // The actual batch memory will be released when the batch is sent via SendBatchAsync
                var overestimate = recordSize - result.ActualSizeAdded;
                if (overestimate > 0)
                    ReleaseMemory(overestimate);
                return result;
            }

            // Batch is full - atomically remove it from dictionary BEFORE completing.
            // Only the thread that wins the TryRemove race will complete the batch.
            // This prevents a race where:
            // 1. Thread A calls Complete(), batch returned to pool, Reset() clears _isCompleted
            // 2. Thread B (holding stale reference) calls Complete() on recycled batch
            if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch)))
            {
                var readyBatch = batch.Complete();
                if (readyBatch is not null)
                {
#if DEBUG
                    ProducerDebugCounters.RecordBatchCompleted(readyBatch.CompletionSourcesCount);
#endif
                    // Track delivery task for FlushAsync
                    TrackDeliveryTask(readyBatch);

                    // Try synchronous write first to avoid async state machine allocation
                    if (!_readyBatches.Writer.TryWrite(readyBatch))
                    {
                        try
                        {
                            // Backpressure happens here: WriteAsync blocks when channel is full
                            await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
#if DEBUG
                            ProducerDebugCounters.RecordBatchQueuedToReady();
#endif
                        }
                        catch
                        {
#if DEBUG
                            ProducerDebugCounters.RecordBatchFailedToQueue();
#endif
                            // CRITICAL: If WriteAsync fails (cancellation or disposal), fail the batch
                            // and release memory to prevent permanent leak
                            readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                            ReleaseMemory(readyBatch.DataSize);
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
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
        PooledValueTaskSource<RecordMetadata> completion)
    {
        if (_disposed)
            return false;

        // Calculate record size for buffer memory tracking
        var recordSize = PartitionBatch.EstimateRecordSize(key.Length, value.Length, headers);

        // Reserve memory before appending - blocks synchronously if buffer is full
        // This provides backpressure when producers are faster than the network can drain
        ReserveMemorySync(recordSize);

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
                var newBatch = _batchPool.Rent(topicPartition);
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
                }
            }

            var result = batch.TryAppend(timestamp, key, value, headers, pooledHeaderArray, completion);

            if (result.Success)
            {
#if DEBUG
                ProducerDebugCounters.RecordMessageAppended(hasCompletionSource: true);
#endif
                // Release the difference between estimated and actual size to prevent memory leak
                // The actual batch memory will be released when the batch is sent via SendBatchAsync
                var overestimate = recordSize - result.ActualSizeAdded;
                if (overestimate > 0)
                    ReleaseMemory(overestimate);

                return true;
            }

            // Batch is full - atomically remove it from dictionary BEFORE completing.
            // Only the thread that wins the TryRemove race will complete the batch.
            if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch)))
            {
                var readyBatch = batch.Complete();
                if (readyBatch is not null)
                {
#if DEBUG
                    ProducerDebugCounters.RecordBatchCompleted(readyBatch.CompletionSourcesCount);
#endif
                    // Track delivery task for FlushAsync
                    TrackDeliveryTask(readyBatch);

                    // Non-blocking write to unbounded channel - should always succeed
                    // If it fails (channel completed), the producer is being disposed
                    if (!_readyBatches.Writer.TryWrite(readyBatch))
                    {
#if DEBUG
                        ProducerDebugCounters.RecordBatchFailedToQueue();
#endif
                        // Channel is closed, fail the batch and return false
                        readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                        // Release the batch's buffer memory since it won't go through producer
                        ReleaseMemory(readyBatch.DataSize);
                        return false;
                    }

#if DEBUG
                    ProducerDebugCounters.RecordBatchQueuedToReady();
#endif
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
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray)
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
        if (t_cachedAccumulator == this &&
            t_cachedTopic == topic &&
            t_cachedPartition == partition &&
            t_cachedBatch is { } cachedBatch)
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
            if (entry.Topic == topic && entry.Partition == partition && entry.Batch is { } mpCachedBatch)
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
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
        int recordSize)
    {
        // Atomically remove the old batch from dictionary BEFORE completing.
        // Only the thread that wins the TryRemove race will complete the batch.
        if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, oldBatch)))
        {
            var readyBatch = oldBatch.Complete();
            if (readyBatch is not null)
            {
                if (!_readyBatches.Writer.TryWrite(readyBatch))
                {
                    readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                    // Release the batch's buffer memory since it won't go through producer
                    ReleaseMemory(readyBatch.DataSize);
                    t_cachedBatch = null;
                    return false;
                }

                // Return the completed batch shell to the pool for reuse
                _batchPool.Return(oldBatch);
            }
        }

        // Rent a new batch from the pool
        var newBatch = _batchPool.Rent(topicPartition);

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
                var newBatch = _batchPool.Rent(topicPartition);
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

            // Batch is full - atomically remove it from dictionary BEFORE completing.
            // Only the thread that wins the TryRemove race will complete the batch.
            if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch)))
            {
                var readyBatch = batch.Complete();
                if (readyBatch is not null)
                {
                    // Track delivery task for FlushAsync
                    TrackDeliveryTask(readyBatch);

                    // Non-blocking write to unbounded channel - should always succeed
                    // If it fails (channel completed), the producer is being disposed
                    if (!_readyBatches.Writer.TryWrite(readyBatch))
                    {
                        // Channel is closed, fail the batch and return false
                        readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                        // Release the batch's buffer memory since it won't go through producer
                        ReleaseMemory(readyBatch.DataSize);
                        // Invalidate caches
                        t_cachedBatch = null;
                        mpCache[cacheIndex].Batch = null;
                        return false;
                    }

                    // Return the completed batch shell to the pool for reuse
                    _batchPool.Return(batch);
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

        // Track actual memory used for proper accounting
        var memoryUsed = 0;

        try
        {
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
                    // Rent from pool
                    var newBatch = _batchPool.Rent(topicPartition);
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
                    // Atomically remove the completed batch BEFORE completing.
                    // Only the thread that wins the TryRemove race will complete the batch.
                    if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(topicPartition, batch)))
                    {
                        var readyBatch = batch.Complete();
                        if (readyBatch is not null)
                        {
                            // Track delivery task for FlushAsync
                            TrackDeliveryTask(readyBatch);

                            // Track memory for this batch - it will be released when sent or on failure
                            memoryUsed += readyBatch.DataSize;

                            // BUG FIX: Wrap in try-catch to ensure ReadyBatch cleanup if exception occurs
                            // between Complete() and successful channel write
                            try
                            {
                                if (!_readyBatches.Writer.TryWrite(readyBatch))
                                {
                                    readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                                    // Release the batch's buffer memory since it won't go through producer
                                    // Note: This releases the actual batch memory, not from our reserved pool
                                    ReleaseMemory(readyBatch.DataSize);
                                    // Subtract from our tracking since we released it
                                    memoryUsed -= readyBatch.DataSize;
                                    t_cachedBatch = null;
                                    return false;
                                }

                                // Batch successfully sent to channel - sender will release its memory
                                // Return the completed batch shell to the pool for reuse
                                _batchPool.Return(batch);
                            }
                            catch
                            {
                                // CRITICAL: If exception occurs after Complete(), must clean up ReadyBatch
                                // to prevent ArrayPool leaks from pooled arrays in the batch
                                readyBatch.Fail(new InvalidOperationException("Batch append failed"));
                                // Release the batch memory here since it won't reach SendBatchAsync
                                ReleaseMemory(readyBatch.DataSize);
                                // NOTE: Don't decrement memoryUsed - this keeps the accounting correct:
                                // - Catch releases: readyBatch.DataSize (actual batch)
                                // - Finally releases: totalEstimatedSize - memoryUsed (overestimate portion)
                                // - Together they equal totalEstimatedSize (correct accounting)
                                throw;
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
            // CRITICAL: Release any unused reserved memory
            // We reserved totalEstimatedSize but only used memoryUsed (actual batch sizes)
            // The difference must be released to prevent permanent memory leak
            // Note: memoryUsed batches will be released by SendBatchAsync when sent
            var unusedMemory = totalEstimatedSize - memoryUsed;
            if (unusedMemory > 0)
            {
                ReleaseMemory(unusedMemory);
            }
        }
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
                // Atomically remove the batch from dictionary BEFORE completing.
                // Only the thread that wins the TryRemove race will complete the batch.
                if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(kvp.Key, batch)))
                {
                    var readyBatch = batch.Complete();
                    if (readyBatch is not null)
                    {
#if DEBUG
                        ProducerDebugCounters.RecordBatchCompleted(readyBatch.CompletionSourcesCount);
#endif
                        // Track delivery task for FlushAsync
                        TrackDeliveryTask(readyBatch);

                        // Try synchronous write first to avoid async state machine allocation
                        if (!_readyBatches.Writer.TryWrite(readyBatch))
                        {
                            try
                            {
                                // Channel is bounded or busy, fall back to async
                                await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
#if DEBUG
                                ProducerDebugCounters.RecordBatchQueuedToReady();
#endif
                            }
                            catch
                            {
#if DEBUG
                                ProducerDebugCounters.RecordBatchFailedToQueue();
#endif
                                // CRITICAL: If WriteAsync fails (cancellation or disposal), fail the batch
                                // and release memory to prevent permanent leak
                                readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                                ReleaseMemory(readyBatch.DataSize);
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
                    }
                }
            }
        }
    }

    /// <summary>
    /// Registers a ReadyBatch for tracking by FlushAsync.
    /// Completed tasks automatically remove themselves from tracking to prevent memory leaks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrackDeliveryTask(ReadyBatch readyBatch)
    {
        if (readyBatch is not null)
        {
            var task = readyBatch.DoneTask;
            _inFlightDeliveryTasks.TryAdd(task, 0);

            if (!task.IsCompleted)
            {
                // Register continuation to auto-remove when complete
                // Allocation cost: ~100 bytes per batch (not per message)
                // Amortized: ~0.1 bytes per message (batch = ~1000 messages)
                // This prevents unbounded memory growth in long-running fire-and-forget scenarios
                // NOTE: No exception observation needed - DoneTask never faults (uses TrySetResult)
                _ = task.ContinueWith(static (t, state) =>
                {
                    var accumulator = (RecordAccumulator)state!;

                    // Don't remove during disposal - disposal will clear dictionary
                    if (!accumulator._disposing)
                        accumulator._inFlightDeliveryTasks.TryRemove(t, out _);
                }, this,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
                return;
            }

            // Task completed synchronously (rare) - clean up immediately
            // NOTE: No exception observation needed - DoneTask never faults
            _inFlightDeliveryTasks.TryRemove(task, out _);
        }
    }

    /// <summary>
    /// Flushes all batches and waits for them to be delivered to Kafka.
    /// </summary>
    /// <remarks>
    /// Optimized to avoid async state machine allocation when there are no batches to flush.
    /// Respects caller's cancellation token - if no timeout is provided, will wait indefinitely.
    /// </remarks>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        // Check cancellation upfront - must throw immediately if already cancelled
        cancellationToken.ThrowIfCancellationRequested();

        // Fast path: no batches to flush AND no in-flight batches - avoid enumeration and async overhead entirely
        if (_batches.IsEmpty && _inFlightDeliveryTasks.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        return FlushAsyncCore(cancellationToken);
    }

    private async ValueTask FlushAsyncCore(CancellationToken cancellationToken)
    {
#if DEBUG
        ProducerDebugCounters.RecordFlushCall();
#endif
        // Step 1: Flush all batches from _batches dictionary
        // Take a snapshot of keys to avoid infinite loop if messages are continuously added
        // This ensures FlushAsync only flushes batches that existed when it was called
        var keysToFlush = _batches.Keys.ToArray();

        foreach (var key in keysToFlush)
        {
            if (_batches.TryGetValue(key, out var batch))
            {
                // Atomically remove the batch from dictionary BEFORE completing.
                // Only the thread that wins the TryRemove race will complete the batch.
                if (_batches.TryRemove(new KeyValuePair<TopicPartition, PartitionBatch>(key, batch)))
                {
#if DEBUG
                    ProducerDebugCounters.RecordBatchFlushedFromDictionary();
#endif
                    var readyBatch = batch.Complete();
                    if (readyBatch is not null)
                    {
#if DEBUG
                        ProducerDebugCounters.RecordBatchCompleted(readyBatch.CompletionSourcesCount);
#endif
                        // Track delivery task for FlushAsync to wait on
                        TrackDeliveryTask(readyBatch);

                        // Try synchronous write first to avoid async state machine allocation
                        if (!_readyBatches.Writer.TryWrite(readyBatch))
                        {
                            try
                            {
                                // Channel is bounded or busy, fall back to async
                                await _readyBatches.Writer.WriteAsync(readyBatch, cancellationToken).ConfigureAwait(false);
                            }
                            catch
                            {
                                // CRITICAL: Use nested try-finally to ensure memory is ALWAYS released
                                // even if readyBatch.Fail() itself throws an exception
                                try
                                {
                                    readyBatch.Fail(new ObjectDisposedException(nameof(RecordAccumulator)));
                                }
                                finally
                                {
                                    // ALWAYS release memory to prevent permanent leak
                                    ReleaseMemory(readyBatch.DataSize);
                                }
                                throw;
                            }
                        }

                        // Return the completed batch shell to the pool for reuse
                        _batchPool.Return(batch);
                    }
                }
            }
        }

        // Step 2: Take a snapshot of all in-flight delivery tasks
        // This ensures we wait for batches that were expired by linger loop before FlushAsync was called
        // Use ToArray() for atomic snapshot (avoids enumeration issues if tasks complete during iteration)
        var allTasks = _inFlightDeliveryTasks.Keys.ToArray();

        // CRITICAL: No locks are held during this await, preventing deadlock
        // The sender loop will complete these tasks when batches are sent
        // Respect cancellation token to allow caller to timeout the wait
        if (allTasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(allTasks).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // FlushAsync was cancelled - observe all task exceptions before rethrowing
                // In unit tests with no sender loop, these tasks will fail with ObjectDisposedException
                // If we don't observe them here, GC finalizer will throw UnobservedTaskException
                foreach (var task in allTasks)
                {
                    if (task.IsFaulted)
                        _ = task.Exception;
                }
                throw;
            }

            // Clean up completed tasks from tracking dictionary
            // Use for loop to avoid allocation-heavy foreach with array
            for (int i = 0; i < allTasks.Length; i++)
            {
                _inFlightDeliveryTasks.TryRemove(allTasks[i], out _);
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
        _disposing = true; // Prevent continuations from removing tasks during disposal

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
                _readyBatches.Writer.Complete();
            }
            catch
            {
                // Other exceptions (e.g., no connection) - proceed with immediate shutdown
                _readyBatches.Writer.Complete();
            }
        }

        // Cancel the disposal token and signal the disposal event to interrupt any blocked operations
        // Do this AFTER graceful shutdown attempt so FlushAsync can complete normally
        try
        {
            _disposalCts.Cancel();
            _disposalEvent.Set();
        }
        catch
        {
            // Ignore exceptions during cancellation
        }

        // Stop the completion loop and wait for it to finish
        _completionCts.Cancel();
        try
        {
            await _completionTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Completion loop stuck - force shutdown
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }

        // NOW fail any remaining batches that couldn't be sent during graceful shutdown
        var disposedException = new ObjectDisposedException(nameof(RecordAccumulator));

        // Fail incomplete batches still in the dictionary (weren't flushed in time)
        foreach (var kvp in _batches)
        {
            // Use TryRemove to ensure only one thread handles each batch during disposal.
            // This prevents races where a batch is completed and recycled while we're disposing.
            if (_batches.TryRemove(kvp))
            {
                var readyBatch = kvp.Value.Complete();
                if (readyBatch is not null)
                {
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
        }

        // Wait for all in-flight batch tasks to complete with a timeout
        // After failing all batches above, their DoneTasks should complete quickly
        // NOTE: No exception observation needed - DoneTask never faults (uses TrySetResult)
        var allTasks = _inFlightDeliveryTasks.Keys.ToArray();
        if (allTasks.Length > 0)
        {
            try
            {
                // Wait for all tasks with a 5-second timeout to prevent hanging during disposal
                await Task.WhenAll(allTasks).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Some tasks didn't complete in time - proceed with disposal anyway
            }

            // Clean up tracked tasks
            _inFlightDeliveryTasks.Clear();
        }

        // Final drain: catch any batches that may have been added during our cleanup
        while (_readyBatches.Reader.TryRead(out var batch))
        {
            if (batch is not null)
            {
                batch.Fail(disposedException);
                // Release the batch's buffer memory
                ReleaseMemory(batch.DataSize);
            }
        }

        // Clear the batch pool
        _batchPool.Clear();

        // Dispose resources to prevent leaks
        _bufferSpaceAvailable?.Dispose();
        _disposalCts?.Dispose();
        _disposalEvent?.Dispose();
        _completionCts?.Dispose();
    }
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
    /// Gets a batch from the pool or creates a new one.
    /// </summary>
    public PartitionBatch Rent(TopicPartition topicPartition)
    {
        if (_pool.TryPop(out var batch))
        {
            batch.Reset(topicPartition);
            return batch;
        }

        return new PartitionBatch(topicPartition, _options);
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

    public PartitionBatch(TopicPartition topicPartition, ProducerOptions options)
    {
        _topicPartition = topicPartition;
        _options = options;
        _createdAt = DateTimeOffset.UtcNow;

        // Clamp initial capacity to reasonable bounds (16-1024)
        _initialRecordCapacity = Math.Max(16, Math.Min(options.InitialBatchRecordCapacity, 1024));

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

        _pooledHeaderArrays = ArrayPool<RecordHeader[]>.Shared.Rent(8); // Headers less common
        _pooledHeaderArrayCount = 0;
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

        // Allocate new arena for the pooled batch
        var arenaCapacity = options.ArenaCapacity > 0 ? options.ArenaCapacity : options.BatchSize;
        _arena = new BatchArena(arenaCapacity);

        // Arrays were transferred to ReadyBatch by Complete() and are now null.
        // Allocate fresh arrays for the pooled batch.
        _records = ArrayPool<Record>.Shared.Rent(_initialRecordCapacity);
        _completionSources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(_initialRecordCapacity);
        _pooledArrays = ArrayPool<byte[]>.Shared.Rent(_initialRecordCapacity * 2);
        _pooledHeaderArrays = ArrayPool<RecordHeader[]>.Shared.Rent(8);

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
    /// Appends a record to the batch with completion tracking.
    /// Uses lock-free CAS-based synchronization for the common single-producer case,
    /// falling back to spin-wait under contention.
    /// </summary>
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
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
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
#if DEBUG
        // Track inside exclusive lock - this MUST match batch completion count
        ProducerDebugCounters.RecordCompletionSourceStoredInBatch();
#endif

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
        IReadOnlyList<RecordHeader>? headers,
        RecordHeader[]? pooledHeaderArray,
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

        return new RecordAppendResult(Success: true, ActualSizeAdded: recordSize);
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
            // Early exit if already completed
            if (_isCompleted != 0)
                return new RecordAppendResult(false);

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
    /// Smart batching strategy (similar to librdkafka):
    /// - If there are completion sources waiting (awaited produces), send immediately
    /// - Otherwise, wait for full linger time (fire-and-forget batching)
    /// This provides low latency for awaited produces while maintaining efficient
    /// batching for fire-and-forget workloads.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShouldFlush(DateTimeOffset now, int lingerMs)
    {
        // Volatile read for thread-safe access without locking.
        // A stale read that returns 0 when there are records just delays flush to next cycle.
        // A stale read that returns non-zero for an empty batch results in a no-op Complete().
        if (Volatile.Read(ref _recordCount) == 0)
            return false;

        // Early flush: If there are completion sources waiting, send immediately.
        // This provides low latency for awaited produces (ProduceAsync).
        // Fire-and-forget messages (Send) don't add completion sources, so they
        // still benefit from full linger time batching.
        if (Volatile.Read(ref _completionSourceCount) > 0)
            return true;

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
                _estimatedSize,
                pooledRecordsArray,
                _arena);

            // Null out references - ownership transferred to ReadyBatch
            _completionSources = null!;
            _pooledArrays = null!;
            _pooledHeaderArrays = null!;
            _arena = null;

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
    internal static int EstimateRecordSize(int keyLength, int valueLength, IReadOnlyList<RecordHeader>? headers)
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
                // Use UTF-8 byte count for accurate sizing (header keys may contain multi-byte characters)
                var headerKeyByteCount = System.Text.Encoding.UTF8.GetByteCount(header.Key);
                size += headerKeyByteCount + (header.IsValueNull ? 0 : header.Value.Length) + 10;
            }
        }

        return size;
    }
}

/// <summary>
/// Result of a record append operation.
/// </summary>
/// <param name="Success">Whether the append succeeded.</param>
/// <param name="ActualSizeAdded">Actual size added to batch (for memory accounting). Only valid when Success=true.</param>
public readonly record struct RecordAppendResult(bool Success, int ActualSizeAdded = 0);

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

    /// <summary>
    /// Estimated size of all data in this batch (for buffer memory tracking).
    /// </summary>
    public int DataSize { get; }

    /// <summary>
    /// Task that completes when this batch is done (either sent successfully or failed).
    /// Used by FlushAsync to wait for batch completion.
    /// IMPORTANT: This task never faults - it completes with true (success) or false (failure).
    /// This design eliminates UnobservedTaskException issues for fire-and-forget scenarios.
    /// Per-message exceptions are handled via the completion sources array, not this task.
    /// </summary>
    public Task DoneTask => _doneCompletionSource.Task;

    // Working arrays from accumulator (pooled)
    private readonly PooledValueTaskSource<RecordMetadata>[] _completionSourcesArray;
    private readonly int _completionSourcesCount;
    private readonly byte[][] _pooledDataArrays;
    private readonly int _pooledDataArraysCount;
    private readonly RecordHeader[][] _pooledHeaderArrays;
    private readonly int _pooledHeaderArraysCount;
    private readonly Record[]? _pooledRecordsArray; // Pooled records array from RecordBatch
    private readonly BatchArena? _arena; // Arena for zero-copy serialization data

    // Batch-level completion tracking - signals "batch is done" (success or failure)
    // Never faults - uses TrySetResult(true) for success, TrySetResult(false) for failure
    private readonly TaskCompletionSource<bool> _doneCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
        int dataSize,
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
        DataSize = dataSize;
        _pooledRecordsArray = pooledRecordsArray;
        _arena = arena;
    }

    /// <summary>
    /// Marks batch as "ready" (processed by completion loop).
    /// Called by RecordAccumulator.CompletionLoopAsync.
    /// This unblocks FlushAsync for fire-and-forget scenarios.
    /// For unit tests without a sender loop, this is the final completion.
    /// </summary>
    public void CompleteDelivery()
    {
        // Guard against calling after Cleanup
        if (Volatile.Read(ref _cleanedUp) != 0)
            return;

        // Signal batch is done (ready for fire-and-forget semantic)
        // This uses TrySetResult, not TrySetException, to avoid UnobservedTaskException
        _doneCompletionSource.TrySetResult(true);
    }

    /// <summary>
    /// Marks batch as successfully sent to Kafka.
    /// Called by KafkaProducer.SenderLoopAsync after network send.
    /// This completes per-message ProduceAsync operations with success metadata.
    /// NOTE: DoneTask is already completed by CompleteDelivery(), so this is a no-op for that.
    /// </summary>
    public void CompleteSend(long baseOffset, DateTimeOffset timestamp)
    {
        // Guard against calling after Cleanup
        if (Volatile.Read(ref _cleanedUp) != 0)
            return;

        try
        {
#if DEBUG
            ProducerDebugCounters.RecordBatchSentSuccessfully();
#endif
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
                        Topic = TopicPartition.Topic,
                        Partition = TopicPartition.Partition,
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

            // Signal batch is done (successfully)
            _doneCompletionSource.TrySetResult(true);
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
    /// DoneTask completes with false (no exception) to avoid UnobservedTaskException.
    /// </summary>
    public void Fail(Exception exception)
    {
        // Guard against calling Fail after Cleanup has been performed
        if (Volatile.Read(ref _cleanedUp) != 0)
            return;

        try
        {
#if DEBUG
            ProducerDebugCounters.RecordBatchFailed();
#endif
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

            // Signal batch is done (failed) - NO EXCEPTION to avoid UnobservedTaskException
            // For fire-and-forget, no one awaits this, so exception would go unobserved
            // For FlushAsync, it just needs to know "done", not success/failure details
            _doneCompletionSource.TrySetResult(false);
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
                ArrayPool<RecordHeader>.Shared.Return(_pooledHeaderArrays[i], clearArray: false);
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
