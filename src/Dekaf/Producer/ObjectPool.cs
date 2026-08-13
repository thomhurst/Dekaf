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

    /// <summary>
    /// Best-effort retained count for diagnostics and cold-path pre-warming.
    /// Reservoir remains the sole authority for retention decisions.
    /// </summary>
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

    /// <summary>Releases resources owned by an item discarded by Reservoir.</summary>
    protected virtual void Destroy(T item) { }

    /// <summary>Rents an item, creating one on a miss.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        var item = Volatile.Read(ref _pool).Rent();
        Interlocked.Decrement(ref _retainedCount);
        return item;
    }

    /// <summary>Returns an item, discarding it when retained capacity is full.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        Reset(item);
        Volatile.Read(ref _pool).Return(item);
        // Destroy runs synchronously on rejection, so this balances both retained and discarded items.
        Interlocked.Increment(ref _retainedCount);
    }

    /// <summary>Increases retained capacity.</summary>
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

            Volatile.Write(ref _pool, CreatePool(newSize));
            currentPool.Clear();
        }
    }

    /// <summary>Pre-allocates retained items up to <paramref name="count"/>.</summary>
    public void PreWarm(int count)
    {
        count = Math.Min(count, MaxPoolSize);
        var missing = Math.Max(0, count - ApproximateCount);
        for (var i = 0; i < missing; i++)
            Return(Create());
    }

    /// <summary>Clears retained items while leaving pool usable.</summary>
    public void Clear()
    {
        Volatile.Read(ref _pool).Clear();
    }

    private Reservoir.ObjectPool<T, PoolPolicy> CreatePool(int capacity) =>
        new(new PoolPolicy(this), capacity);

    private readonly struct PoolPolicy(ObjectPool<T> owner)
        : Reservoir.IPooledObjectDestroyPolicy<T>
    {
        public T Create()
        {
            Interlocked.Increment(ref owner._misses);
            var item = owner.Create();
            Interlocked.Increment(ref owner._retainedCount);
            return item;
        }

        public bool TryReset(T item) => true;

        public void Destroy(T item)
        {
            Interlocked.Decrement(ref owner._retainedCount);
            owner.Destroy(item);
        }
    }
}
