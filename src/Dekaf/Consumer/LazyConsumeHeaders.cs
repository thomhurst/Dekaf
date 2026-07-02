using System.Collections;
using Dekaf.Serialization;

namespace Dekaf.Consumer;

/// <summary>
/// Exposes pooled record headers without copying until the caller reads header values.
/// </summary>
internal sealed class LazyConsumeHeaders : IReadOnlyList<Header>
{
    private readonly Header[] _pooledHeaders;
    private readonly int _count;
    private readonly PendingFetchData _owner;
    private readonly int _generation;
    private Header[]? _snapshot;
    private const string StaleHeadersMessage = "Consumer headers were not materialized before the owning fetch batch was disposed.";

    private LazyConsumeHeaders(Header[] pooledHeaders, int count, PendingFetchData owner, int generation)
    {
        _pooledHeaders = pooledHeaders;
        _count = count;
        _owner = owner;
        _generation = generation;
    }

    internal static IReadOnlyList<Header>? Create(
        Header[]? pooledHeaders,
        int count,
        PendingFetchData? owner,
        int generation)
    {
        if (pooledHeaders is null || count == 0)
            return null;

        if (owner is null)
            throw new ObjectDisposedException(nameof(Headers), StaleHeadersMessage);

        return new LazyConsumeHeaders(pooledHeaders, count, owner, generation);
    }

    public int Count => _count;

    public Header this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return GetSnapshot()[index];
        }
    }

    public IEnumerator<Header> GetEnumerator() => ((IEnumerable<Header>)GetSnapshot()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private Header[] GetSnapshot()
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot is not null)
            return snapshot;

        return MaterializeSnapshot();
    }

    private Header[] MaterializeSnapshot()
    {
        ThrowIfStale();

        var snapshot = new Header[_count];
        _pooledHeaders.AsSpan(0, _count).CopyTo(snapshot);

        ThrowIfStale();

        var published = Interlocked.CompareExchange(ref _snapshot, snapshot, null);
        return published ?? snapshot;
    }

    private void ThrowIfStale()
    {
        if (!_owner.IsHeaderGenerationActive(_generation))
            throw new ObjectDisposedException(nameof(Headers), StaleHeadersMessage);
    }
}
