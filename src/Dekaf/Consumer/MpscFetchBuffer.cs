using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;

namespace Dekaf.Consumer;

/// <summary>
/// Multi-producer single-consumer bounded ring buffer for PendingFetchData transfer
/// between prefetch threads and the consumer thread. Replaces Channel&lt;PendingFetchData&gt;
/// with lower overhead: no ValueTask wrappers, no async continuations, cache-line-padded
/// indices to eliminate false sharing.
///
/// Multiple prefetch tasks (one per broker) write concurrently via <see cref="TryWrite"/>,
/// while the single consumer thread reads via <see cref="TryRead"/>.
/// </summary>
internal sealed class MpscFetchBuffer
{
    private readonly PendingFetchData?[] _buffer;
    private readonly long[] _committedSequences;
    private readonly int _mask;

    private PaddedIndex _headReserved;
    private PaddedIndex _tail;

    private readonly ManualResetEventSlim _dataAvailable = new(false);
    private readonly SemaphoreSlim _spaceAvailable = new(0, int.MaxValue);
    private readonly object _dataAvailableWaiterLock = new();
    private readonly ReadWaiter _readWaiter;
    private readonly Action? _afterProducerWaiterCountIncrementedForTesting;
    private readonly Action? _beforeConsumerWaitSpinForTesting;
    private readonly Action<long>? _afterProducerSlotReservedForTesting;
    private readonly Action? _beforeConsumerTimeoutForTesting;
    private readonly Action? _afterConsumerTimeoutForTesting;
    private readonly Action? _beforeConsumerTimeoutCompletionForTesting;
    private bool _readWaiterActive;
    private int _producerWaiterCount;
    private int _consumerWaiting;
    private volatile bool _completed;
    private volatile Exception? _completionError;

    public MpscFetchBuffer(int capacity)
        : this(
            capacity,
            afterProducerWaiterCountIncrementedForTesting: null,
            beforeConsumerWaitSpinForTesting: null,
            afterProducerSlotReservedForTesting: null,
            beforeConsumerTimeoutForTesting: null,
            afterConsumerTimeoutForTesting: null,
            beforeConsumerTimeoutCompletionForTesting: null)
    {
    }

    internal MpscFetchBuffer(
        int capacity,
        Action? afterProducerWaiterCountIncrementedForTesting,
        Action? beforeConsumerWaitSpinForTesting = null,
        Action<long>? afterProducerSlotReservedForTesting = null,
        Action? beforeConsumerTimeoutForTesting = null,
        Action? afterConsumerTimeoutForTesting = null,
        Action? beforeConsumerTimeoutCompletionForTesting = null)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        const int MaxPowerOfTwoCapacity = 1 << 30;
        if (capacity > MaxPowerOfTwoCapacity)
            throw new ArgumentOutOfRangeException(nameof(capacity), $"Capacity must be <= {MaxPowerOfTwoCapacity}.");

