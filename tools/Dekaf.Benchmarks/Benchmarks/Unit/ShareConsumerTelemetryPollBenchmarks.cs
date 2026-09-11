using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Exercises subscribed request, acknowledgement and poll metrics through both polling APIs.</summary>
[MemoryDiagnoser]
public class ShareConsumerTelemetryPollBenchmarks
{
    private static readonly string[] MetricPrefixes = ["org.apache.kafka.consumer.share."];
    private ShareConsumerPollBenchmarks _poll = null!;

    [Params(1, 1024)]
    public int RecordCount { get; set; }

    [Params(false, true)]
    public bool Subscribed { get; set; }

    [Params(false, true)]
    public bool AsynchronousResponse { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        _poll = new ShareConsumerPollBenchmarks
        {
            RecordCount = RecordCount, BatchCount = 1, AsynchronousResponse = AsynchronousResponse
        };
        await _poll.Setup();
        if (!Subscribed) return;
        foreach (var name in new[] { "_compatibility", "_borrowed" })
        {
            var consumer = typeof(ShareConsumerPollBenchmarks)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_poll)!;
            var collector = consumer.GetType().GetField("_telemetryMetricCollector",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;
            var metrics = collector.GetType().GetProperty("ShareConsumerMetrics",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(collector);
            // The pre-feature baseline has no built-in share recorder.
            metrics?.GetType().GetMethod("Subscribe", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(metrics, [MetricPrefixes]);
        }
    }

    [Benchmark]
    public ValueTask<long> PollClassic() => _poll.PollCompatibilityBatch();

    [Benchmark]
    public ValueTask<long> PollBorrowed() => _poll.PollBorrowedBatch();

    [GlobalCleanup]
    public ValueTask Cleanup() => _poll.Cleanup();
}
