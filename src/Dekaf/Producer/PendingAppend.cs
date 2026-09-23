using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using Dekaf.Errors;
using Dekaf.Serialization;

namespace Dekaf.Producer;

/// <summary>
/// Pooled <see cref="IValueTaskSource{T}"/> that replaces the async state machine
/// on the producer slow path (buffer full / backpressure).
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="RecordAccumulator.TryReserveMemory"/> fails, instead of entering an
/// <c>async ValueTask&lt;bool&gt;</c> method (which allocates ~200+ bytes for the compiler-generated
/// state machine), the caller rents a <see cref="PendingAppend"/> from a pool, stores all
/// append parameters, enqueues it, and returns <c>new ValueTask&lt;bool&gt;(op, op.Version)</c>.
/// </para>
/// <para>
/// Completion is driven by one of four sources (CAS on <c>_state</c> ensures exactly one wins):
/// <list type="bullet">
///   <item><see cref="TryClaim"/> — called by <see cref="RecordAccumulator.DrainPendingAppends"/>
///   when buffer space is freed.</item>
///   <item>Timeout — <see cref="_timer"/> fires when max.block.ms deadline expires.</item>
///   <item>Cancellation — <see cref="CancellationToken.Register(Action{object}, object)"/> callback.</item>
///   <item>Disposal — <see cref="TryFail(Exception, int)"/> called during <see cref="RecordAccumulator.DisposeAsync"/>.</item>
/// </list>
/// </para>
/// <para>
/// <c>_state</c> doubles as the incarnation token: it is even while a rental is pending and odd
/// once that rental completes (or the instance is idle in the pool). Each
/// <see cref="Initialize"/> advances it to the next even value, so a reference captured for an
/// earlier rental can never claim or fail a later one (#3389).
/// </para>
/// <para>
/// The <see cref="Timer"/> is allocated once in the constructor and reused via <c>Change()</c>
/// across rentals, avoiding per-message timer allocation.
/// </para>
/// </remarks>
internal sealed class PendingAppend : IValueTaskSource<bool>
{
    private ManualResetValueTaskSourceCore<bool> _core;
    // Even = pending incarnation (the value is that rental's Generation); odd = completed or idle.
    // Completion CASes generation -> generation + 1, so each incarnation completes exactly once.
    private int _state = 1;
    private int _generation;

    // Append parameters — stored on Initialize, consumed by drain
    private string _topic = null!;
    private int _partition;
    private int _partitionCount;
    private long _timestamp;
    private PooledMemory _key;
    private PooledMemory _value;
    private Header[]? _headers;
    private int _headerCount;
    private PooledValueTaskSource<RecordMetadata>? _completionSource;
    private Action<RecordMetadata, Exception?>? _callback;
    private int _recordSize;

    // Timeout / cancellation state
    private readonly Timer _timer;
    private CancellationTokenRegistration _cancellationRegistration;
    private CancellationToken _cancellationToken;
    private long _startTicks;
    private long _deadlineTickCount;
    private RecordAccumulator _accumulator = null!;
    private int _pendingCounted;

    // Pool return
    private PendingAppendPool _pool = null!;

    /// <summary>
    /// Gets the current version token for <see cref="ValueTask{T}"/> binding.
    /// </summary>
    public short Version => _core.Version;

    /// <summary>
    /// Whether this operation has been completed (drain, timeout, cancel, or dispose).
    /// </summary>
    public bool IsCompleted => (Volatile.Read(ref _state) & 1) != 0;

    /// <summary>
    /// The incarnation token of the current rental, assigned by <see cref="Initialize"/>.
    /// Queue entries and drain reservations capture it so they only ever act on the rental
    /// they were created for.
    /// </summary>
    internal int Generation => _generation;

    /// <summary>
    /// Whether the rental identified by <paramref name="generation"/> is still pending. False once
    /// it completed, and for every later rental of this pooled instance.
    /// </summary>
    internal bool IsPending(int generation) => Volatile.Read(ref _state) == generation;

    // Expose stored parameters for DrainPendingAppends
    internal string Topic => _topic;
    internal int Partition => _partition;
    internal int PartitionCount => _partitionCount;
    internal long Timestamp => _timestamp;
    internal PooledMemory Key => _key;
    internal PooledMemory Value => _value;
    internal Header[]? Headers => _headers;
    internal int HeaderCount => _headerCount;
    internal PooledValueTaskSource<RecordMetadata>? CompletionSource => _completionSource;
    internal Action<RecordMetadata, Exception?>? Callback => _callback;
    internal int RecordSize => _recordSize;

