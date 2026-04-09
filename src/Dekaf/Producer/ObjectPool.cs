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
    private readonly LockFreeStack<T> _stack;
    private long _misses;

    /// <summary>
    /// Maximum number of items the pool will retain. Excess items are discarded for GC.
    /// </summary>
    public int MaxPoolSize { get; }

    /// <summary>
    /// Approximate number of items currently in the pool.
    /// </summary>
    public int ApproximateCount => _stack.Count;

    /// <summary>
    /// Number of times <see cref="Rent"/> found the pool empty and had to allocate.
    /// Use this to diagnose pool sizing — sustained misses under load indicate the pool is too small.
    /// </summary>
    public long Misses => Volatile.Read(ref _misses);

    protected ObjectPool(int maxPoolSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPoolSize);
        MaxPoolSize = maxPoolSize;
        _stack = new LockFreeStack<T>(maxPoolSize);
    }

    /// <summary>
    /// Creates a new instance when the pool is empty.
    /// </summary>
    protected abstract T Create();

    /// <summary>
    /// Resets an item before it is returned to the pool, preparing it for reuse.
    /// Must be idempotent — may be called on items that are ultimately discarded
    /// if the pool fills between the capacity check and the TryPush.
    /// </summary>
    protected abstract void Reset(T item);

    /// <summary>
    /// Gets an item from the pool or creates a new one if empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        if (_stack.TryPop(out var item))
            return item;

        Interlocked.Increment(ref _misses);
        return Create();
    }

    /// <summary>
    /// Returns an item to the pool for reuse. If the pool is full, the item is discarded without reset.
    /// </summary>
    /// <remarks>
    /// The Count check and TryPush are not atomic, so Reset may run on an item that is ultimately
    /// discarded if the pool fills between the check and push. This is benign — Reset is idempotent
    /// and the wasted work is rare (only under high contention at exact pool capacity).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        if (_stack.Count >= MaxPoolSize)
            return; // Pool full — discard without reset

        Reset(item);
        _stack.TryPush(item);
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
            if (!_stack.TryPush(item))
                break;
        }
    }

    /// <summary>
    /// Clears all pooled items.
    /// Not thread-safe with concurrent Rent/Return — only call during single-threaded teardown.
    /// </summary>
    public void Clear()
    {
        _stack.Clear();
    }
}
