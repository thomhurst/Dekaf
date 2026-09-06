using Dekaf.Admin;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class NodeFeatureIntegrationTests(RackAwareKafkaContainer kafka)
{
    [Test]
    [Timeout(120_000)]
    public async Task DescribeFeaturesAsync_QueriesAllThreeBrokerDestinations(CancellationToken cancellationToken)
    {
        await using var admin = new AdminClientBuilder().WithBootstrapServers(kafka.BootstrapServers).Build();
        var cluster = await admin.DescribeClusterAsync(cancellationToken);
        await Assert.That(cluster.Nodes.Count).IsEqualTo(3);
        foreach (var node in cluster.Nodes)
        {
            var features = await admin.DescribeFeaturesAsync(
                new DescribeFeaturesOptions { NodeId = node.NodeId, TimeoutMs = 10_000 }, cancellationToken);
            await Assert.That(features.SupportedFeatures).ContainsKey("metadata.version");
            await Assert.That(features.FinalizedFeaturesEpoch).IsGreaterThanOrEqualTo(0);
        }
    }
}
