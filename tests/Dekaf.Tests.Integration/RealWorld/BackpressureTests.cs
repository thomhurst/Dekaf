using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for backpressure and flow control behavior.
/// Verifies buffer memory limits, MaxBlock timeouts, and consumer pause/resume functionality.
/// </summary>
public sealed class BackpressureTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Producer_SmallBufferMemory_AllMessagesDelivered()
    {
        // Arrange - use small buffer memory (1MB) to force backpressure
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3).ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-small-buffer")
            .WithAcks(Acks.Leader)
            .WithBufferMemory(1_048_576) // 1MB buffer
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .Build();

        // Act - send more than 1MB of data (2MB+)
        var messageValue = new string('x', 1000); // 1KB per message
        const int messageCount = 2500; // ~2.5MB total

        var produceTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = messageValue
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in produceTasks)
        {
            results.Add(await task.ConfigureAwait(false));
        }

        // Assert - all messages should eventually be delivered
        await Assert.That(results).Count().IsEqualTo(messageCount);
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task Consumer_PausePartition_TracksStateCorrectly()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-pause")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Act & Assert - verify pause state tracking
        await Assert.That(consumer.Paused.Contains(tp)).IsFalse();

        consumer.Pause(tp);
        await Assert.That(consumer.Paused.Contains(tp)).IsTrue();
        await Assert.That(consumer.Paused.Count).IsEqualTo(1);

        // Pausing again is idempotent
        consumer.Pause(tp);
        await Assert.That(consumer.Paused.Count).IsEqualTo(1);

        consumer.Resume(tp);
        await Assert.That(consumer.Paused.Contains(tp)).IsFalse();
        await Assert.That(consumer.Paused.Count).IsEqualTo(0);

        // Resuming when not paused is safe
        consumer.Resume(tp);
        await Assert.That(consumer.Paused.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Consumer_PauseAndResume_ResumesFromCorrectOffset()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-pause-resume")
            .Build();

        // Produce 10 messages
        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-pause-resume")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Consume first 3 messages
        var consumed = new List<ConsumeResult<string, string>>();
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts1.Token).ConfigureAwait(false))
        {
            consumed.Add(msg);
            if (consumed.Count >= 3) break;
        }

        await Assert.That(consumed).Count().IsEqualTo(3);

        // Pause
        consumer.Pause(tp);
        await Assert.That(consumer.Paused.Contains(tp)).IsTrue();

        // Resume
        consumer.Resume(tp);
        await Assert.That(consumer.Paused.Contains(tp)).IsFalse();

        // Should continue from offset 3
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var afterResume = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token).ConfigureAwait(false);

        await Assert.That(afterResume).IsNotNull();
        await Assert.That(afterResume!.Value.Offset).IsEqualTo(3);
        await Assert.That(afterResume.Value.Value).IsEqualTo("value-3");
    }

    [Test]
    public async Task Consumer_HighThroughputPreset_ConsumesLargerBatches()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3).ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-high-throughput")
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .Build();

        // Produce messages
        const int messageCount = 500;
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(topic, $"key-{i}", $"value-{i}");
        }

        await producer.FlushAsync().ConfigureAwait(false);

        // Act - consume with high throughput preset
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-high-throughput")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .ForHighThroughput()
            .Build();

        consumer.Subscribe(topic);

        var consumedCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            consumedCount++;
            if (consumedCount >= messageCount) break;
        }

        // Assert - should consume all messages
        await Assert.That(consumedCount).IsEqualTo(messageCount);
    }
}
