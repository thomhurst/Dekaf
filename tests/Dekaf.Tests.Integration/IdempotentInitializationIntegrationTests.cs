using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

[Category("Producer")]
[Category("Resilience")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class IdempotentInitializationIntegrationTests(RackAwareKafkaContainer kafka)
{
    [Test]
    [Timeout(180_000)]
    public async Task InitializeAsync_FirstKnownBrokerOffline_ObtainsIdAndProduces(CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateReplicatedTopicAsync();
        var bootstrapServers = kafka.BootstrapServers.Split(',');
        await using var pool = new ConnectionPool(
            "idempotent-init-failover",
            new ConnectionOptions { ConnectionTimeout = TimeSpan.FromSeconds(2), RequestTimeout = TimeSpan.FromSeconds(5) },
            GlobalTestSetup.GetLoggerFactory());
        await using var metadata = new MetadataManager(pool, bootstrapServers,
            options: new MetadataOptions { EnableBackgroundRefresh = false });
        await metadata.InitializeAsync(cancellationToken);

        // Cache the real cluster view, then stop exactly the broker initialization will
        // select first. Disabling background refresh keeps the stale view deterministic.
        var firstBroker = metadata.Metadata.GetBrokers()[0];
        var previousLeader = await kafka.GetPartitionLeaderIdAsync(topic, cancellationToken);
        try
        {
            await kafka.StopBrokerAsync(firstBroker.NodeId, cancellationToken);
            await pool.CloseAllAsync();
            if (previousLeader == firstBroker.NodeId)
                await kafka.WaitForPartitionLeaderChangeAsync(topic, previousLeader, cancellationToken);

            await using (var producer = new KafkaProducer<string, string>(
                new ProducerOptions
                {
                    BootstrapServers = bootstrapServers,
                    EnableIdempotence = true,
                    Acks = Acks.All,
                    MaxBlockMs = 30_000,
                    RequestTimeoutMs = 5000,
                    DeliveryTimeoutMs = 45_000
                }, Serializers.String, Serializers.String, pool, metadata, DekafMemoryBudget.Global,
                GlobalTestSetup.GetLoggerFactory()))
            {
                await Assert.That(metadata.Metadata.GetBrokers()[0].NodeId).IsEqualTo(firstBroker.NodeId);
                await producer.InitializeAsync(cancellationToken);
                await Assert.That(producer.RecordAccumulator.ProducerId).IsGreaterThanOrEqualTo(0L);
                var result = await producer.ProduceAsync(topic, "cached-metadata", "first", cancellationToken);
                await Assert.That(result.Offset).IsEqualTo(0L);
            }

            // Also exercise the public builder's complete bootstrap + initialization path
            // against all original addresses while that broker is still offline.
            await using var builtProducer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(kafka.BootstrapServers)
                .WithIdempotence(true)
                .WithAcks(Acks.All)
                .WithRequestTimeout(TimeSpan.FromSeconds(5))
                .WithDeliveryTimeout(TimeSpan.FromSeconds(45))
                .BuildAsync(cancellationToken);
            var builtResult = await builtProducer.ProduceAsync(topic, "build-async", "second", cancellationToken);
            await Assert.That(builtResult.Offset).IsEqualTo(1L);
        }
        finally
        {
            await kafka.StartBrokerAsync(firstBroker.NodeId, CancellationToken.None);
            await kafka.WaitForInSyncReplicasAsync(topic, 3, CancellationToken.None);
        }
    }
}
