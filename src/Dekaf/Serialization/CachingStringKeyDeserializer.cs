using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dekaf.Serialization;

/// <summary>
/// String deserializer that caches small key strings to avoid per-message allocation.
/// Only caches key deserialization (not values). Bounded to prevent unbounded growth.
/// </summary>
internal sealed class CachingStringKeyDeserializer : ISerde<string>
{
    private const int MaxCachedKeyBytes = 128;
    private const int MaxCachedEntries = 16_384;

    private readonly ISerde<string> _inner;
    private readonly ConcurrentDictionary<long, CacheEntry> _cache = new();
    private int _cacheCount;

    internal CachingStringKeyDeserializer(ISerde<string> inner)
    {
        _inner = inner;
    }

    public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
        where TWriter : System.Buffers.IBufferWriter<byte>, allows ref struct
    {
        _inner.Serialize(value, ref destination, context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        if (context.Component != SerializationComponent.Key || data.Length == 0 || data.Length > MaxCachedKeyBytes)
            return _inner.Deserialize(data, context);

        return DeserializeWithCache(data, context);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string DeserializeWithCache(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var span = data.Span;
        var hash = ComputeHash(span);

        // Lookup by hash; verify with byte-level equality on hit to handle collisions.
        if (_cache.TryGetValue(hash, out var entry) && entry.Matches(span))
        {
            return entry.Value;
        }

        var result = _inner.Deserialize(data, context);

        if (Volatile.Read(ref _cacheCount) < MaxCachedEntries)
        {
            var newEntry = new CacheEntry(span.ToArray(), result);
            if (_cache.TryAdd(hash, newEntry))
            {
                Interlocked.Increment(ref _cacheCount);
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ComputeHash(ReadOnlySpan<byte> data)
    {
        // Use two independent hash computations combined into a 64-bit key
        // to reduce collision probability. Length is mixed in for extra differentiation.
        var hc = new HashCode();
        hc.AddBytes(data);
        var h1 = hc.ToHashCode();
        return ((long)h1 << 32) | (uint)(data.Length * 397 ^ h1);
    }

    /// <summary>
    /// Stores the original UTF-8 bytes alongside the cached string for collision-safe equality.
    /// One allocation per unique key (amortized over millions of messages).
    /// </summary>
    private readonly struct CacheEntry(byte[] utf8Bytes, string value)
    {
        private readonly byte[] _utf8Bytes = utf8Bytes;
        public string Value { get; } = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(ReadOnlySpan<byte> data) => data.SequenceEqual(_utf8Bytes);
    }
}
