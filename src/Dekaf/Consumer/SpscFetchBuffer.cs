using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dekaf.Consumer;

/// <summary>
/// Single-producer single-consumer bounded ring buffer for PendingFetchData transfer
/// between the prefetch thread and the consumer thread. Replaces Channel&lt;PendingFetchData&gt;
/// with lower overhead: no ValueTask wrappers, no async continuations, cache-line-padded
/// head/tail to eliminate false sharing.
/// </summary>
internal sealed class SpscFetchBuffer
{
    private readonly PendingFetchData?[] _buffer;
    private readonly int _mask;

    private PaddedIndex _head;
    private PaddedIndex _tail;

    private readonly ManualResetEventSlim _dataAvailable = new(false);
    private volatile bool _consumerWaiting;
    private volatile bool _completed;
    private Exception? _completionError;

    public SpscFetchBuffer(int capacity)
    {
        var size = 1;
        while (size < capacity) size <<= 1;
        _buffer = new PendingFetchData?[size];
        _mask = size - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWrite(PendingFetchData item)
    {
        var head = Volatile.Read(ref _head.Value);
        var tail = Volatile.Read(ref _tail.Value);

        if (head - tail >= _buffer.Length)
            return false;

        _buffer[head & _mask] = item;
        Volatile.Write(ref _head.Value, head + 1);
        if (_consumerWaiting)
            _dataAvailable.Set();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(out PendingFetchData? item)
    {
        var tail = Volatile.Read(ref _tail.Value);
        var head = Volatile.Read(ref _head.Value);

        if (tail >= head)
        {
            item = null;
            return false;
        }

        item = _buffer[tail & _mask];
        _buffer[tail & _mask] = null;
        Volatile.Write(ref _tail.Value, tail + 1);
        return true;
    }

    public bool WaitToRead(int timeoutMs, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _head.Value) > Volatile.Read(ref _tail.Value))
            return true;

        if (_completed)
        {
            if (_completionError is not null)
                throw _completionError;
            return Volatile.Read(ref _head.Value) > Volatile.Read(ref _tail.Value);
        }

        _dataAvailable.Reset();
        _consumerWaiting = true;

        try
        {
            if (Volatile.Read(ref _head.Value) > Volatile.Read(ref _tail.Value))
                return true;

            if (_completed)
            {
                if (_completionError is not null)
                    throw _completionError;
                return Volatile.Read(ref _head.Value) > Volatile.Read(ref _tail.Value);
            }

            _dataAvailable.Wait(timeoutMs, cancellationToken);

            if (_completionError is not null)
                throw _completionError;

            return Volatile.Read(ref _head.Value) > Volatile.Read(ref _tail.Value);
        }
        finally
        {
            _consumerWaiting = false;
        }
    }

    public void Complete(Exception? error = null)
    {
        _completionError = error;
        _completed = true;
        _dataAvailable.Set();
    }

    public bool IsCompleted => _completed;

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct PaddedIndex
    {
        [FieldOffset(0)]
        public long Value;
    }
}
