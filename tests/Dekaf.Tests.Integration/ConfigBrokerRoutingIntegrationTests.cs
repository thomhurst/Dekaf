using Dekaf.Admin;

namespace Dekaf.Tests.Integration;

/// <summary>
/// A broker answers <c>Broker(id)</c> and <c>BrokerLogger(id)</c> config resources only for its own node ID,
/// so the plain config APIs must route each one to node <c>id</c> rather than to the first broker.
/// </summary>
[Category("Admin")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ConfigBrokerRoutingIntegrationTests(RackAwareKafkaContainer kafka)
{
    [Test]
    [Timeout(120_000)]
    public async Task DescribeConfigsAsync_ReturnsBrokerAndLoggerConfigsForEveryNode(CancellationToken cancellationToken)
    {
        await using var admin = kafka.CreateAdminClient();
        var nodeIds = await GetNodeIdsAsync(admin, cancellationToken);
        var resources = nodeIds.Select(ConfigResource.Broker)
            .Concat(nodeIds.Select(ConfigResource.BrokerLogger))
            .Append(ConfigResource.ClusterBroker())
            .ToArray();

        var result = await admin.DescribeConfigsAsync(
            resources, new DescribeConfigsOptions { TimeoutMs = 30_000 }, cancellationToken);

        await Assert.That(result.Count).IsEqualTo(resources.Length);
        foreach (var nodeId in nodeIds)
        {
            var nodeIdEntry = result[ConfigResource.Broker(nodeId)].Single(static entry => entry.Name == "node.id");
            await Assert.That(nodeIdEntry.Value).IsEqualTo(nodeId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await Assert.That(result[ConfigResource.BrokerLogger(nodeId)]).IsNotEmpty();
        }
    }

    [Test]
    [Timeout(120_000)]
    public async Task AlterConfigsAsync_ValidatesBrokerAndLoggerResourcesOnEveryNode(CancellationToken cancellationToken)
    {
        await using var admin = kafka.CreateAdminClient();
        var nodeIds = await GetNodeIdsAsync(admin, cancellationToken);

        // ValidateOnly changes nothing, but a broker still rejects a resource for another node ID with INVALID_REQUEST.
        await admin.AlterConfigsAsync(
            nodeIds.ToDictionary(
                ConfigResource.Broker,
                static _ => (IReadOnlyList<ConfigEntry>)[new ConfigEntry { Name = "log.cleaner.threads", Value = "1" }]),
            new AlterConfigsOptions { ValidateOnly = true },
            cancellationToken: cancellationToken);

        await admin.IncrementalAlterConfigsAsync(
            nodeIds.Select(ConfigResource.Broker)
                .Concat(nodeIds.Select(ConfigResource.BrokerLogger))
                .ToDictionary(
                    static resource => resource,
                    static resource => (IReadOnlyList<ConfigAlter>)[resource.Type == ConfigResourceType.BrokerLogger
                        ? ConfigAlter.Set("root", "INFO")
                        : ConfigAlter.Set("log.cleaner.threads", "1")]),
            new IncrementalAlterConfigsOptions { ValidateOnly = true },
            cancellationToken: cancellationToken);
    }

    private static async Task<int[]> GetNodeIdsAsync(IAdminClient admin, CancellationToken cancellationToken)
    {
        var cluster = await admin.DescribeClusterAsync(cancellationToken);
        await Assert.That(cluster.Nodes.Count).IsEqualTo(3);
        return cluster.Nodes.Select(static node => node.NodeId).Order().ToArray();
    }
}
