using System.Buffers;
using System.Reflection;
using Dekaf.Compression;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;
using Dekaf.Telemetry;
using Dekaf.Tests.Integration.Telemetry;

namespace Dekaf.Tests.Unit.Telemetry;

public sealed partial class ClientTelemetryManagerTests
{
    [Test]
    public async Task ShareConsumer_TerminatingPushContainsRegisteredApplicationMetric()
    {
        const string name = "com.example.share.depth";
        await using var context = new TelemetryTestContext(shareConsumerOptions: new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "share-telemetry",
            ApplicationMetrics = [new ApplicationTelemetryMetric(name, ApplicationTelemetryMetricKind.Gauge, () => 42)]
        });
        var identity = Guid.NewGuid();
        context.Connection.Enqueue(Subscription(identity, 7, 60000, requestedMetrics: ["com.example.share."]));
        await context.Manager.StartAsync();
        await context.ShareConsumer!.CloseAsync();
        var pushes = context.Connection.RequestsOfType<PushTelemetryRequest>();
        await Assert.That(pushes).HasSingleItem();
        var push = pushes[0];
        await Assert.That(push.ClientInstanceId).IsEqualTo(identity);
        await Assert.That(push.Terminating).IsTrue();
        var decoded = DecodeShareMetrics(push);
        await Assert.That(decoded).HasSingleItem();
        await Assert.That(decoded[0].Name).IsEqualTo(name);
        await Assert.That(decoded[0].Gauge.DataPoints.Single().AsDouble).IsEqualTo(42);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShareConsumer_CountersFollowSubscriptionTemporality(bool delta)
    {
        double value = 3;
        await using var context = new TelemetryTestContext(shareConsumerOptions: ShareOptions(
            new ApplicationTelemetryMetric("com.example.share.count", ApplicationTelemetryMetricKind.Counter, () => value)));
        context.Connection.Enqueue(Subscription(Guid.NewGuid(), 7, 60000,
            deltaTemporality: delta, requestedMetrics: ["com.example.share."]));
        await context.Manager.StartAsync();
        await PushShareMetricsAsync(context);
        value = 8;
        await PushShareMetricsAsync(context);
        var pushes = context.Connection.RequestsOfType<PushTelemetryRequest>();
        var first = DecodeShareMetrics(pushes[0]).Single().Sum;
        var second = DecodeShareMetrics(pushes[1]).Single().Sum;
        await Assert.That(first.IsMonotonic).IsTrue();
        await Assert.That(first.AggregationTemporality).IsEqualTo(delta ? 1 : 2);
        await Assert.That(first.DataPoints.Single().AsDouble).IsEqualTo(3);
        await Assert.That(second.DataPoints.Single().AsDouble).IsEqualTo(delta ? 5 : 8);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShareConsumer_RequestedPrefixesControlObservation(bool emptySubscription)
    {
        var requestedCalls = 0;
        var excludedCalls = 0;
        await using var context = new TelemetryTestContext(shareConsumerOptions: ShareOptions());
        context.ShareConsumer!.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.share.depth", ApplicationTelemetryMetricKind.Gauge, () => ++requestedCalls));
        context.ShareConsumer.RegisterMetricForSubscription(new ApplicationTelemetryMetric(
            "com.example.other.depth", ApplicationTelemetryMetricKind.Gauge, () => ++excludedCalls));
        context.Connection.Enqueue(Subscription(Guid.NewGuid(), 7, 60000,
            requestedMetrics: emptySubscription ? [] : ["com.example.share."]));
        await context.Manager.StartAsync();
        await PushShareMetricsAsync(context);
        var metrics = DecodeShareMetrics(context.Connection.RequestsOfType<PushTelemetryRequest>().Single());
        await Assert.That(requestedCalls).IsEqualTo(emptySubscription ? 0 : 1);
        await Assert.That(excludedCalls).IsEqualTo(0);
        await Assert.That(metrics.Length).IsEqualTo(emptySubscription ? 0 : 1);
        if (!emptySubscription)
            await Assert.That(metrics[0].Name).IsEqualTo("com.example.share.depth");
    }

    [Test]
    public async Task ShareConsumer_ReplacementAndRemovalResetCounterHistory()
    {
        const string name = "com.example.share.count";
        await using var context = new TelemetryTestContext(shareConsumerOptions: ShareOptions(
            new ApplicationTelemetryMetric(name, ApplicationTelemetryMetricKind.Counter, () => 10)));
        context.Connection.Enqueue(Subscription(Guid.NewGuid(), 7, 60000, requestedMetrics: [name]));
        await context.Manager.StartAsync();
        await PushShareMetricsAsync(context);
        context.ShareConsumer!.RegisterMetricForSubscription(
            new ApplicationTelemetryMetric(name, ApplicationTelemetryMetricKind.Counter, () => 20));
        await PushShareMetricsAsync(context);
        context.ShareConsumer.UnregisterMetricFromSubscription(name);
        context.ShareConsumer.UnregisterMetricFromSubscription("missing");
        await PushShareMetricsAsync(context);
        context.ShareConsumer.RegisterMetricForSubscription(
            new ApplicationTelemetryMetric(name, ApplicationTelemetryMetricKind.Counter, () => 30));
        await PushShareMetricsAsync(context);
        var pushes = context.Connection.RequestsOfType<PushTelemetryRequest>();
        await Assert.That(DecodeShareMetrics(pushes[0]).Single().Sum.DataPoints.Single().AsDouble).IsEqualTo(10);
        await Assert.That(DecodeShareMetrics(pushes[1]).Single().Sum.DataPoints.Single().AsDouble).IsEqualTo(20);
        await Assert.That(pushes[2].Metrics.IsEmpty).IsTrue();
        await Assert.That(DecodeShareMetrics(pushes[3]).Single().Sum.DataPoints.Single().AsDouble).IsEqualTo(30);
    }

    [Test]
    public async Task ShareConsumer_OptionsAndAttributesAreSnapshottedPerClient()
    {
        const string name = "com.example.share.depth";
        var attributes = new Dictionary<string, string> { ["team"] = "first" };
        var metric = new ApplicationTelemetryMetric(name, ApplicationTelemetryMetricKind.Gauge, () => 42, attributes);
        var optionsMetrics = new List<ApplicationTelemetryMetric> { metric };
        await using var first = new TelemetryTestContext(shareConsumerOptions: new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], GroupId = "first", ApplicationMetrics = optionsMetrics
        });
        attributes["team"] = "changed";
        optionsMetrics[0] = new ApplicationTelemetryMetric(name, ApplicationTelemetryMetricKind.Gauge, () => 99);
        await using var second = new TelemetryTestContext(shareConsumerOptions: ShareOptions(
            new ApplicationTelemetryMetric(name, ApplicationTelemetryMetricKind.Gauge, () => 7,
                new Dictionary<string, string> { ["team"] = "second" })));
        foreach (var context in new[] { first, second })
        {
            context.Connection.Enqueue(Subscription(Guid.NewGuid(), 7, 60000, requestedMetrics: [name]));
            await context.Manager.StartAsync();
            await PushShareMetricsAsync(context);
        }
        var firstPoint = DecodeShareMetrics(first.Connection.RequestsOfType<PushTelemetryRequest>().Single())
            .Single().Gauge.DataPoints.Single();
        var secondPoint = DecodeShareMetrics(second.Connection.RequestsOfType<PushTelemetryRequest>().Single())
            .Single().Gauge.DataPoints.Single();
        await Assert.That(firstPoint.AsDouble).IsEqualTo(42);
        await Assert.That(firstPoint.Attributes.Single().Value.StringValue).IsEqualTo("first");
        await Assert.That(secondPoint.AsDouble).IsEqualTo(7);
        await Assert.That(secondPoint.Attributes.Single().Value.StringValue).IsEqualTo("second");
    }

    [Test]
    [Arguments((sbyte)0)]
    [Arguments((sbyte)1)]
    public async Task ShareConsumer_PushUsesNegotiatedCompression(sbyte compression)
    {
        await using var context = new TelemetryTestContext(shareConsumerOptions: ShareOptions(
            new ApplicationTelemetryMetric("com.example.share.depth", ApplicationTelemetryMetricKind.Gauge, () => 42)));
        context.Connection.Enqueue(Subscription(Guid.NewGuid(), 7, 60000,
            acceptedCompressionTypes: [compression], requestedMetrics: ["com.example.share."]));
        await context.Manager.StartAsync();
        await PushShareMetricsAsync(context);
        var push = context.Connection.RequestsOfType<PushTelemetryRequest>().Single();
        await Assert.That(push.CompressionType).IsEqualTo(compression);
        await Assert.That(DecodeShareMetrics(push).Single().Gauge.DataPoints.Single().AsDouble).IsEqualTo(42);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShareConsumer_OverlargePayloadUsesExistingEmptyFallback(bool brokerRejects)
    {
        await using var context = new TelemetryTestContext(shareConsumerOptions: ShareOptions(
            new ApplicationTelemetryMetric("com.example.share.depth", ApplicationTelemetryMetricKind.Gauge, () => 42)));
        context.Connection.Enqueue(Subscription(Guid.NewGuid(), 7, 60000,
            telemetryMaxBytes: brokerRejects ? 1024 : 1, requestedMetrics: ["com.example.share."]));
        if (brokerRejects)
            context.Connection.Enqueue(new PushTelemetryResponse { ErrorCode = ErrorCode.TelemetryTooLarge });
        await context.Manager.StartAsync();
        await PushShareMetricsAsync(context);
        var pushes = context.Connection.RequestsOfType<PushTelemetryRequest>();
        await Assert.That(pushes.Count).IsEqualTo(brokerRejects ? 2 : 1);
        await Assert.That(pushes[^1].Metrics.IsEmpty).IsTrue();
        if (brokerRejects)
            await Assert.That(DecodeShareMetrics(pushes[0])).HasSingleItem();
    }

    [Test]
    public async Task ShareConsumer_UnsupportedTelemetryNeverObservesApplicationMetrics()
    {
        var calls = 0;
        await using var context = new TelemetryTestContext(shareConsumerOptions: ShareOptions(
            new ApplicationTelemetryMetric("com.example.share.depth", ApplicationTelemetryMetricKind.Gauge, () => ++calls)));
        context.Connection.Enqueue(new GetTelemetrySubscriptionsResponse
        {
            ErrorCode = ErrorCode.UnsupportedVersion
        });
        await context.Manager.StartAsync();
        await context.ShareConsumer!.CloseAsync();
        await Assert.That(context.Manager.IsDisabled).IsTrue();
        await Assert.That(context.Connection.RequestsOfType<PushTelemetryRequest>()).IsEmpty();
        await Assert.That(calls).IsEqualTo(0);
    }

    private static ShareConsumerOptions ShareOptions(params ApplicationTelemetryMetric[] metrics) => new()
    {
        BootstrapServers = ["localhost:9092"], GroupId = "share-telemetry", ApplicationMetrics = metrics
    };

    private static async Task PushShareMetricsAsync(TelemetryTestContext context)
    {
        var subscription = (ClientTelemetrySubscription)typeof(ClientTelemetryManager)
            .GetField("_subscription", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context.Manager)!;
        var pending = (ValueTask<ErrorCode>)typeof(ClientTelemetryManager)
            .GetMethod("PushTelemetryAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(context.Manager, [subscription, false, CancellationToken.None])!;
        await Assert.That(await pending).IsEqualTo(ErrorCode.None);
    }

    private static Metric[] DecodeShareMetrics(PushTelemetryRequest push)
    {
        var bytes = push.Metrics;
        if (!bytes.IsEmpty && push.CompressionType != 0)
        {
            var decompressed = new ArrayBufferWriter<byte>();
            CompressionCodecRegistry.Default.GetCodec((Dekaf.Protocol.Records.CompressionType)push.CompressionType)
                .Decompress(new ReadOnlySequence<byte>(bytes), decompressed);
            bytes = decompressed.WrittenMemory;
        }
        return MetricsData.Parser.ParseFrom(bytes.ToArray()).ResourceMetrics
            .SelectMany(resource => resource.ScopeMetrics).SelectMany(scope => scope.Metrics).ToArray();
    }
}
