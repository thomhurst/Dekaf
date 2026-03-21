using System.Buffers;
using System.Diagnostics;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for BufferMemory accounting in the arena fast path.
/// These tests verify that the fast path correctly reserves and releases memory.
/// </summary>
public class BufferMemoryTests
{
    private static ProducerOptions CreateTestOptions(ulong bufferMemory = 10_000_000, int maxBlockMs = 60000)
    {
        return new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = bufferMemory,
            BatchSize = 16384,
            LingerMs = 100,
            MaxBlockMs = maxBlockMs
        };
    }

    [Test]
    public async Task BufferMemory_FireAndForget_ReservesMemory()
    {
        // Arrange
        var options = CreateTestOptions(10_000_000); // 10MB
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Verify starting state
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(0);

            // Act: Fire-and-forget append
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            var result1 = accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);

            await Assert.That(result1).IsTrue();

            var result2 = accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);

            await Assert.That(result2).IsTrue();

            // Assert: Verify memory was reserved
            var bufferedBytes = accumulator.BufferedBytes;
            await Assert.That(bufferedBytes).IsGreaterThan(0);

            // Verify it's within the limit
            await Assert.That((ulong)bufferedBytes).IsLessThanOrEqualTo(options.BufferMemory);
        }
        finally
        {
            await accumulator.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task BufferMemory_FireAndForget_ReleasesOnBatchRelease()
    {
        // Arrange
        var options = CreateTestOptions(10_000_000); // 10MB
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act: Append messages
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);

            accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);

            // Verify memory is reserved
            var bufferedBytesBeforeRelease = accumulator.BufferedBytes;
            await Assert.That(bufferedBytesBeforeRelease).IsGreaterThan(0);

            // Get the batch
            if (accumulator.TryGetBatch("test-topic", 0, out var batch))
            {
                var batchSize = batch!.EstimatedSize;

                // Simulate the batch being sent - release the memory.
                // In production, CurrentBatch is set to null when sealed.
                // We must do the same to prevent DisposeAsync from double-releasing.
                var dequesField = typeof(RecordAccumulator).GetField("_partitionDeques",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var deques = dequesField!.GetValue(accumulator)!;
                var tryGetMethod = deques.GetType().GetMethod("TryGetValue");
                var parms = new object[] { new TopicPartition("test-topic", 0), null! };
                tryGetMethod!.Invoke(deques, parms);
                var pd = parms[1]!;
                pd.GetType().GetField("CurrentBatch")!.SetValue(pd, null);

                accumulator.ReleaseMemory(batchSize);

                // Assert: Verify memory was released
                var bufferedBytesAfterRelease = accumulator.BufferedBytes;
                await Assert.That(bufferedBytesAfterRelease).IsEqualTo(0);
            }
            else
            {
                throw new InvalidOperationException("Expected batch to exist");
            }
        }
        finally
        {
            await accumulator.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task BufferMemory_MultiplePartitions_IndependentAccounting()
    {
        // Arrange
        var options = CreateTestOptions(10_000_000); // 10MB
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act: Append to multiple partitions
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);

            accumulator.Append(
                "test-topic", 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);

            accumulator.Append(
                "test-topic", 2, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);

            // Assert: Verify memory was reserved for all partitions
            var bufferedBytes = accumulator.BufferedBytes;
            await Assert.That(bufferedBytes).IsGreaterThan(0);

            // Each partition should have its own batch
            await Assert.That(accumulator.TryGetBatch("test-topic", 0, out _)).IsTrue();
            await Assert.That(accumulator.TryGetBatch("test-topic", 1, out _)).IsTrue();
            await Assert.That(accumulator.TryGetBatch("test-topic", 2, out _)).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task BufferMemory_ConsecutiveMessagesToSamePartition_AccumulatesCorrectly()
    {
        // Arrange
        var options = CreateTestOptions(10_000_000); // 10MB
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act: Append multiple messages to the same partition
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Append 10 messages
            for (int i = 0; i < 10; i++)
            {
                var result = accumulator.Append(
                    "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, null, null);

                await Assert.That(result).IsTrue();
            }

            // Assert: Verify memory was reserved for all messages
            var bufferedBytes = accumulator.BufferedBytes;
            await Assert.That(bufferedBytes).IsGreaterThan(0);

            // Verify batch exists
            if (accumulator.TryGetBatch("test-topic", 0, out var batch))
            {
                // The batch should have data
                await Assert.That(batch!.EstimatedSize).IsGreaterThan(0);
            }
            else
            {
                throw new InvalidOperationException("Expected batch to exist");
            }
        }
        finally
        {
            await accumulator.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task BufferMemory_TracksCorrectly_AcrossMultipleOperations()
    {
        // Arrange
        var options = CreateTestOptions(10_000_000); // 10MB
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act: Append, release, append again
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // First batch
            accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);

            var bufferedAfterFirst = accumulator.BufferedBytes;
            await Assert.That(bufferedAfterFirst).IsGreaterThan(0);

            // Second batch to different partition
            accumulator.Append(
                "test-topic", 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);

            var bufferedAfterSecond = accumulator.BufferedBytes;
            await Assert.That(bufferedAfterSecond).IsGreaterThan(bufferedAfterFirst);

            // Release first batch - also clear CurrentBatch to prevent double-release
            // during disposal (in production, CurrentBatch is nulled when sealed)
            if (accumulator.TryGetBatch("test-topic", 0, out var batch1))
            {
                var dequesField = typeof(RecordAccumulator).GetField("_partitionDeques",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var deques = dequesField!.GetValue(accumulator)!;
                var tryGetMethod = deques.GetType().GetMethod("TryGetValue");
                var parms = new object[] { new TopicPartition("test-topic", 0), null! };
                tryGetMethod!.Invoke(deques, parms);
                var pd = parms[1]!;
                pd.GetType().GetField("CurrentBatch")!.SetValue(pd, null);

                accumulator.ReleaseMemory(batch1!.EstimatedSize);
            }

            var bufferedAfterRelease = accumulator.BufferedBytes;
            await Assert.That(bufferedAfterRelease).IsLessThan(bufferedAfterSecond);

            // Verify accounting stayed correct
            await Assert.That(bufferedAfterRelease).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task BufferMemory_ThrowsKafkaTimeoutException_WhenExhausted()
    {
        // Arrange: Create producer with very small buffer and short timeout
        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 1000, // Very small buffer - 1KB
            BatchSize = 16384,
            LingerMs = 100,
            MaxBlockMs = 500 // Short timeout - 500ms (controls buffer wait timeout)
        };

        var accumulator = new RecordAccumulator(options);

        var rentedArrays = new List<byte[]>();
        try
        {
            // Act: Fill the buffer by appending many messages
            var pooledKey = new PooledMemory(null, 0, isNull: true);

            var messageCount = 0;
            var startTime = Environment.TickCount64;
            var timeoutThrown = false;

            // Keep appending until we get a timeout
            // The timeout should occur because we're not draining batches (no sender loop)
            try
            {
                for (int i = 0; i < 100; i++) // Try to append many messages
                {
                    // Rent from ArrayPool for each message to avoid reusing the same array
                    var valueBytes = ArrayPool<byte>.Shared.Rent(200);
                    rentedArrays.Add(valueBytes); // Track for cleanup
                    var pooledValue = new PooledMemory(valueBytes, 200);

                    accumulator.Append(
                        "test-topic",
                        0,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey,
                        pooledValue,
                        null,
                        0, null, null);
                    messageCount++;
                }
            }
            catch (KafkaTimeoutException ex)
            {
                timeoutThrown = true;
                await Assert.That(ex.TimeoutKind).IsEqualTo(TimeoutKind.MaxBlock);
                await Assert.That(ex.Configured).IsEqualTo(TimeSpan.FromMilliseconds(500));
                await Assert.That(ex.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.Zero);
                await Assert.That(ex.Elapsed).IsLessThanOrEqualTo(ex.Configured + TimeSpan.FromSeconds(5));
            }

            var elapsedMs = Environment.TickCount64 - startTime;

            // Verify timeout was thrown
            await Assert.That(timeoutThrown).IsTrue();

            // Verify timeout occurred within reasonable time (should be ~500ms + some overhead)
            await Assert.That(elapsedMs).IsGreaterThanOrEqualTo(400); // At least 400ms
            await Assert.That(elapsedMs).IsLessThan(3000); // More generous for slow CI (< 3s)

            // Verify we filled the buffer before timing out
            await Assert.That(messageCount).IsGreaterThan(0);
        }
        finally
        {
            // Return all rented arrays to pool
            foreach (var array in rentedArrays)
            {
                ArrayPool<byte>.Shared.Return(array);
            }

            await accumulator.DisposeAsync().ConfigureAwait(false);
        }
    }

    // ==================== EDGE CASE TESTS ====================

    // NOTE: Concurrent append tests removed
    // TryAppendFireAndForget() is fire-and-forget - creates background tasks that can't be properly awaited
    // Causes unobserved task exceptions when RecordAccumulator is disposed while background work is running
    // Thread-safety is still tested indirectly through integration tests with concurrent producers

    [Test]
    public async Task BufferMemory_MessageLargerThanLimit_ThrowsImmediately()
    {
        // Arrange: Buffer limit 1KB, message 2KB
        var options = CreateTestOptions(1000, maxBlockMs: 100); // 1KB limit, short timeout
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act: Try to append message larger than total buffer
            var largeValue = ArrayPool<byte>.Shared.Rent(2000);
            try
            {
                var pooledKey = new PooledMemory(null, 0, isNull: true);
                var pooledValue = new PooledMemory(largeValue, 2000);

                var exception = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
                {
                    accumulator.Append(
                        "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey, pooledValue, null, 0, null, null);
                    await Task.CompletedTask.ConfigureAwait(false);
                });

                // Assert: Should throw with descriptive message about max.block.ms
                await Assert.That(exception!.Message).Contains("max.block.ms");
                await Assert.That(exception.TimeoutKind).IsEqualTo(TimeoutKind.MaxBlock);
                await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromMilliseconds(100));
                await Assert.That(exception.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.Zero);
                await Assert.That(exception.Elapsed).IsLessThanOrEqualTo(exception.Configured + TimeSpan.FromSeconds(5));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(largeValue);
            }
        }
        finally
        {
            await accumulator.DisposeAsync().ConfigureAwait(false);
        }
    }


    [Test]
    public async Task BufferMemory_ZeroLimit_AllAppendsThrowImmediately()
    {
        // Arrange: Zero buffer memory (edge case)
        var options = CreateTestOptions(0, maxBlockMs: 100);
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act & Assert: Any append should fail immediately
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            var ex = await Assert.ThrowsAsync<KafkaTimeoutException>(async () =>
            {
                accumulator.Append(
                    "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, null, null);
                await Task.CompletedTask.ConfigureAwait(false);
            });

            await Assert.That(ex!.TimeoutKind).IsEqualTo(TimeoutKind.MaxBlock);
            await Assert.That(ex.Configured).IsEqualTo(TimeSpan.FromMilliseconds(100));
            await Assert.That(ex.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.Zero);
        }
        finally
        {
            await accumulator.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task BufferMemory_MinimalLimit_EnforcesStrictBackpressure()
    {
        // Arrange: Minimal buffer (100 bytes)
        var options = CreateTestOptions(100, maxBlockMs: 100);
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act: Append small messages until buffer is full
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            var successCount = 0;
            var threwTimeout = false;

            try
            {
                for (int i = 0; i < 20; i++)
                {
                    accumulator.Append(
                        "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey, pooledValue, null, 0, null, null);
                    successCount++;
                }
            }
            catch (KafkaTimeoutException ex)
            {
                threwTimeout = true;
                await Assert.That(ex.TimeoutKind).IsEqualTo(TimeoutKind.MaxBlock);
                await Assert.That(ex.Configured).IsEqualTo(TimeSpan.FromMilliseconds(100));
                await Assert.That(ex.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.Zero);
                await Assert.That(ex.Elapsed).IsLessThanOrEqualTo(ex.Configured + TimeSpan.FromSeconds(5));
            }

            // Assert: Should eventually hit backpressure
            await Assert.That(threwTimeout).IsTrue();
            await Assert.That(successCount).IsGreaterThan(0); // At least some succeeded
            await Assert.That((ulong)accumulator.BufferedBytes).IsLessThanOrEqualTo(options.BufferMemory);
        }
        finally
        {
            await accumulator.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task BufferMemory_AfterDisposal_ReturnsFailure()
    {
        // Arrange: Test that TryAppendFireAndForget returns false after disposal
        var options = CreateTestOptions(10_000_000);
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Dispose immediately without filling buffer
            await accumulator.DisposeAsync().ConfigureAwait(false);

            // After disposal, TryAppendFireAndForget should return false immediately
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);
            var resultAfterDisposal = accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);

            await Assert.That(resultAfterDisposal).IsFalse();
        }
        finally
        {
            // Already disposed in test
        }
    }

    // NOTE: BufferMemory_DisposalDuringMemoryWait test removed
    // Test was inherently flaky - caused unobserved task exceptions in CI
    // Background tasks in RecordAccumulator continue running after DisposeAsync() completes,
    // which is expected for prompt disposal but creates GC finalization issues in tests.
    // The behavior (prompt disposal without 30-second hang) is still tested by integration tests.

    // ==================== COMPLETION LOOP ARCHITECTURE TESTS ====================

    [Test]
    public async Task RecordAccumulator_DisposeWithPendingFireAndForget_CompletesGracefully()
    {
        // This test verifies that disposal with pending fire-and-forget batches
        // completes gracefully via the completion loop

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);

        // Add fire-and-forget messages
        for (int i = 0; i < 10; i++)
        {
            accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new PooledMemory(null, 0, isNull: true),
                new PooledMemory(null, 0, isNull: true),
                null, 0, null, null);
        }

        // Dispose should complete quickly (completion loop processes batches)
        var sw = Stopwatch.StartNew();
        await accumulator.DisposeAsync();
        sw.Stop();

        // Should complete in under 5 seconds (allow for CI variability)
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(5000);
    }

    [Test]
    public async Task ReadyBatch_TwoPhaseCompletion_DeliveryCompletesBeforeSend()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 10_000_000,
            BatchSize = 16384,
            LingerMs = 50 // Short linger
        };
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Append to create batch
            accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new PooledMemory(null, 0, isNull: true),
                new PooledMemory(null, 0, isNull: true),
                null, 0, null, null);

            // Start background task to drain batches (simulates sender loop)
            using var cts = new CancellationTokenSource(15000);
            var doneTaskWasCompleted = false;
            var drainTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (accumulator.TryDrainBatch(out var batch))
                    {
                        // Simulate sender loop: complete delivery
                        batch.CompleteDelivery();

                        // Capture DoneTask state BEFORE returning to pool (which resets it)
                        doneTaskWasCompleted = batch.DoneTask.IsCompleted;

                        // Exit pipeline and return to pool
                        accumulator.OnBatchExitsPipeline(batch);
                        accumulator.ReturnReadyBatch(batch);
                        break; // Only expect one batch
                    }
                    else
                    {
                        await accumulator.WaitForWakeupAsync(100);
                    }
                }
            }, cts.Token);

            // Flush to make batch ready immediately (bypasses linger)
            // FlushAsync completes when all batches exit pipeline
            await accumulator.FlushAsync(cts.Token);

            // Cancel drain task since flush completed
            await cts.CancelAsync();

            // DoneTask should have been completed by CompleteDelivery
            await Assert.That(doneTaskWasCompleted).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task CompletionLoop_ProcessesAllBatches_BeforeDisposal()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 10_000_000,
            BatchSize = 16384,
            LingerMs = 10
        };
        var accumulator = new RecordAccumulator(options);

        try
        {
            var batchCount = 10;

            // Create multiple batches across different partitions
            for (int i = 0; i < batchCount; i++)
            {
                accumulator.Append(
                    "test-topic", i, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new PooledMemory(null, 0, isNull: true),
                    new PooledMemory(null, 0, isNull: true),
                    null, 0, null, null);
            }

            // Start background task to drain batches (simulates sender loop)
            // Use 15s timeout to accommodate slower CI runners (Windows especially)
            using var cts = new CancellationTokenSource(15000);
            var receivedCount = 0;
            var drainTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (accumulator.TryDrainBatch(out var batch))
                    {
                        // Increment count BEFORE exiting pipeline to avoid race with FlushAsync
                        Interlocked.Increment(ref receivedCount);

                        batch.CompleteDelivery();
                        accumulator.OnBatchExitsPipeline(batch);
                        batch.CompleteSend(0, DateTimeOffset.UtcNow);
                        accumulator.ReturnReadyBatch(batch);

                        if (Volatile.Read(ref receivedCount) >= batchCount)
                            break;
                    }
                    else
                    {
                        await accumulator.WaitForWakeupAsync(100);
                    }
                }
            }, cts.Token);

            // Flush to make all batches ready (bypasses linger)
            // FlushAsync completes when all batches exit pipeline
            await accumulator.FlushAsync(cts.Token);

            // Cancel drain task since flush completed
            await cts.CancelAsync();

            await Assert.That(Volatile.Read(ref receivedCount)).IsEqualTo(batchCount);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task FlushAsync_WithSimulatedSenderLoop_CompletesSuccessfully()
    {
        // FlushAsync completes when all batches exit the pipeline
        // This requires a sender loop (or simulated one) to drain batches

        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 10_000_000,
            BatchSize = 16384,
            LingerMs = 100  // Use small linger so batches become ready quickly
        };
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Add multiple fire-and-forget messages across 5 partitions
            for (int i = 0; i < 50; i++)
            {
                accumulator.Append(
                    "test-topic", i % 5, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new PooledMemory(null, 0, isNull: true),
                    new PooledMemory(null, 0, isNull: true),
                    null, 0, null, null);
            }

            // Start background task to drain batches (simulates sender loop)
            using var cts = new CancellationTokenSource(15000);

            // Simulate the sender loop's linger expiration - this makes batches ready
            var lingerTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await accumulator.ExpireLingerAsync(cts.Token).ConfigureAwait(false);
                    await Task.Delay(10, cts.Token).ConfigureAwait(false);
                }
            }, cts.Token);

            var drainTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (accumulator.TryDrainBatch(out var batch))
                    {
                        batch.CompleteDelivery();
                        accumulator.OnBatchExitsPipeline(batch);
                        accumulator.ReturnReadyBatch(batch);
                    }
                    else
                    {
                        await accumulator.WaitForWakeupAsync(100);
                    }
                }
            }, cts.Token);

            // FlushAsync should complete quickly once batches are drained
            var sw = Stopwatch.StartNew();
            await accumulator.FlushAsync(cts.Token);
            sw.Stop();

            await cts.CancelAsync();

            // Await background tasks to observe their cancellation exceptions,
            // preventing unobserved task exceptions after CTS disposal.
            try { await lingerTask; } catch (OperationCanceledException) { }
            try { await drainTask; } catch (OperationCanceledException) { }

            // Should complete quickly - using 5000ms to account for CI variability
            // (LingerMs=100 + some overhead for task scheduling)
            await Assert.That(sw.ElapsedMilliseconds).IsLessThan(5000);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task ReadyBatches_ReceivesAllBatchesFromCompletionLoop()
    {
        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 10_000_000,
            BatchSize = 16384,
            LingerMs = 10
        };
        var accumulator = new RecordAccumulator(options);

        try
        {
            var batchCount = 5;

            // Create batches
            for (int i = 0; i < batchCount; i++)
            {
                accumulator.Append(
                    "test-topic", i, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new PooledMemory(null, 0, isNull: true),
                    new PooledMemory(null, 0, isNull: true),
                    null, 0, null, null);
            }

            // Start background task to drain batches (simulates sender loop)
            using var cts = new CancellationTokenSource(15000);
            var receivedBatches = 0;
            var drainTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (accumulator.TryDrainBatch(out var batch))
                    {
                        receivedBatches++;
                        batch.CompleteDelivery();
                        accumulator.OnBatchExitsPipeline(batch);
                        batch.CompleteSend(0, DateTimeOffset.UtcNow);
                        accumulator.ReturnReadyBatch(batch);
                        if (receivedBatches >= batchCount)
                            break;
                    }
                    else
                    {
                        await accumulator.WaitForWakeupAsync(100);
                    }
                }
            }, cts.Token);

            // Flush to make batches ready (bypasses linger)
            await accumulator.FlushAsync(cts.Token);

            // Cancel and cleanup
            await cts.CancelAsync();

            await Assert.That(receivedBatches).IsEqualTo(batchCount);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task CompletionLoop_StopsGracefully_OnDisposal()
    {
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);

        // Add some messages
        for (int i = 0; i < 10; i++)
        {
            accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new PooledMemory(null, 0, isNull: true),
                new PooledMemory(null, 0, isNull: true),
                null, 0, null, null);
        }

        // Disposal should complete quickly (completion loop stops)
        var sw = Stopwatch.StartNew();
        await accumulator.DisposeAsync();
        sw.Stop();

        // Should complete in under 5 seconds (allow for CI variability)
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(5000);
    }

    [Test]
    public async Task ReserveMemorySync_AdaptiveBackpressure_AcquiresPromptlyAfterRelease()
    {
        // Verifies that after a semaphore wake-up, the adaptive spin budget
        // lets the thread acquire newly-freed memory without re-entering
        // the kernel wait. Without spinWait.Reset(), the thread would fall
        // straight back to the 100ms semaphore wait.

        // Small buffer: just enough for one batch overhead (~80 bytes)
        var options = CreateTestOptions(bufferMemory: 200, maxBlockMs: 5000);
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Fill the buffer
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);

            var bufferedBefore = accumulator.BufferedBytes;

            // Start a background task that will try to reserve more memory (will block)
            var reserveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var reserveTask = Task.Run(() =>
            {
                reserveStarted.SetResult();
                var sw = Stopwatch.StartNew();
                // This will block because buffer is full, then succeed after release
                accumulator.ReserveMemorySync(10);
                sw.Stop();
                return sw.ElapsedMilliseconds;
            });

            // Wait for the reserve task to start, then yield to let it enter the wait.
            // Using Task.Yield instead of Task.Delay(50) for deterministic synchronization;
            // the completion timeout below guards correctness regardless of scheduling.
            await reserveStarted.Task;
            await Task.Yield();

            // Release memory — this signals the semaphore
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory((int)bufferedBefore);

            // The reserve should complete promptly (well under 100ms after signal).
            // With adaptive backpressure (spinWait.Reset), the thread spins after
            // wake-up and catches the freed memory. Without it, the thread would
            // re-block for up to 100ms.
            var completedInTime = await Task.WhenAny(
                reserveTask,
                Task.Delay(2000) // generous timeout for CI
            ) == reserveTask;

            await Assert.That(completedInTime).IsTrue();

            var elapsedMs = await reserveTask;
            // Should be well under 1 second (typically < 100ms).
            // Using 1000ms as upper bound to account for slow CI runners.
            await Assert.That(elapsedMs).IsLessThan(1000);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

}
