using System.Collections.Concurrent;

namespace Dekaf.SchemaRegistry;

internal sealed class SubjectSchemaIdCache
{
    // Match CachingStringKeyDeserializer: fixed topic sets stay cached,
    // dynamic topic names cannot grow without bound.
    internal const int MaxCachedEntries = 16_384;

    private readonly ConcurrentDictionary<SubjectSchemaIdCacheKey, SubjectSchemaIdCacheEntry> _cache = new();
    private int _cacheCount;
    private SubjectSchemaIdCacheEntry? _last;

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

    // Cheap membership check for async preparation: true once (topic, isKey) has a resolved schema ID,
    // meaning a subsequent synchronous GetOrAdd for it will hit the cache instead of fetching/blocking.
    internal bool Contains(string topic, bool isKey) =>
        TryGetCached(new SubjectSchemaIdCacheKey(topic, isKey), out _);

    // Shared lookup: the single-entry MRU (_last) fast-check followed by the concurrent dictionary.
    private bool TryGetCached(in SubjectSchemaIdCacheKey key, out SubjectSchemaIdCacheEntry entry)
    {
        var last = Volatile.Read(ref _last);
        if (last is not null && last.Key.Equals(key))
        {
            entry = last;
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

    internal int Cache(string topic, bool isKey, string subject, int schemaId, Schema? schema) =>
        Cache(new SubjectSchemaIdCacheKey(topic, isKey), subject, schemaId, schema).SchemaId;

    private SubjectSchemaIdCacheEntry Cache(SubjectSchemaIdCacheKey key, string? subject, int schemaId, Schema? schema)
    {
        if (_cache.TryGetValue(key, out var existing))
        {
            Volatile.Write(ref _last, existing);
            return existing;
        }

        var entry = new SubjectSchemaIdCacheEntry(key, subject, schemaId, schema);
        if (!TryReserveSlot())
        {
            Volatile.Write(ref _last, entry);
            return entry;
        }

        if (_cache.TryAdd(key, entry))
        {
            Volatile.Write(ref _last, entry);
            return entry;
        }

        Interlocked.Decrement(ref _cacheCount);
        if (_cache.TryGetValue(key, out existing))
        {
            Volatile.Write(ref _last, existing);
            return existing;
        }

        Volatile.Write(ref _last, entry);
        return entry;
    }

    private bool TryReserveSlot()
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

    internal sealed record SubjectSchemaIdCacheEntry(
        SubjectSchemaIdCacheKey Key,
        string? Subject,
        int SchemaId,
        Schema? Schema);
}
