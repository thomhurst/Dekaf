using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Dekaf.SchemaRegistry;

internal sealed class DeserializerSubjectNameCache : IAssociatedNameCacheInvalidationTarget
{
    private const int MaxCachedSubjectCount = 1024;
    private const int MaxAssociatedNameInvalidationRetries = 4;

    private readonly SubjectNameStrategy _strategy;
    private readonly ISubjectNameStrategy? _customStrategy;
    private readonly IAsyncSubjectNameStrategy? _asyncStrategy;
    private readonly bool _useLegacySubjectNames;
    private readonly ConditionalWeakTable<string, PerTopicSubjectNames> _subjectsByTopicIdentity = new();
    private ConcurrentDictionary<CacheKey, string> _subjects = new();
    private Queue<CacheKey> _subjectOrder = new(MaxCachedSubjectCount);
    private readonly object _subjectsLock = new();
    private int _generation;

    private DeserializerSubjectNameCache(
        SubjectNameStrategy strategy,
        ISubjectNameStrategy? customStrategy,
        IAsyncSubjectNameStrategy? asyncStrategy,
        bool useLegacySubjectNames)
    {
        _strategy = strategy;
        _customStrategy = customStrategy;
        _asyncStrategy = asyncStrategy;
        _useLegacySubjectNames = useLegacySubjectNames;
        if (asyncStrategy is AssociatedNameStrategy associatedNameStrategy)
            associatedNameStrategy.RegisterCacheInvalidationTarget(this);
    }

    internal static DeserializerSubjectNameCache? Create(
        ISchemaRegistryClient schemaRegistry,
        SchemaRegistryDeserializerConfig? config)
        => config is null
            ? null
            : Create(
                schemaRegistry,
                config.SubjectNameStrategy,
                config.CustomSubjectNameStrategy,
                config.AsyncSubjectNameStrategy,
                config.UseLegacySubjectNames);

    internal static DeserializerSubjectNameCache? Create(SchemaRegistryDeserializerConfig? config)
    {
        if (config is null)
            return null;

        if (config.AsyncSubjectNameStrategy is not null
            || config is
            {
                CustomSubjectNameStrategy: null,
                SubjectNameStrategy: SubjectNameStrategy.AssociatedName
            })
        {
            throw new InvalidOperationException(
                "An asynchronous subject-name strategy requires a Schema Registry client.");
        }

        if (config.CustomSubjectNameStrategy is null
            && config.SubjectNameStrategy == SubjectNameStrategy.TopicName)
        {
            return null;
        }

        return new DeserializerSubjectNameCache(
            config.SubjectNameStrategy,
            config.CustomSubjectNameStrategy,
            asyncStrategy: null,
            config.UseLegacySubjectNames);
    }

    internal static DeserializerSubjectNameCache? Create(
        ISchemaRegistryClient schemaRegistry,
        SubjectNameStrategy strategy,
        ISubjectNameStrategy? customStrategy,
        IAsyncSubjectNameStrategy? asyncStrategy,
        bool useLegacySubjectNames)
    {
        ArgumentNullException.ThrowIfNull(schemaRegistry);
        if (customStrategy is not null)
            asyncStrategy = null;
        else if (asyncStrategy is null && strategy == SubjectNameStrategy.AssociatedName)
            asyncStrategy = new AssociatedNameStrategy(schemaRegistry);

        if (customStrategy is null
            && asyncStrategy is null
            && strategy == SubjectNameStrategy.TopicName)
        {
            return null;
        }

        return new DeserializerSubjectNameCache(
            strategy,
            customStrategy,
            asyncStrategy,
            useLegacySubjectNames);
    }

    internal bool RequiresPreparation => _asyncStrategy is not null;

    internal int Generation => Volatile.Read(ref _generation);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryReadSchemaId(ReadOnlyMemory<byte> data, out int schemaId)
    {
        var span = data.Span;
        if (span.Length >= 5 && span[0] == 0)
        {
            schemaId = BinaryPrimitives.ReadInt32BigEndian(span.Slice(1, 4));
            return true;
        }

        schemaId = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsPrepared(int schemaId, string topic, bool isKey) =>
        _asyncStrategy is null
        || Volatile.Read(ref _subjects).TryGetValue(new CacheKey(schemaId, topic, isKey), out _);

    internal ValueTask PrepareAsync(
        ISchemaRegistryClient schemaRegistry,
        int schemaId,
        string topic,
        bool isKey,
        string fallbackRecordName,
        CancellationToken cancellationToken,
        int? cacheSchemaId = null)
    {
        var key = new CacheKey(cacheSchemaId ?? schemaId, topic, isKey);
        var subjects = Volatile.Read(ref _subjects);
        if (_asyncStrategy is null
            || subjects.TryGetValue(key, out _))
        {
            return default;
        }

        return PrepareSlowAsync(
            schemaRegistry,
            schemaId,
            subjects,
            key,
            topic,
            isKey,
            fallbackRecordName,
            cancellationToken);
    }

    internal string GetSubjectName(
        int schemaId,
        Schema? schema,
        string topic,
        bool isKey,
        string fallbackRecordName)
    {
        if (_asyncStrategy is not null)
        {
            var preparedKey = new CacheKey(schemaId, topic, isKey);
            if (Volatile.Read(ref _subjects).TryGetValue(preparedKey, out var preparedSubject))
                return preparedSubject;

            throw new InvalidOperationException(
                "The asynchronous subject-name strategy must be prepared before deserialization.");
        }

        if (_subjectsByTopicIdentity.TryGetValue(topic, out var topicSubjects))
            return topicSubjects.GetSubjectName(this, schemaId, schema, topic, isKey, fallbackRecordName);

        var key = new CacheKey(schemaId, topic, isKey);
        if (Volatile.Read(ref _subjects).TryGetValue(key, out var subject))
            return subject;

        topicSubjects = _subjectsByTopicIdentity.GetValue(topic, static _ => new PerTopicSubjectNames());
        return topicSubjects.GetSubjectName(this, schemaId, schema, topic, isKey, fallbackRecordName);
    }

    private async ValueTask PrepareSlowAsync(
        ISchemaRegistryClient schemaRegistry,
        int schemaId,
        ConcurrentDictionary<CacheKey, string> subjects,
        CacheKey key,
        string topic,
        bool isKey,
        string fallbackRecordName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAssociatedNameInvalidationRetries; attempt++)
        {
            if (!ReferenceEquals(subjects, Volatile.Read(ref _subjects)))
                subjects = Volatile.Read(ref _subjects);

            if (subjects.TryGetValue(key, out _))
                return;

            var schema = await schemaRegistry.GetSchemaAsync(schemaId, cancellationToken).ConfigureAwait(false);
            var recordName = SubjectNameResolver.GetRecordName(schema, fallbackRecordName);
            var subject = await _asyncStrategy!.GetSubjectNameAsync(
                    topic,
                    recordName,
                    isKey,
                    cancellationToken)
                .ConfigureAwait(false);
            _ = await schemaRegistry.GetSchemaAsync(schemaId, subject, cancellationToken)
                .ConfigureAwait(false);
            if (AddPreparedSubject(subjects, key, subject))
                return;
        }

        throw new InvalidOperationException(
            "Associated-name cache changed repeatedly while preparing the deserializer.");
    }

