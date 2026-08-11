using System.Buffers;
using System.Runtime.CompilerServices;

namespace Dekaf.Producer;

/// <summary>
/// Provides indexed access to an <see cref="IEnumerable{T}"/> without allocating a
/// heap-backed list when the source does not already support indexing.
/// </summary>
internal ref struct PooledReadOnlyList<T>
{
#if NET10_0_OR_GREATER
    private const int InlineCapacity = 16;
#else
    private const int InlineCapacity = 8;
#endif
    private const int InitialPooledCapacity = 128;

    private InlineBuffer _inlineBuffer;
    private T[]? _rentedArray;

    private PooledReadOnlyList(InlineBuffer inlineBuffer, T[]? rentedArray, int count)
    {
        _inlineBuffer = inlineBuffer;
        _rentedArray = rentedArray;
        Count = count;
    }

    public int Count { get; }

    public T this[int index] => _rentedArray is not null ? _rentedArray[index] : _inlineBuffer[index];

    public static PooledReadOnlyList<T> Rent(IEnumerable<T> source)
    {
        InlineBuffer inlineBuffer = default;
        T[]? rentedArray = null;
        var count = 0;

        try
        {
            foreach (var item in source)
            {
                if (rentedArray is null && count < InlineCapacity)
                {
                    inlineBuffer[count++] = item;
                    continue;
                }

                if (rentedArray is null)
                {
                    rentedArray = ArrayPool<T>.Shared.Rent(InitialPooledCapacity);
                    for (var i = 0; i < InlineCapacity; i++)
                        rentedArray[i] = inlineBuffer[i];
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

            return new PooledReadOnlyList<T>(inlineBuffer, rentedArray, count);
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
        if (buffer is null)
            return;

        _rentedArray = null;
        ArrayPool<T>.Shared.Return(buffer, clearArray: true);
    }

#if NET10_0_OR_GREATER
    [InlineArray(InlineCapacity)]
    private struct InlineBuffer
    {
        private T _element0;
    }
#else
    private struct InlineBuffer
    {
        private T _element0;
        private T _element1;
        private T _element2;
        private T _element3;
        private T _element4;
        private T _element5;
        private T _element6;
        private T _element7;

        public T this[int index]
        {
            readonly get => index switch
            {
                0 => _element0,
                1 => _element1,
                2 => _element2,
                3 => _element3,
                4 => _element4,
                5 => _element5,
                6 => _element6,
                7 => _element7,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };
            set
            {
                switch (index)
                {
                    case 0: _element0 = value; break;
                    case 1: _element1 = value; break;
                    case 2: _element2 = value; break;
                    case 3: _element3 = value; break;
                    case 4: _element4 = value; break;
                    case 5: _element5 = value; break;
                    case 6: _element6 = value; break;
                    case 7: _element7 = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }
    }
#endif
}
