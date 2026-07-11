using System.Runtime.CompilerServices;
using Dekaf.Internal;

namespace Dekaf.Producer;

/// <summary>
/// Thread-safe bounded object pool backed by <see cref="LockFreeStack{T}"/>.
/// Zero allocation in steady state: Rent/Return only perform Interlocked operations
/// on the stack pointer and array slots — no linked list nodes or wrapper objects.
/// Provides pre-warming to eliminate ramp-up allocation bursts and miss tracking for diagnostics.
/// </summary>
/// <remarks>
/// Subclasses implement <see cref="Create"/> to produce new items and <see cref="Reset"/>
/// to prepare returned items for reuse.
/// </remarks>
/// <typeparam name="T">The pooled item type. Must be a reference type.</typeparam>
internal abstract class ObjectPool<T> where T : class
{
    private LockFreeStack<T> _stack;
    private readonly Lock _resizeLock = new();
    private int _maxPoolSize;
    private long _misses;

    /// <summary>
    /// Maximum number of items the pool will retain. Excess items are discarded for GC.
    /// </summary>
    public int MaxPoolSize => Volatile.Read(ref _maxPoolSize);

    /// <summary>
    /// Approximate number of items currently in the pool.
    /// </summary>
    public int ApproximateCount => Volatile.Read(ref _stack).Count;

    /// <summary>
    /// Number of times <see cref="Rent"/> found the pool empty and had to allocate.
    /// Use this to diagnose pool sizing — sustained misses under load indicate the pool is too small.
    /// </summary>
    public long Misses => Volatile.Read(ref _misses);

    protected ObjectPool(int maxPoolSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPoolSize);
        _maxPoolSize = maxPoolSize;
        _stack = new LockFreeStack<T>(maxPoolSize);
    }

    /// <summary>
    /// Creates a new instance when the pool is empty.
    /// </summary>
    protected abstract T Create();

    /// <summary>
    /// Resets an item before it is returned to the pool, preparing it for reuse.
    /// Must be idempotent and must not throw — may be called on items that are
    /// ultimately discarded if the pool fills between the capacity check and the
    /// TryPush. If Reset throws, the item is neither pooled nor returned to the caller.
    /// </summary>
    protected abstract void Reset(T item);

    /// <summary>
    /// Gets an item from the pool or creates a new one if empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        if (Volatile.Read(ref _stack).TryPop(out var item))
            return item;

        Interlocked.Increment(ref _misses);
        return Create();
    }

    /// <summary>
    /// Returns an item to the pool for reuse. If the pool is full, the reset item is discarded.
    /// </summary>
    /// <remarks>
    /// TryPush is the sole capacity gate so Return stays O(1) on striped stacks; Reset may run on an
    /// item that is ultimately discarded when the pool is full, which is benign because Reset is
    /// idempotent.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        Reset(item);
        Volatile.Read(ref _stack).TryPush(item);
    }

    /// <summary>
    /// Increases retained capacity while preserving items already in the pool.
    /// A concurrent return through a stale stack reference may be lost during the
    /// one-time migration; later returns use the newly published pool.
    /// </summary>
    public void RatchetMaxPoolSize(int newSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newSize);
        InterlockedHelper.RatchetUp(ref _maxPoolSize, newSize);

        var currentPool = Volatile.Read(ref _stack);
        if (currentPool.Capacity >= newSize)
            return;

        lock (_resizeLock)
        {
            currentPool = Volatile.Read(ref _stack);
            if (currentPool.Capacity >= newSize)
                return;

            var newPool = new LockFreeStack<T>(newSize);
            while (currentPool.TryPop(out var item))
                newPool.TryPush(item);
            Volatile.Write(ref _stack, newPool);
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
            var item = Create();
            if (!Volatile.Read(ref _stack).TryPush(item))
                break;
        }
    }

    /// <summary>
    /// Clears all pooled items.
    /// Not thread-safe with concurrent Rent/Return — only call during single-threaded teardown.
    /// </summary>
    public void Clear()
    {
        Volatile.Read(ref _stack).Clear();
    }
}
