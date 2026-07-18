using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Tooling;
using DekafProducer = Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Client;

/// <summary>
/// Producer benchmarks comparing Dekaf vs Confluent.Kafka.
/// Confluent is marked as baseline for ratio comparison.
/// </summary>
/// <remarks>
/// Message keys are pre-created in <c>[GlobalSetup]</c> so the Allocated column reflects
/// the clients, not the benchmark's own key-string interpolation. Dekaf ops use the
/// allocation-optimized <c>ProduceAsync(topic, key, value)</c> / <c>FireAsync(topic, key, value)</c>
/// overloads — the idiomatic hot-path API — rather than allocating a
/// <see cref="DekafProducer.ProducerMessage{TKey, TValue}"/> per message.
/// </remarks>
[MemoryDiagnoser]
[ThroughputJob]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ProducerBenchmarks
{
    private KafkaTestEnvironment _kafka = null!;
    private DekafProducer.IKafkaProducer<string, string> _dekafProducer = null!;
    private Confluent.Kafka.IProducer<string, string> _confluentProducer = null!;
    private Confluent.Kafka.IProducer<string, string> _confluentFireAndForgetProducer = null!;

    private const string Topic = "benchmark-producer";
    private static readonly TimeSpan QueueFullRetryTimeout = TimeSpan.FromSeconds(30);
    private string _messageValue = null!;
    private string[] _keys = null!;

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
        _keys = BenchmarkData.CreateKeys(BatchSize);

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
            CreateConfluentConfig(
                "confluent-benchmark-fnf",
                enableDeliveryReports: false,
                queueBufferingMaxMessages: ConfluentProducerBackpressure.QueueBufferingMaxMessages))
            .Build();

        await WarmupAsync().ConfigureAwait(false);
    }

    private Confluent.Kafka.ProducerConfig CreateConfluentConfig(
        string clientId,
        bool enableDeliveryReports,
        int? queueBufferingMaxMessages = null)
    {
        var config = new Confluent.Kafka.ProducerConfig
        {
            BootstrapServers = _kafka.BootstrapServers,
            ClientId = clientId,
            Acks = Confluent.Kafka.Acks.Leader,
            LingerMs = 5,
            BatchSize = 16384,
            EnableDeliveryReports = enableDeliveryReports
        };

        if (queueBufferingMaxMessages is { } maxMessages)
            config.QueueBufferingMaxMessages = maxMessages;

        return config;
    }

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
        return await _dekafProducer.ProduceAsync(Topic, "key", _messageValue, CancellationToken.None)
            .ConfigureAwait(false);
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
                Key = _keys[i],
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

        // .AsTask() per message is required: the pooled ValueTask contract forbids
        // collecting raw ValueTasks for deferred await (see IKafkaProducer remarks).
        for (var i = 0; i < BatchSize; i++)
        {
            tasks.Add(_dekafProducer.ProduceAsync(Topic, _keys[i], _messageValue, CancellationToken.None)
                .AsTask());
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
            ProduceConfluentFireAndForget(_keys[i], _messageValue);
        }
    }

    private void ProduceConfluentFireAndForget(string key, string value)
    {
        var message = new Confluent.Kafka.Message<string, string>
        {
            Key = key,
            Value = value
        };

        ConfluentProducerBackpressure.ProduceWithBackpressure(
            _confluentFireAndForgetProducer,
            Topic,
            message,
            deliveryHandler: null,
            cancellationToken: CancellationToken.None,
            retryTimeout: QueueFullRetryTimeout);
    }

    [BenchmarkCategory("FireAndForget")]
    [Benchmark]
    public async Task Dekaf_FireAndForget()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            await _dekafProducer.FireAsync(Topic, _keys[i], _messageValue);
        }
    }
}
