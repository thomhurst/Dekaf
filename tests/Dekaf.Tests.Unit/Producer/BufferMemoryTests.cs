using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for BufferMemory accounting in the arena fast path.
/// These tests verify that the fast path correctly reserves and releases memory.
/// </summary>
public class BufferMemoryTests
{
    private static ProducerOptions CreateTestOptions(ulong bufferMemory = 10_000_000)
    {
        return new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = bufferMemory,
            BatchSize = 16384,
            LingerMs = 100
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

            var result1 = accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null);

            await Assert.That(result1).IsTrue();

            var result2 = accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null);

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

            accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null);

            accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null);

            // Verify memory is reserved
            var bufferedBytesBeforeRelease = accumulator.BufferedBytes;
            await Assert.That(bufferedBytesBeforeRelease).IsGreaterThan(0);

            // Get the batch
            if (accumulator.TryGetBatch("test-topic", 0, out var batch))
            {
                var batchSize = batch!.EstimatedSize;

                // Simulate the batch being sent - release the memory
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

            accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null);

            accumulator.TryAppendFireAndForget(
                "test-topic", 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null);

            accumulator.TryAppendFireAndForget(
                "test-topic", 2, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null);

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
                var result = accumulator.TryAppendFireAndForget(
                    "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, null);

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
            accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null);

            var bufferedAfterFirst = accumulator.BufferedBytes;
            await Assert.That(bufferedAfterFirst).IsGreaterThan(0);

            // Second batch to different partition
            accumulator.TryAppendFireAndForget(
                "test-topic", 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null);

            var bufferedAfterSecond = accumulator.BufferedBytes;
            await Assert.That(bufferedAfterSecond).IsGreaterThan(bufferedAfterFirst);

            // Release first batch
            if (accumulator.TryGetBatch("test-topic", 0, out var batch1))
            {
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
    public async Task BufferMemory_ThrowsTimeoutException_WhenExhausted()
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

                    accumulator.TryAppendFireAndForget(
                        "test-topic",
                        0,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey,
                        pooledValue,
                        null,
                        null);
                    messageCount++;
                }
            }
            catch (TimeoutException)
            {
                timeoutThrown = true;
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
        var options = CreateTestOptions(1000); // 1KB limit
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act: Try to append message larger than total buffer
            var largeValue = ArrayPool<byte>.Shared.Rent(2000);
            try
            {
                var pooledKey = new PooledMemory(null, 0, isNull: true);
                var pooledValue = new PooledMemory(largeValue, 2000);

                var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
                {
                    accumulator.TryAppendFireAndForget(
                        "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey, pooledValue, null, null);
                    await Task.CompletedTask.ConfigureAwait(false);
                });

                // Assert: Should throw with descriptive message about max.block.ms
                await Assert.That(exception!.Message).Contains("max.block.ms");
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
        var options = CreateTestOptions(0);
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act & Assert: Any append should fail immediately
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                accumulator.TryAppendFireAndForget(
                    "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, null);
                await Task.CompletedTask.ConfigureAwait(false);
            });
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
        var options = CreateTestOptions(100);
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
                    accumulator.TryAppendFireAndForget(
                        "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey, pooledValue, null, null);
                    successCount++;
                }
            }
            catch (TimeoutException)
            {
                threwTimeout = true;
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
            var resultAfterDisposal = accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, null);

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
            accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new PooledMemory(null, 0, isNull: true),
                new PooledMemory(null, 0, isNull: true),
                null, null);
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
            accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new PooledMemory(null, 0, isNull: true),
                new PooledMemory(null, 0, isNull: true),
                null, null);

            // Start background task to drain batches (simulates sender loop)
            using var cts = new CancellationTokenSource(15000);
            var doneTaskWasCompleted = false;
            var drainTask = Task.Run(async () =>
            {
                await foreach (var batch in accumulator.ReadyBatches.ReadAllAsync(cts.Token))
                {
                    // Simulate sender loop: complete delivery
                    batch.CompleteDelivery();

                    // Capture DoneTask state BEFORE returning to pool (which resets it)
                    doneTaskWasCompleted = batch.DoneTask.IsCompleted;

                    // Exit pipeline and return to pool
                    accumulator.OnBatchExitsPipeline();
                    accumulator.ReturnReadyBatch(batch);
                    break; // Only expect one batch
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
                accumulator.TryAppendFireAndForget(
                    "test-topic", i, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new PooledMemory(null, 0, isNull: true),
                    new PooledMemory(null, 0, isNull: true),
                    null, null);
            }

            // Start background task to drain batches (simulates sender loop)
            // Use 15s timeout to accommodate slower CI runners (Windows especially)
            using var cts = new CancellationTokenSource(15000);
            var receivedCount = 0;
            var drainTask = Task.Run(async () =>
            {
                await foreach (var batch in accumulator.ReadyBatches.ReadAllAsync(cts.Token))
                {
                    // Increment count BEFORE exiting pipeline to avoid race with FlushAsync
                    Interlocked.Increment(ref receivedCount);

                    batch.CompleteDelivery();
                    accumulator.OnBatchExitsPipeline();
                    batch.CompleteSend(0, DateTimeOffset.UtcNow);
                    accumulator.ReturnReadyBatch(batch);

                    if (Volatile.Read(ref receivedCount) >= batchCount)
                        break;
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
                accumulator.TryAppendFireAndForget(
                    "test-topic", i % 5, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new PooledMemory(null, 0, isNull: true),
                    new PooledMemory(null, 0, isNull: true),
                    null, null);
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
                await foreach (var batch in accumulator.ReadyBatches.ReadAllAsync(cts.Token))
                {
                    batch.CompleteDelivery();
                    accumulator.OnBatchExitsPipeline();
                    accumulator.ReturnReadyBatch(batch);
                }
            }, cts.Token);

            // FlushAsync should complete quickly once batches are drained
            var sw = Stopwatch.StartNew();
            await accumulator.FlushAsync(cts.Token);
            sw.Stop();

            await cts.CancelAsync();

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
                accumulator.TryAppendFireAndForget(
                    "test-topic", i, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new PooledMemory(null, 0, isNull: true),
                    new PooledMemory(null, 0, isNull: true),
                    null, null);
            }

            // Start background task to drain batches (simulates sender loop)
            using var cts = new CancellationTokenSource(15000);
            var receivedBatches = 0;
            var drainTask = Task.Run(async () =>
            {
                await foreach (var batch in accumulator.ReadyBatches.ReadAllAsync(cts.Token))
                {
                    receivedBatches++;
                    batch.CompleteDelivery();
                    accumulator.OnBatchExitsPipeline();
                    batch.CompleteSend(0, DateTimeOffset.UtcNow);
                    accumulator.ReturnReadyBatch(batch);
                    if (receivedBatches >= batchCount)
                        break;
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
            accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new PooledMemory(null, 0, isNull: true),
                new PooledMemory(null, 0, isNull: true),
                null, null);
        }

        // Disposal should complete quickly (completion loop stops)
        var sw = Stopwatch.StartNew();
        await accumulator.DisposeAsync();
        sw.Stop();

        // Should complete in under 5 seconds (allow for CI variability)
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(5000);
    }

}
