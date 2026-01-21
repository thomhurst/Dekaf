using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using DekafLib = Dekaf;
using DekafProducer = Dekaf.Producer;

namespace Dekaf.Benchmarks;

/// <summary>
/// Benchmarks comparing Dekaf producer vs Confluent.Kafka producer.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 3, iterationCount: 10)]
public class ProducerBenchmarks
{
    private KafkaTestEnvironment _kafka = null!;
    private DekafProducer.IKafkaProducer<string, string> _dekafProducer = null!;
    private Confluent.Kafka.IProducer<string, string> _confluentProducer = null!;

    private const string Topic = "benchmark-producer";
    private string _messageValue = null!;

    [Params(100, 1000)]
    public int MessageSize { get; set; }

    [Params(100, 1000)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _kafka = await KafkaTestEnvironment.CreateAsync();
        await _kafka.CreateTopicAsync(Topic, 3);

        // Create message payload
        _messageValue = new string('x', MessageSize);

        // Setup Dekaf producer
        _dekafProducer = DekafLib.Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("dekaf-benchmark")
            .WithAcks(DekafProducer.Acks.Leader)
            .WithLingerMs(5)
            .WithBatchSize(16384)
            .Build();

        // Setup Confluent producer
        var confluentConfig = new Confluent.Kafka.ProducerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            ClientId = "confluent-benchmark",
            Acks = Confluent.Kafka.Acks.Leader,
            LingerMs = 5,
            BatchSize = 16384
        };
        _confluentProducer = new Confluent.Kafka.ProducerBuilder<string, string>(confluentConfig).Build();

        // Warm up
        await WarmupAsync();
    }

    private async Task WarmupAsync()
    {
        // Send a few messages to warm up connections
        for (var i = 0; i < 10; i++)
        {
            await _dekafProducer.ProduceAsync(new DekafProducer.ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = "warmup",
                Value = "warmup"
            });

            await _confluentProducer.ProduceAsync(Topic, new Confluent.Kafka.Message<string, string>
            {
                Key = "warmup",
                Value = "warmup"
            });
        }

        _confluentProducer.Flush(TimeSpan.FromSeconds(5));
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _dekafProducer.DisposeAsync();
        _confluentProducer.Dispose();
        await _kafka.DisposeAsync();
    }

    [Benchmark(Description = "Dekaf: Single Message Produce")]
    public async Task<DekafProducer.RecordMetadata> DekafSingleProduce()
    {
        return await _dekafProducer.ProduceAsync(new DekafProducer.ProducerMessage<string, string>
        {
            Topic = Topic,
            Key = "key",
            Value = _messageValue
        });
    }

    [Benchmark(Description = "Confluent: Single Message Produce")]
    public async Task<Confluent.Kafka.DeliveryResult<string, string>> ConfluentSingleProduce()
    {
        return await _confluentProducer.ProduceAsync(Topic, new Confluent.Kafka.Message<string, string>
        {
            Key = "key",
            Value = _messageValue
        });
    }

    [Benchmark(Description = "Dekaf: Batch Produce")]
    public async Task DekafBatchProduce()
    {
        var tasks = new List<ValueTask<DekafProducer.RecordMetadata>>(BatchSize);

        for (var i = 0; i < BatchSize; i++)
        {
            tasks.Add(_dekafProducer.ProduceAsync(new DekafProducer.ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = $"key-{i}",
                Value = _messageValue
            }));
        }

        foreach (var task in tasks)
        {
            await task;
        }
    }

    [Benchmark(Description = "Confluent: Batch Produce")]
    public async Task ConfluentBatchProduce()
    {
        var tasks = new List<Task<Confluent.Kafka.DeliveryResult<string, string>>>(BatchSize);

        for (var i = 0; i < BatchSize; i++)
        {
            tasks.Add(_confluentProducer.ProduceAsync(Topic, new Confluent.Kafka.Message<string, string>
            {
                Key = $"key-{i}",
                Value = _messageValue
            }));
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark(Description = "Dekaf: Fire-and-Forget Produce")]
    public void DekafFireAndForget()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            _ = _dekafProducer.ProduceAsync(new DekafProducer.ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = $"key-{i}",
                Value = _messageValue
            });
        }
    }

    [Benchmark(Description = "Confluent: Fire-and-Forget Produce")]
    public void ConfluentFireAndForget()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            _confluentProducer.Produce(Topic, new Confluent.Kafka.Message<string, string>
            {
                Key = $"key-{i}",
                Value = _messageValue
            });
        }
    }
}
