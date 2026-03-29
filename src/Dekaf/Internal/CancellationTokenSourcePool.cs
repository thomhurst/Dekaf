// Based on dotnet/aspnetcore (MIT license)
// https://github.com/dotnet/aspnetcore/blob/main/src/Shared/CancellationTokenSourcePool.cs

using System.Collections.Concurrent;

namespace Dekaf.Internal;

internal sealed class CancellationTokenSourcePool
{
    private const int DefaultMaxQueueSize = 1024;

    private readonly int _maxQueueSize;
    private readonly ConcurrentQueue<PooledCancellationTokenSource> _queue = new();
    private int _count;

    public CancellationTokenSourcePool() : this(DefaultMaxQueueSize) { }

    public CancellationTokenSourcePool(int maxQueueSize)
    {
        _maxQueueSize = maxQueueSize;
    }

    public PooledCancellationTokenSource Rent()
    {
        if (_queue.TryDequeue(out var cts))
        {
            Interlocked.Decrement(ref _count);
            return cts;
        }
        return new PooledCancellationTokenSource(this);
    }

    private bool Return(PooledCancellationTokenSource cts)
    {
        if (Interlocked.Increment(ref _count) > _maxQueueSize || !cts.TryReset())
        {
            Interlocked.Decrement(ref _count);
            return false;
        }

        _queue.Enqueue(cts);
        return true;
    }

    public void Clear()
    {
        while (_queue.TryDequeue(out var cts))
        {
            Interlocked.Decrement(ref _count);
            cts.DisposeBase();
        }
    }

    /// <summary>
    /// A <see cref="CancellationTokenSource"/> with a back pointer to the pool it came from.
    /// Dispose will return it to the pool.
    /// </summary>
    public sealed class PooledCancellationTokenSource : CancellationTokenSource
    {
        private readonly CancellationTokenSourcePool _pool;

        public PooledCancellationTokenSource(CancellationTokenSourcePool pool)
        {
            _pool = pool;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // If we failed to return to the pool then dispose
                if (!_pool.Return(this))
                {
                    base.Dispose(disposing);
                }
            }
        }

        internal void DisposeBase() => base.Dispose(true);
    }
}
