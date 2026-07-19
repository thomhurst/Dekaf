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
    internal const int ProbeLookupCount = 1_024;
    internal const int MinimumProbeHits = (ProbeLookupCount + 9) / 10;
    // The default cache's threshold-safe reuse scan can reach ~164K lookups. A 512K
    // bypass gives sustained unique traffic a greater than 3:1 bypass/probe duty cycle.
    internal const int BypassInterval = 512 * 1_024;

    private readonly ISerde<string> _configuredInner;
    // Swapping the existing cold-path target keeps Deserialize's cached-hit JIT shape unchanged.
    private readonly ISerde<string> _probeSerde;
    private readonly ISerde<string> _bypassSerde;
    private readonly int _configuredMaxCachedBytes;
    private readonly int _maxCachedEntries;
    private CacheGeneration _cache = new();
    private ISerde<string> _inner;
    private int _maxCachedBytes;
    private int _missesUntilProbe = AdmissionProbeLimit;
    private int _probeRemaining;
    private int _probeHits;
    private int _reuseProbeAdmissions;
    private int _minimumReuseProbeHits;
    private bool _isReuseProbe;
    private int _bypassRemaining;

    internal CachingStringDeserializer(
        ISerde<string> inner,
        int maxCachedBytes,
        int maxCachedEntries)
    {
        _configuredInner = inner;
        _inner = inner;
        _probeSerde = new ProbeSerde(this);
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
        var cache = Volatile.Read(ref _cache);

        if (cache.Entries.TryGetValue(hash, out var cachedValue))
            return cachedValue;

        var result = _inner.Deserialize(data, context);

        // Soft cap: concurrent threads may each read count < max and add simultaneously,
        // transiently overshooting by the number of racing threads. Bounded and acceptable.
        if (Volatile.Read(ref cache.Count) < _maxCachedEntries)
        {
            if (cache.Entries.TryAdd(hash, result))
                Interlocked.Increment(ref cache.Count);
        }

        if (ReferenceEquals(cache, Volatile.Read(ref _cache)))
            ObserveCacheMiss();

        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string DeserializeWhileBypassing(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        ObserveBypassLookup();
        return _configuredInner.Deserialize(data, context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ObserveBypassLookup()
    {
        var remaining = _bypassRemaining - 1;
        if (remaining <= 0)
        {
            _bypassRemaining = 0;
            StartPrimaryProbe();
        }
        else
        {
            _bypassRemaining = remaining;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string DeserializeWhileProbing(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        var hash = ComputeHash(data.Span);
        var cache = Volatile.Read(ref _cache);
        if (cache.Entries.TryGetValue(hash, out var cachedValue))
        {
            ObserveProbeLookup(hit: true);
            return cachedValue;
        }

        var result = _configuredInner.Deserialize(data, context);
        var admitted = false;
        if (Volatile.Read(ref cache.Count) < _maxCachedEntries)
        {
            if (cache.Entries.TryAdd(hash, result))
            {
                Interlocked.Increment(ref cache.Count);
                admitted = true;
            }
        }

        ObserveProbeLookup(hit: false, admitted);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ObserveProbeLookup(bool hit, bool admitted = false)
    {
        if (hit)
            _probeHits++;
        else if (_isReuseProbe && admitted)
            _reuseProbeAdmissions++;

        // A newly admitted entry could not hit on its first occurrence. Count that fill
        // once when evaluating a reuse window, without letting primary-probe fills pass.
        if (_isReuseProbe
            && (long)_probeHits + _reuseProbeAdmissions >= _minimumReuseProbeHits)
        {
            RestoreCache();
            return;
        }

        var remaining = _probeRemaining - 1;
        if (remaining > 0)
        {
            _probeRemaining = remaining;
            return;
        }

        if (!_isReuseProbe)
        {
            if (_probeHits >= MinimumProbeHits)
                RestoreCache();
            else
                StartReuseProbe();
            return;
        }

        StartBypass();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ObserveCacheMiss()
    {
        // Approximate by design: racing callers may slightly shift the probe and
        // bypass boundaries, but no synchronization belongs on this hot path.
        var remaining = _missesUntilProbe - 1;
        if (remaining <= 0)
        {
            _missesUntilProbe = AdmissionProbeLimit;
            StartPrimaryProbe();
            return;
        }

        _missesUntilProbe = remaining;
    }

    private void StartPrimaryProbe()
    {
        _probeRemaining = ProbeLookupCount;
        _probeHits = 0;
        _isReuseProbe = false;
        // These mode fields are deliberately lock-free. Racing callers may observe one
        // transition late, but every combination still deserializes correctly and the
        // next probe/bypass cycle self-corrects the approximate counters.
        _inner = _probeSerde;
        _maxCachedBytes = -1;
    }

    private void StartReuseProbe()
    {
        // A cache with the minimum accepted hit rate can take cache capacity divided by
        // that rate lookups to repeat. Observe that full cycle, plus the primary-probe
        // prefix, before declaring the retained generation cold.
        _probeRemaining = CalculateReuseProbeLookupCount(_maxCachedEntries);
        _probeHits = 0;
        _reuseProbeAdmissions = 0;
        _minimumReuseProbeHits = CalculateMinimumReuseProbeHits(_probeRemaining);
        _isReuseProbe = true;
    }

    internal static int CalculateReuseProbeLookupCount(int maxCachedEntries)
    {
        var maximumUsefulCycleLength =
            ((long)maxCachedEntries * ProbeLookupCount + MinimumProbeHits - 1)
            / MinimumProbeHits;
        var lookupCount = maximumUsefulCycleLength + ProbeLookupCount;
        return lookupCount >= int.MaxValue ? int.MaxValue : (int)lookupCount;
    }

    private static int CalculateMinimumReuseProbeHits(int lookupCount)
    {
        var minimumHits =
            ((long)lookupCount * MinimumProbeHits + ProbeLookupCount - 1)
            / ProbeLookupCount;
        return minimumHits >= int.MaxValue ? int.MaxValue : (int)minimumHits;
    }

    private void StartBypass()
    {
        // A full reuse window without a hit means the retained entries are cold. Retire
        // the generation in O(1); clearing every entry here would stall Deserialize.
        Interlocked.Exchange(ref _cache, new CacheGeneration());
        _bypassRemaining = BypassInterval;
        _inner = _bypassSerde;
    }

    private void RestoreCache()
    {
        _missesUntilProbe = AdmissionProbeLimit;
        _inner = _configuredInner;
        _maxCachedBytes = _configuredMaxCachedBytes;
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

    private sealed class CacheGeneration
    {
        public readonly ConcurrentDictionary<Hash128Key, string> Entries = new();
        public int Count;
    }

    private sealed class ProbeSerde(CachingStringDeserializer owner) : ISerde<string>
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
            {
                owner.ObserveProbeLookup(hit: false);
                return owner._configuredInner.Deserialize(data, context);
            }

            return owner.DeserializeWhileProbing(data, context);
        }
    }

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
            {
                owner.ObserveBypassLookup();
                return owner._configuredInner.Deserialize(data, context);
            }

            return owner.DeserializeWhileBypassing(data, context);
        }
    }
}
