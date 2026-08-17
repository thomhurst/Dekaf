using System.Collections.Concurrent;

namespace Dekaf.SchemaRegistry;

internal sealed class SubjectSchemaIdCache
{
    internal const int FixedOverflowCapacity = 8;
    internal const int TurnoverCapacity = 4;

    // Match CachingStringDeserializer: fixed topic sets stay cached,
    // dynamic topic names cannot grow without bound.
    internal const int MaxCachedEntries = 16_384;

    private readonly ConcurrentDictionary<SubjectSchemaIdCacheKey, CachedEntry> _cache = new();
    private CachedEntry? _last;
    private CachedEntry? _overflowFirst;
    private CachedEntry? _overflowSecond;
    private CachedEntry? _overflowThird;
    private CachedEntry? _overflowFourth;
    private int _cacheCount;
    private int _turnoverCursor = -1;
    private readonly TurnoverEntry[] _turnover = CreateTurnoverSlots();
    private CachedEntry? _overflowFifth;
    private CachedEntry? _overflowSixth;
    private CachedEntry? _overflowSeventh;
    private CachedEntry? _overflowEighth;
    private int _turnoverCount;

    internal int CachedEntryCount => Volatile.Read(ref _cacheCount);

    internal SubjectSchemaIdCacheEntry GetOrAdd<TState>(
        string topic,
        bool isKey,
        TState state,
        Func<TState, string, bool, string> getSubjectName,
        Func<TState, string, SubjectSchemaIdCacheValue> getSchema)
    {
        var key = new SubjectSchemaIdCacheKey(topic, isKey);
        if (TryGetCached(key, out var cached))
            return cached;

        var subject = getSubjectName(state, topic, isKey);
        var schema = getSchema(state, subject);
        return Cache(key, subject, schema.SchemaId, schema.Schema);
    }

    internal SubjectSchemaIdCacheEntry GetOrAdd<TState>(
        string topic,
        bool isKey,
        TState state,
        Func<TState, string, bool, SubjectSchemaIdCacheEntry> resolve)
    {
        var key = new SubjectSchemaIdCacheKey(topic, isKey);
        if (TryGetCached(key, out var cached))
            return cached;

        var resolved = resolve(state, topic, isKey);
        return Cache(key, resolved.Subject, resolved.SchemaId, resolved.Schema);
    }

    internal bool TryGet(
        string topic,
        bool isKey,
        out SubjectSchemaIdCacheEntry entry) =>
        TryGetCached(new SubjectSchemaIdCacheKey(topic, isKey), out entry);

    // Preserve the four-slot fast path before the dictionary. Keep the secondary
    // and turnover tiers out of this method so common-path code size stays fixed.
    private bool TryGetCached(in SubjectSchemaIdCacheKey key, out SubjectSchemaIdCacheEntry entry)
    {
        var last = Volatile.Read(ref _last);
        if (last is not null && last.Key.Equals(key))
        {
            entry = last.Value;
            return true;
        }

        var overflowFirst = Volatile.Read(ref _overflowFirst);
        if (overflowFirst is not null && overflowFirst.Key.Equals(key))
        {
            Volatile.Write(ref _last, overflowFirst);
            entry = overflowFirst.Value;
            return true;
        }

        var overflowSecond = Volatile.Read(ref _overflowSecond);
        if (overflowSecond is not null && overflowSecond.Key.Equals(key))
        {
            Volatile.Write(ref _last, overflowSecond);
            entry = overflowSecond.Value;
            return true;
        }

        var overflowThird = Volatile.Read(ref _overflowThird);
        if (overflowThird is not null && overflowThird.Key.Equals(key))
        {
            Volatile.Write(ref _last, overflowThird);
            entry = overflowThird.Value;
            return true;
        }

        var overflowFourth = Volatile.Read(ref _overflowFourth);
        if (overflowFourth is not null && overflowFourth.Key.Equals(key))
        {
            Volatile.Write(ref _last, overflowFourth);
            entry = overflowFourth.Value;
            return true;
        }

        if (_cache.TryGetValue(key, out var cached))
        {
            Volatile.Write(ref _last, cached);
            entry = cached.Value;
            return true;
        }

        return TryGetSecondary(key, out entry);
    }

