using System.Collections.Concurrent;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for RecordAccumulator focusing on memory accounting and cleanup correctness.
/// These tests verify that the double-cleanup bug (GitHub issue) is fixed and doesn't regress.
/// </summary>
public class RecordAccumulatorTests
{
    private static ProducerOptions CreateTestOptions()
    {
        return new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 10000, // Small buffer to trigger memory waiting in tests
            BatchSize = 1000,
            LingerMs = 10
        };
    }

    [Test]
    public async Task PartitionBatch_Complete_CalledTwice_ReturnsIdempotent()
    {
        // This test verifies that calling Complete() multiple times on the same PartitionBatch
        // returns the same ReadyBatch instance (or null after the first call), preventing
        // double-cleanup of resources.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var topicPartition = new TopicPartition("test-topic", 0);

        try
        {
            // Append a record to create a non-empty batch
            var completion = new TaskCompletionSource<RecordMetadata>();
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            var batch = await accumulator.AppendAsync(
                topicPartition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey,
                pooledValue,
                null,
                null,
                completion,
                CancellationToken.None);

            await Assert.That(batch.Success).IsTrue();

            // Use reflection to access the internal PartitionBatch
            var batchesField = typeof(RecordAccumulator).GetField("_batches",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var batches = (ConcurrentDictionary<TopicPartition, object>)batchesField!.GetValue(accumulator)!;

            await Assert.That(batches.TryGetValue(topicPartition, out var partitionBatch)).IsTrue();

            // Get the Complete method via reflection
            var completeMethod = partitionBatch!.GetType().GetMethod("Complete");
            await Assert.That(completeMethod).IsNotNull();

            // Call Complete() the first time
            var readyBatch1 = completeMethod!.Invoke(partitionBatch, null);
            await Assert.That(readyBatch1).IsNotNull();

            // Call Complete() the second time - should return the SAME instance or null
            var readyBatch2 = completeMethod.Invoke(partitionBatch, null);

            // Either returns the same instance (cached) or null (already completed)
            // Both are acceptable - the key is NOT creating a NEW ReadyBatch with duplicate resources
            if (readyBatch2 != null)
            {
                await Assert.That(ReferenceEquals(readyBatch1, readyBatch2)).IsTrue();
            }
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task ReadyBatch_Cleanup_CalledTwice_OnlyReleasesMemoryOnce()
    {
        // This test verifies that even if Cleanup() is called twice on a ReadyBatch,
        // memory is only released once, preventing underflow.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var topicPartition = new TopicPartition("test-topic", 0);

        try
        {
            // Append a record to track some memory
            var completion = new TaskCompletionSource<RecordMetadata>();
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            await accumulator.AppendAsync(
                topicPartition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey,
                pooledValue,
                null,
                null,
                completion,
                CancellationToken.None);

            // Flush to create a ReadyBatch
            var readyBatches = new List<ReadyBatch>();
            await foreach (var batch in accumulator.GetReadyBatchesAsync(CancellationToken.None))
            {
                readyBatches.Add(batch);
                break; // Only get one batch
            }

            await Assert.That(readyBatches.Count).IsEqualTo(1);
            var readyBatch = readyBatches[0];

            // Get initial memory usage via reflection
            var usedMemoryField = typeof(RecordAccumulator).GetField("_usedMemory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initialMemory = (ulong)usedMemoryField!.GetValue(accumulator)!;

            // Complete the batch (calls Cleanup)
            readyBatch.Complete(0, DateTimeOffset.UtcNow);
            var memoryAfterFirstCleanup = (ulong)usedMemoryField.GetValue(accumulator)!;

            // Memory should be released
            await Assert.That(memoryAfterFirstCleanup).IsLessThan(initialMemory);

            // Call cleanup again via reflection (simulating the bug)
            var cleanupMethod = typeof(ReadyBatch).GetMethod("Cleanup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // This should NOT cause underflow or double-release
            cleanupMethod!.Invoke(readyBatch, null);

            var memoryAfterSecondCleanup = (ulong)usedMemoryField.GetValue(accumulator)!;

            // Memory should be the same - cleanup should be idempotent
            await Assert.That(memoryAfterSecondCleanup).IsEqualTo(memoryAfterFirstCleanup);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task RecordAccumulator_ConcurrentCompleteAndDispose_NoMemoryUnderflow()
    {
        // This test simulates the actual bug: concurrent calls to Complete() during disposal.
        // It verifies that memory accounting doesn't underflow.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var topicPartition = new TopicPartition("test-topic", 0);

        // Append many records to create batches
        var completions = new List<TaskCompletionSource<RecordMetadata>>();
        for (int i = 0; i < 100; i++)
        {
            var completion = new TaskCompletionSource<RecordMetadata>();
            completions.Add(completion);

            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            await accumulator.AppendAsync(
                topicPartition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey,
                pooledValue,
                null,
                null,
                completion,
                CancellationToken.None);
        }

        // Start concurrent operations:
        // 1. Flush batches (calls Complete)
        // 2. Dispose (also calls Complete)
        var flushTask = Task.Run(async () =>
        {
            try
            {
                await accumulator.FlushAsync(CancellationToken.None);
            }
            catch { }
        });

        var disposeTask = Task.Run(async () =>
        {
            await Task.Delay(5); // Small delay to increase chance of race
            await accumulator.DisposeAsync();
        });

        await Task.WhenAll(flushTask, disposeTask);

        // Get final memory usage via reflection
        var usedMemoryField = typeof(RecordAccumulator).GetField("_usedMemory",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var finalMemory = (ulong)usedMemoryField!.GetValue(accumulator)!;

        // Memory should be 0 or close to 0 (all released)
        // The key is it should NOT be a huge number indicating underflow
        await Assert.That(finalMemory).IsLessThan(10000UL);
    }

    [Test]
    public async Task RecordAccumulator_ConcurrentAppendAndExpire_NoDoubleCleanup()
    {
        // This test verifies that concurrent AppendAsync (which may call Complete when batch is full)
        // and ExpireLingerAsync don't cause double-cleanup.

        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 10000,
            BatchSize = 1000,
            LingerMs = 1 // Very short linger
        };
        var accumulator = new RecordAccumulator(options);
        var topicPartition = new TopicPartition("test-topic", 0);

        var errors = new ConcurrentBag<Exception>();

        try
        {
            // Concurrent append operations
            var appendTasks = Enumerable.Range(0, 50).Select(async i =>
            {
                try
                {
                    var completion = new TaskCompletionSource<RecordMetadata>();
                    var pooledKey = new PooledMemory(null, 0, isNull: true);
                    var pooledValue = new PooledMemory(null, 0, isNull: true);

                    await accumulator.AppendAsync(
                        topicPartition,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey,
                        pooledValue,
                        null,
                        null,
                        completion,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }).ToList();

            // Concurrent expire operations
            var expireTasks = Enumerable.Range(0, 10).Select(async _ =>
            {
                try
                {
                    await Task.Delay(2); // Wait for linger to expire
                    await accumulator.ExpireLingerAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }).ToList();

            await Task.WhenAll(appendTasks.Concat(expireTasks));

            // Check for memory accounting errors
            foreach (var error in errors)
            {
                await Assert.That(error.Message).DoesNotContain("underflow");
                await Assert.That(error.Message).DoesNotContain("Memory accounting");
            }
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task ReleaseMemory_ReleasingMoreThanAllocated_ThrowsException()
    {
        // This test verifies that ReleaseMemory throws an exception instead of silently
        // causing underflow when trying to release more memory than allocated.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Use reflection to call AllocateMemory and ReleaseMemory directly
            var allocateMethod = typeof(RecordAccumulator).GetMethod("AllocateMemory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var releaseMethod = typeof(RecordAccumulator).GetMethod("ReleaseMemory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Allocate 100 bytes
            allocateMethod!.Invoke(accumulator, new object[] { 100u });

            // Try to release 200 bytes - should throw
            var exception = await Assert.That(() =>
                releaseMethod!.Invoke(accumulator, new object[] { 200u })
            ).ThrowsException();

            await Assert.That(exception).IsNotNull();
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task ReadyBatch_FailThenComplete_OnlyCleanupOnce()
    {
        // This test verifies that calling both Fail() and Complete() on the same ReadyBatch
        // only triggers cleanup once.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var topicPartition = new TopicPartition("test-topic", 0);

        try
        {
            // Create a batch with tracked memory
            var completion = new TaskCompletionSource<RecordMetadata>();
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            await accumulator.AppendAsync(
                topicPartition,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey,
                pooledValue,
                null,
                null,
                completion,
                CancellationToken.None);

            // Flush to create ReadyBatch
            await accumulator.FlushAsync(CancellationToken.None);

            // Get the batch from the channel
            ReadyBatch? readyBatch = null;
            await foreach (var batch in accumulator.GetReadyBatchesAsync(CancellationToken.None))
            {
                readyBatch = batch;
                break;
            }

            await Assert.That(readyBatch).IsNotNull();

            // Get initial memory
            var usedMemoryField = typeof(RecordAccumulator).GetField("_usedMemory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initialMemory = (ulong)usedMemoryField!.GetValue(accumulator)!;

            // Call Fail (triggers cleanup)
            readyBatch!.Fail(new InvalidOperationException("Test exception"));
            var memoryAfterFail = (ulong)usedMemoryField.GetValue(accumulator)!;

            await Assert.That(memoryAfterFail).IsLessThan(initialMemory);

            // Call Complete (should NOT cleanup again)
            readyBatch.Complete(0, DateTimeOffset.UtcNow);
            var memoryAfterComplete = (ulong)usedMemoryField.GetValue(accumulator)!;

            // Memory should remain the same
            await Assert.That(memoryAfterComplete).IsEqualTo(memoryAfterFail);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task RecordAccumulator_StressTest_NoMemoryLeak()
    {
        // Stress test: rapid append/flush/dispose cycles should not leak memory
        // or cause accounting errors.

        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 100000,
            BatchSize = 1000,
            LingerMs = 10
        };
        var accumulator = new RecordAccumulator(options);
        var topicPartition = new TopicPartition("test-topic", 0);

        try
        {
            // Perform many operations
            for (int iteration = 0; iteration < 100; iteration++)
            {
                // Append messages
                for (int i = 0; i < 10; i++)
                {
                    var completion = new TaskCompletionSource<RecordMetadata>();
                    var pooledKey = new PooledMemory(null, 0, isNull: true);
                    var pooledValue = new PooledMemory(null, 0, isNull: true);

                    await accumulator.AppendAsync(
                        topicPartition,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        pooledKey,
                        pooledValue,
                        null,
                        null,
                        completion,
                        CancellationToken.None);
                }

                // Flush and consume batches
                await accumulator.FlushAsync(CancellationToken.None);

                await foreach (var batch in accumulator.GetReadyBatchesAsync(CancellationToken.None))
                {
                    batch.Complete(0, DateTimeOffset.UtcNow);
                }
            }

            // Final memory should be close to 0 (all released)
            var usedMemoryField = typeof(RecordAccumulator).GetField("_usedMemory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var finalMemory = (ulong)usedMemoryField!.GetValue(accumulator)!;

            await Assert.That(finalMemory).IsLessThan(1000UL);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }
}
