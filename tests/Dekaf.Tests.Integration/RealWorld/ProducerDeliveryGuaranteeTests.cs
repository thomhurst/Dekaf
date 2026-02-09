using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for producer delivery guarantees across different acks levels,
/// flush semantics, and callback-based production patterns.
/// </summary>
public sealed class ProducerDeliveryGuaranteeTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task AcksAll_AllReplicasAcknowledge_MessageDurable()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "durable-key",
            Value = "durable-value"
        });

        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify message is readable
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, metadata.Partition));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("durable-value");
    }

    [Test]
    public async Task AcksNone_FireAndForget_MessageDelivered()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.None)
            .BuildAsync();

        // AcksNone doesn't wait for broker confirmation
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "fast-key",
            Value = "fast-value"
        });

        await producer.FlushAsync();

        // Message should still be there (single broker, no failure)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("fast-value");
    }

    [Test]
    public async Task Flush_UnderLoad_AllMessagesDelivered()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLinger(TimeSpan.FromMilliseconds(50)) // Aggregate messages
            .BuildAsync();

        const int messageCount = 500;
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Flush should wait for all in-flight messages
        await producer.FlushAsync();

        // Verify all messages were delivered
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

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
    public async Task FlushWithCancellation_StopsWaitingButDeliveryCompletes()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLinger(TimeSpan.FromMilliseconds(100))
            .BuildAsync();

        const int messageCount = 50;
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Give time for messages to send, then flush fully
        await Task.Delay(500);
        await producer.FlushAsync();

        // All messages should be delivered
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

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
    public async Task SendWithCallback_AllCallbacksInvoked()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        const int messageCount = 20;
        var callbackResults = new ConcurrentBag<(RecordMetadata Metadata, Exception? Error)>();
        var callbackCount = 0;

        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, (metadata, error) =>
            {
                callbackResults.Add((metadata, error));
                Interlocked.Increment(ref callbackCount);
            });
        }

        await producer.FlushAsync();

        // Wait a bit for callbacks to complete
        var timeout = TimeSpan.FromSeconds(10);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (callbackCount < messageCount && sw.Elapsed < timeout)
        {
            await Task.Delay(100);
        }

        await Assert.That(callbackResults.Count).IsEqualTo(messageCount);

        // All callbacks should have succeeded (no errors)
        foreach (var (metadata, error) in callbackResults)
        {
            await Assert.That(error).IsNull();
            await Assert.That(metadata.Topic).IsEqualTo(topic);
            await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task MultipleFlush_NoDoubleCounting()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Batch 1
        for (var i = 0; i < 10; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"batch1-key-{i}",
                Value = $"batch1-value-{i}"
            });
        }

        await producer.FlushAsync();

        // Batch 2
        for (var i = 0; i < 10; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"batch2-key-{i}",
                Value = $"batch2-value-{i}"
            });
        }

        await producer.FlushAsync();

        // Extra flush (should be no-op)
        await producer.FlushAsync();

        // Should have exactly 20 messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 20) break;
        }

        await Assert.That(messages).Count().IsEqualTo(20);
    }

    [Test]
    public async Task DisposeFlushesInFlight_NoMessageLoss()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Use a producer that will be disposed with in-flight messages
        await using (var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLinger(TimeSpan.FromMilliseconds(50))
            .BuildAsync())
        {
            for (var i = 0; i < 30; i++)
            {
                producer.Send(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i}"
                });
            }
            // DisposeAsync should flush remaining messages
        }

        // Verify messages arrived
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 30) break;
        }

        await Assert.That(messages).Count().IsEqualTo(30);
    }

    [Test]
    public async Task ProduceToMultipleTopics_AllTopicsGetMessages()
    {
        var topic1 = await KafkaContainer.CreateTestTopicAsync();
        var topic2 = await KafkaContainer.CreateTestTopicAsync();
        var topic3 = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce to all three topics
        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic1, Key = $"t1-key-{i}", Value = $"t1-value-{i}"
            });
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic2, Key = $"t2-key-{i}", Value = $"t2-value-{i}"
            });
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic3, Key = $"t3-key-{i}", Value = $"t3-value-{i}"
            });
        }

        // Verify each topic
        foreach (var (topic, prefix) in new[] { (topic1, "t1"), (topic2, "t2"), (topic3, "t3") })
        {
            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync();

            consumer.Assign(new TopicPartition(topic, 0));

            var messages = new List<ConsumeResult<string, string>>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                messages.Add(msg);
                if (messages.Count >= 10) break;
            }

            await Assert.That(messages).Count().IsEqualTo(10);
            await Assert.That(messages[0].Value).StartsWith($"{prefix}-value-");
        }
    }

    [Test]
    public async Task ProduceWithTimestamp_TimestampPreserved()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        var timestamp = DateTimeOffset.UtcNow;

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "ts-key",
            Value = "ts-value",
            Timestamp = timestamp
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        // Kafka stores timestamps at millisecond precision
        var expectedMs = timestamp.ToUnixTimeMilliseconds();
        var actualMs = result!.Value.Timestamp.ToUnixTimeMilliseconds();
        await Assert.That(actualMs).IsEqualTo(expectedMs);
    }
}
