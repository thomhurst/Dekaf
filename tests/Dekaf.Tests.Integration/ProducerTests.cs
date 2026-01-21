using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for the Kafka producer.
/// </summary>
[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class ProducerTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task Producer_ProduceWithAcksAll_SuccessfullyProduces()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-acks-all")
            .WithAcks(Acks.All)
            .Build();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceWithAcksOne_SuccessfullyProduces()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-acks-one")
            .WithAcks(Acks.Leader)
            .Build();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceWithAcksNone_SuccessfullyProduces()
    {
        // With acks=0, Kafka doesn't send a response, so this is fire-and-forget.
        // The producer returns immediately without waiting for broker acknowledgment.
        // Offset will be -1 since we don't receive it from the broker.

        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-acks-none")
            .WithAcks(Acks.None)
            .Build();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Assert - topic and partition are known, but offset is -1 for fire-and-forget
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsEqualTo(-1); // Unknown for acks=0

        // Verify the message was actually produced by consuming it
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-verify")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            await Assert.That(msg.Key).IsEqualTo("key1");
            await Assert.That(msg.Value).IsEqualTo("value1");
            break;
        }
    }

    [Test]
    public async Task Producer_ProduceWithNullKey_SuccessfullyProduces()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-null-key")
            .Build();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = null,
            Value = "value-without-key"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceWithHeaders_SuccessfullyProduces()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-headers")
            .Build();

        var headers = new Headers
        {
            { "header1", "value1"u8.ToArray() },
            { "header2", "value2"u8.ToArray() }
        };

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1",
            Headers = headers
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceToSpecificPartition_SuccessfullyProduces()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-partition")
            .Build();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1",
            Partition = 1
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsEqualTo(1);
    }

    [Test]
    public async Task Producer_ProduceMultipleMessages_AllSucceed()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        const int messageCount = 10;

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-batch")
            .Build();

        // Act
        var tasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            tasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in tasks)
        {
            results.Add(await task);
        }

        // Assert
        await Assert.That(results).Count().IsEqualTo(messageCount);
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task Producer_ProduceWithCustomTimestamp_SuccessfullyProduces()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var timestamp = DateTimeOffset.UtcNow.AddHours(-1);

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-timestamp")
            .Build();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1",
            Timestamp = timestamp
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceLargeMessage_SuccessfullyProduces()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var largeValue = new string('x', 100_000); // 100KB message

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-large")
            .Build();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "large-key",
            Value = largeValue
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceEmptyValue_SuccessfullyProduces()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-empty")
            .Build();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = string.Empty
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_Flush_WaitsForPendingMessages()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-flush")
            .WithLingerMs(1000) // Long linger to test flush
            .Build();

        // Act - produce without awaiting
        var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Flush should complete the pending produce
        await producer.FlushAsync();
        var metadata = await produceTask;

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_WithStickyPartitioner_DistributesMessages()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-sticky")
            .WithPartitioner(PartitionerType.Sticky)
            .Build();

        // Act - produce messages without keys (should use sticky partitioner)
        var tasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = null,
                Value = $"value-{i}"
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in tasks)
        {
            results.Add(await task);
        }

        // Assert - all messages should go to same partition (sticky)
        await Assert.That(results).Count().IsEqualTo(10);
        var partitions = results.Select(r => r.Partition).Distinct().ToList();
        // With sticky partitioner and quick produces, should mostly go to same partition
        await Assert.That(partitions.Count).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task Producer_WithRoundRobinPartitioner_DistributesMessages()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-roundrobin")
            .WithPartitioner(PartitionerType.RoundRobin)
            .Build();

        // Act - produce messages without keys
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 9; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = null,
                Value = $"value-{i}"
            });
            results.Add(metadata);
        }

        // Assert - should distribute across partitions
        var partitionCounts = results.GroupBy(r => r.Partition).ToDictionary(g => g.Key, g => g.Count());
        await Assert.That(partitionCounts.Count).IsEqualTo(3);
        // Each partition should have 3 messages
        foreach (var count in partitionCounts.Values)
        {
            await Assert.That(count).IsEqualTo(3);
        }
    }
}
