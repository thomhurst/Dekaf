using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class ProduceTopicCorrelationBenchmarks
{
    private const string Topic = "benchmark-produce-topic";
    private static readonly Guid TopicId = new("00112233-4455-6677-8899-aabbccddeeff");
    private readonly Dictionary<BrokerSender.ProduceResponseKey, ProduceResponsePartitionData> _responses = [];
    private ProduceResponsePartitionData _response;

    [GlobalSetup]
    public void Setup()
    {
        _response = new ProduceResponsePartitionData
        {
            Index = 7,
            ErrorCode = ErrorCode.None,
            BaseOffset = 42
        };
    }

    [Benchmark(Baseline = true)]
    public bool CorrelateV12Name()
    {
        _responses.Clear();
        var key = BrokerSender.ProduceResponseKey.FromName(Topic, 7);
        _responses[key] = _response;
        return _responses.TryGetValue(key, out _);
    }

    [Benchmark]
    public bool CorrelateV13TopicId()
    {
        _responses.Clear();
        var key = BrokerSender.ProduceResponseKey.FromId(TopicId, 7);
        _responses[key] = _response;
        return _responses.TryGetValue(key, out _);
    }
}
