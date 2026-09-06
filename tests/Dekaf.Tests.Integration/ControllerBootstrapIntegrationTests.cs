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
        var live = await admin.DescribeClusterAsync(new DescribeClusterOptions(), timeout.Token).ConfigureAwait(false);
        await Assert.That(live.EndpointType).IsEqualTo(Dekaf.Protocol.Messages.DescribeClusterEndpointType.Controller);
        await Assert.That(live.Nodes.Single().IsFenced).IsNull();
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await admin.DescribeClusterAsync(new DescribeClusterOptions { IncludeFencedBrokers = true }, timeout.Token));
        var quorum = await admin.DescribeMetadataQuorumAsync(timeout.Token).ConfigureAwait(false);
        var features = await admin.DescribeFeaturesAsync(timeout.Token).ConfigureAwait(false);
        var selectedFeatures = await admin.DescribeFeaturesAsync(
            new DescribeFeaturesOptions { NodeId = cluster.ControllerId }, timeout.Token);
        await Assert.That(selectedFeatures.SupportedFeatures).IsEquivalentTo(features.SupportedFeatures);
        await Assert.That(selectedFeatures.FinalizedFeaturesEpoch).IsGreaterThanOrEqualTo(0);

        await Assert.That(cluster.ControllerId).IsEqualTo(1);
        await Assert.That(cluster.Nodes.Select(static node => node.NodeId)).IsEquivalentTo([1]);
        await Assert.That(quorum.LeaderId).IsEqualTo(1);
        await Assert.That(features.SupportedFeatures).ContainsKey("metadata.version");
    }
}
