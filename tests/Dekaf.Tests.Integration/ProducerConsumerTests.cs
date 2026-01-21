namespace Dekaf.Tests.Integration;

[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class ProducerConsumerTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task Producer_CanConnect_ToKafka()
    {
        // Arrange
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Act & Assert - should not throw
        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task Consumer_CanConnect_ToKafka()
    {
        // Arrange
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId("test-group")
            .Build();

        // Act & Assert - should not throw
        await Assert.That(consumer).IsNotNull();
    }

    [Test]
    public async Task ProduceAndConsume_RoundTrip()
    {
        // Arrange - create a unique topic for this test
        var testTopic = await kafka.CreateTestTopicAsync(partitions: 1);

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .WithAcks(Producer.Acks.All)
            .Build();

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .Build();

        // Act - Produce
        var metadata = await producer.ProduceAsync(new Producer.ProducerMessage<string, string>
        {
            Topic = testTopic,
            Key = "test-key",
            Value = "test-value"
        });

        await Assert.That(metadata.Topic).IsEqualTo(testTopic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Act - Consume
        consumer.Subscribe(testTopic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var received = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Key).IsEqualTo("test-key");
        await Assert.That(received.Value).IsEqualTo("test-value");
    }
}
