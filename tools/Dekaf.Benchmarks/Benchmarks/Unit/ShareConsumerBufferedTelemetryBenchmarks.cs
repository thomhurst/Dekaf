using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures subscribed metrics while classic polling resumes oversized responses.</summary>
[MemoryDiagnoser]
public class ShareConsumerBufferedTelemetryBenchmarks
{
    private static readonly string[] MetricPrefixes = ["org.apache.kafka.consumer.share."];
    private ShareConsumerPollBufferBenchmarks _poll = null!;

    [Params(1, 64)]
    public int PartitionCount { get; set; }

    [Params(false, true)]
    public bool Prepared { get; set; }

    [Params(false, true)]
    public bool Subscribed { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _poll = new ShareConsumerPollBufferBenchmarks
        {
            PartitionCount = PartitionCount, Prepared = Prepared, Overflow = true, RenewalBuffering = false
        };
        await _poll.Setup();
        if (!Subscribed) return;

        var consumer = typeof(ShareConsumerPollBufferBenchmarks)
            .GetField("_consumer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_poll)!;
        var collector = consumer.GetType().GetField("_telemetryMetricCollector",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;
        var metrics = collector.GetType().GetProperty("ShareConsumerMetrics",
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(collector);
        if (metrics is null) return;
        metrics.GetType().GetMethod("Subscribe", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(metrics, [MetricPrefixes]);
        // Begin a response under the subscription before measuring its retained windows.
        _poll.Resubscribe();
        await _poll.PollWindow();
    }

    [Benchmark]
    public ValueTask<long> PollWindow() => _poll.PollWindow();

    [GlobalCleanup]
    public Task Cleanup() => _poll.Cleanup();
}
