using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Dekaf.Internal;

/// <summary>
/// Thread-safe bounded LIFO stack using pre-allocated striped arrays with CAS-guarded indices.
/// Zero allocation in steady state: TryPush/TryPop only perform Interlocked operations
/// on stripe pointers and array slots — no linked list nodes or wrapper objects.
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
/// <para>
/// Large pools are striped by processor so hot rent/return paths do not all contend on
/// one cache line. A pop starts at the current processor's stripe and steals from other
/// stripes on miss; this keeps the pool bounded while spreading CAS traffic.
/// </para>
/// </remarks>
/// <typeparam name="T">The pooled item type. Must be a reference type for Interlocked.Exchange.</typeparam>
internal sealed class LockFreeStack<T> where T : class
{
    private const int MinCapacityForStriping = 64;
    private const int MaxStripeCount = 32;
    private const int MinSlotsPerStripe = 16;

    private readonly Stripe[] _stripes;
    private readonly int _capacity;

    private sealed class Stripe(int capacity)
    {
        public readonly T?[] Slots = new T?[capacity];
        public int Top;
    }

    /// <summary>
    /// Creates a new stack with the specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of items the stack can hold.</param>
    public LockFreeStack(int capacity)
    {
        CompatibilityThrowHelpers.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;

        var stripeCount = ComputeStripeCount(capacity);
        _stripes = new Stripe[stripeCount];

        var baseCapacity = capacity / stripeCount;
        var extraSlots = capacity % stripeCount;
        for (var i = 0; i < stripeCount; i++)
            _stripes[i] = new Stripe(baseCapacity + (i < extraSlots ? 1 : 0));
    }

    /// <summary>
    /// Attempts to push an item onto the stack.
    /// </summary>
    /// <param name="item">The item to push.</param>
    /// <returns><c>true</c> if the item was pushed; <c>false</c> if the stack is full.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPush(T item)
    {
        var start = GetStartStripe();
        for (var i = 0; i < _stripes.Length; i++)
        {
            if (TryPush(_stripes[(start + i) % _stripes.Length], item))
                return true;
        }

        return false;
    }

    private static bool TryPush(Stripe stripe, T item)
    {
        var slots = stripe.Slots;
        while (true)
        {
            var top = Volatile.Read(ref stripe.Top);
            if (top >= slots.Length)
                return false; // Stack full

            if (Interlocked.CompareExchange(ref stripe.Top, top + 1, top) == top)
            {
                Volatile.Write(ref slots[top], item);
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
        var start = GetStartStripe();
        for (var i = 0; i < _stripes.Length; i++)
        {
            if (TryPop(_stripes[(start + i) % _stripes.Length], out item))
                return true;
        }

        item = null;
        return false;
    }

    private static bool TryPop(Stripe stripe, [NotNullWhen(true)] out T? item)
    {
        var slots = stripe.Slots;
        while (true)
        {
            var top = Volatile.Read(ref stripe.Top);
            if (top <= 0)
            {
                item = null;
                return false;
            }

            if (Interlocked.CompareExchange(ref stripe.Top, top - 1, top) == top)
            {
                // We own slot [top - 1]. Exchange it to null atomically to prevent
                // another concurrent pop from seeing the same item.
                item = Interlocked.Exchange(ref slots[top - 1], null);
                if (item is not null)
                    return true;

                // Slot was null — a concurrent TryPush advanced this stripe's top
                // but had not written the item yet. That late write can strand one
                // pooled item until a later TryPush overwrites the same slot; pool
                // misses recreate on demand.
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
    public int Count
    {
        get
        {
            var count = 0;
            foreach (var stripe in _stripes)
                count += Volatile.Read(ref stripe.Top);
            return count;
        }
    }

    /// <summary>
    /// Maximum number of items the stack can hold.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Clears all items from the stack.
    /// Not thread-safe with concurrent TryPush/TryPop — only call during single-threaded teardown.
    /// </summary>
    public void Clear()
    {
        foreach (var stripe in _stripes)
        {
            Array.Clear(stripe.Slots, 0, stripe.Slots.Length);
            Volatile.Write(ref stripe.Top, 0);
        }
    }

    private static int ComputeStripeCount(int capacity)
    {
        if (capacity < MinCapacityForStriping)
            return 1;

        var maxByCapacity = capacity / MinSlotsPerStripe;
        return Math.Min(Math.Min(Environment.ProcessorCount, MaxStripeCount), maxByCapacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetStartStripe()
        => _stripes.Length == 1
            ? 0
            : (CompatibilityBcl.GetCurrentProcessorId() & int.MaxValue) % _stripes.Length;
}
