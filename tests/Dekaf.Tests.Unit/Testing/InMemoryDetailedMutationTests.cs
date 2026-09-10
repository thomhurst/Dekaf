using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryDetailedMutationTests
{
    [Test]
    public async Task MixedCreateAndDelete_ReflectActualEntityState()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("existing");
        await using var admin = new InMemoryAdminClient(cluster);
        IAdminClient client = admin;
        var created = await client.CreateTopicsDetailedAsync([new() { Name = "new" }, new() { Name = "existing" }]);
        await Assert.That(created["new"].IsSuccess).IsTrue();
        await Assert.That(created["existing"].ErrorCode).IsEqualTo(ErrorCode.TopicAlreadyExists);
        var deleted = await client.DeleteTopicsDetailedAsync(["new", "missing"]);
        await Assert.That(deleted["new"].IsSuccess).IsTrue();
        await Assert.That(deleted["missing"].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(["existing"]);
        var id = (await client.DescribeTopicsAsync(["existing"]))["existing"].TopicId;
        var missing = Guid.NewGuid();
        var byId = await client.DeleteTopicsDetailedAsync([id, missing]);
        await Assert.That(byId[id].IsSuccess).IsTrue();
        await Assert.That(byId[missing].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicId);
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    public async Task ValidateOnlyAndExplicitAssignments_DoNotMutateUntilApplied()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        NewTopic[] topics = [new() { Name = "orders", NumPartitions = -1, ReplicationFactor = -1,
            ReplicaAssignments = new Dictionary<int, IReadOnlyList<int>> { [0] = [0] } }];
        await Assert.That((await admin.CreateTopicsDetailedAsync(topics, new() { ValidateOnly = true }))["orders"].IsSuccess).IsTrue();
        await Assert.That(cluster.ListTopics()).IsEmpty();
        await Assert.That((await admin.CreateTopicsDetailedAsync(topics))["orders"].IsSuccess).IsTrue();
        var expand = new Dictionary<string, NewPartitions>
        {
            ["orders"] = new() { TotalCount = 2, ReplicaAssignments = [[0]] },
            ["missing"] = new() { TotalCount = 2 }
        };
        var validated = await admin.CreatePartitionsDetailedAsync(expand, new() { ValidateOnly = true });
        await Assert.That(validated["orders"].IsSuccess).IsTrue();
        await Assert.That(validated["missing"].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(cluster.GetTopicPartitions("orders").Count).IsEqualTo(1);
        await admin.CreatePartitionsDetailedAsync(expand);
        await Assert.That(cluster.GetTopicPartitions("orders").Count).IsEqualTo(2);
    }

    [Test]
    public async Task Cancellation_PreservesSuccessAndLeavesPausedAndRemainingEntitiesUnchanged()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin, topic: "paused"));
        using var cancellation = new CancellationTokenSource();
        var pending = admin.CreateTopicsDetailedAsync([new() { Name = "done" }, new() { Name = "paused" }, new() { Name = "remaining" }],
            cancellationToken: cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        var results = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        barrier.Release();
        await Assert.That(results["done"].IsSuccess).IsTrue();
        await Assert.That(results["paused"].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(results["remaining"].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(["done"]);
    }

    [Test]
    public async Task CancellationDuringRetry_PreservesConfirmedRejection()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var scope = new KafkaFaultScope(KafkaFaultOperation.Admin, topic: "retry");
        cluster.FaultPlan.Fail(scope, new KafkaException(ErrorCode.NotController, "confirmed rejection"));
        var barrier = cluster.FaultPlan.PauseNext(scope);
        using var cancellation = new CancellationTokenSource();
        var pending = admin.CreateTopicsDetailedAsync([new() { Name = "retry" }, new() { Name = "remaining" }],
            cancellationToken: cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        var results = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        barrier.Release();

        await Assert.That(results["retry"].ErrorCode).IsEqualTo(ErrorCode.NotController);
        await Assert.That(results["retry"].ErrorMessage).IsEqualTo("confirmed rejection");
        await Assert.That(results["retry"].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(results["remaining"].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    [Arguments(ErrorCode.NotController)]
    [Arguments(ErrorCode.TopicAuthorizationFailed)]
    public async Task CancellationWithBrokerRejection_PreservesConfirmedOutcome(ErrorCode errorCode)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        using var cancellation = new CancellationTokenSource();
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, topic: "rejected"),
            new KafkaException(errorCode, "confirmed rejection"));
        cluster.FaultPlan.FaultConsumed += _ => cancellation.Cancel();

        var results = await admin.CreateTopicsDetailedAsync(
            [new() { Name = "done" }, new() { Name = "rejected" }, new() { Name = "remaining" }],
            cancellationToken: cancellation.Token);

        await Assert.That(results["done"].IsSuccess).IsTrue();
        await Assert.That(results["rejected"].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(results["rejected"].ErrorCode).IsEqualTo(errorCode);
        await Assert.That(results["rejected"].ErrorMessage).IsEqualTo("confirmed rejection");
        await Assert.That(results["remaining"].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(["done"]);
    }

    [Test]
    public async Task AuthorizationAndControllerFaults_PreserveSiblingsAndRetryOnlyRejectedEntity()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, topic: "denied"), new KafkaException(ErrorCode.TopicAuthorizationFailed, "original denial"));
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, topic: "retry"), new KafkaException(ErrorCode.NotController, "moved"));
        var results = await admin.CreateTopicsDetailedAsync([new() { Name = "done" }, new() { Name = "denied" }, new() { Name = "retry" }]);
        await Assert.That(results["done"].IsSuccess).IsTrue();
        await Assert.That(results["retry"].IsSuccess).IsTrue();
        await Assert.That(results["denied"].ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(results["denied"].ErrorMessage).IsEqualTo("original denial");
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(["done", "retry"]);
    }

    [Test]
    public async Task Reassignment_ValidatesActualPartitionAndSingleBrokerModel()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", 3);
        await using var admin = new InMemoryAdminClient(cluster);
        var good = new TopicPartition("orders", 0);
        var invalid = new TopicPartition("orders", 1);
        var cancel = new TopicPartition("orders", 2);
        var missing = new TopicPartition("missing", 0);
        var results = await admin.AlterPartitionReassignmentsDetailedAsync(new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
        {
            [good] = NewPartitionReassignment.ToReplicas(0),
            [invalid] = NewPartitionReassignment.ToReplicas(9),
            [cancel] = Optional.None<NewPartitionReassignment>(),
            [missing] = NewPartitionReassignment.ToReplicas(0)
        });
        await Assert.That(results[good].IsSuccess).IsTrue();
        await Assert.That(results[invalid].ErrorCode).IsEqualTo(ErrorCode.InvalidReplicaAssignment);
        await Assert.That(results[cancel].ErrorCode).IsEqualTo(ErrorCode.NoReassignmentInProgress);
        await Assert.That(results[missing].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
    }

    [Test]
    public async Task SnapshotBeforePause_PreservesCallerReplicaArrays()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var brokerIds = new[] { 0 };
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin, topic: "orders"));
        var pending = admin.CreateTopicsDetailedAsync([new() { Name = "orders", NumPartitions = -1, ReplicationFactor = -1,
            ReplicaAssignments = new Dictionary<int, IReadOnlyList<int>> { [0] = brokerIds } }]).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        brokerIds[0] = 99;
        barrier.Release();
        await Assert.That((await pending)["orders"].IsSuccess).IsTrue();
    }
}
