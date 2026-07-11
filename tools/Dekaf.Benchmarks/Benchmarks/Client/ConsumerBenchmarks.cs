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
/// <remarks>
/// The topic is seeded once per process in <c>[GlobalSetup]</c>; each iteration creates,
/// subscribes, and primes a fresh consumer (fresh group id, earliest reset) in
/// <c>[IterationSetup]</c> so it re-reads the same data and the timed region measures
/// steady-state consumption only. Including the lifecycle in the op would make both the
/// time and Allocated columns incomparable: Confluent's group join dominates its time
/// (~3s of waiting), and its session setup is native librdkafka memory that
/// MemoryDiagnoser cannot see, while Dekaf's is managed and fully visible.
/// Single-message poll benchmarks live in <see cref="ConsumerPollBenchmarks"/>.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ConsumerBenchmarks
{
    private KafkaTestEnvironment _kafka = null!;
    private Confluent.Kafka.IProducer<string, string> _confluentProducer = null!;

    private const string TopicPrefix = "benchmark-consumer-";
    private const int PrimeMessages = 1;
    private static readonly TimeSpan ConsumeTimeout = TimeSpan.FromSeconds(30);

    private string _topic = null!;

    private Confluent.Kafka.IConsumer<string, string>? _confluentConsumer;
    private DekafConsumer.IKafkaConsumer<string, string>? _dekafConsumer;
    private CancellationTokenSource? _dekafConsumeCts;

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

        SeedTopic();
    }

    // Mirrors ConsumerPollBenchmarks.SeedTopic; keep the two in sync.
    private void SeedTopic()
    {
        _topic = $"{TopicPrefix}{MessageCount}-{MessageSize}-{Guid.NewGuid():N}";
        _kafka.CreateTopicAsync(_topic, 1).GetAwaiter().GetResult();

        var value = new string('x', MessageSize);
        var totalMessages = MessageCount + PrimeMessages;
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

    [IterationSetup(Targets = [nameof(Confluent_ConsumeAll)])]
    public void ConfluentIterationSetup()
    {
        _confluentConsumer = new Confluent.Kafka.ConsumerBuilder<string, string>(
            new Confluent.Kafka.ConsumerConfig
            {
                BootstrapServers = _kafka.BootstrapServers,
                ClientId = "confluent-consumer-benchmark",
                GroupId = $"confluent-benchmark-{Guid.NewGuid():N}",
                AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            }).Build();
        _confluentConsumer.Subscribe(_topic);

        // Prime through group join and the first fetch so the timed region starts warm.
        if (_confluentConsumer.Consume(ConsumeTimeout) is null)
        {
            throw new InvalidOperationException("Confluent consumer did not receive a prime message.");
        }
    }

    [IterationSetup(Targets = [nameof(Dekaf_ConsumeAll)])]
    public void DekafIterationSetup()
    {
        _dekafConsumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("dekaf-consumer-benchmark")
            .WithGroupId($"dekaf-benchmark-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(DekafConsumer.AutoOffsetReset.Earliest)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
        _dekafConsumer.Subscribe(_topic);

        if (_dekafConsumer.ConsumeOneAsync(ConsumeTimeout).AsTask().GetAwaiter().GetResult() is null)
        {
            throw new InvalidOperationException("Dekaf consumer did not receive a prime message.");
        }

        // Created outside the timed op so its timer allocation doesn't skew the Allocated
        // column (the Confluent baseline's timeout budget is an allocation-free deadline).
        _dekafConsumeCts = new CancellationTokenSource(ConsumeTimeout);
    }

    [IterationCleanup(Targets = [nameof(Confluent_ConsumeAll)])]
    public void ConfluentIterationCleanup()
    {
        _confluentConsumer?.Close();
        _confluentConsumer?.Dispose();
        _confluentConsumer = null;
    }

    [IterationCleanup(Targets = [nameof(Dekaf_ConsumeAll)])]
    public void DekafIterationCleanup()
    {
        _dekafConsumer?.DisposeAsync().GetAwaiter().GetResult();
        _dekafConsumer = null;
        _dekafConsumeCts?.Dispose();
        _dekafConsumeCts = null;
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

        var deadline = DateTime.UtcNow + ConsumeTimeout;
        while (count < MessageCount && DateTime.UtcNow < deadline)
        {
            var result = _confluentConsumer!.Consume(TimeSpan.FromSeconds(5));
            if (result is not null)
            {
                count++;
            }
        }

        return count;
    }

    [BenchmarkCategory("ConsumeAll")]
    [Benchmark]
    public async Task<int> Dekaf_ConsumeAll()
    {
        var count = 0;

        await foreach (var result in _dekafConsumer!.ConsumeAsync(_dekafConsumeCts!.Token).ConfigureAwait(false))
        {
            count++;
            if (count >= MessageCount)
                break;
        }

        return count;
    }
}
