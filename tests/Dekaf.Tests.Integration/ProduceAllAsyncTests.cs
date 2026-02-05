using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class ProduceAllAsyncTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task ProduceAllAsync_MultipleMessages_AllDelivered()
    {
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        var messages = Enumerable.Range(0, 10).Select(i => new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = $"key-{i}",
            Value = $"value-{i}"
        });

        var results = await producer.ProduceAllAsync(messages);

        await Assert.That(results.Length).IsEqualTo(10);

        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Partition).IsGreaterThanOrEqualTo(0);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task ProduceAllAsync_ToSingleTopic_WithKeyValuePairs_AllDelivered()
    {
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        var messages = Enumerable.Range(0, 5)
            .Select(i => ((string?)$"key-{i}", $"value-{i}"))
            .ToList();

        var results = await producer.ProduceAllAsync(topic, messages);

        await Assert.That(results.Length).IsEqualTo(5);

        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task ProduceAllAsync_EmptyList_ReturnsEmptyResults()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        var results = await producer.ProduceAllAsync(Array.Empty<ProducerMessage<string, string>>());

        await Assert.That(results.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ProduceAllAsync_VerifyAllMessagesConsumed()
    {
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .Build();

        var messages = Enumerable.Range(0, 5).Select(i => new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = $"key-{i}",
            Value = $"value-{i}"
        });

        await producer.ProduceAllAsync(messages);

        // Consume all messages
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var consumed = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(result.Value!);
            if (consumed.Count >= 5)
                break;
        }

        await Assert.That(consumed.Count).IsEqualTo(5);
        for (var i = 0; i < 5; i++)
        {
            await Assert.That(consumed).Contains($"value-{i}");
        }
    }
}
