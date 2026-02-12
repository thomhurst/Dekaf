using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for error propagation and exception handling.
/// Verifies correct exception types for various failure scenarios,
/// including disposed resources, invalid configuration, and error context.
/// </summary>
[Category("Resilience")]
public sealed class ErrorPropagationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task DisposedProducer_ProduceAsync_ThrowsObjectDisposedException()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-disposed-producer")
            .BuildAsync();

        await producer.DisposeAsync().ConfigureAwait(false);

        // Act & Assert
        await Assert.That(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = "value"
            }).ConfigureAwait(false);
        }).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task DisposedProducer_Send_ThrowsObjectDisposedException()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-disposed-producer-send")
            .BuildAsync();

        await producer.DisposeAsync().ConfigureAwait(false);

        // Act & Assert
        await Assert.That(() =>
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = "value"
            });
            return Task.CompletedTask;
        }).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task DisposedProducer_FlushAsync_CompletesGracefully()
    {
        // Arrange
        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-disposed-producer-flush")
            .BuildAsync();

        await producer.DisposeAsync().ConfigureAwait(false);

        // Act - FlushAsync on disposed producer is a graceful no-op (nothing to flush)
        await producer.FlushAsync().ConfigureAwait(false);

        // Assert - completes without throwing
    }

    [Test]
    public async Task DisposedConsumer_ConsumeAsync_ThrowsObjectDisposedException()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-disposed-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));
        await consumer.DisposeAsync().ConfigureAwait(false);

        // Act & Assert
        await Assert.That(async () =>
        {
            await foreach (var _ in consumer.ConsumeAsync())
            {
                // Should throw before yielding
            }
        }).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task DisposedConsumer_CommitAsync_CompletesGracefully()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-disposed-consumer-commit")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer.Subscribe(topic);
        await consumer.DisposeAsync().ConfigureAwait(false);

        // Act - CommitAsync on disposed consumer is a graceful no-op
        await consumer.CommitAsync().ConfigureAwait(false);

        // Assert - completes without throwing
    }

    [Test]
    public async Task InvalidBootstrapServers_ProduceAsync_ThrowsWithContext()
    {
        // Arrange - invalid host that won't resolve
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("invalid-host-that-does-not-exist:9092")
            .WithClientId("test-invalid-bootstrap")
            .Build();

        try
        {
            // Act & Assert - should fail with a meaningful error
            await Assert.That(async () =>
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = "test-topic",
                    Key = "key",
                    Value = "value"
                }).ConfigureAwait(false);
            }).Throws<Exception>(); // Could be KafkaException, SocketException, TimeoutException
        }
        finally
        {
            await producer.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task MultipleDispose_IsIdempotent_Producer()
    {
        // Arrange
        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-dispose-producer")
            .BuildAsync();

        // Act - dispose multiple times
        await producer.DisposeAsync().ConfigureAwait(false);
        await producer.DisposeAsync().ConfigureAwait(false);
        await producer.DisposeAsync().ConfigureAwait(false);

        // Assert - no exception thrown (idempotent)
    }

    [Test]
    public async Task MultipleDispose_IsIdempotent_Consumer()
    {
        // Arrange
        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-dispose-consumer")
            .BuildAsync();

        // Act - dispose multiple times
        await consumer.DisposeAsync().ConfigureAwait(false);
        await consumer.DisposeAsync().ConfigureAwait(false);
        await consumer.DisposeAsync().ConfigureAwait(false);

        // Assert - no exception thrown (idempotent)
    }

    [Test]
    public async Task ProducerDisposal_FlushesInFlightMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-disposal-flushes")
            .WithLinger(TimeSpan.FromMilliseconds(5000)) // Long linger
            .BuildAsync();

        // Send fire-and-forget messages
        for (var i = 0; i < 10; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - dispose should flush pending messages
        await producer.DisposeAsync().ConfigureAwait(false);

        // Verify messages were delivered
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-disposal-verify")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                consumed.Add(msg);
                if (consumed.Count >= 10) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout reached - fall through to assertion
        }

        // Assert - all messages should have been flushed on disposal
        await Assert.That(consumed.Count).IsEqualTo(10);
    }

    [Test]
    public async Task ConcurrentDisposeAndProduce_HandlesGracefully()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-concurrent-dispose-produce")
            .BuildAsync();

        // Warm up
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "warmup",
            Value = "warmup"
        });

        // Act - dispose and produce concurrently
        var produceExceptions = new List<Exception>();
        var produceTask = Task.Run(async () =>
        {
            for (var i = 0; i < 100; i++)
            {
                try
                {
                    await producer.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"key-{i}",
                        Value = $"value-{i}"
                    }).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    produceExceptions.Add(new ObjectDisposedException("producer"));
                    break;
                }
                catch (Exception ex)
                {
                    produceExceptions.Add(ex);
                }
            }
        });

        // Brief delay to let some produces start
        await Task.Delay(10).ConfigureAwait(false);
        await producer.DisposeAsync().ConfigureAwait(false);

        await produceTask.ConfigureAwait(false);

        // Assert - should not hang, and exceptions (if any) should be ObjectDisposedException
        foreach (var ex in produceExceptions)
        {
            await Assert.That(ex).IsTypeOf<ObjectDisposedException>();
        }
    }

    [Test]
    public async Task ConsumerClose_ThenDispose_NoDoubleClose()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-close-then-dispose")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Act - close first, then dispose
        await consumer.CloseAsync().ConfigureAwait(false);
        await consumer.DisposeAsync().ConfigureAwait(false);

        // Assert - no exception (both operations are idempotent)
    }
}
