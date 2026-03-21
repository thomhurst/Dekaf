using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Dekaf.Producer;

/// <summary>
/// Thread-safe bounded object pool using a lock-free <see cref="ConcurrentStack{T}"/>.
/// Provides pre-warming to eliminate ramp-up allocation bursts and miss tracking for diagnostics.
/// </summary>
/// <remarks>
/// <para>
/// The pool count is approximate due to lock-free operations. Under high contention,
/// the pool may temporarily contain slightly more or fewer items than <see cref="MaxPoolSize"/>.
/// This is intentional to avoid locks in the hot path and has no correctness impact.
/// </para>
/// <para>
/// Subclasses implement <see cref="Create"/> to produce new items and <see cref="Reset"/>
/// to prepare returned items for reuse.
/// </para>
/// </remarks>
/// <typeparam name="T">The pooled item type. Must be a reference type.</typeparam>
internal abstract class ObjectPool<T> where T : class
{
    private readonly ConcurrentStack<T> _pool = new();
    private int _poolCount;
    private long _misses;

    /// <summary>
    /// Maximum number of items the pool will retain. Excess items are discarded for GC.
    /// </summary>
    public int MaxPoolSize { get; }

    /// <summary>
    /// Approximate number of items currently in the pool.
    /// </summary>
    public int ApproximateCount => Volatile.Read(ref _poolCount);

    /// <summary>
    /// Number of times <see cref="Rent"/> found the pool empty and had to allocate.
    /// Use this to diagnose pool sizing — sustained misses under load indicate the pool is too small.
    /// </summary>
    public long Misses => Volatile.Read(ref _misses);

    protected ObjectPool(int maxPoolSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPoolSize);
        MaxPoolSize = maxPoolSize;
    }

    /// <summary>
    /// Creates a new instance when the pool is empty.
    /// </summary>
    protected abstract T Create();

    /// <summary>
    /// Resets an item before it is returned to the pool, preparing it for reuse.
    /// </summary>
    protected abstract void Reset(T item);

    /// <summary>
    /// Gets an item from the pool or creates a new one if empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        if (_pool.TryPop(out var item))
        {
            Interlocked.Decrement(ref _poolCount);
            return item;
        }

        Interlocked.Increment(ref _misses);
        return Create();
    }

    /// <summary>
    /// Returns an item to the pool for reuse. If the pool is full, the item is discarded without reset.
    /// Reset is only performed on items that will actually be pooled, avoiding wasted work on discards
    /// and preserving exception safety (pool count is rolled back if Reset throws).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        if (Interlocked.Increment(ref _poolCount) <= MaxPoolSize)
        {
            try
            {
                Reset(item);
                _pool.Push(item);
            }
            catch
            {
                Interlocked.Decrement(ref _poolCount);
                throw;
            }
        }
        else
        {
            Interlocked.Decrement(ref _poolCount);
            // Pool is full — item will be garbage collected without reset
        }
    }

    /// <summary>
    /// Pre-allocates items up to the specified count (capped at <see cref="MaxPoolSize"/>).
    /// Call during initialization to eliminate ramp-up allocation bursts.
    /// </summary>
    /// <param name="count">Number of items to pre-allocate.</param>
    public void PreWarm(int count)
    {
        count = Math.Min(count, MaxPoolSize);

        for (var i = 0; i < count; i++)
        {
            var current = Volatile.Read(ref _poolCount);
            if (current >= MaxPoolSize)
                break;

            var item = Create();

            if (Interlocked.Increment(ref _poolCount) <= MaxPoolSize)
            {
                _pool.Push(item);
            }
            else
            {
                Interlocked.Decrement(ref _poolCount);
                break;
            }
        }
    }

    /// <summary>
    /// Clears all pooled items.
    /// Not thread-safe with concurrent Rent/Return — only call during single-threaded teardown.
    /// </summary>
    public void Clear()
    {
        _pool.Clear();
        Volatile.Write(ref _poolCount, 0);
    }
}
