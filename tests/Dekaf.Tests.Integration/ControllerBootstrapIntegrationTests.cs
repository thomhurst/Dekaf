using Dekaf.Admin;

namespace Dekaf.Tests.Integration;

[NotInParallel("ControllerOnlyKafkaContainer")]
[ClassDataSource<ControllerOnlyKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ControllerBootstrapIntegrationTests(ControllerOnlyKafkaContainer kafka)
{
    [Test]
    public async Task ControllerBootstrap_AdminWorksWithoutBrokerProcess()
    {
        await using var admin = new AdminClientBuilder()
            .WithBootstrapControllers(kafka.BootstrapControllers)
            .WithRetryBackoff(TimeSpan.FromMilliseconds(50))
            .WithRetryBackoffMax(TimeSpan.FromMilliseconds(200))
            .Build();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cluster = await admin.DescribeClusterAsync(timeout.Token).ConfigureAwait(false);
        var quorum = await admin.DescribeMetadataQuorumAsync(timeout.Token).ConfigureAwait(false);
        var features = await admin.DescribeFeaturesAsync(timeout.Token).ConfigureAwait(false);

        await Assert.That(cluster.ControllerId).IsEqualTo(1);
        await Assert.That(cluster.Nodes.Select(static node => node.NodeId)).IsEquivalentTo([1]);
        await Assert.That(quorum.LeaderId).IsEqualTo(1);
        await Assert.That(features.SupportedFeatures).ContainsKey("metadata.version");
    }
}
