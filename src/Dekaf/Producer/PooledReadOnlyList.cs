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

    public static PooledReadOnlyList<T> Rent(IEnumerable<T> source)
    {
        T[]? rentedArray = null;
        T? firstItem = default;
        var count = 0;

        try
        {
            foreach (var item in source)
            {
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
