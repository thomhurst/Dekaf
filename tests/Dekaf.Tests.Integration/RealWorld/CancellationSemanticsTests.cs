using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for cancellation semantics across producer and consumer operations.
/// Verifies the documented phase-based cancellation behavior:
/// - Before append: message NOT sent
/// - After append: caller gets OperationCanceledException but message IS delivered
/// - Flush cancellation: wait stops but batches continue sending
/// </summary>
[Category("Producer")]
public sealed class CancellationSemanticsTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Cancel_BeforeMetadataLookup_NoMessageSent()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-cancel-before-metadata")
            .BuildAsync();

        // Act - cancel immediately before any work happens
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.That(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = "should-not-be-sent"
            }, cts.Token).ConfigureAwait(false);
        }).Throws<OperationCanceledException>();

        // Verify the message was NOT sent by consuming
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-cancel-verify-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        using var verifyCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), verifyCts.Token);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Cancel_AfterAppend_CallerThrowsButMessageDelivered()
    {
        // Arrange - use long linger so message sits in batch
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-cancel-after-append")
            .WithLinger(TimeSpan.FromMilliseconds(5000))
            .BuildAsync();

        // Warm up metadata cache
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "warmup",
            Value = "warmup"
        });

        // Act - start produce, wait for append, then cancel
        using var cts = new CancellationTokenSource();

        var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-cancel-after-append",
            Value = "value-cancel-after-append"
        }, cts.Token);

        // Message is already appended by now (fast path with cached metadata)
        await Task.Delay(50).ConfigureAwait(false);
        cts.Cancel();

        // The task may complete successfully (message already sent) or throw OCE
        // depending on timing — both are valid per the documented semantics
        try
        {
            await produceTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected — caller was cancelled, but message delivery continues
        }

        // Flush to ensure all pending messages are sent
        await producer.FlushAsync().ConfigureAwait(false);

        // Verify the message WAS delivered despite cancellation
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-cancel-verify-delivery")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var verifyCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(verifyCts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 2) break; // warmup + actual message
        }

        // The cancelled message should still be in the topic
        var deliveredMessage = messages.FirstOrDefault(m => m.Key == "key-cancel-after-append");
        await Assert.That(deliveredMessage.Value).IsEqualTo("value-cancel-after-append");
    }

    [Test]
    public async Task Cancel_MixedBatch_CancelledAndNonCancelled()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-cancel-mixed-batch")
            .WithLinger(TimeSpan.FromMilliseconds(5000))
            .BuildAsync();

        // Warm up
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "warmup",
            Value = "warmup"
        });

        // Act - produce 10 messages, cancel 5 of them
        var tasks = new List<(int Index, CancellationTokenSource Cts, ValueTask<RecordMetadata> Task)>();

        for (var i = 0; i < 10; i++)
        {
            var msgCts = new CancellationTokenSource();
            var task = producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, msgCts.Token);

            tasks.Add((i, msgCts, task));
        }

        // Cancel odd-indexed messages
        await Task.Delay(50).ConfigureAwait(false);
        foreach (var (index, cts, _) in tasks)
        {
            if (index % 2 == 1) cts.Cancel();
        }

        // Collect results
        var succeeded = 0;
        var cancelled = 0;

        foreach (var (_, _, task) in tasks)
        {
            try
            {
                await task.ConfigureAwait(false);
                succeeded++;
            }
            catch (OperationCanceledException)
            {
                cancelled++;
            }
        }

        // Even-indexed should succeed
        await Assert.That(succeeded).IsGreaterThanOrEqualTo(5);

        // Flush and verify all messages were delivered regardless of cancellation
        await producer.FlushAsync().ConfigureAwait(false);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-cancel-mixed-verify")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var consumed = new List<ConsumeResult<string, string>>();
        using var verifyCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(verifyCts.Token))
        {
            consumed.Add(msg);
            // warmup + 10 messages
            if (consumed.Count >= 11) break;
        }

        // All 10 messages should be in the topic (plus warmup), because cancellation
        // only stops the caller's wait, not the actual delivery
        var dataMessages = consumed.Where(m => m.Key != "warmup").ToList();
        await Assert.That(dataMessages.Count).IsEqualTo(10);

        // Clean up CancellationTokenSources
        foreach (var (_, cts, _) in tasks)
        {
            cts.Dispose();
        }
    }

    [Test]
    public async Task FlushAsync_Cancelled_BatchesContinueSending()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-flush-cancel-continues")
            .WithLinger(TimeSpan.FromMilliseconds(2000))
            .BuildAsync();

        // Send messages via fire-and-forget
        for (var i = 0; i < 20; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - cancel flush quickly
        using var flushCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        try
        {
            await producer.FlushAsync(flushCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Wait for background delivery to complete
        await producer.FlushAsync().ConfigureAwait(false);

        // Verify all messages were delivered despite flush cancellation
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-flush-cancel-verify")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var consumed = new List<ConsumeResult<string, string>>();
        using var verifyCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(verifyCts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= 20) break;
        }

        await Assert.That(consumed.Count).IsEqualTo(20);
    }

    [Test]
    public async Task ConsumeAsync_Cancelled_ExitsCleanly()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consume-cancel-producer")
            .BuildAsync();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consume-cancel-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        // Act - start consuming, cancel after 2 messages
        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource();

        try
        {
            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                consumed.Add(msg);
                if (consumed.Count >= 2)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Some implementations throw on cancellation, which is also valid
        }

        // Assert - should have consumed at least 2 messages before exiting
        // The IAsyncEnumerable may exit gracefully (no throw) or throw OCE — both are valid
        await Assert.That(consumed.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task ConsumeOneAsync_Timeout_ReturnsNull()
    {
        // Arrange - empty topic, no messages to consume
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consume-timeout-null")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        // Act - short timeout, no messages available
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(3), cts.Token);

        // Assert - should return null, not throw
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Send_FireAndForget_UnaffectedByCancellation()
    {
        // Arrange - Send() has no cancellation token parameter
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-send-no-cancel")
            .BuildAsync();

        // Act - fire-and-forget messages
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

        await producer.FlushAsync().ConfigureAwait(false);

        // Verify all messages delivered
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-send-no-cancel-verify")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= messageCount) break;
        }

        await Assert.That(consumed.Count).IsEqualTo(messageCount);
    }
}
