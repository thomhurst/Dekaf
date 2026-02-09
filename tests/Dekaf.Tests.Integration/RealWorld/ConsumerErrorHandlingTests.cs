using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for consumer error handling and edge cases.
/// Verifies behavior with empty topics, double subscribe, consume without subscribe,
/// and AutoOffsetReset.None behavior.
/// </summary>
[Category("Consumer")]
public sealed class ConsumerErrorHandlingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Consumer_EmptyTopic_ConsumeOneReturnsNull()
    {
        // Arrange - create topic but produce nothing
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-empty-topic")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

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
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-double-sub")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic2,
            Key = "key",
            Value = "from-topic2"
        }).ConfigureAwait(false);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-double-sub")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

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
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-no-sub")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .BuildAsync();

        // Act - try to consume with no subscription
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(3), cts.Token).ConfigureAwait(false);

        // Assert - should return null (no subscription means no partitions assigned)
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Consumer_AutoOffsetResetNone_NoCommittedOffset_ThrowsException()
    {
        // AutoOffsetReset.None with no committed offsets should throw KafkaException
        // when seeking to an invalid offset on a manually-assigned partition
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        // Produce a message so the topic isn't empty
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-offset-none")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        }).ConfigureAwait(false);

        // Use manual assignment to avoid group coordinator delays
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-offset-none")
            .WithAutoOffsetReset(AutoOffsetReset.None)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Seek to invalid offset to trigger OffsetOutOfRange with AutoOffsetReset.None
        consumer.Seek(new TopicPartitionOffset(topic, 0, 999999));

        // Act & Assert - with AutoOffsetReset.None, should throw KafkaException
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
