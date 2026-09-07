using Dekaf.Admin;
using Dekaf.Diagnostics;
using Dekaf.Telemetry;
using Dekaf.ShareConsumer;
using Dekaf.Tests.Integration.Telemetry;

namespace Dekaf.Tests.Integration;

[Category("Telemetry")]
[SupportsKafka(420)]
[ClassDataSource<TelemetryReceiverKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ClientTelemetryReceiverIntegrationTests(TelemetryReceiverKafkaContainer kafka)
{
    [Test]
    [Arguments(42.0)]
    [Arguments(84.0)]
    [Timeout(90_000)]
    public async Task Producer_BrokerReceivesBuiltInAndApplicationMetrics(
        double applicationValue, CancellationToken cancellationToken)
    {
        const string applicationName = "com.example.telemetry.integration.depth";
        const string builtinName = "org.apache.kafka.producer.connection.creation.total";
        var clientId = $"telemetry-receiver-{Guid.NewGuid():N}";
        await using var admin = kafka.CreateAdminClient();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.ClientMetrics, Name = clientId }] =
            [
                ConfigAlter.Set("metrics", $"{applicationName},{builtinName}"),
                ConfigAlter.Set("interval.ms", "1000"),
                ConfigAlter.Set("match", $"client_id={clientId}")
            ]
        }, cancellationToken: cancellationToken);

        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId(clientId)
            .RegisterMetricForSubscription(new ApplicationTelemetryMetric(
                applicationName, ApplicationTelemetryMetricKind.Gauge, () => applicationValue))
            .BuildAsync(cancellationToken);
        try
        {
            var identity = ((IKafkaClientInstanceIdentity)producer).ClientInstanceId;
            await Assert.That(identity).IsNotNull();
            var received = await kafka.WaitForPayloadAsync(clientId, payload =>
            {
                var metrics = Decode(payload);
                return !payload.IsTerminating && metrics.Any(metric => metric.Name == applicationName)
                    && metrics.Any(metric => metric.Name == builtinName);
            }, cancellationToken);
            await Assert.That(received.ClientInstanceId).IsEqualTo(identity!.Value);
            await Assert.That(received.Data.Length).IsGreaterThan(0);
            await Assert.That(received.ContentType).IsEqualTo("OTLP");
            var decoded = Decode(received);
            var application = decoded.Single(metric => metric.Name == applicationName);
            var builtin = decoded.Single(metric => metric.Name == builtinName);
            await Assert.That(application.Gauge.DataPoints.Single().AsDouble).IsEqualTo(applicationValue);
            await Assert.That(builtin.Sum.IsMonotonic).IsTrue();
            await Assert.That(builtin.Sum.DataPoints.Single().AsDouble).IsGreaterThan(0);

            await producer.DisposeAsync();
            var terminating = await kafka.WaitForPayloadAsync(clientId,
                payload => payload.IsTerminating && Decode(payload).Any(metric => metric.Name == applicationName),
                cancellationToken);
            await Assert.That(terminating.ClientInstanceId).IsEqualTo(identity.Value);
        }
        finally
        {
            await producer.DisposeAsync();
        }
    }

    [Test]
    [Arguments(42.0)]
    [Arguments(84.0)]
    [Timeout(90_000)]
    public async Task ShareConsumer_BrokerReceivesBuiltInAndApplicationMetrics(
        double applicationValue, CancellationToken cancellationToken)
    {
        const string applicationName = "com.example.telemetry.share.depth";
        const string builtinPrefix = "org.apache.kafka.consumer.share.";
        const string recordsName = builtinPrefix + "fetch.manager.records.consumed.total";
        const string acknowledgementsName = builtinPrefix + "fetch.manager.acknowledgements.send.total";
        var topic = $"share-metrics-{Guid.NewGuid():N}";
        var group = $"share-telemetry-{Guid.NewGuid():N}";
        var clientId = $"share-telemetry-receiver-{Guid.NewGuid():N}";
        await using var admin = kafka.CreateAdminClient();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.ClientMetrics, Name = clientId }] =
            [
                ConfigAlter.Set("metrics", $"{applicationName},{builtinPrefix}"),
                ConfigAlter.Set("interval.ms", "1000"),
                ConfigAlter.Set("match", $"client_id={clientId}")
            ]
        }, cancellationToken: cancellationToken);

        await admin.CreateTopicsAsync([new NewTopic { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }],
            cancellationToken: cancellationToken);
        var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId(clientId)
            .WithGroupId(group)
            .RegisterMetricForSubscription(new ApplicationTelemetryMetric(
                applicationName, ApplicationTelemetryMetricKind.Gauge, () => applicationValue,
                new Dictionary<string, string> { ["tenant"] = clientId }))
            .BuildAsync(cancellationToken);
        try
        {
            var identity = ((IKafkaClientInstanceIdentity)consumer).ClientInstanceId;
            await Assert.That(identity).IsNotNull();
            consumer.Subscribe(topic);
            await ShareConsumerTestHelper.PrimeShareConsumerAsync(consumer);
            await using (var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(kafka.BootstrapServers).BuildAsync(cancellationToken))
            {
                await producer.ProduceAsync(topic, "key", "value", cancellationToken);
            }
            await using (var poll = consumer.PollAsync(cancellationToken).GetAsyncEnumerator(cancellationToken))
            {
                await Assert.That(await poll.MoveNextAsync()).IsTrue();
                await Assert.That(poll.Current.Value).IsEqualTo("value");
                consumer.Acknowledge(poll.Current);
            }
            await consumer.CommitAsync(cancellationToken);
            var received = await kafka.WaitForPayloadAsync(clientId,
                payload => !payload.IsTerminating && Decode(payload).Any(metric => metric.Name == applicationName)
                    && Decode(payload).Any(metric => metric.Name == recordsName && metric.Sum.DataPoints.Any(p => p.AsDouble > 0)),
                cancellationToken);
            // Delta subscriptions can place fetch and acknowledgement counters in different pushes.
            _ = await kafka.WaitForPayloadAsync(clientId,
                payload => !payload.IsTerminating && Decode(payload).Any(metric => metric.Name == applicationName)
                    && Decode(payload).Any(metric => metric.Name == acknowledgementsName && metric.Sum.DataPoints.Any(p => p.AsDouble > 0)),
                cancellationToken);
            foreach (var name in new[] { builtinPrefix + "coordinator.heartbeat.total", builtinPrefix + "coordinator.rebalance.total" })
            {
                _ = await kafka.WaitForPayloadAsync(clientId,
                    payload => Decode(payload).Any(metric => metric.Name == name && metric.Sum.DataPoints.Any(p => p.AsDouble > 0)),
                    cancellationToken);
            }
            await Assert.That(received.ClientInstanceId).IsEqualTo(identity!.Value);
            await Assert.That(received.ContentType).IsEqualTo("OTLP");
            var decoded = Decode(received);
            await Assert.That(decoded.Any(metric => metric.Name == builtinPrefix + "coordinator.heartbeat.total")).IsTrue();
            var resource = MetricsData.Parser.ParseFrom(received.Data).ResourceMetrics.Single().Resource;
            await Assert.That(resource.Attributes.Single(a => a.Key == "group_id").Value.StringValue).IsEqualTo(group);
            var application = decoded.Single(metric => metric.Name == applicationName);
            var point = application.Gauge.DataPoints.Single();
            await Assert.That(point.AsDouble).IsEqualTo(applicationValue);
            await Assert.That(point.Attributes.Single(attribute => attribute.Key == "tenant").Value.StringValue)
                .IsEqualTo(clientId);

            await consumer.DisposeAsync();
            var terminating = await kafka.WaitForPayloadAsync(clientId,
                payload => payload.IsTerminating && Decode(payload).Any(metric => metric.Name == applicationName),
                cancellationToken);
            await Assert.That(terminating.ClientInstanceId).IsEqualTo(identity.Value);
        }
        finally
        {
            await consumer.DisposeAsync();
        }
    }

    private static Metric[] Decode(ReceivedTelemetry payload) => MetricsData.Parser.ParseFrom(payload.Data)
        .ResourceMetrics.SelectMany(resource => resource.ScopeMetrics)
        .SelectMany(scope => scope.Metrics).ToArray();
}
