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
    private int _topicIndex;

    [GlobalSetup]
    public void WarmCache()
    {
        SubjectNameResolver.GetTopicSubjectName(_equalTopics[0], isKey: true);
        SubjectNameResolver.GetTopicSubjectName(_equalTopics[0], isKey: false);
    }

    [Benchmark]
    public string ResolveFromDistinctEqualTopicInstance()
    {
        var topic = _equalTopics[_topicIndex++ & 1];
        return SubjectNameResolver.GetTopicSubjectName(topic, isKey: false);
    }
}
