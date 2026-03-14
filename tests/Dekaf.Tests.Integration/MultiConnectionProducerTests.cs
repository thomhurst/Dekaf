using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for the multi-connection (ConnectionsPerBroker > 1) producer path.
/// Verifies that round-robin connection distribution works end-to-end without data loss.
/// </summary>
[Category("Producer")]
public class MultiConnectionProducerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Producer_WithMultipleConnectionsPerBroker_DeliversAllMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        const int messageCount = 100;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-conn-producer")
            .WithConnectionsPerBroker(4)
            .WithIdempotence(false)
            .WithAcks(Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .BuildAsync();

        // Act - produce all messages
        var pendingTasks = new List<ValueTask<RecordMetadata>>(messageCount);
        for (var i = 0; i < messageCount; i++)
        {
            pendingTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }));
        }

        var produceResults = new List<RecordMetadata>(messageCount);
        foreach (var task in pendingTasks)
        {
            produceResults.Add(await task);
        }

        // Assert - all produce calls succeeded with valid offsets
        await Assert.That(produceResults).Count().IsEqualTo(messageCount);
        foreach (var result in produceResults)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }

        // Verify by consuming all messages back
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-conn-consumer")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= messageCount)
                break;
        }

        // Assert - all messages arrived
        await Assert.That(consumed).Count().IsEqualTo(messageCount);

        // Verify all expected values are present (order may differ across partitions)
        var consumedValues = consumed.Select(c => c.Value).OrderBy(v => v).ToList();
        var expectedValues = Enumerable.Range(0, messageCount).Select(i => $"value-{i}").OrderBy(v => v).ToList();
        await Assert.That(consumedValues).IsEquivalentTo(expectedValues);
    }
}
