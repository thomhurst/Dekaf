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

    public int GetOrAdd<TState>(
        string topic,
        bool isKey,
        TState state,
        Func<TState, string, bool, string> getSubjectName,
        Func<TState, string, int> getSchemaId)
    {
        var key = new SubjectSchemaIdCacheKey(topic, isKey);
        var last = Volatile.Read(ref _last);
        if (last is not null && last.Key.Equals(key))
            return last.SchemaId;

        if (_cache.TryGetValue(key, out var cached))
        {
            Volatile.Write(ref _last, cached);
            return cached.SchemaId;
        }

        var subject = getSubjectName(state, topic, isKey);
        var schemaId = getSchemaId(state, subject);
        return Cache(key, schemaId);
    }

    public int Cache(string topic, bool isKey, int schemaId) =>
        Cache(new SubjectSchemaIdCacheKey(topic, isKey), schemaId);

    private int Cache(SubjectSchemaIdCacheKey key, int schemaId)
    {
        if (_cache.TryGetValue(key, out var existing))
        {
            Volatile.Write(ref _last, existing);
            return existing.SchemaId;
        }

        var entry = new SubjectSchemaIdCacheEntry(key, schemaId);
        if (!TryReserveSlot())
        {
            Volatile.Write(ref _last, entry);
            return schemaId;
        }

        if (_cache.TryAdd(key, entry))
        {
            Volatile.Write(ref _last, entry);
            return schemaId;
        }

        Interlocked.Decrement(ref _cacheCount);
        if (_cache.TryGetValue(key, out existing))
        {
            Volatile.Write(ref _last, existing);
            return existing.SchemaId;
        }

        Volatile.Write(ref _last, entry);
        return schemaId;
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

    private readonly record struct SubjectSchemaIdCacheKey(string Topic, bool IsKey);

    private sealed record SubjectSchemaIdCacheEntry(SubjectSchemaIdCacheKey Key, int SchemaId);
}
