using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for watermark offsets functionality.
/// </summary>
[Category("Messaging")]
public class WatermarkOffsetsTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task GetWatermarkOffsets_ReturnsNull_WhenNoDataCached()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Act - check watermarks before any consuming
        var watermarks = consumer.GetWatermarkOffsets(tp);

        // Assert - should return null since no fetch has occurred
        await Assert.That(watermarks).IsNull();
    }

    [Test]
    public async Task GetWatermarkOffsets_ReturnsCachedValues_AfterConsuming()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Produce some messages first
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - consume messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Consume one message to trigger a fetch
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
        await Assert.That(result).IsNotNull();

        // Now check cached watermarks
        var watermarks = consumer.GetWatermarkOffsets(tp);

        // Assert - watermarks should now be cached
        await Assert.That(watermarks).IsNotNull();
        await Assert.That(watermarks!.Value.Low).IsEqualTo(0);
        await Assert.That(watermarks!.Value.High).IsEqualTo(5);
    }

    [Test]
    public async Task QueryWatermarkOffsetsAsync_ReturnsCorrectOffsets()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Produce some messages
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - query watermarks directly from cluster
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        var watermarks = await consumer.QueryWatermarkOffsetsAsync(tp);

        // Assert
        await Assert.That(watermarks.Low).IsEqualTo(0);
        await Assert.That(watermarks.High).IsEqualTo(10);
    }

    [Test]
    public async Task QueryWatermarkOffsetsAsync_EmptyTopic_ReturnsZeroOffsets()
    {
        // Arrange - create topic but don't produce any messages
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);

        // Act
        var watermarks = await consumer.QueryWatermarkOffsetsAsync(tp);

        // Assert - empty topic should have (0, 0)
        await Assert.That(watermarks.Low).IsEqualTo(0);
        await Assert.That(watermarks.High).IsEqualTo(0);
    }

    [Test]
    public async Task QueryWatermarkOffsetsAsync_TopicWithMessages_ReturnsCorrectRange()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce 3 messages
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);

        // Act - query initial watermarks
        var initialWatermarks = await consumer.QueryWatermarkOffsetsAsync(tp);

        // Assert initial state
        await Assert.That(initialWatermarks.Low).IsEqualTo(0);
        await Assert.That(initialWatermarks.High).IsEqualTo(3);

        // Produce more messages
        for (var i = 3; i < 7; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - query updated watermarks
        var updatedWatermarks = await consumer.QueryWatermarkOffsetsAsync(tp);

        // Assert updated state
        await Assert.That(updatedWatermarks.Low).IsEqualTo(0);
        await Assert.That(updatedWatermarks.High).IsEqualTo(7);
    }

    [Test]
    public async Task QueryWatermarkOffsetsAsync_AlsoCachesResult()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);

        // Verify no cached watermarks initially
        var cachedBefore = consumer.GetWatermarkOffsets(tp);
        await Assert.That(cachedBefore).IsNull();

        // Act - query watermarks
        var queried = await consumer.QueryWatermarkOffsetsAsync(tp);

        // Assert - result should now be cached
        var cachedAfter = consumer.GetWatermarkOffsets(tp);
        await Assert.That(cachedAfter).IsNotNull();
        await Assert.That(cachedAfter!.Value.Low).IsEqualTo(queried.Low);
        await Assert.That(cachedAfter!.Value.High).IsEqualTo(queried.High);
    }

    [Test]
    public async Task GetWatermarkOffsets_MultiplePartitions_ReturnsSeparateValues()
    {
        // Arrange - create topic with multiple partitions
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce different amounts to different partitions
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-p0",
            Value = "value-p0",
            Partition = 0
        });

        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p1-{i}",
                Value = $"value-p1-{i}",
                Partition = 1
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .BuildAsync();

        var tp0 = new TopicPartition(topic, 0);
        var tp1 = new TopicPartition(topic, 1);
        var tp2 = new TopicPartition(topic, 2);

        // Act - query watermarks for each partition
        var watermarks0 = await consumer.QueryWatermarkOffsetsAsync(tp0);
        var watermarks1 = await consumer.QueryWatermarkOffsetsAsync(tp1);
        var watermarks2 = await consumer.QueryWatermarkOffsetsAsync(tp2);

        // Assert - each partition has its own watermarks
        await Assert.That(watermarks0.Low).IsEqualTo(0);
        await Assert.That(watermarks0.High).IsEqualTo(1); // 1 message

        await Assert.That(watermarks1.Low).IsEqualTo(0);
        await Assert.That(watermarks1.High).IsEqualTo(3); // 3 messages

        await Assert.That(watermarks2.Low).IsEqualTo(0);
        await Assert.That(watermarks2.High).IsEqualTo(0); // no messages
    }
}
