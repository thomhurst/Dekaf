using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for multi-connection consumer (ConnectionsPerBroker).
/// Verifies that fetch traffic on index 0 and coordination traffic on index 1
/// work correctly against a real Kafka broker, including the single-connection fallback.
/// </summary>
[Category("Consumer")]
public sealed class MultiConnectionConsumerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task MultiConnectionConsumer_ProduceConsumeCommit_Succeeds()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        const int messageCount = 50;

        // Produce messages
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-conn-consumer-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i % 10}",
                Value = $"msg-{i}"
            }, CancellationToken.None);
        }

        // Consume with 2 connections per broker (fetch on index 0, coordination on index 1)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-conn-consumer")
            .WithGroupId(groupId)
            .WithConnectionsPerBroker(2)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        // Commit offsets — this exercises the coordination path (connection index 1)
        var offsets = messages
            .GroupBy(m => new TopicPartition(m.Topic, m.Partition))
            .Select(g => new TopicPartitionOffset(g.Key.Topic, g.Key.Partition, g.Max(m => m.Offset) + 1))
            .ToArray();

        await consumer.CommitAsync(offsets);

        // Verify committed offsets
        foreach (var offset in offsets)
        {
            var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(offset.Topic, offset.Partition));
            await Assert.That(committed).IsEqualTo(offset.Offset);
        }
    }

    [Test]
    public async Task MultiConnectionConsumer_SingleConnection_FallbackWorks()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        const int messageCount = 10;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-single-conn-consumer-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"msg-{i}"
            }, CancellationToken.None);
        }

        // ConnectionsPerBroker=1: both fetch and coordination share the same connection
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-single-conn-consumer")
            .WithGroupId(groupId)
            .WithConnectionsPerBroker(1)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        // Commit on the single connection
        await consumer.CommitAsync([new TopicPartitionOffset(topic, 0, messageCount)]);
        var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0));
        await Assert.That(committed).IsEqualTo(messageCount);
    }
}
