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
/// Completion is driven by one of four sources (CAS on <c>_completed</c> ensures exactly one wins):
/// <list type="bullet">
///   <item><see cref="TryComplete"/> — called by <see cref="RecordAccumulator.DrainPendingAppends"/>
///   when buffer space is freed.</item>
///   <item>Timeout — <see cref="_timer"/> fires when max.block.ms deadline expires.</item>
///   <item>Cancellation — <see cref="CancellationToken.Register"/> callback.</item>
///   <item>Disposal — <see cref="TryFail"/> called during <see cref="RecordAccumulator.DisposeAsync"/>.</item>
/// </list>
/// </para>
/// <para>
/// The <see cref="Timer"/> is allocated once in the constructor and reused via <c>Change()</c>
/// across rentals, avoiding per-message timer allocation.
/// </para>
/// </remarks>
internal sealed class PendingAppend : IValueTaskSource<bool>
{
    private ManualResetValueTaskSourceCore<bool> _core;
    private int _completed; // 0 = pending, 1 = completed (CAS guard)

    // Append parameters — stored on Initialize, consumed by drain
    private string _topic = null!;
    private int _partition;
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
    private long _startTicks;
    private RecordAccumulator _accumulator = null!;

    // Pool return
    private PendingAppendPool _pool = null!;

    /// <summary>
    /// Gets the current version token for <see cref="ValueTask{T}"/> binding.
    /// </summary>
    public short Version => _core.Version;

    /// <summary>
    /// Whether this operation has been completed (drain, timeout, cancel, or dispose).
    /// </summary>
    public bool IsCompleted => Volatile.Read(ref _completed) != 0;

    // Expose stored parameters for DrainPendingAppends
    internal string Topic => _topic;
    internal int Partition => _partition;
    internal long Timestamp => _timestamp;
    internal PooledMemory Key => _key;
    internal PooledMemory Value => _value;
    internal Header[]? Headers => _headers;
    internal int HeaderCount => _headerCount;
    internal PooledValueTaskSource<RecordMetadata>? CompletionSource => _completionSource;
    internal Action<RecordMetadata, Exception?>? Callback => _callback;
    internal int RecordSize => _recordSize;

    public PendingAppend()
    {
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
        _timestamp = timestamp;
        _key = key;
        _value = value;
        _headers = headers;
        _headerCount = headerCount;
        _completionSource = completionSource;
        _callback = callback;
        _recordSize = recordSize;
        _startTicks = startTicks;
        _accumulator = accumulator;
        _pool = pool;

        // Arm timeout timer. Compute remaining ms from deadline.
        var remainingMs = deadlineTickCount - Environment.TickCount64;
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
    /// <see cref="RecordAccumulator.AppendAfterReservation"/> to prevent timeout/cancel
    /// from cleaning up resources while the drain is using them.
    /// </summary>
    /// <returns>True if this call won the race; false if timeout/cancel/dispose already completed it.</returns>
    public bool TryClaim()
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return false;

        DisarmTimerAndCancellation();
        return true;
    }

    /// <summary>
    /// Sets the successful result after <see cref="TryClaim"/> + AppendAfterReservation.
    /// Must only be called after <see cref="TryClaim"/> returned true.
    /// Resources are consumed by AppendAfterReservation — no cleanup needed.
    /// </summary>
    public void CompleteResult(bool result) => _core.SetResult(result);

    /// <summary>
    /// Sets an exception result after <see cref="TryClaim"/> + failed AppendAfterReservation.
    /// Must only be called after <see cref="TryClaim"/> returned true.
    /// AppendAfterReservation handles its own resource cleanup on throw.
    /// </summary>
    public void CompleteException(Exception exception) => _core.SetException(exception);

    /// <summary>
    /// Attempts to fail the operation with an exception (timeout, cancellation, disposal).
    /// Cleans up owned resources (PooledMemory, headers, pending produce count) since
    /// drain will not process this operation.
    /// </summary>
    /// <param name="exception">The exception to complete with.</param>
    /// <returns>True if this call won the completion race; false if drain already claimed it.</returns>
    public bool TryFail(Exception exception)
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return false;

        DisarmTimerAndCancellation();

        // Clean up owned resources since drain will not process this operation
        _key.Return();
        _value.Return();
        RecordAccumulator.ReturnPooledHeadersInternal(_headers);

        if (_completionSource is not null)
            _accumulator.DecrementPendingAwaitedProduceCount();

        _core.SetException(exception);
        return true;
    }

    private void OnTimeout()
    {
        var configured = TimeSpan.FromMilliseconds(_accumulator.MaxBlockMsOption);
        var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - _startTicks);

        var exception = new KafkaTimeoutException(
            TimeoutKind.MaxBlock,
            elapsed,
            configured,
            $"Failed to allocate buffer within max.block.ms ({_accumulator.MaxBlockMsOption}ms). " +
            $"Requested {_recordSize} bytes, current usage: {_accumulator.BufferedBytes}/{_accumulator.MaxBufferMemory} bytes. " +
            $"Producer is generating messages faster than the network can send them. " +
            $"Consider: increasing BufferMemory, increasing MaxBlockMs, reducing production rate, or checking network connectivity.");

        TryFail(exception);
    }

    private void OnCancellation()
    {
        TryFail(new OperationCanceledException());
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
            // Clear references before returning to pool to avoid rooting objects
            _topic = null!;
            _key = default;
            _value = default;
            _headers = null;
            _completionSource = null;
            _callback = null;
            _accumulator = null!;

            // Reset for reuse
            Volatile.Write(ref _completed, 0);
            _core.Reset();
            _pool.Return(this);
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
