using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using DekafLib = Dekaf;
using DekafConsumer = Dekaf.Consumer;

namespace Dekaf.Benchmarks;

/// <summary>
/// Benchmarks comparing Dekaf consumer vs Confluent.Kafka consumer.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 2, iterationCount: 5)]
public class ConsumerBenchmarks
{
    private KafkaTestEnvironment _kafka = null!;
    private Confluent.Kafka.IProducer<string, string> _confluentProducer = null!;

    private const string TopicPrefix = "benchmark-consumer-";
    private string _topic = null!;
    private int _topicCounter;

    [Params(100, 1000)]
    public int MessageCount { get; set; }

    [Params(100, 1000)]
    public int MessageSize { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _kafka = await KafkaTestEnvironment.CreateAsync();

        // Setup producer for seeding data
        var confluentConfig = new Confluent.Kafka.ProducerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            ClientId = "benchmark-seeder"
        };
        _confluentProducer = new Confluent.Kafka.ProducerBuilder<string, string>(confluentConfig).Build();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Create a fresh topic for each iteration to ensure clean state
        _topic = $"{TopicPrefix}{++_topicCounter}-{MessageCount}-{MessageSize}";
        _kafka.CreateTopicAsync(_topic, 1).GetAwaiter().GetResult();

        // Seed the topic with messages
        var value = new string('x', MessageSize);
        for (var i = 0; i < MessageCount; i++)
        {
            _confluentProducer.Produce(_topic, new Confluent.Kafka.Message<string, string>
            {
                Key = $"key-{i}",
                Value = value
            });
        }
        _confluentProducer.Flush(TimeSpan.FromSeconds(30));
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _confluentProducer.Dispose();
        await _kafka.DisposeAsync();
    }

    [Benchmark(Description = "Dekaf: Consume All Messages")]
    public async Task<int> DekafConsumeAll()
    {
        var count = 0;

        await using var consumer = DekafLib.Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("dekaf-consumer-benchmark")
            .WithGroupId($"dekaf-benchmark-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(DekafConsumer.AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(_topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            count++;
            if (count >= MessageCount)
                break;
        }

        return count;
    }

    [Benchmark(Description = "Confluent: Consume All Messages")]
    public int ConfluentConsumeAll()
    {
        var count = 0;

        var config = new Confluent.Kafka.ConsumerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            ClientId = "confluent-consumer-benchmark",
            GroupId = $"confluent-benchmark-{Guid.NewGuid():N}",
            AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new Confluent.Kafka.ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_topic);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (count < MessageCount && DateTime.UtcNow < deadline)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(5));
            if (result is not null)
            {
                count++;
            }
        }

        consumer.Close();
        return count;
    }

    [Benchmark(Description = "Dekaf: Poll Single Message")]
    public async Task<DekafConsumer.ConsumeResult<string, string>?> DekafPollSingle()
    {
        await using var consumer = DekafLib.Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("dekaf-poll-benchmark")
            .WithGroupId($"dekaf-poll-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(DekafConsumer.AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(_topic);

        return await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10));
    }

    [Benchmark(Description = "Confluent: Poll Single Message")]
    public Confluent.Kafka.ConsumeResult<string, string>? ConfluentPollSingle()
    {
        var config = new Confluent.Kafka.ConsumerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            ClientId = "confluent-poll-benchmark",
            GroupId = $"confluent-poll-{Guid.NewGuid():N}",
            AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest
        };

        using var consumer = new Confluent.Kafka.ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_topic);

        var result = consumer.Consume(TimeSpan.FromSeconds(10));
        consumer.Close();

        return result;
    }
}
