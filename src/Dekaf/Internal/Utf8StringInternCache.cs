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
    private int _count;

    internal Utf8StringInternCache(int maxCachedEntries, int maxCachedBytes, Func<string, string>? canonicalize = null)
    {
        _maxCachedEntries = maxCachedEntries;
        _maxCachedBytes = maxCachedBytes;
        _canonicalize = canonicalize;
    }

    internal string Intern(ReadOnlyMemory<byte> utf8Bytes)
    {
        if (utf8Bytes.Length == 0)
            return string.Empty;

        var span = utf8Bytes.Span;
        if (utf8Bytes.Length > _maxCachedBytes)
            return Decode(span);

        var hash = XxHash64.HashToUInt64(span);
        if (_cache.TryGetValue(hash, out var entry) && entry.Matches(span))
            return entry.Value;

        var value = Decode(span);
        if (Volatile.Read(ref _count) >= _maxCachedEntries)
            return value;

        var newEntry = new CacheEntry(span.ToArray(), value);
        if (_cache.TryAdd(hash, newEntry))
        {
            Interlocked.Increment(ref _count);
        }
        else if (_cache.TryGetValue(hash, out entry) && entry.Matches(span))
        {
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

    private readonly struct CacheEntry(byte[] utf8Bytes, string value)
    {
        private readonly byte[] _utf8Bytes = utf8Bytes;
        internal string Value { get; } = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Matches(ReadOnlySpan<byte> utf8Bytes) => utf8Bytes.SequenceEqual(_utf8Bytes);
    }
}
