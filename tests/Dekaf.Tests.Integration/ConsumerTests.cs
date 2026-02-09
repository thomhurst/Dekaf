using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for the Kafka consumer.
/// </summary>
public class ConsumerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Consumer_SubscribeAndConsume_ReceivesMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce a message first
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "test-key",
            Value = "test-value"
        });

        // Act - consume
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Topic).IsEqualTo(topic);
        await Assert.That(r.Key).IsEqualTo("test-key");
        await Assert.That(r.Value).IsEqualTo("test-value");
    }

    [Test]
    public async Task Consumer_AutoOffsetResetEarliest_ConsumesFromBeginning()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages first
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - new consumer with earliest should see all messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 3) break;
        }

        // Assert - should have all 3 messages starting from offset 0
        await Assert.That(messages).Count().IsEqualTo(3);
        await Assert.That(messages[0].Offset).IsEqualTo(0);
        await Assert.That(messages[0].Value).IsEqualTo("value-0");
    }

    [Test]
    public async Task Consumer_ManualAssignment_ConsumesFromAssignedPartition()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        // Produce messages to specific partitions
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "partition-1-message",
            Partition = 1
        });

        // Act - manually assign only partition 1
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 1));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Partition).IsEqualTo(1);
        await Assert.That(r.Value).IsEqualTo("partition-1-message");
    }

    [Test]
    public async Task Consumer_SeekToOffset_ConsumesFromSpecifiedOffset()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce multiple messages
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

        // Act - seek to offset 3
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));
        consumer.Seek(new TopicPartitionOffset(topic, 0, 3));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert - should get message at offset 3
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Offset).IsEqualTo(3);
        await Assert.That(r.Value).IsEqualTo("value-3");
    }

    [Test]
    public async Task Consumer_SeekToBeginning_ConsumesFromStart()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Produce messages
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

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
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);
        consumer.SeekToBeginning(tp);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task Consumer_ManualCommit_CommitsOffset()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce message
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Act - consume and commit
        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000)) // Short timeout for faster rebalance
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer1.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
        await Assert.That(result).IsNotNull();

        // Commit the offset
        await consumer1.CommitAsync();

        // Close first consumer
        await consumer1.DisposeAsync();

        // Produce another message
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key2",
            Value = "value2"
        });

        // Act - new consumer should start after committed offset
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000)) // Short timeout for faster rebalance
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result2 = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);

        // Assert - should get second message (first was committed)
        await Assert.That(result2).IsNotNull();
        await Assert.That(result2!.Value.Value).IsEqualTo("value2");
    }

    [Test]
    public async Task Consumer_CommitSpecificOffsets_CommitsCorrectly()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages
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

        // Act - consume some and commit specific offset
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Consume 3 messages
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 3) break;
        }

        // Commit offset 3 (next message to consume)
        await consumer.CommitAsync([new TopicPartitionOffset(topic, 0, 3)]);

        // Get committed offset
        var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0));

        // Assert
        await Assert.That(committed).IsEqualTo(3);
    }

    [Test]
    public async Task Consumer_ConsumeWithHeaders_ReceivesHeaders()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce message with headers
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        var headers = new Headers
        {
            { "header1", "headerValue1"u8.ToArray() },
            { "header2", "headerValue2"u8.ToArray() }
        };

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1",
            Headers = headers
        });

        // Act
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Headers).IsNotNull();
        await Assert.That(r.Headers!.Count).IsEqualTo(2);

        var header1 = r.Headers.FirstOrDefault(h => h.Key == "header1");
        await Assert.That(header1.Key).IsNotNull();
        await Assert.That(System.Text.Encoding.UTF8.GetString(header1.Value.Span)).IsEqualTo("headerValue1");
    }

    [Test]
    public async Task Consumer_PauseAndResume_WorksCorrectly()
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
            Key = "key1",
            Value = "value1"
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Act - pause
        consumer.Pause(tp);

        // Assert
        await Assert.That(consumer.Paused.Contains(tp)).IsTrue();

        // Act - resume
        consumer.Resume(tp);

        // Assert
        await Assert.That(consumer.Paused.Contains(tp)).IsFalse();
    }

    [Test]
    public async Task Consumer_Unsubscribe_ClearsSubscription()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .BuildAsync();

        // Act
        consumer.Subscribe(topic);
        await Assert.That(consumer.Subscription.Contains(topic)).IsTrue();

        consumer.Unsubscribe();

        // Assert
        await Assert.That(consumer.Subscription).IsEmpty();
    }

    [Test]
    public async Task Consumer_MultipleTopicSubscription_ConsumesFromAll()
    {
        // Arrange
        var topic1 = await KafkaContainer.CreateTestTopicAsync();
        var topic2 = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic1,
            Key = "key1",
            Value = "from-topic1"
        });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic2,
            Key = "key2",
            Value = "from-topic2"
        });

        // Act
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic1, topic2);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 2) break;
        }

        // Assert
        await Assert.That(messages).Count().IsEqualTo(2);
        var topics = messages.Select(m => m.Topic).Distinct().ToList();
        await Assert.That(topics).Contains(topic1);
        await Assert.That(topics).Contains(topic2);
    }

    [Test]
    public async Task Consumer_GetPosition_ReturnsCurrentPosition()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Act
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // After consuming, position should be 1 (next offset to consume)
        var position = consumer.GetPosition(new TopicPartition(topic, 0));

        // Assert
        await Assert.That(position).IsEqualTo(1);
    }

    [Test]
    public async Task Consumer_SeekToEnd_SkipsExistingMessages()
    {
        // Arrange - produce messages first
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"old-key-{i}",
                Value = $"old-value-{i}"
            });
        }

        // Act - create consumer, assign, and use AutoOffsetReset.Latest
        // This tests that with Latest, we only get new messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Latest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Start consuming in background - should wait for new messages
        var receivedMessages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var consumeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in consumer.ConsumeAsync(cts.Token))
                {
                    receivedMessages.Add(msg);
                    if (receivedMessages.Count >= 1) break;
                }
            }
            catch (OperationCanceledException) { }
        });

        // Wait for consumer to start and resolve the offset
        await Task.Delay(1000);

        // Produce a new message
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "new-key",
            Value = "new-value"
        });

        await consumeTask;

        // Assert - should get the new message, not the old ones
        await Assert.That(receivedMessages.Count).IsEqualTo(1);
        await Assert.That(receivedMessages[0].Key).IsEqualTo("new-key");
        await Assert.That(receivedMessages[0].Value).IsEqualTo("new-value");
    }

    [Test]
    public async Task Consumer_ReplayMessages_SeekBackAfterConsuming()
    {
        // This tests the common "replay" scenario where a consumer
        // consumes messages, then seeks back to replay them

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

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

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Act - consume all 5 messages
        var firstPassMessages = new List<string>();
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts1.Token))
        {
            firstPassMessages.Add(msg.Value);
            if (firstPassMessages.Count >= 5) break;
        }

        // Verify we consumed all 5
        await Assert.That(firstPassMessages.Count).IsEqualTo(5);

        // Now seek back to beginning for replay
        consumer.SeekToBeginning(tp);

        // Consume again - should get same 5 messages
        var replayMessages = new List<string>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts2.Token))
        {
            replayMessages.Add(msg.Value);
            if (replayMessages.Count >= 5) break;
        }

        // Assert - replay should match first pass
        await Assert.That(replayMessages.Count).IsEqualTo(5);
        for (var i = 0; i < 5; i++)
        {
            await Assert.That(replayMessages[i]).IsEqualTo(firstPassMessages[i]);
        }
    }

    [Test]
    public async Task Consumer_SeekToSpecificOffset_ReplaysFromThatPoint()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce 10 messages
        for (var i = 0; i < 10; i++)
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
        consumer.Assign(tp);

        // Act - consume first 5 messages
        var consumed = new List<ConsumeResult<string, string>>();
        consumer.Seek(new TopicPartitionOffset(topic, 0, 0));

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts1.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= 5) break;
        }

        // Verify position is now 5
        var position = consumer.GetPosition(tp);
        await Assert.That(position).IsEqualTo(5);

        // Seek back to offset 2
        consumer.Seek(new TopicPartitionOffset(topic, 0, 2));

        // Consume 3 messages starting from offset 2
        var replayed = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts2.Token))
        {
            replayed.Add(msg);
            if (replayed.Count >= 3) break;
        }

        // Assert - should get messages at offsets 2, 3, 4
        await Assert.That(replayed.Count).IsEqualTo(3);
        await Assert.That(replayed[0].Offset).IsEqualTo(2);
        await Assert.That(replayed[0].Value).IsEqualTo("value-2");
        await Assert.That(replayed[1].Offset).IsEqualTo(3);
        await Assert.That(replayed[2].Offset).IsEqualTo(4);
    }

    [Test]
    public async Task Consumer_AutoOffsetResetLatest_OnlyGetsNewMessages()
    {
        // Arrange - produce messages before consumer starts
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce old messages
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"old-key-{i}",
                Value = $"old-value-{i}"
            });
        }

        // Act - create consumer with Latest offset reset
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Latest)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Start consuming in background - should not get old messages
        var receivedMessages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var consumeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in consumer.ConsumeAsync(cts.Token))
                {
                    receivedMessages.Add(msg);
                    if (receivedMessages.Count >= 1) break;
                }
            }
            catch (OperationCanceledException) { }
        });

        // Wait for consumer to get partition assignment before producing new message
        // This ensures ListOffsets (which determines "latest") is called before our produce
        var assignmentTimeout = TimeSpan.FromSeconds(15);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (consumer.Assignment.Count == 0 && sw.Elapsed < assignmentTimeout)
        {
            await Task.Delay(100);
        }

        if (consumer.Assignment.Count == 0)
        {
            throw new InvalidOperationException("Consumer did not receive assignment within timeout");
        }

        // Small additional delay to ensure positions are initialized after assignment
        await Task.Delay(500);

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "new-key",
            Value = "new-value"
        });

        await consumeTask;

        // Assert - should only get the new message, not old ones
        await Assert.That(receivedMessages.Count).IsEqualTo(1);
        await Assert.That(receivedMessages[0].Key).IsEqualTo("new-key");
        await Assert.That(receivedMessages[0].Value).IsEqualTo("new-value");
    }

    [Test]
    public async Task Consumer_SeekAfterCommit_OverridesCommittedOffset()
    {
        // Tests that seek takes precedence over committed offset
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

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

        // First consumer: consume all and commit
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync())
        {
            consumer1.Subscribe(topic);

            var count = 0;
            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer1.ConsumeAsync(cts1.Token))
            {
                count++;
                if (count >= 5) break;
            }

            // Commit at offset 5 (all messages consumed)
            await consumer1.CommitAsync();
        }

        // Second consumer: same group, but seek back to offset 1
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer2.Assign(tp);

        // Seek to offset 1 (should override the committed offset of 5)
        consumer2.Seek(new TopicPartitionOffset(topic, 0, 1));

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);

        // Assert - should get message at offset 1, not offset 5
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Offset).IsEqualTo(1);
        await Assert.That(r.Value).IsEqualTo("value-1");
    }

    [Test]
    public async Task Consumer_MultipleSeeks_LastSeekWins()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

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

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Act - multiple seeks, last one should win
        consumer.Seek(new TopicPartitionOffset(topic, 0, 2));
        consumer.Seek(new TopicPartitionOffset(topic, 0, 5));
        consumer.Seek(new TopicPartitionOffset(topic, 0, 7));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert - should get message at offset 7 (last seek)
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Offset).IsEqualTo(7);
        await Assert.That(r.Value).IsEqualTo("value-7");
    }

    [Test]
    public async Task Consumer_ParallelFetchFromMultiplePartitions_ConsumesAllMessages()
    {
        // This test verifies that parallel fetches work correctly across multiple partitions
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 6);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce messages to all 6 partitions
        const int messagesPerPartition = 100;
        for (var partition = 0; partition < 6; partition++)
        {
            for (var i = 0; i < messagesPerPartition; i++)
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

        await producer.FlushAsync();

        // Act - consume with prefetching enabled (default)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var receivedMessages = new List<ConsumeResult<string, string>>();
        var partitionCounts = new Dictionary<int, int>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            receivedMessages.Add(msg);
            partitionCounts[msg.Partition] = partitionCounts.GetValueOrDefault(msg.Partition) + 1;
            if (receivedMessages.Count >= 6 * messagesPerPartition) break;
        }

        // Assert - should receive all messages from all partitions
        await Assert.That(receivedMessages.Count).IsEqualTo(6 * messagesPerPartition);
        await Assert.That(partitionCounts.Count).IsEqualTo(6);
        foreach (var (partition, count) in partitionCounts)
        {
            await Assert.That(count).IsEqualTo(messagesPerPartition);
        }
    }

    [Test]
    public async Task Consumer_PrefetchEnabled_ImprovesThroughput()
    {
        // This test verifies that prefetching (enabled by default) works correctly
        // by consuming a large number of messages and checking timing
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithBatchSize(65536)
            .BuildAsync();

        // Produce a batch of messages
        const int totalMessages = 1000;
        for (var i = 0; i < totalMessages; i++)
        {
            producer.Send(topic, $"key-{i}", $"value-{i}");
        }

        await producer.FlushAsync();

        // Act - consume with prefetching (default QueuedMinMessages = 100000)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var receivedCount = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            receivedCount++;
            if (receivedCount >= totalMessages) break;
        }

        sw.Stop();

        // Assert - should receive all messages
        await Assert.That(receivedCount).IsEqualTo(totalMessages);

        // With prefetching enabled, we should consume messages efficiently
        // This is more of a smoke test - if parallelization is broken, this would timeout
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task Consumer_OffsetOutOfRange_WithEarliest_ResetsToBeginning()
    {
        // This tests that when a consumer seeks to an invalid offset (beyond high watermark),
        // it recovers by resetting to earliest when AutoOffsetReset.Earliest is configured
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce 3 messages (offsets 0, 1, 2)
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
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Seek to an offset that doesn't exist (way beyond high watermark)
        // This will cause OffsetOutOfRange error on fetch
        consumer.Seek(new TopicPartitionOffset(topic, 0, 999999));

        // With AutoOffsetReset.Earliest, should recover and get messages from beginning
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert - should reset to earliest and get first message
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(0);
        await Assert.That(result.Value.Value).IsEqualTo("value-0");
    }

    [Test]
    public async Task Consumer_OffsetOutOfRange_WithLatest_ResetsToEnd()
    {
        // This tests that when AutoOffsetReset.Latest is configured and OffsetOutOfRange occurs,
        // the consumer resets to the end and only receives new messages
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce 3 old messages
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"old-key-{i}",
                Value = $"old-value-{i}"
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Latest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Seek to an invalid offset to trigger OffsetOutOfRange
        consumer.Seek(new TopicPartitionOffset(topic, 0, 999999));

        // Start consuming in background
        var receivedMessages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var consumeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in consumer.ConsumeAsync(cts.Token))
                {
                    receivedMessages.Add(msg);
                    if (receivedMessages.Count >= 1) break;
                }
            }
            catch (OperationCanceledException) { }
        });

        // Wait for consumer to recover from OffsetOutOfRange
        await Task.Delay(2000);

        // Produce a new message
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "new-key",
            Value = "new-value"
        });

        await consumeTask;

        // Assert - with Latest, should only get the new message
        await Assert.That(receivedMessages.Count).IsEqualTo(1);
        await Assert.That(receivedMessages[0].Key).IsEqualTo("new-key");
        await Assert.That(receivedMessages[0].Value).IsEqualTo("new-value");
    }

    [Test]
    public async Task Consumer_OffsetOutOfRange_WithNone_ThrowsException()
    {
        // This tests that when AutoOffsetReset.None is configured and OffsetOutOfRange occurs,
        // the consumer throws a KafkaException
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce a message
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.None)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Seek to an invalid offset to trigger OffsetOutOfRange
        consumer.Seek(new TopicPartitionOffset(topic, 0, 999999));

        // With AutoOffsetReset.None, should throw KafkaException
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await Assert.That(async () =>
        {
            await foreach (var _ in consumer.ConsumeAsync(cts.Token))
            {
                // Should never get here
            }
        }).Throws<KafkaException>();
    }

    [Test]
    public async Task Consumer_ManualAssignMultiplePartitions_FetchesInParallel()
    {
        // This test verifies parallel fetching with manual partition assignment
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce messages to each partition
        const int messagesPerPartition = 50;
        for (var partition = 0; partition < 4; partition++)
        {
            for (var i = 0; i < messagesPerPartition; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{partition}-{i}",
                    Value = $"partition-{partition}-msg-{i}",
                    Partition = partition
                });
            }
        }

        await producer.FlushAsync();

        // Act - manually assign all 4 partitions
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        // Assign all partitions
        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 1),
            new TopicPartition(topic, 2),
            new TopicPartition(topic, 3));

        var receivedByPartition = new Dictionary<int, List<string>>();
        for (var i = 0; i < 4; i++)
        {
            receivedByPartition[i] = [];
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            receivedByPartition[msg.Partition].Add(msg.Value);
            var totalReceived = receivedByPartition.Values.Sum(l => l.Count);
            if (totalReceived >= 4 * messagesPerPartition) break;
        }

        // Assert - should have received all messages from all partitions
        for (var partition = 0; partition < 4; partition++)
        {
            await Assert.That(receivedByPartition[partition].Count).IsEqualTo(messagesPerPartition);
        }
    }
}
