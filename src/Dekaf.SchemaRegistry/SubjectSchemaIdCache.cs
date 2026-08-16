using System.Collections.Concurrent;

namespace Dekaf.SchemaRegistry;

internal sealed class SubjectSchemaIdCache
{
    private const int OverflowCapacity = 4;

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
    private int _overflowCursor = -1;

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

    // Shared lookup: the single-entry MRU (_last) fast-check followed by the concurrent dictionary.
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

    private SubjectSchemaIdCacheEntry Cache(SubjectSchemaIdCacheKey key, string? subject, int schemaId, Schema? schema)
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
        var first = Volatile.Read(ref _overflowFirst);
        if (first is null)
        {
            var candidate = new CachedEntry(entry);
            first = Interlocked.CompareExchange(ref _overflowFirst, candidate, null);
            if (first is null)
            {
                Volatile.Write(ref _last, candidate);
                return entry;
            }
        }

        if (first.Key.Equals(entry.Key))
        {
            Volatile.Write(ref _last, first);
            return first.Value;
        }

        var second = Volatile.Read(ref _overflowSecond);
        if (second is null)
        {
            var candidate = new CachedEntry(entry);
            second = Interlocked.CompareExchange(ref _overflowSecond, candidate, null);
            if (second is null)
            {
                Volatile.Write(ref _last, candidate);
                return entry;
            }
        }

        if (second.Key.Equals(entry.Key))
        {
            Volatile.Write(ref _last, second);
            return second.Value;
        }

        var third = Volatile.Read(ref _overflowThird);
        if (third is null)
        {
            var candidate = new CachedEntry(entry);
            third = Interlocked.CompareExchange(ref _overflowThird, candidate, null);
            if (third is null)
            {
                Volatile.Write(ref _last, candidate);
                return entry;
            }
        }

        if (third.Key.Equals(entry.Key))
        {
            Volatile.Write(ref _last, third);
            return third.Value;
        }

        var fourth = Volatile.Read(ref _overflowFourth);
        if (fourth is null)
        {
            var candidate = new CachedEntry(entry);
            fourth = Interlocked.CompareExchange(ref _overflowFourth, candidate, null);
            if (fourth is null)
            {
                Volatile.Write(ref _last, candidate);
                return entry;
            }
        }

        if (fourth.Key.Equals(entry.Key))
        {
            Volatile.Write(ref _last, fourth);
            return fourth.Value;
        }

        var replacement = new CachedEntry(entry);
        _ = (Interlocked.Increment(ref _overflowCursor) & (OverflowCapacity - 1)) switch
        {
            0 => Interlocked.Exchange(ref _overflowFirst, replacement),
            1 => Interlocked.Exchange(ref _overflowSecond, replacement),
            2 => Interlocked.Exchange(ref _overflowThird, replacement),
            _ => Interlocked.Exchange(ref _overflowFourth, replacement)
        };
        Volatile.Write(ref _last, replacement);
        return entry;
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
}
