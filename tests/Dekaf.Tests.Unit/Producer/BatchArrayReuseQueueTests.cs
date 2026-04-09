using System.Buffers;
using Dekaf.Producer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

public class BatchArrayReuseQueueTests
{
    private static (Record[] Records, PooledValueTaskSource<RecordMetadata>[] CompletionSources, byte[][] DataArrays, Header[][] HeaderArrays) CreateTestArrays()
    {
        return (
            ArrayPool<Record>.Shared.Rent(16),
            ArrayPool<PooledValueTaskSource<RecordMetadata>>.Shared.Rent(16),
            ArrayPool<byte[]>.Shared.Rent(16),
            ArrayPool<Header[]>.Shared.Rent(16)
        );
    }

    [Test]
    public async Task Enqueue_BelowCapacity_ThenDequeue_ReturnsArrays()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 4);
        var arrays = CreateTestArrays();

        queue.EnqueueOrReturn(arrays.Records, arrays.CompletionSources, arrays.DataArrays, arrays.HeaderArrays);

        var success = queue.TryDequeue(out var records, out var completionSources, out var dataArrays, out var headerArrays);

        await Assert.That(success).IsTrue();
        await Assert.That(records).IsSameReferenceAs(arrays.Records);
        await Assert.That(completionSources).IsSameReferenceAs(arrays.CompletionSources);
        await Assert.That(dataArrays).IsSameReferenceAs(arrays.DataArrays);
        await Assert.That(headerArrays).IsSameReferenceAs(arrays.HeaderArrays);
    }

    [Test]
    public async Task Enqueue_AtCapacity_FallsBackToArrayPool()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 2);

        // Fill the queue to capacity
        var arrays1 = CreateTestArrays();
        var arrays2 = CreateTestArrays();
        queue.EnqueueOrReturn(arrays1.Records, arrays1.CompletionSources, arrays1.DataArrays, arrays1.HeaderArrays);
        queue.EnqueueOrReturn(arrays2.Records, arrays2.CompletionSources, arrays2.DataArrays, arrays2.HeaderArrays);

        // This one should be returned to ArrayPool (queue is full)
        var overflow = CreateTestArrays();
        queue.EnqueueOrReturn(overflow.Records, overflow.CompletionSources, overflow.DataArrays, overflow.HeaderArrays);

        // Should only be able to dequeue the 2 that fit
        var success1 = queue.TryDequeue(out _, out _, out _, out _);
        var success2 = queue.TryDequeue(out _, out _, out _, out _);
        var success3 = queue.TryDequeue(out _, out _, out _, out _);

        await Assert.That(success1).IsTrue();
        await Assert.That(success2).IsTrue();
        await Assert.That(success3).IsFalse();
    }

    [Test]
    public async Task Dequeue_OnEmptyQueue_ReturnsFalse()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 4);

        var success = queue.TryDequeue(out _, out _, out _, out _);

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
                    queue.EnqueueOrReturn(arrays.Records, arrays.CompletionSources, arrays.DataArrays, arrays.HeaderArrays);
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
                    if (queue.TryDequeue(out _, out _, out _, out _))
                    {
                        Interlocked.Increment(ref dequeueCount);
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Drain any remaining items
        while (queue.TryDequeue(out _, out _, out _, out _))
        {
            Interlocked.Increment(ref dequeueCount);
        }

        // Total enqueued items should equal dequeued + those that went to ArrayPool fallback
        // We can't know the exact split, but dequeueCount should be <= enqueueCount
        // and the queue should be empty at the end
        await Assert.That(dequeueCount).IsGreaterThan(0);
        await Assert.That(dequeueCount).IsLessThanOrEqualTo(enqueueCount);
        await Assert.That(queue.TryDequeue(out _, out _, out _, out _)).IsFalse();
    }

    [Test]
    public async Task Enqueue_ThenDequeue_MultipleTimes_WorksCorrectly()
    {
        var queue = new BatchArrayReuseQueue(maxSize: 2);

        // First cycle
        var arrays1 = CreateTestArrays();
        queue.EnqueueOrReturn(arrays1.Records, arrays1.CompletionSources, arrays1.DataArrays, arrays1.HeaderArrays);
        var success1 = queue.TryDequeue(out var records1, out _, out _, out _);

        await Assert.That(success1).IsTrue();
        await Assert.That(records1).IsSameReferenceAs(arrays1.Records);

        // Second cycle - queue should accept new items after draining
        var arrays2 = CreateTestArrays();
        queue.EnqueueOrReturn(arrays2.Records, arrays2.CompletionSources, arrays2.DataArrays, arrays2.HeaderArrays);
        var success2 = queue.TryDequeue(out var records2, out _, out _, out _);

        await Assert.That(success2).IsTrue();
        await Assert.That(records2).IsSameReferenceAs(arrays2.Records);
    }
}
