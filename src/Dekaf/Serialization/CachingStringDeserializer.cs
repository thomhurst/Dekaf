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
    internal const int AdmissionProbeLimit = 256;
    internal const int BypassInterval = 64 * 1_024;

    private readonly ISerde<string> _configuredInner;
    // Swapping the existing cold-path target keeps Deserialize's cached-hit JIT shape unchanged.
    private readonly ISerde<string> _bypassSerde;
    private readonly int _configuredMaxCachedBytes;
    private readonly int _maxCachedEntries;
    private readonly ConcurrentDictionary<Hash128Key, string> _cache = new();
    private ISerde<string> _inner;
    private int _maxCachedBytes;
    private int _cacheCount;
    private int _admissionsRemaining = AdmissionProbeLimit;
    private int _bypassRemaining;

    internal CachingStringDeserializer(
        ISerde<string> inner,
        int maxCachedBytes,
        int maxCachedEntries)
    {
        _configuredInner = inner;
        _inner = inner;
        _bypassSerde = new BypassSerde(this);
        _configuredMaxCachedBytes = maxCachedBytes;
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
            return cachedValue;

        var result = _inner.Deserialize(data, context);

        // Soft cap: concurrent threads may each read count < max and add simultaneously,
        // transiently overshooting by the number of racing threads. Bounded and acceptable.
        if (Volatile.Read(ref _cacheCount) < _maxCachedEntries)
        {
            if (_cache.TryAdd(hash, result))
            {
                Interlocked.Increment(ref _cacheCount);
                ObserveAdmission();
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string DeserializeWhileBypassing(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var remaining = _bypassRemaining - 1;
        if (remaining <= 0)
        {
            _bypassRemaining = 0;
            _inner = _configuredInner;
            _maxCachedBytes = _configuredMaxCachedBytes;
        }
        else
        {
            _bypassRemaining = remaining;
        }

        return _configuredInner.Deserialize(data, context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ObserveAdmission()
    {
        // Approximate by design: racing callers may slightly shift the probe and
        // bypass boundaries, but no synchronization belongs on this hot path.
        var remaining = _admissionsRemaining - 1;
        if (remaining <= 0)
        {
            _admissionsRemaining = AdmissionProbeLimit;
            _bypassRemaining = BypassInterval;
            _inner = _bypassSerde;
            _maxCachedBytes = -1;
            return;
        }

        _admissionsRemaining = remaining;
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

    private sealed class BypassSerde(CachingStringDeserializer owner) : ISerde<string>
    {
        public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
            where TWriter : System.Buffers.IBufferWriter<byte>
#if !NETSTANDARD2_0
            , allows ref struct
#endif
        {
            owner._configuredInner.Serialize(value, ref destination, context);
        }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            if (data.Length == 0 || data.Length > owner._configuredMaxCachedBytes)
                return owner._configuredInner.Deserialize(data, context);

            return owner.DeserializeWhileBypassing(data, context);
        }
    }
}
