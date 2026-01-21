using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for the Kafka consumer.
/// </summary>
[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class ConsumerTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task Consumer_SubscribeAndConsume_ReceivesMessages()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce a message first
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "test-key",
            Value = "test-value"
        });

        // Act - consume
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Topic).IsEqualTo(topic);
        await Assert.That(result.Key).IsEqualTo("test-key");
        await Assert.That(result.Value).IsEqualTo("test-value");
    }

    [Test]
    public async Task Consumer_AutoOffsetResetEarliest_ConsumesFromBeginning()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages first
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

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
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

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
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);

        // Produce messages to specific partitions
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "partition-1-message",
            Partition = 1
        });

        // Act - manually assign only partition 1
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 1));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Partition).IsEqualTo(1);
        await Assert.That(result.Value).IsEqualTo("partition-1-message");
    }

    [Test]
    public async Task Consumer_SeekToOffset_ConsumesFromSpecifiedOffset()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce multiple messages
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

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
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 0));
        consumer.Seek(new TopicPartitionOffset(topic, 0, 3));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert - should get message at offset 3
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Offset).IsEqualTo(3);
        await Assert.That(result.Value).IsEqualTo("value-3");
    }

    [Test]
    public async Task Consumer_SeekToBeginning_ConsumesFromStart()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        // Produce messages
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

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
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);
        consumer.SeekToBeginning(tp);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task Consumer_ManualCommit_CommitsOffset()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce message
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Act - consume and commit
        await using var consumer1 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000) // Short timeout for faster rebalance
            .DisableAutoCommit()
            .Build();

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
        await using var consumer2 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000) // Short timeout for faster rebalance
            .DisableAutoCommit()
            .Build();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result2 = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);

        // Assert - should get second message (first was committed)
        await Assert.That(result2).IsNotNull();
        await Assert.That(result2!.Value).IsEqualTo("value2");
    }

    [Test]
    public async Task Consumer_CommitSpecificOffsets_CommitsCorrectly()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

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
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .DisableAutoCommit()
            .Build();

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
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce message with headers
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

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
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Headers).IsNotNull();
        await Assert.That(result.Headers!.Count).IsEqualTo(2);

        var header1 = result.Headers.FirstOrDefault(h => h.Key == "header1");
        await Assert.That(header1).IsNotNull();
        await Assert.That(System.Text.Encoding.UTF8.GetString(header1!.Value)).IsEqualTo("headerValue1");
    }

    [Test]
    public async Task Consumer_PauseAndResume_WorksCorrectly()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .Build();

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
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .Build();

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
        var topic1 = await kafka.CreateTestTopicAsync();
        var topic2 = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

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
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

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
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Act
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // After consuming, position should be 1 (next offset to consume)
        var position = consumer.GetPosition(new TopicPartition(topic, 0));

        // Assert
        await Assert.That(position).IsEqualTo(1);
    }
}
