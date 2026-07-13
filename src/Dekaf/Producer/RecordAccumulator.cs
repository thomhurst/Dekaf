using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
#if NETSTANDARD2_0
using SpinLock = Dekaf.Producer.CompatibilitySpinLock;
#endif

namespace Dekaf.Producer;

#if NETSTANDARD2_0
internal sealed class CompatibilitySpinLock
{
    private readonly object _gate = new();
    private readonly bool _enableThreadOwnerTracking;
    private int _ownerThreadId;

    public CompatibilitySpinLock(bool enableThreadOwnerTracking = false)
    {
        _enableThreadOwnerTracking = enableThreadOwnerTracking;
    }

    public bool IsHeldByCurrentThread =>
        _enableThreadOwnerTracking && _ownerThreadId == Environment.CurrentManagedThreadId;

    public void Enter(ref bool lockTaken)
    {
        Monitor.Enter(_gate);
        if (_enableThreadOwnerTracking)
            _ownerThreadId = Environment.CurrentManagedThreadId;

        lockTaken = true;
    }

    public void Exit()
    {
        if (_enableThreadOwnerTracking)
            _ownerThreadId = 0;

        Monitor.Exit(_gate);
    }
}
#endif

/// <summary>
/// RAII guard for SpinLock that ensures Exit() is called on dispose.
/// Must be used with <c>using var guard = new SpinLockGuard(ref spinLock);</c>.
/// </summary>
#if NETSTANDARD2_0
internal readonly struct SpinLockGuard : IDisposable
{
    private readonly SpinLock _lock;
    private readonly bool _taken;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpinLockGuard(ref SpinLock spinLock)
    {
        _lock = spinLock;
        var taken = false;
        _lock.Enter(ref taken);
        _taken = taken;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_taken) _lock.Exit();
    }
}
#else
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
#endif


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
    private static int _inlineContinuationExceptions;

    // ReadyBatch pool lifecycle. A return count above the rent count proves the same
    // object entered the pool more than once and can be handed to concurrent renters.
    private static int _readyBatchesRented;
    private static int _readyBatchesReturned;
    private static int _readyBatchDuplicateReturns;

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
    public static void RecordInlineContinuationException() =>
        Interlocked.Increment(ref _inlineContinuationExceptions);

    [Conditional("DEBUG")]
    public static void RecordReadyBatchRented() => Interlocked.Increment(ref _readyBatchesRented);

    [Conditional("DEBUG")]
    public static void RecordReadyBatchReturned() => Interlocked.Increment(ref _readyBatchesReturned);

    [Conditional("DEBUG")]
    public static void RecordReadyBatchDuplicateReturn() => Interlocked.Increment(ref _readyBatchDuplicateReturns);

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
        _inlineContinuationExceptions = 0;
        _readyBatchesRented = 0;
        _readyBatchesReturned = 0;
        _readyBatchDuplicateReturns = 0;
        _flushCalls = 0;
        _batchesFlushedFromDictionary = 0;
    }

    public static ProducerDebugCounterSnapshot GetSnapshot()
    {
#if DEBUG
        return new ProducerDebugCounterSnapshot
        {
            MessagesAppended = Volatile.Read(ref _messagesAppended),
            MessagesAppendedWithCompletion = Volatile.Read(ref _messagesAppendedWithCompletion),
            MessagesAppendedFireAndForget = Volatile.Read(ref _messagesAppendedFireAndForget),
            CompletionSourcesStoredInBatch = Volatile.Read(ref _completionSourcesStoredInBatch),
            BatchesCompleted = Volatile.Read(ref _batchesCompleted),
            BatchesSentSuccessfully = Volatile.Read(ref _batchesSentSuccessfully),
            BatchesFailed = Volatile.Read(ref _batchesFailed),
            CompletionSourcesCompleted = Volatile.Read(ref _completionSourcesCompleted),
            CompletionSourcesFailed = Volatile.Read(ref _completionSourcesFailed),
            ReadyBatchesRented = Volatile.Read(ref _readyBatchesRented),
            ReadyBatchesReturned = Volatile.Read(ref _readyBatchesReturned),
            ReadyBatchDuplicateReturns = Volatile.Read(ref _readyBatchDuplicateReturns),
            FlushCalls = Volatile.Read(ref _flushCalls)
        };
#else
        return default;
#endif
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
              Inline continuation exceptions: {_inlineContinuationExceptions}
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

internal readonly record struct ProducerDebugCounterSnapshot
{
    public int MessagesAppended { get; init; }
    public int MessagesAppendedWithCompletion { get; init; }
    public int MessagesAppendedFireAndForget { get; init; }
    public int CompletionSourcesStoredInBatch { get; init; }
    public int BatchesCompleted { get; init; }
    public int BatchesSentSuccessfully { get; init; }
    public int BatchesFailed { get; init; }
    public int CompletionSourcesCompleted { get; init; }
    public int CompletionSourcesFailed { get; init; }
    public int ReadyBatchesRented { get; init; }
    public int ReadyBatchesReturned { get; init; }
    public int ReadyBatchDuplicateReturns { get; init; }
    public int FlushCalls { get; init; }
}

/// <summary>
/// Dedicated <see cref="ArrayPool{T}"/> for producer key/value data arrays.
/// <para/>
/// <b>Why not <see cref="ArrayPool{T}.Shared"/>?</b>
/// <c>ArrayPool&lt;byte&gt;.Shared</c> is a <c>TlsOverPerCoreLockedStacksArrayPool</c> that retains
/// returned arrays in per-thread (TLS) and per-core stacks. These stacks grow to accommodate access
/// patterns but never shrink. Producer key/value arrays are rented on the caller's thread during
/// serialization but returned on BrokerSender LongRunning threads during batch cleanup. With N
/// brokers, N dedicated BrokerSender threads each accumulate returned arrays in their TLS slots,
/// causing working set growth proportional to broker count (~1-2 GB per broker over 15 minutes).
/// <para/>
/// <c>ArrayPool&lt;byte&gt;.Create()</c> returns a <c>ConfigurableArrayPool</c> that uses per-bucket
/// locks instead of TLS caching. Cross-thread rent/return patterns do not cause unbounded growth
/// because all threads share the same bounded bucket arrays.
/// </summary>
internal static class ProducerDataPool
{
    private static readonly RatchetableArrayPool<byte> s_pool = new(
        maxArrayLength: 4 * 1024 * 1024,
        initialArraysPerBucket: 16);

    /// <summary>
    /// Shared pool for producer key/value data arrays. Bounded to 4 MB max array size
    /// (covers the default 1 MB BatchSize with headroom for oversized messages).
    /// Bucket depth is scaled via <see cref="RatchetBucketCapacity"/> when multi-broker
    /// configurations are detected.
    /// </summary>
    internal static ArrayPool<byte> BytePool => s_pool.Pool;

    /// <summary>
    /// Gets the current per-bucket array capacity. Used by <see cref="RecordAccumulator"/>
    /// to read the actual pool depth when deciding whether to ratchet further.
    /// </summary>
    internal static int CurrentArraysPerBucket => s_pool.CurrentArraysPerBucket;

    /// <inheritdoc cref="RatchetableArrayPool{T}.RatchetBucketCapacity"/>
    internal static void RatchetBucketCapacity(int arraysPerBucket) =>
        s_pool.RatchetBucketCapacity(arraysPerBucket);
}

/// <summary>
/// Dedicated <see cref="ArrayPool{T}"/> instances for producer batch container arrays.
/// <para/>
/// <b>Why not <see cref="ArrayPool{T}.Shared"/>?</b>
/// Batch container arrays (<c>PooledValueTaskSource[]</c>, <c>Header[]</c>, <c>Action[]</c>)
/// are rented on the producer/accumulator thread during batch creation but returned on
/// BrokerSender LongRunning threads during
/// <see cref="ReadyBatch.Cleanup"/>. <c>ArrayPool&lt;T&gt;.Shared</c> uses TLS-based caching
/// (<c>TlsOverPerCoreLockedStacksArrayPool</c>) that retains returned arrays in per-thread slots
/// that grow but never shrink. With N brokers, N BrokerSender threads each accumulate returned
/// arrays in their TLS slots while the producer thread allocates fresh arrays — causing linear
/// working set growth proportional to message throughput (~10-14 MB per million messages).
/// <para/>
/// The <see cref="BatchArrayReuseQueue"/> mitigates this by recycling arrays directly between
/// PartitionBatch and ReadyBatch, but when the queue overflows (transient batch churn spikes),
/// the fallback path must use <c>ArrayPool</c>. Using <c>ArrayPool&lt;T&gt;.Create()</c> for the
/// fallback ensures cross-thread rent/return does not cause TLS accumulation.
/// <para/>
/// Most pools use <c>maxArraysPerBucket: 16</c> — sufficient for concurrent access from
/// producer threads and BrokerSender threads while bounding total retention. The per-message
/// <c>Header[]</c> pool is ratcheted separately because header arrays stay in-flight for
/// the whole batch round trip, and header-heavy workloads can have thousands outstanding.
/// <c>maxArrayLength</c> values are set to accommodate both the initial capacity and one
/// round of doubling via <c>GrowArray</c>. Arrays that grow beyond this (extremely rare —
/// requires more records per batch than <c>_initialRecordCapacity × 2</c>) fall through to
/// unpooled allocations, which is acceptable.
/// </summary>
internal static class ProducerContainerPools
{
    // Max initial record capacity is 16384 (from ComputeInitialRecordCapacity).
    // GrowArray doubles, so allow up to 32768 to cover one growth step.
    private const int MaxRecordArrayLength = 32768;
    private static readonly RatchetableArrayPool<Header> s_headers = new(
        maxArrayLength: 1024,
        initialArraysPerBucket: 16);

    /// <summary>Pool for PooledValueTaskSource[] container arrays.</summary>
    internal static readonly ArrayPool<PooledValueTaskSource<RecordMetadata>> CompletionSources =
        ArrayPool<PooledValueTaskSource<RecordMetadata>>.Create(
            maxArrayLength: MaxRecordArrayLength, maxArraysPerBucket: 16);

    /// <summary>Pool for Header[] arrays (per-message headers).</summary>
    internal static ArrayPool<Header> Headers => s_headers.Pool;

    internal static int CurrentHeaderArraysPerBucket => s_headers.CurrentArraysPerBucket;

    /// <inheritdoc cref="RatchetableArrayPool{T}.RatchetBucketCapacity"/>
    internal static void RatchetHeaderBucketCapacity(int arraysPerBucket) =>
        s_headers.RatchetBucketCapacity(arraysPerBucket);

    /// <summary>Pool for callback arrays (Send with callback pattern).</summary>
    internal static readonly ArrayPool<Action<RecordMetadata, Exception?>?> Callbacks =
        ArrayPool<Action<RecordMetadata, Exception?>?>.Create(
            maxArrayLength: MaxRecordArrayLength, maxArraysPerBucket: 16);
}

internal static class PooledCompletionSource
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TrySetResult(
        PooledValueTaskSource<RecordMetadata>? source,
        RecordMetadata metadata)
    {
        if (source is null)
            return false;

        return source.RunContinuationsAsynchronously
            ? TrySetResultAsynchronous(source, metadata)
            : TrySetResultInline(source, metadata);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TrySetResultAsynchronous(
        PooledValueTaskSource<RecordMetadata> source,
        RecordMetadata metadata)
    {
        try
        {
            return source.TrySetResult(metadata);
        }
        catch
        {
            RecordCompletionSourceFault();
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TrySetResultInline(
        PooledValueTaskSource<RecordMetadata> source,
        RecordMetadata metadata)
    {
        try
        {
            return source.TrySetResult(metadata);
        }
        catch
        {
            // A raw inline continuation can throw after the source is complete.
            // Isolate it so sibling completions and producer cleanup can continue.
            RecordInlineContinuationException();
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TrySetException(
        PooledValueTaskSource<RecordMetadata>? source,
        Exception exception)
    {
        if (source is null)
            return false;

        return source.RunContinuationsAsynchronously
            ? TrySetExceptionAsynchronous(source, exception)
            : TrySetExceptionInline(source, exception);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TrySetExceptionAsynchronous(
        PooledValueTaskSource<RecordMetadata> source,
        Exception exception)
    {
        try
        {
            return source.TrySetException(exception);
        }
        catch
        {
            RecordCompletionSourceFault();
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TrySetExceptionInline(
        PooledValueTaskSource<RecordMetadata> source,
        Exception exception)
    {
        try
        {
            return source.TrySetException(exception);
        }
        catch
        {
            RecordInlineContinuationException();
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TrySetCanceled(
        PooledValueTaskSource<RecordMetadata>? source,
        CancellationToken cancellationToken)
    {
        if (source is null)
            return false;

        return source.RunContinuationsAsynchronously
            ? TrySetCanceledAsynchronous(source, cancellationToken)
            : TrySetCanceledInline(source, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TrySetCanceledAsynchronous(
        PooledValueTaskSource<RecordMetadata> source,
        CancellationToken cancellationToken)
    {
        try
        {
            return source.TrySetCanceled(cancellationToken);
        }
        catch
        {
            RecordCompletionSourceFault();
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TrySetCanceledInline(
        PooledValueTaskSource<RecordMetadata> source,
        CancellationToken cancellationToken)
    {
        try
        {
            return source.TrySetCanceled(cancellationToken);
        }
        catch
        {
            RecordInlineContinuationException();
            return true;
        }
    }

    private static void RecordInlineContinuationException()
    {
        // The source already completed. Preserve sibling progress while exposing the raw
        // continuation failure through Release telemetry and richer DEBUG diagnostics.
        ProducerDebugCounters.RecordInlineContinuationException();
        Diagnostics.DekafMetrics.InlineContinuationExceptions.Add(1);
    }

    private static void RecordCompletionSourceFault() =>
        Diagnostics.DekafMetrics.CompletionSourceFaults.Add(1);
}

/// <summary>
/// Represents memory rented from <see cref="ProducerDataPool.BytePool"/> that must be returned
/// when no longer needed.
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
    /// Returns the array to the producer data pool.
    /// </summary>
    public void Return()
    {
        if (_array is not null)
        {
            ProducerDataPool.BytePool.Return(_array, clearArray: false);
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
    public readonly int PartitionCount;
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
        int partitionCount,
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
        PartitionCount = partitionCount;
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
    // Lock-free stack pool. Eliminates the ~32-byte ConcurrentQueue Node allocation
    // per Enqueue that caused Gen2 GC pressure under high batch churn.
    // Replaced atomically during RatchetPoolSize — always access via Volatile.Read/Write.
    private static LockFreeStack<BatchArena> s_pool = new(DefaultPoolSize);
    private static readonly object s_resizeLock = new();
    // Memory tradeoff: pooling arenas retains POH memory for the pool's lifetime.
    // This is a static/process-wide pool shared across all RecordAccumulator instances.
    // POH buffers are reclaimable by the GC when evicted from the pool (references dropped).
    // Each pooled PartitionBatch retains a BatchArena, so the arena pool should be at least
    // as large as PartitionBatchPool.
    //
    // Pool size scales with BufferMemory/BatchSize to handle high batch churn rates
    // (e.g., 16KB batches create ~6,250 batches/sec at 100K msg/sec vs ~100 for 1MB batches).
    // The default (128) covers sustained load with default 256MB buffer and 1MB batches;
    // smaller batches ratchet up automatically via ComputePoolSize.
    internal const int DefaultPoolSize = 128;
    // Upper bound on pool size. Worst-case POH retention: MaxPoolSizeCap × arena capacity.
    // With 16KB batches (the smallest that triggers scaling): 512 × ~18KB ≈ 9MB.
    // With 256KB batches and 256MB buffer: 512 × ~280KB ≈ 140MB POH retention.
    // With 1MB batches: ComputePoolSize returns 128 (not 512), so 128 × ~1.1MB ≈ ~140MB.
    internal const int MaxPoolSizeCap = 512;
    internal const long MissRatchetThreshold = 128;
    private static int s_maxPoolSize = DefaultPoolSize;
    private static long s_misses;
    private static long s_drops;
    private static long s_lastRatchetMissCount;

    /// <summary>
    /// Increases the static pool size limit if the new value is larger.
    /// Called when a new RecordAccumulator is created with a higher pool size requirement.
    /// Thread-safe via CAS ratchet — the pool size only ever increases because arenas are
    /// expensive POH allocations; shrinking would discard them only to re-allocate later.
    /// Note: in multi-producer scenarios, a disposed small-batch producer leaves the raised
    /// cap in place. This is acceptable because re-creating POH buffers on demand is costlier
    /// than retaining the pool headroom, and most applications use a single producer config.
    /// Worst-case amplification: if a transient small-batch producer (e.g., 256KB batches)
    /// ratchets the cap to 512, then a 1MB-batch producer can retain up to
    /// 512 × ~1.1MB ≈ ~560MB of POH memory instead of the normal 128 × ~1.1MB ≈ ~140MB.
    /// </summary>
    internal static void RatchetPoolSize(int newSize)
    {
        InterlockedHelper.RatchetUp(ref s_maxPoolSize, newSize);

        // Grow the pool if needed. The lock serializes concurrent resize attempts
        // (e.g. multiple producers created simultaneously with different batch sizes).
        // Brief lock is acceptable because RatchetPoolSize is called during producer
        // initialization or after a sustained run of pool misses, not for every rental.
        var currentPool = Volatile.Read(ref s_pool);
        if (currentPool.Capacity < newSize)
        {
            lock (s_resizeLock)
            {
                currentPool = Volatile.Read(ref s_pool);
                if (currentPool.Capacity < newSize)
                {
                    var newPool = new LockFreeStack<BatchArena>(newSize);
                    // Drain existing pool into the new one.
                    // Note: threads holding a stale reference to currentPool (captured
                    // before this lock) may ReturnToPool into it after this drain completes
                    // but before the Volatile.Write below. Those arenas are not migrated
                    // and will be GC'd. This one-time loss is recovered on demand via
                    // the miss path.
                    while (currentPool.TryPop(out var arena))
                        newPool.TryPush(arena);
                    Volatile.Write(ref s_pool, newPool);
                }
            }
        }
    }

    /// <summary>
    /// Number of times <see cref="RentOrCreate"/> found the pool empty and had to allocate a new arena.
    /// Use this to diagnose pool sizing — sustained misses under load indicate the pool is too small.
    /// </summary>
    internal static long Misses => Volatile.Read(ref s_misses);

    /// <summary>
    /// Number of arenas rejected from the pool because their buffer was oversized or the pool was full.
    /// </summary>
    internal static long Drops => Volatile.Read(ref s_drops);

    /// <summary>
    /// Current capacity of the process-wide arena pool.
    /// </summary>
    internal static int PoolCapacity => Volatile.Read(ref s_pool).Capacity;

    internal static int ComputeRatchetPoolSize(int currentSize, long missesSinceLastRatchet)
    {
        if (missesSinceLastRatchet < MissRatchetThreshold || currentSize >= MaxPoolSizeCap)
            return currentSize;

        return Math.Min(MaxPoolSizeCap, currentSize * 2);
    }

    private static void MaybeRatchetPoolSize(long missCount)
    {
        var previousThreshold = Volatile.Read(ref s_lastRatchetMissCount);
        var nextThreshold = previousThreshold + MissRatchetThreshold;
        if (missCount < nextThreshold)
            return;

        if (Interlocked.CompareExchange(
                ref s_lastRatchetMissCount,
                nextThreshold,
                previousThreshold) != previousThreshold)
        {
            return;
        }

        var currentSize = PoolCapacity;
        var newSize = ComputeRatchetPoolSize(currentSize, MissRatchetThreshold);
        if (newSize > currentSize)
            RatchetPoolSize(newSize);
    }

    /// <summary>
    /// Pre-allocates arenas into the static pool to eliminate ramp-up allocation bursts.
    /// Call during producer initialization.
    /// </summary>
    /// <param name="count">Number of arenas to pre-allocate.</param>
    /// <param name="capacity">Buffer capacity for each arena.</param>
    internal static void PreWarm(int count, int capacity)
    {
        var pool = Volatile.Read(ref s_pool);
        for (var i = 0; i < count; i++)
        {
            if (pool.Count >= Volatile.Read(ref s_maxPoolSize))
                break;

            var arena = new BatchArena(capacity);
            if (!pool.TryPush(arena))
                break; // Pool full
        }
    }

    private byte[] _buffer;
    private int _position;
    private int _maxPooledCapacity;

    /// <summary>
    /// Creates a new arena with the specified capacity.
    /// The arena does not grow - when full, the batch should be rotated.
    /// </summary>
    /// <param name="capacity">Buffer size. Pinned on the POH permanently (not from ArrayPool) to avoid LOH fragmentation and Gen2 GC pressure.</param>
    public BatchArena(int capacity)
        : this(capacity, capacity)
    {
    }

    internal BatchArena(int capacity, int maxPooledCapacity)
    {
        _buffer = GC.AllocateUninitializedArray<byte>(capacity, pinned: true);
        _position = 0;
        _maxPooledCapacity = maxPooledCapacity;
    }

    /// <summary>
    /// Rents an arena from the pool or creates a new one.
    /// Pooled arenas retain their POH buffers for the pool's lifetime - no ArrayPool rent/return overhead.
    /// </summary>
    public static BatchArena RentOrCreate(int capacity)
        => RentOrCreate(capacity, capacity);

    public static BatchArena RentOrCreate(int capacity, int maxPooledCapacity)
    {
        var pool = Volatile.Read(ref s_pool);
        if (pool.TryPop(out var arena))
        {
            arena.Reset(capacity, maxPooledCapacity);
            return arena;
        }

        var missCount = Interlocked.Increment(ref s_misses);
        MaybeRatchetPoolSize(missCount);
        return new BatchArena(capacity, maxPooledCapacity);
    }

    /// <summary>
    /// Returns an arena to the pool for reuse, or drops the reference for GC collection if the pool is full.
    /// The POH buffer is never returned to ArrayPool.
    /// </summary>
    public static bool ReturnToPool(BatchArena arena)
    {
        arena._position = 0;

        if (arena._buffer.Length > arena._maxPooledCapacity)
        {
            arena._buffer = null!;
            Interlocked.Increment(ref s_drops);
            return false;
        }

        var pool = Volatile.Read(ref s_pool);
        if (!pool.TryPush(arena))
        {
            // Pool full — drop the reference so the GC can reclaim the POH segment
            // once all objects on it are dead. No ArrayPool return needed.
            arena._buffer = null!;
            Interlocked.Increment(ref s_drops);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resets the arena for reuse. Keeps the existing buffer if it's large enough,
    /// otherwise allocates a new permanent buffer.
    /// </summary>
    private void Reset(int capacity, int maxPooledCapacity)
    {
        _maxPooledCapacity = maxPooledCapacity;
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

    internal int Capacity => _buffer.Length;

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
    /// Gets read-only memory for data at the specified offset and length.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<byte> GetMemory(int offset, int length)
    {
        return _buffer.AsMemory(offset, length);
    }

    /// <summary>
    /// Rewinds the last allocation if no later allocation has occurred.
    /// Used only while the owning partition lock is held to abandon a failed record encode.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRewindLastAllocation(int offset, int length)
    {
        return Interlocked.CompareExchange(ref _position, offset, offset + length) == offset + length;
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
/// Accumulates records into batches for efficient sending.
/// Provides backpressure through bounded channel capacity (similar to librdkafka's queue.buffering.max.messages).
/// Simple, reliable, and uses modern C# primitives.
/// </summary>
public sealed partial class RecordAccumulator : IAsyncDisposable
{
    private const int MaxConnectionScaleDiagnosticEvents = 256;
    private const int MaxBrokerBudgetDiagnosticSamples = 4096;
    private const int BrokerBudgetDiagnosticIntervalMs = 1_000;
    private const int MaxTrackedCoalesceWidth = 64;
    // ReadyBatch lifecycle (seal→send→response→cleanup) is longer than PartitionBatch
    // (create→fill→seal), so its pool needs proportionally more capacity.
    private const int ReadyBatchPoolSizeRatio = 2;
    private const int DisposeAppendInProgressWaitMs = 5000;
    private static readonly long DisposeAppendInProgressWaitTicks =
        (long)(DisposeAppendInProgressWaitMs * (Stopwatch.Frequency / 1000.0));

    private readonly ProducerOptions _options;
    private readonly bool _serializeBatchesPerPartition;
    private readonly CompressionCodecRegistry? _compressionCodecs;
    private readonly ConcurrentDictionary<string, int> _singleBatchRequestFixedSizes =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Per-partition deques of sealed ReadyBatches, matching Java's
    /// ConcurrentMap&lt;TopicPartition, Deque&lt;ProducerBatch&gt;&gt;.
    /// Accessed by producer threads (AddLast under lock) and sender thread (drain).
    /// </summary>
    private readonly ConcurrentDictionary<TopicPartition, PartitionDeque> _partitionDeques = new();

    /// <summary>
    /// Per-broker unacked-byte budgets bounding delivery latency under sustained overload.
    /// Populated lazily at the first batch seal for a broker; entries are never removed
    /// (brokers are few and long-lived). Null-pattern: the registry is consulted only when
    /// <see cref="_unackedBudgetEnabled"/> is true.
    /// </summary>
    private readonly ConcurrentDictionary<int, BrokerUnackedByteBudget> _brokerUnackedBudgets = new();
    private readonly Func<string, int, int>? _resolveLeaderId;
    private readonly bool _unackedBudgetEnabled;

    /// <summary>
    /// Muted partitions — skipped by Ready() and Drain(). The value is a reference count:
    /// independent BrokerSenders and crash-recovery barriers may overlap for the same partition.
    /// Updates are serialized by <see cref="_partitionMuteLock"/>, while Ready/Drain perform
    /// lock-free presence checks.
    /// </summary>
    private readonly ConcurrentDictionary<TopicPartition, int> _mutedPartitions = new();
    private readonly System.Threading.Lock _partitionMuteLock = new();

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

    /// <summary>
    /// Signals the linger loop when an unsealed batch is created or gains its first awaiter.
    /// This keeps the 1 ms linger cadence armed only while batches need it.
    /// </summary>
    private readonly AsyncAutoResetSignal _lingerWakeupSignal = new();

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

    // Reference-tracking for in-flight batches using an intrusive doubly-linked list.
    // Eliminates ConcurrentDictionary.Node allocations that caused pathological Gen2 GC
    // promotion: each TryAdd created a ~48-byte Node that survived Gen0 during the batch's
    // network round-trip, got promoted to Gen2, then died after TryRemove — creating a
    // near 1:1 Gen0:Gen2 collection ratio in idempotent producer workloads.
    // The intrusive list embeds prev/next pointers directly in ReadyBatch, so tracking
    // is zero-allocation. SpinLock critical sections are ~5-10 instructions (pointer ops).
    private SpinLock _inFlightBatchLock = new(enableThreadOwnerTracking: false);
    private ReadyBatch? _inFlightBatchHead;
    private ReadyBatch? _inFlightBatchTail;
    private readonly object _deliveryDiagnosticsLock = new();
    private readonly List<ProducerConnectionScaleDiagnostic> _connectionScaleEvents = [];
    private readonly List<ProducerBrokerBudgetDiagnostic> _brokerBudgetSamples = [];
    private readonly long[]? _coalesceWidthCounts;
    private readonly ConcurrentDictionary<int, ProducerRequestDiagnosticCounters>? _brokerProduceRequestCounters;
    private long _produceRequestCount;
    private long _produceRequestDiagnosticsStartedAt;
    private long _nextBrokerBudgetDiagnosticTimestampMs;

    // Optimization: Track the oldest batch creation time to skip unnecessary enumeration.
    // With LingerMs=5ms and 1ms timer, we'd enumerate 5x per batch without this optimization.
    // By tracking the oldest batch, we can skip enumeration when no batch is old enough to flush.
    // Uses Stopwatch ticks for high-resolution timing. long.MaxValue means no batches exist.
    private long _oldestBatchCreatedTicks = long.MaxValue;

    // Track whether there are unsealed batches containing awaited produces (ProduceAsync).
    // These need micro-linger checks regardless of LingerMs, so ExpireLingerAsync drains the
    // active linger queue when this counter is non-zero. Count is per batch, not per message.
    private int _pendingAwaitedProduceCount;

    // Push-based notification queue for partitions with an unsealed CurrentBatch.
    // Linger drains this instead of scanning _partitionDeques every 1ms, making awaited-produce
    // checks O(active unsealed partitions) instead of O(all partitions ever touched).
    private readonly ConcurrentQueue<TopicPartition> _lingerPartitions = new();

    // Push-based notification queue for partitions with sealed batches ready to send.
    // Populated by append rotation, SealBatchesAsync, and Reenqueue when a batch
    // enters a partition deque. Drained by Ready() instead of scanning all _partitionDeques,
    // converting O(n_partitions) to O(n_ready_partitions) per sender cycle.
    // ConcurrentQueue is lock-free and safe for multi-producer (append workers, linger timer)
    // single-consumer (sender thread) usage.
    //
    // Duplicate entries for the same partition are harmless: Ready() dequeues and checks the
    // partition deque, so extra notifications for an already-drained partition are no-ops.
    // Muted partitions are dropped (not re-enqueued), and UnmutePartition re-enqueues only if
    // sealed batches exist.
    private readonly ConcurrentQueue<TopicPartition> _readyPartitions = new();

    // Per-node partition tracking: populated by Ready() with the specific partitions it
    // consumed from _readyPartitions for each node. Used by DrainBatchesForOneNode to
    // re-enqueue only those specific partitions on leader migration (instead of the O(n)
    // full _partitionDeques scan that _needsFullPartitionScan previously triggered).
    // Single-threaded access: both Ready() and Drain() run on the sender thread.
    // Dictionary reused across cycles (cleared at start of Ready()). Lists are pooled
    // in _partitionListPool to avoid per-cycle allocations.
    // INVARIANT: Ready() and Drain() are called in strict Ready→Drain→Ready order on the sender
    // thread. Entries survive after Drain re-enqueues partitions and are only cleared by the next
    // Ready() call. Breaking this ordering could cause duplicate re-enqueues.
    private readonly Dictionary<int, List<TopicPartition>> _readyPartitionsPerNode = new();
    private readonly HashSet<TopicPartition> _readyPartitionDedup = new();
    private readonly Stack<List<TopicPartition>> _partitionListPool = new();

    // Coordination lock between FlushAsyncCore and ExpireLingerAsyncCore.
    // Ensures linger and flush don't both seal batches simultaneously,
    // which could cause ordering or double-seal issues.
    private readonly SemaphoreSlim _flushLingerLock = new(1, 1);

    private readonly ILogger _logger;
    private readonly Action<string, int>? _onBatchComplete;
    private readonly Action<string, int, int, int>? _onRecordAppended;
    private readonly ConcurrentDictionary<TopicPartition, long> _partitionQueueBytes = new();

    private int _disposed;
    private int _closed;

    internal Action? PurgeAppendWaitObservedForTest;

    /// <summary>
    /// True after CloseAsync has been called. Used by the sender loop to know
    /// when to exit after draining remaining batches.
    /// </summary>
    internal bool Closed => Volatile.Read(ref _closed) != 0;

    // Transaction support: ProducerId, ProducerEpoch, and transactional flag
    // Set by KafkaProducer.InitTransactionsAsync after successful InitProducerId.
    // Uses Volatile.Read/Write to ensure visibility across threads (written under lock,
    // read from append worker threads without synchronization — stale reads on ARM64 otherwise).
    private long _producerId = -1;
    private short _producerEpoch = -1;
    internal long ProducerId { get => Volatile.Read(ref _producerId); set => Volatile.Write(ref _producerId, value); }
    internal short ProducerEpoch { get => Volatile.Read(ref _producerEpoch); set => Volatile.Write(ref _producerEpoch, value); }
    private bool _isTransactional;
    internal bool IsTransactional { get => Volatile.Read(ref _isTransactional); set => Volatile.Write(ref _isTransactional, value); }

    // Per-partition sequence numbers for idempotent/transactional producing.
    // The broker requires monotonically increasing BaseSequence per partition.
    // Uses StrongBox<int> so GetOrAdd returns a mutable reference on the fast path
    // (lock-free hash lookup only), avoiding AddOrUpdate's per-call bucket locking.
    // During leader migration, two BrokerSender threads may access the same partition
    // concurrently, so StrongBox.Value is mutated via Interlocked.Add for atomicity.
    //
    // Reset operations use Interlocked.Exchange(ref box.Value, 0) instead of TryRemove
    // to avoid re-allocating ConcurrentDictionary internal Node objects (~48 bytes) and
    // StrongBox instances (~24 bytes) per partition. Under epoch bump recovery, TryRemove
    // followed by GetOrAdd caused these mid-lived objects to survive Gen0, get promoted
    // to Gen2, then die — creating pathological Gen2 GC pressure in idempotent workloads.
    private readonly ConcurrentDictionary<TopicPartition, StrongBox<int>> _sequenceNumbers = new();

    /// <summary>
    /// Gets the next base sequence number for a partition and increments by the record count.
    /// Fast path: lock-free ConcurrentDictionary.GetOrAdd (existing key) + atomic increment.
    /// Uses Interlocked.Add to be safe during leader migration when two BrokerSender threads
    /// could call this concurrently for the same partition.
    /// </summary>
    internal int GetAndIncrementSequence(TopicPartition topicPartition, int recordCount)
    {
        var box = _sequenceNumbers.GetOrAdd(topicPartition, static _ => new StrongBox<int>(0));
        return Interlocked.Add(ref box.Value, recordCount) - recordCount;
    }

    /// <summary>
    /// Resets all sequence numbers. Called after InitTransactionsAsync when epoch changes.
    /// Resets values in place rather than clearing the dictionary to avoid re-allocating
    /// ConcurrentDictionary Node and StrongBox objects when sequences are next assigned.
    /// O(n) over tracked partitions; acceptable for this recovery path since n is bounded
    /// by the partition count the producer writes to.
    /// </summary>
    /// <remarks>
    /// The dictionary is intentionally append-only — entries are never removed, only zeroed.
    /// This avoids Node/StrongBox churn that causes Gen2 GC pressure under high throughput.
    /// </remarks>
    internal void ResetSequenceNumbers()
    {
        foreach (var kvp in _sequenceNumbers)
            Interlocked.Exchange(ref kvp.Value.Value, 0);
    }

    /// <summary>
    /// Resets sequence numbers for specific partitions only (Java-style per-partition reset).
    /// Called during client-side epoch bump for idempotent producers — only the partitions
    /// that triggered OOSN/InvalidProducerEpoch need their sequences reset to 0.
    /// Unaffected partitions keep their current sequence counters; the broker carries
    /// forward per-partition sequence state across epoch bumps (KIP-360).
    /// Resets in place via Interlocked.Exchange to avoid Node/StrongBox reallocation.
    /// </summary>
    internal void ResetSequencesForPartitions(IReadOnlyCollection<TopicPartition> partitions)
    {
        foreach (var tp in partitions)
        {
            if (_sequenceNumbers.TryGetValue(tp, out var box))
                Interlocked.Exchange(ref box.Value, 0);
        }
    }

    // Buffer memory tracking for backpressure.
    // Stored as long so Volatile.Read/Interlocked.Exchange apply; always holds a non-negative value.
    private long _maxBufferMemory;
    private long _bufferedBytes;
    // Adaptive connection scaling: counts slow-path entries in ReserveMemoryAsync
    private long _bufferPressureEvents;
    // Dynamic pool ratchet: last pressure snapshot when pool was ratcheted.
    // When pressure accumulates beyond the threshold, the ProducerDataPool bucket
    // capacity is ratcheted up to match the workload's actual cold-path depth.
    private long _lastPoolRatchetPressure;
    private readonly CancellationTokenSource _disposalCts = new();
    // Broadcast signal for buffer space waiters. When buffer space is freed, the current
    // TCS is completed (waking ALL waiters simultaneously), then swapped for a fresh one.
    // This eliminates the serial convoy problem of SemaphoreSlim(0,1) where N waiters
    // must wake one-at-a-time in a chain: waiter1 -> CAS -> wake waiter2 -> CAS -> ...
    // With broadcast, all N waiters wake, attempt CAS concurrently, and losers immediately
    // re-enter the wait loop — O(1) wake latency instead of O(N).
    // The TCS allocation per signal is acceptable: it only fires on the backpressure slow
    // path (buffer full), not per-message, and is guarded by a waiter count.
    private volatile TaskCompletionSource _bufferSpaceSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _bufferSpaceWaiters; // Number of threads waiting on _bufferSpaceSignal

    // Pooled slow path: replaces async state machine allocation with a pooled IValueTaskSource<bool>.
    // When TryReserveMemory fails, PendingAppend instances are enqueued here and drained by ReleaseMemory.
    private readonly PendingAppendPool _pendingAppendPool;
    private readonly ConcurrentQueue<PendingAppend> _pendingAppends = new();
    private readonly object _pendingAppendQueueLock = new();
    private readonly Dictionary<TopicPartition, long> _blockedPendingPartitionRecheckTimes = [];
    private readonly List<PendingAppend> _pendingAppendScan = [];
    private readonly List<PendingAppend> _drainablePendingAppends = [];
    private int _draining; // CAS guard for DrainPendingAppends

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

        /// <summary>1 when this partition has an entry in the linger notification queue.</summary>
        public int LingerQueued;

        /// <summary>
        /// True while a thread has detached CurrentBatch and is completing it outside Lock.
        /// Appenders wait for the ready batch to be enqueued before creating or rotating another batch.
        /// </summary>
        public bool RotationInProgress;

        /// <summary>
        /// True while a thread has reserved space in CurrentBatch and is encoding record bytes outside Lock.
        /// Appenders and linger sealing wait until the reserved record metadata is committed.
        /// </summary>
        public bool AppendInProgress;

        public int SlowPathAppendCount;

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
        var nowTimestamp = Stopwatch.GetTimestamp();

        // Return all per-node partition lists to the pool and clear the mapping.
        // This runs once per sender cycle (not per message) so the iteration is acceptable.
        foreach (var (_, list) in _readyPartitionsPerNode)
        {
            list.Clear();
            _partitionListPool.Push(list);
        }
        _readyPartitionsPerNode.Clear();
        _readyPartitionDedup.Clear();

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
                var backoffRemaining = head.RetryNotBefore - nowTimestamp;
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

            // Pre-serialization runs on a worker after seal. If a stale or early
            // notification arrives first, preserve it so the fallback sender tick can
            // retry without relying solely on the worker's completion publish.
            if (!head.IsPreSerialized)
            {
                _readyPartitions.Enqueue(tp);
                continue;
            }

            // Sealed batches in the deque are always sendable. The linger/micro-linger
            // timer already determined readiness when it sealed the batch, so re-checking
            // linger here would double-count the wait (e.g., a batch sealed by micro-linger
            // after 1ms would wait another 5000ms with lingerMs=5000).
            // Only retry backoff (handled above) can delay a sealed batch.
            readyNodes.Add(leader.NodeId);

            // Track which partitions Ready() consumed for each node so Drain can
            // re-enqueue only these specific partitions on leader migration, avoiding
            // the O(n) full _partitionDeques scan (#577).
            if (!_readyPartitionsPerNode.TryGetValue(leader.NodeId, out var nodePartitions))
            {
                // Count check is load-bearing: Stack<T>.Pop() throws on empty stack.
                nodePartitions = _partitionListPool.Count > 0
                    ? _partitionListPool.Pop()
                    : new List<TopicPartition>();
                _readyPartitionsPerNode[leader.NodeId] = nodePartitions;
            }
            if (_readyPartitionDedup.Add(tp))
                nodePartitions.Add(tp);
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
        var usingTrackedPartitions = _readyPartitionsPerNode.TryGetValue(nodeId, out var readyPartitionsForNode)
            && readyPartitionsForNode.Count > 0;
        IReadOnlyList<TopicPartition> partitions = usingTrackedPartitions
            ? readyPartitionsForNode!
            : metadataManager.GetPartitionsForNode(nodeId);
        if (partitions.Count == 0)
        {
            // Leader migrated between Ready() and Drain(). Re-enqueue only the specific
            // partitions that Ready() consumed for this node — O(k) where k is the number
            // of ready partitions for this node, not O(n) over all partition deques (#577).
            if (_readyPartitionsPerNode.TryGetValue(nodeId, out var migratedPartitions))
            {
                for (var j = 0; j < migratedPartitions.Count; j++)
                    _readyPartitions.Enqueue(migratedPartitions[j]);
            }
            SignalWakeup();
            return;
        }

        if (!_drainIndex.TryGetValue(nodeId, out var startIndex))
            startIndex = 0;

        var size = 0L;
        var count = partitions.Count;
        var now = Stopwatch.GetTimestamp();
        var lastDrainIndex = startIndex;
        var reenqueueLeaderChanges = false;

        for (var i = 0; i < count; i++)
        {
            var idx = (startIndex + i) % count;
            var tp = partitions[idx];

            lastDrainIndex = (startIndex + i + 1) % count;

            if (usingTrackedPartitions)
            {
                var currentLeader = metadataManager.TryGetCachedPartitionLeader(tp.Topic, tp.Partition);
                if (currentLeader is null || currentLeader.NodeId != nodeId)
                {
                    // Ready() consumed this partition for nodeId, but leadership changed before
                    // Drain(). Re-enqueue it so the next Ready() resolves the current leader.
                    _readyPartitions.Enqueue(tp);
                    reenqueueLeaderChanges = true;
                    continue;
                }
            }

            if (_mutedPartitions.ContainsKey(tp))
                continue;

            var pd = _partitionDeques.GetValueOrDefault(tp);
            if (pd is null)
                continue;

            ReadyBatch? batch;
            var batchRequestSize = 0;
            var oversizedBatch = false;
            {
                using var guard = new SpinLockGuard(ref pd.Lock);
                batch = pd.PeekFirst();
                if (batch is null)
                    continue;

                // Safety check: Ready() should have pre-serialized the head batch before
                // marking this node ready. If another path exposes an unready batch,
                // keep the consumed notification alive for the next sender cycle.
                if (!batch.IsPreSerialized)
                {
                    _readyPartitions.Enqueue(tp);
                    continue;
                }

                if (batch.IsRetry && batch.RetryNotBefore > 0
                    && now < batch.RetryNotBefore)
                    continue;

                batchRequestSize = ProduceRequestSizeCalculator.GetSingleBatchRequestBodySize(
                    GetSingleBatchRequestFixedSize(tp.Topic),
                    batch.EncodedSize);
                oversizedBatch = batchRequestSize > maxRequestSize;
                if (!oversizedBatch && ready.Count > 0 && size + batchRequestSize > maxRequestSize)
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
                if (oversizedBatch)
                {
                    FailOversizedBatch(batch, batchRequestSize, maxRequestSize);
                    continue;
                }

                // Ready/Drain selected this exact broker after revalidating leadership.
                // Transfer the seal-time charge before handing the batch to its sender.
                ReattributeUnackedBudget(batch, nodeId);

                if (_options.EnableDeliveryDiagnostics)
                    batch.AppendDiag('D');
                size += batchRequestSize;
                ready.Add(batch);
            }
        }

        _drainIndex[nodeId] = lastDrainIndex;
        if (reenqueueLeaderChanges)
            SignalWakeup();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void FailOversizedBatch(ReadyBatch batch, int requestBodySize, int maxRequestSize)
    {
        var topicPartition = batch.TopicPartition;
        var exception = new ProduceException(
            ErrorCode.MessageTooLarge,
            $"Encoded ProduceRequest body size {requestBodySize} bytes " +
            $"(record batch {batch.EncodedSize} bytes) exceeds MaxRequestSize of {maxRequestSize} bytes.")
        {
            Topic = topicPartition.Topic,
            Partition = topicPartition.Partition
        };

        FailBatchAndCleanup(
            batch,
            exception,
            beforeFailure: null,
            removeFromPipeline: true,
            returnToPool: true);
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

    internal void MutePartition(TopicPartition tp)
    {
        lock (_partitionMuteLock)
        {
            if (_mutedPartitions.TryGetValue(tp, out var count))
                _mutedPartitions[tp] = count + 1;
            else
                _mutedPartitions[tp] = 1;
        }
    }

    /// <summary>
    /// Registers the batch's single crash-recovery mute reference without racing pool return.
    /// A temporary mute reservation is taken before the token CAS; the loser rolls its
    /// reservation back, and a winner that became stale releases only if ReturnReadyBatch
    /// did not already consume the token.
    /// </summary>
    internal bool TryRegisterLoopExitRecovery(ReadyBatch batch, int expectedGeneration)
    {
        if (!batch.TryAcquireResourcePin(expectedGeneration))
            return false;

        try
        {
            var topicPartition = batch.TopicPartition;
            MutePartition(topicPartition);

            if (!batch.TryRegisterLoopExitRecovery())
            {
                UnmutePartition(topicPartition);
                return batch.IsCurrentIncarnation(expectedGeneration)
                    && batch.IsLoopExitRedelivery;
            }

            if (batch.IsCurrentIncarnation(expectedGeneration))
                return true;

            if (batch.TryCompleteLoopExitRecovery())
                UnmutePartition(topicPartition);
            return false;
        }
        finally
        {
            batch.ReleaseResourcePin();
        }
    }

    internal void UnmutePartition(TopicPartition tp)
    {
        lock (_partitionMuteLock)
        {
            if (!_mutedPartitions.TryGetValue(tp, out var count))
                return;

            if (count > 1)
            {
                _mutedPartitions[tp] = count - 1;
                return;
            }

            // Remove must precede notification so Ready() cannot consume the notification
            // while the partition still appears muted.
            _mutedPartitions.TryRemove(tp, out _);
        }

        // Re-notify so Ready() picks up any sealed batches that were skipped while muted.
        if (HasQueuedBatches(tp))
            _readyPartitions.Enqueue(tp);

        SignalWakeup(); // Wake sender loop so it can drain the newly-unmuted partition
    }
    internal bool IsMuted(TopicPartition tp) => _mutedPartitions.ContainsKey(tp);

    internal bool HasQueuedBatches(TopicPartition tp)
    {
        if (!_partitionDeques.TryGetValue(tp, out var pd))
            return false;

        using var guard = new SpinLockGuard(ref pd.Lock);
        return pd.PeekFirst() is not null;
    }

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
    /// Drains the <see cref="_pendingAppends"/> queue, preserving per-partition FIFO while
    /// rotating budget-blocked partitions so healthy brokers can progress.
    /// </summary>
    /// <remarks>
    /// CAS on <c>_draining</c> ensures only one thread drains at a time. After releasing
    /// the drain lock, we re-check for pending items with available memory to prevent
    /// missed signals (a release could arrive while we hold the lock).
    /// </remarks>
    /// <returns>
    /// <see langword="false"/> only when another thread already owns the drain guard;
    /// otherwise <see langword="true"/>.
    /// </returns>
    internal bool DrainPendingAppends()
    {
        if (_pendingAppends.IsEmpty)
            return true;

        // Only one thread drains at a time — others return immediately.
        if (Interlocked.CompareExchange(ref _draining, 1, 0) != 0)
            return false;

        var ownsDrainGuard = true;
        try
        {
            while (true)
            {
                // Bail if accumulator is being disposed — DisposeAsync will drain the queue.
                if (Volatile.Read(ref _disposed) != 0)
                    return true;

                var madeProgress = false;
                lock (_pendingAppendQueueLock)
                {
                    _pendingAppendScan.Clear();
                    _drainablePendingAppends.Clear();
                    _blockedPendingPartitionRecheckTimes.Clear();

                    var remainingToInspect = _pendingAppends.Count;
                    while (remainingToInspect-- > 0 && _pendingAppends.TryDequeue(out var candidate))
                        _pendingAppendScan.Add(candidate);

                    var memoryExhausted = false;
                    foreach (var candidate in _pendingAppendScan)
                    {
                        // Timeout/cancel/dispose already released this operation's FIFO count.
                        if (candidate.IsCompleted)
                            continue;

                        var topicPartition = new TopicPartition(candidate.Topic, candidate.Partition);
                        if (memoryExhausted)
                        {
                            _pendingAppends.Enqueue(candidate);
                            continue;
                        }

                        if (_blockedPendingPartitionRecheckTimes.TryGetValue(
                                topicPartition,
                                out var existingRecheckAt))
                        {
                            _pendingAppends.Enqueue(candidate);
                            candidate.ScheduleAdmissionRecheck(existingRecheckAt);
                            continue;
                        }

                        var admissionBlocked = IsBrokerAdmissionBlocked(
                            candidate.Topic,
                            candidate.Partition,
                            recordBlockEvent: false,
                            out var admissionRecheckDelayMs);
                        if (admissionBlocked)
                        {
                            var recheckAt = admissionRecheckDelayMs == Timeout.Infinite
                                ? 0
                                : Dekaf.MonotonicClock.GetMilliseconds()
                                  + Math.Max(1, admissionRecheckDelayMs);
                            _pendingAppends.Enqueue(candidate);
                            candidate.ScheduleAdmissionRecheck(recheckAt);
                            _blockedPendingPartitionRecheckTimes.Add(
                                topicPartition,
                                recheckAt);
                            continue;
                        }

                        if (!TryReserveMemory(candidate.RecordSize))
                        {
                            memoryExhausted = true;
                            _pendingAppends.Enqueue(candidate);
                            continue;
                        }

                        _drainablePendingAppends.Add(candidate);
                    }

                    _pendingAppendScan.Clear();
                    madeProgress = _drainablePendingAppends.Count > 0;
                }

                foreach (var op in _drainablePendingAppends)
                {
                    // Claim the operation with CAS BEFORE touching resources.
                    // This prevents timeout/cancel from cleaning up key/value/headers
                    // while AppendAfterReservation is using them.
                    if (!op.TryClaim())
                    {
                        // Timeout/cancel won the race and already cleaned up resources.
                        // Release the memory reservation we made above.
                        ReleaseMemoryWithoutDrain(op.RecordSize);
                        continue;
                    }

                    try
                    {
                        var result = AppendAfterReservation(
                            op.Topic, op.Partition, op.Timestamp,
                            op.Key, op.Value, op.Headers, op.HeaderCount,
                            op.CompletionSource, op.Callback, op.RecordSize, op.PartitionCount);

                        op.ReleasePendingCountAfterClaim();
                        op.CompleteResult(result);
                    }
                    catch (Exception ex)
                    {
                        // AppendAfterReservation handles its own resource cleanup on throw
                        op.ReleasePendingCountAfterClaim();
                        op.CompleteException(ex);
                    }
                }
                _drainablePendingAppends.Clear();

                // Release the drain lock before re-checking so a concurrent ReleaseMemory
                // can enter if we decide not to loop again.
                ownsDrainGuard = false;
                Volatile.Write(ref _draining, 0);

                // Re-check: a ReleaseMemory may have arrived while we held the drain lock,
                // and new pending items may have been enqueued. Only re-enter if we made
                // progress last round (prevents infinite loop when head item can't be served).
                if (!madeProgress
                    || _pendingAppends.IsEmpty
                    || (ulong)Volatile.Read(ref _bufferedBytes) >= (ulong)Volatile.Read(ref _maxBufferMemory))
                {
                    return true;
                }

                // Re-acquire the drain lock for another pass
                if (Interlocked.CompareExchange(ref _draining, 1, 0) != 0)
                    return true; // Another thread took over

                ownsDrainGuard = true;
            }
        }
        finally
        {
            if (ownsDrainGuard)
            {
                _blockedPendingPartitionRecheckTimes.Clear();
                _pendingAppendScan.Clear();
                _drainablePendingAppends.Clear();
                // Ensure drain lock is released on exception. Once ownership passes to
                // another drainer, its invocation owns both the guard and shared scratch.
                Volatile.Write(ref _draining, 0);
            }
        }
    }

    /// <summary>
    /// Broadcasts a buffer-space-available signal, waking ALL async waiters simultaneously.
    /// Completes the current TCS and swaps in a fresh one for future waiters.
    /// This is O(1) wake latency vs the O(N) serial convoy of SemaphoreSlim.
    /// Guarded by waiter count to avoid allocating a new TCS when nobody is waiting.
    /// </summary>
    private void SignalBufferSpaceAvailable()
    {
        if (Volatile.Read(ref _bufferSpaceWaiters) == 0)
            return;

        var prev = Interlocked.Exchange(
            ref _bufferSpaceSignal,
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        prev.TrySetResult();
    }

    internal ValueTask<bool> WaitForWakeupAsync(int timeoutMs)
    {
        return _wakeupSignal.WaitAsync(timeoutMs);
    }

    internal ValueTask<bool> WaitForLingerWakeupAsync(int timeoutMs) =>
        _lingerWakeupSignal.WaitAsync(timeoutMs);

    internal bool HasPendingLingerBatches => HasUnsealedBatches();

    /// <summary>
    /// Registers a shutdown token with the wakeup signal so cancellation wakes the sender loop
    /// without per-wait allocation. Must be called once before the sender loop starts waiting.
    /// </summary>
    internal void RegisterWakeupShutdownToken(CancellationToken cancellationToken)
    {
        _wakeupSignal.RegisterShutdownToken(cancellationToken);
    }

    internal void RegisterLingerWakeupShutdownToken(CancellationToken cancellationToken) =>
        _lingerWakeupSignal.RegisterShutdownToken(cancellationToken);

    internal long GetPartitionQueueBytes(string topic, int partition)
    {
        return _partitionQueueBytes.TryGetValue(new TopicPartition(topic, partition), out var bytes)
            ? Math.Max(0, bytes)
            : 0;
    }

    public RecordAccumulator(
        ProducerOptions options,
        CompressionCodecRegistry? compressionCodecs = null,
        ILogger? logger = null,
        Action<string, int>? onBatchComplete = null,
        Action<string, int, int, int>? onRecordAppended = null,
        Func<string, int, int>? resolveLeaderId = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _options = options;
        _serializeBatchesPerPartition = options.MaxInFlightRequestsPerConnection <= 1;
        _compressionCodecs = compressionCodecs;
        _onBatchComplete = onBatchComplete;
        _onRecordAppended = onRecordAppended;
        _resolveLeaderId = resolveLeaderId;
        _unackedBudgetEnabled = options.DeliveryLatencyTargetMs > 0 && resolveLeaderId is not null;
        _produceRequestDiagnosticsStartedAt = Stopwatch.GetTimestamp();
        if (options.EnableDeliveryDiagnostics)
        {
            _coalesceWidthCounts = new long[MaxTrackedCoalesceWidth + 2];
            _brokerProduceRequestCounters = new ConcurrentDictionary<int, ProducerRequestDiagnosticCounters>();
        }

        ProducerOptions.ValidateArenaCapacity(options.BatchSize, options.ArenaCapacity);

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
        _maxBufferMemory = (long)options.BufferMemory;

        // PendingAppend pool for the zero-allocation slow path.
        // Pool size scales with max connections × max in-flight.
        var producerPoolSizes = PoolSizing.ForProducer(options.BufferMemory, options.BatchSize,
            options.MaxInFlightRequestsPerConnection, options.MaxConnectionsPerBroker);
        _pendingAppendPool = new PendingAppendPool(producerPoolSizes.PendingAppends);
        _pendingAppendPool.PreWarm(Math.Min(producerPoolSizes.PendingAppends / 4, 32));

        // Pre-warm a small number of pool entries to cover initial burst without
        // excessive upfront memory usage. The pools grow lazily on demand after this.
        // ReadyBatch is lightweight (no arena), so warm more of those.
        // PartitionBatch and BatchArena each allocate a ~BatchSize POH buffer,
        // so pre-warm only a handful (e.g., 8 × 1MB = 8MB for default settings).
        var preWarmCount = Math.Min(poolSize / 8, 16);
        _readyBatchPool.PreWarm(Math.Min(poolSize, 32));
        _batchPool.PreWarm(preWarmCount);
        BatchArena.PreWarm(preWarmCount,
            ProducerOptions.GetEffectiveArenaCapacity(options.BatchSize, options.ArenaCapacity));

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
        // Divide by 4: most batches are either filling or in-flight, not all in the pool
        // simultaneously. The pool grows lazily on miss, so undersizing causes a few extra
        // allocations during ramp-up but avoids excessive steady-state POH retention.
        // With default 256MB/1MB = 256 batches, pool = max(64, 128) = 128 (DefaultPoolSize floor), ~140MB of arenas.
        var batchCapacity = (int)Math.Min(options.BufferMemory / (ulong)Math.Max(options.BatchSize, 1), int.MaxValue);
        return Math.Clamp(batchCapacity / 4, BatchArena.DefaultPoolSize, BatchArena.MaxPoolSizeCap);
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
    internal void ReturnReadyBatch(ReadyBatch batch)
    {
        if (!TryClaimReadyBatchReturn(batch))
        {
            ProducerDebugCounters.RecordReadyBatchDuplicateReturn();
            return;
        }

        CompleteClaimedReadyBatchReturn(batch);
    }

    /// <summary>
    /// Publishes completion of generation-safe terminal bookkeeping and returns the batch
    /// without touching it after a racing return owner can recycle it.
    /// </summary>
    internal void CompleteTerminalBookkeepingAndReturnReadyBatch(ReadyBatch batch)
    {
        // Claim before publishing completion. If another path already owns the return, it is
        // waiting on the bookkeeping flag and may recycle the object immediately after the
        // volatile write below, so this method must not access batch again in that case.
        var returnClaimed = TryClaimReadyBatchReturn(batch);
        batch.CompleteTerminalBookkeeping();

        if (returnClaimed)
            CompleteClaimedReadyBatchReturn(batch);
    }

    private static bool TryClaimReadyBatchReturn(ReadyBatch batch)
    {
        // Atomic guard: only the first caller returns the batch to the pool.
        // Multiple paths may race during disposal; a double-return lets two renters share
        // the same object and causes silent data corruption.
        return Interlocked.Exchange(ref batch._returnedToPool, 1) == 0;
    }

    private void CompleteClaimedReadyBatchReturn(ReadyBatch batch)
    {
        batch.WaitForPreSerializationIfStarted();
        batch.WaitForResourcePins();
        batch.WaitForCleanupIfStarted();
        batch.WaitForMemoryReleaseIfStarted();
        batch.WaitForTerminalBookkeeping();

        // Registration is protected by a resource pin, so release recovery only after all
        // pins and terminal bookkeeping finish. This also keeps newer work fenced until the
        // recovered batch is fully disposed. Only the pool-return owner reaches this point.
        if (batch.TryCompleteLoopExitRecovery())
            UnmutePartition(batch.TopicPartition);

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
            if (Volatile.Read(ref _disposed) != 0)
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
            var appendStarted = false;
            try
            {
                workItem.CancellationToken.ThrowIfCancellationRequested();

                appendStarted = true;
                var appendTask = AppendAsync(
                    workItem.Topic,
                    workItem.Partition,
                    workItem.Timestamp,
                    workItem.Key,
                    workItem.Value,
                    workItem.Headers,
                    workItem.HeaderCount,
                    workItem.Completion,
                    null,
                    workItem.CancellationToken,
                    partitionCount: workItem.PartitionCount);

                if (!await appendTask.ConfigureAwait(false))
                {
                    CleanupWorkItemResources(in workItem);
                    PooledCompletionSource.TrySetException(
                        workItem.Completion,
                        new ObjectDisposedException(nameof(RecordAccumulator)));
                }
            }
            catch (OperationCanceledException) when (workItem.CancellationToken.IsCancellationRequested)
            {
                if (!appendStarted)
                    CleanupWorkItemResources(in workItem);
                PooledCompletionSource.TrySetCanceled(workItem.Completion, workItem.CancellationToken);
            }
            catch (Exception ex)
            {
                PooledCompletionSource.TrySetException(workItem.Completion, ex);
            }
            finally
            {
                DecrementSlowPathAppendCount(workItem.Topic, workItem.Partition);
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
        ReturnPooledHeaders(workItem.Headers);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReturnPooledHeaders(Header[]? headers)
    {
        if (headers is not null)
            ProducerContainerPools.Headers.Return(headers);
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
        CancellationToken cancellationToken,
        int partitionCount = 0)
    {
        EnsureAppendWorkersStarted();
        var workerIndex = (int)((uint)partition % (uint)_appendWorkerCount);
        var workItem = new AppendWorkItem(topic, partition, partitionCount, timestamp, key, value,
            headers, headerCount, completion, cancellationToken);

        IncrementSlowPathAppendCount(topic, partition);
        if (!_appendWorkerChannels[workerIndex].Writer.TryWrite(workItem))
        {
            DecrementSlowPathAppendCount(topic, partition);
            CleanupWorkItemResources(in workItem);
            PooledCompletionSource.TrySetException(
                completion,
                new ObjectDisposedException(nameof(RecordAccumulator)));
        }
    }

    /// <summary>
    /// Rents a new PartitionBatch from the pool and configures it with current transaction state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PartitionBatch RentBatch(TopicPartition topicPartition, int partitionCount)
    {
        var batch = _batchPool.Rent(topicPartition, partitionCount);
        batch.SetTransactionState(ProducerId, ProducerEpoch, IsTransactional);
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
        if (cache.CachedAccumulator == this && Volatile.Read(ref _disposed) == 0 && cache.CachedTopicPartition == tp && cache.CachedDeque is { } cached)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrackCurrentBatchForLinger(PartitionDeque pd, TopicPartition topicPartition, PartitionBatch batch)
    {
        TrackOldestBatchCreated(batch.CreatedAtStopwatchTimestamp);
        QueueLingerPartition(pd, topicPartition);
    }

    private void TrackOldestBatchCreated(long createdTicks)
    {
        var current = Volatile.Read(ref _oldestBatchCreatedTicks);
        while (createdTicks < current)
        {
            var original = Interlocked.CompareExchange(ref _oldestBatchCreatedTicks, createdTicks, current);
            if (original == current)
                break;
            current = original;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void QueueLingerPartition(
        PartitionDeque pd,
        TopicPartition topicPartition,
        bool signalLingerLoop = true)
    {
        if (Interlocked.Exchange(ref pd.LingerQueued, 1) == 0)
            _lingerPartitions.Enqueue(topicPartition);

        if (signalLingerLoop)
            _lingerWakeupSignal.Signal();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MarkLingerPartitionDequeued(PartitionDeque pd)
    {
        Volatile.Write(ref pd.LingerQueued, 0);
    }

    /// <summary>
    /// Async append method for all produce paths (fire-and-forget and awaitable).
    /// Hot path: if <see cref="TryReserveMemory"/> succeeds, completes synchronously with no async state machine.
    /// Cold path: awaits <see cref="ReserveMemoryAsync"/> when buffer memory is exhausted (backpressure).
    /// </summary>
    /// <returns>true if appended successfully, false if the accumulator is disposed.</returns>
    internal ValueTask<bool> AppendAsync(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        CancellationToken cancellationToken,
        int partitionCount = 0)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return new ValueTask<bool>(false);

        int recordSize;
        try
        {
            recordSize = PartitionBatch.EstimateRecordSize(key.Length, value.Length, headers, headerCount);
            ThrowIfRecordExceedsMaxRequestSize(
                topic, partition, key.IsNull, key.Length, value.IsNull, value.Length,
                headers, headerCount, recordSize);
        }
        catch
        {
            key.Return();
            value.Return();
            ReturnPooledHeaders(headers);
            throw;
        }

        // Hot path: non-blocking gate check + CAS reservation — no async state machine
        // allocated. An over-budget broker applies the same backpressure as a full buffer.
        if (TryAdmitAndReserve(topic, partition, recordSize))
            return new ValueTask<bool>(AppendAfterReservation(topic, partition, timestamp, key, value,
                headers, headerCount, completionSource, callback, recordSize, partitionCount));

        // Cold path: buffer full or broker over budget — enqueue pooled PendingAppend
        // (zero async state machine allocation)
        return AppendSlowPathPooled(topic, partition, timestamp, key, value,
            headers, headerCount, completionSource, callback, recordSize, cancellationToken, partitionCount);
    }

    private bool AppendAfterReservation(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int recordSize,
        int partitionCount)
    {
        return AppendPooledAfterReservationCore(topic, partition, timestamp, key, value, headers, headerCount,
            completionSource, callback, recordSize, partitionCount);
    }

    private bool AppendPooledAfterReservationCore(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int recordSize,
        int partitionCount)
    {
        var topicPartition = new TopicPartition(topic, partition);
        var pd = GetOrCreateDeque(topic, partition);
        ReadyBatch? sealedBatchToEnqueue = null;
        var sealedBatchBytesToRelease = 0;
        PartitionBatch? rentedBatch = null;
        var ownsRotation = false;
        var ownsReservation = true;
        var ownsRecordResources = true;
        var spinner = new SpinWait();

        void ReleaseOwnedAppendState()
        {
            if (ownsReservation)
            {
                ReleaseMemory(recordSize);
                ownsReservation = false;
            }
            if (ownsRecordResources)
            {
                key.Return();
                value.Return();
                ReturnPooledHeaders(headers);
                ownsRecordResources = false;
            }
        }

        try
        {
            while (true)
            {
                PartitionBatch? batchToComplete = null;
                PartitionBatch? batchToReturn = null;
                ReadyBatch? batchToPublish = null;
                ReadyBatch? batchToFail = null;
                var waitForRotation = false;
                var needBatch = false;
                var appendSucceeded = false;
                var disposed = false;
                var messageTooLarge = false;
                var actualBytesAdded = 0;

                {
                    using var guard = new SpinLockGuard(ref pd.Lock);

                    if (Volatile.Read(ref _disposed) != 0)
                    {
                        batchToReturn = rentedBatch;
                        rentedBatch = null;
                        batchToFail = sealedBatchToEnqueue;
                        sealedBatchToEnqueue = null;
                        if (ownsRotation)
                        {
                            ClearRotationInProgressUnderLock(pd);
                            ownsRotation = false;
                        }
                        disposed = true;
                    }
                    else if ((pd.RotationInProgress && !ownsRotation) || pd.AppendInProgress)
                    {
                        waitForRotation = true;
                    }
                    else
                    {
                        if (sealedBatchToEnqueue is not null)
                        {
                            EnqueueCompletedBatchUnderLock(pd, sealedBatchToEnqueue);
                            batchToPublish = sealedBatchToEnqueue;
                            sealedBatchToEnqueue = null;
                        }

                        if (pd.CurrentBatch is { } currentBatch)
                        {
                            if (TryAppendToBatch(currentBatch, timestamp, key, value, headers, headerCount,
                                completionSource, callback, recordSize, out var appendedBytes,
                                out var firstAwaitedProduceInBatch))
                            {
                                actualBytesAdded = appendedBytes;
                                ownsReservation = false;
                                ownsRecordResources = false;
                                if (firstAwaitedProduceInBatch)
                                    Interlocked.Increment(ref _pendingAwaitedProduceCount);
                                if (ShouldSealAppendedBatch(currentBatch))
                                {
                                    batchToComplete = DetachCurrentBatchForSealUnderLock(pd, currentBatch);
                                    ownsRotation = true;
                                }
                                else if (completionSource is not null)
                                {
                                    QueueLingerPartition(pd, topicPartition);
                                }
                                batchToReturn = rentedBatch;
                                rentedBatch = null;
                                appendSucceeded = true;
                            }
                            else
                            {
                                batchToComplete = DetachCurrentBatchForSealUnderLock(pd, currentBatch);
                                ownsRotation = true;
                                needBatch = true;
                            }
                        }
                        else if (rentedBatch is null)
                        {
                            needBatch = true;
                        }
                        else
                        {
                            var newBatch = rentedBatch;
                            rentedBatch = null;
                            pd.CurrentBatch = newBatch;
                            Interlocked.Increment(ref _unsealedBatchCount);

                            if (TryAppendToBatch(newBatch, timestamp, key, value, headers, headerCount,
                                completionSource, callback, recordSize, out var appendedBytes,
                                out var firstAwaitedProduceInBatch))
                            {
                                actualBytesAdded = appendedBytes;
                                ownsReservation = false;
                                ownsRecordResources = false;
                                if (firstAwaitedProduceInBatch)
                                    Interlocked.Increment(ref _pendingAwaitedProduceCount);
                                if (ShouldSealAppendedBatch(newBatch))
                                {
                                    batchToComplete = DetachCurrentBatchForSealUnderLock(pd, newBatch);
                                    ownsRotation = true;
                                }
                                else
                                {
                                    TrackCurrentBatchForLinger(pd, topicPartition, newBatch);
                                    ClearRotationInProgressUnderLock(pd);
                                    ownsRotation = false;
                                }
                                appendSucceeded = true;
                            }
                            else
                            {
                                pd.CurrentBatch = null;
                                MarkLingerPartitionDequeued(pd);
                                Interlocked.Decrement(ref _unsealedBatchCount);
                                ClearRotationInProgressUnderLock(pd);
                                ownsRotation = false;
                                batchToReturn = newBatch;
                                messageTooLarge = true;
                            }
                        }
                    }
                }

                if (batchToPublish is not null)
                    StartPreSerialization(batchToPublish);

                if (batchToReturn is not null)
                    _batchPool.Return(batchToReturn);

                if (batchToFail is not null)
                {
                    var disposedException = new ObjectDisposedException(nameof(RecordAccumulator));
                    batchToFail.Fail(disposedException);
                    ReleaseBatchMemory(batchToFail);
                }

                if (!ownsRotation && sealedBatchToEnqueue is null && sealedBatchBytesToRelease > 0)
                {
                    var bytesToRelease = sealedBatchBytesToRelease;
                    sealedBatchBytesToRelease = 0;
                    ReleaseMemory(bytesToRelease);
                }

                if (disposed)
                {
                    ReleaseOwnedAppendState();
                    return false;
                }

                if (messageTooLarge)
                {
                    ReleaseOwnedAppendState();
                    throw new KafkaException(ErrorCode.MessageTooLarge,
                        $"Record of size {recordSize} exceeds maximum batch size of {_options.BatchSize}");
                }

                if (appendSucceeded)
                {
                    NotifyRecordAppended(topic, partition, actualBytesAdded, partitionCount);
                    if (batchToComplete is not null)
                    {
                        var readyBatch = CompleteDetachedBatchAndEnqueue(pd, batchToComplete);
                        if (readyBatch is not null)
                            StartPreSerialization(readyBatch);
                    }

                    return true;
                }

                if (waitForRotation)
                {
                    spinner.SpinOnce();
                    continue;
                }

                if (batchToComplete is not null)
                {
                    try
                    {
                        sealedBatchToEnqueue = CompleteDetachedBatch(
                            batchToComplete,
                            out sealedBatchBytesToRelease,
                            releaseMemoryOnFailure: false);
                        if (sealedBatchToEnqueue is not null)
                        {
                            sealedBatchToEnqueue.UnackedBudget = ResolveUnackedBudget(
                                topic,
                                partition);
                        }
                    }
                    catch
                    {
                        ClearRotationAndReleaseDetachedBatchBytes(pd, ref sealedBatchBytesToRelease);
                        throw;
                    }
                }

                if (needBatch && rentedBatch is null)
                    rentedBatch = RentBatch(topicPartition, partitionCount);
            }
        }
        catch
        {
            ReleaseOwnedAppendState();
            throw;
        }
    }

    /// <summary>
    /// Zero-allocation slow path using pooled <see cref="PendingAppend"/>.
    /// Enqueues the operation and returns a <see cref="ValueTask{T}"/> backed by the pooled source.
    /// Drain serves it when <see cref="ReleaseMemory"/> frees buffer space.
    /// </summary>
    private ValueTask<bool> AppendSlowPathPooled(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int recordSize,
        CancellationToken cancellationToken,
        int partitionCount = 0)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            key.Return();
            value.Return();
            ReturnPooledHeaders(headers);
            return new ValueTask<bool>(Task.FromException<bool>(new ObjectDisposedException(nameof(RecordAccumulator))));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            key.Return();
            value.Return();
            ReturnPooledHeaders(headers);
            return new ValueTask<bool>(Task.FromException<bool>(new OperationCanceledException(cancellationToken)));
        }

        var (startTicks, deadline) = BeginReservationWait(recordSize);

        var op = _pendingAppendPool.Rent();
        op.Initialize(topic, partition, partitionCount, timestamp, key, value, headers, headerCount,
            completionSource, callback, recordSize, startTicks, deadline,
            this, _pendingAppendPool, cancellationToken);

        lock (_pendingAppendQueueLock)
            _pendingAppends.Enqueue(op);

        // Close TOCTOU window: if DisposeAsync ran between the _disposed check above and the
        // Enqueue, this op would sit in the queue until its timer fires. Fail it promptly.
        if (Volatile.Read(ref _disposed) != 0)
        {
            var disposedException = new ObjectDisposedException(nameof(RecordAccumulator));
            if (op.TryFail(disposedException))
            {
                // Caller gets a pre-built exception ValueTask, so nobody will call GetResult
                // on this op. Manually return it to the pool to avoid leaking.
                op.ReturnToPoolAfterTryFail();
                return new ValueTask<bool>(Task.FromException<bool>(disposedException));
            }
        }

        // Try immediate serve — memory may have been freed between TryReserveMemory and now
        DrainPendingAppends();

        return new ValueTask<bool>(op, op.Version);
    }

    /// <summary>
    /// Non-blocking variant of Append for the ProduceAsync fast path.
    /// Returns false when buffer is full (caller falls back to async slow path)
    /// OR when the accumulator is disposed.
    /// Retained for pooled-memory append coverage; production ProduceAsync uses
    /// <see cref="TryAppendFromSpansWithCompletion"/> to avoid per-message data-array rents.
    /// </summary>
    internal bool TryAppendWithCompletion(
        string topic,
        int partition,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata> completionSource,
        int partitionCount = 0)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return false;

        int recordSize;
        try
        {
            recordSize = PartitionBatch.EstimateRecordSize(key.Length, value.Length, headers, headerCount);
            ThrowIfRecordExceedsMaxRequestSize(
                topic, partition, key.IsNull, key.Length, value.IsNull, value.Length,
                headers, headerCount, recordSize);
        }
        catch
        {
            key.Return();
            value.Return();
            ReturnPooledHeaders(headers);
            throw;
        }

        // Try the admission gate + non-blocking memory reservation. If the buffer is full
        // or the destination broker is over its unacked-byte budget, return false so the
        // caller (ProduceAsync fast path) falls back to the async path.
        if (!TryAdmitAndReserve(topic, partition, recordSize))
            return false;

        return AppendPooledAfterReservationCore(topic, partition, timestamp, key, value, headers, headerCount,
            completionSource, callback: null, recordSize, partitionCount);
    }

    /// <summary>
    /// Non-blocking span variant for the ProduceAsync fast path.
    /// Stores the completion source in the batch while copying serialized key/value bytes
    /// into the batch arena instead of renting per-message pooled arrays.
    /// </summary>
    internal bool TryAppendFromSpansWithCompletion(
        string topic,
        int partition,
        long timestamp,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata> completionSource,
        int partitionCount = 0)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return false;

        var keyLength = keyIsNull ? 0 : keyData.Length;
        var valueLength = valueIsNull ? 0 : valueData.Length;
        var recordSize = PartitionBatch.EstimateRecordSize(keyLength, valueLength, headers, headerCount);
        ThrowIfRecordExceedsMaxRequestSize(
            topic, partition, keyIsNull, keyLength, valueIsNull, valueLength,
            headers, headerCount, recordSize);

        if (!TryAdmitAndReserve(topic, partition, recordSize))
            return false;

        return AppendFromSpansAfterReservationCore(topic, partition, timestamp, keyData, keyIsNull,
            valueData, valueIsNull, headers, headerCount, completionSource, callback: null,
            recordSize, partitionCount, returnHeadersOnFailure: false);
    }

    private bool AppendFromSpansAfterReservationCore(
        string topic,
        int partition,
        long timestamp,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int recordSize,
        int partitionCount,
        bool returnHeadersOnFailure)
    {
        var topicPartition = new TopicPartition(topic, partition);
        var pd = GetOrCreateDeque(topic, partition);
        ReadyBatch? sealedBatchToEnqueue = null;
        var sealedBatchBytesToRelease = 0;
        PartitionBatch? rentedBatch = null;
        var ownsRotation = false;
        var ownsReservation = true;
        var ownsHeaderResources = returnHeadersOnFailure;
        var spinner = new SpinWait();

        void ReleaseOwnedAppendState()
        {
            if (ownsReservation)
            {
                ReleaseMemory(recordSize);
                ownsReservation = false;
            }
            if (ownsHeaderResources)
            {
                ReturnPooledHeaders(headers);
                ownsHeaderResources = false;
            }
        }

        void ReleaseDetachedBatchBytesIfSafe()
        {
            if (!ownsRotation && sealedBatchToEnqueue is null && sealedBatchBytesToRelease > 0)
            {
                var bytesToRelease = sealedBatchBytesToRelease;
                sealedBatchBytesToRelease = 0;
                ReleaseMemory(bytesToRelease);
            }
        }

        void ClearAppendAndRotationInProgressUnderLock()
        {
            ClearAppendInProgressUnderLock(pd);
            if (ownsRotation)
            {
                ClearRotationInProgressUnderLock(pd);
                ownsRotation = false;
            }
        }

        try
        {
            while (true)
            {
                PartitionBatch? batchToComplete = null;
                PartitionBatch? batchToReturn = null;
                ReadyBatch? batchToPublish = null;
                ReadyBatch? batchToFail = null;
                PartitionBatch? reservedAppendBatch = null;
                PartitionBatch.ReservedRecordAppend reservedAppend = default;
                var waitForRotation = false;
                var needBatch = false;
                var appendReserved = false;
                var reservedAppendStartedNewBatch = false;
                var disposed = false;
                var messageTooLarge = false;
                var reservedActualBytesAdded = 0;
                var reservedFirstAwaitedProduceInBatch = false;

                {
                    using var guard = new SpinLockGuard(ref pd.Lock);

                    if (Volatile.Read(ref _disposed) != 0)
                    {
                        batchToReturn = rentedBatch;
                        rentedBatch = null;
                        batchToFail = sealedBatchToEnqueue;
                        sealedBatchToEnqueue = null;
                        if (ownsRotation)
                        {
                            ClearRotationInProgressUnderLock(pd);
                            ownsRotation = false;
                        }
                        disposed = true;
                    }
                    else if ((pd.RotationInProgress && !ownsRotation) || pd.AppendInProgress)
                    {
                        waitForRotation = true;
                    }
                    else
                    {
                        if (sealedBatchToEnqueue is not null)
                        {
                            EnqueueCompletedBatchUnderLock(pd, sealedBatchToEnqueue);
                            batchToPublish = sealedBatchToEnqueue;
                            sealedBatchToEnqueue = null;
                        }

                        if (pd.CurrentBatch is { } currentBatch)
                        {
                            if (TryReserveAppendFromSpansToBatch(currentBatch, timestamp, keyData, keyIsNull, valueData, valueIsNull,
                                headers, headerCount, completionSource, callback, recordSize, out reservedAppend,
                                out var actualBytesAdded, out var firstAwaitedProduceInBatch))
                            {
                                pd.AppendInProgress = true;
                                reservedAppendBatch = currentBatch;
                                reservedActualBytesAdded = actualBytesAdded;
                                reservedFirstAwaitedProduceInBatch = firstAwaitedProduceInBatch;
                                batchToReturn = rentedBatch;
                                rentedBatch = null;
                                appendReserved = true;
                            }
                            else
                            {
                                batchToComplete = DetachCurrentBatchForSealUnderLock(pd, currentBatch);
                                ownsRotation = true;
                                needBatch = true;
                            }
                        }
                        else if (rentedBatch is null)
                        {
                            needBatch = true;
                        }
                        else
                        {
                            var newBatch = rentedBatch;
                            rentedBatch = null;
                            pd.CurrentBatch = newBatch;
                            Interlocked.Increment(ref _unsealedBatchCount);

                            if (TryReserveAppendFromSpansToBatch(newBatch, timestamp, keyData, keyIsNull, valueData, valueIsNull,
                                headers, headerCount, completionSource, callback, recordSize, out reservedAppend,
                                out var actualBytesAdded, out var firstAwaitedProduceInBatch))
                            {
                                pd.AppendInProgress = true;
                                reservedAppendBatch = newBatch;
                                reservedAppendStartedNewBatch = true;
                                appendReserved = true;
                                reservedActualBytesAdded = actualBytesAdded;
                                reservedFirstAwaitedProduceInBatch = firstAwaitedProduceInBatch;
                            }
                            else
                            {
                                pd.CurrentBatch = null;
                                MarkLingerPartitionDequeued(pd);
                                Interlocked.Decrement(ref _unsealedBatchCount);
                                ClearRotationInProgressUnderLock(pd);
                                ownsRotation = false;
                                batchToReturn = newBatch;
                                messageTooLarge = true;
                            }
                        }
                    }
                }

                if (batchToPublish is not null)
                    StartPreSerialization(batchToPublish);

                if (batchToReturn is not null)
                    _batchPool.Return(batchToReturn);

                if (batchToFail is not null)
                {
                    var disposedException = new ObjectDisposedException(nameof(RecordAccumulator));
                    batchToFail.Fail(disposedException);
                    ReleaseBatchMemory(batchToFail);
                }

                ReleaseDetachedBatchBytesIfSafe();

                if (appendReserved)
                {
                    var committedReservedAppend = false;

                    try
                    {
                        PartitionBatch.EncodeReservedAppendFromSpans(
                            reservedAppend,
                            keyData,
                            keyIsNull,
                            valueData,
                            valueIsNull,
                            headers,
                            headerCount);

                        var disposedAfterReserve = false;
                        {
                            using var guard = new SpinLockGuard(ref pd.Lock);

                            if (Volatile.Read(ref _disposed) != 0)
                            {
                                PartitionBatch.CancelReservedAppend(reservedAppend);
                                ClearAppendAndRotationInProgressUnderLock();
                                disposedAfterReserve = true;
                            }
                            else
                            {
                                reservedAppendBatch!.CommitReservedAppendFromSpans(
                                     reservedAppend,
                                     completionSource,
                                     callback,
                                     headers,
                                     recordSize);
                                committedReservedAppend = true;
                                ProducerDebugCounters.RecordMessageAppended(hasCompletionSource: completionSource is not null);
                                ClearAppendInProgressUnderLock(pd);
                                ownsReservation = false;
                                ownsHeaderResources = false;
                                if (reservedFirstAwaitedProduceInBatch)
                                    Interlocked.Increment(ref _pendingAwaitedProduceCount);

                                if (ShouldSealAppendedBatch(reservedAppendBatch))
                                {
                                    batchToComplete = DetachCurrentBatchForSealUnderLock(pd, reservedAppendBatch);
                                    ownsRotation = true;
                                }
                                else if (reservedAppendStartedNewBatch)
                                {
                                    TrackCurrentBatchForLinger(pd, topicPartition, reservedAppendBatch);
                                    ClearRotationInProgressUnderLock(pd);
                                    ownsRotation = false;
                                }
                                else if (completionSource is not null)
                                {
                                    QueueLingerPartition(pd, topicPartition);
                                }
                            }
                        }

                        if (disposedAfterReserve)
                        {
                            ReleaseOwnedAppendState();
                            return false;
                        }

                        NotifyRecordAppended(topic, partition, reservedActualBytesAdded, partitionCount);
                        if (batchToComplete is not null)
                        {
                            var readyBatch = CompleteDetachedBatchAndEnqueue(pd, batchToComplete);
                            if (readyBatch is not null)
                                StartPreSerialization(readyBatch);
                            ownsRotation = false;
                        }

                        ReleaseDetachedBatchBytesIfSafe();
                        return true;
                    }
                    catch
                    {
                        if (!committedReservedAppend)
                        {
                            using var guard = new SpinLockGuard(ref pd.Lock);
                            PartitionBatch.CancelReservedAppend(reservedAppend);
                            ClearAppendAndRotationInProgressUnderLock();
                        }

                        throw;
                    }
                }

                if (disposed)
                {
                    ReleaseOwnedAppendState();
                    return false;
                }

                if (messageTooLarge)
                {
                    ReleaseOwnedAppendState();
                    throw new KafkaException(ErrorCode.MessageTooLarge,
                        $"Record of size {recordSize} exceeds maximum batch size of {_options.BatchSize}");
                }

                if (waitForRotation)
                {
                    spinner.SpinOnce();
                    continue;
                }

                if (batchToComplete is not null)
                {
                    try
                    {
                        sealedBatchToEnqueue = CompleteDetachedBatch(
                            batchToComplete,
                            out sealedBatchBytesToRelease,
                            releaseMemoryOnFailure: false);
                        if (sealedBatchToEnqueue is not null)
                        {
                            sealedBatchToEnqueue.UnackedBudget = ResolveUnackedBudget(
                                topic,
                                partition);
                        }
                    }
                    catch
                    {
                        ClearRotationAndReleaseDetachedBatchBytes(pd, ref sealedBatchBytesToRelease);
                        throw;
                    }
                }

                if (needBatch && rentedBatch is null)
                    rentedBatch = RentBatch(topicPartition, partitionCount);
            }
        }
        catch
        {
            ReleaseOwnedAppendState();
            throw;
        }
    }

    /// <summary>
    /// Helper: tries to append a record to the given batch.
    /// Called under the deque lock.
    /// </summary>
    private static bool TryAppendToBatch(
        PartitionBatch batch,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int estimatedSize,
        out int actualBytesAdded,
        out bool firstAwaitedProduceInBatch)
    {
        actualBytesAdded = 0;
        firstAwaitedProduceInBatch = false;
        var estimatedSizeBefore = batch.EstimatedSize;
        var result = batch.TryAppend(timestamp, key, value, headers, headerCount, completionSource, callback, estimatedSize);

        if (result.Success)
        {
            ProducerDebugCounters.RecordMessageAppended(hasCompletionSource: completionSource is not null);
            actualBytesAdded = batch.EstimatedSize - estimatedSizeBefore;
            firstAwaitedProduceInBatch = result.FirstCompletionSourceInBatch;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Async version of AppendFromSpans that handles backpressure without blocking.
    /// Hot path: non-blocking CAS reservation — no async state machine allocated.
    /// Cold path: copies span data to <see cref="PooledMemory"/> before awaiting memory reservation.
    /// </summary>
    /// <returns>true if appended successfully, false if the accumulator is disposed.</returns>
    internal ValueTask<bool> AppendFromSpansAsync(
        string topic,
        int partition,
        long timestamp,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        Header[]? headers,
        int headerCount,
        Action<RecordMetadata, Exception?>? callback,
        CancellationToken cancellationToken,
        int partitionCount = 0)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return new ValueTask<bool>(false);

        var keyLength = keyIsNull ? 0 : keyData.Length;
        var valueLength = valueIsNull ? 0 : valueData.Length;
        int recordSize;
        try
        {
            recordSize = PartitionBatch.EstimateRecordSize(keyLength, valueLength, headers, headerCount);
            ThrowIfRecordExceedsMaxRequestSize(
                topic, partition, keyIsNull, keyLength, valueIsNull, valueLength,
                headers, headerCount, recordSize);
        }
        catch
        {
            ReturnPooledHeaders(headers);
            throw;
        }

        // Hot path: non-blocking gate check + CAS reservation — no async state machine
        // allocated. An over-budget broker applies the same backpressure as a full buffer.
        if (TryAdmitAndReserve(topic, partition, recordSize))
            return new ValueTask<bool>(AppendFromSpansAfterReservation(topic, partition, timestamp,
                keyData, keyIsNull, valueData, valueIsNull, headers, headerCount, callback, recordSize, partitionCount));

        // Cold path: buffer full. Copy spans to PooledMemory BEFORE the await boundary
        // (ReadOnlySpan<byte> cannot survive across async suspension points).
        var keyPooled = keyIsNull ? PooledMemory.Null : CopySpanToPooledMemory(keyData);
        var valuePooled = valueIsNull ? PooledMemory.Null : CopySpanToPooledMemory(valueData);

        return AppendFromSpansSlowPathPooled(topic, partition, timestamp, keyPooled, valuePooled,
            headers, headerCount, callback, recordSize, partitionCount, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfRecordExceedsMaxRequestSize(
        string topic,
        int partition,
        bool keyIsNull,
        int keyLength,
        bool valueIsNull,
        int valueLength,
        Header[]? headers,
        int headerCount,
        int estimatedRecordSize)
    {
        var maxRequestSize = _options.MaxRequestSize > 0
            ? _options.MaxRequestSize
            : ProduceRequestSizeCalculator.DefaultMaxRequestSize;
        var fixedSize = GetSingleBatchRequestFixedSize(topic);
        var estimatedBatchSize = (long)RecordBatch.TotalBatchHeaderSize + estimatedRecordSize;
        if (estimatedBatchSize <= int.MaxValue)
        {
            var estimatedRequestBodySize = fixedSize +
                ProduceRequestSizeCalculator.CompactBytesLengthSize((int)estimatedBatchSize) +
                estimatedBatchSize;
            if (estimatedRequestBodySize <= maxRequestSize)
                return;
        }

        ThrowIfRecordExceedsMaxRequestSizeSlow(
            topic, partition, keyIsNull, keyLength, valueIsNull, valueLength,
            headers, headerCount, maxRequestSize, fixedSize);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIfRecordExceedsMaxRequestSizeSlow(
        string topic,
        int partition,
        bool keyIsNull,
        int keyLength,
        bool valueIsNull,
        int valueLength,
        Header[]? headers,
        int headerCount,
        int maxRequestSize,
        int singleBatchFixedSize)
    {
        var bodySize = Record.ComputeBodySize(
            timestampDelta: 0,
            offsetDelta: 0,
            keyIsNull,
            keyLength,
            valueIsNull,
            valueLength,
            headers,
            headerCount);
        var encodedRecordSize = checked(Record.VarIntSize(bodySize) + bodySize);
        var encodedBatchSize = checked(RecordBatch.TotalBatchHeaderSize + encodedRecordSize);
        var encodedRequestBodySize = ProduceRequestSizeCalculator.GetSingleBatchRequestBodySize(
            singleBatchFixedSize,
            encodedBatchSize);
        if (encodedRequestBodySize <= maxRequestSize)
            return;

        throw new ProduceException(
            ErrorCode.MessageTooLarge,
            $"Encoded ProduceRequest body size {encodedRequestBodySize} bytes " +
            $"(record batch {encodedBatchSize} bytes) exceeds MaxRequestSize of {maxRequestSize} bytes.")
        {
            Topic = topic,
            Partition = partition
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetSingleBatchRequestFixedSize(string topic)
    {
        if (_singleBatchRequestFixedSizes.TryGetValue(topic, out var fixedSize))
            return fixedSize;

        fixedSize = ProduceRequestSizeCalculator.GetSingleBatchFixedSize(
            _options.TransactionalId,
            topic);
        return _singleBatchRequestFixedSizes.GetOrAdd(topic, fixedSize);
    }

    /// <summary>
    /// Synchronous append-under-lock logic for span-based append after memory has been reserved.
    /// </summary>
    private bool AppendFromSpansAfterReservation(
        string topic,
        int partition,
        long timestamp,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        Header[]? headers,
        int headerCount,
        Action<RecordMetadata, Exception?>? callback,
        int recordSize,
        int partitionCount)
    {
        return AppendFromSpansAfterReservationCore(topic, partition, timestamp, keyData, keyIsNull,
            valueData, valueIsNull, headers, headerCount, completionSource: null, callback,
            recordSize, partitionCount, returnHeadersOnFailure: true);
    }

    /// <summary>
    /// Zero-allocation slow path for span-based append using pooled <see cref="PendingAppend"/>.
    /// Delegates to <see cref="AppendSlowPathPooled"/> with null completionSource.
    /// </summary>
    private ValueTask<bool> AppendFromSpansSlowPathPooled(
        string topic,
        int partition,
        long timestamp,
        PooledMemory keyPooled,
        PooledMemory valuePooled,
        Header[]? headers,
        int headerCount,
        Action<RecordMetadata, Exception?>? callback,
        int recordSize,
        int partitionCount,
        CancellationToken cancellationToken)
    {
        return AppendSlowPathPooled(topic, partition, timestamp, keyPooled, valuePooled,
            headers, headerCount, null, callback, recordSize, cancellationToken, partitionCount);
    }

    /// <summary>
    /// Copies a ReadOnlySpan to a PooledMemory backed by ArrayPool.
    /// Used on the cold path to preserve span data across async boundaries.
    /// </summary>
    private static PooledMemory CopySpanToPooledMemory(ReadOnlySpan<byte> data)
    {
        var array = ProducerDataPool.BytePool.Rent(data.Length);
        data.CopyTo(array);
        return new PooledMemory(array, data.Length);
    }

    /// <summary>
    /// Helper: reserves space for a span-backed record using arena zero-copy.
    /// Called under the deque lock.
    /// </summary>
    private static bool TryReserveAppendFromSpansToBatch(
        PartitionBatch batch,
        long timestamp,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int estimatedSize,
        out PartitionBatch.ReservedRecordAppend reservedAppend,
        out int actualBytesAdded,
        out bool firstAwaitedProduceInBatch)
    {
        actualBytesAdded = 0;
        firstAwaitedProduceInBatch = false;
        var result = batch.TryReserveAppendFromSpans(timestamp, keyData, keyIsNull, valueData, valueIsNull,
            headers, headerCount, completionSource, callback, out reservedAppend);

        if (result.Success)
        {
            actualBytesAdded = result.ActualSizeAdded;
            firstAwaitedProduceInBatch = result.FirstCompletionSourceInBatch;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Detaches the current batch from a partition deque for completion outside the SpinLock.
    /// MUST be called under pd.Lock.
    /// </summary>
    private PartitionBatch DetachCurrentBatchForSealUnderLock(PartitionDeque pd, PartitionBatch currentBatch)
    {
        pd.CurrentBatch = null;
        MarkLingerPartitionDequeued(pd);
        pd.RotationInProgress = true;
        Interlocked.Decrement(ref _unsealedBatchCount);
        return currentBatch;
    }

    /// <summary>
    /// Completes and returns a batch removed from the active append path.
    /// The caller must release <paramref name="bytesToRelease"/> after the partition
    /// rotation gate is cleared. On success this is the seal-time overestimate refund.
    /// On failure this is the whole reserved batch size unless released here.
    /// </summary>
    private ReadyBatch? CompleteDetachedBatch(
        PartitionBatch currentBatch,
        out int bytesToRelease,
        bool preSerialize = true,
        bool releaseMemoryOnFailure = true)
    {
        bytesToRelease = currentBatch.OverestimatedBytes;
        var completionSourcesCount = currentBatch.CompletionSourcesCount;
        var pendingAwaitedProduceBatchReleased = false;
        try
        {
            var readyBatch = currentBatch.Complete();
            if (readyBatch is not null)
            {
                ReleasePendingAwaitedProduceBatch(completionSourcesCount, ref pendingAwaitedProduceBatchReleased);
                ProducerDebugCounters.RecordBatchCompleted(readyBatch.CompletionSourcesCount);
                if (currentBatch.PartitionCount > 1)
                    _onBatchComplete?.Invoke(readyBatch.TopicPartition.Topic, currentBatch.PartitionCount);
                if (preSerialize)
                {
                    if (_options.CompressionType == CompressionType.None)
                        PrepareBatchForPublish(readyBatch, readyBatch.Generation);
                    else
                        readyBatch.SetPreSerializationTask(CreatePreSerializationTask(readyBatch));
                }
            }

            _batchPool.Return(currentBatch);
            return readyBatch;
        }
        catch (Exception ex)
        {
            ReleasePendingAwaitedProduceBatch(completionSourcesCount, ref pendingAwaitedProduceBatchReleased);
            bytesToRelease = currentBatch.FailCompleteFailure(ex);
            try { _batchPool.Return(currentBatch); }
            catch (Exception returnEx) { LogBatchCleanupStepFailed(returnEx); }
            if (releaseMemoryOnFailure)
                ReleaseDetachedBatchBytes(ref bytesToRelease);
            throw;
        }
    }

    private void ReleasePendingAwaitedProduceBatch(int completionSourcesCount, ref bool released)
    {
        if (released || completionSourcesCount <= 0)
            return;

        Interlocked.Decrement(ref _pendingAwaitedProduceCount);
        released = true;
    }

    private bool TryCompleteDetachedBatchForCleanup(
        PartitionBatch currentBatch,
        out ReadyBatch? readyBatch,
        out int bytesToRelease)
    {
        try
        {
            readyBatch = CompleteDetachedBatch(
                currentBatch,
                out bytesToRelease,
                preSerialize: false);
            return true;
        }
        catch (Exception completeEx)
        {
            LogBatchCleanupStepFailed(completeEx);
            readyBatch = null;
            bytesToRelease = 0;
            return false;
        }
    }

    private void NotifyRecordAppended(string topic, int partition, int actualBytesAdded, int partitionCount)
    {
        if (partitionCount > 1 && actualBytesAdded > 0)
            _onRecordAppended?.Invoke(topic, partition, actualBytesAdded, partitionCount);
    }

    private static long SaturatingAdd(long current, int value)
    {
        var result = current + value;
        return result < current ? long.MaxValue : result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldSealAppendedBatch(PartitionBatch batch)
        => batch.IsExactlyAtSizeLimit
            || (_options.LingerMs == 0
                && !ShouldDeferPartialBatchSeal(batch)
                && batch.ShouldFlush(Stopwatch.GetTimestamp(), _options.LingerMs));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldDeferPartialBatchSeal(PartitionBatch batch)
    {
        var topicPartition = batch.TopicPartition;
        var hasPipelineBatch = _partitionQueueBytes.TryGetValue(topicPartition, out var queuedBytes)
            && queuedBytes > 0;
        return ShouldDeferPartialBatchSeal(
            _mutedPartitions.ContainsKey(topicPartition),
            _serializeBatchesPerPartition,
            hasPipelineBatch,
            batch.EstimatedSize,
            batch.EffectiveBatchSizeLimit);
    }

    // An earlier pipeline batch provides a short delivery clock under load. Keep the next
    // partial batch open until it fills; if traffic becomes sparse, the predecessor exits,
    // queued bytes reach zero, and the next linger pass seals normally. Size-full batches
    // bypass this method in ShouldSealAppendedBatch and continue pipelining immediately.
    internal static bool ShouldDeferPartialBatchSeal(
        bool isMuted,
        bool serializeBatchesPerPartition,
        bool hasPipelineBatch,
        int currentBatchSize,
        int maximumBatchSize) =>
        isMuted
        || (hasPipelineBatch
            && (serializeBatchesPerPartition
                || currentBatchSize < maximumBatchSize));

    private ReadyBatch? CompleteDetachedBatchAndEnqueue(PartitionDeque pd, PartitionBatch batchToComplete)
    {
        ReadyBatch? readyBatch;
        var bytesToRelease = 0;
        try
        {
            readyBatch = CompleteDetachedBatch(
                batchToComplete,
                out bytesToRelease,
                releaseMemoryOnFailure: false);
        }
        catch
        {
            ClearRotationAndReleaseDetachedBatchBytes(pd, ref bytesToRelease);
            throw;
        }

        if (readyBatch is not null)
        {
            readyBatch.UnackedBudget = ResolveUnackedBudget(
                readyBatch.TopicPartition.Topic,
                readyBatch.TopicPartition.Partition);
        }

        {
            using var guard = new SpinLockGuard(ref pd.Lock);
            if (readyBatch is not null)
                EnqueueCompletedBatchUnderLock(pd, readyBatch);
            ClearRotationInProgressUnderLock(pd);
        }

        if (bytesToRelease > 0)
            ReleaseMemory(bytesToRelease);

        return readyBatch;
    }

    /// <summary>
    /// Enqueues a completed ready batch into the partition deque.
    /// MUST be called under pd.Lock.
    /// </summary>
    private void EnqueueCompletedBatchUnderLock(PartitionDeque pd, ReadyBatch readyBatch)
    {
        OnBatchEntersPipeline(pd, readyBatch);
        pd.AddLast(readyBatch);
        ProducerDebugCounters.RecordBatchQueuedToReady();
    }

    /// <summary>
    /// Clears a stuck rotation gate if completion fails before the normal install/enqueue phase.
    /// </summary>
    private static void ClearRotationInProgressUnderLock(PartitionDeque pd)
    {
        pd.RotationInProgress = false;
    }

    private static void ClearRotationInProgress(PartitionDeque pd)
    {
        using var guard = new SpinLockGuard(ref pd.Lock);
        ClearRotationInProgressUnderLock(pd);
    }

    private void ClearRotationAndReleaseDetachedBatchBytes(PartitionDeque pd, ref int bytesToRelease)
    {
        ClearRotationInProgress(pd);
        ReleaseDetachedBatchBytes(ref bytesToRelease);
    }

    private void ReleaseDetachedBatchBytes(ref int bytesToRelease)
    {
        if (bytesToRelease <= 0)
            return;

        var bytes = bytesToRelease;
        bytesToRelease = 0;
        ReleaseMemory(bytes);
    }

    private static void ClearAppendInProgressUnderLock(PartitionDeque pd)
    {
        pd.AppendInProgress = false;
    }

    /// <summary>
    /// Publishes a sealed, wire-ready batch to the sender loop.
    /// </summary>
    private void PublishSealedBatch(ReadyBatch readyBatch, bool signalWakeup = true)
    {
        _readyPartitions.Enqueue(readyBatch.TopicPartition);
        if (signalWakeup)
            SignalWakeup();
    }

    /// <summary>
    /// Starts pre-serialization after the batch is visible in its partition deque, so the
    /// worker cannot publish a notification before the sender can observe the batch.
    /// </summary>
    private void StartPreSerialization(ReadyBatch readyBatch)
    {
        if (readyBatch.IsPreSerialized)
        {
            PublishSealedBatch(readyBatch);
            return;
        }

        readyBatch.StartPreSerializationTask();
    }

    private Task CreatePreSerializationTask(ReadyBatch readyBatch)
    {
        var generation = readyBatch.Generation;
        return new Task(
            static state =>
            {
                var workItem = (PreSerializationWorkItem)state!;
                workItem.Accumulator.PreSerializeAndPublishBatch(workItem.Batch, workItem.Generation);
            },
            new PreSerializationWorkItem(this, readyBatch, generation),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach);
    }

    private void PreSerializeAndPublishBatch(ReadyBatch readyBatch, int generation)
    {
        if (PrepareBatchForPublish(readyBatch, generation) && readyBatch.IsCurrentIncarnation(generation))
            PublishSealedBatch(readyBatch);
    }

    private sealed class PreSerializationWorkItem(
        RecordAccumulator accumulator,
        ReadyBatch batch,
        int generation)
    {
        public RecordAccumulator Accumulator { get; } = accumulator;

        public ReadyBatch Batch { get; } = batch;

        public int Generation { get; } = generation;
    }

    /// <summary>
    /// Prepares a sealed batch's records for send, compressing when configured. Runs on a
    /// worker after the batch is enqueued, outside producer caller threads and outside the
    /// partition rotation gate. Record bytes are already encoded at append time, so this step
    /// either leaves the arena-backed records in place or compresses those encoded bytes.
    ///
    /// Thread safety: the batch is already sealed and detached from its mutable
    /// <see cref="PartitionBatch"/>, so no append thread can modify it. The drain loop skips
    /// batches where <see cref="ReadyBatch.IsPreSerialized"/> is false, so the sender only
    /// picks up wire-ready batches.
    /// </summary>
    private bool PrepareBatchForPublish(ReadyBatch readyBatch, int generation)
    {
        if (!readyBatch.TryAcquireResourcePin(generation))
            return false;

        try
        {
            Exception? failure = null;
            try
            {
                readyBatch.RecordBatch.PreCompress(_options.CompressionType, _compressionCodecs);
                readyBatch.SetEncodedSize(readyBatch.RecordBatch.GetEncodedSize(_options.CompressionType));
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            if (!readyBatch.IsCurrentIncarnation(generation))
                return false;

            // Mark pre-serialization complete so the drain loop can pick up this batch.
            readyBatch.MarkPreSerialized();
            if (failure is null)
                return !readyBatch.IsSendCompleted && readyBatch.IsCurrentIncarnation(generation);

            return readyBatch.IsCurrentIncarnation(generation) && readyBatch.FailFromPreSerialization(failure);
        }
        finally
        {
            readyBatch.ReleaseResourcePin();
        }
    }

    /// <summary>
    /// Gets the current buffered memory usage in bytes.
    /// </summary>
    public long BufferedBytes => Volatile.Read(ref _bufferedBytes);

    /// <summary>
    /// Gets the maximum buffer memory limit in bytes.
    /// </summary>
    public ulong MaxBufferMemory => (ulong)Volatile.Read(ref _maxBufferMemory);

    /// <summary>
    /// The configured max.block.ms value. Exposed for <see cref="PendingAppend"/> timeout diagnostics.
    /// </summary>
    internal int MaxBlockMsOption => _options.MaxBlockMs;

    /// <summary>
    /// Atomically updates the maximum buffer memory limit. Used by the global
    /// memory budget for dynamic rebalancing.
    /// Growing the limit signals waiting producers so they re-check immediately.
    /// Shrinking the limit takes effect on the next reservation attempt — in-flight
    /// reservations are not revoked.
    /// </summary>
    internal void SetMaxBufferMemory(ulong newLimit)
    {
        // Concurrent calls from overlapping budget rebalances race on this write —
        // whichever Interlocked.Exchange runs last wins. That is safe: each rebalance
        // is computed under DekafMemoryBudget._lock against the latest registry state,
        // so the last-writer-wins outcome still matches the latest logical budget, and
        // the next Register/Unregister will rebalance again if the state changes.
        // Do NOT add "only apply if newer" logic — there is no monotonic version here.
        var previous = (ulong)Volatile.Read(ref _maxBufferMemory);
        if (previous == newLimit)
            return;

        Interlocked.Exchange(ref _maxBufferMemory, (long)newLimit);

        // Growing: wake any waiters so they retry their reservation immediately.
        if (newLimit > previous)
            SignalBufferSpaceAvailable();
    }

    /// <summary>Test-only synchronous reservation helper.</summary>
    internal bool TryReserveMemoryForTest(int size) => TryReserveMemory(size);

    /// <summary>
    /// Gets the cumulative count of buffer pressure events (slow-path entries in memory reservation).
    /// Used by adaptive connection scaling to detect sustained backpressure.
    /// </summary>
    internal long BufferPressureEvents => Volatile.Read(ref _bufferPressureEvents);

    internal int PendingAppendCountForTest => _pendingAppends.Count;

    /// <summary>
    /// Gets the current buffer utilization as a ratio (0.0 to 1.0+).
    /// Used by adaptive connection scaling to confirm buffer is actually full.
    /// </summary>
    internal double BufferUtilization => (double)Volatile.Read(ref _bufferedBytes) / (double)(ulong)Volatile.Read(ref _maxBufferMemory);

    /// <summary>
    /// Attempts to reserve buffer memory for a record without blocking.
    /// Uses atomic add with rollback so contention cannot be misreported as exhaustion.
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
        // Hoisted once per call — rebalancing is rare and one-cycle staleness is acceptable
        // (matches the snapshot semantics already used for _bufferedBytes).
        var max = (ulong)Volatile.Read(ref _maxBufferMemory);
        var newValue = Interlocked.Add(ref _bufferedBytes, recordSize);

        if ((ulong)newValue <= max)
            return true;

        Interlocked.Add(ref _bufferedBytes, -recordSize);
        return false;
    }

    /// <summary>
    /// Prepares for an async reservation wait: tracks pressure for adaptive connection scaling,
    /// triggers pool ratcheting if needed, and computes the deadline for max.block.ms timeout.
    /// Returns the start ticks and deadline for use in the reservation loop.
    /// </summary>
    private (long StartTicks, long Deadline) BeginReservationWait(int recordSize)
    {
        // Track for adaptive connection scaling
        var pressureCount = Interlocked.Increment(ref _bufferPressureEvents);

        // Dynamic pool ratchet: when sustained cold-path pressure is detected, increase
        // ProducerDataPool bucket capacity to reduce pool misses.
        MaybeRatchetPoolCapacity(pressureCount);

        var currentBufferedBytes = Volatile.Read(ref _bufferedBytes);
        var currentMaxBufferMemory = (ulong)Volatile.Read(ref _maxBufferMemory);
        LogBufferMemoryWaiting(recordSize, currentBufferedBytes, currentMaxBufferMemory);
        var currentTicks = Dekaf.MonotonicClock.GetMilliseconds();
        var deadline = (long.MaxValue - currentTicks > _options.MaxBlockMs)
            ? currentTicks + _options.MaxBlockMs
            : long.MaxValue;

        return (currentTicks, deadline);
    }

    /// <summary>
    /// Executes the reservation wait loop: spins on <see cref="TryReserveMemory"/> with async
    /// semaphore-based wake-ups from <see cref="ReleaseMemory"/>, respecting disposal, cancellation,
    /// and the max.block.ms deadline. Must be called after <see cref="BeginReservationWait"/>.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="ReserveMemoryAsync"/> (called from tests).
    /// The production slow paths now use the pooled <see cref="PendingAppend"/> mechanism
    /// instead of inlining this loop.
    /// </remarks>
    private async ValueTask WaitForReservationAsync(int recordSize, long startTicks, long deadline,
        CancellationToken cancellationToken)
    {
        // Avoid CreateLinkedTokenSource allocation by using the caller's token directly
        // and checking disposal manually on each wake-up.
        while (!TryReserveMemory(recordSize))
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(RecordAccumulator));

            cancellationToken.ThrowIfCancellationRequested();

            var remainingMs = deadline - Dekaf.MonotonicClock.GetMilliseconds();
            if (remainingMs <= 0)
                ThrowBufferMemoryTimeout(recordSize, startTicks);

            Interlocked.Increment(ref _bufferSpaceWaiters);

            // Re-check AFTER incrementing to close the missed-signal window.
            // Without this, ReleaseMemory can see waiters=0 and skip the signal.
            if ((ulong)Volatile.Read(ref _bufferedBytes) < (ulong)Volatile.Read(ref _maxBufferMemory))
            {
                Interlocked.Decrement(ref _bufferSpaceWaiters);
                continue; // space likely available, re-evaluate TryReserveMemory
            }

            try
            {
                await _bufferSpaceSignal.Task.WaitAsync(
                    TimeSpan.FromMilliseconds(Math.Min(remainingMs, int.MaxValue)),
                    cancellationToken
                ).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Timed out waiting for signal — loop back and re-check.
            }
            catch (OperationCanceledException) when (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(RecordAccumulator));
            }
            finally
            {
                Interlocked.Decrement(ref _bufferSpaceWaiters);
            }
        }
    }

    /// <summary>
    /// Public-facing reservation method for direct callers (including tests).
    /// Delegates to <see cref="BeginReservationWait"/> + <see cref="WaitForReservationAsync"/>.
    /// Internal slow paths inline the loop body to avoid a nested async state machine.
    /// </summary>
    internal ValueTask ReserveMemoryAsync(int recordSize, CancellationToken cancellationToken)
    {
        if (TryReserveMemory(recordSize))
            return default;

        var (startTicks, deadline) = BeginReservationWait(recordSize);
        return WaitForReservationAsync(recordSize, startTicks, deadline, cancellationToken);
    }

    /// <summary>
    /// Ratchets the <see cref="ProducerDataPool.BytePool"/> bucket capacity when sustained
    /// cold-path pressure is detected. Fires after every 1,000 pressure events, doubling
    /// the capacity each time up to 4,096. This ensures the pool adapts to workloads where
    /// Acks.All or slow brokers cause the buffer to stay full, forcing every message through
    /// the cold path that rents PooledMemory arrays.
    /// </summary>
    private void MaybeRatchetPoolCapacity(long pressureCount)
    {
        const int RatchetThreshold = 1_000;
        const int MaxRatchetCapacity = 4096;

        var lastRatchet = Volatile.Read(ref _lastPoolRatchetPressure);
        if (pressureCount - lastRatchet < RatchetThreshold)
            return;

        // CAS to claim this ratchet event (only one thread ratchets per threshold crossing)
        if (Interlocked.CompareExchange(ref _lastPoolRatchetPressure, pressureCount, lastRatchet) != lastRatchet)
            return;

        var currentCapacity = ProducerDataPool.CurrentArraysPerBucket;
        var newCapacity = Math.Min(currentCapacity * 2, MaxRatchetCapacity);
        if (newCapacity > currentCapacity)
            ProducerDataPool.RatchetBucketCapacity(newCapacity);
    }

    private void ThrowBufferMemoryTimeout(int recordSize, long startTicks)
    {
        var configured = TimeSpan.FromMilliseconds(_options.MaxBlockMs);
        var elapsed = TimeSpan.FromMilliseconds(Dekaf.MonotonicClock.GetMilliseconds() - startTicks);
        throw new KafkaTimeoutException(
            TimeoutKind.MaxBlock,
            elapsed,
            configured,
            $"Failed to allocate buffer within max.block.ms ({_options.MaxBlockMs}ms). " +
            $"Requested {recordSize} bytes, current usage: {Volatile.Read(ref _bufferedBytes)}/{(ulong)Volatile.Read(ref _maxBufferMemory)} bytes. " +
            $"Producer is generating messages faster than the network can send them. " +
            $"Consider: increasing BufferMemory, increasing MaxBlockMs, reducing production rate, or checking network connectivity.");
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

        // First try to serve queued PendingAppend operations with the freed space.
        // This avoids the TCS allocation + async wakeup overhead when pending ops exist.
        DrainPendingAppends();

        // Signal that space is available — wake async waiters via semaphore.
        SignalBufferSpaceAvailable();
    }

    /// <summary>
    /// Releases a batch's BufferMemory reservation exactly once. Pool reset waits for this
    /// operation to finish, so the winning thread cannot observe a cleared or reused DataSize.
    /// </summary>
    internal void ReleaseBatchMemory(ReadyBatch batch)
    {
        if (!batch.TryBeginMemoryRelease(out var dataSize))
            return;

        try
        {
            ReleaseMemory(dataSize);
        }
        finally
        {
            batch.CompleteMemoryRelease();
        }
    }

    /// <summary>
    /// Releases reserved buffer memory without triggering <see cref="DrainPendingAppends"/>.
    /// Used inside <see cref="DrainPendingAppends"/> when a claimed operation was already
    /// completed by timeout/cancel and the memory reservation must be returned.
    /// Avoids infinite recursion: ReleaseMemory → DrainPendingAppends → ReleaseMemoryWithoutDrain.
    /// </summary>
    private void ReleaseMemoryWithoutDrain(int size)
    {
        Interlocked.Add(ref _bufferedBytes, -size);
        // No drain or signal — we're already inside the drain loop.
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
        if (Volatile.Read(ref _disposed) != 0)
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
                MarkLingerPartitionDequeued(pd);
            }
        }
    }

    /// <remarks>
    /// Optimized with multiple fast paths:
    /// 1. No-unsealed-batch check avoids linger work entirely.
    /// 2. Oldest batch age check skips active queue draining if no fire-and-forget batch can be ready.
    /// 3. Active linger queue avoids scanning all historical partition deques under awaited load.
    /// Also uses synchronous TryWrite when possible to avoid async overhead.
    /// </remarks>
    public ValueTask ExpireLingerAsync(CancellationToken cancellationToken)
    {
        MaybeRecordBrokerBudgetDiagnosticSample();

        // Fast path 1: no unsealed batches to check - avoid queue draining and async overhead entirely
        if (!HasUnsealedBatches())
        {
            // Reset oldest batch tracking since there are no batches
            Volatile.Write(ref _oldestBatchCreatedTicks, long.MaxValue);
            return default;
        }

        // Fast path 2: if the oldest batch hasn't reached linger time yet AND there are no
        // pending awaited produces, skip active queue checks. Awaited produces (ProduceAsync)
        // use a micro-linger (min(1ms, LingerMs/10)) so they need more frequent checks.
        if (Volatile.Read(ref _pendingAwaitedProduceCount) == 0)
        {
            var oldestTicks = Volatile.Read(ref _oldestBatchCreatedTicks);
            if (oldestTicks != long.MaxValue)
            {
                var millisSinceOldest = (long)Stopwatch.GetElapsedTime(oldestTicks).TotalMilliseconds;
                if (millisSinceOldest < _options.LingerMs)
                {
                    // No batch is old enough to flush yet.
                    return default;
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
    /// false = linger mode: seals expired batches from the active linger notification queue.
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
            var anySealed = false;

            if (sealAll)
            {
                foreach (var kvp in _partitionDeques)
                {
                    if (TrySealCurrentBatch(kvp.Key, kvp.Value, now, sealAll: true, cancellationToken, ref newOldestTicks))
                        anySealed = true;
                }
            }
            else
            {
                var count = _lingerPartitions.Count;
                for (var i = 0; i < count; i++)
                {
                    if (!_lingerPartitions.TryDequeue(out var topicPartition))
                        break;

                    if (!_partitionDeques.TryGetValue(topicPartition, out var pd))
                        continue;

                    MarkLingerPartitionDequeued(pd);
                    if (TrySealCurrentBatch(topicPartition, pd, now, sealAll: false, cancellationToken, ref newOldestTicks))
                        anySealed = true;
                }
            }

            if (!sealAll)
            {
                // This is a best-effort lower-bound hint. Linger mode drains a queue snapshot,
                // so a concurrent append or queued partition outside this snapshot may be older
                // than newOldestTicks. Only lower the hint; raising it can delay an already
                // expired batch until a newer batch reaches linger.
                if (newOldestTicks != long.MaxValue)
                    TrackOldestBatchCreated(newOldestTicks);
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

    private bool TrySealCurrentBatch(
        TopicPartition topicPartition,
        PartitionDeque pd,
        long now,
        bool sealAll,
        CancellationToken cancellationToken,
        ref long newOldestTicks)
    {
        ReadyBatch? sealedBatch = null;
        PartitionBatch? batchToComplete = null;
        var spinner = new SpinWait();

        while (true)
        {
            var waitForRotation = false;

            {
                using var guard = new SpinLockGuard(ref pd.Lock);

                if (pd.RotationInProgress || pd.AppendInProgress)
                {
                    waitForRotation = true;
                }
                else if (pd.CurrentBatch is null)
                {
                    break;
                }
                // A muted or serialized busy partition already has an earlier batch to send.
                // Keep its partial arena open so zero-linger appends coalesce instead of
                // allocating one BatchSize arena per record while they wait. Size-full batches
                // still seal in the append path, and FlushAsync must always seal.
                else if (sealAll
                    || (!ShouldDeferPartialBatchSeal(pd.CurrentBatch)
                        && pd.CurrentBatch.ShouldFlush(now, _options.LingerMs)))
                {
                    ProducerDebugCounters.RecordBatchFlushedFromDictionary();
                    batchToComplete = DetachCurrentBatchForSealUnderLock(pd, pd.CurrentBatch);
                    break;
                }
                else
                {
                    var batchCreatedTicks = pd.CurrentBatch.CreatedAtStopwatchTimestamp;
                    if (batchCreatedTicks < newOldestTicks)
                        newOldestTicks = batchCreatedTicks;

                    if (!sealAll)
                        QueueLingerPartition(pd, topicPartition, signalLingerLoop: false);
                    break;
                }
            }

            if (!waitForRotation)
                break;

            if (!sealAll)
            {
                QueueLingerPartition(pd, topicPartition, signalLingerLoop: false);
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            spinner.SpinOnce();
        }

        if (batchToComplete is null)
            return false;

        sealedBatch = CompleteDetachedBatchAndEnqueue(pd, batchToComplete);

        if (sealedBatch is null)
            return false;

        StartPreSerialization(sealedBatch);
        return true;
    }

    /// <summary>
    /// Tracks a batch entering the pipeline: increments counter and adds to reference-tracking dictionary.
    /// Called when a batch is completed and enqueued to a partition deque.
    /// </summary>
    private void OnBatchEntersPipeline(PartitionDeque _, ReadyBatch batch)
    {
        if (_unackedBudgetEnabled)
            ChargeUnackedBudget(batch);

        _partitionQueueBytes.AddOrUpdate(
            batch.TopicPartition,
            static (_, batch) => batch.DataSize,
            static (_, current, batch) => SaturatingAdd(current, batch.DataSize),
            batch);

        // Counter first, list second. If a batch races between the counter increment
        // and the list add, the sweep will miss it in the snapshot — but that's acceptable
        // because the sweep is defense-in-depth at 3× delivery timeout and the batch will
        // be caught on the next sweep interval.
        if (_options.EnableDeliveryDiagnostics)
        {
            batch.EnableDeliveryDiagnostics();
            batch.AppendDiag('E');
        }
        Interlocked.Increment(ref _inFlightBatchCount);
        InFlightBatchListAdd(batch);
    }

    /// <summary>
    /// Removes a batch from the pipeline: decrements counter and removes from reference-tracking dictionary.
    /// Called after a batch is sent (CompleteSend) or failed (Fail).
    /// Must be called by KafkaProducer's SenderLoopAsync after processing each batch.
    /// </summary>
    /// <returns>true if the batch was successfully removed from tracking; false if it was already removed by another thread.</returns>
    internal bool OnBatchExitsPipeline(ReadyBatch batch)
    {
        // InFlightBatchListRemove acts as a natural atomic guard: only the first thread to
        // remove the batch proceeds with the decrement. This prevents double-decrement from
        // concurrent cleanup paths (e.g., DisposeAsync racing with SendLoopAsync's finally block).
        if (!InFlightBatchListRemove(batch))
            return false;

        if (Interlocked.Exchange(ref batch.UnackedBudget, null) is { } unackedBudget)
        {
            // DataSize is exactly what ChargeUnackedBudget charged; it stays valid until
            // Reset() at pool return, which cannot precede this first-exit path.
            unackedBudget.Release(batch.DataSize);
            // Freed unacked headroom can unblock appenders gated on this broker's budget —
            // wake them through the same machinery BufferMemory releases use.
            DrainPendingAppends();
            SignalBufferSpaceAvailable();
        }

        _partitionQueueBytes.AddOrUpdate(
            batch.TopicPartition,
            0,
            (_, current) => Math.Max(0, current - batch.DataSize));

        if (_options.EnableDeliveryDiagnostics)
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
            if (Volatile.Read(ref _closed) != 0)
                SignalWakeup();
        }

        return true;
    }

    /// <summary>
    /// Charges a freshly sealed batch against its current leader broker's unacked-byte budget.
    /// Called under the partition lock (same ordering domain as InFlightBatchListAdd) so a fast ack
    /// can never release before the charge lands. The budget is resolved outside this partition
    /// lock after completion, closing the append-to-seal leader-change window without adding
    /// metadata or dictionary work inside the lock. Drain and rerouting transfer later leader changes.
    /// </summary>
    private static void ChargeUnackedBudget(ReadyBatch batch)
    {
        if (batch.UnackedBudget is not { } budget)
            return;

        budget.Charge(batch.DataSize);
    }

    /// <summary>Transfers a live batch's charge when drain or retry routing selects another broker.</summary>
    internal void ReattributeUnackedBudget(ReadyBatch batch, int brokerId)
    {
        if (GetBrokerUnackedBudget(brokerId) is not { } newBudget)
            return;

        var lockTaken = false;
        try
        {
            _inFlightBatchLock.Enter(ref lockTaken);

            // Terminal cleanup removes the batch under this lock before releasing its
            // charge. Checking linkage prevents installing a new charge after that exit.
            if (!batch.InFlightLinked || ReferenceEquals(batch.UnackedBudget, newBudget))
                return;

            newBudget.Charge(batch.DataSize);
            var oldBudget = batch.UnackedBudget;
            batch.UnackedBudget = newBudget;
            oldBudget?.Release(batch.DataSize);
        }
        finally
        {
            if (lockTaken) _inFlightBatchLock.Exit();
        }

        DrainPendingAppends();
        SignalBufferSpaceAvailable();
    }

    /// <summary>
    /// Returns the unacked-byte budget for a broker, creating it on first use.
    /// Null when the bound is disabled (<see cref="ProducerOptions.DeliveryLatencyTargetMs"/> = 0
    /// or no leader resolver was wired).
    /// </summary>
    internal BrokerUnackedByteBudget? GetBrokerUnackedBudget(int brokerId)
    {
        if (!_unackedBudgetEnabled)
            return null;

        return _brokerUnackedBudgets.GetOrAdd(
            brokerId,
            static (_, accumulator) => accumulator.CreateBrokerUnackedBudget(),
            this);
    }

    private BrokerUnackedByteBudget CreateBrokerUnackedBudget()
    {
        var cap = _options.UnackedByteBudgetCapOverride
            ?? BrokerUnackedByteBudget.ComputeCap(_options.BatchSize, _options.ConnectionsPerBroker);
        // Cold start uses the cap. After the first drain sample, target × rate and the
        // minimum-RTT BDP guard provide time-derived floors; a fixed batch-byte floor makes
        // standing latency grow as per-broker drain rate falls under fan-out.
        return new BrokerUnackedByteBudget(
            _options.DeliveryLatencyTargetMs / 1000.0,
            floorBytes: 1,
            initialCapBytes: cap,
            lingerSeconds: _options.LingerMs / 1000.0);
    }

    /// <summary>
    /// Checks the broker gate before per-partition FIFO priority and buffer reservation, so
    /// every blocked attempt records pressure and never leaks a reservation. This is the
    /// single choke point for every append entry path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryAdmitAndReserve(string topic, int partition, int recordSize)
    {
        var partitionDeque = GetOrCreateDeque(topic, partition);
        if (_unackedBudgetEnabled
            && IsBrokerAdmissionBlocked(topic, partition, recordBlockEvent: true))
            return false;

        return Volatile.Read(ref partitionDeque.SlowPathAppendCount) == 0
            && TryReserveMemory(recordSize);
    }

    /// <summary>
    /// True when the destination broker for (topic, partition) is over its unacked-byte
    /// budget and message admission should block. Lock-free: a cached metadata lookup and
    /// volatile reads.
    /// Block events feed adaptive connection scaling; the pending-append drain passes false
    /// because the operation already recorded one when it was first gated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsBrokerAdmissionBlocked(string topic, int partition, bool recordBlockEvent)
        => IsBrokerAdmissionBlocked(topic, partition, recordBlockEvent, out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsBrokerAdmissionBlocked(
        string topic,
        int partition,
        bool recordBlockEvent,
        out int recheckDelayMilliseconds)
    {
        recheckDelayMilliseconds = Timeout.Infinite;
        if (!_unackedBudgetEnabled)
            return false;

        var leaderId = _resolveLeaderId!(topic, partition);
        var currentBudget = leaderId >= 0 ? GetBrokerUnackedBudget(leaderId) : null;
        if (leaderId < 0
            || currentBudget is null
            || !currentBudget.IsOverBudget())
        {
            return false;
        }

        var nowTicks = Stopwatch.GetTimestamp();
        if (!currentBudget.IsOverBudgetAt(nowTicks))
            return false;

        recheckDelayMilliseconds = currentBudget.GetAdmissionRecheckDelayMilliseconds(nowTicks);
        if (recordBlockEvent)
            currentBudget.RecordAdmissionBlock();
        return true;
    }

    private BrokerUnackedByteBudget? ResolveUnackedBudget(string topic, int partition)
    {
        if (!_unackedBudgetEnabled)
            return null;

        var leaderId = _resolveLeaderId!(topic, partition);
        return leaderId >= 0 ? GetBrokerUnackedBudget(leaderId) : null;
    }

    internal void IncrementSlowPathAppendCount(string topic, int partition)
        => Interlocked.Increment(ref GetOrCreateDeque(topic, partition).SlowPathAppendCount);

    internal void DecrementSlowPathAppendCount(string topic, int partition)
    {
        var remaining = Interlocked.Decrement(
            ref GetOrCreateDeque(topic, partition).SlowPathAppendCount);
        Debug.Assert(remaining >= 0, "Slow-path append count went negative.");
    }

    /// <summary>
    /// Adds a batch to the in-flight tracking list (intrusive doubly-linked list).
    /// Zero-allocation: uses embedded prev/next pointers in ReadyBatch.
    /// </summary>
    private void InFlightBatchListAdd(ReadyBatch batch)
    {
        var lockTaken = false;
        try
        {
            _inFlightBatchLock.Enter(ref lockTaken);

            batch.InFlightLinked = true;
            batch.InFlightPrev = _inFlightBatchTail;
            batch.InFlightNext = null;

            if (_inFlightBatchTail is not null)
                _inFlightBatchTail.InFlightNext = batch;
            else
                _inFlightBatchHead = batch;

            _inFlightBatchTail = batch;
        }
        finally
        {
            if (lockTaken) _inFlightBatchLock.Exit();
        }
    }

    /// <summary>
    /// Removes a batch from the in-flight tracking list.
    /// Returns true if the batch was successfully removed (first caller wins).
    /// Returns false if the batch was already removed by another thread.
    /// </summary>
    private bool InFlightBatchListRemove(ReadyBatch batch)
    {
        var lockTaken = false;
        try
        {
            _inFlightBatchLock.Enter(ref lockTaken);

            if (!batch.InFlightLinked)
                return false;

            batch.InFlightLinked = false;

            if (batch.InFlightPrev is not null)
                batch.InFlightPrev.InFlightNext = batch.InFlightNext;
            else
                _inFlightBatchHead = batch.InFlightNext;

            if (batch.InFlightNext is not null)
                batch.InFlightNext.InFlightPrev = batch.InFlightPrev;
            else
                _inFlightBatchTail = batch.InFlightPrev;

            batch.InFlightPrev = null;
            batch.InFlightNext = null;

            return true;
        }
        finally
        {
            if (lockTaken) _inFlightBatchLock.Exit();
        }
    }

    /// <summary>
    /// Snapshots the in-flight batch list into the provided list for iteration outside the lock.
    /// Used by sweep and disposal to avoid holding the lock during expensive per-batch operations.
    /// </summary>
    private void InFlightBatchListSnapshot(List<ReadyBatch> target)
    {
        var lockTaken = false;
        try
        {
            _inFlightBatchLock.Enter(ref lockTaken);

            var current = _inFlightBatchHead;
            while (current is not null)
            {
                target.Add(current);
                current = current.InFlightNext;
            }
        }
        finally
        {
            if (lockTaken) _inFlightBatchLock.Exit();
        }
    }

    private void InFlightBatchDiagnosticSnapshot(List<DiagnosticBatchReference> target)
    {
        var lockTaken = false;
        try
        {
            _inFlightBatchLock.Enter(ref lockTaken);

            var current = _inFlightBatchHead;
            while (current is not null)
            {
                target.Add(new DiagnosticBatchReference(current, current.Generation, current.TopicPartition));
                current = current.InFlightNext;
            }
        }
        finally
        {
            if (lockTaken) _inFlightBatchLock.Exit();
        }
    }

    private readonly struct DiagnosticBatchReference(ReadyBatch batch, int generation, TopicPartition topicPartition)
    {
        public ReadyBatch Batch { get; } = batch;
        public int Generation { get; } = generation;
        public TopicPartition TopicPartition { get; } = topicPartition;
    }

    internal ProducerDeliveryDiagnosticsSnapshot GetDeliveryDiagnosticsSnapshot()
    {
        if (!_options.EnableDeliveryDiagnostics)
        {
            var measurementStartedAt = Volatile.Read(ref _produceRequestDiagnosticsStartedAt);
            var requestCount = Interlocked.Read(ref _produceRequestCount);
            var elapsedSeconds = Math.Max(
                0,
                (Stopwatch.GetTimestamp() - measurementStartedAt) / (double)Stopwatch.Frequency);
            return new ProducerDeliveryDiagnosticsSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                DiagnosticsEnabled = false,
                ProduceRequestCount = requestCount,
                ProduceRequestElapsedSeconds = elapsedSeconds,
                ProduceRequestsPerSecond = elapsedSeconds > 0
                    ? requestCount / elapsedSeconds
                    : 0
            };
        }

        var batches = new List<DiagnosticBatchReference>();
        InFlightBatchDiagnosticSnapshot(batches);

        var startedAt = Volatile.Read(ref _produceRequestDiagnosticsStartedAt);
        var produceRequestCount = Interlocked.Read(ref _produceRequestCount);
        var produceRequestElapsedSeconds = Math.Max(
            0,
            (Stopwatch.GetTimestamp() - startedAt) / (double)Stopwatch.Frequency);
        var snapshot = new ProducerDeliveryDiagnosticsSnapshot
        {
            DiagnosticsEnabled = true,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            InFlightBatchCount = Volatile.Read(ref _inFlightBatchCount),
            ProduceRequestCount = produceRequestCount,
            ProduceRequestElapsedSeconds = produceRequestElapsedSeconds,
            ProduceRequestsPerSecond = produceRequestElapsedSeconds > 0
                ? produceRequestCount / produceRequestElapsedSeconds
                : 0,
            BatchArenaPoolMisses = BatchArena.Misses,
            BatchArenaPoolDrops = BatchArena.Drops,
            BatchArenaPoolCapacity = BatchArena.PoolCapacity
        };

        var widthCounts = _coalesceWidthCounts!;
        for (var width = 1; width <= MaxTrackedCoalesceWidth; width++)
        {
            var requestCount = Interlocked.Read(ref widthCounts[width]);
            if (requestCount > 0)
            {
                snapshot.CoalesceWidthHistogram.Add(new ProducerCoalesceWidthDiagnostic
                {
                    MinimumWidth = width,
                    MaximumWidth = width,
                    RequestCount = requestCount
                });
            }
        }

        var overflowCount = Interlocked.Read(ref widthCounts[MaxTrackedCoalesceWidth + 1]);
        if (overflowCount > 0)
        {
            snapshot.CoalesceWidthHistogram.Add(new ProducerCoalesceWidthDiagnostic
            {
                MinimumWidth = MaxTrackedCoalesceWidth + 1,
                MaximumWidth = null,
                RequestCount = overflowCount
            });
        }

        foreach (var (brokerId, counters) in _brokerProduceRequestCounters!.OrderBy(entry => entry.Key))
        {
            var requestCount = Interlocked.Read(ref counters.RequestCount);
            if (requestCount == 0)
                continue;

            var requestBytes = Interlocked.Read(ref counters.RequestBytes);
            snapshot.BrokerProduceRequests.Add(new ProducerBrokerRequestDiagnostic
            {
                BrokerId = brokerId,
                RequestCount = requestCount,
                RequestsPerSecond = produceRequestElapsedSeconds > 0
                    ? requestCount / produceRequestElapsedSeconds
                    : 0,
                AverageRequestBytes = requestBytes / (double)requestCount
            });
        }

        var capturedAtUtc = snapshot.CapturedAtUtc;
        foreach (var (brokerId, budget) in _brokerUnackedBudgets)
            snapshot.BrokerBudgets.Add(CreateBrokerBudgetDiagnostic(brokerId, budget, capturedAtUtc, includeHistograms: true));

        lock (_deliveryDiagnosticsLock)
        {
            snapshot.ConnectionScaleEvents.AddRange(_connectionScaleEvents);
            snapshot.BrokerBudgetSamples.AddRange(_brokerBudgetSamples);
        }

        foreach (var batch in batches)
            snapshot.Batches.Add(batch.Batch.CreateDeliveryDiagnostic(batch.Generation, batch.TopicPartition));

        return snapshot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RecordProduceRequest(int brokerId, int coalesceWidth, int requestBytes)
    {
        Interlocked.Increment(ref _produceRequestCount);
        var widthCounts = _coalesceWidthCounts;
        if (widthCounts is null)
            return;

        Interlocked.Increment(ref widthCounts[Math.Min(coalesceWidth, MaxTrackedCoalesceWidth + 1)]);
        var counters = _brokerProduceRequestCounters!.GetOrAdd(
            brokerId,
            static _ => new ProducerRequestDiagnosticCounters());
        Interlocked.Increment(ref counters.RequestCount);
        Interlocked.Add(ref counters.RequestBytes, requestBytes);
    }

    internal void ResetProduceRequestDiagnostics()
    {
        Interlocked.Exchange(ref _produceRequestCount, 0);
        Volatile.Write(ref _produceRequestDiagnosticsStartedAt, Stopwatch.GetTimestamp());
        var widthCounts = _coalesceWidthCounts;
        if (widthCounts is null)
            return;

        for (var width = 1; width < widthCounts.Length; width++)
            Interlocked.Exchange(ref widthCounts[width], 0);
        foreach (var counters in _brokerProduceRequestCounters!.Values)
        {
            Interlocked.Exchange(ref counters.RequestCount, 0);
            Interlocked.Exchange(ref counters.RequestBytes, 0);
        }
    }

    private sealed class ProducerRequestDiagnosticCounters
    {
        public long RequestCount;
        public long RequestBytes;
    }

    internal void RecordConnectionScaleEvent(
        int brokerId,
        int oldConnectionCount,
        int newConnectionCount,
        double bufferUtilization,
        long bufferPressureDelta,
        long sendLoopPressureDelta,
        bool partitionLimited = false,
        long observationCount = 1,
        long observedDurationMs = 0)
    {
        if (!_options.EnableDeliveryDiagnostics)
            return;

        lock (_deliveryDiagnosticsLock)
        {
            if (_connectionScaleEvents.Count >= MaxConnectionScaleDiagnosticEvents)
                return;

            _connectionScaleEvents.Add(new ProducerConnectionScaleDiagnostic
            {
                OccurredAtUtc = DateTimeOffset.UtcNow,
                BrokerId = brokerId,
                OldConnectionCount = oldConnectionCount,
                NewConnectionCount = newConnectionCount,
                BufferUtilization = bufferUtilization,
                BufferPressureDelta = bufferPressureDelta,
                SendLoopPressureDelta = sendLoopPressureDelta,
                PartitionLimited = partitionLimited,
                ObservationCount = observationCount,
                ObservedDurationMs = observedDurationMs
            });
        }
    }

    private void MaybeRecordBrokerBudgetDiagnosticSample()
    {
        if (!_options.EnableDeliveryDiagnostics || !_unackedBudgetEnabled)
            return;

        var now = Dekaf.MonotonicClock.GetMilliseconds();
        var next = Volatile.Read(ref _nextBrokerBudgetDiagnosticTimestampMs);
        if (now < next
            || Interlocked.CompareExchange(
                ref _nextBrokerBudgetDiagnosticTimestampMs,
                now + BrokerBudgetDiagnosticIntervalMs,
                next) != next)
            return;

        RecordBrokerBudgetDiagnosticSample();
    }

    internal void RecordBrokerBudgetDiagnosticSample()
    {
        if (!_options.EnableDeliveryDiagnostics || !_unackedBudgetEnabled)
            return;

        var capturedAtUtc = DateTimeOffset.UtcNow;
        lock (_deliveryDiagnosticsLock)
        {
            foreach (var (brokerId, budget) in _brokerUnackedBudgets)
            {
                if (_brokerBudgetSamples.Count >= MaxBrokerBudgetDiagnosticSamples)
                    _brokerBudgetSamples.RemoveAt(0);

                _brokerBudgetSamples.Add(CreateBrokerBudgetDiagnostic(brokerId, budget, capturedAtUtc, includeHistograms: false));
            }
        }
    }

    private static ProducerBrokerBudgetDiagnostic CreateBrokerBudgetDiagnostic(
        int brokerId,
        BrokerUnackedByteBudget budget,
        DateTimeOffset capturedAtUtc,
        bool includeHistograms) => new()
    {
        CapturedAtUtc = capturedAtUtc,
        BrokerId = brokerId,
        BudgetBytes = budget.BudgetBytes,
        UnackedBytes = budget.UnackedBytes,
        MinRttMicros = budget.MinimumRttMicros,
        MaxRateBytesPerSec = budget.MaxRateBytesPerSecond,
        AdmissionBlockCount = budget.AdmissionBlockEvents,
        CapacityProbeSuccessCount = budget.CapacityProbeSuccessCount,
        CapacityProbeFailureCount = budget.CapacityProbeFailureCount,
        DeliveryLatencyEwmaMicros = budget.DeliveryLatencyEwmaMicros,
        LatencyBudgetScale = budget.LatencyBudgetScale,
        RequestSizeLog2Histogram = includeHistograms ? budget.CopyRequestSizeHistogram() : null,
        RequestRttMicrosLog2Histogram = includeHistograms ? budget.CopyRequestRttMicrosHistogram() : null
    };

    /// <summary>
    /// Sweeps the in-flight batch list for batches whose delivery timeout has expired
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
        if (Volatile.Read(ref _inFlightBatchCount) <= 0)
            return 0;

        var now = Stopwatch.GetTimestamp();
        // Use 3x delivery timeout for the sweep. BrokerSender.ProcessCompletedResponses
        // handles normal delivery timeout (1x) for batches still tracked in _pendingResponses.
        // The sweep only catches truly orphaned batches — those not in any data structure.
        var deliveryTimeoutTicks = _options.DeliveryTimeoutTicks * 3;
        var configured = TimeSpan.FromMilliseconds(_options.DeliveryTimeoutMs * 3);
        var expiredCount = 0;

        // Snapshot the list to avoid holding the lock during per-batch operations.
        // One List<ReadyBatch> allocation per sweep is acceptable — sweeps run at linger
        // interval (1-100ms), not per-message.
        var snapshot = new List<ReadyBatch>();
        InFlightBatchListSnapshot(snapshot);

        foreach (var batch in snapshot)
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
    /// Fails all remaining in-flight batches tracked in the in-flight batch list.
    /// Used as a defense-in-depth sweep after BrokerSender disposal to catch batches
    /// whose references were lost from BrokerSender data structures.
    /// Safe to call multiple times — idempotent due to InFlightLinked guard.
    /// </summary>
    /// <param name="returnToPool">
    /// When true, batches are returned to the pool after failing (calls Reset which nulls fields).
    /// Must only be true when all BrokerSenders are stopped — otherwise the send loop may still
    /// hold references to these batches and access their fields, causing NullReferenceException.
    /// When false, only completion sources are resolved and memory is released; pool return is
    /// deferred to a later sweep (after BrokerSenders are disposed).
    /// </param>
    internal void ForceFailAllInFlightBatches(bool returnToPool = true)
    {
        if (Volatile.Read(ref _inFlightBatchCount) <= 0)
            return;

        // Snapshot the list to iterate outside the lock
        var snapshot = new List<ReadyBatch>();
        InFlightBatchListSnapshot(snapshot);

        if (snapshot.Count == 0)
            return;

        LogOrphanedBatchesDuringDisposal(snapshot.Count);
        var disposedException = new ObjectDisposedException(nameof(RecordAccumulator));

        foreach (var orphanedBatch in snapshot)
        {
            // OnBatchExitsPipeline uses InFlightBatchListRemove as atomic guard and decrements counter.
            if (!OnBatchExitsPipeline(orphanedBatch))
                continue; // Another thread already handled this batch

            FailAndRelease(orphanedBatch, disposedException);

            if (returnToPool)
            {
                // Only safe when BrokerSenders are stopped — their send loops no longer
                // reference these batches, so Reset() won't cause use-after-free.
                try { ReturnReadyBatch(orphanedBatch); }
                catch (Exception returnEx) { LogBatchCleanupStepFailed(returnEx); }
            }
        }
    }

    /// <summary>
    /// Purges queued and/or in-flight batches with a caller-provided exception.
    /// </summary>
    /// <returns>
    /// Diagnostic count of purged work. Queued purge counts pending append operations plus
    /// current/ready batches; in-flight purge counts batches.
    /// </returns>
    internal int Purge(PurgeOptions options, Exception exception, Action<ReadyBatch>? onPurgingBatch = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var purgedCount = 0;

        if ((options & PurgeOptions.Queue) != 0)
            purgedCount += PurgeQueuedBatches(exception, onPurgingBatch);

        if ((options & PurgeOptions.InFlight) != 0)
            purgedCount += PurgeInFlightBatches(exception, onPurgingBatch);

        if (purgedCount > 0)
            SignalWakeup();

        return purgedCount;
    }

    private int PurgeQueuedBatches(Exception exception, Action<ReadyBatch>? onPurgingBatch)
    {
        var purgedCount = FailPendingAppendsForPurge(exception);
        List<PartitionBatch>? currentBatches = null;
        List<ReadyBatch>? readyBatches = null;
        var appendWaitObserved = false;

        foreach (var kvp in _partitionDeques)
        {
            var pd = kvp.Value;
            var spinner = new SpinWait();
            while (true)
            {
                var waitForAppend = false;
                {
                    using var guard = new SpinLockGuard(ref pd.Lock);

                    if (pd.AppendInProgress)
                    {
                        waitForAppend = true;
                        if (!appendWaitObserved)
                        {
                            appendWaitObserved = true;
                            PurgeAppendWaitObservedForTest?.Invoke();
                        }
                    }
                    else
                    {
                        if (pd.CurrentBatch is { } currentBatch)
                        {
                            pd.CurrentBatch = null;
                            MarkLingerPartitionDequeued(pd);
                            Interlocked.Decrement(ref _unsealedBatchCount);

                            currentBatches ??= new List<PartitionBatch>();
                            currentBatches.Add(currentBatch);
                        }

                        while (pd.Count > 0)
                        {
                            readyBatches ??= new List<ReadyBatch>();
                            readyBatches.Add(pd.PollFirst()!);
                        }
                    }
                }

                if (!waitForAppend)
                    break;

                spinner.SpinOnce();
            }
        }

        if (currentBatches is not null)
        {
            foreach (var currentBatch in currentBatches)
            {
                if (!TryCompleteDetachedBatchForCleanup(
                    currentBatch,
                    out var readyBatch,
                    out var bytesToRelease))
                {
                    purgedCount++;
                    continue;
                }

                if (bytesToRelease > 0)
                    ReleaseMemory(bytesToRelease);
                if (readyBatch is not null)
                {
                    readyBatches ??= new List<ReadyBatch>();
                    readyBatches.Add(readyBatch);
                }
            }
        }

        if (readyBatches is null)
            return purgedCount;

        foreach (var batch in readyBatches)
        {
            FailBatchAndCleanup(
                batch,
                exception,
                onPurgingBatch,
                removeFromPipeline: true,
                returnToPool: true);
            purgedCount++;
        }

        return purgedCount;
    }

    private int FailPendingAppendsForPurge(Exception exception)
    {
        var spinWait = new SpinWait();
        while (Interlocked.CompareExchange(ref _draining, 1, 0) != 0)
            spinWait.SpinOnce();

        var failedCount = 0;
        try
        {
            while (_pendingAppends.TryDequeue(out var op))
            {
                if (op.TryFail(exception))
                    failedCount++;
            }
        }
        finally
        {
            Volatile.Write(ref _draining, 0);
        }

        return failedCount;
    }

    private int PurgeInFlightBatches(Exception exception, Action<ReadyBatch>? onPurgingBatch)
    {
        if (Volatile.Read(ref _inFlightBatchCount) <= 0)
            return 0;

        var snapshot = new List<ReadyBatch>();
        InFlightBatchListSnapshot(snapshot);

        var purgedCount = 0;
        foreach (var batch in snapshot)
        {
            if (IsBatchStillQueued(batch))
                continue;

            if (!OnBatchExitsPipeline(batch))
                continue;

            FailBatchAndCleanup(
                batch,
                exception,
                onPurgingBatch,
                removeFromPipeline: false,
                returnToPool: false);
            purgedCount++;
        }

        return purgedCount;
    }

    private bool IsBatchStillQueued(ReadyBatch batch)
    {
        if (!_partitionDeques.TryGetValue(batch.TopicPartition, out var pd))
            return false;

        using var guard = new SpinLockGuard(ref pd.Lock);
        return pd.Contains(batch);
    }

    private void FailBatchAndCleanup(
        ReadyBatch batch,
        Exception exception,
        Action<ReadyBatch>? beforeFailure,
        bool removeFromPipeline,
        bool returnToPool)
    {
        try { beforeFailure?.Invoke(batch); }
        catch (Exception cleanupEx) { LogBatchCleanupStepFailed(cleanupEx); }
        try { batch.Fail(exception); }
        catch (Exception failEx) { LogBatchCleanupStepFailed(failEx); }
        try
        {
            ReleaseBatchMemory(batch);
        }
        catch (Exception memEx) { LogBatchCleanupStepFailed(memEx); }
        if (removeFromPipeline)
        {
            try { OnBatchExitsPipeline(batch); }
            catch (Exception exitEx) { LogBatchCleanupStepFailed(exitEx); }
        }
        if (returnToPool)
        {
            try { ReturnReadyBatch(batch); }
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
            ReleaseBatchMemory(batch);
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
            return default;
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
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _closed, 1) != 0)
            return;

        LogCloseStarted(_partitionDeques.Count);

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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var inFlightBatches = Volatile.Read(ref _inFlightBatchCount);
        LogDisposeStarted(_partitionDeques.Count, inFlightBatches);
        // Note: BatchArena.Misses is process-scoped (shared across all producers),
        // while _batchPool and _readyBatchPool misses are per-producer-instance.
        LogPoolMisses(_batchPool.Misses, _readyBatchPool.Misses, BatchArena.Misses);

        // FIRST: Try graceful shutdown (send remaining batches) with timeout
        // This matches Confluent.Kafka behavior and prevents data loss
        // We do this BEFORE failing batches to give them a chance to be sent
        if (Volatile.Read(ref _closed) == 0)
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

        // Wake async waiters blocked in ReserveMemoryAsync so they recheck _disposed.
        SignalBufferSpaceAvailable();

        // Acquire the drain lock to prevent concurrent DrainPendingAppends from
        // dequeuing ops while we're clearing the queue (avoids TryPeek/TryDequeue mismatch).
        var spinWait = new SpinWait();
        while (Interlocked.CompareExchange(ref _draining, 1, 0) != 0)
            spinWait.SpinOnce();

        while (_pendingAppends.TryDequeue(out var op))
        {
            op.TryFail(new ObjectDisposedException(nameof(RecordAccumulator)));
        }

        Volatile.Write(ref _draining, 0);

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
        // Workers blocked in ReserveMemoryAsync will be woken by the semaphore
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
                PooledCompletionSource.TrySetException(workItem.Completion, disposedException);
            }
        }

        // Fail unsealed current batches and drain sealed batches from partition deques
        foreach (var kvp in _partitionDeques)
        {
            var pd = kvp.Value;
            var appendWaitStartTicks = Stopwatch.GetTimestamp();
            var spinner = new SpinWait();

            while (true)
            {
                var waitForAppend = false;
                var appendWaitTimedOut = false;

                {
                    using var guard = new SpinLockGuard(ref pd.Lock);

                    if (pd.AppendInProgress)
                    {
                        appendWaitTimedOut = DisposeAppendInProgressWaitTimedOut(appendWaitStartTicks);
                        waitForAppend = !appendWaitTimedOut;
                    }

                    if (!waitForAppend)
                    {
                        if (appendWaitTimedOut)
                        {
                            DrainReadyBatchesForDisposeUnderLock(pd, disposedException);
                        }
                        else
                        {
                            // Fail current unsealed batch
                            if (pd.CurrentBatch is { } current)
                            {
                                pd.CurrentBatch = null;
                                MarkLingerPartitionDequeued(pd);
                                Interlocked.Decrement(ref _unsealedBatchCount);

                                if (TryCompleteDetachedBatchForCleanup(
                                    current,
                                    out var readyBatch,
                                    out var bytesToRelease))
                                {
                                    if (readyBatch is not null)
                                    {
                                        readyBatch.Fail(disposedException);
                                        ReleaseBatchMemory(readyBatch);
                                    }
                                    if (bytesToRelease > 0)
                                        ReleaseMemory(bytesToRelease);
                                }
                            }

                            DrainReadyBatchesForDisposeUnderLock(pd, disposedException);
                        }
                    }
                }

                if (waitForAppend)
                {
                    spinner.SpinOnce();
                    continue;
                }

                if (appendWaitTimedOut)
                    LogAppendInProgressDisposalTimeout(kvp.Key.Topic, kvp.Key.Partition);

                break;
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
                DrainReadyBatchesForDisposeUnderLock(pd, disposedException);
            }
        }

        // Signal wakeup so the sender loop can exit if still waiting
        SignalWakeup();
        _lingerWakeupSignal.Signal();

        // Sweep for orphaned batches whose references were lost from BrokerSender data structures.
        // This is the last line of defense: if a batch was tracked via OnBatchEntersPipeline but
        // never cleaned up via OnBatchExitsPipeline (due to a dropped reference), fail it here.
        ForceFailAllInFlightBatches();

        // Clear the batch pool
        _batchPool.Clear();

        // Dispose resources to prevent leaks
        _wakeupSignal?.Dispose();
        _lingerWakeupSignal.Dispose();
        _disposalCts?.Dispose();
        // _bufferSpaceSignal is a TaskCompletionSource — no Dispose needed.
        // Signal any remaining waiters so they can observe disposal.
        _bufferSpaceSignal.TrySetResult();
        _flushLingerLock.Dispose();
        // _flushTcs doesn't need disposal - it's a TaskCompletionSource
    }

    private static bool DisposeAppendInProgressWaitTimedOut(long startTicks)
        => Stopwatch.GetTimestamp() - startTicks >= DisposeAppendInProgressWaitTicks;

    private void DrainReadyBatchesForDisposeUnderLock(PartitionDeque pd, Exception disposedException)
    {
        while (pd.Count > 0)
        {
            var batch = pd.PollFirst()!;
            batch.Fail(disposedException);
            ReleaseBatchMemory(batch);
            OnBatchExitsPipeline(batch);
        }
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timed out waiting for in-progress append during accumulator disposal for {Topic}-{Partition}; skipping current batch cleanup")]
    private partial void LogAppendInProgressDisposalTimeout(string topic, int partition);

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
    public PartitionBatch Rent(TopicPartition topicPartition, int partitionCount)
    {
        var batch = Rent();
        batch.Reset(topicPartition, partitionCount);
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
    private int _partitionCount;
    private ProducerOptions _options;
    private readonly int _initialRecordCapacity;
    private ReadyBatchPool? _readyBatchPool; // Pool for renting ReadyBatch objects
    private BatchArrayReuseQueue? _arrayReuseQueue; // Reuse queue for working arrays

    // Arena holding the encoded record bytes - all records in one contiguous buffer
    private BatchArena? _arena;

    private int _recordCount;

    // Zero-allocation array management: use pooled arrays instead of List<T>
    private PooledValueTaskSource<RecordMetadata>[] _completionSources;
    private int _completionSourceCount;

    // Callbacks for Send(message, callback) - stored directly in batch for inline invocation
    private Action<RecordMetadata, Exception?>?[]? _callbacks;
    private int _callbackCount;

    private long _baseTimestamp;
    private long _maxTimestamp;
    private int _encodedRecordsStart;
    private int _encodedRecordsLength;
    private int _estimatedSize;
    private int _reservedSize;
    private int _effectiveBatchSizeLimit;
    // Note: _offsetDelta removed - it always equals _recordCount at assignment time
    private long _createdStopwatchTimestamp;
    private int _isCompleted; // 0 = not completed, 1 = completed (Interlocked guard for idempotent Complete)
    private ReadyBatch? _completedBatch; // Cached result to ensure Complete() is idempotent

    // Transaction support: set by RecordAccumulator when batch is rented
    private long _producerId = -1;
    private short _producerEpoch = -1;
    private bool _isTransactional;

    /// <summary>
    /// Returns the effective arena capacity: BatchSize + 12.5% margin, or an explicit
    /// ArenaCapacity. Records encode directly into the arena, so explicit values below
    /// the BatchSize-derived minimum are rejected during producer construction.
    /// </summary>
    private static int GetEffectiveArenaCapacity(ProducerOptions options) =>
        ProducerOptions.GetEffectiveArenaCapacity(options.BatchSize, options.ArenaCapacity);

    public PartitionBatch(TopicPartition topicPartition, ProducerOptions options)
    {
        _topicPartition = topicPartition;
        _options = options;
        _effectiveBatchSizeLimit = GetEffectiveBatchSizeLimit(topicPartition, options);
        _createdStopwatchTimestamp = Stopwatch.GetTimestamp();

        _initialRecordCapacity = options.InitialBatchRecordCapacity > 0
            ? Math.Clamp(options.InitialBatchRecordCapacity, 16, 16384)
            : ComputeInitialRecordCapacity(options.BatchSize);

        // Create arena for append-time record encoding
        _arena = new BatchArena(GetEffectiveArenaCapacity(options));
        _recordCount = 0;

        // Rent arrays from pool - eliminates List allocations
        _completionSources = ProducerContainerPools.CompletionSources.Rent(_initialRecordCapacity);
        _completionSourceCount = 0;
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
    internal void SetTransactionState(long producerId, short producerEpoch, bool isTransactional)
    {
        _producerId = producerId;
        _producerEpoch = producerEpoch;
        _isTransactional = isTransactional;
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
    internal void Reset(TopicPartition topicPartition, int partitionCount)
    {
        _topicPartition = topicPartition;
        _partitionCount = partitionCount;
        _effectiveBatchSizeLimit = GetEffectiveBatchSizeLimit(topicPartition, _options);
        _createdStopwatchTimestamp = Stopwatch.GetTimestamp();
        _recordCount = 0;
        _completionSourceCount = 0;
        _callbackCount = 0;
        _baseTimestamp = 0;
        _maxTimestamp = 0;
        _encodedRecordsStart = 0;
        _encodedRecordsLength = 0;
        _estimatedSize = 0;
        _reservedSize = 0;
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

        // The completion sources array was transferred to ReadyBatch by Complete() and is now null.
        // Try to reclaim one from the reuse queue first (returned by ReadyBatch.Cleanup()),
        // avoiding an ArrayPool Rent operation per batch cycle.
        if (_arrayReuseQueue is not null && _arrayReuseQueue.TryDequeue(out var reusable))
        {
            _completionSources = reusable;
        }
        else
        {
            // Fallback: rent a fresh array from the dedicated pool (not ArrayPool<T>.Shared)
            // to prevent TLS accumulation from cross-thread rent/return patterns
            _completionSources = ProducerContainerPools.CompletionSources.Rent(_initialRecordCapacity);
        }

        // Reset counters and state for reuse.
        // IMPORTANT: Do NOT reset _isCompleted here!
        // It must only be reset in Reset() when the batch is actually rented.
        // If we reset _isCompleted here, a stale reference from another thread could
        // successfully append to this pooled batch (since _isCompleted would be 0),
        // and those messages would be lost when Reset() is later called.
        _recordCount = 0;
        _completionSourceCount = 0;
        _baseTimestamp = 0;
        _maxTimestamp = 0;
        _encodedRecordsStart = 0;
        _encodedRecordsLength = 0;
        _estimatedSize = 0;
        _reservedSize = 0;
        // _isCompleted stays at 1 - batch is "completed" while in pool
        _completedBatch = null;
    }

    /// <summary>
    /// Gets the batch's arena for direct serialization.
    /// Returns null if arena is not available (batch completed or arena full).
    /// </summary>
    public BatchArena? Arena => Volatile.Read(ref _isCompleted) == 0 ? _arena : null;

    public TopicPartition TopicPartition => _topicPartition;
    public int PartitionCount => _partitionCount;
    public int RecordCount => _recordCount;
    public int EstimatedSize => _estimatedSize;
    public int ReservedSize => _reservedSize;
    public int OverestimatedBytes => Math.Max(0, _reservedSize - _estimatedSize);
    public int CompletionSourcesCount => _completionSourceCount;
    public bool IsExactlyAtSizeLimit =>
        (long)RecordBatch.TotalBatchHeaderSize + _encodedRecordsLength == EffectiveBatchSizeLimit;

    internal int EffectiveBatchSizeLimit => _effectiveBatchSizeLimit;

    private static int GetEffectiveBatchSizeLimit(
        TopicPartition topicPartition,
        ProducerOptions options)
    {
        var maxRequestSize = options.MaxRequestSize > 0
            ? options.MaxRequestSize
            : ProduceRequestSizeCalculator.DefaultMaxRequestSize;
        var maxEncodedBatchSize = ProduceRequestSizeCalculator.GetMaxEncodedBatchSize(
            maxRequestSize,
            options.TransactionalId,
            topicPartition.Topic ?? string.Empty);
        return Math.Min(options.BatchSize, maxEncodedBatchSize);
    }

    internal readonly record struct ReservedRecordAppend(
        BatchArena Arena,
        int RecordIndex,
        long Timestamp,
        long BaseTimestamp,
        long TimestampDelta,
        int BodySize,
        int EncodedRecordSize,
        int EncodedOffset);

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
        int estimatedSize)
    {
        var result = TryAppendEncoded(
            timestamp,
            key.Span,
            key.IsNull,
            key.Length,
            value.Span,
            value.IsNull,
            value.Length,
            headers,
            headerCount,
            completionSource,
            callback,
            estimatedSize);

        if (result.Success)
        {
            key.Return();
            value.Return();
            RecordAccumulator.ReturnPooledHeaders(headers);
        }

        return result;
    }

    /// <summary>
    /// Appends a record from raw span data, encoding directly into the batch arena.
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
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int estimatedSize)
    {
        var result = TryReserveAppendFromSpans(
            timestamp,
            keyData,
            keyIsNull,
            valueData,
            valueIsNull,
            headers,
            headerCount,
            completionSource,
            callback,
            out var reservedAppend);

        if (result.Success)
        {
            try
            {
                EncodeReservedAppendFromSpans(
                    reservedAppend,
                    keyData,
                    keyIsNull,
                    valueData,
                    valueIsNull,
                    headers,
                    headerCount);
            }
            catch
            {
                CancelReservedAppend(reservedAppend);
                throw;
            }

            CommitReservedAppendFromSpans(reservedAppend, completionSource, callback, headers, estimatedSize);
        }

        return result;
    }

    internal RecordAppendResult TryReserveAppendFromSpans(
        long timestamp,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        out ReservedRecordAppend reservedAppend)
    {
        var keyLength = keyIsNull ? 0 : keyData.Length;
        var valueLength = valueIsNull ? 0 : valueData.Length;

        return TryReserveAppend(
            timestamp,
            keyIsNull,
            keyLength,
            valueIsNull,
            valueLength,
            headers,
            headerCount,
            completionSource is not null,
            callback is not null,
            out reservedAppend);
    }

    private RecordAppendResult TryAppendEncoded(
        long timestamp,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        int keyLength,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        int valueLength,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int estimatedSize)
    {
        var result = TryReserveAppend(
            timestamp,
            keyIsNull,
            keyLength,
            valueIsNull,
            valueLength,
            headers,
            headerCount,
            completionSource is not null,
            callback is not null,
            out var reservedAppend);

        if (!result.Success)
            return result;

        try
        {
            EncodeReservedAppend(
                reservedAppend,
                keyData,
                keyIsNull,
                valueData,
                valueIsNull,
                headers,
                headerCount);
        }
        catch
        {
            CancelReservedAppend(reservedAppend);
            throw;
        }

        CommitReservedAppend(reservedAppend, completionSource, callback, estimatedSize);
        return result;
    }

    private RecordAppendResult TryReserveAppend(
        long timestamp,
        bool keyIsNull,
        int keyLength,
        bool valueIsNull,
        int valueLength,
        Header[]? headers,
        int headerCount,
        bool hasCompletionSource,
        bool hasCallback,
        out ReservedRecordAppend reservedAppend)
    {
        // Check if batch was completed
        if (Volatile.Read(ref _isCompleted) != 0)
        {
            reservedAppend = default;
            return new RecordAppendResult(false);
        }

        // Defensive check: if the array is null, batch is in inconsistent state (being pooled)
        if (_completionSources is null)
        {
            reservedAppend = default;
            return new RecordAppendResult(false);
        }

        var recordIndex = _recordCount;
        var baseTimestamp = recordIndex == 0 ? timestamp : _baseTimestamp;
        var timestampDelta = timestamp - baseTimestamp;
        var bodySize = Record.ComputeBodySize(
            timestampDelta,
            recordIndex,
            keyIsNull,
            keyLength,
            valueIsNull,
            valueLength,
            headers,
            headerCount);
        var encodedRecordSize = checked(Record.VarIntSize(bodySize) + bodySize);

        // BatchSize and MaxRequestSize are full wire-batch budgets, so records must
        // leave room for the fixed RecordBatch header. A first record may exceed
        // BatchSize via the documented expanded-arena path, but never MaxRequestSize.
        var maxRecordsSize = Math.Max(0, EffectiveBatchSizeLimit - RecordBatch.TotalBatchHeaderSize);
        if ((long)_estimatedSize + encodedRecordSize > maxRecordsSize && recordIndex > 0)
        {
            reservedAppend = default;
            return new RecordAppendResult(false);
        }

        if (recordIndex == 0)
        {
            EnsureArenaCanFitFirstRecord(encodedRecordSize);
        }

        // Grow arrays if needed (rare - only happens if batch fills beyond initial capacity)
        if (hasCompletionSource && _completionSourceCount >= _completionSources.Length)
        {
            GrowArray(ref _completionSources, ref _completionSourceCount, ProducerContainerPools.CompletionSources);
        }
        if (hasCallback)
        {
            _callbacks ??= ProducerContainerPools.Callbacks.Rent(_initialRecordCapacity);
            if (_callbackCount >= _callbacks.Length)
            {
                GrowArray(ref _callbacks!, ref _callbackCount, ProducerContainerPools.Callbacks);
            }
        }

        if (_arena is null || !_arena.TryAllocate(encodedRecordSize, out _, out var encodedOffset))
        {
            reservedAppend = default;
            return new RecordAppendResult(false);
        }

        reservedAppend = new ReservedRecordAppend(
            _arena,
            recordIndex,
            timestamp,
            baseTimestamp,
            timestampDelta,
            bodySize,
            encodedRecordSize,
            encodedOffset);

        var firstCompletionSourceInBatch = hasCompletionSource && _completionSourceCount == 0;
        return new RecordAppendResult(
            Success: true,
            ActualSizeAdded: encodedRecordSize,
            FirstCompletionSourceInBatch: firstCompletionSourceInBatch);
    }

    internal static void EncodeReservedAppendFromSpans(
        in ReservedRecordAppend reservedAppend,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        Header[]? headers,
        int headerCount)
    {
        EncodeReservedAppend(reservedAppend, keyData, keyIsNull, valueData, valueIsNull, headers, headerCount);
    }

    private static void EncodeReservedAppend(
        in ReservedRecordAppend reservedAppend,
        ReadOnlySpan<byte> keyData,
        bool keyIsNull,
        ReadOnlySpan<byte> valueData,
        bool valueIsNull,
        Header[]? headers,
        int headerCount)
    {
        var encodedRecord = reservedAppend.Arena.Buffer.AsSpan(
            reservedAppend.EncodedOffset,
            reservedAppend.EncodedRecordSize);

        Record.Encode(
            encodedRecord,
            reservedAppend.BodySize,
            reservedAppend.TimestampDelta,
            reservedAppend.RecordIndex,
            keyData,
            keyIsNull,
            valueData,
            valueIsNull,
            headers,
            headerCount);
    }

    internal void CommitReservedAppendFromSpans(
        in ReservedRecordAppend reservedAppend,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        Header[]? headers,
        int estimatedSize)
    {
        CommitReservedAppend(reservedAppend, completionSource, callback, estimatedSize);
        RecordAccumulator.ReturnPooledHeaders(headers);
    }

    private void CommitReservedAppend(
        in ReservedRecordAppend reservedAppend,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int estimatedSize)
    {
        var recordIndex = reservedAppend.RecordIndex;

        if (recordIndex == 0)
        {
            _baseTimestamp = reservedAppend.BaseTimestamp;
            _encodedRecordsStart = reservedAppend.EncodedOffset;
        }

        Debug.Assert(reservedAppend.EncodedOffset == _encodedRecordsStart + _encodedRecordsLength);
        _encodedRecordsLength += reservedAppend.EncodedRecordSize;
        _maxTimestamp = recordIndex == 0 ? reservedAppend.Timestamp : Math.Max(_maxTimestamp, reservedAppend.Timestamp);

        if (completionSource is not null)
        {
            _completionSources[_completionSourceCount++] = completionSource;
            ProducerDebugCounters.RecordCompletionSourceStoredInBatch();
        }

        if (callback is not null)
        {
            _callbacks![_callbackCount++] = callback;
        }

        _recordCount = recordIndex + 1;
        _estimatedSize += reservedAppend.EncodedRecordSize;
        _reservedSize += estimatedSize;
    }

    internal static void CancelReservedAppend(in ReservedRecordAppend reservedAppend)
    {
        reservedAppend.Arena.TryRewindLastAllocation(reservedAppend.EncodedOffset, reservedAppend.EncodedRecordSize);
    }

    private void EnsureArenaCanFitFirstRecord(int encodedRecordSize)
    {
        var arena = _arena;
        if (arena is not null && encodedRecordSize <= arena.RemainingCapacity)
        {
            return;
        }

        var normalCapacity = GetEffectiveArenaCapacity(_options);
        var capacity = Math.Max(normalCapacity, encodedRecordSize);
        if (arena is not null)
        {
            BatchArena.ReturnToPool(arena);
        }

        _arena = BatchArena.RentOrCreate(capacity, normalCapacity);
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

        RecordBatch? batch = null;
        ReadyBatch? readyBatch = null;
        try
        {
            var encodedRecords = _arena!.GetMemory(_encodedRecordsStart, _encodedRecordsLength);

            // Rent from pool to eliminate per-batch RecordBatch class allocation.
            // The batch lives through the send pipeline (1-10ms) and would otherwise
            // survive Gen0 collection, contributing to high Gen1/Gen0 promotion rate.
            batch = RecordBatch.RentFromPool();
            batch.BaseOffset = 0;
            batch.BaseTimestamp = _baseTimestamp;
            batch.MaxTimestamp = _maxTimestamp;
            batch.LastOffsetDelta = _recordCount - 1;
            batch.ProducerId = _producerId;
            batch.ProducerEpoch = _producerEpoch;
            batch.BaseSequence = baseSequence;
            batch.Attributes = attributes;
            batch.SetPreEncodedRecords(encodedRecords);
            batch.Records = LazyRecordList.Create(encodedRecords, _recordCount);

            // Rent ReadyBatch from pool or create new if no pool available
            // This eliminates per-batch class allocations at high throughput
            readyBatch = _readyBatchPool?.Rent() ?? new ReadyBatch();

            // Initialize with batch data - ownership of arrays transfers to ReadyBatch
            // PooledValueTaskSource auto-returns to its pool when GetResult() is called
            readyBatch.Initialize(
                _topicPartition,
                batch,
                _completionSources,
                _completionSourceCount,
                _estimatedSize,
                _arena,
                _callbacks,
                _callbackCount,
                _arrayReuseQueue,
                _recordCount,
                _createdStopwatchTimestamp);

            _completedBatch = readyBatch;

            batch = null;
            readyBatch = null;

            // Null out references - ownership transferred to ReadyBatch
            _completionSources = null!;
            _arena = null;
            _callbacks = null;
        }
        catch
        {
            if (readyBatch is not null && _readyBatchPool is not null)
                _readyBatchPool.Return(readyBatch);

            if (batch is not null)
            {
                batch.ReturnPreCompressedBuffer();
                batch.DisposeRecordList();
                batch.ReturnToPool();
            }

            throw;
        }

        return _completedBatch;
    }

    internal int FailCompleteFailure(Exception exception)
    {
        var bytesToRelease = _reservedSize;

        if (_completionSourceCount > 0 && _completionSources is not null)
        {
            for (var i = 0; i < _completionSourceCount; i++)
                PooledCompletionSource.TrySetException(_completionSources[i], exception);
        }

        if (_callbackCount > 0 && _callbacks is not null)
        {
            for (var i = 0; i < _callbackCount; i++)
            {
                var callback = _callbacks[i];
                if (callback is null)
                    continue;

                try { ProducerCallbackContext.Invoke(callback, default, exception); }
                catch { }
                _callbacks[i] = null;
            }
        }

        ReturnBatchArraysToPool();
        _recordCount = 0;
        _completionSourceCount = 0;
        _callbackCount = 0;
        _encodedRecordsStart = 0;
        _encodedRecordsLength = 0;
        _estimatedSize = 0;
        _reservedSize = 0;
        _completedBatch = null;

        return bytesToRelease;
    }

    private void ReturnBatchArraysToPool()
    {
        // Return working arrays to dedicated pools (with null checks since they may have been transferred)
        // clearArray: false for internal tracking arrays - they will be overwritten on next use
        if (_completionSources is not null)
            ProducerContainerPools.CompletionSources.Return(_completionSources, clearArray: false);

        // Return arena buffer if present
        _arena?.Return();

        // Null out references to prevent accidental reuse
        _completionSources = null!;
        _arena = null;

        if (_callbacks is not null)
        {
            ProducerContainerPools.Callbacks.Return(_callbacks, clearArray: true);
            _callbacks = null;
        }
    }

    /// <summary>
    /// Estimates the size of a record in the batch for buffer memory accounting.
    /// This is an upper-bound estimate that includes:
    /// - 36 bytes base overhead (worst-case varints for record length, timestamp delta,
    ///   offset delta, key/value lengths, and header count, plus 1 byte attributes)
    /// - Key and value payload lengths
    /// - Exact header sizes via <see cref="Header.CalculateSize"/>
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
        // Upper bound for record wrapper/body metadata:
        // record length (5) + attributes (1) + timestamp delta (10) + offset delta (5) +
        // key length (5) + value length (5) + header count (5).
        var size = 36;
        size += keyLength;
        size += valueLength;

        if (headers is not null)
        {
            for (var i = 0; i < headerCount; i++)
            {
                size += headers[i].CalculateSize();
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
/// <param name="FirstCompletionSourceInBatch">True when this append added the first awaited completion source to the batch.</param>
public readonly record struct RecordAppendResult(
    bool Success,
    int ActualSizeAdded = 0,
    bool FirstCompletionSourceInBatch = false);

/// <summary>
/// Pool for ReadyBatch objects to eliminate per-batch class allocations.
/// Extends <see cref="ObjectPool{T}"/> for pre-warm support and miss tracking.
/// </summary>
internal sealed class ReadyBatchPool(int maxPoolSize = BatchArena.DefaultPoolSize * 2)
    : ObjectPool<ReadyBatch>(maxPoolSize)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new ReadyBatch Rent()
    {
        var batch = base.Rent();
        ProducerDebugCounters.RecordReadyBatchRented();
        return batch;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new void Return(ReadyBatch item)
    {
        base.Return(item);
        ProducerDebugCounters.RecordReadyBatchReturned();
    }

    protected override ReadyBatch Create() => new();
    protected override void Reset(ReadyBatch item) => item.Reset();
}

/// <summary>
/// Reuse queue for the completion sources array that PartitionBatch rents from ArrayPool.
/// When ReadyBatch finishes cleanup, it pushes the array here instead of returning it to
/// ArrayPool. PartitionBatch.PrepareForPooling() dequeues from here first, falling back
/// to ArrayPool on miss. This eliminates an ArrayPool Rent/Return pair per batch cycle.
/// </summary>
internal sealed class BatchArrayReuseQueue
{
    private readonly LockFreeStack<PooledValueTaskSource<RecordMetadata>[]> _queue;

    public BatchArrayReuseQueue(int maxSize = 128)
    {
        _queue = new LockFreeStack<PooledValueTaskSource<RecordMetadata>[]>(maxSize);
    }

    /// <summary>
    /// Pushes the array for reuse. If the queue is full, the array is returned
    /// to the dedicated ArrayPool instead.
    /// </summary>
    public void EnqueueOrReturn(PooledValueTaskSource<RecordMetadata>[] completionSources)
    {
        if (!_queue.TryPush(completionSources))
        {
            ProducerContainerPools.CompletionSources.Return(completionSources, clearArray: false);
        }
    }

    /// <summary>
    /// Tries to pop a reusable completion sources array.
    /// </summary>
    public bool TryDequeue([NotNullWhen(true)] out PooledValueTaskSource<RecordMetadata>[]? completionSources)
        => _queue.TryPop(out completionSources);
}

/// <summary>
/// A batch ready to be sent. Poolable to eliminate per-batch allocations.
/// Returns pooled arrays to ArrayPool when complete.
/// PooledValueTaskSource instances auto-return to their pool when GetResult() is called.
/// </summary>
internal sealed class ReadyBatch
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
    /// Total number of records sealed into this batch.
    /// </summary>
    public int RecordCount => _recordCount;

    /// <summary>
    /// Uncompressed encoded record bytes reserved against BufferMemory.
    /// </summary>
    public int DataSize { get; private set; }

    /// <summary>
    /// Full encoded record batch size used for MaxRequestSize drain accounting.
    /// </summary>
    public int EncodedSize { get; private set; }

    /// <summary>
    /// Gets a ValueTask that completes when this batch is done (either sent successfully or failed).
    /// The producer's FlushAsync path uses RecordAccumulator's in-flight counter; this task is
    /// retained for diagnostics and direct batch observers.
    /// IMPORTANT: This task never faults - it completes with true (success) or false (failure).
    /// This design eliminates UnobservedTaskException issues for fire-and-forget scenarios.
    /// Per-message exceptions are handled via the completion sources array, not this task.
    /// </summary>
    public ValueTask<bool> DoneTask
    {
        get
        {
            var state = RegisterDoneTaskObserver(out var completedResult);
            if (completedResult is { } succeeded)
                return new ValueTask<bool>(succeeded);

            AfterDoneTaskObserverRegisteredForTest?.Invoke();
            return new ValueTask<bool>(state.Source.Task);
        }
    }

    // Working arrays from accumulator (pooled) - mutable for pooling
    private PooledValueTaskSource<RecordMetadata>[]? _completionSourcesArray;
    private int _completionSourcesCount;

    // Total records sealed into this batch, captured at Initialize because the
    // RecordBatch's record list may already be recycled when Fail needs the count.
    private int _recordCount;

    // Reuse queue for returning working arrays back to PartitionBatch without ArrayPool round-trip
    private BatchArrayReuseQueue? _arrayReuseQueue;
    private BatchArena? _arena; // Arena holding the batch's encoded record bytes

    // Callbacks for Send(message, callback) - inline invocation without ThreadPool
    private Action<RecordMetadata, Exception?>?[]? _callbacks;
    private int _callbackCount;

    // In-flight tracker entry for coordinated retry with multiple in-flight batches per partition.
    // Set by KafkaProducer when registering with PartitionInflightTracker, cleared in Reset().
    internal InflightEntry? InflightEntry { get; set; }

    // Unacked-byte budget this batch's DataSize is currently charged against. Released
    // once (first terminal path wins, guarded by OnBatchExitsPipeline's InFlightBatchListRemove)
    // and cleared in Reset(). Rerouting transfers this charge to the selected leader.
    internal BrokerUnackedByteBudget? UnackedBudget;

    // Intrusive linked list pointers for RecordAccumulator._inFlightBatchList.
    // Using embedded pointers eliminates per-batch ConcurrentDictionary.Node allocations
    // that caused pathological Gen2 GC promotion (nodes survived Gen0 during network
    // round-trip, got promoted to Gen2, then died — creating near 1:1 Gen0:Gen2 ratio).
    // Read and written only while holding RecordAccumulator._inFlightBatchLock.
    internal ReadyBatch? InFlightPrev;
    internal ReadyBatch? InFlightNext;
    // Read and written only while holding RecordAccumulator._inFlightBatchLock.
    // Guards against double-remove races.
    internal bool InFlightLinked;

    private int _pipelineGeneration;

    internal int PipelineGeneration => Volatile.Read(ref _pipelineGeneration);

    internal void EnableDeliveryDiagnostics() =>
        Volatile.Write(ref _pipelineGeneration, Generation);

    /// <summary>
    /// Lightweight lifecycle trace for diagnosing orphaned batches in Release builds.
    /// Tracks the last few transitions as single-char codes to keep overhead minimal.
    /// Codes: E=EntersPipeline, D=Drained, Q=EnqueuedToBrokerSender, C=Coalesced,
    /// O=CarriedOver, S=Sent, R=ResponseReceived, Y=LoopExitRedelivery,
    /// X=ExitsPipeline, F=Failed
    /// </summary>
    internal string DiagTrace
    {
        get
        {
            var len = Volatile.Read(ref _diagTraceLen);
            if (len <= 0)
                return "";

            var count = Math.Min(len, _diagTrace.Length);
            if (len <= _diagTrace.Length)
                return new string(_diagTrace, 0, count);

            var chars = new char[count];
            var start = len - count;
            for (var i = 0; i < count; i++)
                chars[i] = _diagTrace[(start + i) % _diagTrace.Length];

            return new string(chars);
        }
    }
    private readonly char[] _diagTrace = new char[32];
    private int _diagTraceLen;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AppendDiag(char code)
    {
        var idx = Interlocked.Increment(ref _diagTraceLen) - 1;
        _diagTrace[idx % _diagTrace.Length] = code;
    }

    internal ProducerBatchDeliveryDiagnostic CreateDeliveryDiagnostic(
        int expectedGeneration,
        TopicPartition capturedTopicPartition)
    {
        if (!TryAcquireResourcePin(expectedGeneration))
            return CreateStaleDeliveryDiagnostic(capturedTopicPartition, expectedGeneration);

        try
        {
            var recordBatch = _recordBatch;
            var arena = _arena;
            var trace = DiagTrace;
            var pipelineGeneration = PipelineGeneration;
            var isPreSerialized = IsPreSerialized;
            var isSendCompleted = IsSendCompleted;
            var isDoneTaskCompleted = Volatile.Read(ref _completed) != 0;
            var isMemoryReleased = MemoryReleased;
            var isReturnedToPool = IsReturnedToPool;
            var inFlightLinked = InFlightLinked;

            var diagnostic = new ProducerBatchDeliveryDiagnostic
            {
                Topic = _topicPartition.Topic ?? string.Empty,
                Partition = _topicPartition.Partition,
                RecordCount = _recordCount > 0 ? _recordCount : _completionSourcesCount,
                DataSize = DataSize,
                EncodedSize = EncodedSize,
                ReadyBatchId = RuntimeHelpers.GetHashCode(this),
                RecordBatchId = recordBatch is null ? null : RuntimeHelpers.GetHashCode(recordBatch),
                ArenaId = arena is null ? null : RuntimeHelpers.GetHashCode(arena),
                PipelineGeneration = pipelineGeneration,
                CurrentGeneration = Generation,
                LifecycleState = GetLifecycleState(
                    isReturnedToPool,
                    isSendCompleted,
                    isMemoryReleased,
                    inFlightLinked,
                    isPreSerialized),
                Trace = trace,
                LastTouchedBy = DescribeLastTouchedBy(trace),
                IsStale = false,
                IsPreSerialized = isPreSerialized,
                IsSendCompleted = isSendCompleted,
                IsDoneTaskCompleted = isDoneTaskCompleted,
                IsMemoryReleased = isMemoryReleased,
                IsReturnedToPool = isReturnedToPool,
                InFlightLinked = inFlightLinked
            };

            return IsCurrentIncarnation(expectedGeneration)
                ? diagnostic
                : CreateStaleDeliveryDiagnostic(capturedTopicPartition, expectedGeneration);
        }
        finally
        {
            ReleaseResourcePin();
        }
    }

    private ProducerBatchDeliveryDiagnostic CreateStaleDeliveryDiagnostic(
        TopicPartition capturedTopicPartition,
        int expectedGeneration)
        => new()
        {
            Topic = capturedTopicPartition.Topic ?? string.Empty,
            Partition = capturedTopicPartition.Partition,
            RecordCount = 0,
            DataSize = 0,
            EncodedSize = 0,
            ReadyBatchId = RuntimeHelpers.GetHashCode(this),
            RecordBatchId = null,
            ArenaId = null,
            PipelineGeneration = expectedGeneration,
            CurrentGeneration = Generation,
            LifecycleState = "stale",
            Trace = "",
            LastTouchedBy = "stale",
            IsStale = true,
            IsPreSerialized = false,
            IsSendCompleted = false,
            IsDoneTaskCompleted = false,
            IsMemoryReleased = false,
            IsReturnedToPool = false,
            InFlightLinked = false
        };

    private static string GetLifecycleState(
        bool isReturnedToPool,
        bool isSendCompleted,
        bool isMemoryReleased,
        bool inFlightLinked,
        bool isPreSerialized)
    {
        if (isReturnedToPool)
            return "recycled";
        if (isSendCompleted)
            return "completed-send";
        if (isMemoryReleased)
            return "awaiting-response";
        if (inFlightLinked)
            return isPreSerialized ? "in-flight" : "appended";
        return isPreSerialized ? "serialized" : "appended";
    }

    private static string DescribeLastTouchedBy(string trace)
    {
        if (trace.Length == 0)
            return "none";

        return trace[^1] switch
        {
            'E' => "RecordAccumulator.OnBatchEntersPipeline",
            'D' => "RecordAccumulator.Drain",
            'Q' => "KafkaProducer.SenderLoop.EnqueueBrokerSender",
            'C' => "BrokerSender.CoalesceBatch",
            'O' => "BrokerSender.CarryOver",
            'S' => "BrokerSender.SendLoop",
            'G' => "BrokerSender.GetConnection",
            'W' => "BrokerSender.PendingResponse",
            'P' => "BrokerSender.ProcessFaultedResponse",
            'R' => "BrokerSender.ProcessResponse",
            'H' => "BrokerSender.HandleRetriableBatch",
            'T' => "BrokerSender.HandleTimedOutRequest",
            'Z' => "BrokerSender.SendFailed",
            'Y' => "BrokerSender.LoopExitRedelivery",
            'F' => "BrokerSender.FailAndCleanupBatch",
            'X' => "RecordAccumulator.OnBatchExitsPipeline",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Replaces the record batch with a rewritten one (updated PID/epoch/sequence).
    /// Only called during epoch bump recovery — not in the hot path.
    /// </summary>
    internal void RewriteRecordBatch(RecordBatch newRecordBatch) => _recordBatch = newRecordBatch;

    internal void SetEncodedSize(int encodedSize) => EncodedSize = encodedSize;

    /// <summary>
    /// Whether the batch's records have been pre-serialized (and compressed, when
    /// configured). Set after <see cref="RecordAccumulator.PrepareBatchForPublish"/> finishes.
    /// The drain loop skips batches where this is false so the per-broker send loop
    /// only ever handles wire-ready payloads.
    /// Uses Volatile reads for lock-free checking from the sender thread.
    /// </summary>
    internal bool IsPreSerialized
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _preSerialized) != 0;
    }
    private int _preSerialized;

    /// <summary>
    /// Marks pre-serialization as complete. Called by
    /// <see cref="RecordAccumulator.PrepareBatchForPublish"/> after the records are encoded
    /// (and compressed, when configured).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkPreSerialized() => Volatile.Write(ref _preSerialized, 1);

    internal void SetPreSerializationTask(Task task)
    {
        Volatile.Write(ref _preSerializationTask, task);
    }

    internal void StartPreSerializationTask()
    {
        var task = Volatile.Read(ref _preSerializationTask)
            ?? throw new InvalidOperationException("Pre-serialization task was not created.");

        task.Start(TaskScheduler.Default);
    }

    internal void WaitForPreSerializationIfStarted()
    {
        var task = Volatile.Read(ref _preSerializationTask);
        if (task is null || task.IsCompleted)
            return;

        task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Whether BufferMemory release has been claimed or completed for this batch.
    /// Prevents double-release across send and cleanup paths.
    /// Uses atomic operations because multiple threads may race to release:
    /// BrokerSender send loop, orphan sweep, and forceful disposal.
    /// For observation/diagnostics only. Production cleanup uses
    /// <see cref="RecordAccumulator.ReleaseBatchMemory"/>.
    /// </summary>
    internal bool MemoryReleased => Volatile.Read(ref _memoryReleased) != MemoryReleasePending;
    private int _memoryReleased;

    private const int MemoryReleasePending = 0;
    private const int MemoryReleaseInProgress = 1;
    private const int MemoryReleaseComplete = 2;

    /// <summary>
    /// Atomically marks memory as already released. Used by tests and batches whose memory
    /// accounting is intentionally external. Production cleanup uses
    /// <see cref="RecordAccumulator.ReleaseBatchMemory"/> so pool reset waits for release.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TrySetMemoryReleased()
    {
        return Interlocked.CompareExchange(
            ref _memoryReleased,
            MemoryReleaseComplete,
            MemoryReleasePending) == MemoryReleasePending;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryBeginMemoryRelease(out int dataSize)
    {
        if (Interlocked.CompareExchange(
                ref _memoryReleased,
                MemoryReleaseInProgress,
                MemoryReleasePending) != MemoryReleasePending)
        {
            dataSize = 0;
            return false;
        }

        dataSize = DataSize;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CompleteMemoryRelease()
        => Volatile.Write(ref _memoryReleased, MemoryReleaseComplete);

    internal void WaitForMemoryReleaseIfStarted()
    {
        if (Volatile.Read(ref _memoryReleased) != MemoryReleaseInProgress)
            return;

        var spinWait = new SpinWait();
        while (Volatile.Read(ref _memoryReleased) == MemoryReleaseInProgress)
            spinWait.SpinOnce();
    }

    /// <summary>
    /// When true, this batch is a same-broker retry. The send loop unmutes the partition
    /// when coalescing a retry batch, ensuring it is sent before newer batches for the
    /// same partition. Set by ProcessCompletedResponses, cleared during coalescing or in Reset().
    /// </summary>
    internal bool IsRetry { get; set; }

    /// <summary>
    /// True while this batch owns a crash-recovery mute reference. Kept separate from
    /// <see cref="IsRetry"/> because the reference survives sender-to-sender reroutes and
    /// is released only when the batch reaches a terminal state.
    /// </summary>
    internal bool IsLoopExitRedelivery => Volatile.Read(ref _loopExitRecoveryRegistered) != 0;
    private int _loopExitRecoveryRegistered;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryRegisterLoopExitRecovery()
        => Interlocked.CompareExchange(ref _loopExitRecoveryRegistered, 1, 0) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryCompleteLoopExitRecovery()
        => Interlocked.CompareExchange(ref _loopExitRecoveryRegistered, 0, 1) == 1;

    /// <summary>Replacement-sender FIFO position for loop-exit recovery ordering.</summary>
    internal long LoopExitRedeliveryOrder { get; set; }

    /// <summary>
    /// Stopwatch timestamp before which this retry batch should not be sent (backoff).
    /// Set by ProcessCompletedResponses when a retriable error occurs. The send loop
    /// skips batches where the backoff hasn't elapsed. 0 means no backoff.
    /// </summary>
    internal long RetryNotBefore { get; set; }

    /// <summary>
    /// Stopwatch timestamp when the accumulator batch was created. Used for absolute delivery
    /// deadline computation in ProcessCompletedResponses (prevents infinite retries with relative
    /// deadlines and includes configured linger time).
    /// </summary>
    internal long StopwatchCreatedTicks { get; private set; }

    /// <summary>
    /// Stopwatch timestamp when the accumulator batch was sealed into this ready batch. Producer
    /// queue-latency control starts here so configured linger is not mistaken for admission delay.
    /// </summary>
    internal long StopwatchSealedTicks { get; private set; }

    /// <summary>
    /// Age of this ready batch in milliseconds since sealing (or since last reenqueue).
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

    // DoneTask is observed only by diagnostics/tests; producer flushing uses the accumulator's
    // in-flight counter. Allocate its source lazily so the hot path remains allocation-free.
    // A Task is deliberately used instead of a resettable IValueTaskSource: ReadyBatch can be
    // returned to its pool before an asynchronous continuation consumes the prior result.
    private sealed class DoneTaskState(int generation)
    {
        public int Generation { get; } = generation;
        public TaskCompletionSource<bool> Source { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private SpinLock _doneTaskObserverLock = new(enableThreadOwnerTracking: false);
    private List<DoneTaskState>? _doneTaskObservers;
    private int _doneTaskObserverCount;
    private long _lastDoneTaskCompletion;
    internal Action? AfterDoneTaskObserverIntentPublishedForTest;
    internal Action? AfterDoneTaskObserverRegisteredForTest;

    private const int CompletionSucceeded = 1;
    private const int CompletionFailed = 2;

    private int _cleanedUp; // 0 = not cleaned, 1 = cleaned (prevents double-cleanup in Cleanup())
    private const int ResourceCleanupPending = 0;
    private const int ResourceCleanupInProgress = 1;
    private const int ResourceCleanupComplete = 2;

    private int _resourcesCleanedUp;
    private int _activeResourcePins; // readers holding RecordBatch/arena stable during serialization
    private int _completed; // 0 = pending, 1 = success, 2 = failure
    private int _sendCompleted; // 0 = not done, 1 = done (prevents concurrent CompleteSend/Fail)
    private int _terminalBookkeepingCompleted = 1; // generation-safe terminal owner finished post-Fail cleanup
    internal int _returnedToPool; // 0 = not returned, 1 = returned (prevents double pool return)
    private int _generation; // Incremented on each Initialize() — detects batch object recycling
    private Task? _preSerializationTask;

    /// <summary>
    /// True when this batch has been returned to the pool. The authoritative signal for
    /// "batch is no longer live" — set atomically by ReturnReadyBatch before Reset() runs.
    /// </summary>
    internal bool IsReturnedToPool => Volatile.Read(ref _returnedToPool) != 0;

    internal bool IsSendCompleted => Volatile.Read(ref _sendCompleted) != 0;

    /// <summary>
    /// Monotonically increasing counter, incremented on each Initialize(). Used to detect
    /// batch object recycling: if a PendingResponse holds a reference to a batch and the
    /// batch's generation doesn't match the expected value, the batch was recycled
    /// (Reset + re-Initialize with a different topic) while the PendingResponse was in flight.
    /// </summary>
    internal int Generation => Volatile.Read(ref _generation);

    /// <summary>
    /// Returns true if this batch is still the live incarnation the caller captured
    /// <paramref name="expectedGeneration"/> from. Unlike <see cref="IsReturnedToPool"/>
    /// alone — whose flag is reset when the object is re-rented, making a recycled batch
    /// indistinguishable from a live one — this also compares the generation stamp, which
    /// only ever moves forward.
    /// </summary>
    /// <remarks>
    /// Read order matters and must not be changed: <see cref="IsReturnedToPool"/> is read
    /// before <see cref="Generation"/>. Initialize() increments the generation before it
    /// clears <c>_returnedToPool</c>, so a reader that observes the cleared flag is
    /// guaranteed to observe the new generation and fail the comparison. Reading in the
    /// opposite order reintroduces the window where both checks pass on a recycled batch.
    /// </remarks>
    internal bool IsCurrentIncarnation(int expectedGeneration)
        => !IsReturnedToPool && Generation == expectedGeneration;

    internal bool TryAcquireResourcePin(int expectedGeneration)
    {
        while (true)
        {
            if (!IsCurrentIncarnation(expectedGeneration) || Volatile.Read(ref _cleanedUp) != 0)
                return false;

            var current = Volatile.Read(ref _activeResourcePins);
            if (Interlocked.CompareExchange(ref _activeResourcePins, current + 1, current) == current)
            {
                if (IsCurrentIncarnation(expectedGeneration) && Volatile.Read(ref _cleanedUp) == 0)
                    return true;

                ReleaseResourcePin();
                return false;
            }
        }
    }

    internal void ReleaseResourcePin()
    {
        var remaining = Interlocked.Decrement(ref _activeResourcePins);
        Debug.Assert(remaining >= 0, "ReadyBatch resource pin count went negative");
        if (remaining == 0 && Volatile.Read(ref _cleanedUp) != 0)
            TryCleanupResources();
    }

    internal void WaitForResourcePins()
    {
        if (Volatile.Read(ref _activeResourcePins) == 0)
            return;

        var sw = new SpinWait();
        do { sw.SpinOnce(); }
        while (Volatile.Read(ref _activeResourcePins) != 0);
    }

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
        int dataSize,
        BatchArena? arena = null,
        Action<RecordMetadata, Exception?>?[]? callbacks = null,
        int callbackCount = 0,
        BatchArrayReuseQueue? arrayReuseQueue = null,
        int recordCount = 0,
        long createdStopwatchTimestamp = 0)
    {
        // The generation increment must happen BEFORE the lifecycle flags are cleared.
        // Stale-reference holders validate liveness by reading _returnedToPool first and
        // _generation second (see IsCurrentIncarnation). With the increment ordered first,
        // a stale holder that observes _returnedToPool == 0 (this re-initialization already
        // cleared it) is guaranteed to also observe the new generation and bail on the
        // mismatch. With the old order (flags first), a holder could see _returnedToPool == 0
        // while still reading its own stale generation — passing both checks and acting on
        // a batch now owned by someone else.
        Interlocked.Increment(ref _generation);

        // Reset lifecycle flags at the START of a new lifecycle (not in Reset()).
        // This ensures stale references from a previous lifecycle see _cleanedUp=1
        // and return early from CompleteSend/Fail, preventing pool corruption.
        // _memoryReleased follows the same pattern: stale references seeing a nonzero state
        // will skip the release, which is the safe/defensive behavior.
        Interlocked.Exchange(ref _cleanedUp, 0);
        Interlocked.Exchange(ref _resourcesCleanedUp, ResourceCleanupPending);
        Interlocked.Exchange(ref _activeResourcePins, 0);
        Interlocked.Exchange(ref _completed, 0);
        Interlocked.Exchange(ref _sendCompleted, 0);
        Volatile.Write(ref _terminalBookkeepingCompleted, 1);
        Interlocked.Exchange(ref _returnedToPool, 0);
        Interlocked.Exchange(ref _memoryReleased, 0);
        Volatile.Write(ref _preSerialized, 0);
        Volatile.Write(ref _preSerializationTask, null);

        _topicPartition = topicPartition;
        _recordBatch = recordBatch;
        _completionSourcesArray = completionSourcesArray;
        _completionSourcesCount = completionSourcesCount;
        // Captured at seal time because the RecordBatch's record list may be recycled or
        // never materialized by the time a (possibly stale) Fail needs the count.
        _recordCount = recordCount;
        DataSize = dataSize;
        EncodedSize = dataSize;
        _arena = arena;
        _callbacks = callbacks;
        _callbackCount = callbackCount;
        _arrayReuseQueue = arrayReuseQueue;
        StopwatchSealedTicks = Stopwatch.GetTimestamp();
        StopwatchCreatedTicks = createdStopwatchTimestamp > 0
            ? createdStopwatchTimestamp
            : StopwatchSealedTicks;
        _createdTimestamp = StopwatchSealedTicks;
    }

    /// <summary>
    /// Resets the batch for reuse. Called by ReadyBatchPool.Return().
    /// </summary>
    public void Reset()
    {
        // SAFETY NET: If CompleteSend/Fail wasn't called (_cleanedUp still 0), resolve
        // orphaned completion sources and return pooled arrays before the batch goes back
        // to the pool. Without this, ProduceAsync callers hang forever on unresolved sources.
        // WaitForCleanupIfStarted in ReturnReadyBatch handles the race where CompleteSend/Fail
        // has started but Cleanup hasn't finished. If we reach here, neither was ever called.
        if (Volatile.Read(ref _cleanedUp) == 0)
        {
            ProducerDebugCounters.RecordBatchFailed();
            var failedCount = 0;
            if (_completionSourcesCount > 0 && _completionSourcesArray is not null)
            {
                var orphanedException = new InvalidOperationException(
                    $"Batch recycled without completing delivery — this indicates a bug in the producer pipeline " +
                    $"(tp={_topicPartition}, sendCompleted={Volatile.Read(ref _sendCompleted)}, " +
                    $"completed={Volatile.Read(ref _completed)}, trace={DiagTrace})");
                for (var i = 0; i < _completionSourcesCount; i++)
                {
                    if (PooledCompletionSource.TrySetException(
                        _completionSourcesArray[i], orphanedException))
                        failedCount++;
                }
            }

            ProducerDebugCounters.RecordCompletionSourceFailed(failedCount);
            CompleteDoneTask(succeeded: false);

            // Cleanup() is idempotent (uses Interlocked.Exchange), so double-call is safe.
            Cleanup();
        }

        WaitForCleanupIfStarted();

        // Clear all references to allow GC
        _topicPartition = default;
        _recordBatch = null!;
        _completionSourcesArray = null;
        _completionSourcesCount = 0;
        DataSize = 0;
        EncodedSize = 0;
        _arena = null;
        _callbacks = null;
        _callbackCount = 0;
        _arrayReuseQueue = null;
        InflightEntry = null;
        UnackedBudget = null;
        InFlightPrev = null;
        InFlightNext = null;
        InFlightLinked = false;
        // NOTE: _memoryReleased is NOT reset here — it stays complete while in pool,
        // so stale references calling TrySetMemoryReleased() return false (safe no-op).
        // Cleared in Initialize() at the start of the next lifecycle.
        IsRetry = false;
        Volatile.Write(ref _loopExitRecoveryRegistered, 0);
        LoopExitRedeliveryOrder = 0;
        RetryNotBefore = 0;
        _diagTraceLen = 0;

        // NOTE: _cleanedUp, _completed, and _sendCompleted are NOT reset here. They stay
        // nonzero (armed) while the batch is in the pool, so stale references from a
        // previous lifecycle calling CompleteSend/Fail will hit the guard and return early.
        // These flags are reset in Initialize() when the batch starts a new lifecycle.
        // Captured DoneTask instances retain their own TaskCompletionSource after pool return.
    }

    /// <summary>
    /// Marks batch as "ready" (processed by completion loop).
    /// Called by RecordAccumulator.CompletionLoopAsync or unified SenderLoopAsync.
    /// This unblocks FlushAsync for fire-and-forget scenarios.
    /// For unit tests without a sender loop, this is the final completion.
    /// </summary>
    public void CompleteDelivery()
    {
        CompleteDoneTask(succeeded: true);
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
                    if (PooledCompletionSource.TrySetResult(source, new RecordMetadata
                    {
                        Topic = _topicPartition.Topic,
                        Partition = _topicPartition.Partition,
                        Offset = baseOffset + i,
                        Timestamp = timestamp
                    }))
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
                            ProducerCallbackContext.Invoke(
                                callback,
                                new RecordMetadata
                                {
                                    Topic = _topicPartition.Topic,
                                    Partition = _topicPartition.Partition,
                                    Offset = baseOffset + i,
                                    Timestamp = timestamp
                                },
                                null);
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
            CompleteDoneTask(succeeded: true);
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
        => FailCore(exception, waitForPreSerialization: true);

    internal bool FailFromPreSerialization(Exception exception)
        => FailCore(exception, waitForPreSerialization: false);

    /// <summary>
    /// Claims terminal completion for the expected incarnation while its caller holds a
    /// resource pin. Pool return waits for pins before evaluating cleanup state, so a
    /// successful claim may safely finish after releasing the pin.
    /// </summary>
    internal bool TryClaimSendCompletion(int expectedGeneration)
    {
        if (!IsCurrentIncarnation(expectedGeneration)
            || Interlocked.CompareExchange(ref _sendCompleted, 1, 0) != 0)
        {
            return false;
        }

        // Caller holds a resource pin. Any racing pool return must wait for that pin,
        // then for this flag, before Reset can recycle fields used by post-Fail cleanup.
        Volatile.Write(ref _terminalBookkeepingCompleted, 0);
        return true;
    }

    internal void CompleteTerminalBookkeeping()
        => Volatile.Write(ref _terminalBookkeepingCompleted, 1);

    internal void WaitForTerminalBookkeeping()
    {
        if (Volatile.Read(ref _terminalBookkeepingCompleted) != 0)
            return;

        var sw = new SpinWait();
        do { sw.SpinOnce(); }
        while (Volatile.Read(ref _terminalBookkeepingCompleted) == 0);
    }

    internal void FailAfterSendCompletionClaimed(Exception exception)
        => CompleteFailure(exception, waitForPreSerialization: true);

    private bool FailCore(Exception exception, bool waitForPreSerialization)
    {
        // Atomic entry guard: only one thread can execute Fail/CompleteSend.
        // Separate from _cleanedUp so Cleanup() in the finally block still runs.
        if (Interlocked.Exchange(ref _sendCompleted, 1) != 0)
            return false;

        CompleteFailure(exception, waitForPreSerialization);
        return true;
    }

    private void CompleteFailure(Exception exception, bool waitForPreSerialization)
    {
        if (waitForPreSerialization)
            WaitForPreSerializationIfStarted();

        try
        {
            ProducerDebugCounters.RecordBatchFailed();

            // Records without a completion source (fire-and-forget and callback appends) have
            // no awaiter that will observe this failure, so count them here — otherwise
            // messaging.client.sent.errors never reflects fire-and-forget delivery failures.
            // ProduceAsync records are excluded: each awaiter increments the counter itself.
            var unobservedRecords = _recordCount - _completionSourcesCount;
            if (Diagnostics.DekafMetrics.ProduceErrors.Enabled && unobservedRecords > 0)
            {
                Diagnostics.DekafMetrics.ProduceErrors.Add(unobservedRecords, new TagList
                {
                    { Diagnostics.DekafDiagnostics.MessagingDestinationName, _topicPartition.Topic }
                });
            }

            // Fail per-message completion sources - these throw for ProduceAsync callers
            if (_completionSourcesCount > 0 && _completionSourcesArray is not null)
            {
#if DEBUG
                var failedCount = 0;
#endif
                for (var i = 0; i < _completionSourcesCount; i++)
                {
                    var source = _completionSourcesArray[i];
                    if (PooledCompletionSource.TrySetException(source, exception))
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
                            ProducerCallbackContext.Invoke(callback, default, exception);
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
            CompleteDoneTask(succeeded: false);
        }
        finally
        {
            Cleanup();
        }
    }

    private void CompleteDoneTask(bool succeeded)
    {
        var generation = Generation;
        var completion = succeeded ? CompletionSucceeded : CompletionFailed;
        if (Interlocked.CompareExchange(ref _completed, completion, 0) != 0)
            return;

        if (Volatile.Read(ref _doneTaskObserverCount) == 0)
        {
            Volatile.Write(ref _lastDoneTaskCompletion, PackDoneTaskCompletion(generation, completion));

            // Close the race with a registration that published its intent after the first
            // count read. It will either consume the packed result under the observer lock,
            // or this thread will acquire that lock and deliver the result directly.
            if (Volatile.Read(ref _doneTaskObserverCount) == 0)
                return;
        }

        CompleteDoneTaskObservers(generation, completion, succeeded);
    }

    private DoneTaskState RegisterDoneTaskObserver(out bool? completedResult)
    {
        completedResult = null;
        var lockTaken = false;
        var observerCountIncremented = false;
        try
        {
            _doneTaskObserverLock.Enter(ref lockTaken);

            // Publish intent while holding the same lock used by observed completions. Once
            // the count is visible, the current lifecycle cannot complete and recycle before
            // its generation is captured and registered below.
            Interlocked.Increment(ref _doneTaskObserverCount);
            observerCountIncremented = true;
            AfterDoneTaskObserverIntentPublishedForTest?.Invoke();

            var state = new DoneTaskState(Generation);
            (_doneTaskObservers ??= new List<DoneTaskState>(1)).Add(state);

            // A completion that observed zero registrations publishes its result before
            // checking the count again. Consume that hand-off before releasing the lock so a
            // later lifecycle cannot overwrite it first.
            if (TryGetDoneTaskCompletion(state.Generation, out var succeeded))
            {
                _doneTaskObservers.Remove(state);
                Interlocked.Decrement(ref _doneTaskObserverCount);
                observerCountIncremented = false;
                completedResult = succeeded;
            }

            return state;
        }
        catch
        {
            if (observerCountIncremented)
                Interlocked.Decrement(ref _doneTaskObserverCount);
            throw;
        }
        finally
        {
            if (lockTaken)
                _doneTaskObserverLock.Exit();
        }
    }

    private void CompleteDoneTaskObservers(int generation, int completion, bool succeeded)
    {
        var lockTaken = false;
        try
        {
            _doneTaskObserverLock.Enter(ref lockTaken);
            Volatile.Write(ref _lastDoneTaskCompletion, PackDoneTaskCompletion(generation, completion));

            if (_doneTaskObservers is null)
                return;

            for (var i = _doneTaskObservers.Count - 1; i >= 0; i--)
            {
                var state = _doneTaskObservers[i];
                if (state.Generation != generation)
                    continue;

                _doneTaskObservers.RemoveAt(i);
                Interlocked.Decrement(ref _doneTaskObserverCount);
                state.Source.TrySetResult(succeeded);
            }
        }
        finally
        {
            if (lockTaken)
                _doneTaskObserverLock.Exit();
        }
    }

    private bool TryGetDoneTaskCompletion(int generation, out bool succeeded)
    {
        var packedCompletion = Volatile.Read(ref _lastDoneTaskCompletion);
        var result = unchecked((int)packedCompletion);
        if (result != 0 && unchecked((int)(packedCompletion >> 32)) == generation)
        {
            succeeded = result == CompletionSucceeded;
            return true;
        }

        succeeded = false;
        return false;
    }

    private static long PackDoneTaskCompletion(int generation, int completion)
        => ((long)generation << 32) | (uint)completion;

    /// <summary>
    /// If CompleteSend/Fail has been called but Cleanup hasn't finished yet, spin-waits
    /// for the winning thread to complete. Returns immediately if no send completion has
    /// started (_sendCompleted == 0) — the safety net in Reset() handles that case.
    /// </summary>
    /// <remarks>
    /// Prevents a race where ForceFailAllInFlightBatches loses the _sendCompleted CAS
    /// (because BrokerSender's CompleteSend won it), then immediately returns the batch
    /// to pool via ReturnReadyBatch — before CompleteSend reaches its finally { Cleanup() }.
    /// Reset() would see _cleanedUp == 0 and fire the safety net, corrupting completion
    /// sources that CompleteSend is still iterating. If cleanup is deferred behind active
    /// resource pins, also waits until the final pin returns the arena and RecordBatch to pools.
    /// </remarks>
    internal void WaitForCleanupIfStarted()
    {
        if (Volatile.Read(ref _cleanedUp) == 0 && Volatile.Read(ref _sendCompleted) != 0)
        {
            var sw = new SpinWait();
            do { sw.SpinOnce(); }
            while (Volatile.Read(ref _cleanedUp) == 0);
        }

        if (Volatile.Read(ref _cleanedUp) != 0
            && Volatile.Read(ref _resourcesCleanedUp) != ResourceCleanupComplete)
        {
            // Reset must wait for deferred resource cleanup; otherwise it can recycle fields
            // while the last serialization pin is still returning the old RecordBatch/arena.
            var sw = new SpinWait();
            do { sw.SpinOnce(); }
            while (Volatile.Read(ref _resourcesCleanedUp) != ResourceCleanupComplete);
        }
    }

    private void Cleanup()
    {
        // Guard against double-cleanup request. Actual pooled-resource return may be
        // deferred until active serialization pins release.
        if (Interlocked.Exchange(ref _cleanedUp, 1) != 0)
            return;

        TryCleanupResources();
    }

    private void TryCleanupResources()
    {
        if (Volatile.Read(ref _activeResourcePins) != 0)
            return;

        if (Interlocked.CompareExchange(
                ref _resourcesCleanedUp,
                ResourceCleanupInProgress,
                ResourceCleanupPending) != ResourceCleanupPending)
            return;

        var completionSourcesArray = _completionSourcesArray;
        var arrayReuseQueue = _arrayReuseQueue;
        var arena = _arena;
        var callbacks = _callbacks;
        var recordBatch = _recordBatch;

        try
        {
            // Return the working (container) array: either to the reuse queue for fast recycling
            // back to PartitionBatch, or to ArrayPool as fallback.
            // Safety: clearArray: false is intentional. Array slots past the used count may contain
            // stale PooledValueTaskSource references, but these are never accessed because counters
            // reset to 0 on reuse. PooledValueTaskSource instances auto-return to their pool via
            // GetResult(), so stale references don't prevent pool recycling.
            if (completionSourcesArray is not null)
            {
                if (arrayReuseQueue is not null)
                {
                    arrayReuseQueue.EnqueueOrReturn(completionSourcesArray);
                }
                else
                {
                    // Fallback: return to the dedicated pool (not ArrayPool<T>.Shared) to prevent
                    // TLS accumulation from cross-thread return on BrokerSender threads
                    ProducerContainerPools.CompletionSources.Return(completionSourcesArray, clearArray: false);
                }
            }

            // Return arena to pool for reuse (arena-based path)
            // This avoids allocating a new BatchArena object on each batch recycle
            if (arena is not null)
            {
                BatchArena.ReturnToPool(arena);
            }

            // Return callback array to dedicated pool if present
            if (callbacks is not null)
            {
                ProducerContainerPools.Callbacks.Return(callbacks, clearArray: true);
            }

            // Return pre-compressed buffer and RecordBatch to pool.
            // ReturnPreCompressedBuffer() releases the producer-pool buffer, then ReturnToPool()
            // clears all references and returns the RecordBatch object for reuse.
            if (recordBatch is not null)
            {
                recordBatch.ReturnPreCompressedBuffer();
                recordBatch.DisposeRecordList();
                recordBatch.ReturnToPool();
            }
        }
        finally
        {
            // Reset must not clear fields until every pooled resource return above completes.
            Volatile.Write(ref _resourcesCleanedUp, ResourceCleanupComplete);
        }
    }
}
