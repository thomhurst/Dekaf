using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
public sealed class AdminControllerRecoveryTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task CreateTopicsAsync_MissingCachedController_RefreshesAndCreatesTopic()
    {
        var bootstrapServers = KafkaContainer.BootstrapServers.Split(',');
        await using var pool = new ConnectionPool("admin-controller-recovery", new ConnectionOptions(),
            GlobalTestSetup.GetLoggerFactory());
        await using var metadata = new MetadataManager(pool, bootstrapServers,
            options: new MetadataOptions { EnableBackgroundRefresh = false });
        await metadata.InitializeAsync();

        // Preserve real broker endpoints but simulate the transient metadata view during
        // controller election. Only the admin retry's metadata refresh can restore the route.
        metadata.Metadata.Update(new MetadataResponse
        {
            ClusterId = metadata.Metadata.ClusterId,
            ControllerId = -1,
            Brokers = metadata.Metadata.GetBrokers().Select(broker => new BrokerMetadata
            {
                NodeId = broker.NodeId, Host = broker.Host, Port = broker.Port
            }).ToList(),
            Topics = []
        });
        await using var admin = new AdminClient(new AdminClientOptions
        {
            BootstrapServers = bootstrapServers, RetryBackoffMs = 1, RetryBackoffMaxMs = 5
        }, pool, metadata);
        var topic = $"controller-recovery-{Guid.NewGuid():N}";

        await admin.CreateTopicsAsync([new NewTopic { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }]);
        try
        {
            await Assert.That(metadata.Metadata.ControllerId).IsGreaterThanOrEqualTo(0);
            var descriptions = await admin.DescribeTopicsAsync([topic]);
            await Assert.That(descriptions[topic].Partitions.Count).IsEqualTo(1);
        }
        finally
        {
            await admin.DeleteTopicsAsync([topic]);
        }
    }
}
