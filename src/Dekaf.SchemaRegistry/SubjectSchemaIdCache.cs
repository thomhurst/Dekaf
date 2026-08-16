using System.Collections.Concurrent;

namespace Dekaf.SchemaRegistry;

internal sealed class SubjectSchemaIdCache
{
    // Match CachingStringDeserializer: fixed topic sets stay cached,
    // dynamic topic names cannot grow without bound.
    internal const int MaxCachedEntries = 16_384;

    private readonly ConcurrentDictionary<SubjectSchemaIdCacheKey, SubjectSchemaIdCacheEntry> _cache = new();
    private SubjectSchemaIdCacheEntry? _last;
    private OverflowEntries? _overflow;
    private int _cacheCount;

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
            entry = last;
            return true;
        }

        var overflow = Volatile.Read(ref _overflow);
        if (overflow?.Last is { } overflowLast && overflowLast.Key.Equals(key))
        {
            Volatile.Write(ref _last, overflowLast);
            entry = overflowLast;
            return true;
        }

        if (overflow?.Previous is { } overflowPrevious && overflowPrevious.Key.Equals(key))
        {
            Volatile.Write(ref _last, overflowPrevious);
            entry = overflowPrevious;
            return true;
        }

        if (_cache.TryGetValue(key, out var cached))
        {
            Volatile.Write(ref _last, cached);
            entry = cached;
            return true;
        }

        entry = null!;
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
            if (_cache.TryAdd(key, entry))
            {
                Volatile.Write(ref _last, entry);
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
        while (true)
        {
            var current = Volatile.Read(ref _overflow);
            if (current?.Last is { } last && last.Key.Equals(entry.Key))
            {
                Volatile.Write(ref _last, last);
                return last;
            }

            if (current?.Previous is { } previous && previous.Key.Equals(entry.Key))
            {
                Volatile.Write(ref _last, previous);
                return previous;
            }

            var updated = new OverflowEntries(entry, current?.Last);
            if (Interlocked.CompareExchange(ref _overflow, updated, current) == current)
            {
                Volatile.Write(ref _last, entry);
                return entry;
            }
        }
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

    internal sealed class SubjectSchemaIdCacheEntry(
        SubjectSchemaIdCacheKey key,
        string? subject,
        int schemaId,
        Schema? schema)
    {
        internal SubjectSchemaIdCacheKey Key { get; } = key;
        internal string? Subject { get; } = subject;
        internal int SchemaId { get; } = schemaId;
        internal Schema? Schema { get; } = schema;
    }

    private sealed class OverflowEntries(
        SubjectSchemaIdCacheEntry last,
        SubjectSchemaIdCacheEntry? previous)
    {
        internal SubjectSchemaIdCacheEntry Last { get; } = last;
        internal SubjectSchemaIdCacheEntry? Previous { get; } = previous;
    }
}
