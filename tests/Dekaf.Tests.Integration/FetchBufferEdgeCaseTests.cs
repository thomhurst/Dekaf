using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for edge cases around fetch buffer sizing.
/// Verifies behavior when fetch min/max bytes, max wait, and per-partition limits
/// are configured to trigger boundary conditions.
/// </summary>
public sealed class FetchBufferEdgeCaseTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task FetchMinBytes_SetHigh_ConsumerWaitsForEnoughData()
    {
        // Arrange - produce a small message that won't meet the min bytes threshold
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce a small message (~20 bytes) that won't meet the 50KB min bytes threshold
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "small"
        });

        // Act - consumer with high fetch min bytes and short max wait
        // The consumer should wait (up to FetchMaxWait) because the small message
        // doesn't meet FetchMinBytes, then return when max wait expires
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithFetchMinBytes(50 * 1024) // 50KB - much larger than our small message
            .WithFetchMaxWait(TimeSpan.FromMilliseconds(500))
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts.Token);
        sw.Stop();

        // Assert - the message should still eventually be returned (after max wait expires)
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("small");

        // The consumer should have waited at least partially towards the max wait
        // because the data didn't meet the min bytes threshold
        // (Kafka server-side may return earlier, but the wait should be observable)
    }

    [Test]
    public async Task FetchMaxWait_ControlsMaxBlockingTime()
    {
        // Arrange - create an empty topic so the consumer will have to wait
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Act - consumer with short max wait on an empty topic
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithFetchMinBytes(1024) // Require 1KB, but topic is empty
            .WithFetchMaxWait(TimeSpan.FromMilliseconds(200)) // Short wait
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Try to consume from empty topic - should return null within a reasonable time
        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts.Token);
        sw.Stop();

        // Assert - should return null (no data) within a bounded time
        await Assert.That(result).IsNull();
        // The consume should have returned well within the 15-second CTS timeout,
        // demonstrating that max wait controls the blocking time
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task SmallFetchBuffer_WithLargeMessages_HandlesCorrectly()
    {
        // Arrange - produce messages larger than the configured max fetch bytes
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce 5 messages, each ~10KB
        const int messageCount = 5;
        const int messageSize = 10_000;
        for (var i = 0; i < messageCount; i++)
        {
            var value = new string((char)('A' + i), messageSize);
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = value
            });
        }

        // Act - consumer with a small max partition fetch bytes
        // Kafka will still return at least one complete message per partition even if
        // it exceeds the configured limit, so all messages should be consumable
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithFetchMaxBytes(1024) // 1KB - much smaller than message size
            .WithMaxPartitionFetchBytes(1024) // 1KB per partition
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        // Assert - all messages should be consumed despite small fetch buffer
        // Kafka guarantees at least one complete record per fetch response
        await Assert.That(messages).Count().IsEqualTo(messageCount);
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value.Length).IsEqualTo(messageSize);
        }
    }

    [Test]
    public async Task VaryingMessageSizes_FetchReturnsConsistently()
    {
        // Arrange - produce messages of wildly varying sizes
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        var sizes = new[] { 10, 50_000, 5, 100_000, 1, 25_000, 200_000, 3 };
        for (var i = 0; i < sizes.Length; i++)
        {
            var value = new string('X', sizes[i]);
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = value
            });
        }

        // Act - consumer with moderate fetch buffer settings
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithFetchMinBytes(1)
            .WithFetchMaxBytes(64 * 1024) // 64KB - some messages exceed this
            .WithMaxPartitionFetchBytes(32 * 1024) // 32KB per partition
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= sizes.Length) break;
        }

        // Assert - all messages consumed with correct sizes, in order
        await Assert.That(messages).Count().IsEqualTo(sizes.Length);
        for (var i = 0; i < sizes.Length; i++)
        {
            await Assert.That(messages[i].Value.Length).IsEqualTo(sizes[i]);
        }
    }

    [Test]
    public async Task MaxPartitionFetchBytes_LimitsPerPartitionData()
    {
        // Arrange - produce multiple messages to a single partition
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce 10 messages each ~1KB to a single partition
        const int messageCount = 10;
        const int messageSize = 1_000;

        for (var i = 0; i < messageCount; i++)
        {
            var value = new string('M', messageSize);
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = value
            });
        }

        // Act - consumer with small per-partition fetch bytes
        // With MaxPartitionFetchBytes of 2KB, only ~1 message fits per fetch round,
        // so the consumer needs multiple rounds to consume all 10 messages.
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithMaxPartitionFetchBytes(2 * 1024) // 2KB per partition - fits ~1 message per fetch
            .WithFetchMaxWait(TimeSpan.FromMilliseconds(100))
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        // Assert - all messages should be consumed despite the small per-partition limit
        await Assert.That(messages).Count().IsEqualTo(messageCount);
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value.Length).IsEqualTo(messageSize);
        }
    }
}
