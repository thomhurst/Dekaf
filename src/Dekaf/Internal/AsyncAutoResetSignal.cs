using System.Threading.Tasks.Sources;

namespace Dekaf.Internal;

/// <summary>
/// A zero-allocation async auto-reset signal for single-waiter, multi-signaler scenarios.
/// Wraps <see cref="ManualResetValueTaskSourceCore{TResult}"/> to provide an awaitable
/// signal that resets automatically after each wait completes.
///
/// After warmup, <see cref="WaitAsync(int, CancellationToken)"/> allocates nothing:
/// the timer is reused via <see cref="Timer.Change(int, int)"/>, and the cancellation
/// registration is only set up when the token is actually cancellable (rare in steady-state).
///
/// Thread safety: <see cref="Signal"/> may be called from any thread concurrently.
/// <see cref="WaitAsync(int, CancellationToken)"/> must only be called by a single consumer
/// (not thread-safe for multiple concurrent waiters).
///
/// Used by <c>BrokerSender</c> to replace <see cref="SemaphoreSlim"/> for response
/// completion notification — eliminates per-wait TaskNode allocations that
/// SemaphoreSlim.WaitAsync(CancellationToken) creates internally.
/// </summary>
internal sealed class AsyncAutoResetSignal : IValueTaskSource<bool>, IDisposable
{
    private ManualResetValueTaskSourceCore<bool> _core;

    /// <summary>
    /// 0 = idle (no waiter), 1 = waiting (waiter registered), 2 = signaled-before-wait.
    /// Transitions:
    ///   idle -> waiting     (WaitAsync when no pending signal)
    ///   idle -> signaled    (Signal when no waiter)
    ///   waiting -> idle     (Signal completes the waiter)
    ///   signaled -> idle    (WaitAsync consumes the pending signal)
    /// </summary>
    private int _state; // 0=idle, 1=waiting, 2=signaled — all accesses via Interlocked

    private const int Idle = 0;
    private const int Waiting = 1;
    private const int Signaled = 2;

    /// <summary>
    /// Reusable timer for timeout support. Created lazily on first WaitAsync with timeout.
    /// Subsequent calls reuse it via <see cref="Timer.Change(int, int)"/> (zero allocation).
    /// </summary>
    private Timer? _timeoutTimer;

    /// <summary>
    /// Pre-allocated callback for the timeout timer. Stored as a field to avoid
    /// delegate allocation on each WaitAsync call.
    /// </summary>
    private readonly TimerCallback _timeoutCallback;

    /// <summary>
    /// Cancellation registration for shutdown signaling. Only set when the caller's
    /// cancellation token is actually cancellable (non-default). In the common
    /// producer hot path this is the outer shutdown token which rarely fires,
    /// and the registration is set up once per send loop start, not per wait.
    /// </summary>
    private CancellationTokenRegistration _shutdownRegistration;
    private volatile bool _shutdownRegistered;

    public AsyncAutoResetSignal()
    {
        _core.RunContinuationsAsynchronously = true;
        _timeoutCallback = static state =>
        {
            var self = (AsyncAutoResetSignal)state!;
            // Timeout fired — only complete if still waiting.
            if (Interlocked.CompareExchange(ref self._state, Idle, Waiting) == Waiting)
            {
                self._core.SetResult(false);
            }
        };
    }

    /// <summary>
    /// Registers a cancellation token for shutdown signaling. The registration persists
    /// across multiple <see cref="WaitAsync(int, CancellationToken)"/> calls — it is set up
    /// once and disposed with the signal. This avoids per-wait registration allocations.
    /// </summary>
    /// <remarks>
    /// Must be called before the first <see cref="WaitAsync(int, CancellationToken)"/> call
    /// if cancellation support is needed. The token should be the long-lived shutdown token,
    /// not a per-operation timeout token.
    /// </remarks>
    public void RegisterShutdownToken(CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || _shutdownRegistered)
            return;