        // Round up to next power of 2 for mask-based indexing
        var size = 1;
        while (size < capacity) size <<= 1;
        _buffer = new PendingFetchData?[size];
        _committedSequences = new long[size];
        _mask = size - 1;
        _afterProducerWaiterCountIncrementedForTesting = afterProducerWaiterCountIncrementedForTesting;
        _beforeConsumerWaitSpinForTesting = beforeConsumerWaitSpinForTesting;
        _afterProducerSlotReservedForTesting = afterProducerSlotReservedForTesting;
        _beforeConsumerTimeoutForTesting = beforeConsumerTimeoutForTesting;
        _afterConsumerTimeoutForTesting = afterConsumerTimeoutForTesting;
        _beforeConsumerTimeoutCompletionForTesting = beforeConsumerTimeoutCompletionForTesting;
        _readWaiter = new ReadWaiter(this);
    }

    internal int Capacity => _buffer.Length;

    internal int Count
    {
        get
        {
            var head = Volatile.Read(ref _headReserved.Value);
            var tail = Volatile.Read(ref _tail.Value);
            return (int)Math.Clamp(head - tail, 0, _buffer.Length);
        }
    }

    internal int ProducerWaiterCount => Volatile.Read(ref _producerWaiterCount);

    /// <summary>
    /// Attempts to write an item. Safe for concurrent callers (multiple prefetch tasks).
    /// Uses CAS on the head reservation index, then publishes a per-slot sequence so the
    /// reader sees the item only after it is fully written. Later producers never wait for
    /// an earlier reserved slot to commit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWrite(PendingFetchData item)
    {
        while (true)
        {
            var head = Volatile.Read(ref _headReserved.Value);
            var tail = Volatile.Read(ref _tail.Value);

            if (head - tail >= _buffer.Length)
                return false; // Full

            if (Interlocked.CompareExchange(ref _headReserved.Value, head + 1, head) != head)
                continue;

            _afterProducerSlotReservedForTesting?.Invoke(head);

            var index = (int)(head & _mask);
            _buffer[index] = item;
            Volatile.Write(ref _committedSequences[index], head + 1);

            // StoreLoad fence: the item publication must precede observing the waiter flag,
            // otherwise a producer and consumer can both miss each other's publication.
            Interlocked.MemoryBarrier();

            // A later reservation may commit first, but the consumer can only read its current
            // tail. Let that tail producer own the wake so waits cannot complete spuriously.
            if (head == Volatile.Read(ref _tail.Value)
                && Volatile.Read(ref _consumerWaiting) != 0)
                SignalConsumerWaitingForData();

            return true;
        }
    }

    /// <summary>
    /// Waits asynchronously until space is available or cancellation is requested.
    /// Used by the prefetch path to avoid blocking a thread-pool thread.
    /// </summary>
    public async ValueTask WaitToWriteAsync(CancellationToken cancellationToken)
    {
        // Fast path: space already available
        if (HasSpaceAvailable())
            return;

        // Slow path: wait for the consumer to drain
        Interlocked.Increment(ref _producerWaiterCount);
        try
        {
            _afterProducerWaiterCountIncrementedForTesting?.Invoke();

            // Re-check after setting flag to avoid missed signal
            if (HasSpaceAvailable())
            {
                // Concurrent TryRead calls may have released permits for this waiter after
                // it incremented _producerWaiterCount but before it registered with
                // SemaphoreSlim. Drain every stale permit non-blockingly so future waiters
                // are not spuriously woken while the buffer is full.
                DrainAvailableSpaceSignals();
                return;
            }

            await _spaceAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _producerWaiterCount);
        }
    }

    /// <summary>
    /// Attempts to read an item. Called only from the single consumer thread.
    /// Reads only a slot whose sequence matches the next tail position, ensuring items are
    /// returned in reservation order after they are fully written.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead([NotNullWhen(true)] out PendingFetchData? item)
    {
        var tail = Volatile.Read(ref _tail.Value);
        var index = (int)(tail & _mask);

        if (Volatile.Read(ref _committedSequences[index]) != tail + 1)
        {
            item = null;
            return false; // Empty
        }

        item = _buffer[index]!;
        _buffer[index] = null;
        Volatile.Write(ref _tail.Value, tail + 1);

        // StoreLoad fence pairs slot release with the producer-waiter check, preventing a
        // producer from publishing its waiter count while this consumer misses it.
        Interlocked.MemoryBarrier();

        if (Volatile.Read(ref _producerWaiterCount) > 0)
            _spaceAvailable.Release();

        return true;
    }

    /// <summary>
    /// Waits until data is available or the buffer is completed.
    /// Returns false if the buffer is completed and empty.
    /// </summary>
    public bool WaitToRead(int timeoutMs, CancellationToken cancellationToken)
    {
        // Fast check: data already available?
        if (HasDataAvailable())
            return true;

        if (_completed)
        {
            if (_completionError is not null)
                throw _completionError;
            return HasDataAvailable();
        }

        // Slow path: wait for signal
        _dataAvailable.Reset();
        Volatile.Write(ref _consumerWaiting, 1);

        try
        {
            Interlocked.MemoryBarrier();

            // Re-check after reset to avoid missed signal
            if (HasDataAvailable())
                return true;

            if (_completed)
            {
                if (_completionError is not null)
                    throw _completionError;
                return HasDataAvailable();
            }

            _dataAvailable.Wait(timeoutMs, cancellationToken);

            if (_completionError is not null)
                throw _completionError;

            return HasDataAvailable();
        }
        finally
        {
            Volatile.Write(ref _consumerWaiting, 0);
        }
    }

    /// <summary>
    /// Waits asynchronously until data is available or the buffer is completed.
    /// Returns false if the wait times out or the buffer is completed and empty.
    /// </summary>
    public ValueTask<bool> WaitToReadAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        if (timeoutMs < Timeout.Infinite)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Timeout must be non-negative or Timeout.Infinite.");

        // Fast check: data already available?
        if (HasDataAvailable())
            return new ValueTask<bool>(true);

        if (_completed)
        {
            if (_completionError is not null)
                throw _completionError;
            return new ValueTask<bool>(HasDataAvailable());
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (timeoutMs == 0)
        {
            if (_completionError is not null)
                throw _completionError;
            return new ValueTask<bool>(HasDataAvailable());
        }

        // Catch near-simultaneous producer arrivals without publishing a TCS and forcing its
        // continuation through the ThreadPool. Stop before SpinWait would yield or sleep so the
        // optimization remains bounded and does not delay unrelated work on a busy host.
        _beforeConsumerWaitSpinForTesting?.Invoke();
        var spin = new SpinWait();
        while (!spin.NextSpinWillYield)
        {
            if (HasDataAvailable())
                return new ValueTask<bool>(true);

            spin.SpinOnce();
        }

        _readWaiter.SetCancellationToken(cancellationToken);
        Volatile.Write(ref _consumerWaiting, 1);
        Interlocked.MemoryBarrier();

        lock (_dataAvailableWaiterLock)
        {
            // Re-check after publishing the waiter flag to avoid missed signals.
            if (HasDataAvailable())
            {
                Volatile.Write(ref _consumerWaiting, 0);
                return new ValueTask<bool>(true);
            }

            if (_completed)
            {
                Volatile.Write(ref _consumerWaiting, 0);
                if (_completionError is not null)
                    throw _completionError;
                return new ValueTask<bool>(HasDataAvailable());
            }

            Debug.Assert(!_readWaiterActive, "MpscFetchBuffer supports one active reader.");
            _readWaiterActive = true;
            return _readWaiter.Prepare(timeoutMs);
        }
    }

    /// <summary>
    /// Signals that no more items will be written. Idempotent — only the first call
    /// takes effect, matching ChannelWriter.TryComplete semantics. This ensures that
    /// a fatal error passed via the first call is not overwritten by a subsequent
    /// null-error call (e.g., from a finally block).
    /// </summary>
    public void Complete(Exception? error = null)
    {
        if (_completed)
            return;

        _completionError = error;
        _completed = true;
        SignalConsumerWaitingForData();
    }

    public bool IsCompleted => _completed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool HasDataAvailable()
    {
        var tail = Volatile.Read(ref _tail.Value);
        return Volatile.Read(ref _committedSequences[tail & _mask]) == tail + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasSpaceAvailable() =>
        Volatile.Read(ref _headReserved.Value) - Volatile.Read(ref _tail.Value) < _buffer.Length;

    private void SignalConsumerWaitingForData()
    {
        _dataAvailable.Set();

        lock (_dataAvailableWaiterLock)
        {
            if (_readWaiterActive)
                _readWaiter.TrySetSignal();
        }
    }

    internal void Dispose()
    {
        _readWaiter.Dispose();
        _dataAvailable.Dispose();
        _spaceAvailable.Dispose();
    }

    private void DrainAvailableSpaceSignals()
    {
        while (_spaceAvailable.Wait(0, CancellationToken.None))
        {
        }
    }

    private sealed class ReadWaiter : IValueTaskSource<bool>, IDisposable
#if NET10_0_OR_GREATER
        , IThreadPoolWorkItem
#endif
    {
        private readonly MpscFetchBuffer _owner;
        private readonly TimerCallback _timeoutCallback;
        private ManualResetValueTaskSourceCore<bool> _core;
        private Timer? _timeoutTimer;
        private CancellationTokenRegistration _cancellationRegistration;
        private CancellationToken _cancellationToken;
        private long _timeoutDeadline;
        private bool _completed;
        private bool _queuedResult;
        private Exception? _queuedException;
        private int _cancellationRequested;
        private bool _disposed;

        public ReadWaiter(MpscFetchBuffer owner)
        {
            _owner = owner;
            _core.RunContinuationsAsynchronously = false;
            _timeoutCallback = static state => ((ReadWaiter)state!).OnTimeout();
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            // CancellationTokenSource.TryReset clears registrations without changing token
            // identity, so equality cannot prove that the previous registration is still live.
            _cancellationRegistration.Dispose();
            _cancellationToken = cancellationToken;
            Volatile.Write(ref _cancellationRequested, cancellationToken.IsCancellationRequested ? 1 : 0);
            _cancellationRegistration = cancellationToken.CanBeCanceled
                ? cancellationToken.UnsafeRegister(static state => ((ReadWaiter)state!).OnCancellation(), this)
                : default;
        }

        public ValueTask<bool> Prepare(int timeoutMs)
        {
            _core.Reset();
            _completed = false;

            if (Volatile.Read(ref _cancellationRequested) != 0)
            {
                TrySetException(new OperationCanceledException(_cancellationToken));
                return new ValueTask<bool>(this, _core.Version);
            }

            if (timeoutMs != Timeout.Infinite)
            {
                _timeoutDeadline = Stopwatch.GetTimestamp()
                    + timeoutMs * Stopwatch.Frequency / 1_000;
                if (_timeoutTimer is null)
                    _timeoutTimer = new Timer(_timeoutCallback, this, timeoutMs, Timeout.Infinite);
                else
                    _timeoutTimer.Change(timeoutMs, Timeout.Infinite);
            }
            else
            {
                _timeoutDeadline = long.MaxValue;
            }

            return new ValueTask<bool>(this, _core.Version);
        }

        public void TrySetSignal()
        {
            if (_completed)
                return;

            _completed = true;
            _queuedResult = true;
            _queuedException = null;
            QueueCompletion();
        }

        private void TrySetException(Exception exception)
        {
            if (_completed)
                return;

            _completed = true;
            _queuedResult = false;
            _queuedException = exception;
            QueueCompletion();
        }

        private void QueueCompletion()
        {
#if NET10_0_OR_GREATER
            if (!ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: false))
                ExecuteQueuedCompletion();
#else
            if (!ThreadPool.QueueUserWorkItem(static state => ((ReadWaiter)state!).ExecuteQueuedCompletion(), this))
                ExecuteQueuedCompletion();
#endif
        }

        private void ExecuteQueuedCompletion()
        {
            var exception = _queuedException;
            _queuedException = null;
            if (exception is not null)
                _core.SetException(exception);
            else
                _core.SetResult(_queuedResult);
        }

#if NET10_0_OR_GREATER
        void IThreadPoolWorkItem.Execute() => ExecuteQueuedCompletion();
#endif

        private void OnCancellation()
        {
            Volatile.Write(ref _cancellationRequested, 1);
            lock (_owner._dataAvailableWaiterLock)
            {
                if (_owner._readWaiterActive)
                    TrySetException(new OperationCanceledException(_cancellationToken));
            }
        }

        private void OnTimeout()
        {
            _owner._beforeConsumerTimeoutForTesting?.Invoke();
            try
            {
                lock (_owner._dataAvailableWaiterLock)
                {
                    if (!_owner._readWaiterActive || _completed || _timeoutDeadline == long.MaxValue)
                        return;

                    var remainingTicks = _timeoutDeadline - Stopwatch.GetTimestamp();
                    if (remainingTicks > 0)
                    {
                        var remainingMs = Math.Max(
                            1,
                            (int)((remainingTicks * 1_000 + Stopwatch.Frequency - 1) / Stopwatch.Frequency));
                        _timeoutTimer!.Change(remainingMs, Timeout.Infinite);
                        return;
                    }

                    _completed = true;
                    _queuedResult = false;
                    _queuedException = null;
                }

                // Completing inline while holding the owner lock can deadlock: the continuation
                // may dispose a linked CTS and wait for a cancellation callback that needs the
                // same lock. Publish completion only after releasing it.
                _owner._beforeConsumerTimeoutCompletionForTesting?.Invoke();
                QueueCompletion();
            }
            finally
            {
                _owner._afterConsumerTimeoutForTesting?.Invoke();
            }
        }

        bool IValueTaskSource<bool>.GetResult(short token)
        {
            try
            {
                var signaled = _core.GetResult(token);
                if (!signaled)
                    return false;

                if (_owner._completionError is not null)
                    throw _owner._completionError;

                return _owner.HasDataAvailable();
            }
            finally
            {
                lock (_owner._dataAvailableWaiterLock)
                {
                    _timeoutTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    _owner._readWaiterActive = false;
                }

                Volatile.Write(ref _owner._consumerWaiting, 0);
            }
        }

        ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => _core.GetStatus(token);

        void IValueTaskSource<bool>.OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags) =>
            _core.OnCompleted(continuation, state, token, flags);

        public void Dispose()
        {
            CancellationTokenRegistration cancellationRegistration;
            Timer? timeoutTimer;

            lock (_owner._dataAvailableWaiterLock)
            {
                if (_disposed)
                    return;

                _disposed = true;

                // Dispose may race the caller consuming the pending ValueTask. Complete it
                // before tearing down its only wake-up sources, then detach the timer so the
                // eventual GetResult cleanup cannot call Change on a disposed instance.
                if (_owner._readWaiterActive)
                    TrySetSignal();

                cancellationRegistration = _cancellationRegistration;
                _cancellationRegistration = default;
                timeoutTimer = _timeoutTimer;
                _timeoutTimer = null;
            }

            cancellationRegistration.Dispose();
            timeoutTimer?.Dispose();
        }
    }

    /// <summary>
    /// Cache-line-padded index to prevent false sharing between producer and consumer.
    /// 128 bytes covers two 64-byte cache lines, preventing false sharing from
    /// Intel's spatial prefetcher which fetches adjacent line pairs.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct PaddedIndex
    {
        [FieldOffset(0)]
        public long Value;
    }
}
