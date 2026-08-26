using System.Net.Sockets;
using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

[Category("Diagnostics")]
public sealed class ClientStatusIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [SupportsKafka(420)]
    [Timeout(90_000)]
    public async Task SharedClients_ReportSameClusterIdentityAndUnavailableTelemetry(
        CancellationToken cancellationToken)
    {
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers);
        await using var producer = await client.CreateProducer<string, string>()
            .BuildAsync(cancellationToken);
        await using var consumer = await client.CreateConsumer<string, string>($"status-{Guid.NewGuid():N}")
            .BuildAsync(cancellationToken);
        await using var shareConsumer = await client.CreateShareConsumer<string, string>($"share-status-{Guid.NewGuid():N}")
            .BuildAsync(cancellationToken);
        await using var admin = client.CreateAdminClient().Build();
        _ = await admin.ListTopicsAsync(cancellationToken: cancellationToken);

        var identities = new (string Role, IKafkaClientIdentity Identity)[]
        {
            ("producer", (IKafkaClientIdentity)producer),
            ("consumer", (IKafkaClientIdentity)consumer),
            ("share consumer", (IKafkaClientIdentity)shareConsumer),
            ("admin", (IKafkaClientIdentity)admin)
        };
        var clusterId = identities[0].Identity.ClusterId;

        await Assert.That(clusterId).IsNotNull();
        foreach (var (role, identity) in identities)
        {
            await Assert.That(identity.ClusterId).IsEqualTo(clusterId).Because($"{role} cluster ID");
            await Assert.That(identity.ClientInstanceId).IsNull()
                .Because($"{role} client instance ID without a broker telemetry receiver");
        }
    }

    [Test]
    [Timeout(90_000)]
    public async Task ProducerBacklogSnapshot_DrainsAfterFlush(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLinger(TimeSpan.FromSeconds(10))
            .WithBatchSize(1024 * 1024)
            .BuildAsync(cancellationToken);
        var statusProvider = (IKafkaClientStatusProvider)producer;

        for (var i = 0; i < 100; i++)
        {
            await producer.FireAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = i.ToString(),
                Value = new string('x', 128)
            });
        }

        var buffered = statusProvider.GetStatus().Producer!.Value;
        await Assert.That(buffered.BufferedBytes).IsGreaterThan(0);
        await Assert.That(buffered.UnsealedBatchCount + buffered.QueuedBatchCount + buffered.InFlightBatchCount)
            .IsGreaterThan(0);

        await producer.FlushAsync(cancellationToken);
        await WaitForConditionAsync(
            () => statusProvider.GetStatus().Producer?.BufferedBytes == 0,
            TimeSpan.FromSeconds(10),
            description: "producer backlog to drain after FlushAsync");

        var drained = statusProvider.GetStatus().Producer!.Value;
        await Assert.That(drained.UnsealedBatchCount).IsEqualTo(0);
        await Assert.That(drained.QueuedBatchCount).IsEqualTo(0);
        await Assert.That(drained.InFlightBatchCount).IsEqualTo(0);
    }

    [Test]
    [Timeout(90_000)]
    public async Task ConsumerGroupSnapshot_TracksStableAssignmentAndHeartbeat(
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using (var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync(cancellationToken))
        {
            await producer.ProduceAsync(topic, "key", "value", cancellationToken);
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"status-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithHeartbeatInterval(TimeSpan.FromSeconds(1))
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);
        var statusProvider = (IKafkaClientStatusProvider)consumer;
        using var pollCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var polling = PollUntilCancelledAsync(consumer, pollCancellation.Token);

        try
        {
            await WaitForConditionAsync(
                () => statusProvider.GetStatus().ConsumerGroup is
                {
                    State: CoordinatorState.Stable,
                    Assignment.Count: > 0,
                    TimeSinceLastHeartbeat: not null
                },
                TimeSpan.FromSeconds(30),
                description: "consumer group status to become stable with a heartbeat");

            var group = statusProvider.GetStatus().ConsumerGroup!;
            await Assert.That(group.HasConsumerGroup).IsTrue();
            await Assert.That(group.CoordinatorId).IsGreaterThanOrEqualTo(0);
            await Assert.That(group.MemberId).IsNotNull();
            await Assert.That(group.GenerationOrMemberEpoch).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            pollCancellation.Cancel();
            await polling;
        }
    }

    private static async Task PollUntilCancelledAsync(
        IKafkaConsumer<string, string> consumer,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in consumer.ConsumeAsync(cancellationToken))
            {
                // Polling drives group coordination; records are irrelevant to this status test.
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }
}

