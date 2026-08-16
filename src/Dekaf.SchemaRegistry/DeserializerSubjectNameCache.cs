using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Dekaf.SchemaRegistry;

internal sealed class DeserializerSubjectNameCache
{
    private const int MaxCachedSubjectCount = 1024;

    private readonly SubjectNameStrategy _strategy;
    private readonly ISubjectNameStrategy? _customStrategy;
    private readonly bool _useLegacySubjectNames;
    private readonly ConditionalWeakTable<string, PerTopicSubjectNames> _subjectsByTopicIdentity = new();
    private readonly ConcurrentDictionary<CacheKey, string> _subjects = new();
    private readonly Queue<CacheKey> _subjectOrder = new(MaxCachedSubjectCount);
    private readonly object _subjectsLock = new();

    private DeserializerSubjectNameCache(
        SubjectNameStrategy strategy,
        ISubjectNameStrategy? customStrategy,
        bool useLegacySubjectNames)
    {
        _strategy = strategy;
        _customStrategy = customStrategy;
        _useLegacySubjectNames = useLegacySubjectNames;
    }

    internal static DeserializerSubjectNameCache? Create(SchemaRegistryDeserializerConfig? config)
        => config is null
            ? null
            : Create(
                config.SubjectNameStrategy,
                config.CustomSubjectNameStrategy,
                config.UseLegacySubjectNames);

    internal static DeserializerSubjectNameCache? Create(
        SubjectNameStrategy strategy,
        ISubjectNameStrategy? customStrategy,
        bool useLegacySubjectNames)
    {
        if (customStrategy is null && strategy == SubjectNameStrategy.TopicName)
            return null;

        return new DeserializerSubjectNameCache(strategy, customStrategy, useLegacySubjectNames);
    }

    internal string GetSubjectName(
        int schemaId,
        Schema? schema,
        string topic,
        bool isKey,
        string fallbackRecordName)
    {
        if (_subjectsByTopicIdentity.TryGetValue(topic, out var topicSubjects))
            return topicSubjects.GetSubjectName(this, schemaId, schema, topic, isKey, fallbackRecordName);

        var key = new CacheKey(schemaId, topic, isKey);
        if (_subjects.TryGetValue(key, out var subject))
            return subject;

        topicSubjects = _subjectsByTopicIdentity.GetValue(topic, static _ => new PerTopicSubjectNames());
        return topicSubjects.GetSubjectName(this, schemaId, schema, topic, isKey, fallbackRecordName);
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
        if (_subjects.TryGetValue(key, out var subject))
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
            if (_subjects.TryGetValue(key, out var subject))
            {
                added = false;
                return subject;
            }

            if (_subjectOrder.Count >= MaxCachedSubjectCount)
            {
                var evictedKey = _subjectOrder.Dequeue();
                _subjects.TryRemove(evictedKey, out _);
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

            _subjects[key] = subject;
            _subjectOrder.Enqueue(key);
            added = true;
            return subject;
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
