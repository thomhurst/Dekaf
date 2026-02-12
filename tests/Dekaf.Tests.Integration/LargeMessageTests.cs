using System.Text;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for large message and payload edge cases.
/// Closes #212
/// </summary>
[Category("Resilience")]
public class LargeMessageTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProduceMessage_NearMaxMessageBytes_Succeeds()
    {
        // Kafka default message.max.bytes is 1048588 (1MB + 12 bytes overhead).
        // The wire format adds overhead for record batch headers, record headers,
        // key/value length prefixes, timestamps, etc. Use a conservatively sized
        // payload (~800KB) that is large but safely under the limit.
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // ~800KB value - large but safely under the 1MB default limit
        var largeValue = new string('A', 800_000);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-near-max")
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "large-key",
            Value = largeValue
        });

        // Assert - message produced successfully
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify round-trip by consuming
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo(largeValue);
        await Assert.That(result.Value.Key).IsEqualTo("large-key");
    }

    [Test]
    public async Task ProduceMessage_ExceedingMaxMessageBytes_FailsWithError()
    {
        // Create a topic with a small max.message.bytes to make the test fast and reliable.
        // The admin client supports topic-level configs.
        var topicName = $"test-topic-small-max-{Guid.NewGuid():N}";

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await adminClient.CreateTopicsAsync([
            new Admin.NewTopic
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string>
                {
                    // Set a small max to 1024 bytes so we can easily exceed it
                    ["max.message.bytes"] = "1024"
                }
            }
        ]);

        // Wait for topic metadata to propagate
        await Task.Delay(3000);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-exceed-max")
            .WithAcks(Acks.All)
            .BuildAsync();

        // Create a message that clearly exceeds the topic's 1024-byte max.message.bytes
        var oversizedValue = new string('X', 2048);

        // Act & Assert - should fail with a KafkaException (MessageTooLarge error code)
        Exception? caughtException = null;
        try
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topicName,
                Key = "key",
                Value = oversizedValue
            });
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        await Assert.That(caughtException).IsNotNull();
        // The producer should throw a KafkaException (or a derived type) when the broker rejects the message
        await Assert.That(caughtException).IsAssignableTo<KafkaException>();
    }

    [Test]
    public async Task ProduceMessage_WithLargeHeaders_RoundTripsCorrectly()
    {
        // Test headers with 64KB+ values
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-large-headers")
            .BuildAsync();

        // Create a header with a 64KB value
        var largeHeaderValue = new byte[65_536]; // 64KB
        Random.Shared.NextBytes(largeHeaderValue);

        // Also add a second large header of 128KB
        var veryLargeHeaderValue = new byte[131_072]; // 128KB
        Random.Shared.NextBytes(veryLargeHeaderValue);

        var headers = new Headers()
            .Add("large-header", largeHeaderValue)
            .Add("very-large-header", veryLargeHeaderValue)
            .Add("normal-header", "small-value");

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        });

        // Consume and verify headers round-trip
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Headers).IsNotNull();
        await Assert.That(result.Value.Headers!.Count).IsEqualTo(3);

        // Verify the 64KB header
        var largeHeader = result.Value.Headers.First(h => h.Key == "large-header");
        await Assert.That(largeHeader.Value.Length).IsEqualTo(65_536);
        await Assert.That(largeHeader.Value.ToArray()).IsEquivalentTo(largeHeaderValue);

        // Verify the 128KB header
        var veryLargeHeader = result.Value.Headers.First(h => h.Key == "very-large-header");
        await Assert.That(veryLargeHeader.Value.Length).IsEqualTo(131_072);
        await Assert.That(veryLargeHeader.Value.ToArray()).IsEquivalentTo(veryLargeHeaderValue);

        // Verify the normal header
        var normalHeader = result.Value.Headers.First(h => h.Key == "normal-header");
        await Assert.That(Encoding.UTF8.GetString(normalHeader.Value.Span)).IsEqualTo("small-value");
    }

    [Test]
    public async Task ProduceMessage_WithManyHeaders_RoundTripsCorrectly()
    {
        // Test 100+ headers per message
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-many-headers")
            .BuildAsync();

        const int headerCount = 150;
        var headers = new Headers(headerCount);
        var expectedValues = new Dictionary<string, string>();

        for (var i = 0; i < headerCount; i++)
        {
            var key = $"header-{i:D3}";
            var value = $"value-{i:D3}-{Guid.NewGuid():N}";
            headers.Add(key, value);
            expectedValues[key] = value;
        }

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value-with-many-headers",
            Headers = headers
        });

        // Consume and verify all headers round-trip
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Headers).IsNotNull();
        await Assert.That(result.Value.Headers!.Count).IsEqualTo(headerCount);
        await Assert.That(result.Value.Value).IsEqualTo("value-with-many-headers");

        // Verify each header value matches
        for (var i = 0; i < headerCount; i++)
        {
            var header = result.Value.Headers[i];
            var expectedKey = $"header-{i:D3}";
            await Assert.That(header.Key).IsEqualTo(expectedKey);

            var actualValue = Encoding.UTF8.GetString(header.Value.Span);
            await Assert.That(actualValue).IsEqualTo(expectedValues[expectedKey]);
        }
    }

    [Test]
    public async Task ProduceMessage_SingleMessageNearBatchSize_SucceedsAsSingleMessageBatch()
    {
        // Test behavior when a single message is close to the batch size limit.
        // The producer should create a single-message batch and send it successfully.
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Use a small batch size (16KB) and produce a message close to that size
        const int batchSize = 16384; // 16KB

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-batch-boundary")
            .WithBatchSize(batchSize)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Create a message that's close to the batch size.
        // The batch has overhead (record batch header ~61 bytes, record header, etc.)
        // so the value should be a bit smaller than batchSize.
        var nearBatchValue = new string('B', batchSize - 200);

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "batch-boundary-key",
            Value = nearBatchValue
        });

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify round-trip
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo(nearBatchValue);
    }

    [Test]
    public async Task ProduceMessage_LargerThanBatchSize_StillDelivered()
    {
        // When a single message is larger than the batch size, the producer should
        // still be able to send it as a single-message batch (the batch expands to fit).
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        const int smallBatchSize = 4096; // 4KB batch size

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-exceeds-batch")
            .WithBatchSize(smallBatchSize)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Create a message that's 3x the batch size
        var largeValue = new string('C', smallBatchSize * 3);

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "exceeds-batch-key",
            Value = largeValue
        });

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify round-trip
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo(largeValue);
        await Assert.That(result.Value.Value.Length).IsEqualTo(smallBatchSize * 3);
    }

    [Test]
    public async Task ProduceMessage_LargeKeyAndValue_RoundTripsCorrectly()
    {
        // Test with both a large key and a large value
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-large-key-value")
            .BuildAsync();

        // 50KB key and 200KB value
        var largeKey = new string('K', 50_000);
        var largeValue = new string('V', 200_000);

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = largeKey,
            Value = largeValue
        });

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify round-trip
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo(largeKey);
        await Assert.That(result.Value.Key!.Length).IsEqualTo(50_000);
        await Assert.That(result.Value.Value).IsEqualTo(largeValue);
        await Assert.That(result.Value.Value.Length).IsEqualTo(200_000);
    }

    [Test]
    public async Task ProduceMessage_LargeValueWithLargeHeaders_CombinedSizeRoundTrips()
    {
        // Test a message with both a large value and large headers that together
        // approach the default Kafka limit
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-combined-large")
            .BuildAsync();

        // 400KB value
        var largeValue = new string('D', 400_000);

        // Add headers totaling ~200KB
        var headers = new Headers();
        for (var i = 0; i < 4; i++)
        {
            var headerValue = new byte[50_000]; // 50KB each = 200KB total
            Random.Shared.NextBytes(headerValue);
            headers.Add($"bulk-header-{i}", headerValue);
        }

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "combined-key",
            Value = largeValue,
            Headers = headers
        });

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify round-trip
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo(largeValue);
        await Assert.That(result.Value.Headers).IsNotNull();
        await Assert.That(result.Value.Headers!.Count).IsEqualTo(4);

        for (var i = 0; i < 4; i++)
        {
            var header = result.Value.Headers[i];
            await Assert.That(header.Key).IsEqualTo($"bulk-header-{i}");
            await Assert.That(header.Value.Length).IsEqualTo(50_000);
        }
    }

    [Test]
    public async Task ProduceMessage_MultipleSmallBatches_WithLargeMessages_AllDelivered()
    {
        // Test that multiple large messages, each forming its own batch due to a small
        // batch size, are all delivered correctly
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        const int smallBatchSize = 8192; // 8KB
        const int messageCount = 5;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-multi-large-batch")
            .WithBatchSize(smallBatchSize)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Each message is ~10KB, larger than the 8KB batch size
        // This forces each message into its own batch
        var values = new string[messageCount];
        for (var i = 0; i < messageCount; i++)
        {
            values[i] = new string((char)('A' + i), 10_000);
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = values[i]
            });
        }

        // Consume all messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= messageCount) break;
        }

        await Assert.That(consumed).Count().IsEqualTo(messageCount);

        // Verify all messages preserved correctly and in order
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(consumed[i].Key).IsEqualTo($"key-{i}");
            await Assert.That(consumed[i].Value).IsEqualTo(values[i]);
            await Assert.That(consumed[i].Value.Length).IsEqualTo(10_000);
        }
    }
}
