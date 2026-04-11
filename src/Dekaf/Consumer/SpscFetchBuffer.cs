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
internal sealed class SpscFetchBuffer
{
    private readonly PendingFetchData?[] _buffer;
    private readonly int _mask;

    // _headReserved: CAS-incremented by producers to reserve a slot.
    // _headCommitted: Volatile-incremented by producers after storing the item.
    //                 The reader only sees slots up to _headCommitted.
    // _tail: only modified by the single consumer thread.
    // Each index occupies its own 128-byte padded struct to prevent false sharing
    // across cache line pairs (128 bytes covers Intel spatial prefetcher).
    private PaddedIndex _headReserved;
    private PaddedIndex _headCommitted;
    private PaddedIndex _tail;

    private readonly ManualResetEventSlim _dataAvailable = new(false);
    private readonly SemaphoreSlim _spaceAvailable = new(0, 1);
    private volatile bool _consumerWaiting;
    private volatile bool _completed;
    private volatile Exception? _completionError;

    public SpscFetchBuffer(int capacity)
    {
        // Round up to next power of 2 for mask-based indexing
        var size = 1;
        while (size < capacity) size <<= 1;
        _buffer = new PendingFetchData?[size];
        _mask = size - 1;
    }

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

            // Reserve this slot via CAS — another producer may beat us
            if (Interlocked.CompareExchange(ref _headReserved.Value, head + 1, head) != head)
                continue; // Lost race, retry

            // Slot reserved — store the item
            _buffer[head & _mask] = item;

            // Advance committed index. Must wait for all prior slots to commit first
            // (producers that reserved earlier slots must finish storing before we advance).
            // SpinWait ensures producers commit in reservation order.
            var spin = new SpinWait();
            while (Volatile.Read(ref _headCommitted.Value) != head)
                spin.SpinOnce();

            Volatile.Write(ref _headCommitted.Value, head + 1);

            if (_consumerWaiting)
                _dataAvailable.Set();

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
        if (Volatile.Read(ref _headReserved.Value) - Volatile.Read(ref _tail.Value) < _buffer.Length)
            return;

        // Slow path: wait for the consumer to drain
        try
        {
            await _spaceAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
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
        _buffer[tail & _mask] = null; // Allow GC
        Volatile.Write(ref _tail.Value, tail + 1);

        // Signal producers that space is available
        if (_spaceAvailable.CurrentCount == 0)
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
        if (Volatile.Read(ref _headCommitted.Value) > Volatile.Read(ref _tail.Value))
            return true;

        if (_completed)
        {
            if (_completionError is not null)
                throw _completionError;
            return Volatile.Read(ref _headCommitted.Value) > Volatile.Read(ref _tail.Value);
        }

        // Slow path: wait for signal
        _dataAvailable.Reset();
        _consumerWaiting = true;

        try
        {
            // Re-check after reset to avoid missed signal
            if (Volatile.Read(ref _headCommitted.Value) > Volatile.Read(ref _tail.Value))
                return true;

            if (_completed)
            {
                if (_completionError is not null)
                    throw _completionError;
                return Volatile.Read(ref _headCommitted.Value) > Volatile.Read(ref _tail.Value);
            }

            _dataAvailable.Wait(timeoutMs, cancellationToken);

            if (_completionError is not null)
                throw _completionError;

            return Volatile.Read(ref _headCommitted.Value) > Volatile.Read(ref _tail.Value);
        }
        finally
        {
            _consumerWaiting = false;
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
        _dataAvailable.Set();
    }

    public bool IsCompleted => _completed;

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
