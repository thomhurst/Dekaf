using System.Threading.Tasks.Sources;

namespace Dekaf.Consumer;

/// <summary>
/// Consumer-wide limits shared by every lane of one partitioned run. Created once per run;
/// a run without consumer-wide limits has no instance and pays only null checks.
/// </summary>
internal sealed class PartitionedProcessingLimits
{
    public PartitionedProcessingLimits(PartitionedBufferBudget? buffer, HandlerSlotLimiter? handlers)
    {
        Buffer = buffer;
        Handlers = handlers;
    }

    public PartitionedBufferBudget? Buffer { get; }

    public HandlerSlotLimiter? Handlers { get; }
}

/// <summary>
/// Counts records the runtime has queued for any lane and no handler has taken yet.
/// Reservation and release are one compare-and-swap or <see cref="Interlocked"/> add each.
/// </summary>
internal sealed class PartitionedBufferBudget
{
    private readonly int _max;
    private readonly int _resumeThreshold;
    private readonly Action _capacityAvailable;
    private readonly Action _resumeAvailable;
    private int _count;
    private int _resumeRequested;

    /// <param name="max">The maximum records counted at once.</param>
    /// <param name="capacityAvailable">Called when a release makes a full budget usable again.</param>
    /// <param name="resumeAvailable">
    /// Called once per <see cref="RequestResume"/> when the count falls to half the maximum, so
    /// partitions paused for the budget resume together instead of once per released record.
    /// </param>
    public PartitionedBufferBudget(int max, Action capacityAvailable, Action resumeAvailable)
    {
        _max = max;
        _resumeThreshold = max / 2;
        _capacityAvailable = capacityAvailable;
        _resumeAvailable = resumeAvailable;
    }

    public int Count => Volatile.Read(ref _count);

    public bool IsFull => Volatile.Read(ref _count) >= _max;

    public bool TryReserve()
    {
        var current = Volatile.Read(ref _count);
        while (current < _max)
        {
            var observed = Interlocked.CompareExchange(ref _count, current + 1, current);
            if (observed == current)
                return true;
            current = observed;
        }

        return false;
    }

    public void Release(int count = 1)
    {
        if (count <= 0)
            return;

        var remaining = Interlocked.Add(ref _count, -count);
        var previous = remaining + count;
        if (previous >= _max && remaining < _max)
            _capacityAvailable();

        if (previous > _resumeThreshold && remaining <= _resumeThreshold)
            TryResume();
    }

    /// <summary>
    /// Asks for one <c>resumeAvailable</c> callback once the count reaches the resume threshold.
    /// Called after pausing a partition, so the request cannot miss a release that already happened.
    /// </summary>
    public void RequestResume()
    {
        Interlocked.Exchange(ref _resumeRequested, 1);
        if (Volatile.Read(ref _count) <= _resumeThreshold)
            TryResume();
    }

    private void TryResume()
    {
        if (Volatile.Read(ref _resumeRequested) != 0 && Interlocked.Exchange(ref _resumeRequested, 0) != 0)
            _resumeAvailable();
    }
}

/// <summary>
/// A counting limiter for handler invocations across every lane. Waiters are served first in,
/// first out, and each lane queues at most one waiter, so a busy partition cannot take every
/// slot from the others. Acquiring an available slot while nobody waits is one compare-and-swap;
/// the lock is taken only to queue or serve waiters.
/// </summary>
internal sealed class HandlerSlotLimiter
{
    private readonly object _gate = new();
    private readonly Queue<HandlerSlotWaiter> _waiters = new();
    private int _available;
    private int _queued;

    public HandlerSlotLimiter(int maxConcurrentHandlers)
    {
        _available = maxConcurrentHandlers;
    }

    internal int Available => Volatile.Read(ref _available);

    public bool TryAcquire()
    {
        // Queued waiters go first, so a new caller does not overtake them.
        if (Volatile.Read(ref _queued) != 0)
            return false;

        return TryTakeAvailable();
    }

    /// <summary>
    /// Takes a slot for <paramref name="waiter"/> now, or queues it and returns false. A queued
    /// waiter is granted through <see cref="HandlerSlotWaiter.OnGranted"/>.
    /// </summary>
    public bool AcquireOrEnqueue(HandlerSlotWaiter waiter)
    {
        lock (_gate)
        {
            RemoveCancelledHead();
            if (_waiters.Count == 0 && TryTakeAvailable())
                return true;

            Volatile.Write(ref waiter.State, HandlerSlotWaiter.Waiting);
            _waiters.Enqueue(waiter);
            Interlocked.Increment(ref _queued);
        }

        // A release can complete between the failed take and the queued count becoming visible.
        // That release saw no waiter, so serve the queue here.
        if (Volatile.Read(ref _available) > 0)
            ServeWaiters();

        return false;
    }

