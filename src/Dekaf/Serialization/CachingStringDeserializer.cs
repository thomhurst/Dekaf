using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Hashing;
using System.Runtime.CompilerServices;

namespace Dekaf.Serialization;

/// <summary>
/// String deserializer that caches bounded repeated strings to avoid per-message allocation.
/// </summary>
/// <remarks>
/// <para><b>Hash collision behavior:</b> A 128-bit hash is used as the cache key without a
/// byte-level equality check. This avoids a second full pass over every cached payload. The
/// risk of returning the wrong value is acceptable because collisions in the ~2^128 hash
/// space are astronomically unlikely in practice.</para>
/// </remarks>
internal sealed class CachingStringDeserializer : ISerde<string>
{
    private readonly ISerde<string> _inner;
    private readonly int _maxCachedBytes;
    private readonly int _maxCachedEntries;
    private readonly ConcurrentDictionary<Hash128Key, string> _cache = new();
    private int _cacheCount;

    internal CachingStringDeserializer(
        ISerde<string> inner,
        int maxCachedBytes,
        int maxCachedEntries)
    {
        _inner = inner;
        _maxCachedBytes = maxCachedBytes;
        _maxCachedEntries = maxCachedEntries;
    }

    public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
        where TWriter : System.Buffers.IBufferWriter<byte>
#if !NETSTANDARD2_0
        , allows ref struct
#endif
    {
        _inner.Serialize(value, ref destination, context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        if (data.Length == 0 || data.Length > _maxCachedBytes)
            return _inner.Deserialize(data, context);

        return DeserializeWithCache(data, context);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string DeserializeWithCache(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var span = data.Span;
        var hash = ComputeHash(span);

        if (_cache.TryGetValue(hash, out var cachedValue))
        {
            return cachedValue;
        }

        var result = _inner.Deserialize(data, context);

        // Soft cap: concurrent threads may each read count < max and add simultaneously,
        // transiently overshooting by the number of racing threads. Bounded and acceptable.
        if (Volatile.Read(ref _cacheCount) < _maxCachedEntries)
        {
            if (_cache.TryAdd(hash, result))
            {
                Interlocked.Increment(ref _cacheCount);
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Hash128Key ComputeHash(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[16];
        XxHash128.Hash(data, hash);
        return new Hash128Key(
            BinaryPrimitives.ReadUInt64LittleEndian(hash),
            BinaryPrimitives.ReadUInt64LittleEndian(hash[sizeof(ulong)..]));
    }

    private readonly record struct Hash128Key(ulong Low, ulong High);
}
