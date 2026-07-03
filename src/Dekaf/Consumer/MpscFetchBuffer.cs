using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
    private readonly int _mask;

    private PaddedIndex _headReserved;
    private PaddedIndex _headCommitted;
    private PaddedIndex _tail;

    private readonly ManualResetEventSlim _dataAvailable = new(false);
    private readonly SemaphoreSlim _spaceAvailable = new(0, int.MaxValue);
    private readonly object _dataAvailableWaiterLock = new();
    private readonly Action? _afterProducerWaiterCountIncrementedForTesting;
    private TaskCompletionSource<bool>? _dataAvailableWaiter;
    private int _producerWaiterCount;
    private int _consumerWaiting;
    private volatile bool _completed;
    private volatile Exception? _completionError;

    public MpscFetchBuffer(int capacity)
        : this(capacity, afterProducerWaiterCountIncrementedForTesting: null)
    {
    }

    internal MpscFetchBuffer(int capacity, Action? afterProducerWaiterCountIncrementedForTesting)
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
        _mask = size - 1;
        _afterProducerWaiterCountIncrementedForTesting = afterProducerWaiterCountIncrementedForTesting;
    }

    internal int Capacity => _buffer.Length;

    internal int ProducerWaiterCount => Volatile.Read(ref _producerWaiterCount);

    /// <summary>
    /// Attempts to write an item. Safe for concurrent callers (multiple prefetch tasks).
    /// Uses CAS on the head reservation index, then stores the item and advances the
    /// committed index so the reader sees the item only after it is fully written.
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

            _buffer[head & _mask] = item;

            // Wait for prior slots to commit so the reader sees items in order
            var spin = new SpinWait();
            while (Volatile.Read(ref _headCommitted.Value) != head)
                spin.SpinOnce();

            Volatile.Write(ref _headCommitted.Value, head + 1);
            Interlocked.MemoryBarrier();

            if (Volatile.Read(ref _consumerWaiting) != 0)
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
    /// Only reads up to <c>_headCommitted</c> to ensure items are fully written.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead([NotNullWhen(true)] out PendingFetchData? item)
    {
        var tail = Volatile.Read(ref _tail.Value);
        var head = Volatile.Read(ref _headCommitted.Value);

        if (tail >= head)
        {
            item = null;
            return false; // Empty
        }

        item = _buffer[tail & _mask]!;
        _buffer[tail & _mask] = null;
        Volatile.Write(ref _tail.Value, tail + 1);
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
    public async ValueTask<bool> WaitToReadAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        if (timeoutMs < Timeout.Infinite)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Timeout must be non-negative or Timeout.Infinite.");

        // Fast check: data already available?
        if (HasDataAvailable())
            return true;

        if (_completed)
        {
            if (_completionError is not null)
                throw _completionError;
            return HasDataAvailable();
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (timeoutMs == 0)
        {
            if (_completionError is not null)
                throw _completionError;
            return HasDataAvailable();
        }

        TaskCompletionSource<bool>? waiter = null;
        Volatile.Write(ref _consumerWaiting, 1);

        try
        {
            Interlocked.MemoryBarrier();

            lock (_dataAvailableWaiterLock)
            {
                // Re-check after publishing the waiter flag to avoid missed signals.
                if (HasDataAvailable())
                    return true;

                if (_completed)
                {
                    if (_completionError is not null)
                        throw _completionError;
                    return HasDataAvailable();
                }

                if (_dataAvailableWaiter is null || _dataAvailableWaiter.Task.IsCompleted)
                {
                    _dataAvailableWaiter =
                        new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                waiter = _dataAvailableWaiter;
            }

            try
            {
                if (timeoutMs == Timeout.Infinite)
                {
                    await waiter.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await waiter.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (TimeoutException)
            {
                return false;
            }

            if (_completionError is not null)
                throw _completionError;

            return HasDataAvailable();
        }
        finally
        {
            if (waiter is not null)
            {
                lock (_dataAvailableWaiterLock)
                {
                    if (ReferenceEquals(_dataAvailableWaiter, waiter))
                        _dataAvailableWaiter = null;
                }
            }

            Volatile.Write(ref _consumerWaiting, 0);
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
    private bool HasDataAvailable() =>
        Volatile.Read(ref _headCommitted.Value) > Volatile.Read(ref _tail.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasSpaceAvailable() =>
        Volatile.Read(ref _headReserved.Value) - Volatile.Read(ref _tail.Value) < _buffer.Length;

    private void SignalConsumerWaitingForData()
    {
        _dataAvailable.Set();

        TaskCompletionSource<bool>? waiter;
        lock (_dataAvailableWaiterLock)
        {
            waiter = _dataAvailableWaiter;
        }

        waiter?.TrySetResult(true);
    }

    private void DrainAvailableSpaceSignals()
    {
        while (_spaceAvailable.Wait(0, CancellationToken.None))
        {
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
