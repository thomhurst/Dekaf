using System.Collections.Concurrent;

namespace Dekaf.SchemaRegistry;

internal sealed class SubjectSchemaIdCache
{
    // Match CachingStringDeserializer: fixed topic sets stay cached,
    // dynamic topic names cannot grow without bound.
    internal const int MaxCachedEntries = 16_384;

    private readonly ConcurrentDictionary<SubjectSchemaIdCacheKey, SubjectSchemaIdCacheEntry> _cache = new();
    private SubjectSchemaIdCacheEntry? _last;
    private SubjectSchemaIdCacheEntry? _overflowLast;
    private SubjectSchemaIdCacheEntry? _overflowPrevious;
    private int _overflowVersion;
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

        ReadOverflowEntries(out var overflowLast, out var overflowPrevious);
        if (overflowLast is not null && overflowLast.Key.Equals(key))
        {
            Volatile.Write(ref _last, overflowLast);
            entry = overflowLast;
            return true;
        }

        if (overflowPrevious is not null && overflowPrevious.Key.Equals(key))
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
        var spinner = new SpinWait();
        while (true)
        {
            var version = Volatile.Read(ref _overflowVersion);
            if ((version & 1) != 0 ||
                Interlocked.CompareExchange(ref _overflowVersion, unchecked(version + 1), version) != version)
            {
                spinner.SpinOnce();
                continue;
            }

            var current = Volatile.Read(ref _overflowLast);
            if (current is not null && current.Key.Equals(entry.Key))
            {
                Volatile.Write(ref _last, current);
                Volatile.Write(ref _overflowVersion, unchecked(version + 2));
                return current;
            }

            var previous = Volatile.Read(ref _overflowPrevious);
            if (previous is not null && previous.Key.Equals(entry.Key))
            {
                Volatile.Write(ref _last, previous);
                Volatile.Write(ref _overflowVersion, unchecked(version + 2));
                return previous;
            }

            Volatile.Write(ref _overflowPrevious, current);
            Volatile.Write(ref _overflowLast, entry);
            Volatile.Write(ref _last, entry);
            Volatile.Write(ref _overflowVersion, unchecked(version + 2));
            return entry;
        }
    }

    private void ReadOverflowEntries(
        out SubjectSchemaIdCacheEntry? last,
        out SubjectSchemaIdCacheEntry? previous)
    {
        var spinner = new SpinWait();
        while (true)
        {
            var version = Volatile.Read(ref _overflowVersion);
            if ((version & 1) != 0)
            {
                spinner.SpinOnce();
                continue;
            }

            last = Volatile.Read(ref _overflowLast);
            previous = Volatile.Read(ref _overflowPrevious);
            if (version == Volatile.Read(ref _overflowVersion))
                return;

            spinner.SpinOnce();
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
}
