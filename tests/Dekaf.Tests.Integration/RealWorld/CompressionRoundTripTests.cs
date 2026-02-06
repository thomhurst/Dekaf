using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for producing with compression and consuming, verifying data integrity.
/// Compression is commonly used in production to reduce network bandwidth and storage costs.
/// </summary>
public sealed class CompressionRoundTripTests(KafkaTestContainer kafka) : KafkaIntegrationTest
{
    [Test]
    public async Task Gzip_SingleMessage_RoundTripPreservesData()
    {
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .UseGzipCompression()
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "gzip-key",
            Value = "gzip-compressed-value"
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"gzip-test-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("gzip-key");
        await Assert.That(result.Value.Value).IsEqualTo("gzip-compressed-value");
    }

    [Test]
    public async Task Gzip_BatchOfMessages_AllPreserved()
    {
        var topic = await kafka.CreateTestTopicAsync();
        const int messageCount = 50;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .UseGzipCompression()
            .WithLinger(TimeSpan.FromMilliseconds(10)) // Encourage batching for better compression
            .Build();

        // Produce many messages to test batch-level compression
        var pendingTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            pendingTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"batch-key-{i}",
                Value = $"batch-compressed-value-{i}-{new string('x', 100)}" // Compressible data
            }));
        }

        foreach (var task in pendingTasks)
        {
            await task;
        }

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"gzip-batch-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            var msg = messages.First(m => m.Key == $"batch-key-{i}");
            await Assert.That(msg.Value).StartsWith($"batch-compressed-value-{i}-");
        }
    }

    [Test]
    public async Task Gzip_LargeMessage_CompressesAndDecompresses()
    {
        var topic = await kafka.CreateTestTopicAsync();
        var largeValue = new string('A', 50_000); // 50KB of highly compressible data

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .UseGzipCompression()
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "large-compressed",
            Value = largeValue
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"gzip-large-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo(largeValue);
        await Assert.That(result.Value.Value.Length).IsEqualTo(50_000);
    }

    [Test]
    public async Task Gzip_WithHeaders_HeadersPreservedWithCompression()
    {
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .UseGzipCompression()
            .Build();

        var headers = new Headers
        {
            { "content-type", "application/json" },
            { "encoding", "gzip" },
            { "trace-id", Guid.NewGuid().ToString() }
        };

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "with-headers",
            Value = "{\"data\":\"compressed-with-headers\"}",
            Headers = headers
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"gzip-headers-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

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
    public async Task Gzip_HighThroughputPreset_CompressesEfficiently()
    {
        // ForHighThroughput preset with explicit compression
        var topic = await kafka.CreateTestTopicAsync(partitions: 3);
        const int messageCount = 100;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .ForHighThroughput()
            .UseGzipCompression()
            .Build();

        var pendingTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            pendingTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"ht-key-{i}",
                Value = $"high-throughput-compressed-message-{i}-payload"
            }));
        }

        foreach (var task in pendingTasks)
        {
            await task;
        }

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"gzip-ht-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);
    }

    [Test]
    public async Task Gzip_MixedMessageSizes_AllDecompressCorrectly()
    {
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .UseGzipCompression()
            .WithLinger(TimeSpan.FromMilliseconds(50)) // Batch them together
            .Build();

        // Produce messages of varying sizes in a single batch
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

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"gzip-mixed-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

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
    public async Task Gzip_NullKeyMessages_CompressAndDecompress()
    {
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .UseGzipCompression()
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = null,
            Value = "null-key-compressed"
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId($"gzip-null-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsNull();
        await Assert.That(result.Value.Value).IsEqualTo("null-key-compressed");
    }
}
