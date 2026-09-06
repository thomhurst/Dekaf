using Dekaf.Admin;
using Dekaf.Protocol.Messages;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryClusterDiscoveryTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DescribeCluster_ReportsUnfencedInMemoryBroker(bool includeFenced)
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { ClusterId = "discovery-test" });
        await using var admin = new InMemoryAdminClient(cluster);
        IAdminClient client = admin;
        var snapshot = await client.DescribeClusterAsync(new DescribeClusterOptions { IncludeFencedBrokers = includeFenced });
        var cached = await admin.DescribeClusterAsync(default);

        await Assert.That(snapshot.ClusterId).IsEqualTo("discovery-test");
        await Assert.That(snapshot.ControllerId).IsEqualTo(cached.ControllerId);
        await Assert.That(snapshot.EndpointType).IsEqualTo(DescribeClusterEndpointType.Broker);
        await Assert.That(snapshot.Nodes.Count).IsEqualTo(1);
        await Assert.That(snapshot.Nodes[0].NodeId).IsEqualTo(cached.Nodes[0].NodeId);
        await Assert.That(snapshot.Nodes[0].Host).IsEqualTo("in-memory");
        await Assert.That(snapshot.Nodes[0].Port).IsEqualTo(0);
        await Assert.That(snapshot.Nodes[0].Rack).IsNull();
        await Assert.That(snapshot.Nodes[0].IsFenced).IsFalse();
        await Assert.That(admin.Metadata.GetBrokers()).IsEmpty();
    }

    [Test]
    public async Task DescribeCluster_ObservesAdminFaultsAndCancellation()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        IAdminClient client = admin;
        var failure = new InvalidOperationException("discovery failed");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => client.DescribeClusterAsync(new DescribeClusterOptions()).AsTask());
        await Assert.That(actual).IsSameReferenceAs(failure);

        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.Admin));
        using var cancellation = new CancellationTokenSource();
        var pending = client.DescribeClusterAsync(new DescribeClusterOptions(), cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        cancellation.Cancel();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        barrier.Release();
        var recovered = await client.DescribeClusterAsync(new DescribeClusterOptions());
        await Assert.That(recovered.Nodes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DescribeCluster_DisposedClientThrows()
    {
        var admin = new InMemoryAdminClient(new InMemoryKafkaCluster());
        await admin.DisposeAsync();
        IAdminClient client = admin;
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => client.DescribeClusterAsync(new DescribeClusterOptions()).AsTask());
    }
}
