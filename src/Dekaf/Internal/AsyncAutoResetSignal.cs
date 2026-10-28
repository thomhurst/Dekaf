using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace Dekaf.Internal;

/// <summary>
/// A zero-allocation async auto-reset signal for single-waiter, multi-signaler scenarios.
/// Wraps <see cref="ManualResetValueTaskSourceCore{TResult}"/> to provide an awaitable
/// signal that resets automatically after each wait completes.
///
/// After warmup, <see cref="WaitAsync"/> allocates nothing:
/// the timer is reused via <see cref="Timer.Change(int, int)"/>, and the cancellation
/// registration is set up once via <see cref="RegisterShutdownToken"/>.
///
/// Thread safety: <see cref="Signal"/> may be called from any thread concurrently.
/// <see cref="WaitAsync"/> must only be called by a single consumer
/// (not thread-safe for multiple concurrent waiters).
/// </summary>
internal sealed class AsyncAutoResetSignal : IValueTaskSource<bool>,
#if NET
    IThreadPoolWorkItem,
#endif
    IDisposable
{
    private const int QueuedSignal = 1;
    private const int QueuedShutdown = 2;

    private ManualResetValueTaskSourceCore<bool> _core;
    private readonly bool _inlineTimeoutContinuations;
    private int _queuedCompletion;

    /// <summary>
    /// The low two bits hold the kind: 0 = idle (no waiter), 1 = waiting (waiter registered),
    /// 2 = signaled-before-wait. The remaining bits hold a wait generation that every
    /// idle -> waiting transition advances, so a compare-and-swap against an observed
    /// waiting state cannot complete a later wait.
    /// Transitions:
    ///   idle -> waiting     (WaitAsync when no pending signal; advances the generation)
    ///   idle -> signaled    (Signal when no waiter)
    ///   waiting -> idle     (Signal, timeout or shutdown completes the waiter)
    ///   signaled -> idle    (WaitAsync consumes the pending signal)
    /// </summary>
    private int _state; // written via Interlocked, or by the single waiter when only it can change the state

    private const int Idle = 0;
    private const int Waiting = 1;
    private const int Signaled = 2;
    private const int KindMask = 3;
    private const int GenerationIncrement = 4;

    /// <summary>
    /// <see cref="Stopwatch"/> timestamp at which the current wait times out, or
    /// <see cref="NoDeadline"/> for an infinite wait. The waiter writes it before it
    /// publishes the waiting state and does not change it until that wait completes.
    /// </summary>
    private long _timeoutDeadline = NoDeadline;
    private const long NoDeadline = long.MaxValue;

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
    /// Cancellation registration for shutdown signaling. Set once via
    /// <see cref="RegisterShutdownToken"/> and disposed with the signal.
    /// </summary>
    private CancellationTokenRegistration _shutdownRegistration;
    private int _shutdownRegistered; // 0 = not registered, 1 = registered — guarded by Interlocked
    private int _shutdownRequested; // 0 = running, 1 = shutdown requested — terminal once set
    private CancellationToken _shutdownToken;

    private readonly Action? _afterSignalObservedWaiterForTesting;
    private readonly Action? _beforeTimeoutCompletionForTesting;
    private readonly Action? _afterTimeoutCallbackForTesting;

    public AsyncAutoResetSignal(bool inlineTimeoutContinuations = false, bool inlineContinuations = false)
        : this(
            inlineTimeoutContinuations,
            inlineContinuations,
            afterSignalObservedWaiterForTesting: null,
            beforeTimeoutCompletionForTesting: null,
            afterTimeoutCallbackForTesting: null)
    {
    }

    internal AsyncAutoResetSignal(
        bool inlineTimeoutContinuations,
        bool inlineContinuations,
        Action? afterSignalObservedWaiterForTesting,
        Action? beforeTimeoutCompletionForTesting,
        Action? afterTimeoutCallbackForTesting)
    {
        _inlineTimeoutContinuations = inlineTimeoutContinuations;
        _core.RunContinuationsAsynchronously = !inlineTimeoutContinuations && !inlineContinuations;
        _afterSignalObservedWaiterForTesting = afterSignalObservedWaiterForTesting;
        _beforeTimeoutCompletionForTesting = beforeTimeoutCompletionForTesting;
        _afterTimeoutCallbackForTesting = afterTimeoutCallbackForTesting;
        _timeoutCallback = static state => ((AsyncAutoResetSignal)state!).OnTimeout();
    }

    /// <summary>
    /// Registers a cancellation token for shutdown signaling. The registration persists
    /// across multiple <see cref="WaitAsync"/> calls — it is set up once and disposed
    /// with the signal. This avoids per-wait registration allocations.
    /// </summary>
    /// <remarks>
    /// Must be called before the first <see cref="WaitAsync"/> call if cancellation
    /// support is needed. The token should be the long-lived shutdown token, not a
    /// per-operation timeout token. Thread-safe: multiple calls are idempotent.
    /// </remarks>
    public void RegisterShutdownToken(CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
            return;

        // Atomic guard: only the first caller registers.
        if (Interlocked.CompareExchange(ref _shutdownRegistered, 1, 0) != 0)
            return;

        _shutdownToken = cancellationToken;
        if (cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref _shutdownRequested, 1);
            return;
        }

        _shutdownRegistration = cancellationToken.UnsafeRegister(
            static state =>
            {
                var self = (AsyncAutoResetSignal)state!;
                Volatile.Write(ref self._shutdownRequested, 1);
                // Shutdown requested — complete the waiter if one is pending. A wait
                // registered after this read rechecks _shutdownRequested itself.
                var observed = Volatile.Read(ref self._state);
                if ((observed & KindMask) == Waiting && self.TryTakeWaiter(observed))
                {
                    self.CompleteShutdown();
                }
            },
            this);
    }

    /// <summary>
    /// Signals the waiter. If a waiter is pending, completes it. If no waiter,
    /// stores the signal so the next <see cref="WaitAsync"/> returns immediately.
    /// Multiple signals without a wait coalesce into one (auto-reset semantics).
    /// </summary>
    public void Signal()
    {
        while (true)
        {
            var observed = Volatile.Read(ref _state);
            switch (observed & KindMask)
            {
                case Signaled:
                    // Signals coalesce.
                    return;
                case Idle:
                    // Store the signal for the next WaitAsync.
                    if (Interlocked.CompareExchange(ref _state, observed | Signaled, observed) == observed)
                        return;
                    break;
                default:
                    _afterSignalObservedWaiterForTesting?.Invoke();
                    // A CAS (not Exchange) guards against the timer or shutdown callback
                    // having completed the waiter already: double completion of
                    // ManualResetValueTaskSourceCore is undefined behavior. When that
                    // callback wins, retry, so the signal is stored for the next wait
                    // instead of being dropped.
                    if (TryTakeWaiter(observed))
                    {
                        CompleteSignal();
                        return;
                    }

                    break;
            }
        }
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
    /// <remarks>
    /// Zero-allocation in steady state: the timer is reused via <see cref="Timer.Change(int, int)"/>,
    /// and no cancellation registration is created per call.
    /// Single-waiter only — must not be called concurrently from multiple threads.
    /// </remarks>
    public ValueTask<bool> WaitAsync(int timeoutMs)
    {
        ThrowIfShutdownRequested();

        // Fast path: if already signaled, consume it immediately (no allocation).
        // Only the waiter leaves the signaled state, so no CAS is needed.
        var idle = Volatile.Read(ref _state);
        if ((idle & KindMask) == Signaled)
        {
            Volatile.Write(ref _state, idle & ~KindMask);
            return new ValueTask<bool>(true);
        }

        // Zero-timeout = immediate check only (already handled above)
        if (timeoutMs == 0)
            return new ValueTask<bool>(false);

        // Reset the core for a new wait cycle.
        _core.Reset();

        // Publish the deadline before the waiting state, so a timer callback that observes
        // this wait also observes its deadline.
        Volatile.Write(
            ref _timeoutDeadline,
            timeoutMs == Timeout.Infinite
                ? NoDeadline
                : Stopwatch.GetTimestamp() + timeoutMs * Stopwatch.Frequency / 1000);

        // Transition: idle -> waiting. Must happen BEFORE arming the timer.
        // If the timer were armed first, it could fire before the CAS, find no waiter,
        // and _core would never be completed (hang).
        var waiting = unchecked(idle + GenerationIncrement) | Waiting;
        var previous = Interlocked.CompareExchange(ref _state, waiting, idle);
        if (previous != idle)
        {
            // Only Signal changes an idle state: a signal arrived between the first
            // check and setting Waiting. Consume it.
            Volatile.Write(ref _state, previous & ~KindMask);
            return new ValueTask<bool>(true);
        }

        // Shutdown can race the loop's cancellation check and this wait registration.
        // The callback latches cancellation even when no waiter exists; this recheck
        // completes a waiter registered after such an idle callback.
        if (Volatile.Read(ref _shutdownRequested) != 0 && TryTakeWaiter(waiting))
        {
            _core.SetException(new OperationCanceledException(_shutdownToken));
        }

        // Now in Waiting state — safe to arm timer. If timer fires immediately,
        // it observes this wait and its deadline and completes it with false.
        // If Signal() already completed _core, the timer callback finds no waiter.
        if (timeoutMs != Timeout.Infinite)
        {
            // Created disarmed, so the field is set before the first callback can read it.
            _timeoutTimer ??= new Timer(_timeoutCallback, this, Timeout.Infinite, Timeout.Infinite);
            _timeoutTimer.Change(timeoutMs, Timeout.Infinite);
        }

        // Return the awaitable — Signal(), timeout, or shutdown will complete _core.
        return new ValueTask<bool>(this, _core.Version);
    }

    private bool TryTakeWaiter(int waiting)
        => Interlocked.CompareExchange(ref _state, waiting & ~KindMask, waiting) == waiting;

    /// <summary>
    /// Timer callback. Disarming the timer cannot stop a callback that is already running,
    /// so a callback armed for an earlier wait can run during a later one. The callback
    /// therefore completes only a wait whose own deadline has passed. A callback that
    /// runs before the current wait's deadline (stale, or early because the timer uses
    /// a coarser clock) re-arms the timer for that wait's remaining time.
    /// </summary>
    private void OnTimeout()
    {
        while (true)
        {
            var waiting = Volatile.Read(ref _state);
            if ((waiting & KindMask) != Waiting)
                break;

            var deadline = Volatile.Read(ref _timeoutDeadline);
            if (Volatile.Read(ref _state) != waiting)
                continue; // The deadline can belong to a newer wait; read both again.

            if (deadline == NoDeadline)
                break;

            var remaining = deadline - Stopwatch.GetTimestamp();
            if (remaining <= 0)
            {
                _beforeTimeoutCompletionForTesting?.Invoke();
                if (TryTakeWaiter(waiting))
                {
                    _core.SetResult(false);
                    break;
                }

                continue; // Signal or shutdown completed it; a newer wait can need the timer.
            }

            try
            {
                _timeoutTimer!.Change(ToTimerMilliseconds(remaining), Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            // This Change can overwrite the arming of a wait that started after the
            // state read; loop to re-arm the timer for that wait.
            if (Volatile.Read(ref _state) == waiting)
                break;
        }

        _afterTimeoutCallbackForTesting?.Invoke();
    }

    private static int ToTimerMilliseconds(long stopwatchTicks)
    {
        var milliseconds = (stopwatchTicks * 1000 + Stopwatch.Frequency - 1) / Stopwatch.Frequency;
        return (int)Math.Min(Math.Max(milliseconds, 1), int.MaxValue - 1);
    }

    private void ThrowIfShutdownRequested()
    {
        if (Volatile.Read(ref _shutdownRequested) != 0)
            throw new OperationCanceledException(_shutdownToken);
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

    private void CompleteSignal()
    {
        if (!_inlineTimeoutContinuations)
        {
            _core.SetResult(true);
            return;
        }

        Volatile.Write(ref _queuedCompletion, QueuedSignal);
        QueueCompletion();
    }

    private void CompleteShutdown()
    {
        if (!_inlineTimeoutContinuations)
        {
            _core.SetException(new OperationCanceledException(_shutdownToken));
            return;
        }

        Volatile.Write(ref _queuedCompletion, QueuedShutdown);
        QueueCompletion();
    }

#if NET
    void IThreadPoolWorkItem.Execute()
#else
    private void ExecuteQueuedCompletion()
#endif
    {
        var completion = Volatile.Read(ref _queuedCompletion);
        if (completion == QueuedSignal)
            _core.SetResult(true);
        else
            _core.SetException(new OperationCanceledException(_shutdownToken));
    }

    private void QueueCompletion()
    {
#if NET
        ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: false);
#else
        ThreadPool.UnsafeQueueUserWorkItem(static state =>
            ((AsyncAutoResetSignal)state!).ExecuteQueuedCompletion(), this);
#endif
    }

    public void Dispose()
    {
        _shutdownRegistration.Dispose();
        _timeoutTimer?.Dispose();
    }
}
