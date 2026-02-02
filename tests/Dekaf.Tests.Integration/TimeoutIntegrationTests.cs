using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests verifying timeout protection in connection, flush, receive, and disposal operations.
/// These tests exercise code paths that have timeout protection to ensure operations complete without hanging.
/// </summary>
[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class TimeoutIntegrationTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task Producer_ConnectionEstablishment_CompletesWithinDefaultTimeout()
    {
        // Arrange - Test that connection establishment completes within default ConnectionTimeout (30s)
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-connection-timeout")
            .Build();

        // Act - Produce a message to trigger connection establishment
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        }).ConfigureAwait(false);

        // Assert - Connection succeeded within timeout
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_RequestWithAcksAll_CompletesWithinDefaultTimeout()
    {
        // Arrange - Test that requests with Acks.All complete within default RequestTimeout (30s)
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-request-timeout")
            .WithAcks(Acks.All)
            .Build();

        // Act - Produce a message to trigger request
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        }).ConfigureAwait(false);

        // Assert - Request succeeded within timeout
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_FlushWithBatchedMessages_CompletesWithinDefaultTimeout()
    {
        // Arrange - Test that flush completes within default RequestTimeout (30s)
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-flush-timeout")
            .WithLingerMs(100) // Add some linger to batch messages
            .Build();

        // Act - Send multiple messages then flush
        for (int i = 0; i < 10; i++)
        {
            producer.Send(topic, $"key{i}", $"value{i}");
        }

        // Flush with explicit timeout to ensure it completes
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await producer.FlushAsync(cts.Token).ConfigureAwait(false);

        // Assert - Flush completed without throwing
    }

    [Test]
    public async Task Producer_DisposalAfterProduction_CompletesWithinReasonableTime()
    {
        // Arrange - Test that disposal completes within reasonable time (ConnectionTimeout grace period)
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-disposal-timeout")
            .Build();

        // Act - Produce a message to establish connection
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        }).ConfigureAwait(false);

        // Dispose with timeout to ensure it doesn't hang
        var disposeTask = producer.DisposeAsync().AsTask();
        var completedInTime = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(35))).ConfigureAwait(false) == disposeTask;

        // Assert - Disposal completed within timeout (30s default + 5s buffer)
        await Assert.That(completedInTime).IsTrue();
    }

    [Test]
    public async Task Producer_ConcurrentProduction_AllRequestsCompleteWithinTimeout()
    {
        // Arrange - Test that concurrent production completes within timeout
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-concurrent-timeout")
            .Build();

        // Act - Produce messages concurrently
        var tasks = new List<ValueTask<RecordMetadata>>();
        for (int i = 0; i < 10; i++)
        {
            var i1 = i;
            tasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key{i1}",
                Value = $"value{i1}"
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in tasks)
        {
            results.Add(await task.ConfigureAwait(false));
        }

        // Assert - All messages produced successfully
        await Assert.That(results).Count().IsEqualTo(10);
        await Assert.That(results.All(r => r.Offset >= 0)).IsTrue();
    }

    [Test]
    public async Task Producer_LargeVolumeProduction_FlushCompletesWithoutHanging()
    {
        // Arrange - Test that large batches with backpressure still flush within timeout
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-backpressure-timeout")
            .WithBatchSize(16384)
            .WithLingerMs(10)
            .Build();

        // Act - Send many messages to trigger backpressure
        var messageValue = new string('x', 1000); // 1 KB messages
        for (int i = 0; i < 1000; i++)
        {
            producer.Send(topic, $"key{i}", messageValue);
        }

        // Flush with timeout to ensure backpressure doesn't cause hang
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await producer.FlushAsync(cts.Token).ConfigureAwait(false);

        // Assert - Flush completed without throwing
    }

    [Test]
    public async Task Producer_ReceiveLoop_ProcessesResponsesWithinTimeout()
    {
        // Arrange - Test that receive loop processes responses within timeout
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-receive-timeout")
            .WithAcks(Acks.All)
            .Build();

        // Act - Produce multiple messages to exercise receive loop
        for (int i = 0; i < 100; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key{i}",
                Value = $"value{i}"
            }).ConfigureAwait(false);

            // Assert each receives a response (no timeout)
            await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    // ==================== EDGE CASE TESTS ====================

    [Test]
    public async Task Producer_ProduceAsync_WithCancelledToken_ThrowsImmediately()
    {
        // Arrange - Test that pre-cancelled token throws immediately
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-cancelled-token")
            .Build();

        // Act & Assert - Pass pre-cancelled token
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key1",
                Value = "value1"
            }, cts.Token).ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Producer_ProduceAsync_CancelledDuringWait_ThrowsOperationCancelled()
    {
        // Arrange - Test cancellation during produce operation
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-cancel-during-produce")
            .WithLingerMs(5000) // Long linger to give time to cancel
            .Build();

        // Act - Start produce, then cancel
        using var cts = new CancellationTokenSource();

        var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        }, cts.Token);

        // Cancel after a short delay
        await Task.Delay(100).ConfigureAwait(false);
        cts.Cancel();

        // Assert - Should throw OperationCanceledException
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await produceTask.ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Producer_FlushAsync_WithCancelledToken_ThrowsImmediately()
    {
        // Arrange - Test that pre-cancelled token throws immediately
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-flush-cancelled-token")
            .Build();

        // Send some messages
        producer.Send(topic, "key1", "value1");

        // Act & Assert - Pass pre-cancelled token to flush
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await producer.FlushAsync(cts.Token).ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Producer_FlushAsync_CancelledDuringFlush_ThrowsOperationCancelled()
    {
        // Arrange - Test cancellation during flush
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-cancel-during-flush")
            .WithLingerMs(100)
            .Build();

        // Send many messages
        for (int i = 0; i < 100; i++)
        {
            producer.Send(topic, $"key{i}", $"value{i}");
        }

        // Act - Start flush, then cancel
        using var cts = new CancellationTokenSource();
        var flushTask = producer.FlushAsync(cts.Token);

        // Cancel after short delay
        await Task.Delay(50).ConfigureAwait(false);
        cts.Cancel();

        // Assert - Should throw OperationCanceledException
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await flushTask.ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Producer_FlushEmpty_CompletesImmediately()
    {
        // Arrange - Test flushing producer with no messages
        await kafka.CreateTestTopicAsync().ConfigureAwait(false); // Create topic but don't use it

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-flush-empty")
            .Build();

        // Act - Flush without sending any messages
        var startTime = Environment.TickCount64;
        await producer.FlushAsync().ConfigureAwait(false);
        var elapsed = Environment.TickCount64 - startTime;

        // Assert - Should complete quickly (< 1 second)
        await Assert.That(elapsed).IsLessThan(1000);
    }

    [Test]
    public async Task Producer_DisposeWithoutProduction_CompletesQuickly()
    {
        // Arrange - Create producer but never send messages
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-dispose-unused")
            .Build();

        // Act - Dispose without producing
        var startTime = Environment.TickCount64;
        await producer.DisposeAsync().ConfigureAwait(false);
        var elapsed = Environment.TickCount64 - startTime;

        // Assert - Should complete quickly (< 2 seconds)
        await Assert.That(elapsed).IsLessThan(2000);
    }

    [Test]
    public async Task Producer_DisposeAfterFailedConnection_CompletesWithinTimeout()
    {
        // Arrange - Create producer with invalid bootstrap servers
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("invalid-host:9092")
            .WithClientId("test-producer-dispose-after-failure")
            .Build();

        // Try to produce (will fail to connect)
        try
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "test-topic",
                Key = "key1",
                Value = "value1"
            }).ConfigureAwait(false);
        }
        catch
        {
            // Expected to fail
        }

        // Act - Dispose after failed connection
        var disposeTask = producer.DisposeAsync().AsTask();
        var completedInTime = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false) == disposeTask;

        // Assert - Should complete within timeout
        await Assert.That(completedInTime).IsTrue();
    }

    [Test]
    public async Task Producer_ConcurrentFlushAndProduce_BothComplete()
    {
        // Arrange - Test concurrent flush and produce operations
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-concurrent-flush-produce")
            .WithLingerMs(50)
            .Build();

        // Send initial messages
        for (int i = 0; i < 50; i++)
        {
            producer.Send(topic, $"key{i}", $"value{i}");
        }

        // Act - Flush and produce concurrently
        var flushTask = producer.FlushAsync().AsTask();
        var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "concurrent-key",
            Value = "concurrent-value"
        }).AsTask();

        await Task.WhenAll(flushTask, produceTask).ConfigureAwait(false);

        // Assert - Both operations completed successfully
        var metadata = await produceTask.ConfigureAwait(false);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ConcurrentDispose_IsIdempotent()
    {
        // Arrange - Test multiple threads disposing simultaneously
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-concurrent-dispose")
            .Build();

        // Produce a message to establish connection
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        }).ConfigureAwait(false);

        // Act - Dispose from multiple threads simultaneously
        var disposeTasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            disposeTasks.Add(Task.Run(async () => await producer.DisposeAsync().AsTask().ConfigureAwait(false)));
        }

        // Assert - All dispose calls complete without exception
        await Task.WhenAll(disposeTasks).ConfigureAwait(false);
    }
}
