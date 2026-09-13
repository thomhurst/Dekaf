using Dekaf.Telemetry;
using Dekaf.Tools.Telemetry;

namespace Dekaf.Tests.Unit.Telemetry;

public sealed class ClientTelemetryResourceTests
{
    [Test]
    [Arguments(1)]
    [Arguments(128)]
    [Arguments(16384)]
    public async Task Collect_EncodesResourceAttributesSeparatelyFromDataPoints(int length)
    {
        var value = new string('\u754c', length) + "\U0001F680";
        var snapshot = Snapshot() with
        {
            ResourceAttributes = new(value, "group-\u03b1", "instance-\u03b2", "member-\u03b3", "transaction-\u03b4")
        };
        var resourceMetrics = Decode(snapshot);
        var attributes = resourceMetrics.Resource.Attributes.ToDictionary(a => a.Key, a => a.Value.StringValue);
        await Assert.That(attributes.Count).IsEqualTo(5);
        await Assert.That(attributes["client_rack"]).IsEqualTo(value);
        await Assert.That(attributes["group_id"]).IsEqualTo("group-\u03b1");
        await Assert.That(attributes["group_instance_id"]).IsEqualTo("instance-\u03b2");
        await Assert.That(attributes["group_member_id"]).IsEqualTo("member-\u03b3");
        await Assert.That(attributes["transactional_id"]).IsEqualTo("transaction-\u03b4");
        var point = resourceMetrics.ScopeMetrics.Single().Metrics.Single().Gauge.DataPoints.Single();
        await Assert.That(point.AsDouble).IsEqualTo(42d);
        await Assert.That(point.Attributes.Select(a => a.Key)).IsEquivalentTo(["node_id", "tenant"]);
        await Assert.That(point.Attributes.Single(a => a.Key == "tenant").Value.StringValue).IsEqualTo("application");
    }

    [Test]
    public async Task Collect_OmitsAbsentAndEmptyOptionalAttributes()
    {
        var snapshot = Snapshot() with { ResourceAttributes = new("", "group", null, "", null) };
        var attributes = Decode(snapshot).Resource.Attributes;
        await Assert.That(attributes.Count).IsEqualTo(1);
        await Assert.That(attributes.Single().Key).IsEqualTo("group_id");
        await Assert.That(Decode(Snapshot()).Resource).IsNull();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Collect_SamplesMembershipOncePerPushAndPreservesEarlierSnapshots(bool delta)
    {
        var calls = 0;
        string? member = null;
        var collector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer)
        {
            ResourceAttributesProvider = () =>
            {
                calls++;
                return new(GroupId: "group", GroupMemberId: member);
            }
        };
        collector.RegisterMetricForSubscription(new("com.example.depth", ApplicationTelemetryMetricKind.Gauge, static () => 42));
        var subscription = Subscription(delta);
        var beforeJoin = collector.Collect(subscription);
        member = "member-a";
        var joined = collector.Collect(subscription);
        member = "member-b";
        var rejoined = collector.Collect(subscription);
        member = null;
        var left = collector.Collect(subscription);
        await Assert.That(calls).IsEqualTo(4);
        await Assert.That(Decode(beforeJoin).Resource.Attributes.Any(a => a.Key == "group_member_id")).IsFalse();
        await Assert.That(Decode(joined).Resource.Attributes.Single(a => a.Key == "group_member_id").Value.StringValue).IsEqualTo("member-a");
        await Assert.That(Decode(rejoined).Resource.Attributes.Single(a => a.Key == "group_member_id").Value.StringValue).IsEqualTo("member-b");
        await Assert.That(Decode(left).Resource.Attributes.Any(a => a.Key == "group_member_id")).IsFalse();
        collector.Collect(subscription with { RequestedMetrics = [] });
        collector.Collect(subscription with { RequestedMetrics = ["unmatched."] });
        await Assert.That(calls).IsEqualTo(4);
    }

    [Test]
    public async Task Collect_MultipleClientsNeverShareResourceAttributes()
    {
        var producer = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Producer)
        {
            ResourceAttributesProvider = static () => new(TransactionalId: "producer-tx")
        };
        var consumer = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Consumer)
        {
            ResourceAttributesProvider = static () => new(GroupId: "consumer-group")
        };
        var admin = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Admin);
        foreach (var collector in new[] { producer, consumer, admin })
            collector.RegisterMetricForSubscription(new("com.example.depth", ApplicationTelemetryMetricKind.Gauge, static () => 1));
        await Assert.That(Decode(producer.Collect(Subscription())).Resource.Attributes.Select(a => a.Key)).IsEquivalentTo(["transactional_id"]);
        await Assert.That(Decode(consumer.Collect(Subscription())).Resource.Attributes.Select(a => a.Key)).IsEquivalentTo(["group_id"]);
        await Assert.That(Decode(admin.Collect(Subscription())).Resource).IsNull();
    }

    private static ClientTelemetryMetricSnapshot Snapshot() => new(false,
        [new ClientTelemetryMetric("com.example.depth", ClientTelemetryMetricKind.Gauge, 42,
            [new("node_id", "3"), new("tenant", "application")])]);

    private static ClientTelemetrySubscription Subscription(bool delta = false) =>
        new(Guid.NewGuid(), 1, 0, 1000, 1000000, delta, [string.Empty]);

    private static ResourceMetrics Decode(ClientTelemetryMetricSnapshot snapshot) =>
        MetricsData.Parser.ParseFrom(new ClientTelemetryPayloadProvider().Collect(Subscription(), snapshot, false).ToArray())
            .ResourceMetrics.Single();
}
