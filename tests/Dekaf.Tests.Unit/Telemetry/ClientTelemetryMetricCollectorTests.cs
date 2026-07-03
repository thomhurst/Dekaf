using Dekaf.Telemetry;

namespace Dekaf.Tests.Unit.Telemetry;

public sealed class ClientTelemetryMetricCollectorTests
{
    [Test]
    public async Task Collect_CumulativeProducerMetrics_ReturnsRequiredMetrics()
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer);
        collector.RecordConnectionCreated();
        collector.RecordConnectionCreated();
        collector.RecordRequestLatency(1, TimeSpan.FromMilliseconds(10));
        collector.RecordRequestLatency(1, TimeSpan.FromMilliseconds(30));
        collector.RecordRequestLatency(2, TimeSpan.FromMilliseconds(20));

        var snapshot = collector.Collect(Subscription(
            deltaTemporality: false,
            "org.apache.kafka.producer."));

        await Assert.That(snapshot.DeltaTemporality).IsFalse();
        await Assert.That(snapshot.Metrics.Count).IsEqualTo(5);
        await Assert.That(Metric(snapshot, ClientTelemetryMetricNames.ProducerConnectionCreationTotal).Value)
            .IsEqualTo(2.0);
        await Assert.That(Metric(snapshot, ClientTelemetryMetricNames.ProducerNodeRequestLatencyAvg, "1").Value)
            .IsEqualTo(20.0);
        await Assert.That(Metric(snapshot, ClientTelemetryMetricNames.ProducerNodeRequestLatencyMax, "1").Value)
            .IsEqualTo(30.0);
        await Assert.That(Metric(snapshot, ClientTelemetryMetricNames.ProducerNodeRequestLatencyAvg, "2").Value)
            .IsEqualTo(20.0);
        await Assert.That(Metric(snapshot, ClientTelemetryMetricNames.ProducerNodeRequestLatencyMax, "2").Value)
            .IsEqualTo(20.0);
    }

    [Test]
    public async Task Collect_FiltersByBrokerRequestedMetricPrefixes()
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer);
        collector.RecordConnectionCreated();
        collector.RecordRequestLatency(3, TimeSpan.FromMilliseconds(12));

        var snapshot = collector.Collect(Subscription(
            deltaTemporality: false,
            "org.apache.kafka.consumer.node.request.latency."));

        await Assert.That(snapshot.Metrics.Count).IsEqualTo(2);
        await Assert.That(snapshot.Metrics.Any(m => m.Name == ClientTelemetryMetricNames.ConsumerConnectionCreationTotal))
            .IsFalse();
        await Assert.That(Metric(snapshot, ClientTelemetryMetricNames.ConsumerNodeRequestLatencyAvg, "3").Value)
            .IsEqualTo(12.0);
        await Assert.That(Metric(snapshot, ClientTelemetryMetricNames.ConsumerNodeRequestLatencyMax, "3").Value)
            .IsEqualTo(12.0);
    }

    [Test]
    public async Task Collect_DeltaTemporality_ResetsCollectedMetrics()
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer);
        collector.RecordConnectionCreated();
        collector.RecordConnectionCreated();
        collector.RecordRequestLatency(1, TimeSpan.FromMilliseconds(5));
        collector.RecordRequestLatency(1, TimeSpan.FromMilliseconds(15));

        var first = collector.Collect(Subscription(deltaTemporality: true, string.Empty));
        var second = collector.Collect(Subscription(deltaTemporality: true, string.Empty));

        collector.RecordConnectionCreated();
        collector.RecordRequestLatency(1, TimeSpan.FromMilliseconds(25));
        var third = collector.Collect(Subscription(deltaTemporality: true, string.Empty));

        await Assert.That(first.DeltaTemporality).IsTrue();
        await Assert.That(Metric(first, ClientTelemetryMetricNames.ProducerConnectionCreationTotal).Value)
            .IsEqualTo(2.0);
        await Assert.That(Metric(first, ClientTelemetryMetricNames.ProducerNodeRequestLatencyAvg, "1").Value)
            .IsEqualTo(10.0);
        await Assert.That(Metric(first, ClientTelemetryMetricNames.ProducerNodeRequestLatencyMax, "1").Value)
            .IsEqualTo(15.0);

        await Assert.That(second.Metrics.Count).IsEqualTo(0);

        await Assert.That(Metric(third, ClientTelemetryMetricNames.ProducerConnectionCreationTotal).Value)
            .IsEqualTo(1.0);
        await Assert.That(Metric(third, ClientTelemetryMetricNames.ProducerNodeRequestLatencyAvg, "1").Value)
            .IsEqualTo(25.0);
        await Assert.That(Metric(third, ClientTelemetryMetricNames.ProducerNodeRequestLatencyMax, "1").Value)
            .IsEqualTo(25.0);
    }

    [Test]
    public async Task Collect_ApplicationMetric_ReturnsOnlyWhenRequested()
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Admin);
        collector.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.queue.depth",
            ApplicationTelemetryMetricKind.Gauge,
            () => 42.5,
            new Dictionary<string, string> { ["queue"] = "orders" }));

        var included = collector.Collect(Subscription(deltaTemporality: false, "com.example."));
        var excluded = collector.Collect(Subscription(deltaTemporality: false, "org.apache.kafka."));

        await Assert.That(included.Metrics.Count).IsEqualTo(1);
        await Assert.That(excluded.Metrics.Count).IsEqualTo(0);

        var metric = included.Metrics[0];
        await Assert.That(metric.Name).IsEqualTo("com.example.queue.depth");
        await Assert.That(metric.Kind).IsEqualTo(ClientTelemetryMetricKind.Gauge);
        await Assert.That(metric.Value).IsEqualTo(42.5);
        await Assert.That(metric.Attributes.Single(a => a.Name == "queue").Value).IsEqualTo("orders");
    }

    [Test]
    public async Task RegisterMetricForSubscription_DuplicateName_ReplacesMetric()
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Admin);
        collector.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.total",
            ApplicationTelemetryMetricKind.Gauge,
            () => 1));
        collector.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.total",
            ApplicationTelemetryMetricKind.Counter,
            () => 2));

        var snapshot = collector.Collect(Subscription(deltaTemporality: true, "com.example."));

        await Assert.That(snapshot.Metrics.Count).IsEqualTo(1);
        await Assert.That(snapshot.Metrics[0].Name).IsEqualTo("com.example.total");
        await Assert.That(snapshot.Metrics[0].Kind).IsEqualTo(ClientTelemetryMetricKind.Counter);
        await Assert.That(snapshot.Metrics[0].Value).IsEqualTo(2.0);
    }

    [Test]
    public async Task RegisterMetricForSubscription_CounterReplacement_ResetsDeltaBaseline()
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Admin);
        collector.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.total",
            ApplicationTelemetryMetricKind.Counter,
            () => 100));
        var first = collector.Collect(Subscription(deltaTemporality: true, "com.example."));

        collector.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.total",
            ApplicationTelemetryMetricKind.Counter,
            () => 150));
        var second = collector.Collect(Subscription(deltaTemporality: true, "com.example."));

        await Assert.That(first.Metrics[0].Value).IsEqualTo(100.0);
        await Assert.That(second.Metrics[0].Value).IsEqualTo(150.0);
    }

    [Test]
    public async Task Collect_ApplicationCounterDeltaTemporality_ReturnsDelta()
    {
        double total = 5;
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Admin);
        collector.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.processed.total",
            ApplicationTelemetryMetricKind.Counter,
            () => total));

        var first = collector.Collect(Subscription(deltaTemporality: true, "com.example."));
        total = 8;
        var second = collector.Collect(Subscription(deltaTemporality: true, "com.example."));

        await Assert.That(first.Metrics[0].Value).IsEqualTo(5.0);
        await Assert.That(second.Metrics[0].Value).IsEqualTo(3.0);
    }

    [Test]
    public async Task UnregisterMetricFromSubscription_RemovesMetricAndIgnoresMissingName()
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Admin);
        collector.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.depth",
            ApplicationTelemetryMetricKind.Gauge,
            () => 3));

        await Assert.That(() => collector.UnregisterMetricFromSubscription("com.example.missing"))
            .ThrowsNothing();
        collector.UnregisterMetricFromSubscription("com.example.depth");

        var snapshot = collector.Collect(Subscription(deltaTemporality: true, "com.example."));

        await Assert.That(snapshot.Metrics.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Collect_ApplicationMetricObserverFails_SkipsMetric()
    {
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Admin);
        collector.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.bad",
            ApplicationTelemetryMetricKind.Gauge,
            () => throw new InvalidOperationException("boom")));
        collector.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.nan",
            ApplicationTelemetryMetricKind.Gauge,
            () => double.NaN));

        var snapshot = collector.Collect(Subscription(deltaTemporality: true, "com.example."));

        await Assert.That(snapshot.Metrics.Count).IsEqualTo(0);
    }

    private static ClientTelemetrySubscription Subscription(
        bool deltaTemporality,
        params string[] requestedMetrics) =>
        new(
            ClientInstanceId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SubscriptionId: 1,
            CompressionType: 0,
            PushIntervalMs: 60000,
            TelemetryMaxBytes: 1024,
            DeltaTemporality: deltaTemporality,
            RequestedMetrics: requestedMetrics);

    private static ClientTelemetryMetric Metric(
        ClientTelemetryMetricSnapshot snapshot,
        string name,
        string? nodeId = null) =>
        snapshot.Metrics.Single(m =>
            m.Name == name &&
            (nodeId is null || m.Attributes.Any(a => a.Name == "node_id" && a.Value == nodeId)));
}
