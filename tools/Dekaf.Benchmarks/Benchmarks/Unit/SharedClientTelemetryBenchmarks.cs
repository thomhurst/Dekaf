using BenchmarkDotNet.Attributes;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Telemetry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Steady-state attribution for requests on owned and shared connections.</summary>
[MemoryDiagnoser]
public class SharedClientTelemetryBenchmarks
{
    private KafkaConnection _connection = null!;
    private ClientTelemetryMetricCollector _collector = null!;
    private readonly FetchRequest _fetch = new();
    private readonly ProduceRequest _produce = new();
    private readonly ListOffsetsRequest _offsetQuery = new() { Topics = [] };
    private readonly OffsetCommitRequest _commit = new() { GroupId = "benchmark", Topics = [] };

    [Params(false, true)]
    public bool Shared { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _collector = new(ClientTelemetryClientRole.Consumer);
        _connection = new KafkaConnection("localhost", 9092, "benchmark", null, null,
            ResponseBufferPool.Default, Shared ? null : _collector);
        ((IRequestWriteSequenceTarget)_fetch).WriteSequenceSource = new Source(_collector);
        ((IRequestWriteSequenceTarget)_offsetQuery).WriteSequenceSource = new Source(_collector);
        _produce.TelemetryMetricCollector = _collector;
    }

    [Benchmark]
    public object? Fetch() => _connection.GetTelemetryMetricCollector(_fetch);

    [Benchmark]
    public object? Produce() => _connection.GetTelemetryMetricCollector(_produce);

    [Benchmark]
    public object? Commit() => _connection.GetTelemetryMetricCollector(_commit, _collector);

    [Benchmark]
    public object? OffsetQuery() => _connection.GetTelemetryMetricCollector(_offsetQuery);

    [Benchmark]
    public object? ObservedControlContext()
    {
        // Both paths attribute the request; only shared connections rent callback state.
        if (!Shared) return _connection.GetTelemetryMetricCollector(_commit);
        var state = TelemetryWriteObservationState.Rent(_collector, static () => { });
        var collector = _connection.GetTelemetryMetricCollector(_commit, state.Collector);
        state.Return();
        return collector;
    }

    [GlobalCleanup]
    public async Task Cleanup() => await _connection.DisposeAsync();

    private sealed class Source(ClientTelemetryMetricCollector collector) : IRequestWriteSequenceSource, IClientTelemetrySource
    {
        public ClientTelemetryMetricCollector? TelemetryMetricCollector => collector;
        public long NextRequestWriteSequence() => 0;
    }
}
