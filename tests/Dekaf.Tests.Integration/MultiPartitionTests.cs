using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for multi-partition scenarios.
/// </summary>
public class MultiPartitionTests(KafkaTestContainer kafka) : KafkaIntegrationTest
{
    [Test]
    public async Task MultiPartition_KeyBasedPartitioning_SameKeyGoesToSamePartition()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 5);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Act - produce multiple messages with same key
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 10; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "consistent-key",
                Value = $"value-{i}"
            });
            results.Add(metadata);
        }

        // Assert - all should go to same partition
        var partitions = results.Select(r => r.Partition).Distinct().ToList();
        await Assert.That(partitions).Count().IsEqualTo(1);
    }

    [Test]
    public async Task MultiPartition_DifferentKeys_DistributeAcrossPartitions()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 5);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Act - produce messages with different keys
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 50; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"different-key-{i}",
                Value = $"value-{i}"
            });
            results.Add(metadata);
        }

        // Assert - should distribute across multiple partitions
        var partitions = results.Select(r => r.Partition).Distinct().ToList();
        await Assert.That(partitions.Count).IsGreaterThanOrEqualTo(2); // At least 2 different partitions
    }

    [Test]
    public async Task MultiPartition_ManualAssignment_ConsumesOnlyAssignedPartition()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);

        await using var producer = Kafka.CreateProducer<string, string>()
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
                Value = $"value-partition-{p}",
                Partition = p
            });
        }

        // Act - manually assign only partition 1
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 1));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Partition).IsEqualTo(1);
        await Assert.That(r.Value).IsEqualTo("value-partition-1");
    }

    [Test]
    public async Task MultiPartition_AssignMultiplePartitions_ConsumesFromAll()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 4);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce to partitions 0, 1, and 2
        for (var p = 0; p < 3; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-p{p}",
                Partition = p
            });
        }

        // Act - assign partitions 0 and 2 only
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 2));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 2) break;
        }

        // Assert - should only get messages from partitions 0 and 2
        await Assert.That(messages).Count().IsEqualTo(2);
        var partitions = messages.Select(m => m.Partition).OrderBy(p => p).ToList();
        int[] expectedPartitions = [0, 2];
        await Assert.That(partitions).IsEquivalentTo(expectedPartitions);
    }

    [Test]
    public async Task MultiPartition_ConsumerGroupSubscription_GetsAllPartitions()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
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

        // Act - subscribe (not manual assign)
        await using var consumer = Kafka.CreateConsumer<string, string>()
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

        // Assert - should get messages from all 3 partitions
        await Assert.That(messages).Count().IsEqualTo(3);
        var partitions = messages.Select(m => m.Partition).Distinct().OrderBy(p => p).ToList();
        int[] expectedPartitions = [0, 1, 2];
        await Assert.That(partitions).IsEquivalentTo(expectedPartitions);
    }

    [Test]
    public async Task MultiPartition_OrderWithinPartition_IsPreserved()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 2);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce ordered messages to partition 0
        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i:D2}",
                Partition = 0
            });
        }

        // Act
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 10) break;
        }

        // Assert - order should be preserved
        await Assert.That(messages).Count().IsEqualTo(10);
        for (var i = 0; i < 10; i++)
        {
            await Assert.That(messages[i].Offset).IsEqualTo(i);
            await Assert.That(messages[i].Value).IsEqualTo($"value-{i:D2}");
        }
    }

    [Test]
    public async Task MultiPartition_SeekOnSpecificPartition_WorksCorrectly()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 2);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce to both partitions
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p0-{i}",
                Value = $"p0-value-{i}",
                Partition = 0
            });
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p1-{i}",
                Value = $"p1-value-{i}",
                Partition = 1
            });
        }

        // Act - assign both but seek partition 0 to offset 3
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 1));
        consumer.Seek(new TopicPartitionOffset(topic, 0, 3));

        // Consume from partition 0
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (msg.Partition == 0)
            {
                messages.Add(msg);
                if (messages.Count >= 2) break;
            }
        }

        // Assert - partition 0 should start from offset 3
        await Assert.That(messages[0].Partition).IsEqualTo(0);
        await Assert.That(messages[0].Offset).IsEqualTo(3);
    }

    [Test]
    public async Task MultiPartition_HighPartitionCount_WorksCorrectly()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 10);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce to all 10 partitions
        for (var p = 0; p < 10; p++)
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
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 10) break;
        }

        // Assert - should get all 10 messages from all partitions
        await Assert.That(messages).Count().IsEqualTo(10);
        var partitions = messages.Select(m => m.Partition).Distinct().OrderBy(p => p).ToList();
        await Assert.That(partitions).IsEquivalentTo(Enumerable.Range(0, 10));
    }

    [Test]
    public async Task MultiPartition_ProduceAndConsumeWithKeys_PartitioningIsConsistent()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 5);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        // Produce same key multiple times
        var expectedPartition = -1;
        for (var i = 0; i < 5; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "consistent-key",
                Value = $"value-{i}"
            });

            if (expectedPartition < 0)
            {
                expectedPartition = metadata.Partition;
            }
            else
            {
                // All should go to same partition
                await Assert.That(metadata.Partition).IsEqualTo(expectedPartition);
            }
        }

        // Act - consume
        await using var consumer = Kafka.CreateConsumer<string, string>()
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
            if (messages.Count >= 5) break;
        }

        // Assert - all should be from same partition
        await Assert.That(messages).Count().IsEqualTo(5);
        foreach (var msg in messages)
        {
            await Assert.That(msg.Partition).IsEqualTo(expectedPartition);
            await Assert.That(msg.Key).IsEqualTo("consistent-key");
        }
    }
}
