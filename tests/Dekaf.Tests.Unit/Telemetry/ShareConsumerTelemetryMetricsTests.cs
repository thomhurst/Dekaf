using Dekaf.ShareConsumer;
using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Telemetry;

public sealed class ShareConsumerTelemetryMetricsTests
{
    private const string Prefix = "org.apache.kafka.consumer.share.";
    private const string Fetch = Prefix + "fetch.manager.";
    private const string Coordinator = Prefix + "coordinator.";

    [Test]
    public async Task Collect_UsesKip932NamesAndValues_AtRequestAndBatchBoundaries()
    {
        var clock = new MetricClock();
        var metrics = new ShareConsumerTelemetryMetrics(clock.GetTimestamp, clock.TimestampFrequency);
        metrics.Configure([Prefix]);
        var poll = metrics.BeginPoll();
        var heartbeat = metrics.BeginHeartbeat();
        var fetch = metrics.BeginFetch();
        using (metrics.MeasureFetchResponse()) metrics.RecordParsed(100, 4);
        metrics.RecordAcknowledgements(7, 2);
        metrics.RecordRebalance();
        clock.Advance(TimeSpan.FromMilliseconds(20));
        metrics.EndFetch(fetch, 7);
        metrics.EndHeartbeat(heartbeat);
        metrics.EndPoll(poll, TimeSpan.FromMilliseconds(5).Ticks);
        clock.Advance(TimeSpan.FromMilliseconds(1980));
        metrics.BeginPoll();
        var snapshot = Collect(metrics);
        var expected = new Dictionary<string, double>
        {
            [Fetch + "bytes.consumed.total"] = 100, [Fetch + "bytes.consumed.rate"] = 50,
            [Fetch + "records.consumed.total"] = 4, [Fetch + "records.consumed.rate"] = 2,
            [Fetch + "acknowledgements.send.total"] = 7, [Fetch + "acknowledgements.send.rate"] = 3.5,
            [Fetch + "acknowledgements.error.total"] = 2, [Fetch + "acknowledgements.error.rate"] = 1,
            [Fetch + "fetch.total"] = 1, [Fetch + "fetch.rate"] = 0.5,
            [Fetch + "fetch.size.avg"] = 100, [Fetch + "fetch.size.max"] = 100,
            [Fetch + "records.per.request.avg"] = 4, [Fetch + "records.per.request.max"] = 4,
            [Fetch + "fetch.latency.avg"] = 20, [Fetch + "fetch.latency.max"] = 20,
            [Fetch + "fetch.throttle.time.avg"] = 7, [Fetch + "fetch.throttle.time.max"] = 7,
            [Coordinator + "heartbeat.total"] = 1, [Coordinator + "heartbeat.rate"] = 0.5,
            [Coordinator + "heartbeat.response.time.max"] = 20, [Coordinator + "last.heartbeat.seconds.ago"] = 2,
            [Coordinator + "rebalance.total"] = 1, [Coordinator + "rebalance.rate.per.hour"] = 1800,
            [Prefix + "last.poll.seconds.ago"] = 0,
            [Prefix + "time.between.poll.avg"] = 2000, [Prefix + "time.between.poll.max"] = 2000,
            [Prefix + "poll.idle.ratio.avg"] = 0.75
        };
        await Assert.That(snapshot.Count).IsEqualTo(expected.Count);
        foreach (var metric in snapshot)
        {
            await Assert.That(expected.ContainsKey(metric.Name)).IsTrue();
            await Assert.That(metric.Value).IsEqualTo(expected[metric.Name]);
            await Assert.That(metric.Attributes.Count).IsEqualTo(0);
            await Assert.That(metric.Kind).IsEqualTo(metric.Name.EndsWith(".total", StringComparison.Ordinal)
                ? ClientTelemetryMetricKind.Counter : ClientTelemetryMetricKind.Gauge);
        }
    }

    [Test]
    public async Task Collect_DeltaCountersPreserveUnrequestedValues_AndHandleTemporalityChanges()
    {
        var clock = new MetricClock();
        var metrics = new ShareConsumerTelemetryMetrics(clock.GetTimestamp, clock.TimestampFrequency);
        metrics.Configure([Prefix]);
        metrics.RecordAcknowledgements(7, 2);
        var first = Collect(metrics, true, Fetch + "acknowledgements.send.total");
        metrics.RecordAcknowledgements(3, 1);
        var second = Collect(metrics, true, Fetch + "acknowledgements.");
        var third = Collect(metrics, false, Fetch + "acknowledgements.");
        await Assert.That(first.Single().Value).IsEqualTo(7);
        await Assert.That(Value(second, "acknowledgements.send.total")).IsEqualTo(3);
        await Assert.That(Value(second, "acknowledgements.error.total")).IsEqualTo(3);
        await Assert.That(Value(third, "acknowledgements.send.total")).IsEqualTo(10);
        await Assert.That(Value(third, "acknowledgements.error.total")).IsEqualTo(3);
        metrics.RecordAcknowledgements(2, 1);
        var fourth = Collect(metrics, true, Fetch + "acknowledgements.");
        await Assert.That(Value(fourth, "acknowledgements.send.total")).IsEqualTo(2);
        await Assert.That(Value(fourth, "acknowledgements.error.total")).IsEqualTo(1);
    }

