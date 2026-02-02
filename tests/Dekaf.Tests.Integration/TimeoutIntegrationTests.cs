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
    public async Task Producer_ProduceAsync_AfterAppend_CancellationDoesNotAffectDelivery()
    {
        // Arrange - Test that cancellation after append completes successfully
        // The low-latency optimization flushes awaited produces within 1-2ms, so by the time
        // we cancel after 100ms, the message has already been sent to Kafka
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-cancel-after-append")
            .WithLingerMs(5000) // Doesn't matter - awaited produces flush immediately
            .Build();

        // Act - Start produce, then cancel after message is already appended
        using var cts = new CancellationTokenSource();

        var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        }, cts.Token);

        // Cancel after message is already appended and sent (1-2ms after ProduceAsync call)
        await Task.Delay(100).ConfigureAwait(false);
        cts.Cancel();

        // Assert - Message completes successfully despite cancellation
        // (it was already committed to being sent when token was cancelled)
        var metadata = await produceTask.ConfigureAwait(false);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
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

    // ==================== COMPREHENSIVE CANCELLATION EDGE CASES ====================
    // These tests validate pre-queue cancellation semantics comprehensively

    #region Pre-Queue Cancellation Points

    [Test]
    public async Task Producer_ProduceAsync_CancelledBeforeMetadataLookup_ThrowsImmediately()
    {
        // Arrange - Pre-cancelled token, no work should happen
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-cancel-before-metadata")
            .Build();

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert - Should throw without doing any work
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key1",
                Value = "value1"
            }, cts.Token).ConfigureAwait(false);
        });
        sw.Stop();

        // Should be nearly instantaneous (< 10ms)
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(10);
    }

    [Test]
    public async Task Producer_ProduceAsync_CancelledDuringSlowMetadataRefresh_ThrowsOperationCancelled()
    {
        // Arrange - Force metadata refresh with cancellation during network operation
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("10.255.255.1:9092") // Non-routable IP for slow timeout
            .WithClientId("test-cancel-during-metadata-refresh")
            .WithRequestTimeout(30000)
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act & Assert - Should cancel during metadata lookup
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "nonexistent-topic",
                Key = "key1",
                Value = "value1"
            }, cts.Token).ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Producer_ProduceAsync_CancelledDuringChannelWrite_ThrowsOperationCancelled()
    {
        // Arrange - Use slow path by producing to new topic before metadata cache warms up
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-cancel-during-channel-write")
            .Build();

        // Act - Cancel immediately, might catch during channel write
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

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

    #endregion

    #region Post-Queue Immunity

    [Test]
    public async Task Producer_ProduceAsync_CancelledAfterFastPathAppend_CompletesSuccessfully()
    {
        // Arrange - Warm up metadata cache first
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-cancel-after-fast-path")
            .Build();

        // Warm up: First produce succeeds
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "warmup",
            Value = "warmup"
        }).ConfigureAwait(false);

        // Act - Now fast path is active, cancel after append
        using var cts = new CancellationTokenSource();

        var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        }, cts.Token);

        // Fast path appends synchronously, so message is already committed
        await Task.Delay(50).ConfigureAwait(false);
        cts.Cancel();

        // Assert - Completes successfully despite cancellation
        var metadata = await produceTask.ConfigureAwait(false);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_ProduceAsync_CancelledAfterSlowPathAppend_CompletesSuccessfully()
    {
        // Arrange - Force slow path by not warming cache
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-cancel-after-slow-path")
            .Build();

        using var cts = new CancellationTokenSource();

        var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        }, cts.Token);

        // Wait for slow path to complete append
        await Task.Delay(100).ConfigureAwait(false);
        cts.Cancel();

        // Assert - Completes successfully
        var metadata = await produceTask.ConfigureAwait(false);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Concurrent Operations

    [Test]
    public async Task Producer_ConcurrentProduces_SomeCancelledSomeNot_CorrectBehavior()
    {
        // Arrange - Mix of cancelled and non-cancelled produces
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-concurrent-mixed-cancellation")
            .Build();

        var tasks = new List<Task<(bool cancelled, RecordMetadata? metadata, Exception? error)>>();

        // Launch 10 produces, cancel odd ones immediately
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            var cts = new CancellationTokenSource();

            if (i % 2 == 1)
                cts.Cancel(); // Cancel odd ones

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"key{index}",
                        Value = $"value{index}"
                    }, cts.Token).ConfigureAwait(false);
                    return (false, metadata, (Exception?)null);
                }
                catch (OperationCanceledException ex)
                {
                    return (true, null, ex);
                }
            }));
        }

        // Act
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - Even indices succeed, odd indices cancelled
        for (int i = 0; i < 10; i++)
        {
            if (i % 2 == 0)
            {
                await Assert.That(results[i].cancelled).IsFalse();
                await Assert.That(results[i].metadata).IsNotNull();
            }
            else
            {
                await Assert.That(results[i].cancelled).IsTrue();
                await Assert.That(results[i].error).IsNotNull();
            }
        }
    }

    [Test]
    public async Task Producer_ConcurrentProducesWithLateCancellation_AllComplete()
    {
        // Arrange - Cancel after all messages likely appended
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-concurrent-late-cancellation")
            .Build();

        using var cts = new CancellationTokenSource();
        var tasks = new List<Task<RecordMetadata>>();

        // Launch 20 produces with same token
        for (int i = 0; i < 20; i++)
        {
            var index = i;
            tasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key{index}",
                Value = $"value{index}"
            }, cts.Token).AsTask());
        }

        // Wait for messages to append, then cancel
        await Task.Delay(200).ConfigureAwait(false);
        cts.Cancel();

        // Assert - All should complete successfully (appended before cancellation)
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        await Assert.That(results.Length).IsEqualTo(20);
        foreach (var metadata in results)
        {
            await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    #endregion

    #region Backpressure Scenarios

    [Test]
    public async Task Producer_CancelledDuringMemoryBackpressure_ThrowsOperationCancelled()
    {
        // Arrange - Tiny buffer to force backpressure
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-cancel-during-backpressure")
            .WithBufferMemory(1000) // Very small
            .WithLingerMs(10000) // Long linger to keep messages in buffer
            .Build();

        // Fill buffer with fire-and-forget (doesn't use cancellation token)
        for (int i = 0; i < 50; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"filler{i}",
                Value = new string('x', 100)
            });
        }

        // Act - Try to produce with cancellation during backpressure wait
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "blocked",
                Value = new string('x', 500)
            }, cts.Token).ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Producer_CancelledAfterMemoryReserved_CompletesSuccessfully()
    {
        // Arrange - Cancel after memory is reserved but before batch sends
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-cancel-after-memory-reserved")
            .WithLingerMs(5000)
            .Build();

        using var cts = new CancellationTokenSource();

        // Fire-and-forget to fill batch without triggering immediate flush
        for (int i = 0; i < 10; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key{i}",
                Value = $"value{i}"
            });
        }

        // Now add awaited produce (will trigger flush)
        var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "awaited",
            Value = "awaited"
        }, cts.Token);

        // Cancel after append (memory already reserved)
        await Task.Delay(50).ConfigureAwait(false);
        cts.Cancel();

        // Assert - Should complete successfully
        var metadata = await produceTask.ConfigureAwait(false);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    #endregion

    #region FlushAsync Edge Cases

    [Test]
    public async Task Producer_FlushAsync_CancelledWithEmptyBatches_ThrowsImmediately()
    {
        // Arrange - No messages queued
        await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-flush-empty-cancelled")
            .Build();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Should throw immediately
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await producer.FlushAsync(cts.Token).ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Producer_FlushAsync_CancelledWhileWaitingForMultipleBatches_StopsWaiting()
    {
        // Arrange - Multiple batches across different partitions
        var topic = await kafka.CreateTestTopicAsync(partitions: 3).ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-flush-multiple-batches-cancelled")
            .WithLingerMs(10000) // Long linger
            .Build();

        // Send to different partitions (fire-and-forget)
        for (int i = 0; i < 30; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = i % 3,
                Key = $"key{i}",
                Value = $"value{i}"
            });
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act - Start flush, then cancel during wait
        var flushTask = producer.FlushAsync(cts.Token);

        // Assert - Should throw OperationCanceledException
        // (batches continue sending in background)
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await flushTask.ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Producer_FlushAsync_CancelledButBatchesStillDeliver()
    {
        // Arrange - Verify batches deliver even if flush is cancelled
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-flush-cancelled-batches-deliver")
            .WithLingerMs(2000)
            .Build();

        var produceTask = producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act - Cancel flush but produce should still complete
        try
        {
            await producer.FlushAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Message still delivers despite flush cancellation
        var metadata = await produceTask.ConfigureAwait(false);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Disposal Interactions

    [Test]
    public async Task Producer_DisposedDuringProduceAsync_ThrowsObjectDisposedException()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-dispose-during-produce")
            .Build();

        // Act - Dispose immediately
        await producer.DisposeAsync().ConfigureAwait(false);

        // Assert - Should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key1",
                Value = "value1"
            }).ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Producer_ConcurrentDisposeAndCancelledProduce_HandlesGracefully()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-concurrent-dispose-cancel")
            .Build();

        using var cts = new CancellationTokenSource();

        // Act - Start produce, cancel and dispose concurrently
        var produceTask = Task.Run(async () =>
        {
            try
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "key1",
                    Value = "value1"
                }, cts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Either OperationCanceledException or ObjectDisposedException is acceptable
            }
        });

        cts.Cancel();
        await producer.DisposeAsync().ConfigureAwait(false);

        // Assert - Should complete without hanging
        var completed = await Task.WhenAny(produceTask, Task.Delay(5000)).ConfigureAwait(false) == produceTask;
        await Assert.That(completed).IsTrue();
    }

    #endregion

    #region Fire-and-Forget Immunity

    [Test]
    public async Task Producer_Send_NeverAffectedByCancellation()
    {
        // Arrange - Send() doesn't use cancellation tokens
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-send-no-cancellation")
            .Build();

        // Act - Send messages (no cancellation token parameter)
        for (int i = 0; i < 50; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key{i}",
                Value = $"value{i}"
            });
        }

        // Flush to verify all were sent
        await producer.FlushAsync().ConfigureAwait(false);

        // Assert - All messages delivered (verify via consumer)
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId("test-group-send-immunity")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var consumed = 0;
        var timeout = DateTimeOffset.UtcNow.AddSeconds(10);
        while (consumed < 50 && DateTimeOffset.UtcNow < timeout)
        {
            var result = await consumer.ConsumeAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            if (result is not null)
                consumed++;
        }

        await Assert.That(consumed).IsEqualTo(50);
    }

    #endregion
}
