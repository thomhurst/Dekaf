using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
public class AdminDetailedConsumerGroupMutationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingTopic_DoesNotPreventValidOffsetMutation(bool missingFirst)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var missingTopic = $"missing-{Guid.NewGuid():N}";
        var group = $"detailed-partial-{Guid.NewGuid():N}";
        var good = new TopicPartitionOffset(topic, 0, 42);
        var missing = new TopicPartitionOffset(missingTopic, 0, 24);
        await using var admin = new AdminClientBuilder().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        try
        {
            var results = await admin.AlterConsumerGroupOffsetsDetailedAsync(group,
                missingFirst ? [missing, good] : [good, missing]);
            await Assert.That(results[new(topic, 0)].IsSuccess).IsTrue();
            var missingResult = results[new(missingTopic, 0)];
            // Name-based versions report the broker error; topic-ID versions fail
            // local mapping before send. Both must preserve the valid sibling.
            if (missingResult.Outcome == AdminMutationOutcome.NotAttempted)
                await Assert.That(missingResult.Exception).IsTypeOf<Dekaf.Errors.KafkaException>();
            else
                await Assert.That(missingResult.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
            var offsets = await admin.ListConsumerGroupOffsetsAsync(group);
            await Assert.That(offsets[new(topic, 0)]).IsEqualTo(42);
        }
        finally
        {
            await admin.DeleteConsumerGroupsDetailedAsync([group]);
        }
    }

    [Test]
    public async Task OffsetAndGroupMutations_RetainMixedBrokerResults()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var group = $"detailed-{Guid.NewGuid():N}";
        var missingGroup = $"missing-{Guid.NewGuid():N}";
        var good = new TopicPartition(topic, 0);
        var bad = new TopicPartition(topic, 99);
        await using var admin = new AdminClientBuilder().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        try
        {
            var altered = await admin.AlterConsumerGroupOffsetsDetailedAsync(group, [new(topic, 0, 12) { Metadata = "retained" }, new(topic, 99, 24)]);
            await Assert.That(altered[good].IsSuccess).IsTrue();
            await Assert.That(altered[bad].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
            var offsets = await admin.ListConsumerGroupOffsetsAsync(group);
            await Assert.That(offsets[good]).IsEqualTo(12);
            var deleted = await admin.DeleteConsumerGroupOffsetsDetailedAsync(group, [good, bad]);
            await Assert.That(deleted[good].IsSuccess).IsTrue();
            await Assert.That(deleted[bad].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
            var groups = await admin.DeleteConsumerGroupsDetailedAsync([group, missingGroup]);
            await Assert.That(groups[group].IsSuccess).IsTrue();
            await Assert.That(groups[missingGroup].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
            var missing = await admin.DeleteConsumerGroupOffsetsDetailedAsync(missingGroup, [good, bad]);
            await Assert.That(missing.Values.All(static result => result.ErrorCode == ErrorCode.GroupIdNotFound)).IsTrue();
        }
        finally
        {
            await admin.DeleteConsumerGroupsDetailedAsync([group]);
        }
    }
}
