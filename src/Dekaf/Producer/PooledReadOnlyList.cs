using System.Buffers;

namespace Dekaf.Producer;

/// <summary>
/// Provides indexed access to an <see cref="IEnumerable{T}"/> without allocating a
/// heap-backed list when the source does not already support indexing.
/// </summary>
internal ref struct PooledReadOnlyList<T>
{
    private readonly IList<T>? _list;
    private T[]? _rentedArray;

    private PooledReadOnlyList(IList<T> list)
    {
        _list = list;
        _rentedArray = null;
        Count = list.Count;
    }

    private PooledReadOnlyList(T[]? rentedArray, int count)
    {
        _list = null;
        _rentedArray = rentedArray;
        Count = count;
    }

    public int Count { get; }

    public T this[int index] => _list is not null ? _list[index] : _rentedArray![index];

    public static PooledReadOnlyList<T> Rent(IEnumerable<T> source)
    {
        if (source is IList<T> list)
            return new PooledReadOnlyList<T>(list);

        T[]? buffer = null;
        var count = 0;

        try
        {
            foreach (var item in source)
            {
                if (buffer is null)
                {
                    buffer = ArrayPool<T>.Shared.Rent(16);
                }
                else if (count == buffer.Length)
                {
                    var expanded = ArrayPool<T>.Shared.Rent(checked(buffer.Length * 2));
                    buffer.AsSpan(0, count).CopyTo(expanded);
                    ArrayPool<T>.Shared.Return(buffer, clearArray: true);
                    buffer = expanded;
                }

                buffer[count++] = item;
            }

            return new PooledReadOnlyList<T>(buffer, count);
        }
        catch
        {
            if (buffer is not null)
                ArrayPool<T>.Shared.Return(buffer, clearArray: true);
            throw;
        }
    }

    public void Dispose()
    {
        var buffer = _rentedArray;
        if (buffer is null)
            return;

        _rentedArray = null;
        ArrayPool<T>.Shared.Return(buffer, clearArray: true);
    }
}
