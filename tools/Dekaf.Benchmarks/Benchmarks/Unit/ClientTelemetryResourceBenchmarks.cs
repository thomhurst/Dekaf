using BenchmarkDotNet.Attributes;
using Dekaf.Telemetry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Steady-state telemetry collection and OTLP encoding, with and without configured
/// consumer resource labels. Costs are per periodic push, not per Kafka message.
/// </summary>
[MemoryDiagnoser]
public class ClientTelemetryResourceBenchmarks
{
    private ClientTelemetryMetricCollector _collector = null!;
    private ClientTelemetryPayloadProvider _provider = null!;
    private ClientTelemetrySubscription _subscription = null!;
    private ClientTelemetryMetricSnapshot _snapshot = null!;

    [Params(false, true)]
    public bool ResourceLabels { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _collector = new(ClientTelemetryClientRole.Consumer);
        if (ResourceLabels)
            _collector.ResourceAttributesProvider = static () => new(
                ClientRack: "rack-1", GroupId: "orders", GroupInstanceId: "worker-1",
                GroupMemberId: "11111111-1111-1111-1111-111111111111");
        _collector.RegisterMetricForSubscription(new("com.example.depth", ApplicationTelemetryMetricKind.Gauge, static () => 42));
        _collector.RecordConnectionCreated();
        _collector.RecordRequestLatency(1, TimeSpan.FromMilliseconds(1));
        _subscription = new(Guid.NewGuid(), 1, 0, 60000, 65536, false, [string.Empty]);
        _provider = new();
        _snapshot = _collector.Collect(_subscription);
        _ = _provider.Collect(_subscription, _snapshot, false);
    }

    [Benchmark]
    public object Collect() => _collector.Collect(_subscription);

    // Keep the metric object's allocation visible when its optional OTLP fields change.
    [Benchmark]
    public object MetricObject() => new ClientTelemetryMetric(
        ClientTelemetryMetricNames.ConsumerConnectionCreationTotal,
        ClientTelemetryMetricKind.Counter, 1, []);

    [Benchmark]
    public ReadOnlyMemory<byte> Encode() => _provider.Collect(_subscription, _snapshot, false);
}