    [Test]
    [Arguments("")]
    [Arguments("org.apache.kafka.")]
    [Arguments("org.apache.kafka.consumer.share.fetch.manager.fetch.total")]
    public async Task Configure_KnownPrefixesEnableCollection(string requested)
    {
        var metrics = new ShareConsumerTelemetryMetrics();
        metrics.Configure([requested]);
        await Assert.That(metrics.Enabled).IsTrue();
        metrics.Configure([]);
        await Assert.That(metrics.Enabled).IsFalse();
        await Assert.That(metrics.BeginFetch()).IsEqualTo(-1);
    }

    [Test]
    [Arguments("com.example.")]
    [Arguments("org.apache.kafka.consumer.share.unknown")]
    [Arguments("org.apache.kafka.consumer.share.fetch.manager.fetch.total.extra")]
    public async Task Configure_UnrequestedMetricsLeaveFastPathsDisabled(string requested)
    {
        var metrics = new ShareConsumerTelemetryMetrics();
        metrics.Configure([requested]);
        await Assert.That(metrics.Enabled).IsFalse();
        await Assert.That(metrics.BeginPoll()).IsEqualTo(-1);
        await Assert.That(metrics.BeginHeartbeat()).IsEqualTo(-1);
        await Assert.That(Collect(metrics).Count).IsEqualTo(0);
    }

    [Test]
    public async Task FetchResponse_DisposeCapturesPartialParsing_AndEmptyResponses()
    {
        var metrics = new ShareConsumerTelemetryMetrics();
        metrics.Configure([Prefix]);
        using (metrics.MeasureFetchResponse()) metrics.RecordParsed(80, 2);
        using (metrics.MeasureFetchResponse()) { }
        var snapshot = Collect(metrics);
        await Assert.That(Value(snapshot, "bytes.consumed.total")).IsEqualTo(80);
        await Assert.That(Value(snapshot, "records.consumed.total")).IsEqualTo(2);
        await Assert.That(Value(snapshot, "fetch.size.avg")).IsEqualTo(40);
        await Assert.That(Value(snapshot, "fetch.size.max")).IsEqualTo(80);
        await Assert.That(Value(snapshot, "records.per.request.avg")).IsEqualTo(1);
    }

    [Test]
    public async Task FailedFetch_RecordsAttemptAndLatency_WithoutInventingThrottle()
    {
        var clock = new MetricClock();
        var metrics = new ShareConsumerTelemetryMetrics(clock.GetTimestamp, clock.TimestampFrequency);
        metrics.Configure([Prefix]);
        var request = metrics.BeginFetch();
        clock.Advance(TimeSpan.FromMilliseconds(8));
        metrics.EndFetch(request, -1);
        var snapshot = Collect(metrics);
        await Assert.That(Value(snapshot, "fetch.total")).IsEqualTo(1);
        await Assert.That(Value(snapshot, "fetch.latency.max")).IsEqualTo(8);
        await Assert.That(snapshot.Any(m => m.Name.Contains("throttle", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task CoordinatorReset_RemovesMemberResourceAttribute()
    {
        var metrics = new ShareConsumerTelemetryMetrics();
        metrics.ConfigureIdentity("group", "rack");
        metrics.SetMemberId("member");
        var pool = NSubstitute.Substitute.For<Dekaf.Networking.IConnectionPool>();
        await using var metadata = new Dekaf.Metadata.MetadataManager(pool, ["localhost:9092"]);
        await using var coordinator = new ShareConsumerCoordinator(new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], GroupId = "group"
        }, pool, metadata, telemetryMetrics: metrics);
        typeof(ShareConsumerCoordinator).GetMethod("ResetMemberState",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(coordinator, null);
        var attributes = metrics.ResourceAttributes();
        await Assert.That(attributes.Any(a => a.Name == "group_member_id")).IsFalse();
        await Assert.That(attributes.Single(a => a.Name == "group_id").Value).IsEqualTo("group");
        await Assert.That(attributes.Single(a => a.Name == "client_rack").Value).IsEqualTo("rack");
    }

    private static double Value(List<ClientTelemetryMetric> metrics, string suffix) => metrics.Single(m => m.Name == Fetch + suffix).Value;
    private static List<ClientTelemetryMetric> Collect(ShareConsumerTelemetryMetrics metrics, bool delta = false, params string[] prefixes)
    {
        List<ClientTelemetryMetric> result = [];
        metrics.Collect(new ClientTelemetrySubscription(Guid.Empty, 1, 0, 1000, 100000, delta,
            prefixes.Length == 0 ? [Prefix] : prefixes), result);
        return result;
    }

    private sealed class MetricClock : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        internal void Advance(TimeSpan duration) => _timestamp += duration.Ticks;
    }
}
