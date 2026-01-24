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
        var accumulator = new RecordAccumulator(options, _ => { }); // No-op callback for tests
        var topicPartition = new TopicPartition("test-topic", 0);

        try
        {
            // Append a record to create a non-empty batch
            var completion = new TaskCompletionSource<RecordMetadata>();
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            var result = await accumulator.AppendAsync(
                topicPartition,
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
        }
    }

    [Test]
    public async Task ReadyBatch_Cleanup_CalledTwice_OnlyExecutesOnce()
    {
        // This test verifies that Cleanup() uses an interlocked guard to ensure
        // it only executes once, even if called multiple times.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options, _ => { }); // No-op callback for tests
        var topicPartition = new TopicPartition("test-topic", 0);

        try
        {
            // Append a record
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
        }
    }

    [Test]
    public async Task ReadyBatch_CompleteThenFail_OnlyCleanupOnce()
    {
        // This test verifies that calling Complete() followed by Fail() only triggers cleanup once.

        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options, _ => { }); // No-op callback for tests
        var topicPartition = new TopicPartition("test-topic", 0);

        try
        {
            // Create a batch
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
        }
    }
}
