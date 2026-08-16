using BenchmarkDotNet.Attributes;
using Dekaf.SchemaRegistry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards topic-subject resolution used by rule-executor deserialization.
/// </summary>
[MemoryDiagnoser]
public class SchemaRegistrySubjectNameBenchmarks
{
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
}
