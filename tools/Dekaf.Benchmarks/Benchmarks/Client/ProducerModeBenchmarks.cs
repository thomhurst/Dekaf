using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using Dekaf.Benchmarks.Infrastructure;
using DekafProducer = Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Client;

/// <summary>
/// Compares producer allocation and throughput across modes (idempotent, acks, etc.).
/// Isolates whether Acks.All vs Acks.Leader or idempotent flag itself drives GC differences.
///
/// Uses FireAsync (fire-and-forget) to match the stress test hot path.
/// Pre-allocated keys avoid string allocation noise.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 3, iterationCount: 10)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ProducerModeBenchmarks
{
    private KafkaTestEnvironment _kafka = null!;
    private DekafProducer.IKafkaProducer<string, string> _nonIdempotentLeader = null!;
    private DekafProducer.IKafkaProducer<string, string> _nonIdempotentAll = null!;
    private DekafProducer.IKafkaProducer<string, string> _idempotentAll = null!;

    private const string TopicNonIdempotentLeader = "bench-mode-ni-leader";
    private const string TopicNonIdempotentAll = "bench-mode-ni-all";
    private const string TopicIdempotentAll = "bench-mode-idem-all";
    private const int BatchCount = 1000;

    private string _messageValue = null!;
    private static readonly string[] PreAllocatedKeys = CreateKeys(BatchCount);

    [Params(100, 1000)]
    public int MessageSize { get; set; }

    private static string[] CreateKeys(int count)
    {
        var keys = new string[count];
        for (var i = 0; i < count; i++)
            keys[i] = $"key-{i}";
        return keys;
    }

    [GlobalSetup]
    public async Task Setup()
    {
        _kafka = await KafkaTestEnvironment.CreateAsync().ConfigureAwait(false);
        _messageValue = new string('x', MessageSize);

        await Task.WhenAll(
            _kafka.CreateTopicAsync(TopicNonIdempotentLeader, 3),
            _kafka.CreateTopicAsync(TopicNonIdempotentAll, 3),
            _kafka.CreateTopicAsync(TopicIdempotentAll, 3)
        ).ConfigureAwait(false);

        _nonIdempotentLeader = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("bench-ni-leader")
            .WithIdempotence(false)
            .WithAcks(DekafProducer.Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .BuildAsync()
            .ConfigureAwait(false);

        _nonIdempotentAll = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("bench-ni-all")
            .WithIdempotence(false)
            .WithAcks(DekafProducer.Acks.All)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .BuildAsync()
            .ConfigureAwait(false);

        _idempotentAll = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("bench-idem-all")
            .WithIdempotence(true)
            .WithAcks(DekafProducer.Acks.All)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .BuildAsync()
            .ConfigureAwait(false);

        // Warmup all producers
        await WarmupProducer(_nonIdempotentLeader, TopicNonIdempotentLeader).ConfigureAwait(false);
        await WarmupProducer(_nonIdempotentAll, TopicNonIdempotentAll).ConfigureAwait(false);
        await WarmupProducer(_idempotentAll, TopicIdempotentAll).ConfigureAwait(false);
    }

    private static async Task WarmupProducer(DekafProducer.IKafkaProducer<string, string> producer, string topic)
    {
        // Extended warmup: fill pools, stabilize GC
        for (var i = 0; i < 10_000; i++)
        {
            await producer.FireAsync(topic, PreAllocatedKeys[i % PreAllocatedKeys.Length], "warmup").ConfigureAwait(false);
        }
        await producer.FlushAsync().ConfigureAwait(false);

        // Let GC settle
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await _nonIdempotentLeader.FlushAsync(cts.Token).ConfigureAwait(false); } catch { }
        try { await _nonIdempotentAll.FlushAsync(cts.Token).ConfigureAwait(false); } catch { }
        try { await _idempotentAll.FlushAsync(cts.Token).ConfigureAwait(false); } catch { }

        await _nonIdempotentLeader.DisposeAsync().ConfigureAwait(false);
        await _nonIdempotentAll.DisposeAsync().ConfigureAwait(false);
        await _idempotentAll.DisposeAsync().ConfigureAwait(false);
        await _kafka.DisposeAsync().ConfigureAwait(false);
    }

    // ===== FireAsync Batch (isolates per-message allocation) =====

    [BenchmarkCategory("FireAsync")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BatchCount)]
    public async Task NonIdempotent_AcksLeader()
    {
        for (var i = 0; i < BatchCount; i++)
        {
            await _nonIdempotentLeader.FireAsync(TopicNonIdempotentLeader, PreAllocatedKeys[i], _messageValue)
                .ConfigureAwait(false);
        }
    }

    [BenchmarkCategory("FireAsync")]
    [Benchmark(OperationsPerInvoke = BatchCount)]
    public async Task NonIdempotent_AcksAll()
    {
        for (var i = 0; i < BatchCount; i++)
        {
            await _nonIdempotentAll.FireAsync(TopicNonIdempotentAll, PreAllocatedKeys[i], _messageValue)
                .ConfigureAwait(false);
        }
    }

    [BenchmarkCategory("FireAsync")]
    [Benchmark(OperationsPerInvoke = BatchCount)]
    public async Task Idempotent_AcksAll()
    {
        for (var i = 0; i < BatchCount; i++)
        {
            await _idempotentAll.FireAsync(TopicIdempotentAll, PreAllocatedKeys[i], _messageValue)
                .ConfigureAwait(false);
        }
    }
}
