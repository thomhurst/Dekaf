using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("Messaging")]
public sealed class KafkaClientIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ClientMaxInFlight_NonIdempotentProducer_ProducesOrderedBatches()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder => builder
            .WithMaxInFlightRequestsPerConnection(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var producer = await client.CreateProducer<string, string>()
            .WithIdempotence(false)
            .WithBatchSize(1024)
            .BuildAsync();
        var options = (ProducerOptions)producer.GetType()
            .GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(producer)!;
        await Assert.That(options.MaxInFlightRequestsPerConnection).IsEqualTo(1);

        await using var consumer = await client.CreateConsumer<string, string>()
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();
        consumer.Assign(new TopicPartition(topic, 0));

        // Resolve topic metadata before appending concurrent batches so append order is deterministic.
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Partition = 0,
            Value = "warmup"
        }, CancellationToken.None);

        var produces = new Task<RecordMetadata>[32];
        var payload = new string('x', 1024);
        for (var i = 0; i < produces.Length; i++)
        {
            produces[i] = producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = 0,
                Key = i.ToString(),
                Value = payload
            }, CancellationToken.None).AsTask();
        }

        await Task.WhenAll(produces).WaitAsync(TimeSpan.FromSeconds(30));
        var messages = await ConsumeMessagesAsync(consumer, produces.Length + 1);
        await Assert.That(messages[0].Value).IsEqualTo("warmup");
        for (var i = 0; i < produces.Length; i++)
        {
            await Assert.That(messages[i + 1].Key).IsEqualTo(i.ToString());
            await Assert.That(messages[i + 1].Value).IsEqualTo(payload);
        }
    }

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
