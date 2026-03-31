using System.Runtime.CompilerServices;

namespace Dekaf.Producer;

/// <summary>
/// Thread-safe bounded object pool using a pre-allocated array.
/// Zero allocation in steady state: Rent/Return only perform Interlocked operations
/// on the stack pointer and array slots — no linked list nodes or wrapper objects.
/// Provides pre-warming to eliminate ramp-up allocation bursts and miss tracking for diagnostics.
/// </summary>
/// <remarks>
/// <para>
/// Uses an array-based LIFO stack with a CAS-guarded top index. Each slot stores
/// a pooled item reference. Push writes to <c>_slots[top]</c> and increments top;
/// Pop decrements top and reads from <c>_slots[top]</c>. Interlocked.Exchange on
/// slots prevents ABA-style issues where two concurrent pops could return the same item.
/// </para>
/// <para>
/// The previous ConcurrentStack&lt;T&gt; implementation allocated a Node object (~32 bytes)
/// per Push call. At 4000 inflight entries/sec (400 batches × 10 partitions), this created
/// ~128 KB/sec of short-lived Gen0 allocations — enough to seed a GC feedback loop on
/// 2-core CI machines where GC pauses cause pool exhaustion and cascading allocations.
/// </para>
/// <para>
/// Subclasses implement <see cref="Create"/> to produce new items and <see cref="Reset"/>
/// to prepare returned items for reuse.
/// </para>
/// </remarks>
/// <typeparam name="T">The pooled item type. Must be a reference type.</typeparam>
internal abstract class ObjectPool<T> where T : class
{
    // Pre-allocated array of slots. Indices [0, _top) contain pooled items.
    // _top is the next write position (empty slot) — the stack grows upward.
    private readonly T?[] _slots;
    private int _top;
    private long _misses;

    /// <summary>
    /// Maximum number of items the pool will retain. Excess items are discarded for GC.
    /// </summary>
    public int MaxPoolSize { get; }

    /// <summary>
    /// Approximate number of items currently in the pool.
    /// </summary>
    public int ApproximateCount => Volatile.Read(ref _top);

    /// <summary>
    /// Number of times <see cref="Rent"/> found the pool empty and had to allocate.
    /// Use this to diagnose pool sizing — sustained misses under load indicate the pool is too small.
    /// </summary>
    public long Misses => Volatile.Read(ref _misses);

    protected ObjectPool(int maxPoolSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPoolSize);
        MaxPoolSize = maxPoolSize;
        _slots = new T?[maxPoolSize];
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
        // Optimistic CAS loop: try to decrement _top and take the item at that index.
        // Contention is rare since pools are typically accessed by 1-2 threads.
        while (true)
        {
            var top = Volatile.Read(ref _top);
            if (top <= 0)
                break; // Pool empty — fall through to Create

            if (Interlocked.CompareExchange(ref _top, top - 1, top) == top)
            {
                // We own slot [top - 1]. Exchange it to null atomically to prevent
                // another concurrent pop from seeing the same item (ABA prevention).
                var item = Interlocked.Exchange(ref _slots[top - 1], null);
                if (item is not null)
                    return item;

                // Slot was null — another Rent concurrently claimed this item before
                // we read it. The _top was already decremented, so the pool "lost" a slot.
                // This is benign: pool count is approximate, and a future Return will
                // overwrite _slots[top-1] with a fresh item, recovering the position.
                break;
            }
            // CAS failed — another thread modified _top. Retry.
        }

        Interlocked.Increment(ref _misses);
        return Create();
    }

    /// <summary>
    /// Returns an item to the pool for reuse. If the pool is full, the item is discarded without reset.
    /// Reset is only performed on items that will actually be pooled, avoiding wasted work on discards
    /// and preserving exception safety (pool top is rolled back if Reset throws).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        while (true)
        {
            var top = Volatile.Read(ref _top);
            if (top >= MaxPoolSize)
                return; // Pool full — item will be garbage collected without reset

            if (Interlocked.CompareExchange(ref _top, top + 1, top) == top)
            {
                // We own slot [top]. Reset the item and write it.
                try
                {
                    Reset(item);
                    Volatile.Write(ref _slots[top], item);
                }
                catch
                {
                    // Reset failed — don't store a corrupt item. The slot stays null,
                    // which is equivalent to "losing" a pool slot. Decrement _top to
                    // avoid leaving a null gap that would be returned as a miss.
                    Interlocked.Decrement(ref _top);
                    throw;
                }
                return;
            }
            // CAS failed — retry.
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
            var top = Volatile.Read(ref _top);
            if (top >= MaxPoolSize)
                break;

            var item = Create();

            if (Interlocked.CompareExchange(ref _top, top + 1, top) == top)
            {
                _slots[top] = item; // Plain write: PreWarm runs before the pool is shared
            }
            // CAS failed — discard item. PreWarm is best-effort; the next iteration
            // will create a fresh item at the updated _top position.
        }
    }

    /// <summary>
    /// Clears all pooled items.
    /// Not thread-safe with concurrent Rent/Return — only call during single-threaded teardown.
    /// </summary>
    public void Clear()
    {
        var top = Volatile.Read(ref _top);
        for (var i = 0; i < top; i++)
            _slots[i] = null;
        Volatile.Write(ref _top, 0);
    }
}
