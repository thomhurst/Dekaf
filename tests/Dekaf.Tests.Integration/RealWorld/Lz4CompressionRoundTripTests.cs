using Dekaf.Compression.Lz4;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for producing with LZ4 compression and consuming, verifying data integrity.
/// Codec registration is handled by <see cref="GlobalTestSetup"/>.
/// </summary>
public sealed class Lz4CompressionRoundTripTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Lz4_SingleMessage_RoundTripPreservesData()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseLz4Compression()
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "lz4-key",
            Value = "lz4-compressed-value"
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"lz4-test-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("lz4-key");
        await Assert.That(result.Value.Value).IsEqualTo("lz4-compressed-value");
    }

    [Test]
    public async Task Lz4_BatchOfMessages_AllPreserved()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 50;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseLz4Compression()
            .WithLinger(TimeSpan.FromMilliseconds(10))
            .BuildAsync();

        var pendingTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            pendingTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"batch-key-{i}",
                Value = $"batch-compressed-value-{i}-{new string('x', 100)}"
            }));
        }

        foreach (var task in pendingTasks)
        {
            await task;
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"lz4-batch-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        var messagesByKey = messages.ToDictionary(m => m.Key!, m => m.Value);
        for (var i = 0; i < messageCount; i++)
        {
            var key = $"batch-key-{i}";
            await Assert.That(messagesByKey.ContainsKey(key)).IsTrue();
            await Assert.That(messagesByKey[key]).StartsWith($"batch-compressed-value-{i}-");
        }
    }

    [Test]
    public async Task Lz4_LargeMessage_CompressesAndDecompresses()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var largeValue = new string('A', 100_000);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseLz4Compression()
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "large-compressed",
            Value = largeValue
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"lz4-large-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo(largeValue);
        await Assert.That(result.Value.Value.Length).IsEqualTo(100_000);
    }

    [Test]
    public async Task Lz4_MixedMessageSizes_AllDecompressCorrectly()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseLz4Compression()
            .WithLinger(TimeSpan.FromMilliseconds(50))
            .BuildAsync();

        var expectedMessages = new Dictionary<string, string>
        {
            ["tiny"] = "x",
            ["small"] = new('a', 100),
            ["medium"] = new('b', 1_000),
            ["large"] = new('c', 10_000),
            ["empty"] = string.Empty
        };

        var tasks = new List<ValueTask<RecordMetadata>>();
        foreach (var (key, value) in expectedMessages)
        {
            tasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = key,
                Value = value
            }));
        }

        foreach (var task in tasks)
        {
            await task;
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"lz4-mixed-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= expectedMessages.Count) break;
        }

        await Assert.That(messages).Count().IsEqualTo(expectedMessages.Count);

        foreach (var msg in messages)
        {
            await Assert.That(expectedMessages.ContainsKey(msg.Key!)).IsTrue();
            await Assert.That(msg.Value).IsEqualTo(expectedMessages[msg.Key!]);
        }
    }

    [Test]
    public async Task Lz4_NullKeyMessages_CompressAndDecompress()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseLz4Compression()
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = null,
            Value = "null-key-compressed"
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"lz4-null-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsNull();
        await Assert.That(result.Value.Value).IsEqualTo("null-key-compressed");
    }

    [Test]
    public async Task Lz4_WithHeaders_HeadersPreservedWithCompression()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseLz4Compression()
            .BuildAsync();

        var headers = new Headers
        {
            { "content-type", "application/json" },
            { "encoding", "lz4" },
            { "trace-id", Guid.NewGuid().ToString() }
        };

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "with-headers",
            Value = "{\"data\":\"compressed-with-headers\"}",
            Headers = headers
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"lz4-headers-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Headers).IsNotNull();
        await Assert.That(result.Value.Headers!.Count).IsEqualTo(3);

        var contentType = result.Value.Headers.First(h => h.Key == "content-type");
        await Assert.That(contentType.GetValueAsString()).IsEqualTo("application/json");
    }

    [Test]
    public async Task Lz4_CrossClient_ProduceAndConsumeCompatibility()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 10;

        // Produce messages with one producer instance
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("lz4-producer-client")
            .UseLz4Compression()
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"cross-key-{i}",
                Value = $"cross-value-{i}"
            });
        }

        // Consume messages with a separate consumer instance
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("lz4-consumer-client")
            .WithGroupId($"lz4-cross-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        var messagesByKey = messages.ToDictionary(m => m.Key!, m => m.Value);
        for (var i = 0; i < messageCount; i++)
        {
            var key = $"cross-key-{i}";
            await Assert.That(messagesByKey.ContainsKey(key)).IsTrue();
            await Assert.That(messagesByKey[key]).IsEqualTo($"cross-value-{i}");
        }
    }
}
