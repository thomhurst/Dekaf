using Dekaf.ShareConsumer;
using Dekaf.Telemetry;
using Dekaf.Tests.Integration.Telemetry;

namespace Dekaf.Tests.Unit.Telemetry;

public sealed partial class ClientTelemetryManagerTests
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShareConsumer_BuiltInPayloadPreservesTemporalityAndResourceAttribution(bool delta)
    {
        const string name = "org.apache.kafka.consumer.share.fetch.manager.records.consumed.total";
        await using var context = new TelemetryTestContext(shareConsumerOptions: ShareOptions());
        context.Connection.Enqueue(Subscription(Guid.NewGuid(), 7, 60000,
            deltaTemporality: delta, requestedMetrics: [name]));
        await context.Manager.StartAsync();
        var collector = (ClientTelemetryMetricCollector)typeof(KafkaShareConsumer<string, string>)
            .GetField("_telemetryMetricCollector", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(context.ShareConsumer)!;
        await Assert.That(collector.ShareMetrics!.Enabled).IsTrue();
        collector.ShareMetrics.RecordParsed(100, 4);
        await PushShareMetricsAsync(context);
        collector.ShareMetrics.RecordParsed(50, 2);
        await PushShareMetricsAsync(context);
        var pushes = context.Connection.RequestsOfType<Dekaf.Protocol.Messages.PushTelemetryRequest>();
        var first = DecodeShareMetrics(pushes[0]).Single();
        var second = DecodeShareMetrics(pushes[1]).Single();
        await Assert.That(first.Name).IsEqualTo(name);
        await Assert.That(first.Sum.IsMonotonic).IsTrue();
        await Assert.That(first.Sum.AggregationTemporality).IsEqualTo(delta ? 1 : 2);
        await Assert.That(first.Sum.DataPoints.Single().AsDouble).IsEqualTo(4);
        await Assert.That(second.Sum.DataPoints.Single().AsDouble).IsEqualTo(delta ? 2 : 6);
        await Assert.That(first.Sum.DataPoints.Single().Attributes.Count).IsEqualTo(0);
        var resource = MetricsData.Parser.ParseFrom(pushes[0].Metrics.ToArray()).ResourceMetrics.Single().Resource;
        await Assert.That(resource.Attributes.Single(a => a.Key == "group_id").Value.StringValue).IsEqualTo("share-telemetry");
        await Assert.That(resource.Attributes.Any(a => a.Key == "client_id")).IsFalse();
    }
}
