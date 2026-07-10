using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Dekaf.Internal;

/// <summary>
/// Thread-safe bounded LIFO stack using pre-allocated striped node arrays with stamped CAS heads.
/// Zero allocation in steady state: TryPush/TryPop only perform Interlocked operations
/// on stripe heads and pre-allocated nodes — no linked list nodes or wrapper objects are allocated.
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
/// all per-operation allocations — only fixed-size arrays are allocated at construction time.
/// </para>
/// <para>
/// Large pools are striped by processor so hot rent/return paths do not all contend on
/// one cache line. A pop starts at the current processor's stripe and steals from other
/// stripes on miss; this keeps the pool bounded while spreading CAS traffic.
/// </para>
/// <para>
/// Each node is either on a stripe's free list, on its available-item stack, or exclusively
/// owned by one operation. Push writes the item before publishing the node. Version-stamped
/// heads prevent ABA when nodes are rapidly recycled between the two lists.
/// </para>
/// </remarks>
/// <typeparam name="T">The pooled item type. Must be a reference type for Interlocked.Exchange.</typeparam>
internal sealed class LockFreeStack<T> where T : class
{
    private const int MinCapacityForStriping = 64;
    private const int MaxStripeCount = 32;
    private const int MinSlotsPerStripe = 16;
    private const int EmptyIndex = -1;

    private readonly Stripe[] _stripes;
    private readonly int _capacity;
    private readonly Action? _beforePublish;

    private struct Node
    {
        public T? Item;
        public int Next;
    }

    private sealed class Stripe
    {
        public readonly Node[] Nodes;
        public long AvailableHead;
        public long FreeHead;
        public int Count;

        public Stripe(int capacity)
        {
            Nodes = new Node[capacity];
            Reset();
        }

        public void Reset()
        {
            for (var i = 0; i < Nodes.Length; i++)
            {
                Nodes[i].Item = null;
                Nodes[i].Next = i + 1 < Nodes.Length ? i + 1 : EmptyIndex;
            }

            Volatile.Write(ref AvailableHead, PackHead(0, EmptyIndex));
            Volatile.Write(ref FreeHead, PackHead(0, 0));
            Volatile.Write(ref Count, 0);
        }
    }

    /// <summary>
    /// Creates a new stack with the specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of items the stack can hold.</param>
    /// <param name="beforePublish">Optional test hook invoked after storing an item but before publishing it.</param>
    public LockFreeStack(int capacity, Action? beforePublish = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _beforePublish = beforePublish;

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

    private bool TryPush(Stripe stripe, T item)
    {
        if (!TryTakeNode(ref stripe.FreeHead, stripe.Nodes, out var nodeIndex))
            return false;

        Volatile.Write(ref stripe.Nodes[nodeIndex].Item, item);
        _beforePublish?.Invoke();
        Interlocked.Increment(ref stripe.Count);
        PublishNode(ref stripe.AvailableHead, stripe.Nodes, nodeIndex);
        return true;
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
        if (!TryTakeNode(ref stripe.AvailableHead, stripe.Nodes, out var nodeIndex))
        {
            item = null;
            return false;
        }

        item = Interlocked.Exchange(ref stripe.Nodes[nodeIndex].Item, null);
        Interlocked.Decrement(ref stripe.Count);
        PublishNode(ref stripe.FreeHead, stripe.Nodes, nodeIndex);
        return item is not null;
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
                count += Volatile.Read(ref stripe.Count);
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
            stripe.Reset();
    }

    private static bool TryTakeNode(ref long head, Node[] nodes, out int nodeIndex)
    {
        while (true)
        {
            var observedHead = Volatile.Read(ref head);
            nodeIndex = GetIndex(observedHead);
            if (nodeIndex == EmptyIndex)
                return false;

            var nextIndex = Volatile.Read(ref nodes[nodeIndex].Next);
            var updatedHead = NextHead(observedHead, nextIndex);
            if (Interlocked.CompareExchange(ref head, updatedHead, observedHead) == observedHead)
                return true;
        }
    }

    private static void PublishNode(ref long head, Node[] nodes, int nodeIndex)
    {
        while (true)
        {
            var observedHead = Volatile.Read(ref head);
            Volatile.Write(ref nodes[nodeIndex].Next, GetIndex(observedHead));
            var updatedHead = NextHead(observedHead, nodeIndex);
            if (Interlocked.CompareExchange(ref head, updatedHead, observedHead) == observedHead)
                return;
        }
    }

    private static long PackHead(int version, int index)
        => ((long)version << 32) | (uint)index;

    private static long NextHead(long observedHead, int index)
        => PackHead(unchecked((int)(observedHead >> 32) + 1), index);

    private static int GetIndex(long head)
        => (int)head;

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
            : (Thread.GetCurrentProcessorId() & int.MaxValue) % _stripes.Length;
}
