using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using Dekaf.Benchmarks.Infrastructure;
using DekafConsumer = Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Client;

/// <summary>
/// Consumer benchmarks comparing Dekaf vs Confluent.Kafka.
/// Confluent is marked as baseline for ratio comparison.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ConsumerBenchmarks
{
    private KafkaTestEnvironment _kafka = null!;
    private Confluent.Kafka.IProducer<string, string> _confluentProducer = null!;

    private const string TopicPrefix = "benchmark-consumer-";
    private const int PollSingleWarmupMessages = 1;
    private static readonly TimeSpan PollSingleTimeout = TimeSpan.FromSeconds(10);

    private string _topic = null!;
    private int _topicCounter;

    // Pre-created and primed consumers for warm PollSingle benchmarks.
    private Confluent.Kafka.IConsumer<string, string>? _confluentPollConsumer;
    private DekafConsumer.IKafkaConsumer<string, string>? _dekafPollConsumer;

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

    private void SeedTopic(int extraMessages = 0)
    {
        _topic = $"{TopicPrefix}{++_topicCounter}-{MessageCount}-{MessageSize}";
        _kafka.CreateTopicAsync(_topic, 1).GetAwaiter().GetResult();

        var value = new string('x', MessageSize);
        var totalMessages = MessageCount + extraMessages;
        for (var i = 0; i < totalMessages; i++)
        {
            _confluentProducer.Produce(_topic, new Confluent.Kafka.Message<string, string>
            {
                Key = $"key-{i}",
                Value = value
            });
        }
        _confluentProducer.Flush(TimeSpan.FromSeconds(30));
    }

    [IterationSetup(Targets = [nameof(Confluent_ConsumeAll), nameof(Dekaf_ConsumeAll)])]
    public void ConsumeAllIterationSetup() => SeedTopic();

    [IterationSetup(Targets = [nameof(Confluent_PollSingle), nameof(Dekaf_PollSingle)])]
    public void PollSingleIterationSetup()
    {
        SeedTopic(PollSingleWarmupMessages);

        _confluentPollConsumer = new Confluent.Kafka.ConsumerBuilder<string, string>(
            new Confluent.Kafka.ConsumerConfig
            {
                BootstrapServers = _kafka.BootstrapServers,
                ClientId = "confluent-poll-benchmark",
                GroupId = $"confluent-poll-{Guid.NewGuid():N}",
                AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest
            }).Build();
        _confluentPollConsumer.Subscribe(_topic);

        _dekafPollConsumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("dekaf-poll-benchmark")
            .WithGroupId($"dekaf-poll-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(DekafConsumer.AutoOffsetReset.Earliest)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        _dekafPollConsumer.Subscribe(_topic);

        PrimeConfluentPollConsumer();
        PrimeDekafPollConsumer().GetAwaiter().GetResult();
    }

    [IterationCleanup(Targets = [nameof(Confluent_PollSingle), nameof(Dekaf_PollSingle)])]
    public void PollSingleIterationCleanup()
    {
        _confluentPollConsumer?.Close();
        _confluentPollConsumer?.Dispose();
        _confluentPollConsumer = null;

        _dekafPollConsumer?.DisposeAsync().GetAwaiter().GetResult();
        _dekafPollConsumer = null;
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

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("dekaf-consumer-benchmark")
            .WithGroupId($"dekaf-benchmark-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(DekafConsumer.AutoOffsetReset.Earliest)
            .BuildAsync()
            .ConfigureAwait(false);

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
        return _confluentPollConsumer!.Consume(PollSingleTimeout);
    }

    [BenchmarkCategory("PollSingle")]
    [Benchmark]
    public async Task<DekafConsumer.ConsumeResult<string, string>?> Dekaf_PollSingle()
    {
        return await _dekafPollConsumer!.ConsumeOneAsync(PollSingleTimeout).ConfigureAwait(false);
    }

    private void PrimeConfluentPollConsumer()
    {
        var result = _confluentPollConsumer!.Consume(PollSingleTimeout);
        if (result is null)
        {
            throw new InvalidOperationException("Confluent poll consumer did not receive a warmup message.");
        }
    }

    private async Task PrimeDekafPollConsumer()
    {
        var result = await _dekafPollConsumer!.ConsumeOneAsync(PollSingleTimeout).ConfigureAwait(false);
        if (result is null)
        {
            throw new InvalidOperationException("Dekaf poll consumer did not receive a warmup message.");
        }
    }
}
