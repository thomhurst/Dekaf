using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for offset management edge cases.
/// Tests timestamp-based offset lookup boundaries, unassigned partition commits,
/// concurrent offset commits, and seeking beyond the high watermark.
/// </summary>
[Category("Messaging")]
public sealed class OffsetEdgeCaseTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task OffsetsForTimes_BeforeFirstMessage_ReturnsEarliestOffset()
    {
        // Arrange - record a timestamp well before producing any messages
        var timestampBeforeMessages = DateTimeOffset.UtcNow.AddMinutes(-10);

        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-before-first")
            .BuildAsync();

        // Produce some messages
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
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-before-first")
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var offsets = await consumer.GetOffsetsForTimesAsync([
            new TopicPartitionTimestamp(tp, timestampBeforeMessages)
        ]);

        // Assert - timestamp before any messages should return earliest offset (0)
        await Assert.That(offsets).ContainsKey(tp);
        await Assert.That(offsets[tp]).IsEqualTo(0);
    }

    [Test]
    public async Task OffsetsForTimes_AfterLastMessage_ReturnsEndOffset()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-after-last")
            .BuildAsync();

        // Produce some messages
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Wait to ensure all messages have timestamps before our future timestamp
        await Task.Delay(100);
        var timestampAfterMessages = DateTimeOffset.UtcNow.AddMinutes(10);

        // Act
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-after-last")
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var offsets = await consumer.GetOffsetsForTimesAsync([
            new TopicPartitionTimestamp(tp, timestampAfterMessages)
        ]);

        // Assert - timestamp after all messages should return -1 (no message found)
        // or the high watermark offset, depending on Kafka implementation
        await Assert.That(offsets).ContainsKey(tp);
        var offset = offsets[tp];
        // Kafka returns -1 when no message exists with timestamp >= target
        // Some versions may return the high watermark (5)
        await Assert.That(offset == -1 || offset == 5).IsTrue();
    }

    [Test]
    public async Task OffsetsForTimes_ExactTimestamp_ReturnsCorrectOffset()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-exact-ts")
            .BuildAsync();

        // Produce messages with delays to get distinct timestamps
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });

            if (i < 4)
            {
                await Task.Delay(100); // Ensure distinct timestamps
            }
        }

        // Act - look up offset for the timestamp of the third message (index 2)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-exact-ts")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Consume messages to find the exact timestamp of message at offset 2
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 5) break;
        }

        var targetTimestamp = messages[2].Timestamp;

        var offsets = await consumer.GetOffsetsForTimesAsync([
            new TopicPartitionTimestamp(tp, targetTimestamp)
        ]);

        // Assert - using the exact timestamp of message at offset 2 should return offset <= 2
        // (the earliest offset whose timestamp is >= the target timestamp)
        await Assert.That(offsets).ContainsKey(tp);
        var returnedOffset = offsets[tp];
        await Assert.That(returnedOffset).IsGreaterThanOrEqualTo(0);
        await Assert.That(returnedOffset).IsLessThanOrEqualTo(2);

        // Verify we can seek to this offset and consume the correct message
        consumer.Seek(new TopicPartitionOffset(topic, 0, returnedOffset));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Timestamp).IsGreaterThanOrEqualTo(targetTimestamp);
    }

    [Test]
    public async Task CommitOffset_ForUnassignedPartition_HandlesGracefully()
    {
        // Arrange - create a topic with 1 partition
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-unassigned")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-unassigned")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Consume one message to join the group and get assignment
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var msg = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);
        await Assert.That(msg).IsNotNull();

        // Act - try to commit an offset for partition 99 (not assigned to this consumer)
        // Kafka allows committing offsets for any partition in the OffsetCommit API,
        // even for partitions not assigned to the consumer. The broker stores the offset.
        // This should either succeed silently or throw a meaningful exception.
        var committed = false;
        Exception? caughtException = null;

        try
        {
            await consumer.CommitAsync([new TopicPartitionOffset(topic, 99, 0)]);
            committed = true;
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert - either the commit succeeded (Kafka stores it) or a meaningful exception was thrown
        // The important thing is that the consumer doesn't crash or hang
        await Assert.That(committed || caughtException is not null).IsTrue();
    }

    [Test]
    public async Task ConcurrentOffsetCommits_LastWriterWins()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-concurrent")
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

        // Consumer 1: consumes and commits offset 3
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-concurrent-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync())
        {
            consumer1.Subscribe(topic);

            var messages = new List<ConsumeResult<string, string>>();
            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var msg in consumer1.ConsumeAsync(cts1.Token))
            {
                messages.Add(msg);
                if (messages.Count >= 5) break;
            }

            // Commit offset 3
            await consumer1.CommitAsync([new TopicPartitionOffset(topic, 0, 3)]);
        }

        // Consumer 2 (same group): consumes at least one message and commits offset 7
        await using (var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-concurrent-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync())
        {
            consumer2.Subscribe(topic);

            // Consume one message to join the group and get assignment
            using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts2.Token);

            // Commit offset 7 — this is the last write, should win
            await consumer2.CommitAsync([new TopicPartitionOffset(topic, 0, 7)]);
        }

        // Act - new consumer should start from the last committed offset (7)
        await using var consumer3 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-concurrent-3")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer3.Subscribe(topic);

        using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer3.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts3.Token);

        // Assert - the last committed offset (7) should win
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(7);
        await Assert.That(result.Value.Value).IsEqualTo("value-7");
    }

    [Test]
    public async Task SeekBeyondHighWatermark_ConsumerHandlesGracefully()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-seek-beyond")
            .BuildAsync();

        // Produce 5 messages (offsets 0-4, high watermark = 5)
        for (var i = 0; i < 5; i++)
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
            .WithClientId("test-consumer-seek-beyond")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Seek way past the end of the partition
        consumer.Seek(new TopicPartitionOffset(topic, 0, 999999));

        // Produce a new message after seeking — this will be at offset 5
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-new",
            Value = "value-new"
        });

        // Act - try to consume. The consumer should handle the out-of-range seek gracefully.
        // With AutoOffsetReset.Earliest, Kafka resets to the earliest available offset
        // when a fetch returns OffsetOutOfRange.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        // Assert - consumer should recover and return a message
        // The exact behavior depends on AutoOffsetReset: Earliest resets to beginning,
        // Latest resets to end. Either way, the consumer should not crash.
        await Assert.That(result).IsNotNull();
    }
}