[Category("Diagnostics")]
[Category("Resilience")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class BrokerConnectionStatusIntegrationTests(RackAwareKafkaContainer kafka)
{
    [Test]
    [Timeout(120_000)]
    public async Task BrokerStatus_TracksDisconnectAndReconnect(CancellationToken cancellationToken)
    {
        var firstBootstrap = kafka.BootstrapServers.Split(',')[0];
        var endpoint = BootstrapServerList.Parse(firstBootstrap);
        await using var pool = new ConnectionPool(
            "connection-status-integration",
            new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(5) });
        pool.RegisterBroker(1, endpoint.Host, endpoint.Port);
        var statusSource = (IConnectionPoolStatusSource)pool;
        var brokerStopped = false;

        try
        {
            _ = await pool.GetConnectionAsync(1, cancellationToken);
            var connected = statusSource.GetBrokerConnectionStatus().Single();
            await Assert.That(connected.State).IsEqualTo(BrokerConnectionState.Connected);
            await Assert.That(connected.LastSuccessfulRequestAtUtc).IsNotNull();
            var connectedAt = connected.LastConnectionStateChangeAtUtc
                ?? throw new InvalidOperationException("Connect timestamp was not captured.");

            await kafka.StopBrokerAsync(1, cancellationToken);
            brokerStopped = true;
            await TestWait.WaitForConditionAsync(
                () => statusSource.GetBrokerConnectionStatus().Single().State == BrokerConnectionState.Disconnected,
                TimeSpan.FromSeconds(15),
                description: "client to observe broker disconnect");
            var disconnected = statusSource.GetBrokerConnectionStatus().Single();
            await Assert.That(disconnected.LastConnectionStateChangeAtUtc).IsNotNull();
            await Assert.That(disconnected.LastConnectionStateChangeAtUtc!.Value).IsGreaterThan(connectedAt);

            await kafka.StartBrokerAsync(1, cancellationToken);
            brokerStopped = false;
            await pool.RemoveConnectionAsync(1);
            await TestWait.WaitForConditionAsync(
                async () =>
                {
                    try
                    {
                        _ = await pool.GetConnectionAsync(1, cancellationToken);
                        return statusSource.GetBrokerConnectionStatus().Single().State
                            == BrokerConnectionState.Connected;
                    }
                    catch (Exception ex) when (ex is KafkaException or SocketException)
                    {
                        return false;
                    }
                },
                static connected => connected,
                maxRetries: 10,
                initialDelayMs: 250,
                description: "client to reconnect after broker restart");
            var reconnected = statusSource.GetBrokerConnectionStatus().Single();
            await Assert.That(reconnected.State).IsEqualTo(BrokerConnectionState.Connected);
            var disconnectedAt = disconnected.LastConnectionStateChangeAtUtc
                ?? throw new InvalidOperationException("Disconnect timestamp was not captured.");
            var reconnectedAt = reconnected.LastConnectionStateChangeAtUtc
                ?? throw new InvalidOperationException("Reconnect timestamp was not captured.");
            await Assert.That(reconnectedAt).IsGreaterThanOrEqualTo(disconnectedAt);
        }
        finally
        {
            if (brokerStopped)
                await kafka.StartBrokerAsync(1, CancellationToken.None);
        }
    }
}
