using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
public class AdminPartitionExpansionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ValidateThenApply_PreservesExistingPartitions(bool explicitAssignments)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var admin = new AdminClientBuilder().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var before = (await admin.DescribeTopicsAsync([topic]))[topic];
        var broker = before.Partitions[0].ReplicaNodes[0];
        var expansion = new Dictionary<string, NewPartitions>
        {
            [topic] = new() { TotalCount = 3, ReplicaAssignments = explicitAssignments ? [[broker], [broker]] : null }
        };
        await admin.CreatePartitionsAsync(expansion, new CreatePartitionsOptions { ValidateOnly = true });
        var validated = (await admin.DescribeTopicsAsync([topic]))[topic];
        await Assert.That(validated.Partitions.Count).IsEqualTo(1);
        await Assert.That(validated.Partitions[0].ReplicaNodes[0]).IsEqualTo(broker);

        await admin.CreatePartitionsAsync(expansion);
        // Controller acknowledgement can precede propagation to broker metadata.
        var applied = await WaitForConditionAsync(
            async () => (await admin.DescribeTopicsAsync([topic]))[topic],
            description => description.Partitions.Count == 3,
            description: "expanded partition metadata");
        await Assert.That(applied.Partitions.Count).IsEqualTo(3);
        foreach (var partition in applied.Partitions)
        {
            await Assert.That(partition.ReplicaNodes.Count).IsEqualTo(1);
            await Assert.That(partition.ReplicaNodes[0]).IsEqualTo(broker);
        }
    }

    [Test]
    public async Task IncorrectAdditionalAssignmentCount_IsRejectedByController()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        await using var admin = new AdminClientBuilder().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var before = (await admin.DescribeTopicsAsync([topic]))[topic];
        var broker = before.Partitions[0].ReplicaNodes[0];
        // The assignment shape is valid locally; only the controller knows how many partitions already exist.
        var error = await Assert.ThrowsAsync<KafkaException>(async () => await admin.CreatePartitionsAsync(
            new Dictionary<string, NewPartitions> { [topic] = new() { TotalCount = 3, ReplicaAssignments = [[broker]] } },
            new CreatePartitionsOptions { ValidateOnly = true }));
        await Assert.That(error!.ErrorCode).IsEqualTo(ErrorCode.InvalidReplicaAssignment);
        await Assert.That((await admin.DescribeTopicsAsync([topic]))[topic].Partitions.Count).IsEqualTo(1);
    }
}
