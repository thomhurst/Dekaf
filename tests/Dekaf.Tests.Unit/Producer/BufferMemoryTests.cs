using System.Buffers;
using System.Collections.Concurrent;
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
            await accumulator.DisposeAsync();
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
            await accumulator.DisposeAsync();
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
            await accumulator.DisposeAsync();
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
            await accumulator.DisposeAsync();
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
            await accumulator.DisposeAsync();
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
            DeliveryTimeoutMs = 500 // Short timeout - 500ms
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

            await accumulator.DisposeAsync();
        }
    }

    // ==================== EDGE CASE TESTS ====================

    [Test]
    public async Task BufferMemory_ConcurrentAppends_ThreadSafe()
    {
        // Arrange: Test concurrent appends from multiple threads to different partitions
        var options = CreateTestOptions(10_000_000); // 10MB
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act: Spawn 10 threads, each appending to different partition
            var tasks = new List<Task>();
            var exceptions = new ConcurrentBag<Exception>();

            for (int threadId = 0; threadId < 10; threadId++)
            {
                var partition = threadId;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var pooledKey = new PooledMemory(null, 0, isNull: true);
                        var pooledValue = new PooledMemory(null, 0, isNull: true);

                        for (int i = 0; i < 100; i++)
                        {
                            accumulator.TryAppendFireAndForget(
                                "test-topic", partition, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                pooledKey, pooledValue, null, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert: No exceptions, memory tracked correctly
            await Assert.That(exceptions).IsEmpty();
            await Assert.That(accumulator.BufferedBytes).IsGreaterThan(0);
            await Assert.That((ulong)accumulator.BufferedBytes).IsLessThanOrEqualTo(options.BufferMemory);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task BufferMemory_ConcurrentAppendsToSamePartition_ThreadSafe()
    {
        // Arrange: Test concurrent appends from multiple threads to SAME partition
        var options = CreateTestOptions(10_000_000); // 10MB
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Act: Spawn 10 threads, all appending to partition 0
            var tasks = new List<Task>();
            var exceptions = new ConcurrentBag<Exception>();

            for (int threadId = 0; threadId < 10; threadId++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var pooledKey = new PooledMemory(null, 0, isNull: true);
                        var pooledValue = new PooledMemory(null, 0, isNull: true);

                        for (int i = 0; i < 50; i++)
                        {
                            accumulator.TryAppendFireAndForget(
                                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                pooledKey, pooledValue, null, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert: No exceptions, memory tracked correctly
            await Assert.That(exceptions).IsEmpty();
            await Assert.That(accumulator.BufferedBytes).IsGreaterThan(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

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
                    await Task.CompletedTask;
                });

                // Assert: Should throw immediately, not wait for timeout
                await Assert.That(exception!.Message).Contains("buffer memory");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(largeValue);
            }
        }
        finally
        {
            await accumulator.DisposeAsync();
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
                await Task.CompletedTask;
            });
        }
        finally
        {
            await accumulator.DisposeAsync();
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
            await accumulator.DisposeAsync();
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
            await accumulator.DisposeAsync();

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

    [Test]
    public async Task BufferMemory_DisposalDuringMemoryWait_ThrowsOperationCanceled()
    {
        // Arrange: Create accumulator with tiny buffer to force blocking
        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 200, // Small buffer - 200 bytes
            BatchSize = 16384,
            LingerMs = 100,
            DeliveryTimeoutMs = 30_000 // 30 second timeout (should not be reached)
        };

        var accumulator = new RecordAccumulator(options);
        var rentedArrays = new List<byte[]>();

        try
        {
            // Fill the buffer completely - use single large message to ensure it fills
            var pooledKey = new PooledMemory(null, 0, isNull: true);

            // Add one message that fills most of the buffer (200 byte limit)
            // EstimateRecordSize adds ~20 bytes overhead, so 70 bytes value = ~90 bytes total
            var firstValueBytes = ArrayPool<byte>.Shared.Rent(70);
            rentedArrays.Add(firstValueBytes);
            var firstPooledValue = new PooledMemory(firstValueBytes, 70);

            accumulator.TryAppendFireAndForget(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, firstPooledValue, null, null);

            // Buffer should now be ~90 bytes used out of 200

            // Start a task that will block waiting for memory
            var startTime = Environment.TickCount64;
            var blockingTask = Task.Run(() =>
            {
                // Try to add two more large messages - this will exceed buffer and block
                var valueBytes1 = ArrayPool<byte>.Shared.Rent(70);
                lock (rentedArrays) { rentedArrays.Add(valueBytes1); }
                var pooledValue1 = new PooledMemory(valueBytes1, 70);

                accumulator.TryAppendFireAndForget(
                    "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue1, null, null);

                // Second message - this one should block because buffer is full
                var valueBytes2 = ArrayPool<byte>.Shared.Rent(70);
                lock (rentedArrays) { rentedArrays.Add(valueBytes2); }
                var pooledValue2 = new PooledMemory(valueBytes2, 70);

                accumulator.TryAppendFireAndForget(
                    "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue2, null, null);
            });

            // Give the blocking task time to actually start blocking
            await Task.Delay(200);

            // Act: Dispose while the task is blocked waiting for memory
            await accumulator.DisposeAsync();

            // Assert: The blocking task should throw OperationCanceledException promptly
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await blockingTask;
            });

            var elapsedMs = Environment.TickCount64 - startTime;

            // Verify disposal was prompt (< 2 seconds), not the full 30 second timeout
            await Assert.That(elapsedMs).IsLessThan(2000);

            // Verify the exception indicates cancellation
            await Assert.That(exception).IsNotNull();
        }
        finally
        {
            // Return all rented arrays to pool
            foreach (var array in rentedArrays)
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }
    }

}
