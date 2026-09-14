using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.ShareConsumer;
using Dekaf.Telemetry;
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
            .WithClientRack("producer-rack")
            .WithTransactionalId(clientId + "-transaction")
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
            var resource = ResourceAttributes(received);
            await Assert.That(resource).IsEquivalentTo(new Dictionary<string, string>
            {
                ["client_rack"] = "producer-rack",
                ["transactional_id"] = clientId + "-transaction"
            });
            var decoded = Decode(received);
            var application = decoded.Single(metric => metric.Name == applicationName);
            var builtin = decoded.Single(metric => metric.Name == builtinName);
            await Assert.That(builtin.Unit).IsEqualTo("1");
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
            .WithGroupId(clientId + "-group")
            .WithRackId("share-rack")
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
            await Assert.That(ResourceAttributes(received)).IsEquivalentTo(new Dictionary<string, string>
            {
                ["client_rack"] = "share-rack",
                ["group_id"] = clientId + "-group"
            });
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
        var shareMemberId = consumer.MemberId;
        await Assert.That(shareMemberId).IsNotNull();
        var memberPayload = await kafka.WaitForPayloadAsync(clientId,
            payload => !payload.IsTerminating && ResourceAttributes(payload).GetValueOrDefault("group_member_id") == shareMemberId,
            cancellationToken);
        await Assert.That(ResourceAttributes(memberPayload)["group_id"]).IsEqualTo(group);
        await Assert.That(ResourceAttributes(memberPayload).ContainsKey("group_instance_id")).IsFalse();
        await Assert.That(ResourceAttributes(memberPayload).ContainsKey("transactional_id")).IsFalse();
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

    [Test]
    [Timeout(90_000)]
    public async Task Consumer_BrokerReceivesConfiguredAndCurrentMembershipResources(CancellationToken cancellationToken)
    {
        const string metricName = "com.example.telemetry.membership";
        var clientId = $"resource-consumer-{Guid.NewGuid():N}";
        var groupId = clientId + "-group";
        var topic = await kafka.CreateTestTopicAsync(partitions: 1);
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
        await kafka.WaitForSubscriptionAsync(clientId, [metricName], 500, cancellationToken);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers).BuildAsync(cancellationToken);
        await producer.ProduceAsync(topic, "key", "value", cancellationToken);
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers).WithClientId(clientId)
            .WithGroupId(groupId).WithGroupInstanceId(clientId + "-instance").WithClientRack("consumer-rack")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .RegisterMetricForSubscription(new(metricName, ApplicationTelemetryMetricKind.Gauge, static () => 1))
            .BuildAsync(cancellationToken);
        var beforeJoin = await kafka.WaitForPayloadAsync(clientId,
            payload => !payload.IsTerminating && Decode(payload).Any(metric => metric.Name == metricName), cancellationToken);
        await Assert.That(ResourceAttributes(beforeJoin)).IsEquivalentTo(new Dictionary<string, string>
        {
            ["client_rack"] = "consumer-rack",
            ["group_id"] = groupId,
            ["group_instance_id"] = clientId + "-instance"
        });
        consumer.Subscribe(topic);
        var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cancellationToken);
        await Assert.That(record).IsNotNull();
        var memberId = consumer.MemberId;
        await Assert.That(memberId).IsNotNull();
        var joined = await kafka.WaitForPayloadAsync(clientId,
            payload => ResourceAttributes(payload).GetValueOrDefault("group_member_id") == memberId, cancellationToken);
        await Assert.That(ResourceAttributes(joined)).IsEquivalentTo(new Dictionary<string, string>
        {
            ["client_rack"] = "consumer-rack",
            ["group_id"] = groupId,
            ["group_instance_id"] = clientId + "-instance",
            ["group_member_id"] = memberId!
        });
        await consumer.CloseAsync(new ConsumerCloseOptions
        {
            GroupMembershipOperation = ConsumerGroupMembershipOperation.LeaveGroup
        }, cancellationToken);
        var terminated = await kafka.WaitForPayloadAsync(clientId, payload => payload.IsTerminating, cancellationToken);
        await Assert.That(ResourceAttributes(terminated).ContainsKey("group_member_id")).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(90_000)]
    public async Task Producer_BrokerReceivesQueueTimeAndConnectionRate(bool sharedClient, CancellationToken cancellationToken)
    {
        var clientId = $"standard-producer-{Guid.NewGuid():N}";
        var topic = await kafka.CreateTestTopicAsync(partitions: 1);
        string[] names =
        [
            StandardClientTelemetryMetrics.QueuePrefix + "avg",
            StandardClientTelemetryMetrics.QueuePrefix + "max",
            StandardClientTelemetryMetrics.ProducerPrefix + "connection.creation.rate",
            ClientTelemetryMetricNames.ProducerConnectionCreationTotal,
            ClientTelemetryMetricNames.ProducerProduceThrottleTimeMax,
            ClientTelemetryMetricNames.ProducerProduceThrottleTimeAvg
        ];
        var standardNames = names[..^3];
        await ConfigureStandardMetricsAsync(clientId, names, cancellationToken);
        await using var rootClient = sharedClient
            ? Kafka.Connect(kafka.BootstrapServers, builder => builder.WithClientId(clientId)) : null;
        var producerBuilder = rootClient?.CreateProducer<string, string>()
            ?? Kafka.CreateProducer<string, string>().WithBootstrapServers(kafka.BootstrapServers);
        await using var producer = await producerBuilder
            .WithClientId(clientId)
            .BuildAsync(cancellationToken);
        await producer.ProduceAsync(topic, "key", "value", cancellationToken);
        var received = await kafka.WaitForPayloadAsync(clientId,
            payload => standardNames.All(name => Decode(payload).Any(metric => metric.Name == name)), cancellationToken);
        var metrics = Decode(received);
        await Assert.That(metrics.Length).IsLessThanOrEqualTo(names.Length);
        var average = metrics.Single(metric => metric.Name == names[0]);
        var maximum = metrics.Single(metric => metric.Name == names[1]);
        await Assert.That(average.Unit).IsEqualTo("ms");
        await Assert.That(average.Gauge.DataPoints.Single().AsDouble).IsGreaterThan(0d);
        await Assert.That(maximum.Gauge.DataPoints.Single().AsDouble)
            .IsGreaterThanOrEqualTo(average.Gauge.DataPoints.Single().AsDouble);
        await Assert.That(metrics.Single(metric => metric.Name == names[2]).Unit).IsEqualTo("1/s");
        var payloads = await kafka.ReadPayloadsAsync(clientId, cancellationToken);
        var history = payloads.SelectMany(Decode).ToArray();
        await Assert.That(ExportedTotal(payloads, names[3])).IsGreaterThan(0d);
        await Assert.That(history.First(metric => metric.Name == names[4]).Unit).IsEqualTo("ms");
        await Assert.That(history.First(metric => metric.Name == names[5]).Unit).IsEqualTo("ms");
        await producer.DisposeAsync();
        var terminated = await kafka.WaitForPayloadAsync(clientId, payload => payload.IsTerminating, cancellationToken);
        await Assert.That(Decode(terminated).Any(metric => metric.Name == names[0])).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(90_000)]
    public async Task Consumer_BrokerReceivesStandardMetricsAndClearedAssignment(bool sharedClient, CancellationToken cancellationToken)
    {
        var clientId = $"standard-consumer-{Guid.NewGuid():N}";
        var topic = await kafka.CreateTestTopicAsync(partitions: 1);
        string[] names =
        [
            StandardClientTelemetryMetrics.ConsumerPrefix + "connection.creation.rate",
            StandardClientTelemetryMetrics.PollIdleRatio,
            StandardClientTelemetryMetrics.CommitPrefix + "avg",
            StandardClientTelemetryMetrics.CommitPrefix + "max",
            StandardClientTelemetryMetrics.AssignedPartitions,
            StandardClientTelemetryMetrics.RebalancePrefix + "avg",
            StandardClientTelemetryMetrics.RebalancePrefix + "max",
            StandardClientTelemetryMetrics.RebalancePrefix + "total",
            StandardClientTelemetryMetrics.FetchPrefix + "avg",
            StandardClientTelemetryMetrics.FetchPrefix + "max",
            ClientTelemetryMetricNames.ConsumerConnectionCreationTotal,
            ClientTelemetryMetricNames.ConsumerFetchThrottleTimeMax,
            ClientTelemetryMetricNames.ConsumerFetchThrottleTimeAvg
        ];
        var standardNames = names[..^3];
        await ConfigureStandardMetricsAsync(clientId, names, cancellationToken);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers).BuildAsync(cancellationToken);
        await producer.ProduceAsync(topic, "key", "value", cancellationToken);
        await using var rootClient = sharedClient
            ? Kafka.Connect(kafka.BootstrapServers, builder => builder.WithClientId(clientId)) : null;
        var consumerBuilder = rootClient?.CreateConsumer<string, string>()
            ?? Kafka.CreateConsumer<string, string>().WithBootstrapServers(kafka.BootstrapServers);
        await using var consumer = await consumerBuilder
            .WithClientId(clientId).WithGroupId(clientId + "-group")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest).BuildAsync(cancellationToken);
        consumer.Subscribe(topic);
        var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cancellationToken);
        await Assert.That(record).IsNotNull();
        await consumer.CommitAsync(cancellationToken);
        var received = await kafka.WaitForPayloadAsync(clientId,
            payload => standardNames.All(name => Decode(payload).Any(metric => metric.Name == name)), cancellationToken);
        var metrics = Decode(received);
        await Assert.That(metrics.Length).IsLessThanOrEqualTo(names.Length);
        await Assert.That(metrics.Single(metric => metric.Name == StandardClientTelemetryMetrics.AssignedPartitions)
            .Gauge.DataPoints.Single().AsDouble).IsEqualTo(1d);
        var ratio = metrics.Single(metric => metric.Name == StandardClientTelemetryMetrics.PollIdleRatio);
        await Assert.That(ratio.Unit).IsEqualTo("1");
        await Assert.That(ratio.Gauge.DataPoints.Single().AsDouble).IsGreaterThanOrEqualTo(0d);
        await Assert.That(ratio.Gauge.DataPoints.Single().AsDouble).IsLessThanOrEqualTo(1d);
        foreach (var prefix in new[] { StandardClientTelemetryMetrics.CommitPrefix,
                     StandardClientTelemetryMetrics.FetchPrefix, StandardClientTelemetryMetrics.RebalancePrefix })
        {
            var average = metrics.Single(metric => metric.Name == prefix + "avg");
            var maximum = metrics.Single(metric => metric.Name == prefix + "max");
            await Assert.That(average.Unit).IsEqualTo("ms");
            await Assert.That(average.Gauge.DataPoints.Single().AsDouble).IsGreaterThan(0d);
            await Assert.That(maximum.Gauge.DataPoints.Single().AsDouble)
                .IsGreaterThanOrEqualTo(average.Gauge.DataPoints.Single().AsDouble);
        }
        var total = metrics.Single(metric => metric.Name == StandardClientTelemetryMetrics.RebalancePrefix + "total");
        await Assert.That(total.Sum.IsMonotonic).IsTrue();
        await Assert.That(total.Unit).IsEqualTo("ms");
        var payloads = await kafka.ReadPayloadsAsync(clientId, cancellationToken);
        var history = payloads.SelectMany(Decode).ToArray();
        await Assert.That(ExportedTotal(payloads, ClientTelemetryMetricNames.ConsumerConnectionCreationTotal)).IsGreaterThan(0d);
        await Assert.That(history.First(metric => metric.Name == ClientTelemetryMetricNames.ConsumerFetchThrottleTimeMax)
            .Unit).IsEqualTo("ms");
        await Assert.That(history.First(metric => metric.Name == ClientTelemetryMetricNames.ConsumerFetchThrottleTimeAvg)
            .Unit).IsEqualTo("ms");
        await Assert.That(ExportedTotal(payloads, total.Name)).IsGreaterThan(0d);
        await consumer.CloseAsync(new ConsumerCloseOptions
        {
            GroupMembershipOperation = ConsumerGroupMembershipOperation.LeaveGroup
        }, cancellationToken);
        var terminated = await kafka.WaitForPayloadAsync(clientId, payload => payload.IsTerminating, cancellationToken);
        await Assert.That(Decode(terminated).Single(metric => metric.Name == StandardClientTelemetryMetrics.AssignedPartitions)
            .Gauge.DataPoints.Single().AsDouble).IsEqualTo(0d);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(90_000)]
    public async Task TransactionInitialization_ExportsNodeLatencyWithoutProducing(bool sharedClient, CancellationToken cancellationToken)
    {
        var clientId = $"transaction-telemetry-{Guid.NewGuid():N}";
        var name = ClientTelemetryMetricNames.ProducerNodeRequestLatencyAvg;
        await ConfigureStandardMetricsAsync(clientId, [name], cancellationToken);
        await using var rootClient = sharedClient
            ? Kafka.Connect(kafka.BootstrapServers, builder => builder.WithClientId(clientId)) : null;
        var builder = rootClient?.CreateProducer<string, string>()
            ?? Kafka.CreateProducer<string, string>().WithBootstrapServers(kafka.BootstrapServers);
        await using var producer = await builder.WithClientId(clientId)
            .WithTransactionalId(clientId + "-transaction").BuildAsync(cancellationToken);
        await ReconnectKnownBrokerAsync(producer, typeof(Producer.KafkaProducer<string, string>), cancellationToken);
        await producer.InitTransactionsAsync(cancellationToken);
        var received = await kafka.WaitForPayloadAsync(clientId,
            payload => Decode(payload).Any(metric => metric.Name == name), cancellationToken);
        var metric = Decode(received).First(metric => metric.Name == name);
        await Assert.That(metric.Unit).IsEqualTo("ms");
        await Assert.That(metric.Gauge.DataPoints.Single().AsDouble).IsGreaterThan(0d);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(90_000)]
    public async Task GroupJoin_ExportsNodeLatencyBeforePolling(bool sharedClient, CancellationToken cancellationToken)
    {
        var clientId = $"group-telemetry-{Guid.NewGuid():N}";
        var topic = await kafka.CreateTestTopicAsync(partitions: 1);
        var name = ClientTelemetryMetricNames.ConsumerNodeRequestLatencyAvg;
        await ConfigureStandardMetricsAsync(clientId, [name], cancellationToken);
        await using var rootClient = sharedClient
            ? Kafka.Connect(kafka.BootstrapServers, builder => builder.WithClientId(clientId)) : null;
        var builder = rootClient?.CreateConsumer<string, string>()
            ?? Kafka.CreateConsumer<string, string>().WithBootstrapServers(kafka.BootstrapServers);
        await using var consumer = await builder.WithClientId(clientId).WithGroupId(clientId + "-group")
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);
        var coordinator = (ConsumerCoordinator)typeof(KafkaConsumer<string, string>).GetField("_coordinator",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(consumer)!;
        await ReconnectKnownBrokerAsync(coordinator, typeof(ConsumerCoordinator), cancellationToken);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { topic }, cancellationToken);
        var received = await kafka.WaitForPayloadAsync(clientId,
            payload => Decode(payload).Any(metric => metric.Name == name), cancellationToken);
        var metric = Decode(received).First(metric => metric.Name == name);
        await Assert.That(metric.Unit).IsEqualTo("ms");
        await Assert.That(metric.Gauge.DataPoints.Single().AsDouble).IsGreaterThan(0d);
    }

    private static async Task ReconnectKnownBrokerAsync(object owner,
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields)] Type ownerType,
        CancellationToken cancellationToken)
    {
        // Replace the ID-less bootstrap connection so node-tagged metrics have a known broker ID.
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var pool = (Networking.ConnectionPool)ownerType.GetField("_connectionPool", flags)!.GetValue(owner)!;
        var metadata = (Metadata.MetadataManager)ownerType.GetField("_metadataManager", flags)!.GetValue(owner)!;
        var brokerId = metadata.GetEndpointsToTry().First(endpoint => endpoint.BrokerId >= 0).BrokerId;
        await pool.CloseAllAsync();
        var connection = await pool.GetConnectionAsync(brokerId, cancellationToken);
        await Assert.That(connection.BrokerId).IsEqualTo(brokerId);
    }

    private async Task ConfigureStandardMetricsAsync(string clientId, string[] names, CancellationToken cancellationToken)
    {
        await using var admin = kafka.CreateAdminClient();
        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.ClientMetrics, Name = clientId }] =
            [
                ConfigAlter.Set("metrics", string.Join(',', names)),
                ConfigAlter.Set("interval.ms", "500"),
                ConfigAlter.Set("match", $"client_id={clientId}")
            ]
        }, cancellationToken: cancellationToken);
        await kafka.WaitForSubscriptionAsync(clientId, names, 500, cancellationToken);
    }

    private static Dictionary<string, string> ResourceAttributes(ReceivedTelemetry payload) =>
        MetricsData.Parser.ParseFrom(payload.Data).ResourceMetrics.Single().Resource?.Attributes
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value.StringValue) ?? [];

    private static double ExportedTotal(IReadOnlyList<ReceivedTelemetry> payloads, string name) => payloads
        .SelectMany(Decode).Where(metric => metric.Name == name)
        .Sum(metric => metric.Sum.DataPoints.Single().AsDouble);

    private static Metric[] Decode(ReceivedTelemetry payload) => MetricsData.Parser.ParseFrom(payload.Data)
        .ResourceMetrics.SelectMany(resource => resource.ScopeMetrics)
        .SelectMany(scope => scope.Metrics).ToArray();
}