        _shutdownRegistration = cancellationToken.UnsafeRegister(
            static state =>
            {
                var self = (AsyncAutoResetSignal)state!;
                // Shutdown requested — complete the waiter if one is pending.
                if (Interlocked.CompareExchange(ref self._state, Idle, Waiting) == Waiting)
                {
                    self._core.SetException(new OperationCanceledException());
                }
            },
            this);
        _shutdownRegistered = true;
    }

    /// <summary>
    /// Signals the waiter. If a waiter is pending, completes it. If no waiter,
    /// stores the signal so the next <see cref="WaitAsync(int, CancellationToken)"/> returns immediately.
    /// Multiple signals without a wait coalesce into one (auto-reset semantics).
    /// </summary>
    public void Signal()
    {
        // Try transition: idle -> signaled (store for next WaitAsync)
        // If already signaled, that's fine — signals coalesce.
        var previous = Interlocked.CompareExchange(ref _state, Signaled, Idle);
        if (previous == Idle || previous == Signaled)
            return;

        // previous == Waiting: a waiter is registered. Complete it.
        // Must use CAS (not Exchange) to guard against the timer or shutdown callback
        // having already transitioned Waiting → Idle and called SetResult/SetException.
        // Double-completion of ManualResetValueTaskSourceCore is undefined behavior.
        if (Interlocked.CompareExchange(ref _state, Idle, Waiting) == Waiting)
            _core.SetResult(true);
    }

    /// <summary>
    /// Waits for a signal with an optional timeout. Returns a <see cref="ValueTask{Boolean}"/>
    /// that completes with <c>true</c> when signaled, <c>false</c> on timeout, or throws
    /// <see cref="OperationCanceledException"/> when the shutdown token (registered via
    /// <see cref="RegisterShutdownToken"/>) fires.
    /// </summary>
    /// <param name="timeoutMs">
    /// Timeout in milliseconds. Use <see cref="Timeout.Infinite"/> for no timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// Not used for per-call registration — shutdown cancellation is handled via
    /// <see cref="RegisterShutdownToken"/>. This parameter exists for API consistency
    /// and is checked synchronously for already-cancelled tokens only.
    /// </param>
    /// <remarks>
    /// Zero-allocation in steady state: the timer is reused via <see cref="Timer.Change(int, int)"/>,
    /// and no cancellation registration is created per call.
    /// Single-waiter only — must not be called concurrently from multiple threads.
    /// </remarks>
    public ValueTask<bool> WaitAsync(int timeoutMs, CancellationToken cancellationToken = default)
    {
        // Fast path: if already signaled, consume it immediately (no allocation).
        var previous = Interlocked.CompareExchange(ref _state, Idle, Signaled);
        if (previous == Signaled)
            return new ValueTask<bool>(true);

        // Check cancellation before setting up the wait
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<bool>(cancellationToken);

        // Zero-timeout = immediate check only (already handled above)
        if (timeoutMs == 0)
            return new ValueTask<bool>(false);

        // Reset the core for a new wait cycle.
        _core.Reset();

        // Arm the timeout timer (reused — no allocation after first call).
        if (timeoutMs != Timeout.Infinite)
        {
            if (_timeoutTimer is null)
                _timeoutTimer = new Timer(_timeoutCallback, this, timeoutMs, Timeout.Infinite);
            else
                _timeoutTimer.Change(timeoutMs, Timeout.Infinite);
        }

        // Transition: idle -> waiting. Must happen AFTER Reset() + timer arm
        // to avoid Signal()/timeout completing _core before Reset().
        previous = Interlocked.CompareExchange(ref _state, Waiting, Idle);
        if (previous == Signaled)
        {
            // Signal arrived between our first check and setting Waiting.
            // Consume it — transition back to idle.
            Interlocked.Exchange(ref _state, Idle);
            DisarmTimer();
            return new ValueTask<bool>(true);
        }

        // Now waiting — Signal(), timeout, or shutdown will complete _core.
        return new ValueTask<bool>(this, _core.Version);
    }

    private void DisarmTimer()
    {
        _timeoutTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    bool IValueTaskSource<bool>.GetResult(short token)
    {
        // Disarm the timer to prevent it from firing after we've moved on.
        // Timer.Change is safe to call even if already fired.
        DisarmTimer();
        return _core.GetResult(token);
    }

    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
        => _core.GetStatus(token);

    void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);

    public void Dispose()
    {
        _shutdownRegistration.Dispose();
        _timeoutTimer?.Dispose();
    }
}
