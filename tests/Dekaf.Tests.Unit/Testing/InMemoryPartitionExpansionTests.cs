using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryPartitionExpansionTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TypedExpansion_ValidatesThenExpands(bool explicitAssignments)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        IAdminClient client = admin;
        var expansion = new Dictionary<string, NewPartitions>
        {
            ["orders"] = new() { TotalCount = 3, ReplicaAssignments = explicitAssignments ? [[0], [0]] : null }
        };
        await client.CreatePartitionsAsync(expansion, new CreatePartitionsOptions { ValidateOnly = true });
        await Assert.That(cluster.DescribeTopics(["orders"])["orders"].Partitions.Count).IsEqualTo(1);
        await client.CreatePartitionsAsync(expansion);
        var partitions = cluster.DescribeTopics(["orders"])["orders"].Partitions;
        await Assert.That(partitions.Count).IsEqualTo(3);
        await Assert.That(partitions[2].ReplicaNodes).Count().IsEqualTo(1);
        await Assert.That(partitions[2].ReplicaNodes[0]).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InvalidClusterAssignment_DoesNotMutate(bool validateOnly)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        IAdminClient client = admin;
        foreach (var assignments in new IReadOnlyList<IReadOnlyList<int>>[] { [[0]], [[0], [1]], [[0, 1], [0, 1]] })
        {
            var error = await Assert.ThrowsAsync<KafkaException>(() => client.CreatePartitionsAsync(
                new Dictionary<string, NewPartitions> { ["orders"] = new() { TotalCount = 3, ReplicaAssignments = assignments } },
                new CreatePartitionsOptions { ValidateOnly = validateOnly }).AsTask());
            await Assert.That(error!.ErrorCode).IsEqualTo(ErrorCode.InvalidReplicaAssignment);
            await Assert.That(cluster.DescribeTopics(["orders"])["orders"].Partitions.Count).IsEqualTo(1);
        }
    }

    [Test]
    public async Task FaultPause_SnapshotsAssignmentsAndHonorsCancellation()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        IAdminClient client = admin;
        var replicas = new[] { 0 };
        var expansion = new Dictionary<string, NewPartitions> { ["orders"] = new() { TotalCount = 2, ReplicaAssignments = [replicas] } };
        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.Admin, topic: "orders"));
        var pending = client.CreatePartitionsAsync(expansion).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        replicas[0] = 99;
        barrier.Release();
        await pending;
        await Assert.That(cluster.DescribeTopics(["orders"])["orders"].Partitions.Count).IsEqualTo(2);

        expansion["orders"] = new() { TotalCount = 3 };
        barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.Admin, topic: "orders"));
        using var cancellation = new CancellationTokenSource();
        pending = client.CreatePartitionsAsync(expansion, cancellationToken: cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        barrier.Release();
        await Assert.That(cluster.DescribeTopics(["orders"])["orders"].Partitions.Count).IsEqualTo(2);
    }

    [Test]
    public async Task InvalidLaterShape_PreservesFaultAndEarlierTopic()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        IAdminClient client = admin;
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);
        await Assert.ThrowsAsync<ArgumentException>(() => client.CreatePartitionsAsync(new Dictionary<string, NewPartitions>
        {
            ["orders"] = new() { TotalCount = 2 },
            ["later"] = new() { TotalCount = 3, ReplicaAssignments = [[0, 0]] }
        }).AsTask());
        await Assert.That(cluster.DescribeTopics(["orders"])["orders"].Partitions.Count).IsEqualTo(1);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => client.CreatePartitionsAsync(
            new Dictionary<string, NewPartitions> { ["orders"] = new() { TotalCount = 2 } }).AsTask());
        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task MissingTopic_ValidationDoesNotAutoCreate()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        IAdminClient client = admin;
        var error = await Assert.ThrowsAsync<KafkaException>(() => client.CreatePartitionsAsync(
            new Dictionary<string, NewPartitions> { ["missing"] = new() { TotalCount = 2 } },
            new CreatePartitionsOptions { ValidateOnly = true }).AsTask());
        await Assert.That(error!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(await admin.ListTopicsAsync()).IsEmpty();
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task NonIncreasingTotal_IsRejected(int total)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", partitionCount: 2);
        await using var admin = new InMemoryAdminClient(cluster);
        IAdminClient client = admin;
        var error = await Assert.ThrowsAsync<KafkaException>(() => client.CreatePartitionsAsync(
            new Dictionary<string, NewPartitions> { ["orders"] = new() { TotalCount = total } }).AsTask());
        await Assert.That(error!.ErrorCode).IsEqualTo(ErrorCode.InvalidPartitions);
        await Assert.That(cluster.DescribeTopics(["orders"])["orders"].Partitions.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments(0, 30000)]
    [Arguments(2, -1)]
    public async Task InvalidCountOrTimeout_PreservesFault(int total, int timeoutMs)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        IAdminClient client = admin;
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), new InvalidOperationException("blocked"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.CreatePartitionsAsync(
            new Dictionary<string, NewPartitions> { ["orders"] = new() { TotalCount = total } },
            new CreatePartitionsOptions { TimeoutMs = timeoutMs }).AsTask());
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DisposedAndPreCancelledCalls_AreRejected()
    {
        var admin = new InMemoryAdminClient(new InMemoryKafkaCluster());
        IAdminClient client = admin;
        var expansion = new Dictionary<string, NewPartitions> { ["orders"] = new() { TotalCount = 2 } };
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.CreatePartitionsAsync(expansion,
            cancellationToken: new CancellationToken(true)).AsTask());
        await admin.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.CreatePartitionsAsync(expansion).AsTask());
    }
}
