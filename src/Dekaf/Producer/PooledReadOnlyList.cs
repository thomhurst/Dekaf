using System.Buffers;
using System.Runtime.CompilerServices;

namespace Dekaf.Producer;

/// <summary>
/// Buffers a non-indexed <see cref="IEnumerable{T}"/> for indexed access without
/// allocating a heap-backed list.
/// </summary>
internal ref struct PooledReadOnlyList<T>
{
    private const int InitialPooledCapacity = 128;

    private T? _firstItem;
    private T[]? _rentedArray;

    private PooledReadOnlyList(T? firstItem, T[]? rentedArray, int count)
    {
        _firstItem = firstItem;
        _rentedArray = rentedArray;
        Count = count;
    }

    public int Count { get; }

    public T this[int index] => _rentedArray is not null ? _rentedArray[index] : _firstItem!;

    public static PooledReadOnlyList<T> Rent(IEnumerable<T> source) => source switch
    {
        Queue<T> { Count: 1 } queue => new PooledReadOnlyList<T>(queue.Peek(), null, 1),
        Queue<T> queue => RentQueue(queue),
        Stack<T> { Count: 1 } stack => new PooledReadOnlyList<T>(stack.Peek(), null, 1),
        Stack<T> stack => RentStack(stack),
        LinkedList<T> { Count: 1 } linkedList => new PooledReadOnlyList<T>(linkedList.First!.Value, null, 1),
        LinkedList<T> linkedList => RentLinkedList(linkedList),
        HashSet<T> { Count: 1 } hashSet => RentEnumerator(hashSet.GetEnumerator()),
        HashSet<T> hashSet => RentHashSet(hashSet),
        SortedSet<T> { Count: 1 } sortedSet => new PooledReadOnlyList<T>(sortedSet.Min, null, 1),
        SortedSet<T> sortedSet => RentSortedSet(sortedSet),
        _ => RentEnumerator(source.GetEnumerator()),
    };

    private static PooledReadOnlyList<T> RentQueue(Queue<T> queue)
    {
        if (queue.Count == 0)
            return new PooledReadOnlyList<T>(default, null, 0);

        // Keep the concrete CopyTo call: dispatch through ICollection<T> measured 40 B/op.
        var count = queue.Count;
        var rentedArray = ArrayPool<T>.Shared.Rent(Math.Max(InitialPooledCapacity, count));
        try
        {
            queue.CopyTo(rentedArray, 0);
            return new PooledReadOnlyList<T>(default, rentedArray, count);
        }
        catch
        {
            Return(rentedArray, count);
            throw;
        }
    }

    private static PooledReadOnlyList<T> RentStack(Stack<T> stack)
    {
        if (stack.Count == 0)
            return new PooledReadOnlyList<T>(default, null, 0);

        // Keep the concrete CopyTo call: dispatch through IEnumerable<T> boxes Stack<T>.Enumerator.
        var count = stack.Count;
        var rentedArray = ArrayPool<T>.Shared.Rent(Math.Max(InitialPooledCapacity, count));
        try
        {
            stack.CopyTo(rentedArray, 0);
            return new PooledReadOnlyList<T>(default, rentedArray, count);
        }
        catch
        {
            Return(rentedArray, count);
            throw;
        }
    }

    private static PooledReadOnlyList<T> RentLinkedList(LinkedList<T> linkedList)
    {
        if (linkedList.Count == 0)
            return new PooledReadOnlyList<T>(default, null, 0);

        var count = linkedList.Count;
        var rentedArray = ArrayPool<T>.Shared.Rent(Math.Max(InitialPooledCapacity, count));
        try
        {
            linkedList.CopyTo(rentedArray, 0);
            return new PooledReadOnlyList<T>(default, rentedArray, count);
        }
        catch
        {
            Return(rentedArray, count);
            throw;
        }
    }

    private static PooledReadOnlyList<T> RentHashSet(HashSet<T> hashSet)
    {
        if (hashSet.Count == 0)
            return new PooledReadOnlyList<T>(default, null, 0);

        var count = hashSet.Count;
        var rentedArray = ArrayPool<T>.Shared.Rent(Math.Max(InitialPooledCapacity, count));
        try
        {
            hashSet.CopyTo(rentedArray, 0);
            return new PooledReadOnlyList<T>(default, rentedArray, count);
        }
        catch
        {
            Return(rentedArray, count);
            throw;
        }
    }

    private static PooledReadOnlyList<T> RentSortedSet(SortedSet<T> sortedSet)
    {
        if (sortedSet.Count == 0)
            return new PooledReadOnlyList<T>(default, null, 0);

        var count = sortedSet.Count;
        var rentedArray = ArrayPool<T>.Shared.Rent(Math.Max(InitialPooledCapacity, count));
        try
        {
            sortedSet.CopyTo(rentedArray, 0);
            return new PooledReadOnlyList<T>(default, rentedArray, count);
        }
        catch
        {
            Return(rentedArray, count);
            throw;
        }
    }

    private static PooledReadOnlyList<T> RentEnumerator<TEnumerator>(TEnumerator enumerator)
        where TEnumerator : IEnumerator<T>
    {
        T[]? rentedArray = null;
        T? firstItem = default;
        var count = 0;

        try
        {
            if (enumerator.MoveNext())
            {
                firstItem = enumerator.Current;
                count = 1;
                while (enumerator.MoveNext())
                {
                    var item = enumerator.Current;
                    if (rentedArray is null)
                    {
                        rentedArray = ArrayPool<T>.Shared.Rent(InitialPooledCapacity);
                        rentedArray[0] = firstItem!;
                    }
                    else if (count == rentedArray.Length)
                    {
                        var expanded = ArrayPool<T>.Shared.Rent(checked(rentedArray.Length * 2));
                        rentedArray.AsSpan(0, count).CopyTo(expanded);
                        Return(rentedArray, count);
                        rentedArray = expanded;
                    }

                    rentedArray[count++] = item;
                }
            }
        }
        catch
        {
            if (rentedArray is not null)
            {
                Return(rentedArray, count);
                rentedArray = null;
            }

            enumerator.Dispose();
            throw;
        }

        try
        {
            enumerator.Dispose();
        }
        catch
        {
            if (rentedArray is not null)
                Return(rentedArray, count);
            throw;
        }

        return new PooledReadOnlyList<T>(firstItem, rentedArray, count);
    }

    public void Dispose()
    {
        var buffer = _rentedArray;
        _firstItem = default;
        if (buffer is null)
            return;

        _rentedArray = null;
        Return(buffer, Count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Return(T[] buffer, int count)
    {
        // Sparse buckets clear live slots only; dense buckets use ArrayPool's optimized full clear.
        if (count >= buffer.Length / 2)
        {
            ArrayPool<T>.Shared.Return(buffer, clearArray: true);
            return;
        }

        buffer.AsSpan(0, count).Clear();
        ArrayPool<T>.Shared.Return(buffer);
    }
}
