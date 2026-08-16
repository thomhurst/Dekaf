using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards topic-subject resolution used by rule-executor deserialization.
/// </summary>
[MemoryDiagnoser]
public class SchemaRegistrySubjectNameBenchmarks
{
    private const int HighCardinalityTopicCount = 2048;
    private readonly string[] _equalTopics =
    [
        new string("benchmark-topic".AsSpan()),
        new string("benchmark-topic".AsSpan())
    ];
    private readonly DeserializerSubjectNameCache _recordSubjectNames =
        DeserializerSubjectNameCache.Create(
            new SchemaRegistryDeserializerConfig
            {
                SubjectNameStrategy = SubjectNameStrategy.RecordName
            })!;
    private readonly Schema _schema = new()
    {
        SchemaType = SchemaType.Json,
        SchemaString = """{ "type": "string", "title": "BenchmarkRecord" }"""
    };
    private readonly string[] _highCardinalityTopics = CreateTopics();
    private int _topicIndex;

    [GlobalSetup]
    public void WarmCache()
    {
        SubjectNameResolver.GetTopicSubjectName(_equalTopics[0], isKey: true);
        SubjectNameResolver.GetTopicSubjectName(_equalTopics[0], isKey: false);
        _recordSubjectNames.GetSubjectName(
            schemaId: 1,
            _schema,
            _equalTopics[0],
            isKey: false,
            fallbackRecordName: "FallbackRecord");

        for (var i = 0; i < _highCardinalityTopics.Length; i++)
        {
            var topic = _highCardinalityTopics[i];
            SubjectNameResolver.GetTopicSubjectName(topic, isKey: false);
            _recordSubjectNames.GetSubjectName(
                schemaId: 1,
                _schema,
                topic,
                isKey: false,
                fallbackRecordName: "FallbackRecord");
        }
    }

    [Benchmark]
    public string ResolveFromDistinctEqualTopicInstance()
    {
        var topic = _equalTopics[_topicIndex++ & 1];
        return SubjectNameResolver.GetTopicSubjectName(topic, isKey: false);
    }

    [Benchmark]
    public string ResolveConfiguredSubjectFromDistinctEqualTopicInstance()
    {
        var topic = _equalTopics[_topicIndex++ & 1];
        return _recordSubjectNames.GetSubjectName(
            schemaId: 1,
            _schema,
            topic,
            isKey: false,
            fallbackRecordName: "FallbackRecord");
    }

    [Benchmark]
    public string CreateDistinctEqualTopicInstance() =>
        new("benchmark-topic".AsSpan());

    [Benchmark]
    public string ResolveConfiguredSubjectFromNewEqualTopicInstance()
    {
        var topic = new string("benchmark-topic".AsSpan());
        return _recordSubjectNames.GetSubjectName(
            schemaId: 1,
            _schema,
            topic,
            isKey: false,
            fallbackRecordName: "FallbackRecord");
    }

    [Benchmark]
    public string ResolveTopicSubjectAcrossMoreThanCacheCapacity()
    {
        var topic = _highCardinalityTopics[_topicIndex++ & (HighCardinalityTopicCount - 1)];
        return SubjectNameResolver.GetTopicSubjectName(topic, isKey: false);
    }

    [Benchmark]
    public string ResolveConfiguredSubjectAcrossMoreThanCacheCapacity()
    {
        var topic = _highCardinalityTopics[_topicIndex++ & (HighCardinalityTopicCount - 1)];
        return _recordSubjectNames.GetSubjectName(
            schemaId: 1,
            _schema,
            topic,
            isKey: false,
            fallbackRecordName: "FallbackRecord");
    }

    private static string[] CreateTopics()
    {
        var topics = new string[HighCardinalityTopicCount];
        for (var i = 0; i < topics.Length; i++)
            topics[i] = $"benchmark-topic-{i}";

        return topics;
    }
}
