using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Serialization;
using Dekaf.Testing;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ShortRunJob]
public class InMemoryConsumerBenchmarks
{
    private const string Topic = "in-memory-consumer";
    private const string GroupId = "benchmark-group";
    private InMemoryConsumer<Ignore, Ignore> _consumer = null!;
    private InMemoryConsumer<Ignore, Ignore> _unrelatedFaultConsumer = null!;
    private ConsumeResult<Ignore, Ignore>? _result;

    [GlobalSetup]
    public void Setup()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<Ignore, Ignore>(cluster);
        producer.ProduceAsync(Topic, default, default).GetAwaiter().GetResult();
        _consumer = new InMemoryConsumer<Ignore, Ignore>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
                OffsetCommitMode = OffsetCommitMode.Manual
            });
        _consumer.Subscribe(Topic);

        var unrelatedFaultCluster = new InMemoryKafkaCluster();
        var unrelatedFaultProducer = new InMemoryProducer<Ignore, Ignore>(unrelatedFaultCluster);
        unrelatedFaultProducer.ProduceAsync(Topic, default, default).GetAwaiter().GetResult();
        unrelatedFaultCluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(
                KafkaFaultOperation.Fetch,
                topic: "other-topic",
                partition: 0,
                groupId: GroupId),
            new InvalidOperationException("unrelated"));
        _unrelatedFaultConsumer = new InMemoryConsumer<Ignore, Ignore>(
            unrelatedFaultCluster,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
                OffsetCommitMode = OffsetCommitMode.Manual
            });
        _unrelatedFaultConsumer.Subscribe(Topic);
    }

    [Benchmark]
    public void ConsumeOneNoFault()
    {
        _consumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
        var operation = _consumer.ConsumeOneAsync(TimeSpan.Zero);
        if (!operation.IsCompletedSuccessfully)
            throw new InvalidOperationException("No-fault consume did not complete synchronously.");

        _result = operation.Result;
    }

    [Benchmark]
    public void ConsumeOneUnrelatedFault()
    {
        _unrelatedFaultConsumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
        var operation = _unrelatedFaultConsumer.ConsumeOneAsync(TimeSpan.Zero);
        if (!operation.IsCompletedSuccessfully)
            throw new InvalidOperationException("Unrelated-fault consume did not complete synchronously.");

        _result = operation.Result;
    }
}
