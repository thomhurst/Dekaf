using System.Buffers;

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
        LinkedList<T> { Count: 1 } linkedList => new PooledReadOnlyList<T>(linkedList.First!.Value, null, 1),
        HashSet<T> { Count: 1 } hashSet => RentEnumerator(hashSet.GetEnumerator()),
        SortedSet<T> { Count: 1 } sortedSet => RentEnumerator(sortedSet.GetEnumerator()),
        ICollection<T> collection => RentCollection(collection),
        _ => RentEnumerator(source.GetEnumerator()),
    };

    private static PooledReadOnlyList<T> RentQueue(Queue<T> queue)
    {
        if (queue.Count == 0)
            return new PooledReadOnlyList<T>(default, null, 0);

        // Keep the concrete CopyTo call: dispatch through ICollection<T> measured 40 B/op.
        var rentedArray = ArrayPool<T>.Shared.Rent(Math.Max(InitialPooledCapacity, queue.Count));
        try
        {
            queue.CopyTo(rentedArray, 0);
            return new PooledReadOnlyList<T>(default, rentedArray, queue.Count);
        }
        catch
        {
            ArrayPool<T>.Shared.Return(rentedArray, clearArray: true);
            throw;
        }
    }

    private static PooledReadOnlyList<T> RentCollection(ICollection<T> collection)
    {
        if (collection.Count == 0)
            return new PooledReadOnlyList<T>(default, null, 0);

        var rentedArray = ArrayPool<T>.Shared.Rent(Math.Max(InitialPooledCapacity, collection.Count));
        try
        {
            collection.CopyTo(rentedArray, 0);
            return new PooledReadOnlyList<T>(default, rentedArray, collection.Count);
        }
        catch
        {
            ArrayPool<T>.Shared.Return(rentedArray, clearArray: true);
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
            while (enumerator.MoveNext())
            {
                var item = enumerator.Current;
                if (count == 0)
                {
                    firstItem = item;
                    count = 1;
                    continue;
                }

                if (rentedArray is null)
                {
                    rentedArray = ArrayPool<T>.Shared.Rent(InitialPooledCapacity);
                    rentedArray[0] = firstItem!;
                }
                else if (count == rentedArray.Length)
                {
                    var expanded = ArrayPool<T>.Shared.Rent(checked(rentedArray.Length * 2));
                    rentedArray.AsSpan(0, count).CopyTo(expanded);
                    ArrayPool<T>.Shared.Return(rentedArray, clearArray: true);
                    rentedArray = expanded;
                }

                rentedArray[count++] = item;
            }

            return new PooledReadOnlyList<T>(firstItem, rentedArray, count);
        }
        catch
        {
            if (rentedArray is not null)
                ArrayPool<T>.Shared.Return(rentedArray, clearArray: true);
            throw;
        }
        finally
        {
            enumerator.Dispose();
        }
    }

    public void Dispose()
    {
        var buffer = _rentedArray;
        _firstItem = default;
        if (buffer is null)
            return;

        _rentedArray = null;
        ArrayPool<T>.Shared.Return(buffer, clearArray: true);
    }
}
