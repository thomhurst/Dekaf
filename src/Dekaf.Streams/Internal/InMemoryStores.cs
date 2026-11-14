using System.Diagnostics.CodeAnalysis;

namespace Dekaf.Streams.Internal;

internal sealed class InMemoryStateStoreEngine : IStateStoreEngine
{
    public static InMemoryStateStoreEngine Instance { get; } = new();

    public IKeyValueBytesStore CreateKeyValueStore(StateStoreContext context) => new InMemoryKeyValueBytesStore();

    public IWindowBytesStore CreateWindowStore(StateStoreContext context) => new InMemoryWindowBytesStore();
}

/// <summary>Byte-array keys that can be looked up by span without allocating.</summary>
internal sealed class BytesComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
{
    public static BytesComparer Instance { get; } = new();

    public bool Equals(byte[]? x, byte[]? y) => x.AsSpan().SequenceEqual(y);

    public int GetHashCode([DisallowNull] byte[] obj) => GetHashCode(obj.AsSpan());

    public bool Equals(ReadOnlySpan<byte> alternate, byte[] other) => alternate.SequenceEqual(other);

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        var hash = new HashCode();
        hash.AddBytes(alternate);
        return hash.ToHashCode();
    }

    public byte[] Create(ReadOnlySpan<byte> alternate) => alternate.ToArray();
}

internal static class BytesDictionary
{
    public static Dictionary<byte[], byte[]> Create() => new(BytesComparer.Instance);

    /// <summary>
    /// Writes a value. A value of unchanged length overwrites the stored array, so a steady stream of
    /// fixed-size updates (counters, sums) allocates nothing.
    /// </summary>
    public static bool Put(Dictionary<byte[], byte[]> entries, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        var lookup = entries.GetAlternateLookup<ReadOnlySpan<byte>>();
        if (lookup.TryGetValue(key, out var existing))
        {
            if (existing.Length == value.Length)
            {
                value.CopyTo(existing);
            }
            else
            {
                lookup[key] = value.ToArray();
            }

            return false;
        }

        lookup[key] = value.ToArray();
        return true;
    }
}

internal sealed class InMemoryKeyValueBytesStore : IKeyValueBytesStore
{
    private readonly Dictionary<byte[], byte[]> _entries = BytesDictionary.Create();

    public long Count => _entries.Count;

    public bool TryGet(ReadOnlySpan<byte> key, out ReadOnlyMemory<byte> value)
    {
        if (_entries.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(key, out var bytes))
        {
            value = bytes;
            return true;
        }

        value = default;
        return false;
    }

    public void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value) => BytesDictionary.Put(_entries, key, value);

    public bool Delete(ReadOnlySpan<byte> key) => _entries.GetAlternateLookup<ReadOnlySpan<byte>>().Remove(key);

    public void Dispose() => _entries.Clear();
}

internal sealed class InMemoryWindowBytesStore : IWindowBytesStore
{
    // One dictionary per window start, ordered so closed windows are removed from the front.
    private readonly SortedList<long, Dictionary<byte[], byte[]>> _windows = [];
    private long _count;

    public long Count => _count;

    public bool TryGet(ReadOnlySpan<byte> key, long windowStart, out ReadOnlyMemory<byte> value)
    {
        if (_windows.TryGetValue(windowStart, out var entries)
            && entries.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(key, out var bytes))
        {
            value = bytes;
            return true;
        }

        value = default;
        return false;
    }

    public void Put(ReadOnlySpan<byte> key, long windowStart, ReadOnlySpan<byte> value)
    {
        if (!_windows.TryGetValue(windowStart, out var entries))
        {
            entries = BytesDictionary.Create();
            _windows.Add(windowStart, entries);
        }

        if (BytesDictionary.Put(entries, key, value))
        {
            _count++;
        }
    }

    public bool Delete(ReadOnlySpan<byte> key, long windowStart)
    {
        if (!_windows.TryGetValue(windowStart, out var entries)
            || !entries.GetAlternateLookup<ReadOnlySpan<byte>>().Remove(key))
        {
            return false;
        }

        _count--;
        if (entries.Count == 0)
        {
            _windows.Remove(windowStart);
        }

        return true;
    }

    public void RemoveThrough(long windowStart, IWindowEntryVisitor visitor)
    {
        while (_windows.Count > 0 && _windows.GetKeyAtIndex(0) <= windowStart)
        {
            var start = _windows.GetKeyAtIndex(0);
            var entries = _windows.GetValueAtIndex(0);
            _windows.RemoveAt(0);
            _count -= entries.Count;
            foreach (var entry in entries)
            {
                visitor.Visit(entry.Key, start, entry.Value);
            }
        }
    }

    public void Dispose()
    {
        _windows.Clear();
        _count = 0;
    }
}
