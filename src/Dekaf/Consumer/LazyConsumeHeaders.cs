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

    internal static IReadOnlyList<Header> Create(
        Header[]? pooledHeaders,
        int count,
        PendingFetchData? owner,
        int generation)
    {
        if (pooledHeaders is null || count == 0)
            return Array.Empty<Header>();

        if (owner is null)
            throw new ObjectDisposedException(nameof(Headers), StaleHeadersMessage);

        return new LazyConsumeHeaders(pooledHeaders, count, owner, generation);
    }

    internal static IReadOnlyList<Header> CreateSnapshot(IReadOnlyList<Header> headers)
    {
        if (headers.Count == 0)
            return Array.Empty<Header>();

        var snapshot = new Header[headers.Count];
        for (var index = 0; index < snapshot.Length; index++)
        {
            var header = headers[index];
            snapshot[index] = new Header(
                header.Key,
                header.IsValueNull ? null : header.Value.ToArray());
        }

        return snapshot;
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

        // Header values are slices of pooled record storage that the next fetch reuses, so copy
        // them into one exact-size array owned by the snapshot: one allocation per materialization.
        var pooledHeaders = _pooledHeaders.AsSpan(0, _count);
        var valueBytes = 0;
        foreach (ref readonly var header in pooledHeaders)
        {
            if (!header.IsValueNull)
                valueBytes += header.RawValue.Length;
        }

        var values = valueBytes == 0 ? Array.Empty<byte>() : GC.AllocateUninitializedArray<byte>(valueBytes);
        var snapshot = new Header[_count];
        var offset = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            ref readonly var header = ref pooledHeaders[index];
            var length = header.IsValueNull ? 0 : header.RawValue.Length;
            header.RawValue.Span[..length].CopyTo(values.AsSpan(offset));
            snapshot[index] = new Header(header.Key, values.AsMemory(offset, length), header.IsValueNull);
            offset += length;
        }

        // A copy that raced with disposal may hold another fetch's bytes; never publish it.
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
