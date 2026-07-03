using System.Buffers;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public class BatchArrayReuseQueueTests
{
    private static PooledValueTaskSource<RecordMetadata>[] CreateTestArray()
        => ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(16);

    [Test]
    public async Task Enqueue_BelowCapacity_ThenDequeue_ReturnsArray()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 4);
        var array = CreateTestArray();

        queue.EnqueueOrReturn(array);

        var success = queue.TryDequeue(out var reusable);

        await Assert.That(success).IsTrue();
        await Assert.That(reusable).IsSameReferenceAs(array);
    }

    [Test]
    public async Task Enqueue_AtCapacity_FallsBackToArrayPool()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 2);

        // Fill the queue to capacity
        queue.EnqueueOrReturn(CreateTestArray());
        queue.EnqueueOrReturn(CreateTestArray());

        // This one should be returned to ArrayPool (queue is full)
        queue.EnqueueOrReturn(CreateTestArray());

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
                    queue.EnqueueOrReturn(CreateTestArray());
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
        var array1 = CreateTestArray();
        queue.EnqueueOrReturn(array1);
        var success1 = queue.TryDequeue(out var reusable1);

        await Assert.That(success1).IsTrue();
        await Assert.That(reusable1).IsSameReferenceAs(array1);

        // Second cycle - queue should accept new items after draining
        var array2 = CreateTestArray();
        queue.EnqueueOrReturn(array2);
        var success2 = queue.TryDequeue(out var reusable2);

        await Assert.That(success2).IsTrue();
        await Assert.That(reusable2).IsSameReferenceAs(array2);
    }
}
