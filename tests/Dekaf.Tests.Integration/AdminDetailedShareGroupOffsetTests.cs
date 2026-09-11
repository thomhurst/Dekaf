using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

[Category("Admin")]
[SupportsKafka(420)]
public sealed class AdminDetailedShareGroupOffsetTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ShareOffsets_RetainMixedOutcomesAndDeleteWholeTopics()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var missing = $"missing-{Guid.NewGuid():N}";
        var group = $"detailed-share-{Guid.NewGuid():N}";
        await using IAdminClient admin = new AdminClientBuilder().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var first = new TopicPartition(topic, 0);
        var second = new TopicPartition(topic, 1);
        var absent = new TopicPartition(missing, 0);
        var results = await admin.AlterShareGroupOffsetsDetailedAsync(group,
            [new() { TopicPartition = first, StartOffset = 0 },
             new() { TopicPartition = second, StartOffset = 0 },
             new() { TopicPartition = absent, StartOffset = 0 }]);
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[first].IsSuccess).IsTrue();
        await Assert.That(results[second].IsSuccess).IsTrue();
        await Assert.That(results[absent].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        var stored = await admin.DescribeShareGroupOffsetsAsync(group, [first, second]);
        await Assert.That(stored.Single(offset => offset.TopicPartition == first).StartOffset).IsEqualTo(0);
        await Assert.That(stored.Single(offset => offset.TopicPartition == second).StartOffset).IsEqualTo(0);
        var deleted = await admin.DeleteShareGroupOffsetsDetailedAsync(group, [topic, missing]);
        await Assert.That(deleted.Count).IsEqualTo(2);
        await Assert.That(deleted[topic].IsSuccess).IsTrue();
        await Assert.That(deleted[missing].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
    }

    [Test]
    public async Task DeleteMissingGroup_PreservesGroupErrorForEveryTopic()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var admin = new AdminClientBuilder().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var results = await admin.DeleteShareGroupOffsetsDetailedAsync($"missing-{Guid.NewGuid():N}", [topic, "other"]);
        await Assert.That(results.Count).IsEqualTo(2);
        foreach (var result in results.Values)
        {
            await Assert.That(result.Outcome).IsEqualTo(AdminMutationOutcome.Failed);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        }
    }
}
