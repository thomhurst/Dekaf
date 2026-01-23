using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// End-to-end round-trip tests validating produce and consume scenarios.
/// </summary>
[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class RoundTripTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task RoundTrip_SimpleMessage_PreservesData()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Act
        var producedMetadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "round-trip-key",
            Value = "round-trip-value"
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(consumed).IsNotNull();
        var c = consumed!.Value;
        await Assert.That(c.Topic).IsEqualTo(topic);
        await Assert.That(c.Key).IsEqualTo("round-trip-key");
        await Assert.That(c.Value).IsEqualTo("round-trip-value");
        await Assert.That(c.Partition).IsEqualTo(producedMetadata.Partition);
        await Assert.That(c.Offset).IsEqualTo(producedMetadata.Offset);
    }

    [Test]
    public async Task RoundTrip_MessageWithHeaders_PreservesHeaders()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var headers = new Headers
        {
            { "correlationId", "abc123"u8.ToArray() },
            { "source", "test"u8.ToArray() },
            { "timestamp", "2024-01-01T00:00:00Z"u8.ToArray() }
        };

        // Act
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(consumed).IsNotNull();
        var c = consumed!.Value;
        await Assert.That(c.Headers).IsNotNull();
        await Assert.That(c.Headers!.Count).IsEqualTo(3);

        var correlationId = c.Headers.FirstOrDefault(h => h.Key == "correlationId");
        await Assert.That(correlationId.Key).IsNotNull();
        await Assert.That(System.Text.Encoding.UTF8.GetString(correlationId.Value.Span)).IsEqualTo("abc123");
    }

    [Test]
    public async Task RoundTrip_NullKey_Handled()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Act
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = null,
            Value = "value-with-null-key"
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(consumed).IsNotNull();
        var c = consumed!.Value;
        await Assert.That(c.Key).IsNull();
        await Assert.That(c.Value).IsEqualTo("value-with-null-key");
    }

    [Test]
    public async Task RoundTrip_EmptyValue_Handled()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Act
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = string.Empty
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(consumed).IsNotNull();
        var c = consumed!.Value;
        await Assert.That(c.Key).IsEqualTo("key");
        await Assert.That(c.Value).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task RoundTrip_LargeMessage_PreservesData()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var largeValue = new string('A', 50_000); // 50KB

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Act
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "large-key",
            Value = largeValue
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(consumed).IsNotNull();
        var c = consumed!.Value;
        await Assert.That(c.Value).IsEqualTo(largeValue);
        await Assert.That(c.Value.Length).IsEqualTo(50_000);
    }

    [Test]
    public async Task RoundTrip_MultipleMessages_PreservesOrder()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        const int messageCount = 20;

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Act - produce messages in order
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i:D3}",
                Value = $"value-{i:D3}"
            });
        }

        // Consume all messages
        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= messageCount) break;
        }

        // Assert - order should be preserved within partition
        await Assert.That(consumed).Count().IsEqualTo(messageCount);
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(consumed[i].Offset).IsEqualTo(i);
            await Assert.That(consumed[i].Value).IsEqualTo($"value-{i:D3}");
        }
    }

    [Test]
    public async Task RoundTrip_UnicodeContent_PreservesData()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var unicodeKey = "键-Ключ-キー-مفتاح";
        var unicodeValue = "值-Значение-値-قيمة-🎉🚀💡";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Act
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = unicodeKey,
            Value = unicodeValue
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(consumed).IsNotNull();
        var c = consumed!.Value;
        await Assert.That(c.Key).IsEqualTo(unicodeKey);
        await Assert.That(c.Value).IsEqualTo(unicodeValue);
    }

    [Test]
    public async Task RoundTrip_SpecificPartition_WorksCorrectly()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Act - produce to specific partition
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value-partition-2",
            Partition = 2
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(consumed).IsNotNull();
        var c = consumed!.Value;
        await Assert.That(c.Partition).IsEqualTo(2);
        await Assert.That(c.Value).IsEqualTo("value-partition-2");
        await Assert.That(metadata.Partition).IsEqualTo(2);
    }

    [Test]
    public async Task RoundTrip_Timestamp_IsPreserved()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow;

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Act
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Timestamp = timestamp
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert - timestamp should be close (within 1 second due to millisecond precision)
        await Assert.That(consumed).IsNotNull();
        var diff = Math.Abs((consumed!.Value.Timestamp - timestamp).TotalMilliseconds);
        await Assert.That(diff).IsLessThan(1000);
    }

    [Test]
    public async Task RoundTrip_ConcurrentProducers_AllMessagesConsumed()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        const int producerCount = 3;
        const int messagesPerProducer = 5;

        var producers = new List<IKafkaProducer<string, string>>();
        for (var i = 0; i < producerCount; i++)
        {
            producers.Add(Dekaf.CreateProducer<string, string>()
                .WithBootstrapServers(kafka.BootstrapServers)
                .WithClientId($"test-producer-{i}")
                .Build());
        }

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Act - produce from all producers concurrently
        var produceTasks = new List<Task>();
        for (var p = 0; p < producerCount; p++)
        {
            var producerId = p;
            produceTasks.Add(Task.Run(async () =>
            {
                for (var m = 0; m < messagesPerProducer; m++)
                {
                    await producers[producerId].ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"producer-{producerId}-msg-{m}",
                        Value = $"value-{producerId}-{m}"
                    });
                }
            }));
        }

        await Task.WhenAll(produceTasks);

        // Consume all messages
        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= producerCount * messagesPerProducer) break;
        }

        // Cleanup producers
        foreach (var producer in producers)
        {
            await producer.DisposeAsync();
        }

        // Assert
        await Assert.That(consumed).Count().IsEqualTo(producerCount * messagesPerProducer);
    }

    [Test]
    public async Task RoundTrip_ByteSerializer_WorksCorrectly()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var keyBytes = new byte[] { 1, 2, 3, 4, 5 };
        var valueBytes = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

        await using var producer = Dekaf.CreateProducer<byte[], byte[]>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await using var consumer = Dekaf.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        // Act
        await producer.ProduceAsync(new ProducerMessage<byte[], byte[]>
        {
            Topic = topic,
            Key = keyBytes,
            Value = valueBytes
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(consumed).IsNotNull();
        var c = consumed!.Value;
        await Assert.That(c.Key).IsEquivalentTo(keyBytes);
        await Assert.That(c.Value).IsEquivalentTo(valueBytes);
    }
}
