using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryTopicMutationTests
{
    [Test]
    [Arguments(1, 1, false)]
    [Arguments(-1, -1, false)]
    [Arguments(8, 3, false)]
    [Arguments(0, 0, false)]
    [Arguments(1, 1, true)]
    [Arguments(-1, -1, true)]
    public async Task CreateTopics_ManualAssignments_OverrideCounts(int partitions, int replicas, bool validateOnly)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var topic = new NewTopic
        {
            Name = "orders",
            NumPartitions = partitions,
            ReplicationFactor = (short)replicas,
            ReplicaAssignments = new Dictionary<int, IReadOnlyList<int>> { [0] = [0], [1] = [0] }
        };

        await admin.CreateTopicsAsync([topic], new() { ValidateOnly = validateOnly });

        if (validateOnly)
        {
            await Assert.That(cluster.ListTopics()).IsEmpty();
        }
        else
        {
            var created = (await admin.DescribeTopicsAsync([topic.Name]))[topic.Name];
            await Assert.That(created.Partitions.Count).IsEqualTo(2);
            foreach (var partition in created.Partitions)
                await Assert.That(partition.ReplicaNodes).IsEquivalentTo([0]);
        }
        await Assert.That(topic.NumPartitions).IsEqualTo(partitions);
        await Assert.That(topic.ReplicationFactor).IsEqualTo((short)replicas);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CreateTopics_EmptyAssignments_UseRequestedPartitionCount(bool empty)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);

        await admin.CreateTopicsAsync([new()
        {
            Name = "orders",
            NumPartitions = 3,
            ReplicaAssignments = empty ? new Dictionary<int, IReadOnlyList<int>>() : null
        }]);

        await Assert.That(cluster.GetTopicPartitions("orders").Count).IsEqualTo(3);
    }

    [Test]
    [Arguments(1, false)]
    [Arguments(2, false)]
    [Arguments(1, true)]
    [Arguments(2, true)]
    public async Task CreateTopics_InvalidAssignedPartition_ThrowsWithoutCreatingTopic(int partition, bool validateOnly)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);

        async Task Create() => await admin.CreateTopicsAsync([new()
        {
            Name = "orders",
            ReplicaAssignments = new Dictionary<int, IReadOnlyList<int>> { [partition] = [0] }
        }], new() { ValidateOnly = validateOnly });

        var exception = await Assert.That(Create).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidReplicaAssignment);
        await Assert.That(exception.Message).Contains("orders");
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task CreateTopics_InvalidAssignedReplicas_ProcessesSiblingsBeforeThrowing(bool multiple, bool validateOnly)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);

        async Task Create() => await admin.CreateTopicsAsync([
            new() { Name = "before" },
            new()
            {
                Name = "invalid",
                ReplicaAssignments = new Dictionary<int, IReadOnlyList<int>> { [0] = multiple ? [0, 1] : [1] }
            },
            new() { Name = "after" }
        ], new() { ValidateOnly = validateOnly });

        var exception = await Assert.That(Create).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidReplicaAssignment);
        await Assert.That(exception.Message).Contains("invalid");
        string[] expected = validateOnly ? [] : ["before", "after"];
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task CreateTopics_AssignmentsAreSnapshottedBeforeFaultPause()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var replicas = new[] { 0 };
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin, topic: "orders"));
        var pending = admin.CreateTopicsAsync([new()
        {
            Name = "orders",
            ReplicaAssignments = new Dictionary<int, IReadOnlyList<int>> { [0] = replicas, [1] = [0] }
        }]).AsTask();
        try
        {
            await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            replicas[0] = 99;
        }
        finally
        {
            barrier.Release();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(cluster.GetTopicPartitions("orders").Count).IsEqualTo(2);
    }

    [Test]
    [Arguments(1, false)]
    [Arguments(12, false)]
    [Arguments(1, true)]
    [Arguments(12, true)]
    public async Task CreateTopics_ExistingTopic_ThrowsWithoutChangingTopic(int partitions, bool validateOnly)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        var originalId = (await admin.DescribeTopicsAsync(["orders"]))["orders"].TopicId;

        async Task Create() => await admin.CreateTopicsAsync(
            [new() { Name = "orders", NumPartitions = partitions }],
            new() { ValidateOnly = validateOnly });

        var exception = await Assert.That(Create).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAlreadyExists);
        var topic = (await admin.DescribeTopicsAsync(["orders"]))["orders"];
        await Assert.That(topic.TopicId).IsEqualTo(originalId);
        await Assert.That(topic.Partitions.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CreateTopics_AutoCreatedTopic_Throws()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "value");
        await using var admin = new InMemoryAdminClient(cluster);

        async Task Create() => await admin.CreateTopicsAsync([new() { Name = "orders", NumPartitions = 12 }]);

        var exception = await Assert.That(Create).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAlreadyExists);
        await Assert.That((await admin.DescribeTopicsAsync(["orders"]))["orders"].Partitions.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CreateTopics_MixedBatch_ProcessesSiblingsBeforeThrowingFirstError(bool validateOnly)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("existing-first");
        cluster.CreateTopic("existing-last");
        await using var admin = new InMemoryAdminClient(cluster);

        async Task Create() => await admin.CreateTopicsAsync(
            [new() { Name = "before" }, new() { Name = "existing-first" }, new() { Name = "after" }, new() { Name = "existing-last" }],
            new() { ValidateOnly = validateOnly });

        var exception = await Assert.That(Create).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAlreadyExists);
        await Assert.That(exception.Message).Contains("existing-first");
        string[] expected = validateOnly ? ["existing-first", "existing-last"] : ["before", "existing-first", "after", "existing-last"];
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(expected);
    }

    [Test]
    public async Task CreateTopics_ValidateOnlyNewTopic_DoesNotCreateTopic()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);

        await admin.CreateTopicsAsync([new() { Name = "orders", NumPartitions = 12 }], new() { ValidateOnly = true });

        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    public async Task DeleteTopics_MissingTopic_Throws()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);

        async Task Delete() => await admin.DeleteTopicsAsync(["missing"]);

        var exception = await Assert.That(Delete).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(exception.Message).Contains("missing");
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    public async Task DeleteTopics_RepeatedNames_DeletesEachDistinctTopicOnce()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        cluster.CreateTopic("Orders");
        await using var admin = new InMemoryAdminClient(cluster);

        await admin.DeleteTopicsAsync(["orders", "Orders", "orders", "Orders"]);

        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    public async Task DeleteTopics_RepeatedNamesWithMissingTopic_DeletesSiblingsBeforeThrowingMissingError()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("before");
        cluster.CreateTopic("after");
        await using var admin = new InMemoryAdminClient(cluster);

        async Task Delete() => await admin.DeleteTopicsAsync(
            ["before", "before", "missing", "after", "missing", "after"]);

        var exception = await Assert.That(Delete).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(exception.Message).Contains("missing");
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    public async Task DeleteTopics_MixedBatch_DeletesSiblingsBeforeThrowingFirstError()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("before");
        cluster.CreateTopic("after");
        await using var admin = new InMemoryAdminClient(cluster);

        async Task Delete() => await admin.DeleteTopicsAsync(["before", "missing-first", "after", "missing-last"]);

        var exception = await Assert.That(Delete).Throws<KafkaException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(exception.Message).Contains("missing-first");
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }
}
