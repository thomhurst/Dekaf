using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Hashing;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Dekaf.Serialization;

/// <summary>
/// String deserializer that caches bounded repeated strings to avoid per-message allocation.
/// </summary>
/// <remarks>
/// <para><b>Evidence-first activation:</b> The cache starts disabled. In the initial observe
/// mode every lookup is a plain decode plus a sampled hash probe into a recent-hash table;
/// the cache is enabled only once sampled reuse crosses the promotion threshold. In cached
/// mode each lookup window is scored by hits plus admissions, and the cache is demoted back
/// to observe mode when neither shows reuse. This keeps cold starts and high-cardinality key
/// streams (for example UUID keys) at plain-decode cost — the previous design admitted every
/// miss into the cache first and paid hash + dictionary insert per unique key before its
/// probe machinery could react.</para>
/// <para><b>Hash collision behavior:</b> Cached entries are keyed by a 128-bit hash without a
/// byte-level equality check. This avoids a second full pass over every cached payload. The
/// risk of returning the wrong value is acceptable because collisions in the ~2^128 hash
/// space are astronomically unlikely in practice. The observe-mode reuse table stores 32-bit
/// tags: a collision there only miscounts reuse evidence and can never surface a wrong value.</para>
/// <para><b>Thread safety:</b> Mode transitions and the observation counters are deliberately
/// lock-free and approximate. Racing callers may shift a window boundary or observe a mode
/// switch late, but every interleaving still deserializes correctly and the next window
/// self-corrects the counters.</para>
/// </remarks>
internal sealed class CachingStringDeserializer : ISerde<string>
{
    /// <summary>Observe mode samples one of every this-many cache-eligible lookups.</summary>
    internal const int ObserveSampleStride = 8;
    /// <summary>Maximum slots in the observe-mode recent-hash table. The table is sized to
    /// the cache capacity (rounded down to a power of two) so reuse evidence is only
    /// gathered for working sets the cache can actually hold; this cap bounds the table to
    /// 64 KB of <see cref="uint"/> tags, below the large-object heap threshold.</summary>
    internal const int ObserveTableMaxSlots = 16_384;
    /// <summary>Sampled lookups per observation window; hit evidence resets each window so
    /// sparse sub-threshold reuse can never accumulate into a promotion over time.</summary>
    internal const int ObserveWindowSamples = 1_024;
    /// <summary>Sampled hits within one window required to enable the cache (37.5% reuse).
    /// A direct-mapped table tops out near 40-50% sampled hits for a working set at table
    /// capacity, so a higher gate would reject bounded sets the cache serves well; below
    /// this rate the per-miss insert cost outweighs the per-hit allocation saving.</summary>
    internal const int PromoteSampledHits = 384;
    /// <summary>Cache-eligible lookups per cached-mode evaluation window.</summary>
    internal const int DemoteWindowLookups = 4_096;
    /// <summary>Minimum productive lookups (hits + admissions) per window to keep the cache
    /// enabled (25%; well under the promotion rate so borderline workloads do not flap).</summary>
    internal const int DemoteMinProductive = DemoteWindowLookups / 4;

    private readonly ISerde<string> _configuredInner;
    // Swapping the mode target keeps Deserialize's cached-hit JIT shape unchanged.
    private readonly ISerde<string> _observeSerde;
    private readonly int _configuredMaxCachedBytes;
    private readonly int _maxCachedEntries;
    private readonly uint[] _observeTable;
    private CacheGeneration _cache = new();
    private ISerde<string> _inner;
    private int _maxCachedBytes;
    private int _observeStride = 1;
    private int _observeSamples;
    private int _observeHits;
    private int _cachedWindowRemaining;
    private int _cachedMisses;
    private int _cachedFills;

    internal CachingStringDeserializer(
        ISerde<string> inner,
        int maxCachedBytes,
        int maxCachedEntries)
    {
        _configuredInner = inner;
        _observeSerde = new ObserveSerde(this);
        _configuredMaxCachedBytes = maxCachedBytes;
        _maxCachedEntries = maxCachedEntries;
        // Reuse-detection radius matches the cache capacity: evidence for cycles longer
        // than the cache could hold would promote a cache doomed to a sub-threshold hit
        // rate, demote it one window later, and flap between the modes indefinitely.
        var desiredSlots = Math.Clamp(maxCachedEntries, 2, ObserveTableMaxSlots);
        var slots = (int)BitOperations.RoundUpToPowerOf2((uint)desiredSlots);
        if (slots > desiredSlots)
            slots >>= 1;
        _observeTable = new uint[slots];
        // Start in observe mode: -1 routes every lookup through the observe serde.
        _inner = _observeSerde;
        _maxCachedBytes = -1;
    }

