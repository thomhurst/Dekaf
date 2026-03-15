using System.Buffers;
using Dekaf.Producer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

public class BatchArrayReuseQueueTests
{
    private static BatchArrayReuseQueue.ReusableArrays CreateTestArrays()
    {
        var records = ArrayPool<Record>.Shared.Rent(16);
        var completionSources = ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(16);
        var pooledDataArrays = ArrayPool<byte[]>.Shared.Rent(16);
        var pooledHeaderArrays = ArrayPool<Header[]>.Shared.Rent(16);
        return new BatchArrayReuseQueue.ReusableArrays(records, completionSources, pooledDataArrays, pooledHeaderArrays);
    }

    [Test]
    public async Task Enqueue_BelowCapacity_ThenDequeue_ReturnsArrays()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 4);
        var arrays = CreateTestArrays();

        queue.EnqueueOrReturn(arrays.Records, arrays.CompletionSources, arrays.PooledDataArrays, arrays.PooledHeaderArrays);

        var success = queue.TryDequeue(out var dequeued);

        await Assert.That(success).IsTrue();
        await Assert.That(dequeued.Records).IsSameReferenceAs(arrays.Records);
        await Assert.That(dequeued.CompletionSources).IsSameReferenceAs(arrays.CompletionSources);
        await Assert.That(dequeued.PooledDataArrays).IsSameReferenceAs(arrays.PooledDataArrays);
        await Assert.That(dequeued.PooledHeaderArrays).IsSameReferenceAs(arrays.PooledHeaderArrays);
    }

    [Test]
    public async Task Enqueue_AtCapacity_FallsBackToArrayPool()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 2);

        // Fill the queue to capacity
        var arrays1 = CreateTestArrays();
        var arrays2 = CreateTestArrays();
        queue.EnqueueOrReturn(arrays1.Records, arrays1.CompletionSources, arrays1.PooledDataArrays, arrays1.PooledHeaderArrays);
        queue.EnqueueOrReturn(arrays2.Records, arrays2.CompletionSources, arrays2.PooledDataArrays, arrays2.PooledHeaderArrays);

        // This one should be returned to ArrayPool (queue is full)
        var overflow = CreateTestArrays();
        queue.EnqueueOrReturn(overflow.Records, overflow.CompletionSources, overflow.PooledDataArrays, overflow.PooledHeaderArrays);

        // Should only be able to dequeue the 2 that fit
        var success1 = queue.TryDequeue(out _);
        var success2 = queue.TryDequeue(out _);
        var success3 = queue.TryDequeue(out _);

        await Assert.That(success1).IsTrue();
        await Assert.That(success2).IsTrue();
        await Assert.That(success3).IsFalse();
    }

    [Test]
    public async Task Dequeue_OnEmptyQueue_ReturnsFalse()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 4);

        var success = queue.TryDequeue(out _);

        await Assert.That(success).IsFalse();
    }

    [Test]
    public async Task ConcurrentEnqueueDequeue_NoDataLoss()
    {
        const int maxSize = 64;
        const int operationsPerThread = 1000;
        const int threadCount = 4;
        var queue = new BatchArrayReuseQueue(maxSize: maxSize);
        var enqueueCount = 0;
        var dequeueCount = 0;

        var tasks = new Task[threadCount * 2];

        // Producer threads
        for (var i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < operationsPerThread; j++)
                {
                    var arrays = CreateTestArrays();
                    queue.EnqueueOrReturn(arrays.Records, arrays.CompletionSources, arrays.PooledDataArrays, arrays.PooledHeaderArrays);
                    Interlocked.Increment(ref enqueueCount);
                }
            });
        }

        // Consumer threads
        for (var i = 0; i < threadCount; i++)
        {
            tasks[threadCount + i] = Task.Run(() =>
            {
                for (var j = 0; j < operationsPerThread; j++)
                {
                    if (queue.TryDequeue(out _))
                    {
                        Interlocked.Increment(ref dequeueCount);
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Drain any remaining items
        while (queue.TryDequeue(out _))
        {
            Interlocked.Increment(ref dequeueCount);
        }

        // Total enqueued items should equal dequeued + those that went to ArrayPool fallback
        // We can't know the exact split, but dequeueCount should be <= enqueueCount
        // and the queue should be empty at the end
        await Assert.That(dequeueCount).IsGreaterThan(0);
        await Assert.That(dequeueCount).IsLessThanOrEqualTo(enqueueCount);
        await Assert.That(queue.TryDequeue(out _)).IsFalse();
    }

    [Test]
    public async Task Enqueue_ThenDequeue_MultipleTimes_WorksCorrectly()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 2);

        // First cycle
        var arrays1 = CreateTestArrays();
        queue.EnqueueOrReturn(arrays1.Records, arrays1.CompletionSources, arrays1.PooledDataArrays, arrays1.PooledHeaderArrays);
        var success1 = queue.TryDequeue(out var dequeued1);

        await Assert.That(success1).IsTrue();
        await Assert.That(dequeued1.Records).IsSameReferenceAs(arrays1.Records);

        // Second cycle - queue should accept new items after draining
        var arrays2 = CreateTestArrays();
        queue.EnqueueOrReturn(arrays2.Records, arrays2.CompletionSources, arrays2.PooledDataArrays, arrays2.PooledHeaderArrays);
        var success2 = queue.TryDequeue(out var dequeued2);

        await Assert.That(success2).IsTrue();
        await Assert.That(dequeued2.Records).IsSameReferenceAs(arrays2.Records);
    }
}
