using Dekaf.Consumer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for the <see cref="SpscFetchBuffer"/> single-producer single-consumer ring buffer.
/// Validates write/read semantics, capacity enforcement, wait behavior, completion signaling,
/// and concurrent producer/consumer correctness.
/// </summary>
public class SpscFetchBufferTests
{
    private static PendingFetchData CreateDummy(string topic = "test-topic", int partition = 0)
    {
        return PendingFetchData.Create(topic, partition, Array.Empty<RecordBatch>());
    }

    #region TryWrite / TryRead basic operations

    [Test]
    public async Task TryWrite_TryRead_SingleItem_RoundTrips()
    {
        var buffer = new SpscFetchBuffer(4);
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
        var buffer = new SpscFetchBuffer(4);

        var read = buffer.TryRead(out var result);
        await Assert.That(read).IsFalse();
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryWrite_FullBuffer_ReturnsFalse()
    {
        // Capacity rounds up to power of 2, so capacity=2 gives size=2
        var buffer = new SpscFetchBuffer(2);

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
        var buffer = new SpscFetchBuffer(2);

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

    #region WaitToRead

    [Test]
    public async Task WaitToRead_DataAlreadyAvailable_ReturnsImmediately()
    {
        var buffer = new SpscFetchBuffer(4);
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
        var buffer = new SpscFetchBuffer(4);
        var item = CreateDummy();

        // Write from another thread after a short delay
        var writeTask = Task.Run(async () =>
        {
            await Task.Delay(50);
            buffer.TryWrite(item);
        });

        var result = buffer.WaitToRead(5000, CancellationToken.None);
        await Assert.That(result).IsTrue();

        await writeTask;

        // Cleanup
        buffer.TryRead(out _);
        item.Dispose();
    }

    [Test]
    public async Task WaitToRead_CompletedEmptyBuffer_ReturnsFalse()
    {
        var buffer = new SpscFetchBuffer(4);
        buffer.Complete();

        var result = buffer.WaitToRead(1000, CancellationToken.None);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task WaitToRead_CompletedWithError_ThrowsError()
    {
        var buffer = new SpscFetchBuffer(4);
        var expectedException = new InvalidOperationException("test error");
        buffer.Complete(expectedException);

        var act = () => buffer.WaitToRead(1000, CancellationToken.None);
        await Assert.That(act).Throws<InvalidOperationException>()
            .WithMessage("test error");
    }

    [Test]
    public async Task WaitToRead_Cancelled_ThrowsOperationCancelled()
    {
        var buffer = new SpscFetchBuffer(4);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => buffer.WaitToRead(5000, cts.Token);
        await Assert.That(act).Throws<OperationCanceledException>();
    }

    #endregion

    #region Completion

    [Test]
    public async Task IsCompleted_InitiallyFalse()
    {
        var buffer = new SpscFetchBuffer(4);
        await Assert.That(buffer.IsCompleted).IsFalse();
    }

    [Test]
    public async Task IsCompleted_AfterComplete_IsTrue()
    {
        var buffer = new SpscFetchBuffer(4);
        buffer.Complete();
        await Assert.That(buffer.IsCompleted).IsTrue();
    }

    #endregion

    #region Concurrent producer/consumer

    [Test]
    public async Task ConcurrentProducerConsumer_AllItemsTransferred()
    {
        const int itemCount = 10_000;
        var buffer = new SpscFetchBuffer(64);
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
                    consumed.Add(item!.PartitionIndex);
                    item.Dispose();
                }
                else if (buffer.IsCompleted)
                {
                    // Drain remaining
                    while (buffer.TryRead(out var remaining))
                    {
                        consumed.Add(remaining!.PartitionIndex);
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

        // Verify ordering is preserved (SPSC guarantees FIFO)
        for (var i = 0; i < itemCount; i++)
        {
            await Assert.That(consumed[i]).IsEqualTo(i);
        }
    }

    #endregion

    #region Capacity rounding

    [Test]
    public async Task Capacity_RoundsUpToPowerOfTwo()
    {
        // Capacity 3 should round up to 4
        var buffer = new SpscFetchBuffer(3);

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
}
