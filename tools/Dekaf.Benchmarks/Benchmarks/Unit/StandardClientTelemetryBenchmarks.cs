using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Telemetry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Steady-state batch/request/wait recording with and without a broker subscription.</summary>
[MemoryDiagnoser]
public class StandardClientTelemetryBenchmarks
{
    private StandardClientTelemetryMetrics _producer = null!;
    private StandardClientTelemetryMetrics _consumer = null!;
    private ClientTelemetryMetricCollector _collector = null!;
    private ClientTelemetrySubscription _subscription = null!;
    private readonly List<ClientTelemetryMetric> _metrics = new(2);
    private long _created, _drained;

    [Params(false, true)]
    public bool Subscribed { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _producer = new(true);
        _collector = new(ClientTelemetryClientRole.Consumer);
        _consumer = _collector.StandardMetrics!;
        if (Subscribed)
        {
            _producer.Subscribe([StandardClientTelemetryMetrics.ProducerPrefix], 0);
            _collector.Subscribe([StandardClientTelemetryMetrics.ConsumerPrefix]);
        }
        _created = Stopwatch.GetTimestamp();
        _drained = _created + Stopwatch.Frequency / 1000;
        // Warm the existing per-broker latency entry before measuring the request path.
        _collector.RecordRequestLatency(1, _created, ApiKey.Fetch);
        _subscription = new(Guid.Empty, 1, 0, 1000, 10000, false, [StandardClientTelemetryMetrics.QueuePrefix]);
        QueueBatch();
    }

    [Benchmark]
    public void QueueBatch()
    {
        var samples = _producer.BeginQueueTimeBatch();
        samples.Record(_created, _drained);
        samples.Complete();
    }

    [Benchmark(OperationsPerInvoke = 8)]
    public void QueueCoalescedRequest()
    {
        var samples = _producer.BeginQueueTimeBatch();
        for (var i = 0; i < 8; i++) samples.Record(_created, _drained);
        samples.Complete();
    }

    [Benchmark]
    public double CollectQueueSnapshot()
    {
        _metrics.Clear();
        _producer.Collect(_subscription, _metrics, 0);
        return _metrics.Count == 0 ? 0 : _metrics[0].Value;
    }

    [Benchmark]
    public void FetchRequest()
    {
        var started = Stopwatch.GetTimestamp();
        _collector.RecordRequestLatency(1, started, ApiKey.Fetch);
    }

    [Benchmark]
    public void UnknownBrokerFetchRequest()
    {
        var started = Stopwatch.GetTimestamp();
        _collector.RecordRequestLatency(-1, started, ApiKey.Fetch);
    }

    [Benchmark]
    public void ForegroundWait()
    {
        _consumer.BeginPollWait();
        _consumer.EndPollWait();
    }

    [Benchmark]
    public void Rebalance()
    {
        var started = _consumer.RebalanceStarted();
        _consumer.RebalanceCompleted(started);
    }
}