    public void Release()
    {
        Interlocked.Increment(ref _available);
        if (Volatile.Read(ref _queued) != 0)
            ServeWaiters();
    }

    private void ServeWaiters()
    {
        HandlerSlotWaiter? granted;
        while (true)
        {
            granted = null;
            lock (_gate)
            {
                RemoveCancelledHead();
                if (_waiters.Count == 0 || !TryTakeAvailable())
                    return;

                var waiter = _waiters.Dequeue();
                Interlocked.Decrement(ref _queued);
                if (Interlocked.CompareExchange(ref waiter.State, HandlerSlotWaiter.Granted, HandlerSlotWaiter.Waiting)
                    == HandlerSlotWaiter.Waiting)
                {
                    granted = waiter;
                }
                else
                {
                    // Cancelled after RemoveCancelledHead looked at it: return the slot.
                    Interlocked.Increment(ref _available);
                }
            }

            // Grant outside the lock: the waiter may resume its owner inline.
            granted?.OnGranted();
        }
    }

    private void RemoveCancelledHead()
    {
        while (_waiters.Count != 0 && Volatile.Read(ref _waiters.Peek().State) == HandlerSlotWaiter.Cancelled)
        {
            _waiters.Dequeue();
            Interlocked.Decrement(ref _queued);
        }
    }

    private bool TryTakeAvailable()
    {
        var current = Volatile.Read(ref _available);
        while (current > 0)
        {
            var observed = Interlocked.CompareExchange(ref _available, current - 1, current);
            if (observed == current)
                return true;
            current = observed;
        }

        return false;
    }
}

/// <summary>
/// One queued request for a handler slot. Each lane owns one and reuses it, so waiting allocates
/// nothing per handler. A cancelled waiter stays cancelled; its owner is stopping.
/// </summary>
internal abstract class HandlerSlotWaiter
{
    internal const int Idle = 0;
    internal const int Waiting = 1;
    internal const int Granted = 2;
    internal const int Cancelled = 3;

    internal int State;

    /// <summary>Called once, outside the limiter's lock, after the limiter handed this waiter a slot.</summary>
    internal abstract void OnGranted();

    /// <summary>
    /// Stops waiting. Returns true when the waiter held a granted slot it had not used, which the
    /// caller must release.
    /// </summary>
    internal bool Cancel()
    {
        var state = Interlocked.CompareExchange(ref State, Cancelled, Waiting);
        if (state == Waiting)
            return false;

        return state == Granted && Interlocked.CompareExchange(ref State, Cancelled, Granted) == Granted;
    }
}

/// <summary>
/// A reusable awaitable waiter for one lane's handler loop.
/// </summary>
internal sealed class AsyncHandlerSlotWaiter : HandlerSlotWaiter, IValueTaskSource
{
    private readonly HandlerSlotLimiter _limiter;
    private ManualResetValueTaskSourceCore<bool> _core;
    private CancellationTokenRegistration _registration;
    private CancellationToken _cancellationToken;

    public AsyncHandlerSlotWaiter(HandlerSlotLimiter limiter)
    {
        _limiter = limiter;
        // A grant comes from another lane's handler completion; do not run this lane's handler there.
        _core.RunContinuationsAsynchronously = true;
    }

    public ValueTask AcquireAsync(CancellationToken cancellationToken)
    {
        if (_limiter.TryAcquire())
            return default;

        return WaitAsync(cancellationToken);
    }

    /// <summary>Returns the slot taken by the last completed <see cref="AcquireAsync"/>.</summary>
    public void Release() => _limiter.Release();

    private ValueTask WaitAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref State) == Cancelled || cancellationToken.IsCancellationRequested)
            return new ValueTask(Task.FromCanceled(cancellationToken.IsCancellationRequested ? cancellationToken : new CancellationToken(true)));

        _core.Reset();
        _cancellationToken = cancellationToken;
        if (_limiter.AcquireOrEnqueue(this))
        {
            Volatile.Write(ref State, Idle);
            return default;
        }

        if (cancellationToken.CanBeCanceled)
        {
            _registration = cancellationToken.UnsafeRegister(
                static state => ((AsyncHandlerSlotWaiter)state!).OnCancelled(), this);
        }

        return new ValueTask(this, _core.Version);
    }

    internal override void OnGranted() => _core.SetResult(true);

    private void OnCancelled()
    {
        if (Interlocked.CompareExchange(ref State, Cancelled, Waiting) == Waiting)
            _core.SetException(new OperationCanceledException(_cancellationToken));
    }

    void IValueTaskSource.GetResult(short token)
    {
        _registration.Dispose();
        _registration = default;
        _core.GetResult(token);
        // Granted: the slot now belongs to the caller.
        Volatile.Write(ref State, Idle);
    }

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => _core.GetStatus(token);

    void IValueTaskSource.OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}
