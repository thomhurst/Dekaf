using System.Collections.Concurrent;

namespace Dekaf.Internal;

/// <summary>
/// Thread-safe pool for CancellationTokenSource instances to avoid allocations in hot paths.
/// </summary>
internal sealed class CancellationTokenSourcePool
{
    private readonly ConcurrentBag<CancellationTokenSource> _pool = new();
    private const int MaxPoolSize = 16; // Limit pool size to prevent unbounded growth
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
