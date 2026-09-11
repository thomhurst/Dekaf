using Dekaf.Admin;
using Dekaf.Diagnostics;
using Dekaf.Telemetry;
using Dekaf.ShareConsumer;
using Dekaf.Tools.Telemetry;

namespace Dekaf.Tests.Integration;

[Category("Telemetry")]
[SupportsKafka(420)]
[ClassDataSource<TelemetryReceiverKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ClientTelemetryReceiverIntegrationTests(TelemetryReceiverKafkaContainer kafka)
{
    [Test]
    [Timeout(90_000)]
    public async Task SubscriptionProbe_DistinguishesUnconfiguredAndReadyClients(CancellationToken cancellationToken)
    {
        const string metricName = "com.example.telemetry.readiness";
        var clientId = $"telemetry-readiness-{Guid.NewGuid():N}";
        var unconfigured = await kafka.ReadSubscriptionAsync(clientId, cancellationToken);
        await Assert.That(unconfigured.ErrorCode).IsEqualTo(Protocol.ErrorCode.None);
        await Assert.That(unconfigured.RequestedMetrics.Count).IsEqualTo(0);
        await Assert.That(unconfigured.PushIntervalMs).IsGreaterThan(30_000);

        await using var admin = kafka.CreateAdminClient();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.ClientMetrics, Name = clientId }] =
            [
                ConfigAlter.Set("metrics", metricName),
                ConfigAlter.Set("interval.ms", "500"),
                ConfigAlter.Set("match", $"client_id={clientId}")
            ]
        }, cancellationToken: cancellationToken);

        var ready = await kafka.WaitForSubscriptionAsync(clientId, [metricName], 500, cancellationToken);
        await Assert.That(ready.RequestedMetrics).Contains(metricName);
        await Assert.That(ready.PushIntervalMs).IsEqualTo(500);
    }

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

        await kafka.WaitForSubscriptionAsync(clientId, [applicationName, builtinName], 1000, cancellationToken);

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
    public async Task ShareConsumer_BrokerReceivesApplicationMetric(
        double applicationValue, CancellationToken cancellationToken)
    {
        const string applicationName = "com.example.telemetry.share.depth";
        var clientId = $"share-telemetry-receiver-{Guid.NewGuid():N}";
        await using var admin = kafka.CreateAdminClient();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.ClientMetrics, Name = clientId }] =
            [
                ConfigAlter.Set("metrics", applicationName),
                ConfigAlter.Set("interval.ms", "1000"),
                ConfigAlter.Set("match", $"client_id={clientId}")
            ]
        }, cancellationToken: cancellationToken);

        await kafka.WaitForSubscriptionAsync(clientId, [applicationName], 1000, cancellationToken);

        var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId(clientId)
            .WithGroupId($"share-telemetry-{Guid.NewGuid():N}")
            .RegisterMetricForSubscription(new ApplicationTelemetryMetric(
                applicationName, ApplicationTelemetryMetricKind.Gauge, () => applicationValue,
                new Dictionary<string, string> { ["tenant"] = clientId }))
            .BuildAsync(cancellationToken);
        try
        {
            var identity = ((IKafkaClientInstanceIdentity)consumer).ClientInstanceId;
            await Assert.That(identity).IsNotNull();
            var received = await kafka.WaitForPayloadAsync(clientId,
                payload => !payload.IsTerminating && Decode(payload).Any(metric => metric.Name == applicationName),
                cancellationToken);
            await Assert.That(received.ClientInstanceId).IsEqualTo(identity!.Value);
            await Assert.That(received.ContentType).IsEqualTo("OTLP");
            var application = Decode(received).Single(metric => metric.Name == applicationName);
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

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(90_000)]
    public async Task ShareConsumer_BrokerReceivesBuiltInsDuringFetchAndAcknowledgement(
        bool batchApi, CancellationToken cancellationToken)
    {
        const string applicationName = "com.example.telemetry.share.processing";
        const string recordsName = "org.apache.kafka.consumer.share.fetch.manager.records.consumed.total";
        const string acknowledgementsName = "org.apache.kafka.consumer.share.fetch.manager.acknowledgements.send.total";
        const string acknowledgementErrorsName = "org.apache.kafka.consumer.share.fetch.manager.acknowledgements.error.total";
        const string fetchesName = "org.apache.kafka.consumer.share.fetch.manager.fetch.total";
        var clientId = $"share-builtins-{Guid.NewGuid():N}";
        var group = $"share-builtins-group-{Guid.NewGuid():N}";
        var topic = await kafka.CreateTestTopicAsync(partitions: 1);
        await using var admin = kafka.CreateAdminClient();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.ClientMetrics, Name = clientId }] =
            [
                ConfigAlter.Set("metrics", $"{applicationName},{recordsName},{acknowledgementsName},{acknowledgementErrorsName},{fetchesName}"),
                ConfigAlter.Set("interval.ms", "1000"),
                ConfigAlter.Set("match", $"client_id={clientId}")
            ],
            [new ConfigResource { Type = ConfigResourceType.Group, Name = group }] =
                [ConfigAlter.Set("share.auto.offset.reset", "earliest")]
        }, cancellationToken: cancellationToken);
        await kafka.WaitForSubscriptionAsync(clientId,
            [applicationName, recordsName, acknowledgementsName, acknowledgementErrorsName, fetchesName], 1000, cancellationToken);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers).BuildAsync(cancellationToken);
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 2);
        var applicationValue = 42;
        await using var consumer = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers).WithClientId(clientId).WithGroupId(group)
            .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
            .RegisterMetricForSubscription(new ApplicationTelemetryMetric(applicationName,
                ApplicationTelemetryMetricKind.Gauge, () => Volatile.Read(ref applicationValue)))
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);
        var values = new HashSet<string?>();
        if (batchApi)
        {
            await foreach (var batch in consumer.PollBatchesAsync(cancellationToken))
            {
                foreach (var record in batch)
                {
                    values.Add(record.Value);
                    batch.Acknowledge(record);
                }
                await consumer.CommitAsync(cancellationToken);
                if (values.Count == 2) break;
            }
        }
        else
        {
            await foreach (var record in consumer.PollAsync(cancellationToken))
            {
                values.Add(record.Value);
                consumer.Acknowledge(record);
                if (values.Count == 2) break;
            }
            await consumer.CommitAsync(cancellationToken);
        }
        string?[] expected = ["value-0", "value-1"];
        await Assert.That(values).IsEquivalentTo(expected);
        var identity = ((IKafkaClientInstanceIdentity)consumer).ClientInstanceId;
        foreach (var name in new[] { recordsName, acknowledgementsName })
        {
            var received = await kafka.WaitForPayloadAsync(clientId, payload =>
                !payload.IsTerminating && Decode(payload).Any(metric => metric.Name == name
                    && metric.Sum.DataPoints.Single().AsDouble > 0), cancellationToken);
            await Assert.That(received.ClientInstanceId).IsEqualTo(identity!.Value);
            var decoded = Decode(received);
            await Assert.That(decoded.Single(metric => metric.Name == applicationName)
                .Gauge.DataPoints.Single().AsDouble).IsEqualTo(42d);
            await Assert.That(decoded.Single(metric => metric.Name == name).Sum.IsMonotonic).IsTrue();
        }
        // Built-ins are collected before application gauges. The first marker may race with
        // an earlier collection; observing the second proves a complete subsequent export.
        // Polling has stopped, so all pre-close fetch deltas must now be at the receiver.
        foreach (var marker in new[] { 43, 44 })
        {
            Volatile.Write(ref applicationValue, marker);
            await kafka.WaitForPayloadAsync(clientId, payload => !payload.IsTerminating
                && Decode(payload).Any(metric => metric.Name == applicationName
                    && metric.Gauge.DataPoints.Single().AsDouble == marker), cancellationToken);
        }
        var beforeClose = await kafka.ReadPayloadsAsync(clientId, cancellationToken);
        var fetchesBeforeClose = ExportedTotal(beforeClose, fetchesName);
        await Assert.That(fetchesBeforeClose).IsGreaterThan(0d);
        await consumer.DisposeAsync();
        var terminating = await kafka.WaitForPayloadAsync(clientId,
            payload => payload.IsTerminating && Decode(payload).Any(metric => metric.Name == fetchesName), cancellationToken);
        await Assert.That(terminating.ClientInstanceId).IsEqualTo(identity!.Value);
        var finalPayloads = await kafka.ReadPayloadsAsync(clientId, cancellationToken);
        // Sum every delta: a periodic push may export the close fetch before termination.
        // This single-broker consumer sends exactly one session-close fetch.
        await Assert.That(ExportedTotal(finalPayloads, fetchesName))
            .IsEqualTo(fetchesBeforeClose + 1d);
        await Assert.That(ExportedTotal(finalPayloads, acknowledgementsName))
            .IsEqualTo(2d);
        await Assert.That(ExportedTotal(finalPayloads, acknowledgementErrorsName))
            .IsEqualTo(0d);
    }

    private static double ExportedTotal(IReadOnlyList<ReceivedTelemetry> payloads, string name) => payloads
        .SelectMany(Decode).Where(metric => metric.Name == name)
        .Sum(metric => metric.Sum.DataPoints.Single().AsDouble);

    private static Metric[] Decode(ReceivedTelemetry payload) => MetricsData.Parser.ParseFrom(payload.Data)
        .ResourceMetrics.SelectMany(resource => resource.ScopeMetrics)
        .SelectMany(scope => scope.Metrics).ToArray();
}
