using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for producer timeout behavior and delivery guarantees,
/// focusing on linger timing, MaxBlock semantics, cancellation before append,
/// and flush completion semantics.
/// </summary>
public sealed class ProducerTimeoutTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Linger_BatchSentAfterLingerExpires_EvenIfNotFull()
    {
        // Arrange - Configure a short linger so the batch sends after the timer expires,
        // even though we only send one small message (batch is far from full).
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-linger-expiry")
            .WithLinger(TimeSpan.FromMilliseconds(100))
            .WithBatchSize(1_048_576) // 1MB batch - one small message will never fill this
            .Build();

        // Act - Send a single small message via fire-and-forget, then wait for linger to expire
        var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "linger-key",
            Value = "linger-value"
        });

        // The message should be delivered after the linger timer expires (~100ms),
        // not after the batch fills up.
        var metadata = await produceTask;

        // Assert - Message was delivered successfully despite not filling the batch
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify the message is actually readable from Kafka
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, metadata.Partition));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("linger-key");
        await Assert.That(result!.Value.Value).IsEqualTo("linger-value");
    }

    [Test]
    public async Task MaxBlock_ExceededWaitingForMetadata_ErrorPropagated()
    {
        // Arrange - Use an invalid bootstrap server so metadata lookup will fail.
        // WithMaxBlock controls how long the producer blocks waiting for metadata/buffer space.
        // With a short MaxBlock and invalid server, the produce should fail quickly.
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("invalid-host-that-does-not-exist:9092")
            .WithClientId("test-maxblock-exceeded")
            .WithMaxBlock(TimeSpan.FromSeconds(2))
            .Build();

        // Act & Assert - Producing to an unreachable broker should fail within the MaxBlock timeout
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "nonexistent-topic",
                Key = "key",
                Value = "value"
            });
        });

        sw.Stop();

        // The error should propagate within a reasonable time frame.
        // We use a generous upper bound (30s) to avoid flaky tests in slow CI,
        // but the key invariant is that it does NOT hang indefinitely.
        await Assert.That(sw.Elapsed.TotalSeconds).IsLessThan(30);
    }

    [Test]
    public async Task ProduceAsync_WithCancellation_BeforeAppend_PreventsDelivery()
    {
        // Arrange - Cancel the token before calling ProduceAsync.
        // Per CLAUDE.md: "Cancellation before append - message NOT sent"
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-cancel-before-append")
            .Build();

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel before producing

        // Act - Should throw immediately without appending the message
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "cancelled-key",
                Value = "cancelled-value"
            }, cts.Token);
        });

        // Assert - The message should NOT have been delivered.
        // Produce a sentinel message to verify the cancelled message is absent.
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "sentinel-key",
            Value = "sentinel-value"
        });

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 0));

        using var consumeCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var messages = new List<ConsumeResult<string, string>>();

        await foreach (var msg in consumer.ConsumeAsync(consumeCts.Token))
        {
            messages.Add(msg);
            // We expect only the sentinel message at offset 0
            if (messages.Count >= 1)
                break;
        }

        // The first (and only) message should be the sentinel, not the cancelled one
        await Assert.That(messages).Count().IsEqualTo(1);
        await Assert.That(messages[0].Key).IsEqualTo("sentinel-key");
        await Assert.That(messages[0].Value).IsEqualTo("sentinel-value");
    }

    [Test]
    public async Task FlushAsync_WaitsForPendingMessages_ThenCompletes()
    {
        // Arrange - Send multiple fire-and-forget messages with a long linger,
        // then flush and verify all messages are delivered.
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-flush-waits")
            .WithLinger(TimeSpan.FromMilliseconds(100))
            .Build();

        const int messageCount = 25;

        // Send fire-and-forget messages
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"flush-key-{i}",
                Value = $"flush-value-{i}"
            });
        }

        // Act - Flush should block until all pending messages are delivered
        await producer.FlushAsync();

        // Assert - All messages should be consumable after flush returns
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount)
                break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        // Verify message ordering is preserved
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Key).IsEqualTo($"flush-key-{i}");
            await Assert.That(messages[i].Value).IsEqualTo($"flush-value-{i}");
        }
    }

    [Test]
    public async Task FlushAsync_WithCancellation_StopsWaiting()
    {
        // Arrange - Send messages with a long linger, then cancel the flush.
        // Per CLAUDE.md: "Cancelling stops the caller from waiting, but batches continue sending in background."
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-flush-cancel-stops-waiting")
            .WithLinger(TimeSpan.FromMilliseconds(5000)) // Long linger to ensure flush has something to wait for
            .Build();

        // Send messages via fire-and-forget
        for (var i = 0; i < 10; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - Cancel the flush after a short time
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await producer.FlushAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected - flush was cancelled before all messages were delivered
        }

        sw.Stop();

        // Assert - Flush should have been cancelled (or completed very quickly if messages were already sent)
        // If messages were already flushed before cancellation, that is also acceptable behavior.
        // The key invariant is that flush does NOT block for the full 5-second linger.
        await Assert.That(sw.Elapsed.TotalSeconds).IsLessThan(4);

        // Whether flush was cancelled or completed, messages should eventually be delivered.
        // Wait for background delivery to complete, then verify.
        await Task.Delay(1000);
        await producer.FlushAsync(); // Flush again without cancellation to ensure delivery

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var consumeCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(consumeCts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 10)
                break;
        }

        await Assert.That(messages).Count().IsEqualTo(10);
    }
}
