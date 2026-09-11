using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Telemetry;

public sealed class ShareConsumerTelemetryMetricsTests
{
    private const string Prefix = "org.apache.kafka.consumer.share.";
    private const string Fetch = Prefix + "fetch.manager.";
    private const string Coordinator = Prefix + "coordinator.";

    [Test]
    public async Task FetchWindows_AccumulateOnceAcrossPartitions_IncludeEmptyRequests()
    {
        var clock = new Clock();
        var metrics = new ShareConsumerTelemetryMetrics(clock.Read, 1000);
        metrics.Subscribe([Fetch]);
        metrics.FetchStarted(1);
        clock.Advance(20);
        metrics.FetchCompleted(1, 7);
        var sample = metrics.GetFetchSample(1)!;
        metrics.Parsed(sample, 80, 2);
        metrics.Parsed(sample, 160, 4);
        metrics.FetchStarted(2);
        clock.Advance(40);
        metrics.FetchCompleted(2, 3);
        clock.Advance(940);
        var snapshot = Collect(metrics);
        await Assert.That(Value(snapshot, Fetch + "fetch.total")).IsEqualTo(2d);
        await Assert.That(Value(snapshot, Fetch + "bytes.consumed.total")).IsEqualTo(240d);
        await Assert.That(Value(snapshot, Fetch + "records.consumed.total")).IsEqualTo(6d);
        await Assert.That(Value(snapshot, Fetch + "fetch.size.avg")).IsEqualTo(120d);
        await Assert.That(Value(snapshot, Fetch + "fetch.size.max")).IsEqualTo(240d);
        await Assert.That(Value(snapshot, Fetch + "records.per.request.avg")).IsEqualTo(3d);
        await Assert.That(Value(snapshot, Fetch + "records.per.request.max")).IsEqualTo(6d);
        await Assert.That(Value(snapshot, Fetch + "fetch.latency.avg")).IsEqualTo(30d);
        await Assert.That(Value(snapshot, Fetch + "fetch.latency.max")).IsEqualTo(40d);
        await Assert.That(Value(snapshot, Fetch + "fetch.throttle.time.avg")).IsEqualTo(5d);
        await Assert.That(Value(snapshot, Fetch + "fetch.throttle.time.max")).IsEqualTo(7d);
        await Assert.That(Value(snapshot, Fetch + "fetch.rate")).IsEqualTo(2d);
        await Assert.That(Value(snapshot, Fetch + "records.consumed.rate")).IsEqualTo(6d);
        await Assert.That(Value(snapshot, Fetch + "bytes.consumed.rate")).IsEqualTo(240d);
    }

    [Test]
    public async Task PollAndCoordinator_UseElapsedTime_ExcludeApplicationTimeFromIdle()
    {
        var clock = new Clock();
        var metrics = new ShareConsumerTelemetryMetrics(clock.Read, 1000);
        metrics.Subscribe([Prefix]);
        metrics.PollStarted();
        var waiting = metrics.Timestamp(ShareConsumerTelemetryMetrics.Groups.Poll);
        var heartbeat = metrics.HeartbeatStarted();
        clock.Advance(20);
        metrics.HeartbeatCompleted(heartbeat);
        metrics.Rebalanced();
        clock.Advance(80);
        metrics.PollWaitCompleted(waiting);
        clock.Advance(300);
        metrics.PollStarted();
        clock.Advance(600);
        var snapshot = Collect(metrics);
        await Assert.That(snapshot.Count).IsEqualTo(28);
        await Assert.That(snapshot.All(metric => metric.Attributes.Count == 0)).IsTrue();
        await Assert.That(Value(snapshot, Prefix + "last.poll.seconds.ago")).IsEqualTo(.6d);
        await Assert.That(Value(snapshot, Prefix + "time.between.poll.avg")).IsEqualTo(400d);
        await Assert.That(Value(snapshot, Prefix + "time.between.poll.max")).IsEqualTo(400d);
        await Assert.That(Value(snapshot, Prefix + "poll.idle.ratio.avg")).IsEqualTo(.25d);
        await Assert.That(Value(snapshot, Coordinator + "heartbeat.response.time.max")).IsEqualTo(20d);
        await Assert.That(Value(snapshot, Coordinator + "heartbeat.total")).IsEqualTo(1d);
        await Assert.That(Value(snapshot, Coordinator + "heartbeat.rate")).IsEqualTo(1d);
        await Assert.That(Value(snapshot, Coordinator + "last.heartbeat.seconds.ago")).IsEqualTo(1d);
        await Assert.That(Value(snapshot, Coordinator + "rebalance.total")).IsEqualTo(1d);
        await Assert.That(Value(snapshot, Coordinator + "rebalance.rate.per.hour")).IsEqualTo(3600d);
    }

