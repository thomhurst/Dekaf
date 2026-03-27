using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for multi-connection consumer (ConnectionsPerBroker).
/// Verifies that fetch traffic and coordination traffic work correctly against
/// a real Kafka broker, including the single-connection fallback.
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

        var messages = await ConsumeMessagesAsync(consumer, messageCount);
        await Assert.That(messages).Count().IsEqualTo(messageCount);
        await CommitAndVerifyOffsetsAsync(consumer, messages);
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

        var messages = await ConsumeMessagesAsync(consumer, messageCount);
        await Assert.That(messages).Count().IsEqualTo(messageCount);
        await CommitAndVerifyOffsetsAsync(consumer, messages);
    }
}
