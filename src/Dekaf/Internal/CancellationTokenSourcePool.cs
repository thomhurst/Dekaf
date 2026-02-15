using System.Collections.Concurrent;

namespace Dekaf.Internal;

/// <summary>
/// Thread-safe pool for CancellationTokenSource instances to avoid allocations in hot paths.
/// Rent() returns a <see cref="PooledCancellationTokenSource"/> whose Dispose() auto-returns
/// to the pool, making it safe to use with <c>using var</c>.
/// </summary>
/// <remarks>
/// Design based on Microsoft's ASP.NET Core CancellationTokenSourcePool.
/// TryReset() is called on return (not rent) to keep the pool clean — cancelled
/// instances are disposed immediately and never occupy a pool slot.
/// </remarks>
internal sealed class CancellationTokenSourcePool
{
    // Pool size must accommodate peak concurrent requests across all connections.
    // At 250K msg/sec with batching and multiple partitions, concurrent in-flight
    // requests can spike to 500+ during bursts or when broker responses are slow.
    // 512 provides headroom for high-throughput scenarios while bounding memory usage.
    private const int MaxPoolSize = 512;
    private readonly ConcurrentQueue<PooledCancellationTokenSource> _queue = new();
    private int _count;

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
        if (Interlocked.Increment(ref _count) > MaxPoolSize || !cts.TryReset())
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
            cts.DisposeWithoutPooling();
        }
        _count = 0;
    }

    internal sealed class PooledCancellationTokenSource : CancellationTokenSource
    {
        private readonly CancellationTokenSourcePool _pool;

        internal PooledCancellationTokenSource(CancellationTokenSourcePool pool) => _pool = pool;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_pool.Return(this))
                {
                    base.Dispose(disposing);
                }
            }
        }

        /// <summary>
        /// Disposes without returning to pool. Used by <see cref="CancellationTokenSourcePool.Clear"/>.
        /// </summary>
        internal void DisposeWithoutPooling() => base.Dispose(true);
    }
}
