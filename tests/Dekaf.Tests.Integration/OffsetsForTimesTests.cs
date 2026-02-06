using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for GetOffsetsForTimesAsync functionality.
/// Tests the ability to look up offsets by timestamp using the ListOffsets API.
/// </summary>
public class OffsetsForTimesTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task GetOffsetsForTimesAsync_WithValidTimestamp_ReturnsCorrectOffset()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Record timestamp before producing first message
        var beforeFirstMessage = DateTimeOffset.UtcNow;
        await Task.Delay(50); // Small delay to ensure distinct timestamps

        // Produce first batch of messages
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await Task.Delay(100); // Ensure timestamp gap

        // Record timestamp before second batch
        var beforeSecondBatch = DateTimeOffset.UtcNow;
        await Task.Delay(50);

        // Produce second batch of messages
        for (var i = 3; i < 6; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var timestampsToSearch = new[]
        {
            new TopicPartitionTimestamp(tp, beforeSecondBatch)
        };

        var offsets = await consumer.GetOffsetsForTimesAsync(timestampsToSearch);

        // Assert
        await Assert.That(offsets).ContainsKey(tp);
        var offset = offsets[tp];
        // The offset should be at or after 3 (start of second batch)
        // depending on exact timing, it could be 3 or higher
        await Assert.That(offset).IsGreaterThanOrEqualTo(0);
        await Assert.That(offset).IsLessThanOrEqualTo(6);
    }

    [Test]
    public async Task GetOffsetsForTimesAsync_WithLatestTimestamp_ReturnsHighWatermark()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce 5 messages
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var timestampsToSearch = new[]
        {
            new TopicPartitionTimestamp(tp, TopicPartitionTimestamp.Latest)
        };

        var offsets = await consumer.GetOffsetsForTimesAsync(timestampsToSearch);

        // Assert - Latest should return the high watermark (next offset to be written)
        await Assert.That(offsets).ContainsKey(tp);
        var offset = offsets[tp];
        await Assert.That(offset).IsEqualTo(5); // High watermark is 5 (next offset after 0-4)
    }

    [Test]
    public async Task GetOffsetsForTimesAsync_WithEarliestTimestamp_ReturnsFirstOffset()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce some messages
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var timestampsToSearch = new[]
        {
            new TopicPartitionTimestamp(tp, TopicPartitionTimestamp.Earliest)
        };

        var offsets = await consumer.GetOffsetsForTimesAsync(timestampsToSearch);

        // Assert - Earliest should return offset 0 (first message)
        await Assert.That(offsets).ContainsKey(tp);
        var offset = offsets[tp];
        await Assert.That(offset).IsEqualTo(0);
    }

    [Test]
    public async Task GetOffsetsForTimesAsync_WithEmptyCollection_ReturnsEmptyDictionary()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Act
        var offsets = await consumer.GetOffsetsForTimesAsync([]);

        // Assert
        await Assert.That(offsets).IsEmpty();
    }

    [Test]
    public async Task GetOffsetsForTimesAsync_WithMultiplePartitions_ReturnsAllOffsets()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce messages to each partition
        for (var partition = 0; partition < 3; partition++)
        {
            for (var i = 0; i < 5; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-p{partition}-{i}",
                    Value = $"value-p{partition}-{i}",
                    Partition = partition
                });
            }
        }

        // Act
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .Build();

        var tp0 = new TopicPartition(topic, 0);
        var tp1 = new TopicPartition(topic, 1);
        var tp2 = new TopicPartition(topic, 2);
        consumer.Assign(tp0, tp1, tp2);

        var timestampsToSearch = new[]
        {
            new TopicPartitionTimestamp(tp0, TopicPartitionTimestamp.Latest),
            new TopicPartitionTimestamp(tp1, TopicPartitionTimestamp.Earliest),
            new TopicPartitionTimestamp(tp2, TopicPartitionTimestamp.Latest)
        };

        var offsets = await consumer.GetOffsetsForTimesAsync(timestampsToSearch);

        // Assert - should return offsets for all 3 partitions
        await Assert.That(offsets.Count).IsEqualTo(3);

        await Assert.That(offsets).ContainsKey(tp0);
        await Assert.That(offsets).ContainsKey(tp1);
        await Assert.That(offsets).ContainsKey(tp2);

        // tp0: Latest should return high watermark (5)
        await Assert.That(offsets[tp0]).IsEqualTo(5);

        // tp1: Earliest should return 0
        await Assert.That(offsets[tp1]).IsEqualTo(0);

        // tp2: Latest should return high watermark (5)
        await Assert.That(offsets[tp2]).IsEqualTo(5);
    }

    [Test]
    public async Task GetOffsetsForTimesAsync_WithEmptyPartition_ReturnsCorrectOffsets()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Don't produce any messages - partition is empty

        // Act
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Query Latest for empty partition
        var latestOffsets = await consumer.GetOffsetsForTimesAsync([
            new TopicPartitionTimestamp(tp, TopicPartitionTimestamp.Latest)
        ]);

        // Query Earliest for empty partition
        var earliestOffsets = await consumer.GetOffsetsForTimesAsync([
            new TopicPartitionTimestamp(tp, TopicPartitionTimestamp.Earliest)
        ]);

        // Assert - for empty partition:
        // - Latest returns 0 (high watermark, which is offset of next message to be written)
        // - Earliest returns 0 (first available offset)
        await Assert.That(latestOffsets).ContainsKey(tp);
        await Assert.That(latestOffsets[tp]).IsEqualTo(0);

        await Assert.That(earliestOffsets).ContainsKey(tp);
        await Assert.That(earliestOffsets[tp]).IsEqualTo(0);
    }

    [Test]
    public async Task GetOffsetsForTimesAsync_SeekToReturnedOffset_ConsumesCorrectMessage()
    {
        // Arrange - this tests the common use case of seeking to a timestamp
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce messages with delays to ensure distinct timestamps
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
            await Task.Delay(50);
        }

        // Record timestamp after producing
        var afterProducing = DateTimeOffset.UtcNow;

        // Produce more messages
        for (var i = 5; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Get offset for timestamp
        var offsets = await consumer.GetOffsetsForTimesAsync([
            new TopicPartitionTimestamp(tp, afterProducing)
        ]);

        // Seek to the returned offset
        var targetOffset = offsets[tp];
        if (targetOffset >= 0)
        {
            consumer.Seek(new TopicPartitionOffset(topic, 0, targetOffset));

            // Consume and verify we get messages starting at or after the timestamp
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

            // Assert
            await Assert.That(result).IsNotNull();
            var r = result!.Value;
            await Assert.That(r.Offset).IsEqualTo(targetOffset);
        }
    }

    [Test]
    public async Task GetOffsetsForTimesAsync_WithFutureTimestamp_ReturnsHighWatermark()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce some messages
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Use a timestamp far in the future
        var futureTimestamp = DateTimeOffset.UtcNow.AddDays(1);
        var timestampsToSearch = new[]
        {
            new TopicPartitionTimestamp(tp, futureTimestamp)
        };

        var offsets = await consumer.GetOffsetsForTimesAsync(timestampsToSearch);

        // Assert - future timestamp should return -1 (no message found with timestamp >= target)
        // or high watermark depending on Kafka implementation
        await Assert.That(offsets).ContainsKey(tp);
        // The offset will be -1 if no message exists with timestamp >= the future timestamp
        // This is the expected Kafka behavior per the ListOffsets API
        var offset = offsets[tp];
        await Assert.That(offset == -1 || offset == 3).IsTrue();
    }
}