    /// <summary>
    /// The transactional append generation the produce was admitted in, validated at the append
    /// commit point; <see cref="RecordAccumulator.NoTransactionalGeneration"/> skips the check.
    /// </summary>
    internal int TransactionalGeneration { get; set; } = RecordAccumulator.NoTransactionalGeneration;

    /// <summary>Monotonic milliseconds when the originating append started blocking; the
    /// drain uses this to report the admission wait actually paid.</summary>
    internal long StartTicks => _startTicks;

    public PendingAppend()
    {
        _core.RunContinuationsAsynchronously = true;

        // Allocate timer once; it starts dormant (Timeout.Infinite).
        // Reused across pool rentals via Change().
        _timer = new Timer(static state =>
        {
            var self = (PendingAppend)state!;
            self.OnTimeout();
        }, this, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Initializes this instance for a new append operation.
    /// Must be called after renting from the pool and before enqueuing.
    /// </summary>
    internal void Initialize(
        string topic,
        int partition,
        int partitionCount,
        long timestamp,
        PooledMemory key,
        PooledMemory value,
        Header[]? headers,
        int headerCount,
        PooledValueTaskSource<RecordMetadata>? completionSource,
        Action<RecordMetadata, Exception?>? callback,
        int recordSize,
        long startTicks,
        long deadlineTickCount,
        RecordAccumulator accumulator,
        PendingAppendPool pool,
        CancellationToken cancellationToken)
    {
        _topic = topic;
        _partition = partition;
        _partitionCount = partitionCount;
        _timestamp = timestamp;
        _key = key;
        _value = value;
        _headers = headers;
        _headerCount = headerCount;
        _completionSource = completionSource;
        _callback = callback;
        _recordSize = recordSize;
        _startTicks = startTicks;
        _deadlineTickCount = deadlineTickCount;
        _cancellationToken = cancellationToken;
        _accumulator = accumulator;
        _pool = pool;
        accumulator.IncrementSlowPathAppendCount(topic, partition);
        Volatile.Write(ref _pendingCounted, 1);

        // Idle state is odd; the next even value names this rental. Parameters above are
        // published by this release write, so a reader that observes the generation sees them.
        var generation = unchecked(_state + 1);
        _generation = generation;
        Volatile.Write(ref _state, generation);

        // Arm timeout timer. Compute remaining ms from deadline.
        var remainingMs = deadlineTickCount - Dekaf.MonotonicClock.GetMilliseconds();
        if (remainingMs > 0)
        {
            _timer.Change(remainingMs, Timeout.Infinite);
        }
        else
        {
            // Already expired — fire immediately
            _timer.Change(0, Timeout.Infinite);
        }

        // Register cancellation callback (zero-alloc if token is not cancellable)
        if (cancellationToken.CanBeCanceled)
        {
            _cancellationRegistration = cancellationToken.Register(static state =>
            {
                var self = (PendingAppend)state!;
                self.OnCancellation();
            }, this);
        }
    }

    /// <summary>
    /// Claims ownership of this operation (CAS from pending to completed).
    /// Called by <see cref="RecordAccumulator.DrainPendingAppends"/> BEFORE calling
    /// <see cref="RecordAccumulator.AppendPooledAfterReservationCore"/> to prevent timeout/cancel
    /// from cleaning up resources while the drain is using them.
    /// </summary>
    /// <param name="generation">The <see cref="Generation"/> observed when the drain reserved for
    /// this operation. A later rental of the same pooled instance has a different generation and is
    /// never claimed through a stale reference.</param>
    /// <returns>True if this call won the race; false if timeout/cancel/dispose already completed that
    /// rental (the instance may since have been reused).</returns>
    public bool TryClaim(int generation)
    {
        if (!TryComplete(generation))
            return false;

        DisarmTimerAndCancellation();
        return true;
    }

    /// <summary>
    /// Releases per-partition FIFO ownership after a claimed append has entered its batch.
    /// Must run before completing the value task, which can return this object to its pool.
    /// </summary>
    internal void ReleasePendingCountAfterClaim() => ReleasePendingCount();

    /// <summary>
    /// Sets the successful result after <see cref="TryClaim"/> + AppendPooledAfterReservationCore.
    /// Must only be called after <see cref="TryClaim"/> returned true.
    /// Resources are consumed by AppendPooledAfterReservationCore — no cleanup needed.
    /// </summary>
    public void CompleteResult(bool result) => _core.SetResult(result);

    /// <summary>
    /// Sets an exception result after <see cref="TryClaim"/> + failed AppendPooledAfterReservationCore.
    /// Must only be called after <see cref="TryClaim"/> returned true.
    /// AppendPooledAfterReservationCore handles its own resource cleanup on throw.
    /// </summary>
    public void CompleteException(Exception exception) => _core.SetException(exception);

    /// <summary>
    /// Attempts to fail the operation with an exception (timeout, cancellation, disposal).
    /// Cleans up owned resources (PooledMemory, headers) since
    /// drain will not process this operation.
    /// </summary>
    /// <param name="exception">The exception to complete with.</param>
    /// <returns>True if this call won the completion race; false if drain already claimed it.</returns>
    public bool TryFail(Exception exception) => TryFail(exception, Volatile.Read(ref _state));

    /// <summary>
    /// Fails the rental identified by <paramref name="generation"/>. Used by queue sweeps, whose
    /// entries may outlive the rental they were enqueued for.
    /// </summary>
    internal bool TryFail(Exception exception, int generation)
    {
        if (!TryComplete(generation))
            return false;

        ReleasePendingCount();
        DisarmTimerAndCancellation();

        // Clean up owned resources since drain will not process this operation
        _key.Return();
        _value.Return();
        RecordAccumulator.ReturnPooledHeaders(_headers, _headerCount);

        _core.SetException(exception);
        return true;
    }

    /// <summary>
    /// Manually returns this instance to the pool when TryFail succeeded but the caller
    /// bypasses the normal GetResult path (e.g., returning ValueTask.FromException directly).
    /// Must only be called after TryFail returned true.
    /// </summary>
    internal void ReturnToPoolAfterTryFail() => ResetAndReturnToPool();

    private void OnTimeout()
    {
        // Timer.Change(Infinite) can leave an already-queued callback from a previous rental.
        if (IsCompleted)
            return;

        var now = Dekaf.MonotonicClock.GetMilliseconds();
        var deadlineTickCount = Volatile.Read(ref _deadlineTickCount);
        if (now < deadlineTickCount)
        {
            _timer.Change(deadlineTickCount - now, Timeout.Infinite);
            return;
        }

        var accumulator = _accumulator;
        if (accumulator is null)
            return;

        var configured = TimeSpan.FromMilliseconds(accumulator.MaxBlockMsOption);
        var elapsed = TimeSpan.FromMilliseconds(now - _startTicks);

        var exception = new KafkaTimeoutException(
            TimeoutKind.MaxBlock,
            elapsed,
            configured,
            accumulator.BuildBufferTimeoutMessage(_recordSize));

        if (TryFail(exception))
            accumulator.DrainPendingAppendsIfHead(this);
    }

    private void OnCancellation()
    {
        var accumulator = _accumulator;
        if (TryFail(new OperationCanceledException(_cancellationToken)))
            accumulator?.DrainPendingAppendsIfHead(this);
    }

    /// <summary>
    /// Clears all references, resets the core for reuse, and returns to the pool.
    /// Shared by <see cref="IValueTaskSource{T}.GetResult"/> and <see cref="ReturnToPoolAfterTryFail"/>.
    /// </summary>
    private void ResetAndReturnToPool()
    {
        // Clear references to avoid rooting objects across pool rentals
        _topic = null!;
        _partitionCount = 0;
        TransactionalGeneration = RecordAccumulator.NoTransactionalGeneration;
        _key = default;
        _value = default;
        _headers = null;
        _completionSource = null;
        _callback = null;
        _cancellationToken = default;
        _deadlineTickCount = 0;
        _accumulator = null!;
        _pendingCounted = 0;
        _core.Reset();
        _pool.Return(this);
    }

    /// <summary>
    /// Completes the rental named by <paramref name="generation"/> if it is still pending. Odd
    /// values (completed or idle) never match a pending state, so they always fail.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryComplete(int generation) =>
        (generation & 1) == 0
        && Interlocked.CompareExchange(ref _state, unchecked(generation + 1), generation) == generation;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReleasePendingCount()
    {
        if (Interlocked.Exchange(ref _pendingCounted, 0) != 0)
            _accumulator.DecrementSlowPathAppendCount(_topic, _partition);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DisarmTimerAndCancellation()
    {
        // Disarm timer (dormant until next rental)
        _timer.Change(Timeout.Infinite, Timeout.Infinite);

        // Dispose cancellation registration
        _cancellationRegistration.Dispose();
        _cancellationRegistration = default;
    }

    bool IValueTaskSource<bool>.GetResult(short token)
    {
        try
        {
            return _core.GetResult(token);
        }
        finally
        {
            ResetAndReturnToPool();
        }
    }

    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
    {
        return _core.GetStatus(token);
    }

    void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _core.OnCompleted(continuation, state, token, flags);
    }
}
