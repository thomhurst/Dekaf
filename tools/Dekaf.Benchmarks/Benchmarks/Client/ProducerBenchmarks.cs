using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using Dekaf.Benchmarks.Infrastructure;
using DekafLib = Dekaf;
using DekafProducer = Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Client;

/// <summary>
/// Producer benchmarks comparing Dekaf vs Confluent.Kafka.
/// Confluent is marked as baseline for ratio comparison.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 3, iterationCount: 10)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
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
        _kafka = await KafkaTestEnvironment.CreateAsync().ConfigureAwait(false);
        await _kafka.CreateTopicAsync(Topic, 3).ConfigureAwait(false);

        _messageValue = new string('x', MessageSize);

        _dekafProducer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("dekaf-benchmark")
            .WithAcks(DekafProducer.Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithBatchSize(16384)
            .Build();

        var confluentConfig = new Confluent.Kafka.ProducerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            ClientId = "confluent-benchmark",
            Acks = Confluent.Kafka.Acks.Leader,
            LingerMs = 5,
            BatchSize = 16384,
            QueueBufferingMaxMessages = 1000000
        };
        _confluentProducer = new Confluent.Kafka.ProducerBuilder<string, string>(confluentConfig).Build();

        await WarmupAsync().ConfigureAwait(false);
    }

    private async Task WarmupAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            await _dekafProducer.ProduceAsync(new DekafProducer.ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = "warmup",
                Value = "warmup"
            }).ConfigureAwait(false);

            await _confluentProducer.ProduceAsync(Topic, new Confluent.Kafka.Message<string, string>
            {
                Key = "warmup",
                Value = "warmup"
            }).ConfigureAwait(false);
        }

        await _dekafProducer.FlushAsync().ConfigureAwait(false);
        _confluentProducer.Flush(TimeSpan.FromSeconds(5));
    }

    [GlobalCleanup]
    public async Task Cleanup()
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

    // ===== Single Message Produce =====

    [BenchmarkCategory("SingleProduce")]
    [Benchmark(Baseline = true)]
    public async Task<Confluent.Kafka.DeliveryResult<string, string>> Confluent_ProduceSingle()
    {
        return await _confluentProducer.ProduceAsync(Topic, new Confluent.Kafka.Message<string, string>
        {
            Key = "key",
            Value = _messageValue
        }).ConfigureAwait(false);
    }

    [BenchmarkCategory("SingleProduce")]
    [Benchmark]
    public async Task<DekafProducer.RecordMetadata> Dekaf_ProduceSingle()
    {
        return await _dekafProducer.ProduceAsync(new DekafProducer.ProducerMessage<string, string>
        {
            Topic = Topic,
            Key = "key",
            Value = _messageValue
        }).ConfigureAwait(false);
    }

    // ===== Batch Produce =====

    [BenchmarkCategory("BatchProduce")]
    [Benchmark(Baseline = true)]
    public async Task Confluent_ProduceBatch()
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

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    [BenchmarkCategory("BatchProduce")]
    [Benchmark]
    public async Task Dekaf_ProduceBatch()
    {
        var tasks = new List<Task<DekafProducer.RecordMetadata>>(BatchSize);

        for (var i = 0; i < BatchSize; i++)
        {
            tasks.Add(_dekafProducer.ProduceAsync(new DekafProducer.ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = $"key-{i}",
                Value = _messageValue
            }).AsTask());
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    // ===== Fire-and-Forget =====

    [BenchmarkCategory("FireAndForget")]
    [Benchmark(Baseline = true)]
    public void Confluent_FireAndForget()
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

    [BenchmarkCategory("FireAndForget")]
    [Benchmark]
    public void Dekaf_FireAndForget()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            _dekafProducer.Send(Topic, $"key-{i}", _messageValue);
        }
    }
}
