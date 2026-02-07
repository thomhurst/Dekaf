using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for consumer error handling and edge cases.
/// Verifies behavior with empty topics, double subscribe, consume without subscribe,
/// and disposal during active consumption.
/// </summary>
public sealed class ConsumerErrorHandlingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Consumer_EmptyTopic_ConsumeOneReturnsNull()
    {
        // Arrange - create topic but produce nothing
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-empty-topic")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 0));

        // Act - try to consume from empty topic with short timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(3), cts.Token).ConfigureAwait(false);

        // Assert - should return null since no messages exist
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Consumer_DoubleSubscribe_ReplacesSubscription()
    {
        // Arrange
        var topic1 = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var topic2 = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce a message to topic2
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-double-sub")
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic2,
            Key = "key",
            Value = "from-topic2"
        }).ConfigureAwait(false);

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-double-sub")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        // Act - subscribe to topic1, then replace with topic2
        consumer.Subscribe(topic1);
        await Assert.That(consumer.Subscription).Contains(topic1);

        consumer.Subscribe(topic2);

        // Assert - subscription should now be topic2 only
        await Assert.That(consumer.Subscription).Contains(topic2);
        await Assert.That(consumer.Subscription).DoesNotContain(topic1);

        // Verify we can consume from the replaced subscription
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Topic).IsEqualTo(topic2);
        await Assert.That(result.Value.Value).IsEqualTo("from-topic2");
    }

    [Test]
    public async Task Consumer_ConsumeWithoutSubscribe_ReturnsNoMessages()
    {
        // Arrange - create consumer without subscribing or assigning
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-no-sub")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .Build();

        // Act - try to consume with no subscription
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(3), cts.Token).ConfigureAwait(false);

        // Assert - should return null (no subscription means no partitions assigned)
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Consumer_DisposeWhileConsuming_CompletesGracefully()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-dispose-while-consuming")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Act - start consuming in background, then dispose
        var consumeTask = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await foreach (var _ in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
                {
                    // Should not receive messages since topic is empty
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when consumer is disposed
            }
            catch (ObjectDisposedException)
            {
                // Expected when consumer is disposed during consumption
            }
        });

        // Give consumer time to start polling
        await Task.Delay(1000).ConfigureAwait(false);

        // Dispose the consumer while it's consuming
        await consumer.DisposeAsync().ConfigureAwait(false);

        // Wait for consume task to complete (should not hang)
        var completedInTime = await Task.WhenAny(consumeTask, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false) == consumeTask;

        // Assert - should complete gracefully without hanging
        await Assert.That(completedInTime).IsTrue();
    }

    [Test]
    public async Task Consumer_AutoOffsetResetNone_NoCommittedOffset_ThrowsException()
    {
        // Arrange - fresh group with no committed offsets
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce a message so the topic isn't empty
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-offset-none")
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        }).ConfigureAwait(false);

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-offset-none")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.None)
            .Build();

        consumer.Subscribe(topic);

        // Act & Assert - with AutoOffsetReset.None and no committed offsets, should throw
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await Assert.That(async () =>
        {
            await foreach (var _ in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
            {
                // Should throw before yielding any messages
            }
        }).Throws<KafkaException>();
    }
}
