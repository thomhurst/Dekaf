using System.Runtime.CompilerServices;
using Dekaf.Internal;

namespace Dekaf.Producer;

/// <summary>
/// Thread-safe bounded object pool backed by Reservoir.
/// Provides pre-warming, retained-count diagnostics, dynamic capacity ratcheting,
/// and miss tracking for Dekaf's pooled producer and protocol objects.
/// </summary>
/// <remarks>
/// Subclasses implement <see cref="Create"/> to produce new items and <see cref="Reset"/>
/// to prepare returned items for reuse. Reservoir supplies fixed-capacity, zero-allocation
/// storage specialized for small and large pools.
/// </remarks>
/// <typeparam name="T">Pooled reference type.</typeparam>
internal abstract class ObjectPool<T> where T : class
{
    private Reservoir.ObjectPool<T, PoolPolicy> _pool;
    private readonly Lock _resizeLock = new();
    private int _maxPoolSize;
    private int _retainedCount;
    private long _misses;

    /// <summary>Maximum number of items retained.</summary>
    public int MaxPoolSize => Volatile.Read(ref _maxPoolSize);

    /// <summary>Approximate number of retained items.</summary>
    public int ApproximateCount => Volatile.Read(ref _retainedCount);

    /// <summary>Number of empty-pool rents that created an item.</summary>
    public long Misses => Volatile.Read(ref _misses);

    protected ObjectPool(int maxPoolSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPoolSize);
        _maxPoolSize = maxPoolSize;
        _pool = CreatePool(maxPoolSize);
    }

    /// <summary>Creates a new instance when no retained item is available.</summary>
    protected abstract T Create();

    /// <summary>Resets an item before retention or discard.</summary>
    protected abstract void Reset(T item);

    /// <summary>Rents an item, creating one on a miss.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        var item = Volatile.Read(ref _pool).Rent();
        DecrementRetainedCount();
        return item;
    }

    /// <summary>Returns an item, discarding it when retained capacity is full.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        Reset(item);
        TryRetain(item);
    }

    /// <summary>
    /// Resets and attempts to retain an item.
    /// </summary>
    /// <returns><see langword="true"/> when retained; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryReturn(T item)
    {
        Reset(item);
        return TryRetain(item);
    }

    /// <summary>Increases retained capacity while preserving currently retained items.</summary>
    public void RatchetMaxPoolSize(int newSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newSize);
        InterlockedHelper.RatchetUp(ref _maxPoolSize, newSize);

        var currentPool = Volatile.Read(ref _pool);
        if (currentPool.MaximumRetained >= newSize)
            return;

        lock (_resizeLock)
        {
            currentPool = Volatile.Read(ref _pool);
            if (currentPool.MaximumRetained >= newSize)
                return;

            var newPool = CreatePool(newSize);
            var transferCount = Math.Min(
                Volatile.Read(ref _retainedCount),
                currentPool.MaximumRetained);

            for (var i = 0; i < transferCount; i++)
                newPool.Return(currentPool.Rent());

            Volatile.Write(ref _pool, newPool);
            Volatile.Write(ref _retainedCount, transferCount);
            currentPool.Clear();
        }
    }

    /// <summary>Pre-allocates retained items up to <paramref name="count"/>.</summary>
    public void PreWarm(int count)
    {
        count = Math.Min(count, MaxPoolSize);
        while (Volatile.Read(ref _retainedCount) < count && TryRetain(Create())) { }
    }

    /// <summary>Clears retained items while leaving pool usable.</summary>
    public void Clear()
    {
        Volatile.Read(ref _pool).Clear();
        Volatile.Write(ref _retainedCount, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryRetain(T item)
    {
        Volatile.Read(ref _pool).Return(item);

        while (true)
        {
            var count = Volatile.Read(ref _retainedCount);
            if (count >= Volatile.Read(ref _maxPoolSize))
                return false;

            if (Interlocked.CompareExchange(ref _retainedCount, count + 1, count) != count)
                continue;

            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecrementRetainedCount()
    {
        while (true)
        {
            var count = Volatile.Read(ref _retainedCount);
            if (count == 0 ||
                Interlocked.CompareExchange(ref _retainedCount, count - 1, count) == count)
            {
                return;
            }
        }
    }

    private Reservoir.ObjectPool<T, PoolPolicy> CreatePool(int capacity) =>
        new(new PoolPolicy(this), capacity);

    private readonly struct PoolPolicy(ObjectPool<T> owner)
        : Reservoir.IPooledObjectDestroyPolicy<T>
    {
        public T Create()
        {
            Interlocked.Increment(ref owner._misses);
            return owner.Create();
        }

        public bool TryReset(T item) => true;

        // Preserve prior behavior: discarded items become GC-eligible without implicit disposal.
        public void Destroy(T item) { }
    }
}
