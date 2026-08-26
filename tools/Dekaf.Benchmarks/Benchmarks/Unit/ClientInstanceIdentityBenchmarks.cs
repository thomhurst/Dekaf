using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Telemetry;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures the allocation-free cached KIP-714 identity read.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class ClientInstanceIdentityBenchmarks
{
    private static readonly FieldInfo SubscriptionField = typeof(ClientTelemetryManager)
        .GetField("_subscription", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private ConnectionPool _connectionPool = null!;
    private MetadataManager _metadataManager = null!;
    private ClientTelemetryManager _unavailable = null!;
    private ClientTelemetryManager _available = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connectionPool = new ConnectionPool("identity-benchmark", new ConnectionOptions());
        _metadataManager = new MetadataManager(_connectionPool, ["localhost:9092"]);
        _unavailable = new ClientTelemetryManager(
            _connectionPool,
            _metadataManager,
            payloadProvider: EmptyClientTelemetryPayloadProvider.Instance);
        _available = new ClientTelemetryManager(
            _connectionPool,
            _metadataManager,
            payloadProvider: EmptyClientTelemetryPayloadProvider.Instance);
        SubscriptionField.SetValue(_available, new ClientTelemetrySubscription(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SubscriptionId: 1,
            CompressionType: 0,
            PushIntervalMs: 60_000,
            TelemetryMaxBytes: 1_024,
            DeltaTemporality: false,
            RequestedMetrics: []));
    }

    [Benchmark(Baseline = true)]
    public Guid? ReadUnavailable() => _unavailable.ClientInstanceId;

    [Benchmark]
    public Guid? ReadAvailable() => _available.ClientInstanceId;

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _unavailable.DisposeAsync().ConfigureAwait(false);
        await _available.DisposeAsync().ConfigureAwait(false);
        await _metadataManager.DisposeAsync().ConfigureAwait(false);
        await _connectionPool.DisposeAsync().ConfigureAwait(false);
    }
}
