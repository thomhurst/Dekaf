using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Tooling;
using DekafProducer = Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Client;

/// <summary>
/// Acknowledged single-message producer latency comparisons.
/// </summary>
/// <remarks>
/// The zero-linger category is the like-for-like client comparison. The five-millisecond
/// category intentionally measures app-limited behavior: Dekaf sends a sole serial-awaited
/// record immediately, while librdkafka applies its configured linger.
/// </remarks>
[MemoryDiagnoser]
[ThroughputJob]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ProducerSingleBenchmarks
{
    private const string Topic = "benchmark-producer-single";

    private KafkaTestEnvironment _kafka = null!;
    private DekafProducer.IKafkaProducer<string, string> _dekafProducer = null!;
    private Confluent.Kafka.IProducer<string, string> _confluentProducer = null!;
    private string _messageValue = null!;

    [Params(100, 1000)]
    public int MessageSize { get; set; }

    [GlobalSetup(Targets = new[]
    {
        nameof(Confluent_ProduceSingleNoLinger),
        nameof(Dekaf_ProduceSingleNoLinger)
    })]
    public Task SetupNoLinger() => SetupAsync(lingerMs: 0);

    [GlobalCleanup(Targets = new[]
    {
        nameof(Confluent_ProduceSingleNoLinger),
        nameof(Dekaf_ProduceSingleNoLinger)
    })]
    public Task CleanupNoLinger() => CleanupAsync();

    [GlobalSetup(Targets = new[]
    {
        nameof(Confluent_ProduceSingleLinger5),
        nameof(Dekaf_ProduceSingleLinger5)
    })]
    public Task SetupLinger5() => SetupAsync(lingerMs: 5);

    [GlobalCleanup(Targets = new[]
    {
        nameof(Confluent_ProduceSingleLinger5),
        nameof(Dekaf_ProduceSingleLinger5)
    })]
    public Task CleanupLinger5() => CleanupAsync();

    private async Task SetupAsync(double lingerMs)
    {
        _kafka = await KafkaTestEnvironment.CreateAsync().ConfigureAwait(false);
        await _kafka.CreateTopicAsync(Topic, 3).ConfigureAwait(false);

        _messageValue = new string('x', MessageSize);

        _dekafProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId($"dekaf-single-linger-{lingerMs}")
            .WithAcks(DekafProducer.Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(lingerMs))
            .WithBatchSize(16384)
            .BuildAsync()
            .ConfigureAwait(false);

        _confluentProducer = new Confluent.Kafka.ProducerBuilder<string, string>(
            ConfluentBenchmarkConfigs.CreateProducerConfig(
                _kafka.BootstrapServers,
                $"confluent-single-linger-{lingerMs}",
                lingerMs,
                enableDeliveryReports: true))
            .Build();

        await WarmupAsync().ConfigureAwait(false);
    }

    private async Task WarmupAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            await _dekafProducer.ProduceAsync(Topic, "warmup", "warmup", CancellationToken.None)
                .ConfigureAwait(false);
            await _confluentProducer.ProduceAsync(Topic, new Confluent.Kafka.Message<string, string>
            {
                Key = "warmup",
                Value = "warmup"
            }).ConfigureAwait(false);
        }
    }

    private async Task CleanupAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await _dekafProducer.FlushAsync(cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Ignore flush errors during cleanup
        }

        _confluentProducer.Flush(TimeSpan.FromSeconds(60));
        await _dekafProducer.DisposeAsync().ConfigureAwait(false);
        _confluentProducer.Dispose();
        await _kafka.DisposeAsync().ConfigureAwait(false);
    }

    [BenchmarkCategory("SingleProduceNoLinger")]
    [Benchmark(Baseline = true)]
    public Task<Confluent.Kafka.DeliveryResult<string, string>> Confluent_ProduceSingleNoLinger()
        => _confluentProducer.ProduceAsync(Topic, new Confluent.Kafka.Message<string, string>
        {
            Key = "key",
            Value = _messageValue
        });

    [BenchmarkCategory("SingleProduceNoLinger")]
    [Benchmark]
    public ValueTask<DekafProducer.RecordMetadata> Dekaf_ProduceSingleNoLinger()
        => _dekafProducer.ProduceAsync(Topic, "key", _messageValue, CancellationToken.None);

    [BenchmarkCategory("SingleProduceLinger5")]
    [Benchmark(Baseline = true)]
    public Task<Confluent.Kafka.DeliveryResult<string, string>> Confluent_ProduceSingleLinger5()
        => _confluentProducer.ProduceAsync(Topic, new Confluent.Kafka.Message<string, string>
        {
            Key = "key",
            Value = _messageValue
        });

    [BenchmarkCategory("SingleProduceLinger5")]
    [Benchmark]
    public ValueTask<DekafProducer.RecordMetadata> Dekaf_ProduceSingleLinger5()
        => _dekafProducer.ProduceAsync(Topic, "key", _messageValue, CancellationToken.None);
}