    /// <summary>Whether lookups are currently served from the hash cache.</summary>
    internal bool IsCacheEnabled => ReferenceEquals(_inner, _configuredInner);

    public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
        where TWriter : System.Buffers.IBufferWriter<byte>
#if !NETSTANDARD2_0
        , allows ref struct
#endif
    {
        _configuredInner.Serialize(value, ref destination, context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        if (!IsCacheEligible(data.Length, _maxCachedBytes))
            return _inner.Deserialize(data, context);

        return DeserializeWithCache(data, context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCacheEligible(int length, int maxCachedBytes) =>
        length != 0 && length <= maxCachedBytes;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string DeserializeWithCache(ReadOnlyMemory<byte> data, SerializationContext context)
    {
        // <= 0 rather than == 0: racing decrements can skip past zero, and stray
        // re-evaluations are benign while a stuck-negative counter would end scoring.
        if (--_cachedWindowRemaining <= 0 && EvaluateCachedWindow())
        {
            // Demoted on this call; decode plainly without an observe sample.
            return _configuredInner.Deserialize(data, context);
        }

        var hash = ComputeHash(data.Span);
        var cache = Volatile.Read(ref _cache);

        if (cache.Entries.TryGetValue(hash, out var cachedValue))
            return cachedValue;

        _cachedMisses++;
        var result = _configuredInner.Deserialize(data, context);

        // Soft cap: concurrent threads may each read count < max and add simultaneously,
        // transiently overshooting by the number of racing threads. Bounded and acceptable.
        if (Volatile.Read(ref cache.Count) < _maxCachedEntries
            && cache.Entries.TryAdd(hash, result))
        {
            Interlocked.Increment(ref cache.Count);
            _cachedFills++;
        }

        return result;
    }

    /// <summary>
    /// Closes a cached-mode window and decides whether reuse still justifies the cache.
    /// Admissions count as productive alongside hits so a legitimately promoted cache is
    /// never demoted while it is still filling. Returns true when the cache was demoted.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool EvaluateCachedWindow()
    {
        var productive = DemoteWindowLookups - _cachedMisses + _cachedFills;
        _cachedWindowRemaining = DemoteWindowLookups;
        _cachedMisses = 0;
        _cachedFills = 0;

        if (productive >= DemoteMinProductive)
            return false;

        // Retire the generation in O(1); clearing every entry here would stall Deserialize.
        // Racing callers still holding the old generation keep working against it.
        Interlocked.Exchange(ref _cache, new CacheGeneration());
        // Observe state resets on observe-mode entry so promotion needs fresh evidence.
        Array.Clear(_observeTable);
        _observeStride = 1;
        _observeSamples = 0;
        _observeHits = 0;
        _inner = _observeSerde;
        _maxCachedBytes = -1;
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SampleObservation(ReadOnlySpan<byte> data)
    {
        _observeStride = ObserveSampleStride;

        var hash = XxHash3.HashToUInt64(data);
        var table = _observeTable;
        // Slot from the low hash bits, tag from the high bits: a tag collision only
        // miscounts reuse evidence. Masking with the array's own length lets the JIT
        // drop the bounds checks (the length is a power of two by construction).
        var slot = (int)hash & (table.Length - 1);
        var tag = (uint)(hash >> 32);
        if (table[slot] == tag)
        {
            if (++_observeHits >= PromoteSampledHits)
            {
                Promote();
                return;
            }
        }
        else
        {
            table[slot] = tag;
        }

        if (++_observeSamples >= ObserveWindowSamples)
        {
            _observeSamples = 0;
            _observeHits = 0;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Promote()
    {
        _cachedWindowRemaining = DemoteWindowLookups;
        _cachedMisses = 0;
        _cachedFills = 0;
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

    private sealed class ObserveSerde(CachingStringDeserializer owner) : ISerde<string>
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
            // The stride gate runs before data.Span is materialized so the 7-of-8
            // non-sampled lookups pay only a decrement and branch on top of the decode.
            if (IsCacheEligible(data.Length, owner._configuredMaxCachedBytes)
                && --owner._observeStride <= 0)
            {
                owner.SampleObservation(data.Span);
            }

            return owner._configuredInner.Deserialize(data, context);
        }
    }
}
