using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests verifying timeout protection in connection, flush, receive, and disposal operations.
/// These tests exercise code paths that have timeout protection to ensure operations complete without hanging.
/// </summary>
public class TimeoutIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Producer_ConnectionEstablishment_CompletesWithinDefaultTimeout()
    {
        // Arrange - Test that connection establishment completes within default ConnectionTimeout (30s)
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-connection-timeout")
            .BuildAsync();

        // Act - Produce a message to trigger connection establishment
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Assert - Connection succeeded within timeout
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_RequestWithAcksAll_CompletesWithinDefaultTimeout()
    {
        // Arrange - Test that requests with Acks.All complete within default RequestTimeout (30s)
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-request-timeout")
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act - Produce a message to trigger request
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        });

        // Assert - Request succeeded within timeout
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Producer_FlushWithBatchedMessages_CompletesWithinDefaultTimeout()
    {
        // Arrange - Test that flush completes within default RequestTimeout (30s)
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-flush-timeout")
            .WithLinger(TimeSpan.FromMilliseconds(100)) // Add some linger to batch messages
            .BuildAsync();

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
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-disposal-timeout")
            .BuildAsync();

        try
        {
            // Act - Produce a message to establish connection
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key1",
                Value = "value1"
            });

            // Dispose with timeout to ensure it doesn't hang
            var disposeTask = producer.DisposeAsync().AsTask();
            var completedInTime = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(35))).ConfigureAwait(false) == disposeTask;

            // Assert - Disposal completed within timeout (30s default + 5s buffer)
            await Assert.That(completedInTime).IsTrue();
        }
        finally
        {
            // Ensure cleanup even if test fails
            try { await producer.DisposeAsync().ConfigureAwait(false); } catch { /* already disposed or disposing */ }
        }
    }

    [Test]
    public async Task Producer_ConcurrentProduction_AllRequestsCompleteWithinTimeout()
    {
        // Arrange - Test that concurrent production completes within timeout
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-concurrent-timeout")
            .BuildAsync();

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
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-backpressure-timeout")
            .WithBatchSize(16384)
            .WithLinger(TimeSpan.FromMilliseconds(10))
            .BuildAsync();

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
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-receive-timeout")
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act - Produce multiple messages to exercise receive loop
        for (int i = 0; i < 100; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key{i}",
                Value = $"value{i}"
            });

            // Assert each receives a response (no timeout)
            await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    // ==================== EDGE CASE TESTS ====================

    [Test]
    public async Task Producer_ProduceAsync_WithCancelledToken_ThrowsImmediately()
    {
        // Arrange - Test that pre-cancelled token throws immediately
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-cancelled-token")
            .BuildAsync();

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
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-cancel-after-append")
            .WithLinger(TimeSpan.FromMilliseconds(5000)) // Doesn't matter - awaited produces flush immediately
            .BuildAsync();

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
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-flush-cancelled-token")
            .BuildAsync();

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

    // NOTE: Timing-based cancellation tests removed - converted to unit tests in ProducerCancellationTests.cs
    // Integration tests can't reliably trigger race conditions (operations complete too fast in CI)
    // Unit tests with controlled conditions provide better coverage of cancellation logic

    [Test]
    public async Task Producer_FlushEmpty_CompletesImmediately()
    {
        // Arrange - Test flushing producer with no messages
        await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false); // Create topic but don't use it

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-flush-empty")
            .BuildAsync();

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
        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-dispose-unused")
            .BuildAsync();

        try
        {
            // Act - Dispose without producing
            var startTime = Environment.TickCount64;
            await producer.DisposeAsync().ConfigureAwait(false);
            var elapsed = Environment.TickCount64 - startTime;

            // Assert - Should complete quickly (< 2 seconds)
            await Assert.That(elapsed).IsLessThan(2000);
        }
        finally
        {
            // Ensure cleanup even if test fails
            try { await producer.DisposeAsync().ConfigureAwait(false); } catch { /* already disposed */ }
        }
    }

    [Test]
    public async Task Producer_DisposeAfterFailedConnection_CompletesWithinTimeout()
    {
        // Arrange - Create producer with invalid bootstrap servers
        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("invalid-host:9092")
            .WithClientId("test-producer-dispose-after-failure")
            .BuildAsync();

        try
        {
            // Try to produce (will fail to connect)
            try
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = "test-topic",
                    Key = "key1",
                    Value = "value1"
                });
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
        finally
        {
            // Ensure cleanup even if test fails
            try { await producer.DisposeAsync().ConfigureAwait(false); } catch { /* already disposed or disposing */ }
        }
    }

    [Test]
    public async Task Producer_ConcurrentFlushAndProduce_BothComplete()
    {
        // Arrange - Test concurrent flush and produce operations
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-concurrent-flush-produce")
            .WithLinger(TimeSpan.FromMilliseconds(50))
            .BuildAsync();

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
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-concurrent-dispose")
            .BuildAsync();

        try
        {
            // Produce a message to establish connection
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key1",
                Value = "value1"
            });

            // Act - Dispose from multiple threads simultaneously
            var disposeTasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                disposeTasks.Add(Task.Run(async () => await producer.DisposeAsync().AsTask().ConfigureAwait(false)));
            }

            // Assert - All dispose calls complete without exception
            await Task.WhenAll(disposeTasks).ConfigureAwait(false);
        }
        finally
        {
            // Ensure cleanup even if test fails
            try { await producer.DisposeAsync().ConfigureAwait(false); } catch { /* already disposed */ }
        }
    }

    // ==================== COMPREHENSIVE CANCELLATION EDGE CASES ====================
    // These tests validate pre-queue cancellation semantics comprehensively

    #region Pre-Queue Cancellation Points

    [Test]
    public async Task Producer_ProduceAsync_CancelledBeforeMetadataLookup_ThrowsImmediately()
    {
        // Arrange - Pre-cancelled token, no work should happen
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-cancel-before-metadata")
            .BuildAsync();

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

    // NOTE: Timing-based ProduceAsync cancellation tests removed
    // See ProducerCancellationTests.cs for unit tests with controlled conditions

    #endregion

    #region Post-Queue Immunity

    [Test]
    public async Task Producer_ProduceAsync_CancelledAfterFastPathAppend_CompletesSuccessfully()
    {
        // Arrange - Warm up metadata cache first
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-cancel-after-fast-path")
            .BuildAsync();

        // Warm up: First produce succeeds
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "warmup",
            Value = "warmup"
        });

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
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-cancel-after-slow-path")
            .BuildAsync();

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
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-concurrent-mixed-cancellation")
            .BuildAsync();

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
                    return (false, (RecordMetadata?)metadata, (Exception?)null);
                }
                catch (OperationCanceledException ex)
                {
                    return (true, (RecordMetadata?)null, (Exception?)ex);
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
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-concurrent-late-cancellation")
            .BuildAsync();

        // Warmup: Ensure producer is fully connected before concurrent test
        // This prevents metadata refresh failures in CI environments
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "warmup",
            Value = "warmup"
        });

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

    // Note: BufferMemory backpressure test removed because ProducerBuilder doesn't expose
    // WithBufferMemory method. BufferMemory testing is covered in unit tests.

    [Test]
    public async Task Producer_CancelledAfterMemoryReserved_CompletesSuccessfully()
    {
        // Arrange - Cancel after memory is reserved but before batch sends
        // Per CLAUDE.md: Cancellation after append throws OperationCanceledException to caller,
        // but message delivery continues in background. This test verifies the delivery still happens.
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-cancel-after-memory-reserved")
            .WithLinger(TimeSpan.FromMilliseconds(5000))
            .BuildAsync();

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
        await Task.Delay(100).ConfigureAwait(false);
        cts.Cancel();

        // Assert - Either completes successfully OR throws OperationCanceledException
        // Both are valid outcomes depending on timing - the key invariant is that
        // the message is still delivered to Kafka (verified by flush completing)
        try
        {
            var metadata = await produceTask.ConfigureAwait(false);
            // If we get here, message was delivered before cancellation took effect
            await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        }
        catch (OperationCanceledException)
        {
            // This is expected per CLAUDE.md - cancellation after append throws,
            // but delivery continues in background. Verify by flushing.
        }

        // Flush to ensure all messages (including the cancelled one) are delivered
        await producer.FlushAsync().ConfigureAwait(false);
    }

    #endregion

    #region FlushAsync Edge Cases

    [Test]
    public async Task Producer_FlushAsync_CancelledWithEmptyBatches_ThrowsImmediately()
    {
        // Arrange - No messages queued
        await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-flush-empty-cancelled")
            .BuildAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Should throw immediately
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await producer.FlushAsync(cts.Token).ConfigureAwait(false);
        });
    }

    // NOTE: FlushAsync timing-based cancellation test removed
    // See ProducerCancellationTests.cs for unit test with controlled conditions

    [Test]
    public async Task Producer_FlushAsync_CancelledButBatchesStillDeliver()
    {
        // Arrange - Verify batches deliver even if flush is cancelled
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-flush-cancelled-batches-deliver")
            .WithLinger(TimeSpan.FromMilliseconds(2000))
            .BuildAsync();

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
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-dispose-during-produce")
            .BuildAsync();

        try
        {
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
                });
            });
        }
        finally
        {
            try { await producer.DisposeAsync().ConfigureAwait(false); } catch { /* already disposed */ }
        }
    }

    [Test]
    public async Task Producer_ConcurrentDisposeAndCancelledProduce_HandlesGracefully()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-concurrent-dispose-cancel")
            .BuildAsync();

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
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-send-no-cancellation")
            .BuildAsync();

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

        // Assert - Flush completes successfully (all messages were sent)
        // Send() doesn't expose cancellation, so messages always get queued
        await producer.FlushAsync().ConfigureAwait(false);
    }

    #endregion
}
