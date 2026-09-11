using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures parsing with and without a broker subscription to share-consumer metrics.</summary>
[MemoryDiagnoser]
public class ShareConsumerTelemetryParsingBenchmarks
{
    private static readonly string[] MetricPrefixes = ["org.apache.kafka.consumer.share."];
    private ShareConsumerParsingBenchmarks _parsing = null!;
    private Action _resetSynchronous = static () => { };
    private Action _resetPrepared = static () => { };

    [Params(64, 1024)]
    public int RecordCount { get; set; }

    [Params(false, true)]
    public bool Subscribed { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        _parsing = new ShareConsumerParsingBenchmarks { RecordCount = RecordCount, HeaderCount = 0 };
        await _parsing.Setup();
        if (!Subscribed) return;

        // Bind only during setup so this fixture also builds against the pre-feature
        // baseline, which has no built-in recorder. Timed methods use the same parsers.
        foreach (var name in new[] { "_synchronousConsumer", "_preparedConsumer" })
        {
            var consumer = typeof(ShareConsumerParsingBenchmarks)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_parsing)!;
            var collector = consumer.GetType().GetField("_telemetryMetricCollector",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;
            var metrics = collector.GetType().GetProperty("ShareConsumerMetrics",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(collector);
            if (metrics is null) continue;
            metrics.GetType().GetMethod("Subscribe", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(metrics, [MetricPrefixes]);
            metrics.GetType().GetMethod("FetchStarted", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(metrics, [1]);
            var sample = metrics.GetType().GetMethod("GetFetchSample", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(metrics, [1]);
            consumer.GetType().GetField("_activeTelemetryFetch", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(consumer, sample);
            var reset = sample!.GetType().GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Action>(sample);
            if (name == "_synchronousConsumer") _resetSynchronous = reset;
            else _resetPrepared = reset;
        }
    }

    [Benchmark]
    public long ParseSynchronousBatch()
    {
        _resetSynchronous();
        return _parsing.ParseSynchronousBatch();
    }

    [Benchmark]
    public long ParseWarmPreparedBatch()
    {
        _resetPrepared();
        return _parsing.ParseWarmPreparedBatch();
    }

    [Benchmark]
    public ValueTask<long> ParseBorrowedSynchronousBatch()
    {
        _resetSynchronous();
        return _parsing.ParseBorrowedSynchronousBatch();
    }

    [Benchmark]
    public ValueTask<long> ParseBorrowedWarmPreparedBatch()
    {
        _resetPrepared();
        return _parsing.ParseBorrowedWarmPreparedBatch();
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _parsing.Cleanup();
}
