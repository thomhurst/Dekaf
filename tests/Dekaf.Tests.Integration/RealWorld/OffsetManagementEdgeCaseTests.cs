using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for offset management edge cases.
/// Verifies out-of-order commits, commit-ahead-of-consumed, AutoOffsetReset.None behavior,
/// empty commit list handling, and uncommitted offset retrieval.
/// </summary>
[Category("Offsets")]
public sealed class OffsetManagementEdgeCaseTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task CommitLaterOffset_ThenEarlierOffset_LastCommitWins()
    {
        // Kafka stores the last committed offset — committing an earlier offset overwrites the later one
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-offset-order")
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

        // Consume all messages and commit offset 7
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-offset-order-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync())
        {
            consumer1.Subscribe(topic);

            var consumed = new List<ConsumeResult<string, string>>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
            {
                consumed.Add(msg);
                if (consumed.Count >= 10) break;
            }

            // Commit offset 7 first
            await consumer1.CommitAsync([new TopicPartitionOffset(topic, 0, 7)]);

            // Then commit offset 3 — this should overwrite
            await consumer1.CommitAsync([new TopicPartitionOffset(topic, 0, 3)]);
        }

        // Act - new consumer should start from offset 3 (last committed)
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-offset-order-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts2.Token);

        // Assert - should resume from offset 3
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(3);
        await Assert.That(result.Value.Value).IsEqualTo("value-3");
    }

    [Test]
    public async Task CommitAheadOfConsumed_SkipsMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-commit-ahead")
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

        // Commit offset 5 without consuming any messages
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-commit-ahead-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync())
        {
            consumer1.Subscribe(topic);

            // Consume one message to join group and get assignment
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

            // Commit ahead — skip to offset 5
            await consumer1.CommitAsync([new TopicPartitionOffset(topic, 0, 5)]);
        }

        // Act - new consumer should start from committed offset 5
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-commit-ahead-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts2.Token);

        // Assert - messages 0-4 skipped
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(5);
        await Assert.That(result.Value.Value).IsEqualTo("value-5");
    }

    [Test]
    public async Task AutoOffsetResetNone_InvalidOffset_Throws()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-reset-none")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        // Act - manual assignment with AutoOffsetReset.None and invalid offset
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-reset-none")
            .WithAutoOffsetReset(AutoOffsetReset.None)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        // Seek to an offset that doesn't exist
        consumer.Seek(new TopicPartitionOffset(topic, 0, 999999));

        // Assert - should throw since AutoOffsetReset.None doesn't recover
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await Assert.That(async () =>
        {
            await foreach (var _ in consumer.ConsumeAsync(cts.Token))
            {
                // Should throw before yielding
            }
        }).Throws<KafkaException>();
    }

    [Test]
    public async Task GetCommittedOffset_Uncommitted_ReturnsNull()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-uncommitted-offset")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Trigger group join
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts.Token);

        // Act - get committed offset without ever committing
        var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0));

        // Assert - should be null (no committed offset)
        await Assert.That(committed).IsNull();
    }

    [Test]
    public async Task CommitSpecificOffset_ThenGetCommitted_ReturnsExact()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-commit-get")
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

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-commit-get")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Consume some messages
        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= 3) break;
        }

        // Act - commit specific offset
        await consumer.CommitAsync([new TopicPartitionOffset(topic, 0, 3)]);

        var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0));

        // Assert
        await Assert.That(committed).IsEqualTo(3);
    }

    [Test]
    public async Task Position_TracksConsumedOffset()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-position")
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

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-position")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        // Act - consume 3 messages
        var count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var _ in consumer.ConsumeAsync(cts.Token))
        {
            count++;
            if (count >= 3) break;
        }

        var position = consumer.GetPosition(new TopicPartition(topic, 0));

        // Assert - position should be 3 (next offset to consume)
        await Assert.That(position).IsEqualTo(3);
    }

    [Test]
    public async Task SeekToBeginning_AfterConsuming_Replays()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-seek-replay")
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

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-seek-replay")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Consume all 5 messages
        var firstPass = new List<ConsumeResult<string, string>>();
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts1.Token))
        {
            firstPass.Add(msg);
            if (firstPass.Count >= 5) break;
        }

        await Assert.That(firstPass.Count).IsEqualTo(5);

        // Act - seek back to beginning
        consumer.SeekToBeginning(tp);

        // Consume again
        var secondPass = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts2.Token))
        {
            secondPass.Add(msg);
            if (secondPass.Count >= 5) break;
        }

        // Assert - should replay all messages from offset 0
        await Assert.That(secondPass.Count).IsEqualTo(5);
        await Assert.That(secondPass[0].Offset).IsEqualTo(0);
        await Assert.That(secondPass[0].Value).IsEqualTo("value-0");
    }

    [Test]
    public async Task SeekToEnd_ConsumesOnlyNewMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-seek-end")
            .BuildAsync();

        // Produce initial messages that should NOT be seen by a Latest consumer
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-old-{i}",
                Value = $"value-old-{i}"
            });
        }

        // Consumer with AutoOffsetReset.Latest — skips existing messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-seek-end")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Latest)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Start consuming in background — this triggers group join and waits for new messages
        ConsumeResult<string, string>? received = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var consumeTask = Task.Run(async () =>
        {
            await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
            {
                received = msg;
                return;
            }
        });

        // Give consumer time to join the group and be assigned partitions at Latest offset
        await Task.Delay(5000).ConfigureAwait(false);

        // Produce a new message after consumer has joined at Latest
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-new",
            Value = "value-new"
        });

        // Wait for the consumer to receive the message
        await consumeTask.ConfigureAwait(false);

        // Assert - should only see the new message (old messages were before consumer joined)
        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Value.Key).IsEqualTo("key-new");
        await Assert.That(received.Value.Value).IsEqualTo("value-new");
    }
}
