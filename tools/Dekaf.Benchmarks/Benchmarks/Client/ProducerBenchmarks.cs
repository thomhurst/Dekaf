using System.Diagnostics;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using Dekaf.Benchmarks.Infrastructure;
using DekafProducer = Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Client;

/// <summary>
/// Producer benchmarks comparing Dekaf vs Confluent.Kafka.
/// Confluent is marked as baseline for ratio comparison.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ProducerBenchmarks
{
    private KafkaTestEnvironment _kafka = null!;
    private DekafProducer.IKafkaProducer<string, string> _dekafProducer = null!;
    private Confluent.Kafka.IProducer<string, string> _confluentProducer = null!;
    private Confluent.Kafka.IProducer<string, string> _confluentFireAndForgetProducer = null!;

    private const string Topic = "benchmark-producer";
    // Keep librdkafka's local queue byte-bound, not message-count-bound, during bursty fire-and-forget runs.
    private const int ConfluentQueueBufferingMaxKbytes = 1024 * 1024;
    private const int ConfluentQueueBufferingMaxMessages = 10_000_000;
    private static readonly TimeSpan QueueFullRetryTimeout = TimeSpan.FromSeconds(30);
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

        _dekafProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("dekaf-benchmark")
            .WithAcks(DekafProducer.Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithBatchSize(16384)
            .BuildAsync()
            .ConfigureAwait(false);

        _confluentProducer = new Confluent.Kafka.ProducerBuilder<string, string>(
            CreateConfluentConfig("confluent-benchmark", enableDeliveryReports: true))
            .Build();

        _confluentFireAndForgetProducer = new Confluent.Kafka.ProducerBuilder<string, string>(
            CreateConfluentConfig("confluent-benchmark-fnf", enableDeliveryReports: false))
            .Build();

        await WarmupAsync().ConfigureAwait(false);
    }

    private Confluent.Kafka.ProducerConfig CreateConfluentConfig(string clientId, bool enableDeliveryReports)
        => new()
        {
            BootstrapServers = _kafka.BootstrapServers,
            ClientId = clientId,
            Acks = Confluent.Kafka.Acks.Leader,
            LingerMs = 5,
            BatchSize = 16384,
            QueueBufferingMaxKbytes = ConfluentQueueBufferingMaxKbytes,
            QueueBufferingMaxMessages = ConfluentQueueBufferingMaxMessages,
            EnableDeliveryReports = enableDeliveryReports
        };

    private async Task WarmupAsync()
    {
        for (var i = 0; i < 10; i++)
        {
            await _dekafProducer.FireAsync(new DekafProducer.ProducerMessage<string, string>
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

            ProduceConfluentFireAndForget("warmup", "warmup");
        }

        await _dekafProducer.FlushAsync().ConfigureAwait(false);
        _confluentProducer.Flush(TimeSpan.FromSeconds(5));
        _confluentFireAndForgetProducer.Flush(TimeSpan.FromSeconds(5));
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
        _confluentFireAndForgetProducer.Flush(TimeSpan.FromSeconds(60));

        await _dekafProducer.DisposeAsync().ConfigureAwait(false);
        _confluentProducer.Dispose();
        _confluentFireAndForgetProducer.Dispose();
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
        }, CancellationToken.None).ConfigureAwait(false);
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
            }, CancellationToken.None).AsTask());
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
            ProduceConfluentFireAndForget($"key-{i}", _messageValue);
        }
    }

    private void ProduceConfluentFireAndForget(string key, string value)
    {
        var message = new Confluent.Kafka.Message<string, string>
        {
            Key = key,
            Value = value
        };
        var startedAt = Stopwatch.GetTimestamp();

        while (true)
        {
            try
            {
                _confluentFireAndForgetProducer.Produce(Topic, message);
                return;
            }
            catch (Confluent.Kafka.ProduceException<string, string> ex)
                when (ex.Error.Code == Confluent.Kafka.ErrorCode.Local_QueueFull &&
                      Stopwatch.GetElapsedTime(startedAt) < QueueFullRetryTimeout)
            {
                Thread.Sleep(1);
            }
        }
    }

    [BenchmarkCategory("FireAndForget")]
    [Benchmark]
    public async Task Dekaf_FireAndForget()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            await _dekafProducer.FireAsync(Topic, $"key-{i}", _messageValue);
        }
    }
}