    private bool TryGetSecondary(
        in SubjectSchemaIdCacheKey key,
        out SubjectSchemaIdCacheEntry entry)
    {
        var overflowFifth = Volatile.Read(ref _overflowFifth);
        if (overflowFifth is not null && overflowFifth.Key.Equals(key))
        {
            Volatile.Write(ref _last, overflowFifth);
            entry = overflowFifth.Value;
            return true;
        }

        var overflowSixth = Volatile.Read(ref _overflowSixth);
        if (overflowSixth is not null && overflowSixth.Key.Equals(key))
        {
            Volatile.Write(ref _last, overflowSixth);
            entry = overflowSixth.Value;
            return true;
        }

        var overflowSeventh = Volatile.Read(ref _overflowSeventh);
        if (overflowSeventh is not null && overflowSeventh.Key.Equals(key))
        {
            Volatile.Write(ref _last, overflowSeventh);
            entry = overflowSeventh.Value;
            return true;
        }

        var overflowEighth = Volatile.Read(ref _overflowEighth);
        if (overflowEighth is not null && overflowEighth.Key.Equals(key))
        {
            Volatile.Write(ref _last, overflowEighth);
            entry = overflowEighth.Value;
            return true;
        }

        var hashCode = key.GetHashCode();
        for (var index = 0; index < TurnoverCapacity; index++)
        {
            if (_turnover[index].TryGet(key, hashCode, out entry))
                return true;
        }

        entry = default;
        return false;
    }

    internal SubjectSchemaIdCacheEntry CacheEntry(
        string topic,
        bool isKey,
        string subject,
        int schemaId,
        Schema schema) =>
        Cache(new SubjectSchemaIdCacheKey(topic, isKey), subject, schemaId, schema);

    private SubjectSchemaIdCacheEntry Cache(
        SubjectSchemaIdCacheKey key,
        string? subject,
        int schemaId,
        Schema? schema)
    {
        if (TryGetCached(key, out var existing))
            return existing;

        var entry = new SubjectSchemaIdCacheEntry(key, subject, schemaId, schema);
        if (TryReserveCacheSlot())
        {
            var cached = new CachedEntry(entry);
            if (_cache.TryAdd(key, cached))
            {
                Volatile.Write(ref _last, cached);
                return entry;
            }

            Interlocked.Decrement(ref _cacheCount);
            if (TryGetCached(key, out existing))
                return existing;
        }

        return PublishOverflow(entry);
    }

    private SubjectSchemaIdCacheEntry PublishOverflow(SubjectSchemaIdCacheEntry entry)
    {
        CachedEntry? candidate = null;
        if (TryPublishFixed(ref _overflowFirst, entry, ref candidate, out var cached) ||
            TryPublishFixed(ref _overflowSecond, entry, ref candidate, out cached) ||
            TryPublishFixed(ref _overflowThird, entry, ref candidate, out cached) ||
            TryPublishFixed(ref _overflowFourth, entry, ref candidate, out cached) ||
            TryPublishFixed(ref _overflowFifth, entry, ref candidate, out cached) ||
            TryPublishFixed(ref _overflowSixth, entry, ref candidate, out cached) ||
            TryPublishFixed(ref _overflowSeventh, entry, ref candidate, out cached) ||
            TryPublishFixed(ref _overflowEighth, entry, ref candidate, out cached))
        {
            return cached;
        }

        var hashCode = entry.Key.GetHashCode();
        if (Volatile.Read(ref _turnoverCount) >= TurnoverCapacity)
        {
            for (var index = 0; index < TurnoverCapacity; index++)
            {
                if (_turnover[index].TryGet(entry.Key, hashCode, out cached))
                    return cached;
            }

            var fullReplacementIndex = Interlocked.Increment(ref _turnoverCursor) & (TurnoverCapacity - 1);
            _turnover[fullReplacementIndex].Publish(entry, hashCode);
            return entry;
        }

        for (var index = 0; index < TurnoverCapacity; index++)
        {
            var turnover = _turnover[index];
            if (turnover.TryGet(entry.Key, hashCode, out cached))
                return cached;

            if (turnover.TryPublishEmpty(entry, hashCode))
            {
                Interlocked.Increment(ref _turnoverCount);
                return entry;
            }

            if (turnover.TryGet(entry.Key, hashCode, out cached))
                return cached;
        }

        var replacementIndex = Interlocked.Increment(ref _turnoverCursor) & (TurnoverCapacity - 1);
        _turnover[replacementIndex].Publish(entry, hashCode);
        return entry;
    }

