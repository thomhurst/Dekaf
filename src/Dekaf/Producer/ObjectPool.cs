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
/// storage specialized for small and large pools. Dekaf disables Reservoir's thread-local fast
/// path here because these rentals commonly cross thread or asynchronous continuation boundaries.
/// </remarks>
/// <typeparam name="T">Pooled reference type.</typeparam>
internal abstract class ObjectPool<T> where T : class
{
    private Reservoir.ObjectPool<T, PoolPolicy> _pool;
    private PoolState _poolState;
    private PoolState? _retiredPool;
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
    /// <remarks>
    /// Concurrent rents, returns, and pool replacement can make this value transiently
    /// inaccurate. Do not use it as a correctness gate or expect pre-warming during concurrent
    /// use to reach an exact retained count.
    /// </remarks>
    public int ApproximateCount => Volatile.Read(ref _retainedCount);

    /// <summary>Number of empty-pool rents that created an item.</summary>
    public long Misses => Volatile.Read(ref _misses);

    protected ObjectPool(int maxPoolSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPoolSize);
        _maxPoolSize = maxPoolSize;
        _poolState = CreatePool(maxPoolSize);
        _pool = _poolState.Pool;
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

            var currentState = _poolState;
            var replacementState = CreatePool(newSize);
            Volatile.Write(ref currentState.MigrationTarget, replacementState);
            _poolState = replacementState;
            Volatile.Write(ref _pool, replacementState.Pool);

            // Clear invokes the old policy's Destroy callback for every retained item. During
            // a ratchet that callback forwards the item to the replacement pool, preserving
            // warm objects without relying on an approximate count or a creating Rent call.
            if (_retiredPool is { } retiredPool)
            {
                retiredPool.Pool.Clear();
                retiredPool.Pool.Dispose();
            }
            currentPool.Clear();
            // A return may have captured the old pool before publication. Keep that one generation
            // reachable so the next ratchet can forward late returns before releasing its storage.
            _retiredPool = currentState;
        }
    }

    /// <summary>
    /// Creates and retains fresh items up to <paramref name="count"/> without applying the
    /// return-time reset intended for previously used objects.
    /// </summary>
    public void PreWarm(int count)
    {
        count = Math.Min(count, MaxPoolSize);
        var missing = Math.Max(0, count - ApproximateCount);
        for (var i = 0; i < missing; i++)
        {
            Volatile.Read(ref _pool).Return(Create());
            Interlocked.Increment(ref _retainedCount);
        }
    }

    /// <summary>Clears retained items while leaving pool usable.</summary>
    public virtual void Clear()
    {
        lock (_resizeLock)
        {
            var retiredPool = _retiredPool;
            if (retiredPool is not null)
                Volatile.Write(ref retiredPool.MigrationTarget, null);

            Volatile.Read(ref _pool).Clear();

            if (retiredPool is null)
                return;

            retiredPool.Pool.Clear();
            retiredPool.Pool.Dispose();
            _retiredPool = null;
        }
    }

    private PoolState CreatePool(int capacity)
    {
        var state = new PoolState(this);
        state.Pool = new Reservoir.ObjectPool<T, PoolPolicy>(
            new PoolPolicy(state),
            capacity,
            threadLocalFastPath: false);
        return state;
    }

    private sealed class PoolState(ObjectPool<T> owner)
    {
        public readonly ObjectPool<T> Owner = owner;
        public Reservoir.ObjectPool<T, PoolPolicy> Pool = null!;
        public PoolState? MigrationTarget;
    }

    private readonly struct PoolPolicy(PoolState state)
        : Reservoir.IPooledObjectDestroyPolicy<T>
    {
        public T Create()
        {
            var owner = state.Owner;
            Interlocked.Increment(ref owner._misses);
            var item = owner.Create();
            Interlocked.Increment(ref owner._retainedCount);
            return item;
        }

        public bool TryReset(T item) => true;

        public void Destroy(T item)
        {
            var owner = state.Owner;
            Interlocked.Decrement(ref owner._retainedCount);
            var target = Volatile.Read(ref state.MigrationTarget);
            if (target is null)
            {
                owner.Destroy(item);
                return;
            }

            while (Volatile.Read(ref target.MigrationTarget) is { } next)
                target = next;

            target.Pool.Return(item);
            Interlocked.Increment(ref owner._retainedCount);
        }
    }
}