    private bool AddPreparedSubject(
        ConcurrentDictionary<CacheKey, string> subjects,
        CacheKey key,
        string subject)
    {
        lock (_subjectsLock)
        {
            if (!ReferenceEquals(subjects, Volatile.Read(ref _subjects)))
                return false;

            if (subjects.TryGetValue(key, out _))
                return true;

            if (_subjectOrder.Count >= MaxCachedSubjectCount)
            {
                var evictedKey = _subjectOrder.Dequeue();
                subjects.TryRemove(evictedKey, out _);
            }

            subjects[key] = subject;
            _subjectOrder.Enqueue(key);
            return true;
        }
    }

    private string GetSubjectNameByValue(
        int schemaId,
        Schema? schema,
        string topic,
        bool isKey,
        string fallbackRecordName,
        out bool added)
    {
        var key = new CacheKey(schemaId, topic, isKey);
        if (Volatile.Read(ref _subjects).TryGetValue(key, out var subject))
        {
            added = false;
            return subject;
        }

        return AddSubject(key, schema, fallbackRecordName, out added);
    }

    private string AddSubject(
        CacheKey key,
        Schema? schema,
        string fallbackRecordName,
        out bool added)
    {
        lock (_subjectsLock)
        {
            var subjects = Volatile.Read(ref _subjects);
            if (subjects.TryGetValue(key, out var subject))
            {
                added = false;
                return subject;
            }

            if (_subjectOrder.Count >= MaxCachedSubjectCount)
            {
                var evictedKey = _subjectOrder.Dequeue();
                subjects.TryRemove(evictedKey, out _);
            }

            var recordName = schema is null
                ? fallbackRecordName
                : SubjectNameResolver.GetRecordName(schema, fallbackRecordName);
            subject = _customStrategy is not null
                ? _customStrategy.GetSubjectName(key.Topic, recordName, key.IsKey)
                : SubjectNameResolver.GetSubjectName(
                    _strategy,
                    key.Topic,
                    recordName,
                    key.IsKey,
                    _useLegacySubjectNames);

            subjects[key] = subject;
            _subjectOrder.Enqueue(key);
            added = true;
            return subject;
        }
    }

    void IAssociatedNameCacheInvalidationTarget.InvalidateAssociatedNameCache()
    {
        lock (_subjectsLock)
        {
            Interlocked.Increment(ref _generation);
            Volatile.Write(ref _subjects, new ConcurrentDictionary<CacheKey, string>());
            _subjectOrder = new Queue<CacheKey>(MaxCachedSubjectCount);
        }
    }

    private sealed class PerTopicSubjectNames
    {
        private const int MaxCachedSchemaCount = 64;

        private readonly ConcurrentDictionary<SchemaKey, string> _subjects = new();
        private readonly object _subjectsLock = new();

        internal string GetSubjectName(
            DeserializerSubjectNameCache owner,
            int schemaId,
            Schema? schema,
            string topic,
            bool isKey,
            string fallbackRecordName)
        {
            var key = new SchemaKey(schemaId, isKey);
            if (_subjects.TryGetValue(key, out var subject))
                return subject;

            subject = owner.GetSubjectNameByValue(
                schemaId,
                schema,
                topic,
                isKey,
                fallbackRecordName,
                out var added);
            return added ? TryAddSubject(key, subject) : subject;
        }

        private string TryAddSubject(SchemaKey key, string subject)
        {
            lock (_subjectsLock)
            {
                if (_subjects.TryGetValue(key, out var cachedSubject))
                    return cachedSubject;

                // Overflow remains in the bounded value cache instead of evicting an
                // identity entry and turning rotating schema IDs into lock contention.
                if (_subjects.Count >= MaxCachedSchemaCount)
                    return subject;

                _subjects[key] = subject;
                return subject;
            }
        }
    }

    private readonly record struct CacheKey(int SchemaId, string Topic, bool IsKey);
    private readonly record struct SchemaKey(int SchemaId, bool IsKey);
}
