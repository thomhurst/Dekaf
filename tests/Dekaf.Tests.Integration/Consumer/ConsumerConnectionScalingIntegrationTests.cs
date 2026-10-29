using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for consumer connection counts with fixed and adaptive scaling.
/// Verifies that fetch traffic is distributed across multiple connections while
/// coordination traffic uses a dedicated connection.
/// </summary>
[Category("Consumer")]
public sealed class ConsumerConnectionScalingIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(3, false)]
    [Arguments(6, false)]
    [Arguments(6, true)]
    public async Task Consumer_WithConnectionsPerBroker_ConsumesSuccessfully(
        int connectionsPerBroker, bool adaptiveConnections)
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

        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-conn-scaling-consumer")
            .WithGroupId(groupId)
            .WithConnectionsPerBroker(connectionsPerBroker)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());

        if (!adaptiveConnections)
            builder.WithoutAdaptiveConnections();

        await using var consumer = await builder.BuildAsync();

        consumer.Subscribe(topic);

        var messages = await ConsumeMessagesAsync(consumer, messageCount);
        await Assert.That(messages).Count().IsEqualTo(messageCount);
        await CommitAndVerifyOffsetsAsync(consumer, messages);
    }
}
