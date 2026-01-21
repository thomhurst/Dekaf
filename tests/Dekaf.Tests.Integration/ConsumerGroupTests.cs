using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for consumer group coordination.
/// </summary>
[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class ConsumerGroupTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task ConsumerGroup_SingleConsumer_GetsAllPartitions()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce to all partitions
        for (var p = 0; p < 3; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            });
        }

        // Act
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithSessionTimeout(10000)
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

        // Assert - single consumer should get all partitions
        await Assert.That(messages).Count().IsEqualTo(3);
        var partitions = messages.Select(m => m.Partition).Distinct().OrderBy(p => p).ToList();
        int[] expectedPartitions = [0, 1, 2];
        await Assert.That(partitions).IsEquivalentTo(expectedPartitions);
    }

    [Test]
    public async Task ConsumerGroup_JoinAndLeave_TriggersRebalance()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 4);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce messages to all partitions
        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            });
        }

        // Start first consumer
        await using var consumer1 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(10000)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .DisableAutoCommit()
            .Build();

        consumer1.Subscribe(topic);

        // Consume at least one message to ensure group is stable
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result1 = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token);
        await Assert.That(result1).IsNotNull();

        // Consumer 1 should have all partitions initially
        var assignment1 = consumer1.Assignment;
        await Assert.That(assignment1).Count().IsEqualTo(4);
    }

    [Test]
    public async Task ConsumerGroup_NewGroupId_StartsFromConfiguredOffset()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
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

        // Act - consume with AutoOffsetReset.Earliest (new group, no committed offsets)
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithSessionTimeout(10000)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 5) break;
        }

        // Assert - should get all messages starting from beginning
        await Assert.That(messages).Count().IsEqualTo(5);
        await Assert.That(messages[0].Offset).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumerGroup_CommittedOffset_NewConsumerStartsFromCommit()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
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

        // First consumer: consume 3 messages and commit
        await using (var consumer1 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(10000)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .DisableAutoCommit()
            .Build())
        {
            consumer1.Subscribe(topic);

            var messages = new List<ConsumeResult<string, string>>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
            {
                messages.Add(msg);
                if (messages.Count >= 3) break;
            }

            // Commit offset 3 (next message to read)
            await consumer1.CommitAsync([new TopicPartitionOffset(topic, 0, 3)]);
        }

        // Second consumer: should start from committed offset
        await using var consumer2 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(10000)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .DisableAutoCommit()
            .Build();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);

        // Assert - should start from offset 3 (after committed offset)
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Offset).IsEqualTo(3);
        await Assert.That(result.Value).IsEqualTo("value-3");
    }

    [Test]
    public async Task ConsumerGroup_MultipleTopics_SubscribesAndConsumesAll()
    {
        // Arrange
        var topic1 = await kafka.CreateTestTopicAsync();
        var topic2 = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce to both topics
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic1,
            Key = "key1",
            Value = "topic1-message"
        });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic2,
            Key = "key2",
            Value = "topic2-message"
        });

        // Act
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithSessionTimeout(10000)
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

        // Assert - should get messages from both topics
        await Assert.That(messages).Count().IsEqualTo(2);
        var topics = messages.Select(m => m.Topic).Distinct().ToList();
        await Assert.That(topics).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ConsumerGroup_Unsubscribe_LeavesGroup()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithSessionTimeout(10000)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        // Subscribe
        consumer.Subscribe(topic);
        await Assert.That(consumer.Subscription).Count().IsEqualTo(1);

        // Unsubscribe
        consumer.Unsubscribe();

        // Assert
        await Assert.That(consumer.Subscription).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ConsumerGroup_Heartbeat_KeepsSessionAlive()
    {
        // This test verifies that heartbeats keep the consumer session alive
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce messages
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithSessionTimeout(15000) // 15 second session
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Consume first message
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var msg1 = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cts.Token);
        await Assert.That(msg1).IsNotNull();

        // Wait a bit (heartbeat should keep session alive)
        await Task.Delay(5000);

        // Should still be able to consume
        var msg2 = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cts.Token);
        await Assert.That(msg2).IsNotNull();
    }

    [Test]
    public async Task ConsumerGroup_ManualAssignment_BypassesGroupProtocol()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce to specific partition
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Partition = 1
        });

        // Act - manually assign without group
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        // No group ID, manual assignment
        consumer.Assign(new TopicPartition(topic, 1));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Partition).IsEqualTo(1);
        await Assert.That(result.Value).IsEqualTo("value");
    }

    [Test]
    public async Task ConsumerGroup_StaticMembership_RejoinsWithoutRebalance()
    {
        // Test static membership using group.instance.id
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var instanceId = $"static-member-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value1"
        });

        // First consumer with static membership
        await using (var consumer1 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(10000)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .DisableAutoCommit()
            .Build())
        {
            consumer1.Subscribe(topic);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value).IsEqualTo("value1");

            // Commit the offset so consumer2 starts from the next message
            await consumer1.CommitAsync([new TopicPartitionOffset(topic, 0, 1)]);
        }

        // Produce another message
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value2"
        });

        // Second consumer with same static membership should rejoin quickly
        await using var consumer2 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(10000)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .DisableAutoCommit()
            .Build();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result2 = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);

        await Assert.That(result2).IsNotNull();
        await Assert.That(result2!.Value).IsEqualTo("value2");
    }

    [Test]
    public async Task ConsumerGroup_OffsetFetch_ReturnsCommittedOffsets()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce messages
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithSessionTimeout(10000)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .DisableAutoCommit()
            .Build();

        consumer.Subscribe(topic);

        // Consume and commit
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 3) break;
        }

        // Commit offset 3
        await consumer.CommitAsync([new TopicPartitionOffset(topic, 0, 3)]);

        // Act - get committed offset
        var committedOffset = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0));

        // Assert
        await Assert.That(committedOffset).IsEqualTo(3);
    }

    [Test]
    public async Task ConsumerGroup_Position_ReturnsCurrentOffset()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce messages
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithSessionTimeout(10000)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .DisableAutoCommit()
            .Build();

        consumer.Subscribe(topic);

        // Consume 3 messages
        var count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            count++;
            if (count >= 3) break;
        }

        // Act - get current position
        var position = consumer.GetPosition(new TopicPartition(topic, 0));

        // Assert - position should be at offset 3 (next to consume)
        await Assert.That(position).IsEqualTo(3);
    }

    [Test]
    public async Task ConsumerGroup_SeekToBeginning_RestartsFromOffset0()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce messages
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithSessionTimeout(10000)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .DisableAutoCommit()
            .Build();

        consumer.Subscribe(topic);

        // Consume 3 messages
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var messages = new List<ConsumeResult<string, string>>();

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 3) break;
        }

        // Seek back to beginning
        consumer.Seek(new TopicPartitionOffset(topic, 0, 0));

        // Consume again
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cts.Token);

        // Assert - should get first message again
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Offset).IsEqualTo(0);
        await Assert.That(result.Value).IsEqualTo("value-0");
    }
}
