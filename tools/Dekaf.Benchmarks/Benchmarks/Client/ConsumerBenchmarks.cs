using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using Dekaf.Benchmarks.Infrastructure;
using DekafLib = Dekaf;
using DekafConsumer = Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Client;

/// <summary>
/// Consumer benchmarks comparing Dekaf vs Confluent.Kafka.
/// Confluent is marked as baseline for ratio comparison.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 2, iterationCount: 5)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
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
        _kafka = await KafkaTestEnvironment.CreateAsync().ConfigureAwait(false);

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
        _topic = $"{TopicPrefix}{++_topicCounter}-{MessageCount}-{MessageSize}";
        _kafka.CreateTopicAsync(_topic, 1).GetAwaiter().GetResult();

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
        await _kafka.DisposeAsync().ConfigureAwait(false);
    }

    // ===== Consume All Messages =====

    [BenchmarkCategory("ConsumeAll")]
    [Benchmark(Baseline = true)]
    public int Confluent_ConsumeAll()
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

    [BenchmarkCategory("ConsumeAll")]
    [Benchmark]
    public async Task<int> Dekaf_ConsumeAll()
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

        await foreach (var result in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            count++;
            if (count >= MessageCount)
                break;
        }

        return count;
    }

    // ===== Poll Single Message =====

    [BenchmarkCategory("PollSingle")]
    [Benchmark(Baseline = true)]
    public Confluent.Kafka.ConsumeResult<string, string>? Confluent_PollSingle()
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

    [BenchmarkCategory("PollSingle")]
    [Benchmark]
    public async Task<DekafConsumer.ConsumeResult<string, string>?> Dekaf_PollSingle()
    {
        await using var consumer = DekafLib.Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("dekaf-poll-benchmark")
            .WithGroupId($"dekaf-poll-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(DekafConsumer.AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(_topic);

        return await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
    }
}
