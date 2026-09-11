using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
public class AdminDetailedMutationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task TopicMutations_PreserveMixedBrokerOutcomesAndTopicIds()
    {
        var existing = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var createdName = $"detailed-create-{Guid.NewGuid():N}";
        await using var admin = new AdminClientBuilder().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var topicId = (await admin.DescribeTopicsAsync([existing]))[existing].TopicId;
        try
        {
            var created = await admin.CreateTopicsDetailedAsync([new() { Name = createdName }, new() { Name = existing }]);
            await Assert.That(created[createdName].IsSuccess).IsTrue();
            await Assert.That(created[existing].ErrorCode).IsEqualTo(ErrorCode.TopicAlreadyExists);
            var missingName = $"missing-{Guid.NewGuid():N}";
            var deleted = await admin.DeleteTopicsDetailedAsync([createdName, missingName]);
            await Assert.That(deleted[createdName].IsSuccess).IsTrue();
            await Assert.That(deleted[missingName].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
            var missingId = Guid.NewGuid();
            var ids = await admin.DeleteTopicsDetailedAsync([topicId, missingId]);
            await Assert.That(ids[topicId].IsSuccess).IsTrue();
            await Assert.That(ids[missingId].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicId);
        }
        finally
        {
            await admin.DeleteTopicsDetailedAsync([createdName]);
        }
    }

    [Test]
    public async Task PartitionMutations_PreserveSuccessfulPartitionsAndMissingTopicErrors()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var missing = $"missing-{Guid.NewGuid():N}";
        await using var admin = new AdminClientBuilder().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var before = (await admin.DescribeTopicsAsync([topic]))[topic];
        var broker = before.Partitions[0].ReplicaNodes[0];
        var expansion = new Dictionary<string, NewPartitions>
        {
            [topic] = new() { TotalCount = 2, ReplicaAssignments = [[broker]] },
            [missing] = new() { TotalCount = 2 }
        };
        var validated = await admin.CreatePartitionsDetailedAsync(expansion, new() { ValidateOnly = true });
        await Assert.That(validated[topic].IsSuccess).IsTrue();
        await Assert.That(validated[missing].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That((await admin.DescribeTopicsAsync([topic]))[topic].Partitions.Count).IsEqualTo(1);
        var applied = await admin.CreatePartitionsDetailedAsync(expansion);
        await Assert.That(applied[topic].IsSuccess).IsTrue();
        await Assert.That(applied[missing].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await WaitForConditionAsync(async () => (await admin.DescribeTopicsAsync([topic]))[topic],
            description => description.Partitions.Count == 2, description: "detailed partition expansion metadata");

        var good = new TopicPartition(topic, 0);
        var bad = new TopicPartition(missing, 0);
        var reassignments = await admin.AlterPartitionReassignmentsDetailedAsync(new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
        {
            [good] = NewPartitionReassignment.ToReplicas(broker),
            [bad] = NewPartitionReassignment.ToReplicas(broker)
        });
        await Assert.That(reassignments[good].IsSuccess).IsTrue();
        await Assert.That(reassignments[bad].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
    }
}