    [Test]
    public async Task SubscriptionRefresh_DisablesUnrequestedGroups_AndDisableStopsRecording()
    {
        var metrics = new ShareConsumerTelemetryMetrics();
        metrics.FetchStarted(1);
        await Assert.That(metrics.GetFetchSample(1)).IsNull();
        metrics.Subscribe([Coordinator]);
        await Assert.That(metrics.Enabled(ShareConsumerTelemetryMetrics.Groups.Heartbeat)).IsTrue();
        await Assert.That(metrics.Enabled(ShareConsumerTelemetryMetrics.Groups.Records)).IsFalse();
        metrics.HeartbeatStarted();
        metrics.Subscribe(["com.example."]);
        metrics.HeartbeatStarted();
        metrics.Subscribe([Fetch]);
        metrics.AcknowledgementsSent(3);
        metrics.Disable();
        metrics.AcknowledgementsSent(5);
        var snapshot = Collect(metrics);
        await Assert.That(Value(snapshot, Coordinator + "heartbeat.total")).IsEqualTo(1d);
        await Assert.That(Value(snapshot, Fetch + "acknowledgements.send.total")).IsEqualTo(3d);
        var filtered = Collect(metrics, prefixes: [Coordinator + "heartbeat.total"]);
        await Assert.That(filtered.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AcknowledgementRetries_CountersAndRatesHaveIndependentCollectionState(bool delta)
    {
        var clock = new Clock();
        var metrics = new ShareConsumerTelemetryMetrics(clock.Read, 1000);
        metrics.Subscribe([Fetch]);
        metrics.AcknowledgementsSent(10);
        metrics.AcknowledgementsFailed(4);
        clock.Advance(1000);
        var first = Collect(metrics, delta);
        metrics.AcknowledgementsSent(4);
        clock.Advance(2000);
        var second = Collect(metrics, delta);
        await Assert.That(Value(first, Fetch + "acknowledgements.send.total")).IsEqualTo(10d);
        await Assert.That(Value(first, Fetch + "acknowledgements.error.total")).IsEqualTo(4d);
        await Assert.That(Value(first, Fetch + "acknowledgements.send.rate")).IsEqualTo(10d);
        await Assert.That(Value(first, Fetch + "acknowledgements.error.rate")).IsEqualTo(4d);
        await Assert.That(Value(second, Fetch + "acknowledgements.send.total")).IsEqualTo(delta ? 4d : 14d);
        await Assert.That(Value(second, Fetch + "acknowledgements.error.total")).IsEqualTo(delta ? 0d : 4d);
        await Assert.That(Value(second, Fetch + "acknowledgements.send.rate")).IsEqualTo(2d);
        await Assert.That(Value(second, Fetch + "acknowledgements.error.rate")).IsEqualTo(0d);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task BuiltInPayload_EncodesMonotonicCountersAndGauges(bool delta)
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.ShareConsumer);
        collector.ShareConsumerMetrics!.Subscribe([Prefix]);
        collector.ShareConsumerMetrics.AcknowledgementsSent(3);
        collector.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.share.depth", ApplicationTelemetryMetricKind.Gauge, () => 42,
            new Dictionary<string, string> { ["tenant"] = "north" }));
        var subscription = new ClientTelemetrySubscription(Guid.NewGuid(), 1, 0, 1000, 8192, delta,
            [Fetch + "acknowledgements.send.total", Prefix + "last.poll.seconds.ago", "com.example.share."]);
        var provider = new ClientTelemetryPayloadProvider();
        var payload = provider.Collect(subscription, collector.Collect(subscription), false);
        var decoded = Dekaf.Tests.Integration.Telemetry.MetricsData.Parser.ParseFrom(payload.ToArray())
            .ResourceMetrics.SelectMany(resource => resource.ScopeMetrics).SelectMany(scope => scope.Metrics).ToArray();
        await Assert.That(decoded.Length).IsEqualTo(3);
        var counter = decoded.Single(metric => metric.Name == Fetch + "acknowledgements.send.total");
        await Assert.That(counter.Sum.IsMonotonic).IsTrue();
        await Assert.That((int)counter.Sum.AggregationTemporality).IsEqualTo(delta ? 1 : 2);
        await Assert.That(counter.Sum.DataPoints.Single().AsDouble).IsEqualTo(3d);
        await Assert.That(counter.Sum.DataPoints.Single().Attributes.Count).IsEqualTo(0);
        var gauge = decoded.Single(metric => metric.Name == Prefix + "last.poll.seconds.ago");
        await Assert.That(gauge.Gauge.DataPoints.Count).IsEqualTo(1);
        await Assert.That(gauge.Gauge.DataPoints.Single().AsDouble).IsEqualTo(-1d);
        var application = decoded.Single(metric => metric.Name == "com.example.share.depth");
        await Assert.That(application.Gauge.DataPoints.Single().AsDouble).IsEqualTo(42d);
        await Assert.That(application.Gauge.DataPoints.Single().Attributes.Single().Value.StringValue).IsEqualTo("north");
    }

    [Test]
    public async Task RateSubscription_StartsAtSubscriptionRatherThanClientCreation()
    {
        var clock = new Clock();
        var metrics = new ShareConsumerTelemetryMetrics(clock.Read, 1000);
        clock.Advance(10000);
        metrics.Subscribe([Coordinator]);
        metrics.HeartbeatStarted();
        clock.Advance(1000);
        await Assert.That(Value(Collect(metrics), Coordinator + "heartbeat.rate")).IsEqualTo(1d);
        metrics.Subscribe([Coordinator + "heartbeat.total"]);
        metrics.HeartbeatStarted();
        clock.Advance(10000);
        metrics.Subscribe([Coordinator]);
        metrics.HeartbeatStarted();
        clock.Advance(1000);
        var snapshot = Collect(metrics);
        await Assert.That(Value(snapshot, Coordinator + "heartbeat.rate")).IsEqualTo(1d);
        await Assert.That(Value(snapshot, Coordinator + "heartbeat.total")).IsEqualTo(3d);
    }

    [Test]
    public async Task SubscriptionRefresh_PreservesPendingAcknowledgements_WhenRecordMetricsChange()
    {
        var metrics = new ShareConsumerTelemetryMetrics();
        metrics.Subscribe([Prefix]);
        metrics.StartFetch(1, 2);
        metrics.AcknowledgementRequestStarted(1, 3);
        metrics.Subscribe([Fetch + "acknowledgements."]);
        metrics.FetchCompleted(1, 0);
        metrics.AcknowledgementRequestCompleted(1, true);
        var snapshot = Collect(metrics);
        await Assert.That(Value(snapshot, Fetch + "acknowledgements.send.total")).IsEqualTo(5d);
        await Assert.That(Value(snapshot, Fetch + "acknowledgements.error.total")).IsEqualTo(3d);
        metrics.Subscribe([Prefix]);
        await Assert.That(metrics.GetFetchSample(1)).IsNull();
    }

    [Test]
    public async Task SubscriptionRefresh_DoesNotReuseRequestState_FromAnInactiveSubscription()
    {
        var metrics = new ShareConsumerTelemetryMetrics();
        metrics.Subscribe([Prefix]);
        metrics.StartFetch(1, 2);
        metrics.FetchCompleted(1, 0);
        metrics.Subscribe(["com.example."]);
        metrics.StartFetch(1, 7);
        metrics.Subscribe([Prefix]);
        metrics.FetchCompleted(1, 0);
        var snapshot = Collect(metrics);
        await Assert.That(Value(snapshot, Fetch + "fetch.total")).IsEqualTo(1d);
        await Assert.That(Value(snapshot, Fetch + "acknowledgements.send.total")).IsEqualTo(2d);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SubscriptionRefresh_RecordAveragesExcludeFetchOnlyIntervals(bool failFetch)
    {
        var metrics = new ShareConsumerTelemetryMetrics();
        metrics.Subscribe([Prefix]);
        metrics.FetchStarted(1);
        metrics.FetchCompleted(1, 0);
        metrics.Parsed(metrics.GetFetchSample(1)!, 100, 4);

        metrics.Subscribe([Fetch + "fetch.total"]);
        metrics.FetchStarted(1);
        if (failFetch)
            metrics.FetchFailed(1);
        else
            metrics.FetchCompleted(1, 0);

        // A request submitted without record accounting must also be excluded if
        // its response arrives after the record metrics are selected again.
        metrics.FetchStarted(1);
        metrics.Subscribe([Prefix]);
        metrics.FetchCompleted(1, 0);
        metrics.FetchStarted(1);
        metrics.FetchCompleted(1, 0);
        metrics.Parsed(metrics.GetFetchSample(1)!, 300, 8);
        // An empty/failed request submitted with record accounting is a real
        // zero-record sample, so it must remain in the average's denominator.
        metrics.FetchStarted(1);
        if (failFetch)
            metrics.FetchFailed(1);
        else
            metrics.FetchCompleted(1, 0);

        var snapshot = Collect(metrics);
        await Assert.That(Value(snapshot, Fetch + "fetch.total")).IsEqualTo(5d);
        await Assert.That(Value(snapshot, Fetch + "fetch.size.avg")).IsEqualTo(400d / 3);
        await Assert.That(Value(snapshot, Fetch + "records.per.request.avg")).IsEqualTo(4d);
        await Assert.That(Value(snapshot, Fetch + "bytes.consumed.total")).IsEqualTo(400d);
        await Assert.That(Value(snapshot, Fetch + "records.consumed.total")).IsEqualTo(12d);
    }

    [Test]
    public async Task PollSubscriptionRefresh_DiscardsTheInterruptedRoundBeforeMeasuringNewIntervals()
    {
        var clock = new Clock();
        var metrics = new ShareConsumerTelemetryMetrics(clock.Read, 1000);
        metrics.Subscribe([Prefix]);
        metrics.PollStarted();
        var waiting = metrics.Timestamp(ShareConsumerTelemetryMetrics.Groups.Poll);
        clock.Advance(100);
        metrics.Subscribe(["com.example."]);
        clock.Advance(100);
        metrics.Subscribe([Prefix]);
        clock.Advance(100);
        metrics.PollWaitCompleted(waiting);
        metrics.PollStarted();
        clock.Advance(100);
        metrics.PollStarted();
        await Assert.That(Value(Collect(metrics), Prefix + "poll.idle.ratio.avg")).IsEqualTo(0d);
    }

    private static List<ClientTelemetryMetric> Collect(ShareConsumerTelemetryMetrics metrics,
        bool delta = false, string[]? prefixes = null)
    {
        var subscription = new ClientTelemetrySubscription(Guid.Empty, 1, 0, 1000, 4096, delta, prefixes ?? [Prefix]);
        var snapshot = new List<ClientTelemetryMetric>();
        metrics.Collect(subscription, snapshot);
        return snapshot;
    }

    private static double Value(List<ClientTelemetryMetric> metrics, string name) => metrics.Single(metric => metric.Name == name).Value;
    private sealed class Clock
    {
        private long _now;
        internal long Read() => _now;
        internal void Advance(long milliseconds) => _now += milliseconds;
    }
}
