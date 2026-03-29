using System.Collections.Concurrent;
using System.IO.Hashing;
using System.Runtime.CompilerServices;


namespace Dekaf.Serialization;

/// <summary>
/// String deserializer that caches small key strings to avoid per-message allocation.
/// Only used for key deserialization (not values). Bounded to prevent unbounded growth.
/// </summary>
/// <remarks>
/// <para><b>Hash collision behavior:</b> On a 64-bit hash collision, the first key to claim a
/// hash slot wins. The second key will always miss the cache (byte-level equality check fails)
/// and fall through to the inner deserializer, returning correct values but without caching
/// benefit. This is an acceptable tradeoff given the ~2^64 hash space makes collisions
/// astronomically unlikely in practice.</para>
/// </remarks>
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
        // This class is only constructed for key deserializers (in Builders.cs),
        // so no need to check context.Component here on the hot path.
        if (data.Length == 0 || data.Length > MaxCachedKeyBytes)
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

        // Soft cap: concurrent threads may each read count < max and add simultaneously,
        // transiently overshooting by the number of racing threads. Bounded and acceptable.
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
        return (long)XxHash64.HashToUInt64(data);
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
