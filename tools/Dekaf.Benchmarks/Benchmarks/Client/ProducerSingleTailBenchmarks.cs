using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Tooling;
using DekafProducer = Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Client;

/// <summary>
/// Per-operation latency distribution of serial awaited produce (one message in flight at a
/// time, keyed so the sender knows more than one partition). BenchmarkDotNet's percentile
/// columns describe iteration means, which cannot surface a rare per-call stall — the
/// send loop's wave-coalesce probe, for example, costs a full quiet window on roughly one
/// call per 50 ms — so every call's latency is recorded into a preallocated array and the
/// distribution is printed at cleanup (<c>[tail]</c> lines in the run log).
/// </summary>
/// <remarks>
/// Evidence harness, not a published table: the async loop allocates one state machine per
/// invocation (amortised over <see cref="OperationsPerInvoke"/> calls), so its Allocated
/// column is not a hot-path gate.
/// </remarks>
[MemoryDiagnoser]
[ThroughputJob]
public class ProducerSingleTailBenchmarks
{
    private const string Topic = "benchmark-producer-single-tail";
    private const int OperationsPerInvoke = 1_000;
    private const int MaxSamples = 1 << 20;
    private const int MessageSize = 100;

    private KafkaTestEnvironment _kafka = null!;
    private DekafProducer.IKafkaProducer<string, string> _producer = null!;
    private string _messageValue = null!;
    private long[] _samples = null!;
    private int _sampleCount;

    [Params(0, 5)]
    public int LingerMs { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _kafka = await KafkaTestEnvironment.CreateAsync().ConfigureAwait(false);
        await _kafka.CreateTopicAsync(Topic, 3).ConfigureAwait(false);

        _messageValue = new string('x', MessageSize);
        _samples = new long[MaxSamples];
        _sampleCount = 0;

        _producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId($"dekaf-single-tail-linger-{LingerMs}")
            .WithAcks(DekafProducer.Acks.All)
            .WithLinger(TimeSpan.FromMilliseconds(LingerMs))
            .WithBatchSize(16384)
            .BuildAsync()
            .ConfigureAwait(false);

        // Same two keys as ProducerSingleBenchmarks: "warmup" and "key" hash to different
        // partitions, so the sender's known-partition set is wider than each single-batch
        // wave and the wave-coalesce spin gate is open for every request.
        for (var i = 0; i < 10; i++)
        {
            await _producer.ProduceAsync(Topic, "warmup", "warmup", CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        PrintDistribution();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await _producer.FlushAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Ignore the bounded cleanup flush timeout.
        }
        finally
        {
            await _producer.DisposeAsync().ConfigureAwait(false);
            await _kafka.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public async ValueTask Dekaf_ProduceSingleSerial()
    {
        var producer = _producer;
        var samples = _samples;
        var count = _sampleCount;
        for (var i = 0; i < OperationsPerInvoke; i++)
        {
            var started = Stopwatch.GetTimestamp();
            await producer.ProduceAsync(Topic, "key", _messageValue, CancellationToken.None)
                .ConfigureAwait(false);
            if (count < samples.Length)
                samples[count++] = Stopwatch.GetTimestamp() - started;
        }

        _sampleCount = count;
    }

    private void PrintDistribution()
    {
        var count = _sampleCount;
        if (count == 0)
        {
            Console.WriteLine($"[tail] LingerMs={LingerMs} no samples");
            return;
        }

        var sorted = new long[count];
        Array.Copy(_samples, sorted, count);
        Array.Sort(sorted);
        var ticksPerMicrosecond = Stopwatch.Frequency / 1_000_000.0;
        var over500Us = count - LowerBound(sorted, (long)(500 * ticksPerMicrosecond));
        var over1Ms = count - LowerBound(sorted, (long)(1_000 * ticksPerMicrosecond));

        Console.WriteLine(
            $"[tail] LingerMs={LingerMs} n={count} " +
            $"p50={Percentile(sorted, 0.50) / ticksPerMicrosecond:F1}us " +
            $"p90={Percentile(sorted, 0.90) / ticksPerMicrosecond:F1}us " +
            $"p99={Percentile(sorted, 0.99) / ticksPerMicrosecond:F1}us " +
            $"p99.9={Percentile(sorted, 0.999) / ticksPerMicrosecond:F1}us " +
            $"max={sorted[count - 1] / ticksPerMicrosecond:F1}us " +
            $"over500us={over500Us} over1ms={over1Ms}");
    }

    private static long Percentile(long[] sorted, double fraction)
        => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(fraction * sorted.Length) - 1)];

    private static int LowerBound(long[] sorted, long value)
    {
        var index = Array.BinarySearch(sorted, value);
        if (index >= 0)
        {
            while (index > 0 && sorted[index - 1] == value)
                index--;
            return index;
        }

        return ~index;
    }
}
