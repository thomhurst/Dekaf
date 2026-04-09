using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Dekaf.Internal;

/// <summary>
/// Thread-safe bounded LIFO stack using a pre-allocated array with CAS-guarded index.
/// Zero allocation in steady state: TryPush/TryPop only perform Interlocked operations
/// on the stack pointer and array slots — no linked list nodes or wrapper objects.
/// </summary>
/// <remarks>
/// <para>
/// Replaces the per-type inline CAS stack pattern that was duplicated across
/// ValueTaskSourcePool, BatchArena, BatchArrayReuseQueue, SerializationCache pool,
/// and LazyRecordList pool. All five used identical logic; this class extracts it once.
/// </para>
/// <para>
/// The previous ConcurrentStack/ConcurrentQueue implementations allocated a Node object
/// (~32 bytes) per Push/Enqueue call. At high throughput these short-lived allocations
/// promoted to Gen2 and caused a GC feedback loop. This array-based CAS stack eliminates
/// all per-operation allocations — only the fixed-size array is allocated at construction time.
/// </para>
/// </remarks>
/// <typeparam name="T">The pooled item type. Must be a reference type for Interlocked.Exchange.</typeparam>
internal sealed class LockFreeStack<T> where T : class
{
    private readonly T?[] _slots;
    private int _top;

    /// <summary>
    /// Creates a new stack with the specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of items the stack can hold.</param>
    public LockFreeStack(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _slots = new T?[capacity];
    }

    /// <summary>
    /// Attempts to push an item onto the stack.
    /// </summary>
    /// <param name="item">The item to push.</param>
    /// <returns><c>true</c> if the item was pushed; <c>false</c> if the stack is full.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPush(T item)
    {
        while (true)
        {
            var top = Volatile.Read(ref _top);
            if (top >= _slots.Length)
                return false; // Stack full

            if (Interlocked.CompareExchange(ref _top, top + 1, top) == top)
            {
                Volatile.Write(ref _slots[top], item);
                return true;
            }
            // CAS failed — retry.
        }
    }

    /// <summary>
    /// Attempts to pop an item from the stack.
    /// </summary>
    /// <param name="item">The popped item, or <c>null</c> if the stack was empty.</param>
    /// <returns><c>true</c> if an item was popped; <c>false</c> if the stack was empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop([NotNullWhen(true)] out T? item)
    {
        while (true)
        {
            var top = Volatile.Read(ref _top);
            if (top <= 0)
            {
                item = null;
                return false;
            }

            if (Interlocked.CompareExchange(ref _top, top - 1, top) == top)
            {
                // We own slot [top - 1]. Exchange it to null atomically to prevent
                // another concurrent pop from seeing the same item.
                item = Interlocked.Exchange(ref _slots[top - 1], null);
                if (item is not null)
                    return true;

                // Slot was null — a concurrent TryPush incremented _top (claiming this
                // slot) but hadn't written its item yet when we exchanged the slot to
                // null. TryPush will still write to _slots[top - 1], but since _top was
                // already decremented, that item is stranded and will be overwritten by
                // the next TryPush that claims the same index. The pool loses one item;
                // for pool use cases this is benign — the miss path re-creates on demand.
                //
                // Note: if TryPush writes BEFORE our Exchange, Exchange returns the item
                // (non-null) and we return true at line 86 — that path never reaches here.
                return false;
            }
            // CAS failed — retry.
        }
    }

    /// <summary>
    /// Approximate number of items currently in the stack.
    /// </summary>
    public int Count => Volatile.Read(ref _top);

    /// <summary>
    /// Maximum number of items the stack can hold.
    /// </summary>
    public int Capacity => _slots.Length;

    /// <summary>
    /// Clears all items from the stack.
    /// Not thread-safe with concurrent TryPush/TryPop — only call during single-threaded teardown.
    /// </summary>
    public void Clear()
    {
        var top = Volatile.Read(ref _top);
        for (var i = 0; i < top; i++)
            _slots[i] = null;
        Volatile.Write(ref _top, 0);
    }
}
