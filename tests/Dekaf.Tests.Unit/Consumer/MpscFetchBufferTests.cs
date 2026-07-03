using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Tests.Unit;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for the <see cref="MpscFetchBuffer"/> multi-producer single-consumer ring buffer.
/// Validates write/read semantics, capacity enforcement, wait behavior, completion signaling,
/// and concurrent producer/consumer correctness.
/// </summary>
public class MpscFetchBufferTests
{
    private static PendingFetchData CreateDummy(string topic = "test-topic", int partition = 0)
    {
        return PendingFetchData.Create(topic, partition, Array.Empty<RecordBatch>());
    }

    #region TryWrite / TryRead basic operations

    [Test]
    public async Task TryWrite_TryRead_SingleItem_RoundTrips()
    {
        var buffer = new MpscFetchBuffer(4);
        var item = CreateDummy();

        var written = buffer.TryWrite(item);
        await Assert.That(written).IsTrue();

        var read = buffer.TryRead(out var result);
        await Assert.That(read).IsTrue();
        await Assert.That(result).IsEqualTo(item);

        // Cleanup
        item.Dispose();
    }

    [Test]
    public async Task TryRead_EmptyBuffer_ReturnsFalse()
    {
        var buffer = new MpscFetchBuffer(4);

        var read = buffer.TryRead(out var result);
        await Assert.That(read).IsFalse();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryWrite_FullBuffer_ReturnsFalse()
    {
        // Capacity rounds up to power of 2, so capacity=2 gives size=2
        var buffer = new MpscFetchBuffer(2);

        var item1 = CreateDummy("topic", 0);
        var item2 = CreateDummy("topic", 1);
        var item3 = CreateDummy("topic", 2);

        await Assert.That(buffer.TryWrite(item1)).IsTrue();
        await Assert.That(buffer.TryWrite(item2)).IsTrue();
        await Assert.That(buffer.TryWrite(item3)).IsFalse();

        // Cleanup
        buffer.TryRead(out _);
        buffer.TryRead(out _);
        item1.Dispose();
        item2.Dispose();
        item3.Dispose();
    }

    [Test]
    public async Task TryWrite_AfterRead_FreesSlot()
    {
        var buffer = new MpscFetchBuffer(2);

        var item1 = CreateDummy("topic", 0);
        var item2 = CreateDummy("topic", 1);
        var item3 = CreateDummy("topic", 2);

        buffer.TryWrite(item1);
        buffer.TryWrite(item2);

        // Full — read one to free a slot
        buffer.TryRead(out _);

        var written = buffer.TryWrite(item3);
        await Assert.That(written).IsTrue();

        // Cleanup
        buffer.TryRead(out _);
        buffer.TryRead(out _);
        item1.Dispose();
        item2.Dispose();
        item3.Dispose();
    }

    #endregion

    #region WaitToWrite

    [Test]
    public async Task WaitToWriteAsync_MultipleWaiters_RemainSignaled()
    {
        var buffer = new MpscFetchBuffer(1);
        var first = CreateDummy("topic", 0);
        await Assert.That(buffer.TryWrite(first)).IsTrue();

        // Generous outer budget: waiter completion depends on thread-pool scheduling of the
        // SemaphoreSlim continuations, which can lag on a starved CI runner. All waits below
        // bound on this single token rather than shorter fixed delays that flake under load.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var waiter1 = buffer.WaitToWriteAsync(cts.Token).AsTask();
        var waiter2 = buffer.WaitToWriteAsync(cts.Token).AsTask();

        await TestWait.UntilAsync(() => buffer.ProducerWaiterCount == 2, TimeSpan.FromSeconds(10));

        await Assert.That(buffer.TryRead(out var readFirst)).IsTrue();
        readFirst!.Dispose();

        // First read frees one slot -> exactly one waiter is released.
        var firstReleased = await Task.WhenAny(waiter1, waiter2).WaitAsync(cts.Token);
        await Assert.That(firstReleased.IsCompletedSuccessfully).IsTrue();
        var completedWaiterCount =
            (waiter1.IsCompletedSuccessfully ? 1 : 0) +
            (waiter2.IsCompletedSuccessfully ? 1 : 0);
        await Assert.That(completedWaiterCount).IsEqualTo(1);

        var second = CreateDummy("topic", 1);
        await Assert.That(buffer.TryWrite(second)).IsTrue();
        await Assert.That(buffer.TryRead(out var readSecond)).IsTrue();
        readSecond!.Dispose();

        // Second read frees the remaining slot -> both waiters are released.
        await Task.WhenAll(waiter1, waiter2).WaitAsync(cts.Token);
    }

    [Test]
    public async Task WaitToWriteAsync_RecheckDrainsAllReleasedPermits()
    {
        PendingFetchData? readFirst = null;
        PendingFetchData? readSecond = null;
        MpscFetchBuffer? buffer = null;
        var callbackCount = 0;

        buffer = new MpscFetchBuffer(2, afterProducerWaiterCountIncrementedForTesting: () =>
        {
            if (Interlocked.Increment(ref callbackCount) != 1)
                return;

            if (!buffer!.TryRead(out readFirst))
                throw new InvalidOperationException("Expected first read to free a slot.");

            if (!buffer.TryRead(out readSecond))
                throw new InvalidOperationException("Expected second read to free a slot.");
        });

        var first = CreateDummy("topic", 0);
        var second = CreateDummy("topic", 1);
        await Assert.That(buffer.TryWrite(first)).IsTrue();
        await Assert.That(buffer.TryWrite(second)).IsTrue();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await buffer.WaitToWriteAsync(cts.Token).AsTask().WaitAsync(cts.Token);

        await Assert.That(callbackCount).IsEqualTo(1);
        await Assert.That(GetSpaceAvailableSignalCount(buffer)).IsEqualTo(0);

        readFirst!.Dispose();
        readSecond!.Dispose();
    }

    #endregion

    #region WaitToRead

    [Test]
    public async Task WaitToRead_DataAlreadyAvailable_ReturnsImmediately()
    {
        var buffer = new MpscFetchBuffer(4);
        var item = CreateDummy();
        buffer.TryWrite(item);

        var result = buffer.WaitToRead(1000, CancellationToken.None);
        await Assert.That(result).IsTrue();

        // Cleanup
        buffer.TryRead(out _);
        item.Dispose();
    }

    [Test]
    public async Task WaitToRead_DataArrivesAfterDelay_ReturnsTrue()
    {
        var buffer = new MpscFetchBuffer(4);
        var item = CreateDummy();

        // Use a dedicated thread (not thread pool) to avoid starvation on CI
        var writerThread = new Thread(() =>
        {
            Thread.Sleep(50);
            buffer.TryWrite(item);
        })
        { IsBackground = true };
        writerThread.Start();

        try
        {
            var result = buffer.WaitToRead(30_000, CancellationToken.None);
            await Assert.That(result).IsTrue();
        }
        finally
        {
            writerThread.Join();
            buffer.TryRead(out _);
            item.Dispose();
        }
    }

    [Test]
    public async Task WaitToRead_CompletedEmptyBuffer_ReturnsFalse()
    {
        var buffer = new MpscFetchBuffer(4);
        buffer.Complete();

        var result = buffer.WaitToRead(1000, CancellationToken.None);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task WaitToRead_CompletedWithError_ThrowsError()
    {
        var buffer = new MpscFetchBuffer(4);
        var expectedException = new InvalidOperationException("test error");
        buffer.Complete(expectedException);

        var act = () => buffer.WaitToRead(1000, CancellationToken.None);
        await Assert.That(act).Throws<InvalidOperationException>()
            .WithMessage("test error");
    }

    [Test]
    public async Task WaitToRead_Cancelled_ThrowsOperationCancelled()
    {
        var buffer = new MpscFetchBuffer(4);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => buffer.WaitToRead(5000, cts.Token);
        await Assert.That(act).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task WaitToReadAsync_EmptyBuffer_ReturnsWithoutBlockingCaller()
    {
        var buffer = new MpscFetchBuffer(4);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var elapsed = Stopwatch.StartNew();
        var waitTask = buffer.WaitToReadAsync(30_000, cts.Token).AsTask();
        elapsed.Stop();

        await Assert.That(elapsed.Elapsed).IsLessThan(TimeSpan.FromSeconds(1));
        await Assert.That(waitTask.IsCompleted).IsFalse();

        await cts.CancelAsync();
        await Assert.That(async () => await waitTask).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task WaitToReadAsync_DataArrivesAfterWait_ReturnsTrue()
    {
        var buffer = new MpscFetchBuffer(4);
        var item = CreateDummy();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var waitTask = buffer.WaitToReadAsync(30_000, cts.Token).AsTask();
        await Assert.That(waitTask.IsCompleted).IsFalse();

        await Assert.That(buffer.TryWrite(item)).IsTrue();

        await Assert.That(await waitTask).IsTrue();
        await Assert.That(buffer.TryRead(out var result)).IsTrue();
        await Assert.That(result).IsEqualTo(item);

        item.Dispose();
    }

    [Test]
    public async Task WaitToReadAsync_ManyIdleBuffers_CompleteUnblocksAll()
    {
        const int BufferCount = 32;
        var buffers = Enumerable.Range(0, BufferCount)
            .Select(_ => new MpscFetchBuffer(4))
            .ToArray();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var elapsed = Stopwatch.StartNew();
        var waitTasks = buffers
            .Select(buffer => buffer.WaitToReadAsync(30_000, cts.Token).AsTask())
            .ToArray();
        elapsed.Stop();

        await Assert.That(elapsed.Elapsed).IsLessThan(TimeSpan.FromSeconds(1));
        await Assert.That(waitTasks.All(task => !task.IsCompleted)).IsTrue();

        foreach (var buffer in buffers)
        {
            buffer.Complete();
        }

        var results = await Task.WhenAll(waitTasks).WaitAsync(cts.Token);

        await Assert.That(results.All(result => !result)).IsTrue();
    }

    #endregion

    #region Completion

    [Test]
    public async Task IsCompleted_InitiallyFalse()
    {
        var buffer = new MpscFetchBuffer(4);
        await Assert.That(buffer.IsCompleted).IsFalse();
    }

    [Test]
    public async Task IsCompleted_AfterComplete_IsTrue()
    {
        var buffer = new MpscFetchBuffer(4);
        buffer.Complete();
        await Assert.That(buffer.IsCompleted).IsTrue();
    }

    [Test]
    public async Task Complete_IsIdempotent_FirstErrorPreserved()
    {
        var buffer = new MpscFetchBuffer(4);
        var error = new InvalidOperationException("fatal prefetch error");

        buffer.Complete(error);
        buffer.Complete(null); // Second call should be ignored

        await Assert.That(buffer.IsCompleted).IsTrue();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            buffer.WaitToRead(100, CancellationToken.None));
        await Assert.That(ex.Message).IsEqualTo("fatal prefetch error");
    }

    #endregion

    #region Concurrent producer/consumer

    [Test]
    public async Task ConcurrentProducerConsumer_AllItemsTransferred()
    {
        const int itemCount = 10_000;
        var buffer = new MpscFetchBuffer(64);
        var consumed = new List<int>();

        // Producer: writes itemCount items
        var producerTask = Task.Run(() =>
        {
            for (var i = 0; i < itemCount; i++)
            {
                var item = CreateDummy("topic", i);
                while (!buffer.TryWrite(item))
                {
                    Thread.SpinWait(100);
                }
            }
            buffer.Complete();
        });

        // Consumer: reads until completed
        var consumerTask = Task.Run(() =>
        {
            while (true)
            {
                if (buffer.TryRead(out var item))
                {
                    consumed.Add(item.PartitionIndex);
                    item.Dispose();
                }
                else if (buffer.IsCompleted)
                {
                    // Drain remaining
                    while (buffer.TryRead(out var remaining))
                    {
                        consumed.Add(remaining.PartitionIndex);
                        remaining.Dispose();
                    }
                    break;
                }
                else
                {
                    Thread.SpinWait(10);
                }
            }
        });

        await Task.WhenAll(producerTask, consumerTask);

        await Assert.That(consumed).Count().IsEqualTo(itemCount);

        // Single producer: ordering is preserved (FIFO within one producer)
        for (var i = 0; i < itemCount; i++)
        {
            await Assert.That(consumed[i]).IsEqualTo(i);
        }
    }

    [Test]
    public async Task MultipleProducers_SingleConsumer_AllItemsTransferred()
    {
        const int producerCount = 4;
        const int itemsPerProducer = 2_500;
        const int totalItems = producerCount * itemsPerProducer;
        var buffer = new MpscFetchBuffer(64);
        var consumed = new ConcurrentBag<int>();

        // Multiple producers write concurrently (simulates multi-broker prefetch)
        var producerTasks = new Task[producerCount];
        for (var p = 0; p < producerCount; p++)
        {
            var producerId = p;
            producerTasks[p] = Task.Run(() =>
            {
                for (var i = 0; i < itemsPerProducer; i++)
                {
                    var item = CreateDummy("topic", producerId * itemsPerProducer + i);
                    while (!buffer.TryWrite(item))
                    {
                        Thread.SpinWait(100);
                    }
                }
            });
        }

        // Single consumer
        var consumerTask = Task.Run(() =>
        {
            var count = 0;
            while (count < totalItems)
            {
                if (buffer.TryRead(out var item))
                {
                    consumed.Add(item.PartitionIndex);
                    item.Dispose();
                    count++;
                }
                else
                {
                    Thread.SpinWait(10);
                }
            }
        });

        await Task.WhenAll(producerTasks);
        await consumerTask;

        await Assert.That(consumed).Count().IsEqualTo(totalItems);

        // All items should be unique (no duplicates or lost items)
        var sorted = consumed.OrderBy(x => x).ToList();
        await Assert.That(sorted.Distinct().Count()).IsEqualTo(totalItems);
    }

    #endregion

    #region Capacity rounding

    [Test]
    public async Task Capacity_RoundsUpToPowerOfTwo()
    {
        // Capacity 3 should round up to 4
        var buffer = new MpscFetchBuffer(3);
        await Assert.That(buffer.Capacity).IsEqualTo(4);

        var items = new List<PendingFetchData>();
        for (var i = 0; i < 4; i++)
        {
            var item = CreateDummy("topic", i);
            items.Add(item);
            await Assert.That(buffer.TryWrite(item)).IsTrue();
        }

        // 5th item should fail (capacity is 4)
        var extra = CreateDummy("topic", 4);
        await Assert.That(buffer.TryWrite(extra)).IsFalse();

        // Cleanup
        foreach (var item in items)
        {
            buffer.TryRead(out _);
            item.Dispose();
        }
        extra.Dispose();
    }

    #endregion

    private static int GetSpaceAvailableSignalCount(MpscFetchBuffer buffer)
    {
        var field = typeof(MpscFetchBuffer).GetField(
            "_spaceAvailable",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var semaphore = (SemaphoreSlim)field.GetValue(buffer)!;
        return semaphore.CurrentCount;
    }
}
