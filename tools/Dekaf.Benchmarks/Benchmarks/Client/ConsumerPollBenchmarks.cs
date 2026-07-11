using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Dekaf.Benchmarks.Infrastructure;
using DekafConsumer = Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Client;

/// <summary>
/// Steady-state single-message poll benchmarks comparing Dekaf vs Confluent.Kafka.
/// Confluent is marked as baseline for ratio comparison.
/// </summary>
/// <remarks>
/// The topic is seeded once per process in <c>[GlobalSetup]</c>; each iteration creates
/// and primes a fresh consumer (fresh group id, earliest reset) that re-reads it, then
/// measures <see cref="PollsPerIteration"/> consecutive polls. The explicit invocation count in
/// <see cref="PollJobConfig"/> is essential: with an <c>[IterationSetup]</c> present,
/// BenchmarkDotNet otherwise forces a single invocation per iteration, which measures
/// three cold single-shot samples per case — the call graph never reaches the tiered
/// compilation promotion threshold, so the managed poll path runs unoptimized Tier-0
/// code while Confluent's native librdkafka path is unaffected, and one scheduler
/// preemption distorts the whole statistic. Thousands of warm polls per iteration give
/// both clients Tier-1 code and real statistics.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(PollJobConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ConsumerPollBenchmarks
{
    private const int PollsPerIteration = 10_000;
    private const int PrimeMessages = 1;
    private const string TopicPrefix = "benchmark-poll-";
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(10);

    private sealed class PollJobConfig : ManualConfig
    {
        public PollJobConfig()
        {
            AddJob(Job.Default
                .WithStrategy(RunStrategy.Throughput)
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(3)
                .WithInvocationCount(PollsPerIteration)
                .WithUnrollFactor(1));
        }
    }

    private KafkaTestEnvironment _kafka = null!;
    private Confluent.Kafka.IProducer<string, string> _confluentProducer = null!;

    private string _topic = null!;

    private Confluent.Kafka.IConsumer<string, string>? _confluentPollConsumer;
    private DekafConsumer.IKafkaConsumer<string, string>? _dekafPollConsumer;

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

        SeedTopic();
    }

    // Mirrors ConsumerBenchmarks.SeedTopic; keep the two in sync.
    private void SeedTopic()
    {
        _topic = $"{TopicPrefix}{MessageSize}-{Guid.NewGuid():N}";
        _kafka.CreateTopicAsync(_topic, 1).GetAwaiter().GetResult();

        var value = new string('x', MessageSize);
        var totalMessages = PollsPerIteration + PrimeMessages;
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

    [IterationSetup(Targets = [nameof(Confluent_PollSingle)])]
    public void ConfluentIterationSetup()
    {
        _confluentPollConsumer = new Confluent.Kafka.ConsumerBuilder<string, string>(
            new Confluent.Kafka.ConsumerConfig
            {
                BootstrapServers = _kafka.BootstrapServers,
                ClientId = "confluent-poll-benchmark",
                GroupId = $"confluent-poll-{Guid.NewGuid():N}",
                AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest
            }).Build();
        _confluentPollConsumer.Subscribe(_topic);

        if (_confluentPollConsumer.Consume(PollTimeout) is null)
        {
            throw new InvalidOperationException("Confluent poll consumer did not receive a prime message.");
        }
    }

    [IterationSetup(Targets = [nameof(Dekaf_PollSingle)])]
    public void DekafIterationSetup()
    {
        _dekafPollConsumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("dekaf-poll-benchmark")
            .WithGroupId($"dekaf-poll-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(DekafConsumer.AutoOffsetReset.Earliest)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        _dekafPollConsumer.Subscribe(_topic);

        if (_dekafPollConsumer.ConsumeOneAsync(PollTimeout).AsTask().GetAwaiter().GetResult() is null)
        {
            throw new InvalidOperationException("Dekaf poll consumer did not receive a prime message.");
        }
    }

    [IterationCleanup(Targets = [nameof(Confluent_PollSingle)])]
    public void ConfluentIterationCleanup()
    {
        _confluentPollConsumer?.Close();
        _confluentPollConsumer?.Dispose();
        _confluentPollConsumer = null;
    }

    [IterationCleanup(Targets = [nameof(Dekaf_PollSingle)])]
    public void DekafIterationCleanup()
    {
        _dekafPollConsumer?.DisposeAsync().GetAwaiter().GetResult();
        _dekafPollConsumer = null;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _confluentProducer.Dispose();
        await _kafka.DisposeAsync().ConfigureAwait(false);
    }

    // ===== Poll Single Message =====

    [BenchmarkCategory("PollSingle")]
    [Benchmark(Baseline = true)]
    public Confluent.Kafka.ConsumeResult<string, string>? Confluent_PollSingle()
    {
        return _confluentPollConsumer!.Consume(PollTimeout);
    }

    [BenchmarkCategory("PollSingle")]
    [Benchmark]
    public ValueTask<DekafConsumer.ConsumeResult<string, string>?> Dekaf_PollSingle()
    {
        return _dekafPollConsumer!.ConsumeOneAsync(PollTimeout);
    }
}
