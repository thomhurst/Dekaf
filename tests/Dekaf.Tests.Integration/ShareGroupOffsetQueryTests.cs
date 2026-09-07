using Dekaf.Admin;
using Dekaf.Protocol;
using Dekaf.ShareConsumer;

namespace Dekaf.Tests.Integration;

[Category("ShareConsumer")]
[SupportsKafka(420)]
[NotInParallel("ShareConsumerKafka42")]
public class ShareGroupOffsetQueryTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Query_MultipleGroupsMatchesSingleGroupOffsets()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var firstGroup = $"share-query-{Guid.NewGuid():N}";
        var secondGroup = $"share-query-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync();
        await using var first = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(firstGroup).BuildAsync();
        await using var second = await Kafka.CreateShareConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithGroupId(secondGroup).BuildAsync();
        first.Subscribe(topic);
        second.Subscribe(topic);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(first);
        await ShareConsumerTestHelper.PrimeShareConsumerAsync(second);
        await ShareConsumerTestHelper.ProduceAsync(producer, topic, count: 1);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var consumer in new[] { first, second })
        {
            var received = false;
            await foreach (var record in consumer.PollAsync(deadline.Token))
            {
                consumer.Acknowledge(record, AcknowledgeType.Accept);
                received = true;
                break;
            }
            await Assert.That(received).IsTrue();
            await consumer.CommitAsync();
        }
        await using var admin = KafkaContainer.CreateAdminClient();
        var partition = new TopicPartition(topic, 0);
        var result = await admin.ListShareGroupOffsetsAsync(new Dictionary<string, ListShareGroupOffsetsSpec>
        {
            [firstGroup] = new(), [secondGroup] = new() { TopicPartitions = [partition] },
            ["empty-selection"] = new() { TopicPartitions = [] }, [$"missing-{Guid.NewGuid():N}"] = new()
        }, cancellationToken: deadline.Token);
        foreach (var groupId in new[] { firstGroup, secondGroup })
        {
            var single = await admin.DescribeShareGroupOffsetsAsync(groupId, [partition], deadline.Token);
            await Assert.That(result[groupId].ErrorCode).IsEqualTo(ErrorCode.None);
            await Assert.That(result[groupId].Offsets[partition].StartOffset).IsEqualTo(single.Single().StartOffset);
            await Assert.That(result[groupId].Offsets[partition].LeaderEpoch).IsEqualTo(single.Single().LeaderEpoch);
            await Assert.That(result[groupId].Offsets[partition].ErrorCode).IsEqualTo(ErrorCode.None);
        }
        await Assert.That(result["empty-selection"].Offsets).IsEmpty();
        var missing = result.Single(pair => pair.Key.StartsWith("missing-", StringComparison.Ordinal)).Value;
        await Assert.That(missing.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(missing.Offsets).IsEmpty();
    }
}
