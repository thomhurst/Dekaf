using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ShareConsumerRenewalBenchmarks
{
    [Params(false, true)]
    public bool Hosted { get; set; }

    private Action<string, int, long> _removeRenewedRecord = null!;
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
    public void Setup()
    {
        if (Hosted)
            ((IHostedShareConsumer)_consumer).ObserveAcknowledgements(static _ => { });
        _consumer.Acknowledge(_record, AcknowledgeType.Renew);
        // Keep the dictionary allocated while cycling one record's renewal state.
        _consumer.Acknowledge(new ShareConsumeResult<string, string>
        {
            Topic = _record.Topic, Partition = _record.Partition, Offset = 43, Value = "sentinel", DeliveryCount = 1
        }, AcknowledgeType.Renew);
        _removeRenewedRecord = typeof(KafkaShareConsumer<string, string>)
            .GetMethod("RemoveRenewedRecord", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .CreateDelegate<Action<string, int, long>>(_consumer);
    }

    [Benchmark]
    public void AcknowledgeExistingRenewal()
        => _consumer.Acknowledge(_record, AcknowledgeType.Renew);

    // Includes the existing per-renewed-record state allocation; dictionary capacity is steady-state.
    [Benchmark]
    public void CreateRenewalState()
    {
        _removeRenewedRecord(_record.Topic, _record.Partition, _record.Offset);
        _consumer.Acknowledge(_record, AcknowledgeType.Renew);
    }
}
