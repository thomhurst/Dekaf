using System.Text;
using System.Text.Json;
using Dekaf.StressTests.Metrics;
using Dekaf.Tools.Telemetry;
using Google.Protobuf;

namespace Dekaf.Tests.Unit.StressTests;

public sealed class ShareTelemetryObserverTests
{
    private static readonly string[] Clients = ["first", "second"];
    private static readonly Guid[] Identities = [Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222")];

    [Test]
    public async Task Snapshot_RoundTripPreservesRecordedSubscriptionDimensions()
    {
        // A report read on a newer harness must not rewrite historical dimensions.
        var snapshot = new ShareTelemetrySnapshot { PushIntervalMilliseconds = 2000,
            MaximumRetainedPayloads = 32, MaximumPayloadBytes = 8192, RequestedMetrics = ["historical"],
            RetainedPayloads = 4, Workers = ShareTelemetryObserver.Inspect(Complete(), Clients, Identities, true)!.Workers };
        var restored = JsonSerializer.Deserialize<ShareTelemetrySnapshot>(JsonSerializer.Serialize(snapshot))!;
        await Assert.That(restored.PushIntervalMilliseconds).IsEqualTo(2000);
        await Assert.That(restored.MaximumRetainedPayloads).IsEqualTo(32);
        await Assert.That(restored.MaximumPayloadBytes).IsEqualTo(8192);
        await Assert.That(restored.RequestedMetrics.Single()).IsEqualTo("historical");
        await Assert.That(restored.Workers[1].ClientInstanceId).IsEqualTo(Identities[1]);
    }

    [Test]
    public async Task CompleteExports_ConfirmBothWorkersAndRetainedCounts()
    {
        var result = ShareTelemetryObserver.Inspect(Complete(), Clients, Identities, true)!;
        await Assert.That(result.RetainedPayloads).IsEqualTo(4);
        await Assert.That(result.Workers.Length).IsEqualTo(2);
        foreach (var worker in result.Workers)
        {
            await Assert.That(worker.PeriodicPayloads).IsEqualTo(1);
            await Assert.That(worker.TerminatingPayloads).IsEqualTo(1);
            await Assert.That(worker.PayloadBytes).IsGreaterThan(0);
            await Assert.That(worker.PositiveAcknowledgements).IsTrue();
        }
    }

    [Test]
    public async Task PeriodicExports_CannotProveTermination()
    {
        var periodic = Row(0, false) + Row(1, false);
        await Assert.That(ShareTelemetryObserver.Inspect(periodic, Clients, Identities, false)).IsNotNull();
        await Assert.That(ShareTelemetryObserver.Inspect(periodic, Clients, Identities, true)).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task MissingWorker_CannotPass(int worker)
    {
        await Assert.That(ShareTelemetryObserver.Inspect(Row(worker, false) + Row(worker, true), Clients, Identities, true)).IsNull();
    }

    [Test]
    [Arguments(ShareTelemetryObserver.FetchMetric)]
    [Arguments(ShareTelemetryObserver.RecordsMetric)]
    [Arguments(ShareTelemetryObserver.AcknowledgementsMetric)]
    public async Task NoPeriodicProgress_CannotPass(string metric)
    {
        var raw = Row(0, false, metrics => metrics.Single(item => item.Name == metric).Sum.DataPoints[0].AsInt = 0)
            + Row(1, false) + Row(0, true) + Row(1, true);
        await Assert.That(ShareTelemetryObserver.Inspect(raw, Clients, Identities, true)).IsNull();
    }

    [Test]
    [Arguments("identity")]
    [Arguments("client")]
    [Arguments("content-type")]
    [Arguments("gauge")]
    [Arguments("acknowledgement-error")]
    [Arguments("terminating-fetch")]
    [Arguments("duplicate-termination")]
    [Arguments("history-bound")]
    [Arguments("payload-bound")]
    [Arguments("empty-payload")]
    [Arguments("malformed-row")]
    public async Task CorruptEvidence_IsRejected(string mutation)
    {
        var raw = mutation switch
        {
            "identity" => Complete().Replace(Identities[0].ToString(), Guid.Empty.ToString()),
            "client" => Complete().Replace(Encode(Clients[0]), Encode("unexpected")),
            "content-type" => Complete().Replace(Encode("OTLP"), Encode("JSON")),
            "gauge" => Row(0, false, metrics => metrics.Single(item => item.Name == ShareTelemetryObserver.ApplicationMetric).Gauge.DataPoints[0].AsDouble = 7),
            "acknowledgement-error" => Row(0, false, metrics => metrics.Add(SumMetric(ShareTelemetryObserver.AcknowledgementErrorsMetric))),
            "terminating-fetch" => Row(0, true, metrics => metrics.RemoveAll(item => item.Name == ShareTelemetryObserver.FetchMetric)),
            "duplicate-termination" => Complete() + Row(0, true),
            "history-bound" => string.Concat(Enumerable.Repeat(Row(0, false), ShareTelemetryObserver.MaximumPayloads + 1)),
            "payload-bound" => Envelope(0, false, new byte[ShareTelemetryObserver.MaximumPayloadBytes + 1]),
            "empty-payload" => Envelope(0, false, []),
            _ => "malformed\n"
        };
        await Assert.That(() => ShareTelemetryObserver.Inspect(raw, Clients, Identities, true)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task DuplicateWorkerIdentity_IsRejected()
    {
        await Assert.That(() => ShareTelemetryObserver.Inspect(Complete(), Clients, [Identities[0], Identities[0]], true))
            .Throws<InvalidDataException>();
    }

    private static string Complete() => Row(0, false) + Row(1, false) + Row(0, true) + Row(1, true);

    private static string Row(int worker, bool terminating, Action<List<Metric>>? mutate = null)
    {
        var metrics = new List<Metric>
        {
            new() { Name = ShareTelemetryObserver.ApplicationMetric,
                Gauge = new Gauge { DataPoints = { new NumberDataPoint { AsDouble = worker + 1 } } } },
            SumMetric(ShareTelemetryObserver.FetchMetric), SumMetric(ShareTelemetryObserver.RecordsMetric),
            SumMetric(ShareTelemetryObserver.AcknowledgementsMetric)
        };
        mutate?.Invoke(metrics);
        var scope = new ScopeMetrics();
        scope.Metrics.AddRange(metrics);
        var payload = new MetricsData { ResourceMetrics = { new ResourceMetrics { ScopeMetrics = { scope } } } };
        return Envelope(worker, terminating, payload.ToByteArray());
    }

    private static Metric SumMetric(string name) => new() { Name = name,
        Sum = new Sum { IsMonotonic = true, AggregationTemporality = 1, DataPoints = { new NumberDataPoint { AsInt = 10 } } } };
    private static string Envelope(int worker, bool terminating, byte[] bytes) =>
        $"{Identities[worker]}\t{terminating}\t{Encode(Clients[worker])}\t{Encode("OTLP")}\t{Convert.ToBase64String(bytes)}\n";
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
