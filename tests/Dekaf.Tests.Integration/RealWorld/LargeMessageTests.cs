using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for edge cases around message size, headers, partition count, and unicode content.
/// Verifies that large headers, many small messages, multi-partition distribution, and unicode
/// all round-trip correctly through produce/consume.
/// </summary>
[Category("Resilience")]
public sealed class LargeMessageTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task RoundTrip_MessageWith50Headers_AllHeadersPreserved()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-50-headers")
            .BuildAsync();

        var headers = new Headers();
        for (var i = 0; i < 50; i++)
        {
            headers.Add($"header-{i:D2}", Encoding.UTF8.GetBytes($"header-value-{i:D2}"));
        }

        // Act
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        }).ConfigureAwait(false);

        // Consume back
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-50-headers")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Headers).IsNotNull();
        await Assert.That(result.Value.Headers!.Count).IsEqualTo(50);

        // Verify each header
        for (var i = 0; i < 50; i++)
        {
            var header = result.Value.Headers.FirstOrDefault(h => h.Key == $"header-{i:D2}");
            await Assert.That(header.Key).IsNotNull();
            await Assert.That(Encoding.UTF8.GetString(header.Value.Span)).IsEqualTo($"header-value-{i:D2}");
        }
    }

    [Test]
    public async Task RoundTrip_HeaderWithLargeValue_PreservedCorrectly()
    {
        // Arrange - header with 10KB value
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var largeHeaderValue = new string('H', 10_000); // 10KB

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-large-header")
            .BuildAsync();

        var headers = new Headers
        {
            { "large-header", Encoding.UTF8.GetBytes(largeHeaderValue) }
        };

        // Act
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        }).ConfigureAwait(false);

        // Consume
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-large-header")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Headers).IsNotNull();
        var header = result.Value.Headers!.First(h => h.Key == "large-header");
        await Assert.That(Encoding.UTF8.GetString(header.Value.Span)).IsEqualTo(largeHeaderValue);
    }

    [Test]
    public async Task RoundTrip_ManySmallMessages_AllDelivered()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3).ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        const int messageCount = 2_000;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-10k")
            .WithAcks(Acks.Leader)
            .WithLinger(TimeSpan.FromMilliseconds(10))
            .BuildAsync();

        // Act - produce 10K small messages using fire-and-forget for speed
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(topic, $"k{i}", $"v{i}");
        }

        await producer.FlushAsync().ConfigureAwait(false);

        // Consume all back
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-10k")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumedCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            consumedCount++;
            if (consumedCount >= messageCount) break;
        }

        // Assert
        await Assert.That(consumedCount).IsEqualTo(messageCount);
    }

    [Test]
    public async Task MultiPartition_20Partitions_AllPartitionsReceiveMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 20).ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-20-partitions")
            .BuildAsync();

        // Produce to each partition explicitly
        for (var partition = 0; partition < 20; partition++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p{partition}",
                Value = $"value-p{partition}",
                Partition = partition
            }).ConfigureAwait(false);
        }

        // Act - consume all
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-20-partitions")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var partitionsSeen = new HashSet<int>();
        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            consumed.Add(msg);
            partitionsSeen.Add(msg.Partition);
            if (consumed.Count >= 20) break;
        }

        // Assert - all 20 partitions should have received a message
        await Assert.That(consumed).Count().IsEqualTo(20);
        await Assert.That(partitionsSeen.Count).IsEqualTo(20);
    }

    [Test]
    public async Task RoundTrip_MessageWithUnicodeContent_PreservedCorrectly()
    {
        // Arrange - unicode in keys, values, and headers
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var unicodeKey = "键-Ключ-キー-مفتاح-열쇠";
        var unicodeValue = "值-Значение-値-قيمة-값-🎉🚀💡🌍🎶";
        var unicodeHeaderValue = "Заголовок-ヘッダー-رأس-헤더";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-unicode")
            .BuildAsync();

        var headers = new Headers
        {
            { "unicode-header", Encoding.UTF8.GetBytes(unicodeHeaderValue) }
        };

        // Act
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = unicodeKey,
            Value = unicodeValue,
            Headers = headers
        }).ConfigureAwait(false);

        // Consume
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-unicode")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        // Assert
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Key).IsEqualTo(unicodeKey);
        await Assert.That(r.Value).IsEqualTo(unicodeValue);
        await Assert.That(r.Headers).IsNotNull();

        var header = r.Headers!.First(h => h.Key == "unicode-header");
        await Assert.That(Encoding.UTF8.GetString(header.Value.Span)).IsEqualTo(unicodeHeaderValue);
    }
}
