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
    private InMemoryConsumer<Ignore, Ignore> _consumer = null!;
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
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
                OffsetCommitMode = OffsetCommitMode.Manual
            });
        _consumer.Subscribe(Topic);
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
}
