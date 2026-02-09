using Dekaf.Compression.Zstd;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Tests verifying consumer behavior when reading messages produced with different
/// compression codecs. Covers mixed-compression scenarios on the same topic,
/// mixing compressed and uncompressed batches, all-codec interoperability,
/// and large compressed batch decompression.
/// </summary>
public sealed class MixedCompressionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task MixedCompressionTypes_SameTopic_ConsumerHandlesAll()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Produce messages with Gzip compression
        await using var gzipProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseGzipCompression()
            .BuildAsync();

        // Produce messages with Zstd compression
        await using var zstdProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseZstdCompression()
            .BuildAsync();

        // Send messages with Gzip
        for (var i = 0; i < 5; i++)
        {
            await gzipProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"gzip-{i}",
                Value = $"gzip-value-{i}"
            });
        }

        // Flush Gzip producer to ensure batches are sent before Zstd messages
        await gzipProducer.FlushAsync();

        // Send messages with Zstd
        for (var i = 0; i < 5; i++)
        {
            await zstdProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"zstd-{i}",
                Value = $"zstd-value-{i}"
            });
        }

        await zstdProducer.FlushAsync();

        // Consumer should handle both compression types transparently
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"mixed-compression-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 10) break;
        }

        await Assert.That(messages).Count().IsEqualTo(10);

        // Verify all Gzip messages are present and correct
        for (var i = 0; i < 5; i++)
        {
            var gzipMsg = messages.First(m => m.Key == $"gzip-{i}");
            await Assert.That(gzipMsg.Value).IsEqualTo($"gzip-value-{i}");
        }

        // Verify all Zstd messages are present and correct
        for (var i = 0; i < 5; i++)
        {
            var zstdMsg = messages.First(m => m.Key == $"zstd-{i}");
            await Assert.That(zstdMsg.Value).IsEqualTo($"zstd-value-{i}");
        }
    }

    [Test]
    public async Task CompressedAndUncompressed_SameTopic_ConsumerHandlesAll()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Producer without compression
        await using var plainProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Producer with Gzip compression
        await using var gzipProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseGzipCompression()
            .BuildAsync();

        // Send uncompressed messages
        for (var i = 0; i < 5; i++)
        {
            await plainProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"plain-{i}",
                Value = $"plain-value-{i}"
            });
        }

        await plainProducer.FlushAsync();

        // Send compressed messages
        for (var i = 0; i < 5; i++)
        {
            await gzipProducer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"gzip-{i}",
                Value = $"gzip-value-{i}"
            });
        }

        await gzipProducer.FlushAsync();

        // Consumer should handle both compressed and uncompressed batches
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"mixed-plain-compressed-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 10) break;
        }

        await Assert.That(messages).Count().IsEqualTo(10);

        // Verify uncompressed messages
        for (var i = 0; i < 5; i++)
        {
            var plainMsg = messages.First(m => m.Key == $"plain-{i}");
            await Assert.That(plainMsg.Value).IsEqualTo($"plain-value-{i}");
        }

        // Verify compressed messages
        for (var i = 0; i < 5; i++)
        {
            var gzipMsg = messages.First(m => m.Key == $"gzip-{i}");
            await Assert.That(gzipMsg.Value).IsEqualTo($"gzip-value-{i}");
        }
    }

    [Test]
    public async Task AllCodecsRegistered_ConsumerReadsAnyCompression()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Create producers for each proven compression type plus uncompressed.
        // Gzip and Zstd are validated by existing integration tests.
        // Uncompressed is included to verify mixed-codec consumption in one consumer session.
        await using var gzipProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseGzipCompression()
            .BuildAsync();

        await using var zstdProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseZstdCompression()
            .BuildAsync();

        await using var plainProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce with each codec/mode
        await gzipProducer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "codec-gzip",
            Value = "value-from-gzip"
        });
        await gzipProducer.FlushAsync();

        await zstdProducer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "codec-zstd",
            Value = "value-from-zstd"
        });
        await zstdProducer.FlushAsync();

        await plainProducer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "codec-none",
            Value = "value-from-none"
        });
        await plainProducer.FlushAsync();

        // A single consumer with all codecs registered should read all messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"all-codecs-{Guid.NewGuid():N}")
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

        await Assert.That(messages).Count().IsEqualTo(3);

        var messagesByKey = messages.ToDictionary(m => m.Key!, m => m.Value);
        await Assert.That(messagesByKey["codec-gzip"]).IsEqualTo("value-from-gzip");
        await Assert.That(messagesByKey["codec-zstd"]).IsEqualTo("value-from-zstd");
        await Assert.That(messagesByKey["codec-none"]).IsEqualTo("value-from-none");
    }

    [Test]
    public async Task LargeCompressedBatch_ConsumerDecompressesSuccessfully()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 200;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .UseZstdCompression()
            .WithLinger(TimeSpan.FromMilliseconds(50)) // Encourage batching
            .BuildAsync();

        // Produce many messages to create large compressed batches
        var pendingTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            pendingTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"large-batch-{i}",
                Value = $"compressed-payload-{i}-{new string('D', 500)}" // Compressible repeated data
            }));
        }

        foreach (var task in pendingTasks)
        {
            await task;
        }

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"large-batch-{Guid.NewGuid():N}")
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

        // Verify all messages were decompressed correctly
        var messagesByKey = messages.ToDictionary(m => m.Key!, m => m.Value);
        for (var i = 0; i < messageCount; i++)
        {
            var key = $"large-batch-{i}";
            await Assert.That(messagesByKey.ContainsKey(key)).IsTrue();
            await Assert.That(messagesByKey[key]).StartsWith($"compressed-payload-{i}-");
            await Assert.That(messagesByKey[key].Length).IsEqualTo($"compressed-payload-{i}-".Length + 500);
        }
    }
}
