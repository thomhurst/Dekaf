using System.Collections.Concurrent;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dekaf.Internal;

/// <summary>
/// Bounded UTF-8 byte-keyed string intern cache.
/// Avoids allocating a string when repeated protocol names are already cached.
/// </summary>
internal sealed class Utf8StringInternCache
{
    private readonly ConcurrentDictionary<ulong, CacheEntry> _cache = new();
    private readonly int _maxCachedEntries;
    private readonly int _maxCachedBytes;
    private readonly Func<string, string>? _canonicalize;
    private readonly bool _cacheLastEntry;
    private CacheEntry? _lastEntry;
    private int _count;

    internal Utf8StringInternCache(
        int maxCachedEntries,
        int maxCachedBytes,
        Func<string, string>? canonicalize = null,
        bool cacheLastEntry = false)
    {
        _maxCachedEntries = maxCachedEntries;
        _maxCachedBytes = maxCachedBytes;
        _canonicalize = canonicalize;
        _cacheLastEntry = cacheLastEntry;
    }

    internal string Intern(ReadOnlyMemory<byte> utf8Bytes)
        => Intern(utf8Bytes.Span);

    internal string Intern(ReadOnlySpan<byte> utf8Bytes)
    {
        if (utf8Bytes.Length == 0)
            return string.Empty;

        if (utf8Bytes.Length > _maxCachedBytes)
            return Decode(utf8Bytes);

        if (_cacheLastEntry)
        {
            var lastEntry = Volatile.Read(ref _lastEntry);
            if (lastEntry is not null && lastEntry.Matches(utf8Bytes))
                return lastEntry.Value;
        }

        var hash = XxHash64.HashToUInt64(utf8Bytes);
        if (_cache.TryGetValue(hash, out var entry) && entry.Matches(utf8Bytes))
        {
            if (_cacheLastEntry)
                Volatile.Write(ref _lastEntry, entry);
            return entry.Value;
        }

        var value = Decode(utf8Bytes);
        if (Volatile.Read(ref _count) >= _maxCachedEntries)
            return value;

        var newEntry = new CacheEntry(utf8Bytes.ToArray(), value);
        if (_cache.TryAdd(hash, newEntry))
        {
            Interlocked.Increment(ref _count);
            if (_cacheLastEntry)
                Volatile.Write(ref _lastEntry, newEntry);
        }
        else if (_cache.TryGetValue(hash, out entry) && entry.Matches(utf8Bytes))
        {
            if (_cacheLastEntry)
                Volatile.Write(ref _lastEntry, entry);
            return entry.Value;
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string Decode(ReadOnlySpan<byte> utf8Bytes)
    {
        var value = Encoding.UTF8.GetString(utf8Bytes);
        return _canonicalize is null ? value : _canonicalize(value);
    }

    private sealed class CacheEntry(byte[] utf8Bytes, string value)
    {
        private readonly byte[] _utf8Bytes = utf8Bytes;
        internal string Value { get; } = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Matches(ReadOnlySpan<byte> utf8Bytes) => utf8Bytes.SequenceEqual(_utf8Bytes);
    }
}
