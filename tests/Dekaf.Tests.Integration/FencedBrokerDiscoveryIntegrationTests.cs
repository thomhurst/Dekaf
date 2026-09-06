using Dekaf.Admin;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
public sealed class FencedBrokerDiscoveryIntegrationTests
{
    [Test]
    [Timeout(240_000)]
    public async Task DescribeClusterAsync_ObservesRegisteredBrokerBeforeAndAfterFencing(CancellationToken cancellationToken)
    {
        await using var kafka = new RackAwareKafkaContainer();
        await kafka.InitializeAsync();
        // Keep discovery on a surviving broker when broker 3 stops.
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(kafka.BootstrapServers.Split(',')[0])
            .WithMetadataMaxAge(TimeSpan.FromMilliseconds(100))
            .Build();
        var options = new DescribeClusterOptions { IncludeFencedBrokers = true };
        var initial = await admin.DescribeClusterAsync(options, cancellationToken);
        await Assert.That(initial.EndpointType).IsEqualTo(DescribeClusterEndpointType.Broker);
        await Assert.That(initial.Nodes.Select(node => node.NodeId)).IsEquivalentTo([1, 2, 3]);
        await Assert.That(initial.Nodes.All(node => node.IsFenced == false)).IsTrue();

        await kafka.StopBrokerAsync(3, cancellationToken);
        using var observation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        observation.CancelAfter(TimeSpan.FromSeconds(60));
        while (true)
        {
            var snapshot = await admin.DescribeClusterAsync(options, observation.Token);
            if (snapshot.Nodes.Any(node => node.NodeId == 3 && node.IsFenced == true))
                break;
            await Task.Delay(TimeSpan.FromMilliseconds(100), observation.Token);
        }

        var ordinary = await admin.DescribeClusterAsync(new DescribeClusterOptions(), cancellationToken);
        await Assert.That(ordinary.Nodes.Select(node => node.NodeId)).IsEquivalentTo([1, 2]);
        var included = await admin.DescribeClusterAsync(options, cancellationToken);
        await Assert.That(included.Nodes.Single(node => node.NodeId == 3).IsFenced).IsTrue();

        // Discovery must not reintroduce fenced registrations into ordinary metadata routing.
        while (true)
        {
            var cached = await admin.DescribeClusterAsync(observation.Token);
            if (!cached.Nodes.Any(node => node.NodeId == 3))
                break;
            await Task.Delay(TimeSpan.FromMilliseconds(100), observation.Token);
        }
    }
}
