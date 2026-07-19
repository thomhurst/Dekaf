using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ShareConsumerRenewalBenchmarks
{
    private readonly KafkaShareConsumer<string, string> _consumer = new(
        new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "benchmark-share-group",
            AcknowledgementMode = ShareAcknowledgementMode.Explicit
        },
        Serializers.String,
        Serializers.String);

    private readonly ShareConsumeResult<string, string> _record = new()
    {
        Topic = "benchmark-topic",
        Partition = 0,
        Offset = 42,
        Value = "value",
        DeliveryCount = 1
    };

    [GlobalSetup]
    public void Setup() => _consumer.Acknowledge(_record, AcknowledgeType.Renew);

    [Benchmark]
    public void AcknowledgeExistingRenewal()
        => _consumer.Acknowledge(_record, AcknowledgeType.Renew);
}
