using Dekaf.Consumer;
using Dekaf.Serialization;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Consumer;

public class UnassignmentTests
{
    [Test]
    [Arguments(false, "topics")]
    [Arguments(false, "filter")]
    [Arguments(false, "pattern")]
    [Arguments(false, "empty-filter")]
    [Arguments(false, "empty-pattern")]
    [Arguments(true, "topics")]
    [Arguments(true, "filter")]
    [Arguments(true, "pattern")]
    [Arguments(true, "empty-filter")]
    [Arguments(true, "empty-pattern")]
    public async Task Unassign_WhileSubscribed_ThrowsAndPreservesState(bool inMemory, string subscription)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic1", partitionCount: 2);
        await using var consumer = CreateConsumer(inMemory, cluster);
        switch (subscription)
        {
            case "topics": consumer.Subscribe("topic1"); break;
            case "filter": consumer.Subscribe(static topic => topic == "topic1"); break;
            case "pattern": consumer.SubscribePattern("^topic1$"); break;
            case "empty-filter": consumer.Subscribe(static _ => false); break;
            case "empty-pattern": consumer.SubscribePattern("^missing$"); break;
        }

        var assignment = consumer.Assignment;
        var topics = consumer.Subscription;
        if (assignment.Count != 0)
        {
            consumer.Partitions.Pause(new TopicPartition("topic1", 0));
            consumer.Positions.Seek(new TopicPartitionOffset("topic1", 0, 7));
        }
        var paused = consumer.Partitions.Paused;

        await Assert.That(() => consumer.Partitions.Unassign()).Throws<InvalidOperationException>();

        await Assert.That(consumer.Assignment).IsEquivalentTo(assignment);
        await Assert.That(consumer.Subscription).IsEquivalentTo(topics);
        await Assert.That(consumer.Partitions.Paused).IsEquivalentTo(paused);
        if (assignment.Count != 0)
            await Assert.That(consumer.Positions.GetPosition(new TopicPartition("topic1", 0))).IsEqualTo(7);

        // A rejected call must preserve the subscription itself, including a filter with no matches.
        cluster.CreateTopic("topic2");
        await Assert.That(() => consumer.Partitions.Unassign()).Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Unassign_ManualAssignment_ClearsState(bool inMemory, bool afterUnsubscribe)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic1", partitionCount: 2);
        await using var consumer = CreateConsumer(inMemory, cluster);
        if (afterUnsubscribe)
        {
            consumer.Subscribe("topic1");
            consumer.Unsubscribe();
            consumer.Partitions.Unassign();
        }

        var partition = new TopicPartition("topic1", 0);
        consumer.Partitions.Assign(partition);
        consumer.Positions.Seek(new TopicPartitionOffset("topic1", 0, 7));
        consumer.Partitions.Pause(partition);

        consumer.Partitions.Unassign();
        consumer.Partitions.Unassign();

        await Assert.That(consumer.Assignment).IsEmpty();
        await Assert.That(consumer.Partitions.Paused).IsEmpty();
        await Assert.That(consumer.Positions.GetPosition(partition)).IsNull();
    }

    private static IKafkaConsumer<string, string> CreateConsumer(bool inMemory, InMemoryKafkaCluster cluster)
        => inMemory
            ? new InMemoryConsumer<string, string>(cluster, new InMemoryConsumerOptions { GroupId = "unassign-test" })
            : new KafkaConsumer<string, string>(new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "unassign-test",
                GroupId = "unassign-test"
            }, Serializers.String, Serializers.String);
}
