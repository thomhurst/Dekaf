using System.Collections.Concurrent;

namespace Dekaf.Internal;

/// <summary>
/// Thread-safe pool for CancellationTokenSource instances to avoid allocations in hot paths.
/// </summary>
internal sealed class CancellationTokenSourcePool
{
    private readonly ConcurrentBag<CancellationTokenSource> _pool = new();
    // Pool size must accommodate peak concurrent requests across all connections.
    // At 250K msg/sec with batching and multiple partitions, concurrent in-flight
    // requests can spike to 500+ during bursts or when broker responses are slow.
    // 512 provides headroom for high-throughput scenarios while bounding memory usage.
    private const int MaxPoolSize = 512;
    private int _count;

    public CancellationTokenSource Rent()
    {
        if (_pool.TryTake(out var cts))
        {
            Interlocked.Decrement(ref _count);
            if (cts.TryReset())
            {
                return cts;
            }
            // Reset failed, dispose and create new
            cts.Dispose();
        }

        return new CancellationTokenSource();
    }

    public void Return(CancellationTokenSource cts)
    {
        if (cts.IsCancellationRequested)
        {
            cts.Dispose();
            return;
        }

        // Atomic check-and-increment to prevent race condition
        var currentCount = Volatile.Read(ref _count);
        while (currentCount < MaxPoolSize)
        {
            if (Interlocked.CompareExchange(ref _count, currentCount + 1, currentCount) == currentCount)
            {
                _pool.Add(cts);
                return;
            }
            currentCount = Volatile.Read(ref _count);
        }

        // Pool is full, dispose
        cts.Dispose();
    }

    public void Clear()
    {
        while (_pool.TryTake(out var cts))
        {
            cts.Dispose();
        }
        _count = 0;
    }
}
