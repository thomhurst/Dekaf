using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for multi-partition scenarios.
/// </summary>
[Category("Messaging")]
[ParallelLimiter<MessagingTestLimit>]
public class MultiPartitionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    /// <summary>
    /// Produces a warmup message to each partition to ensure the broker has fully initialized
    /// the partition and its producer state tracking.
    /// </summary>
    private static async Task WarmUpAllPartitions(
        IKafkaProducer<string, string> producer, string topic, int partitions,
        CancellationToken cancellationToken = default)
    {
        for (var p = 0; p < partitions; p++)
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = "warmup", Value = "warmup", Partition = p
            }, cancellationToken);
    }

    [Test]
    public async Task MultiPartition_KeyBasedPartitioning_SameKeyGoesToSamePartition()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 5);
        using var produceCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await WarmUpAllPartitions(producer, topic, 5, produceCts.Token);

        // Act - produce multiple messages with same key
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 10; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "consistent-key",
                Value = $"value-{i}"
            }, produceCts.Token);
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
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 5);
        using var produceCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await WarmUpAllPartitions(producer, topic, 5, produceCts.Token);

        // Act - produce messages with different keys
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 50; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"different-key-{i}",
                Value = $"value-{i}"
            }, produceCts.Token);
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
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        using var produceCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await WarmUpAllPartitions(producer, topic, 3, produceCts.Token);

        // Produce to all partitions
        for (var p = 0; p < 3; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-partition-{p}",
                Partition = p
            }, produceCts.Token);
        }

        // Act - manually assign only partition 1
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 1));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 2) break; // 1 warmup + 1 actual
        }

        // Assert - filter out warmup
        var actual = messages.Where(m => m.Key != "warmup").ToList();
        await Assert.That(actual).Count().IsEqualTo(1);
        var r = actual[0];
        await Assert.That(r.Partition).IsEqualTo(1);
        await Assert.That(r.Value).IsEqualTo("value-partition-1");
    }

    [Test]
    public async Task MultiPartition_AssignMultiplePartitions_ConsumesFromAll()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        using var produceCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await WarmUpAllPartitions(producer, topic, 4, produceCts.Token);

        // Produce to partitions 0, 1, and 2
        for (var p = 0; p < 3; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-p{p}",
                Partition = p
            }, produceCts.Token);
        }

        // Act - assign partitions 0 and 2 only
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 2));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 4) break; // 2 warmup + 2 actual
        }

        // Assert - should only get messages from partitions 0 and 2 (filter out warmup)
        var actual = messages.Where(m => m.Key != "warmup").ToList();
        await Assert.That(actual).Count().IsEqualTo(2);
        var partitions = actual.Select(m => m.Partition).OrderBy(p => p).ToList();
        int[] expectedPartitions = [0, 2];
        await Assert.That(partitions).IsEquivalentTo(expectedPartitions);
    }

    [Test]
    public async Task MultiPartition_ConsumerGroupSubscription_GetsAllPartitions()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        using var produceCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await WarmUpAllPartitions(producer, topic, 3, produceCts.Token);

        // Produce to all partitions
        for (var p = 0; p < 3; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            }, produceCts.Token);
        }

        // Act - subscribe (not manual assign)
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
            if (messages.Count >= 6) break; // 3 warmup + 3 actual
        }

        // Assert - should get messages from all 3 partitions (filter out warmup)
        var actual = messages.Where(m => m.Key != "warmup").ToList();
        await Assert.That(actual).Count().IsEqualTo(3);
        var partitions = actual.Select(m => m.Partition).Distinct().OrderBy(p => p).ToList();
        int[] expectedPartitions = [0, 1, 2];
        await Assert.That(partitions).IsEquivalentTo(expectedPartitions);
    }

    [Test]
    public async Task MultiPartition_OrderWithinPartition_IsPreserved()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        using var produceCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await WarmUpAllPartitions(producer, topic, 2, produceCts.Token);

        // Produce ordered messages to partition 0
        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i:D2}",
                Partition = 0
            }, produceCts.Token);
        }

        // Act
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 11) break; // 1 warmup + 10 actual
        }

        // Assert - order should be preserved (filter out warmup)
        var actual = messages.Where(m => m.Key != "warmup").ToList();
        await Assert.That(actual).Count().IsEqualTo(10);
        for (var i = 0; i < 10; i++)
        {
            await Assert.That(actual[i].Value).IsEqualTo($"value-{i:D2}");
        }
    }

    [Test]
    public async Task MultiPartition_SeekOnSpecificPartition_WorksCorrectly()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        using var produceCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await WarmUpAllPartitions(producer, topic, 2, produceCts.Token);

        // Produce to both partitions
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p0-{i}",
                Value = $"p0-value-{i}",
                Partition = 0
            }, produceCts.Token);
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p1-{i}",
                Value = $"p1-value-{i}",
                Partition = 1
            }, produceCts.Token);
        }

        // Act - assign both but seek partition 0 to offset 3
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 1));
        consumer.Seek(new TopicPartitionOffset(topic, 0, 4)); // offset 0 = warmup, 1-5 = actual; seek past warmup+3

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

        // Assert - partition 0 should start from offset 4 (after warmup + 3 actual)
        await Assert.That(messages[0].Partition).IsEqualTo(0);
        await Assert.That(messages[0].Offset).IsEqualTo(4);
    }

    [Test]
    public async Task MultiPartition_HighPartitionCount_WorksCorrectly()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 10);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        using var produceCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await WarmUpAllPartitions(producer, topic, 10, produceCts.Token);

        // Produce to all 10 partitions
        for (var p = 0; p < 10; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            }, produceCts.Token);
        }

        // Act
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 20) break; // 10 warmup + 10 actual
        }

        // Assert - should get all 10 messages from all partitions (filter out warmup)
        var actual = messages.Where(m => m.Key != "warmup").ToList();
        await Assert.That(actual).Count().IsEqualTo(10);
        var partitions = actual.Select(m => m.Partition).Distinct().OrderBy(p => p).ToList();
        await Assert.That(partitions).IsEquivalentTo(Enumerable.Range(0, 10));
    }

    [Test]
    public async Task MultiPartition_ProduceAndConsumeWithKeys_PartitioningIsConsistent()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 5);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        using var produceCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await WarmUpAllPartitions(producer, topic, 5, produceCts.Token);

        // Produce same key multiple times
        var expectedPartition = -1;
        for (var i = 0; i < 5; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "consistent-key",
                Value = $"value-{i}"
            }, produceCts.Token);

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
            if (messages.Count >= 10) break; // 5 warmup + 5 actual
        }

        // Assert - all consistent-key messages should be from same partition
        var actual = messages.Where(m => m.Key == "consistent-key").ToList();
        await Assert.That(actual).Count().IsEqualTo(5);
        foreach (var msg in actual)
        {
            await Assert.That(msg.Partition).IsEqualTo(expectedPartition);
            await Assert.That(msg.Key).IsEqualTo("consistent-key");
        }
    }
}
