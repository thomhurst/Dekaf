using System.Buffers;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests that ReadyBatch.TrySetMemoryReleased is atomic, preventing the double-release
/// race that caused negative _bufferedBytes (GitHub PR #560 investigation).
///
/// The race: BrokerSender send loop, ForceFailAllInFlightBatches, and orphan sweep
/// can concurrently attempt to release the same batch's memory. Without atomicity,
/// two threads read MemoryReleased=false before either writes true, causing a
/// double-release that drives _bufferedBytes negative.
/// </summary>
public class MemoryReleasedAtomicityTests
{
    [Test]
    public async Task TrySetMemoryReleased_FirstCall_ReturnsTrue()
    {
        // Arrange
        var batch = CreateMinimalBatch();

        // Act
        var result = batch.TrySetMemoryReleased();

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TrySetMemoryReleased_SecondCall_ReturnsFalse()
    {
        // Arrange
        var batch = CreateMinimalBatch();
        batch.TrySetMemoryReleased();

        // Act
        var result = batch.TrySetMemoryReleased();

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task MemoryReleased_AfterTrySet_ReturnsTrue()
    {
        // Arrange
        var batch = CreateMinimalBatch();
        await Assert.That(batch.MemoryReleased).IsFalse();

        // Act
        batch.TrySetMemoryReleased();

        // Assert
        await Assert.That(batch.MemoryReleased).IsTrue();
    }

    [Test]
    public async Task TrySetMemoryReleased_ConcurrentCalls_ExactlyOneReturnsTrue()
    {
        // This is the core regression test: multiple threads racing to release
        // the same batch must result in exactly one winner.

        const int threadCount = 16;
        const int iterations = 10_000;
        var failedIterations = 0;

        for (var iter = 0; iter < iterations; iter++)
        {
            var batch = CreateMinimalBatch();
            var trueCount = 0;
            var barrier = new Barrier(threadCount);

            var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
            {
                barrier.SignalAndWait();
                if (batch.TrySetMemoryReleased())
                    Interlocked.Increment(ref trueCount);
            })).ToArray();

            await Task.WhenAll(tasks);

            if (trueCount != 1)
                Interlocked.Increment(ref failedIterations);
        }

        await Assert.That(failedIterations).IsEqualTo(0);
    }

    [Test]
    public async Task ConcurrentRelease_BufferNeverGoesNegative()
    {
        // End-to-end regression test: simulates the exact race between send-time
        // release and forceful shutdown that caused current usage: -216/268435456.
        //
        // Appends messages, seals batches, then races multiple threads to release
        // each batch's memory via the same TrySetMemoryReleased guard used in
        // production. Verifies _bufferedBytes never goes negative.

        var options = new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = 100_000_000,
            BatchSize = 16384,
            LingerMs = 100,
            MaxBlockMs = 60000
        };
        var accumulator = new RecordAccumulator(options);

        try
        {
            const int batchCount = 50;

            // Append messages to create batches
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            for (var i = 0; i < batchCount; i++)
            {
                accumulator.Append(
                    "test-topic", i % 10, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, null, null);
            }

            var bufferedBefore = accumulator.BufferedBytes;
            await Assert.That(bufferedBefore).IsGreaterThan(0);

            // Collect batches by sealing them
            var batches = new List<(ReadyBatch Batch, int DataSize)>();
            for (var p = 0; p < 10; p++)
            {
                if (accumulator.TryGetBatch("test-topic", p, out var partBatch) && partBatch is not null)
                {
                    var readyBatch = partBatch.Complete();
                    if (readyBatch is not null)
                        batches.Add((readyBatch, readyBatch.DataSize));
                }
            }

            // Clear CurrentBatch references to prevent DisposeAsync from double-releasing
            ClearCurrentBatches(accumulator);

            // Race: multiple threads try to release each batch (simulating send loop
            // vs ForceFailAllInFlightBatches vs orphan sweep)
            const int releaserThreads = 4;
            var tasks = new List<Task>();

            foreach (var (batch, dataSize) in batches)
            {
                for (var t = 0; t < releaserThreads; t++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        // This is the exact pattern used in production:
                        // TrySetMemoryReleased() is the atomic guard
                        if (batch.TrySetMemoryReleased())
                        {
                            accumulator.ReleaseMemory(dataSize);
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);

            // The critical assertion: buffer must never go negative
            var bufferedAfter = accumulator.BufferedBytes;
            await Assert.That(bufferedAfter).IsGreaterThanOrEqualTo(0);

            // Should be exactly zero since we released all batches
            await Assert.That(bufferedAfter).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task TrySetMemoryReleased_ResetAfterPoolReturn_AllowsReuse()
    {
        // ReadyBatch is pooled. After Reset(), a recycled batch must allow
        // a fresh TrySetMemoryReleased() call to succeed.
        var batch = CreateMinimalBatch();

        // First lifecycle
        await Assert.That(batch.TrySetMemoryReleased()).IsTrue();
        await Assert.That(batch.TrySetMemoryReleased()).IsFalse();

        // Simulate pool return — flag stays armed while in pool
        batch.Reset();
        await Assert.That(batch.MemoryReleased).IsTrue();
        await Assert.That(batch.TrySetMemoryReleased()).IsFalse();

        // Re-initialize for new lifecycle — flag cleared here, not in Reset()
        batch.Initialize(
            new TopicPartition("topic", 0),
            new RecordBatch { Records = Array.Empty<Record>() },
            completionSourcesArray: null,
            completionSourcesCount: 0,
            pooledDataArrays: null,
            pooledDataArraysCount: 0,
            pooledHeaderArrays: null,
            pooledHeaderArraysCount: 0,
            dataSize: 50);

        // Second lifecycle — must succeed again
        await Assert.That(batch.MemoryReleased).IsFalse();
        await Assert.That(batch.TrySetMemoryReleased()).IsTrue();
        await Assert.That(batch.TrySetMemoryReleased()).IsFalse();
    }

    private static ReadyBatch CreateMinimalBatch()
    {
        var batch = new ReadyBatch();
        batch.Initialize(
            new TopicPartition("test-topic", 0),
            new RecordBatch { Records = Array.Empty<Record>() },
            completionSourcesArray: null,
            completionSourcesCount: 0,
            pooledDataArrays: null,
            pooledDataArraysCount: 0,
            pooledHeaderArrays: null,
            pooledHeaderArraysCount: 0,
            dataSize: 100);
        return batch;
    }

    /// <summary>
    /// Clears CurrentBatch on all partition deques via reflection.
    /// Prevents DisposeAsync from attempting to release memory for batches
    /// we already manually released in the test.
    /// </summary>
    private static void ClearCurrentBatches(RecordAccumulator accumulator)
    {
        var dequesField = typeof(RecordAccumulator).GetField("_partitionDeques",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var deques = dequesField!.GetValue(accumulator)!;

        for (var p = 0; p < 10; p++)
        {
            var tryGetMethod = deques.GetType().GetMethod("TryGetValue");
            var parms = new object[] { new TopicPartition("test-topic", p), null! };
            if ((bool)tryGetMethod!.Invoke(deques, parms)!)
            {
                var pd = parms[1]!;
                pd.GetType().GetField("CurrentBatch")!.SetValue(pd, null);
            }
        }
    }
}
