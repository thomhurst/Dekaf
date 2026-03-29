using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Dekaf.Serialization;

/// <summary>
/// Pooled read-only list of headers backed by an ArrayPool-rented array.
/// Eliminates per-message <c>new Header[n]</c> allocation by reusing both
/// the wrapper instance and the inner array from their respective pools.
/// </summary>
internal sealed class ReadOnlyHeaderList : IReadOnlyList<Header>
{
    private static readonly ConcurrentStack<ReadOnlyHeaderList> s_pool = new();
    private static int s_poolCount;
    private const int MaxPoolSize = 256;

    private Header[] _headers;
    private int _count;

    private ReadOnlyHeaderList()
    {
        _headers = [];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlyHeaderList? CreateOrNull(Header[]? sourceHeaders, int headerCount)
    {
        if (sourceHeaders is null || headerCount == 0)
            return null;

        var instance = Rent();

        if (instance._headers.Length < headerCount)
        {
            if (instance._headers.Length > 0)
                ArrayPool<Header>.Shared.Return(instance._headers, clearArray: true);
            instance._headers = ArrayPool<Header>.Shared.Rent(headerCount);
        }

        sourceHeaders.AsSpan(0, headerCount).CopyTo(instance._headers);
        instance._count = headerCount;

        return instance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlyHeaderList Rent()
    {
        if (s_pool.TryPop(out var instance))
        {
            Interlocked.Decrement(ref s_poolCount);
            return instance;
        }
        return new ReadOnlyHeaderList();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReturnToPool()
    {
        _headers.AsSpan(0, _count).Clear();
        _count = 0;

        if (Volatile.Read(ref s_poolCount) < MaxPoolSize)
        {
            s_pool.Push(this);
            Interlocked.Increment(ref s_poolCount);
        }
        else if (_headers.Length > 0)
        {
            ArrayPool<Header>.Shared.Return(_headers, clearArray: true);
            _headers = [];
        }
    }

    public int Count => _count;

    public Header this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);
            return _headers[index];
        }
    }

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<Header> IEnumerable<Header>.GetEnumerator() => new EnumeratorWrapper(this);

    IEnumerator IEnumerable.GetEnumerator() => new EnumeratorWrapper(this);

    /// <summary>
    /// Value-type enumerator to avoid allocation for foreach loops.
    /// </summary>
    public struct Enumerator
    {
        private readonly ReadOnlyHeaderList _list;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(ReadOnlyHeaderList list)
        {
            _list = list;
            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => ++_index < _list._count;

        public Header Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _list._headers[_index];
        }
    }

    /// <summary>
    /// Reference-type enumerator for IEnumerable interface (LINQ, explicit interface calls).
    /// </summary>
    private sealed class EnumeratorWrapper(ReadOnlyHeaderList list) : IEnumerator<Header>
    {
        private int _index = -1;

        public bool MoveNext() => ++_index < list._count;
        public Header Current => list._headers[_index];
        object IEnumerator.Current => Current;
        public void Reset() => _index = -1;
        public void Dispose() { }
    }
}
