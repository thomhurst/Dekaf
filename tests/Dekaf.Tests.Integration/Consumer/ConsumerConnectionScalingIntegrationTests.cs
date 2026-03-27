using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for consumer connection scaling with ConnectionsPerBroker = 3.
/// Verifies that fetch traffic is distributed across multiple connections while
/// coordination traffic uses a dedicated connection.
/// </summary>
[Category("Consumer")]
public sealed class ConsumerConnectionScalingIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Consumer_WithConnectionsPerBroker3_ConsumesSuccessfully()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        const int messageCount = 50;

        // Produce messages
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-conn-scaling-producer")
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

        // Consume with 3 connections (2 fetch + 1 coordination)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-conn-scaling-consumer")
            .WithGroupId(groupId)
            .WithConnectionsPerBroker(3)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithoutAdaptiveConnections()
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = await ConsumeMessagesAsync(consumer, messageCount);
        await Assert.That(messages).Count().IsEqualTo(messageCount);
        await CommitAndVerifyOffsetsAsync(consumer, messages);
    }
}
