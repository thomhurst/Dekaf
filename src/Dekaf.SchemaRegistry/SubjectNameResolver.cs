using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Dekaf.SchemaRegistry;

internal static class SubjectNameResolver
{
    private const int MaxCachedTopicCount = 1024;
    private static readonly ConditionalWeakTable<string, TopicSubjects> TopicSubjectsByIdentity = new();
    private static readonly ConcurrentDictionary<string, TopicSubjects> TopicSubjectNames =
        new(StringComparer.Ordinal);
    private static readonly Queue<string> TopicSubjectOrder = new(MaxCachedTopicCount);
    private static readonly object TopicSubjectNamesLock = new();

    internal static string GetTopicSubjectName(string topic, bool isKey)
    {
        var subjects = TopicSubjectsByIdentity.GetValue(topic, static value => GetOrAddTopicSubjects(value));
        return isKey ? subjects.Key : subjects.Value;
    }

    private static TopicSubjects GetOrAddTopicSubjects(string topic)
    {
        if (TopicSubjectNames.TryGetValue(topic, out var subjects))
            return subjects;

        lock (TopicSubjectNamesLock)
        {
            if (TopicSubjectNames.TryGetValue(topic, out subjects))
                return subjects;

            if (TopicSubjectOrder.Count >= MaxCachedTopicCount)
            {
                var evictedTopic = TopicSubjectOrder.Dequeue();
                TopicSubjectNames.TryRemove(evictedTopic, out _);
            }

            subjects = new TopicSubjects(topic);
            TopicSubjectNames[topic] = subjects;
            TopicSubjectOrder.Enqueue(topic);
            return subjects;
        }
    }

    internal static string GetSubjectName(
        SubjectNameStrategy strategy,
        string topic,
        string? recordName,
        bool isKey,
        bool useLegacySubjectNames)
    {
        var suffix = isKey ? "-key" : "-value";
        return strategy switch
        {
            SubjectNameStrategy.TopicName => topic + suffix,
            SubjectNameStrategy.RecordName => useLegacySubjectNames
                ? recordName + suffix
                : RequireRecordName(recordName, strategy),
            SubjectNameStrategy.TopicRecordName => useLegacySubjectNames
                ? $"{topic}-{recordName}{suffix}"
                : $"{topic}-{RequireRecordName(recordName, strategy)}",
            _ => topic + suffix
        };
    }

    internal static string GetRecordName(Schema schema, string fallback)
    {
        if (schema.SchemaType is not (SchemaType.Avro or SchemaType.Json))
            return fallback;

        try
        {
            using var document = JsonDocument.Parse(schema.SchemaString);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return fallback;

            if (schema.SchemaType == SchemaType.Json &&
                root.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(title.GetString()))
            {
                return title.GetString()!;
            }

            if (schema.SchemaType == SchemaType.Avro &&
                root.TryGetProperty("name", out var name) &&
                name.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(name.GetString()))
            {
                var recordName = name.GetString()!;
                if (recordName.Contains('.'))
                    return recordName;

                if (root.TryGetProperty("namespace", out var @namespace) &&
                    @namespace.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(@namespace.GetString()))
                {
                    return $"{@namespace.GetString()}.{recordName}";
                }

                return recordName;
            }
        }
        catch (JsonException)
        {
            // Schema Registry will report malformed schemas during lookup or registration.
        }

        return fallback;
    }

    private static string RequireRecordName(string? recordName, SubjectNameStrategy strategy)
    {
        if (!string.IsNullOrEmpty(recordName))
            return recordName;

        throw new InvalidOperationException($"{strategy} requires a fully-qualified record name.");
    }

    private sealed class TopicSubjects(string topic)
    {
        public string Key { get; } = topic + "-key";
        public string Value { get; } = topic + "-value";
    }
}
