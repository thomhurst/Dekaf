using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

public sealed class KafkaClientIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task SeparateRolePools_ProduceAndConsumeThroughRootClient()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var producer = await client.CreateProducer<string, string>().BuildAsync();
        await using var consumer = await client.CreateConsumer<string, string>()
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();
        consumer.Assign(new TopicPartition(topic, 0));

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "root-client-key",
            Value = "root-client-value"
        }, CancellationToken.None);

        var messages = await ConsumeMessagesAsync(consumer, count: 1);

        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(messages[0].Key).IsEqualTo("root-client-key");
        await Assert.That(messages[0].Value).IsEqualTo("root-client-value");
    }
}