    private bool TryPublishFixed(
        ref CachedEntry? slot,
        SubjectSchemaIdCacheEntry entry,
        ref CachedEntry? candidate,
        out SubjectSchemaIdCacheEntry cachedEntry)
    {
        var cached = Volatile.Read(ref slot);
        if (cached is null)
        {
            candidate ??= new CachedEntry(entry);
            cached = Interlocked.CompareExchange(ref slot, candidate, null);
            if (cached is null)
            {
                Volatile.Write(ref _last, candidate);
                cachedEntry = entry;
                return true;
            }
        }

        if (cached.Key.Equals(entry.Key))
        {
            Volatile.Write(ref _last, cached);
            cachedEntry = cached.Value;
            return true;
        }

        cachedEntry = default;
        return false;
    }

    private static TurnoverEntry[] CreateTurnoverSlots()
    {
        var slots = new TurnoverEntry[TurnoverCapacity];
        for (var index = 0; index < slots.Length; index++)
            slots[index] = new TurnoverEntry();

        return slots;
    }

    private bool TryReserveCacheSlot()
    {
        while (true)
        {
            var count = Volatile.Read(ref _cacheCount);
            if (count >= MaxCachedEntries)
                return false;

            if (Interlocked.CompareExchange(ref _cacheCount, count + 1, count) == count)
                return true;
        }
    }

    internal readonly record struct SubjectSchemaIdCacheKey(string Topic, bool IsKey);

    internal readonly record struct SubjectSchemaIdCacheValue(int SchemaId, Schema? Schema);

    internal readonly record struct SubjectSchemaIdCacheEntry(
        SubjectSchemaIdCacheKey Key,
        string? Subject,
        int SchemaId,
        Schema? Schema);

    private sealed class CachedEntry(SubjectSchemaIdCacheEntry value)
    {
        internal SubjectSchemaIdCacheKey Key => Value.Key;
        internal SubjectSchemaIdCacheEntry Value { get; } = value;
    }

    private sealed class TurnoverEntry
    {
        private SubjectSchemaIdCacheEntry _value;
        private int _hashCode;
        private int _version;

        internal bool TryGet(
            in SubjectSchemaIdCacheKey key,
            int hashCode,
            out SubjectSchemaIdCacheEntry value)
        {
            if (Volatile.Read(ref _hashCode) != hashCode)
            {
                value = default;
                return false;
            }

            while (true)
            {
                var version = Volatile.Read(ref _version);
                if (version == 0)
                {
                    value = default;
                    return false;
                }

                if ((version & 1) != 0)
                {
                    Thread.SpinWait(1);
                    continue;
                }

                var candidate = _value;
                if (version != Volatile.Read(ref _version))
                    continue;

                if (candidate.Key.Equals(key))
                {
                    value = candidate;
                    return true;
                }

                value = default;
                return false;
            }
        }

        internal void Publish(SubjectSchemaIdCacheEntry value, int hashCode)
        {
            while (true)
            {
                var version = Volatile.Read(ref _version);
                if ((version & 1) != 0 ||
                    Interlocked.CompareExchange(ref _version, unchecked(version + 1), version) != version)
                {
                    Thread.SpinWait(1);
                    continue;
                }

                _value = value;
                Volatile.Write(ref _hashCode, hashCode);
                var publishedVersion = unchecked(version + 2);
                Volatile.Write(ref _version, publishedVersion == 0 ? 2 : publishedVersion);
                return;
            }
        }

        internal bool TryPublishEmpty(SubjectSchemaIdCacheEntry value, int hashCode)
        {
            if (Interlocked.CompareExchange(ref _version, 1, 0) != 0)
                return false;

            _value = value;
            Volatile.Write(ref _hashCode, hashCode);
            Volatile.Write(ref _version, 2);
            return true;
        }
    }
}
