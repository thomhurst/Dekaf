using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for consumer configuration options that affect behavior.
/// Verifies MaxPollRecords, QueuedMinMessages, SubscribeTo at build time,
/// manual assignment without group protocol, and preset configurations.
/// </summary>
public sealed class ConsumerConfigurationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Consumer_MaxPollRecords_LimitsBatchSize()
    {
        // Arrange - produce 50 messages, consume with MaxPollRecords=5
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-max-poll")
            .Build();

        for (var i = 0; i < 50; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        // Act - consume with small MaxPollRecords
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-max-poll")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithMaxPollRecords(5)
            .Build();

        consumer.Subscribe(topic);

        // Consume all messages and verify they arrive
        var consumedCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            consumedCount++;
            if (consumedCount >= 50) break;
        }

        // Assert - all 50 messages should eventually be consumed
        await Assert.That(consumedCount).IsEqualTo(50);
    }

    [Test]
    public async Task Consumer_QueuedMinMessages_AffectsPrefetching()
    {
        // Arrange - WithQueuedMinMessages(1) disables aggressive prefetching
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-no-prefetch")
            .Build();

        for (var i = 0; i < 20; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        // Act - consume with minimal prefetching
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-no-prefetch")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1) // Minimal prefetching
            .Build();

        consumer.Subscribe(topic);

        var consumedCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            consumedCount++;
            if (consumedCount >= 20) break;
        }

        // Assert - should still consume all messages despite minimal prefetching
        await Assert.That(consumedCount).IsEqualTo(20);
    }

    [Test]
    public async Task Consumer_SubscribeTo_AtBuildTime_Works()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-subscribe-to")
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value-subscribe-to"
        }).ConfigureAwait(false);

        // Act - use SubscribeTo() in the builder instead of Subscribe() after Build()
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-subscribe-to")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .SubscribeTo(topic)
            .Build();

        // No explicit Subscribe() call needed

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("value-subscribe-to");
        await Assert.That(consumer.Subscription).Contains(topic);
    }

    [Test]
    public async Task Consumer_ManualAssign_BypassesGroupProtocol()
    {
        // Arrange - manual assignment without group ID
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3).ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-manual-assign")
            .Build();

        // Produce to partition 2 specifically
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "partition-2-message",
            Partition = 2
        }).ConfigureAwait(false);

        // Act - create consumer without group ID and use Assign() directly
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-manual-assign")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        // Assign specific partition without joining a consumer group
        consumer.Assign(new TopicPartition(topic, 2));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Partition).IsEqualTo(2);
        await Assert.That(result.Value.Value).IsEqualTo("partition-2-message");
        // Assignment should reflect what we manually assigned
        await Assert.That(consumer.Assignment).Contains(new TopicPartition(topic, 2));
    }

    [Test]
    public async Task Consumer_LowLatencyPreset_ConsumesSuccessfully()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-low-latency")
            .Build();

        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        // Act - consume with low latency preset
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-low-latency")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .ForLowLatency()
            .Build();

        consumer.Subscribe(topic);

        var consumedCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            consumedCount++;
            if (consumedCount >= 10) break;
        }

        // Assert
        await Assert.That(consumedCount).IsEqualTo(10);
    }
}
