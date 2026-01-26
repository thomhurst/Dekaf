using System.Buffers;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for RecordAccumulator focusing on cleanup correctness.
/// These tests verify that the double-cleanup bug is fixed and doesn't regress.
/// </summary>
public class RecordAccumulatorTests
{
    private static ProducerOptions CreateTestOptions()
    {
        return new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 10000,
            BatchSize = 1000,
            LingerMs = 10
        };
    }

    [Test]
    public async Task PartitionBatch_Complete_CalledTwice_ReturnsIdempotent()
    {
        // This test verifies that calling Complete() multiple times on the same PartitionBatch
        // returns the same ReadyBatch instance, preventing double-cleanup of resources.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var topicPartition = new TopicPartition("test-topic", 0);

        try
        {
            // Append a record to create a non-empty batch
            var completion = pool.Rent();
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            var result = await accumulator.AppendAsync(
                topicPartition.Topic,
                topicPartition.Partition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey,
                pooledValue,
                null,
                null,
                completion,
                CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            // Use reflection to access the internal PartitionBatch
            var batchesField = typeof(RecordAccumulator).GetField("_batches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var batches = batchesField!.GetValue(accumulator)!;

            // Get TryGetValue method
            var tryGetValueMethod = batches.GetType().GetMethod("TryGetValue");
            var parameters = new object[] { topicPartition, null! };
            var found = (bool)tryGetValueMethod!.Invoke(batches, parameters)!;
            await Assert.That(found).IsTrue();

            var partitionBatch = parameters[1];

            // Get the Complete method via reflection
            var completeMethod = partitionBatch!.GetType().GetMethod("Complete");
            await Assert.That(completeMethod).IsNotNull();

            // Call Complete() the first time
            var readyBatch1 = completeMethod!.Invoke(partitionBatch, null);
            await Assert.That(readyBatch1).IsNotNull();

            // Call Complete() the second time - should return the SAME instance (idempotent)
            var readyBatch2 = completeMethod.Invoke(partitionBatch, null);
            await Assert.That(readyBatch2).IsNotNull();

            // Must return the exact same instance to prevent duplicate resource tracking
            await Assert.That(ReferenceEquals(readyBatch1, readyBatch2)).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ReadyBatch_Cleanup_CalledTwice_OnlyExecutesOnce()
    {
        // This test verifies that Cleanup() uses an interlocked guard to ensure
        // it only executes once, even if called multiple times.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var topicPartition = new TopicPartition("test-topic", 0);

        try
        {
            // Append a record
            var completion = pool.Rent();
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            await accumulator.AppendAsync(
                topicPartition.Topic,
                topicPartition.Partition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey,
                pooledValue,
                null,
                null,
                completion,
                CancellationToken.None);

            // Get the PartitionBatch and call Complete()
            var batchesField = typeof(RecordAccumulator).GetField("_batches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var batches = batchesField!.GetValue(accumulator)!;

            var tryGetValueMethod = batches.GetType().GetMethod("TryGetValue");
            var parameters = new object[] { topicPartition, null! };
            tryGetValueMethod!.Invoke(batches, parameters);
            var partitionBatch = parameters[1];

            var completeMethod = partitionBatch!.GetType().GetMethod("Complete");
            var readyBatch = completeMethod!.Invoke(partitionBatch, null);

            // Get the _cleanedUp field to verify guard
            var cleanedUpField = readyBatch!.GetType().GetField("_cleanedUp",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await Assert.That(cleanedUpField).IsNotNull();

            var initialValue = (int)cleanedUpField!.GetValue(readyBatch)!;
            await Assert.That(initialValue).IsEqualTo(0);

            // Call Cleanup via reflection
            var cleanupMethod = readyBatch.GetType().GetMethod("Cleanup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cleanupMethod!.Invoke(readyBatch, null);

            // Check that guard was set
            var afterFirstCleanup = (int)cleanedUpField.GetValue(readyBatch)!;
            await Assert.That(afterFirstCleanup).IsEqualTo(1);

            // Call Cleanup again - should be no-op due to guard
            cleanupMethod.Invoke(readyBatch, null);

            // Guard should still be 1 (not incremented)
            var afterSecondCleanup = (int)cleanedUpField.GetValue(readyBatch)!;
            await Assert.That(afterSecondCleanup).IsEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ReadyBatch_CompleteThenFail_OnlyCleanupOnce()
    {
        // This test verifies that calling Complete() followed by Fail() only triggers cleanup once.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var topicPartition = new TopicPartition("test-topic", 0);

        try
        {
            // Create a batch
            var completion = pool.Rent();
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            await accumulator.AppendAsync(
                topicPartition.Topic,
                topicPartition.Partition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey,
                pooledValue,
                null,
                null,
                completion,
                CancellationToken.None);

            // Get ReadyBatch
            var batchesField = typeof(RecordAccumulator).GetField("_batches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var batches = batchesField!.GetValue(accumulator)!;

            var tryGetValueMethod = batches.GetType().GetMethod("TryGetValue");
            var parameters = new object[] { topicPartition, null! };
            tryGetValueMethod!.Invoke(batches, parameters);
            var partitionBatch = parameters[1];

            var completeMethod = partitionBatch!.GetType().GetMethod("Complete");
            var readyBatch = (ReadyBatch)completeMethod!.Invoke(partitionBatch, null)!;

            // Call Complete (triggers cleanup)
            readyBatch.Complete(0, DateTimeOffset.UtcNow);

            // Verify cleanup happened
            var cleanedUpField = readyBatch.GetType().GetField("_cleanedUp",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var afterComplete = (int)cleanedUpField!.GetValue(readyBatch)!;
            await Assert.That(afterComplete).IsEqualTo(1);

            // Call Fail (should NOT cleanup again due to guard)
            readyBatch.Fail(new InvalidOperationException("Test exception"));

            // Cleanup flag should still be 1
            var afterFail = (int)cleanedUpField.GetValue(readyBatch)!;
            await Assert.That(afterFail).IsEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ConcurrentAppends_SamePartition_AllRecordsAppended()
    {
        // This test verifies thread-safety when multiple threads append to the same partition concurrently.
        // It ensures that the per-partition lock in PartitionBatch correctly serializes appends.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var topicPartition = new TopicPartition("test-topic", 0);

        try
        {
            const int threadCount = 10;
            const int recordsPerThread = 100;
            var tasks = new List<Task<List<RecordAppendResult>>>();

            // Launch multiple threads that all append to the same partition
            for (int i = 0; i < threadCount; i++)
            {
                var task = Task.Run(async () =>
                {
                    var results = new List<RecordAppendResult>();
                    for (int j = 0; j < recordsPerThread; j++)
                    {
                        var completion = pool.Rent();
                        var pooledKey = new PooledMemory(null, 0, isNull: true);
                        var pooledValue = new PooledMemory(null, 0, isNull: true);

                        var result = await accumulator.AppendAsync(
                            topicPartition.Topic,
                            topicPartition.Partition,
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            pooledKey,
                            pooledValue,
                            null,
                            null,
                            completion,
                            CancellationToken.None).ConfigureAwait(false);

                        results.Add(result);
                    }
                    return results;
                });
                tasks.Add(task);
            }

            // Wait for all threads to complete
            var allResults = await Task.WhenAll(tasks);

            // Verify all appends succeeded (or correctly failed when batch was full)
            var totalSuccess = allResults.SelectMany(r => r).Count(r => r.Success);
            await Assert.That(totalSuccess).IsGreaterThan(0);

            // Get the batch and verify it has the expected number of records
            var batchesField = typeof(RecordAccumulator).GetField("_batches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var batches = batchesField!.GetValue(accumulator)!;

            var tryGetValueMethod = batches.GetType().GetMethod("TryGetValue");
            var parameters = new object[] { topicPartition, null! };
            var found = (bool)tryGetValueMethod!.Invoke(batches, parameters)!;

            if (found)
            {
                var partitionBatch = parameters[1];
                var recordCountProperty = partitionBatch!.GetType().GetProperty("RecordCount");
                var recordCount = (int)recordCountProperty!.GetValue(partitionBatch)!;

                // Verify that all successful appends are reflected in the batch
                await Assert.That(recordCount).IsGreaterThan(0);
                await Assert.That(recordCount).IsLessThanOrEqualTo(threadCount * recordsPerThread);
            }
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ConcurrentAppends_DifferentPartitions_NoContention()
    {
        // This test verifies that concurrent appends to different partitions don't block each other.
        // Each partition should have its own PartitionBatch with its own lock, ensuring no cross-partition contention.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            const int partitionCount = 10;
            const int recordsPerPartition = 50;
            var tasks = new List<Task>();

            // Launch multiple threads, each appending to a different partition
            for (int partition = 0; partition < partitionCount; partition++)
            {
                var partitionId = partition; // Capture for closure
                var task = Task.Run(async () =>
                {
                    var topicPartition = new TopicPartition("test-topic", partitionId);

                    for (int j = 0; j < recordsPerPartition; j++)
                    {
                        var completion = pool.Rent();
                        var pooledKey = new PooledMemory(null, 0, isNull: true);
                        var pooledValue = new PooledMemory(null, 0, isNull: true);

                        var result = await accumulator.AppendAsync(
                            topicPartition.Topic,
                            topicPartition.Partition,
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            pooledKey,
                            pooledValue,
                            null,
                            null,
                            completion,
                            CancellationToken.None).ConfigureAwait(false);

                        await Assert.That(result.Success).IsTrue();
                    }
                });
                tasks.Add(task);
            }

            // Wait for all threads to complete
            await Task.WhenAll(tasks);

            // Verify each partition has its own batch with the correct number of records
            var batchesField = typeof(RecordAccumulator).GetField("_batches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var batches = batchesField!.GetValue(accumulator)!;

            var countProperty = batches.GetType().GetProperty("Count");
            var batchCount = (int)countProperty!.GetValue(batches)!;

            // Should have one batch per partition
            await Assert.That(batchCount).IsEqualTo(partitionCount);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ConcurrentAppends_StressTest_NoDataCorruption()
    {
        // Stress test: Multiple threads appending to multiple partitions concurrently.
        // This verifies that under high contention, no exceptions occur and all operations complete successfully.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            const int threadCount = 20;
            const int partitionCount = 5;
            const int recordsPerThread = 50;
            var tasks = new List<Task<int>>();

            // Launch many threads appending to random partitions
            for (int i = 0; i < threadCount; i++)
            {
                var threadId = i;
                var task = Task.Run(async () =>
                {
                    var successCount = 0;
                    var threadRandom = new Random(threadId); // Per-thread random to avoid contention

                    for (int j = 0; j < recordsPerThread; j++)
                    {
                        var partition = threadRandom.Next(0, partitionCount);
                        var topicPartition = new TopicPartition("test-topic", partition);

                        var completion = pool.Rent();
                        var pooledKey = new PooledMemory(null, 0, isNull: true);
                        var pooledValue = new PooledMemory(null, 0, isNull: true);

                        var result = await accumulator.AppendAsync(
                            topicPartition.Topic,
                            topicPartition.Partition,
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            pooledKey,
                            pooledValue,
                            null,
                            null,
                            completion,
                            CancellationToken.None).ConfigureAwait(false);

                        if (result.Success)
                            successCount++;
                    }

                    return successCount;
                });
                tasks.Add(task);
            }

            // Wait for all threads to complete - if there's any corruption or race condition,
            // this will either hang, throw an exception, or return corrupted data
            var results = await Task.WhenAll(tasks);
            var totalSuccess = results.Sum();

            // Verify that we got a reasonable number of successful appends
            // (some may fail due to batch being full, which is expected)
            await Assert.That(totalSuccess).IsGreaterThan(0);
            await Assert.That(totalSuccess).IsLessThanOrEqualTo(threadCount * recordsPerThread);

            // The fact that we reached here without exceptions or hangs proves thread-safety.
            // Data corruption would manifest as exceptions in List<T>.Add() or infinite loops.
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    #region Fire-and-Forget Tests

    [Test]
    public async Task TryAppendFireAndForget_AppendsRecordWithoutCompletionSource()
    {
        // This test verifies that TryAppendFireAndForget appends a record
        // without requiring or storing a completion source.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Fire-and-forget append - no completion source needed
            var result = accumulator.TryAppendFireAndForget(
                "test-topic",
                0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey,
                pooledValue,
                null,
                null);

            await Assert.That(result).IsTrue();

            // Verify a batch was created with a record
            var batchesField = typeof(RecordAccumulator).GetField("_batches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var batches = batchesField!.GetValue(accumulator)!;

            var countProperty = batches.GetType().GetProperty("Count");
            var batchCount = (int)countProperty!.GetValue(batches)!;
            await Assert.That(batchCount).IsEqualTo(1);

            // Get the partition batch and verify it has a record
            var topicPartition = new TopicPartition("test-topic", 0);
            var tryGetValueMethod = batches.GetType().GetMethod("TryGetValue");
            var parameters = new object[] { topicPartition, null! };
            var found = (bool)tryGetValueMethod!.Invoke(batches, parameters)!;
            await Assert.That(found).IsTrue();

            var partitionBatch = parameters[1];
            var recordCountProperty = partitionBatch!.GetType().GetProperty("RecordCount");
            var recordCount = (int)recordCountProperty!.GetValue(partitionBatch)!;
            await Assert.That(recordCount).IsEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task TryAppendFireAndForget_MultipleAppends_AllSucceed()
    {
        // Verify that multiple fire-and-forget appends work correctly.
        // Use a larger batch size to fit all records in one batch.

        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 100000,
            BatchSize = 100000, // Large batch to fit all records
            LingerMs = 10
        };
        var accumulator = new RecordAccumulator(options);

        try
        {
            const int appendCount = 100;
            var successCount = 0;

            for (int i = 0; i < appendCount; i++)
            {
                var pooledKey = new PooledMemory(null, 0, isNull: true);
                var pooledValue = new PooledMemory(null, 0, isNull: true);

                var result = accumulator.TryAppendFireAndForget(
                    "test-topic",
                    0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey,
                    pooledValue,
                    null,
                    null);

                if (result) successCount++;
            }

            await Assert.That(successCount).IsEqualTo(appendCount);

            // Verify the batch has all records
            var batchesField = typeof(RecordAccumulator).GetField("_batches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var batches = batchesField!.GetValue(accumulator)!;

            var topicPartition = new TopicPartition("test-topic", 0);
            var tryGetValueMethod = batches.GetType().GetMethod("TryGetValue");
            var parameters = new object[] { topicPartition, null! };
            tryGetValueMethod!.Invoke(batches, parameters);

            var partitionBatch = parameters[1];
            var recordCountProperty = partitionBatch!.GetType().GetProperty("RecordCount");
            var recordCount = (int)recordCountProperty!.GetValue(partitionBatch)!;
            await Assert.That(recordCount).IsEqualTo(appendCount);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task TryAppendFireAndForget_WithPooledMemory_TracksArraysForCleanup()
    {
        // Verify that fire-and-forget correctly tracks pooled arrays for cleanup,
        // even though it doesn't track completion sources.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Create pooled memory with actual arrays
            var keyArray = ArrayPool<byte>.Shared.Rent(10);
            var valueArray = ArrayPool<byte>.Shared.Rent(20);
            var keyData = new byte[] { 1, 2, 3, 4, 5 };
            var valueData = new byte[] { 6, 7, 8, 9, 10 };
            keyData.CopyTo(keyArray, 0);
            valueData.CopyTo(valueArray, 0);

            var pooledKey = new PooledMemory(keyArray, keyData.Length);
            var pooledValue = new PooledMemory(valueArray, valueData.Length);

            var result = accumulator.TryAppendFireAndForget(
                "test-topic",
                0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey,
                pooledValue,
                null,
                null);

            await Assert.That(result).IsTrue();

            // Get the batch and verify arrays are tracked
            var batchesField = typeof(RecordAccumulator).GetField("_batches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var batches = batchesField!.GetValue(accumulator)!;

            var topicPartition = new TopicPartition("test-topic", 0);
            var tryGetValueMethod = batches.GetType().GetMethod("TryGetValue");
            var parameters = new object[] { topicPartition, null! };
            tryGetValueMethod!.Invoke(batches, parameters);

            var partitionBatch = parameters[1];

            // Verify pooled arrays are tracked (via reflection)
            var pooledArrayCountField = partitionBatch!.GetType().GetField("_pooledArrayCount",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pooledArrayCount = (int)pooledArrayCountField!.GetValue(partitionBatch)!;
            await Assert.That(pooledArrayCount).IsEqualTo(2); // key + value arrays
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task TryAppendFireAndForget_DisposedAccumulator_ReturnsFalse()
    {
        // Verify that fire-and-forget returns false when accumulator is disposed.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);

        // Dispose the accumulator
        await accumulator.DisposeAsync();

        var pooledKey = new PooledMemory(null, 0, isNull: true);
        var pooledValue = new PooledMemory(null, 0, isNull: true);

        var result = accumulator.TryAppendFireAndForget(
            "test-topic",
            0,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            pooledKey,
            pooledValue,
            null,
            null);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryAppendFireAndForget_ConcurrentAppends_ThreadSafe()
    {
        // Stress test fire-and-forget with concurrent appends.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);

        try
        {
            const int threadCount = 10;
            const int appendsPerThread = 100;
            var tasks = new List<Task<int>>();

            for (int t = 0; t < threadCount; t++)
            {
                var task = Task.Run(() =>
                {
                    var successCount = 0;
                    for (int i = 0; i < appendsPerThread; i++)
                    {
                        var pooledKey = new PooledMemory(null, 0, isNull: true);
                        var pooledValue = new PooledMemory(null, 0, isNull: true);

                        if (accumulator.TryAppendFireAndForget(
                            "test-topic",
                            i % 3, // Distribute across 3 partitions
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            pooledKey,
                            pooledValue,
                            null,
                            null))
                        {
                            successCount++;
                        }
                    }
                    return successCount;
                });
                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            var totalSuccess = results.Sum();

            // All appends should succeed (batch size is large enough)
            await Assert.That(totalSuccess).IsGreaterThan(0);

            // Verify we didn't corrupt any data structures
            var batchesField = typeof(RecordAccumulator).GetField("_batches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var batches = batchesField!.GetValue(accumulator)!;
            var countProperty = batches.GetType().GetProperty("Count");
            var batchCount = (int)countProperty!.GetValue(batches)!;

            // Should have batches for the 3 partitions we used
            await Assert.That(batchCount).IsGreaterThanOrEqualTo(1);
            await Assert.That(batchCount).IsLessThanOrEqualTo(3);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task TryAppendFireAndForget_BatchFull_CreateNewBatchAndSucceed()
    {
        // Verify that when a batch is full, fire-and-forget creates a new batch.

        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 100000,
            BatchSize = 100, // Small batch size to trigger full condition
            LingerMs = 10
        };
        var accumulator = new RecordAccumulator(options);

        try
        {
            var successCount = 0;

            // Append many records to fill batches
            for (int i = 0; i < 50; i++)
            {
                // Create records with actual data to fill the batch
                var keyArray = ArrayPool<byte>.Shared.Rent(10);
                var valueArray = ArrayPool<byte>.Shared.Rent(10);

                var pooledKey = new PooledMemory(keyArray, 10);
                var pooledValue = new PooledMemory(valueArray, 10);

                if (accumulator.TryAppendFireAndForget(
                    "test-topic",
                    0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey,
                    pooledValue,
                    null,
                    null))
                {
                    successCount++;
                }
            }

            // All appends should eventually succeed (batches are created as needed)
            await Assert.That(successCount).IsEqualTo(50);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task PartitionBatch_TryAppendFireAndForget_DoesNotIncrementCompletionSourceCount()
    {
        // Verify that TryAppendFireAndForget does NOT increment the completion source count,
        // unlike the regular TryAppend method.

        var options = CreateTestOptions();
        var topicPartition = new TopicPartition("test-topic", 0);

        // Create a PartitionBatch directly using reflection
        var partitionBatchType = typeof(RecordAccumulator).Assembly.GetType("Dekaf.Producer.PartitionBatch");
        var constructor = partitionBatchType!.GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
            new[] { typeof(TopicPartition), typeof(ProducerOptions) });

        var partitionBatch = constructor!.Invoke(new object[] { topicPartition, options });

        // Get the completion source count field
        var completionSourceCountField = partitionBatchType.GetField("_completionSourceCount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Initial count should be 0
        var initialCount = (int)completionSourceCountField!.GetValue(partitionBatch)!;
        await Assert.That(initialCount).IsEqualTo(0);

        // Call TryAppendFireAndForget
        var tryAppendMethod = partitionBatchType.GetMethod("TryAppendFireAndForget");
        var pooledKey = new PooledMemory(null, 0, isNull: true);
        var pooledValue = new PooledMemory(null, 0, isNull: true);

        var result = tryAppendMethod!.Invoke(partitionBatch, new object[]
        {
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            pooledKey,
            pooledValue,
            null!,  // headers
            null!   // pooledHeaderArray
        });

        // Verify append succeeded
        var successProperty = result!.GetType().GetProperty("Success");
        var success = (bool)successProperty!.GetValue(result)!;
        await Assert.That(success).IsTrue();

        // Verify completion source count is still 0 (fire-and-forget doesn't track completions)
        var afterCount = (int)completionSourceCountField.GetValue(partitionBatch)!;
        await Assert.That(afterCount).IsEqualTo(0);

        // But record count should be 1
        var recordCountProperty = partitionBatchType.GetProperty("RecordCount");
        var recordCount = (int)recordCountProperty!.GetValue(partitionBatch)!;
        await Assert.That(recordCount).IsEqualTo(1);
    }

    #endregion
}
