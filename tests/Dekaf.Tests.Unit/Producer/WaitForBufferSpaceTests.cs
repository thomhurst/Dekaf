using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for <see cref="RecordAccumulator.WaitForBufferSpace"/> which gates
/// fire-and-forget Send() calls when the buffer is full.
/// </summary>
public sealed class WaitForBufferSpaceTests
{
    [Test]
    public async Task WaitForBufferSpace_ReturnsImmediately_WhenBufferHasSpace()
    {
        var accumulator = CreateAccumulator(bufferMemory: 10_000_000);
        try
        {
            // Buffer is empty — fast path should return immediately
            accumulator.WaitForBufferSpace();

            await Assert.That((ulong)accumulator.BufferedBytes).IsLessThan(accumulator.MaxBufferMemory);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task WaitForBufferSpace_BlocksThenUnblocks_WhenReleaseMemoryIsCalled()
    {
        // Step 1: measure the reserved size of a single record
        var recordSize = MeasureRecordSize();

        // Step 2: create accumulator with BufferMemory = recordSize so one append fills it exactly
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 10000);
        try
        {
            AppendOneRecord(accumulator);

            // Buffer should now be exactly at capacity
            await Assert.That((ulong)accumulator.BufferedBytes).IsGreaterThanOrEqualTo(accumulator.MaxBufferMemory);

            // Start WaitForBufferSpace on a background thread with deterministic signaling
            var aboutToWait = new ManualResetEventSlim(false);
            var waitCompleted = new ManualResetEventSlim(false);
            var waitTask = Task.Run(() =>
            {
                aboutToWait.Set();
                accumulator.WaitForBufferSpace();
                waitCompleted.Set();
            });

            // Wait until the background thread is about to call WaitForBufferSpace
            aboutToWait.Wait(TimeSpan.FromSeconds(5));
            // Brief yield to let it enter the wait loop
            Thread.Sleep(100);

            // Verify it hasn't completed yet
            await Assert.That(waitCompleted.IsSet).IsFalse();

            // Release memory — this should unblock WaitForBufferSpace
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);

            // Wait for completion
            var completed = waitTask.Wait(TimeSpan.FromSeconds(10));
            await Assert.That(completed).IsTrue();
            await Assert.That(waitCompleted.IsSet).IsTrue();
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task WaitForBufferSpace_ThrowsObjectDisposedException_WhenDisposedWhileWaiting()
    {
        var recordSize = MeasureRecordSize();
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 30000);
        try
        {
            AppendOneRecord(accumulator);

            await Assert.That((ulong)accumulator.BufferedBytes).IsGreaterThanOrEqualTo(accumulator.MaxBufferMemory);

            // Start WaitForBufferSpace on a background thread
            var aboutToWait = new ManualResetEventSlim(false);
            Exception? caughtException = null;
            var waitTask = Task.Run(() =>
            {
                try
                {
                    aboutToWait.Set();
                    accumulator.WaitForBufferSpace();
                }
                catch (Exception ex)
                {
                    Volatile.Write(ref caughtException!, ex);
                }
            });

            aboutToWait.Wait(TimeSpan.FromSeconds(5));
            Thread.Sleep(100);

            // Dispose — should unblock the waiter with ObjectDisposedException
            await accumulator.DisposeAsync();

            await waitTask.WaitAsync(TimeSpan.FromSeconds(10));

            var exception = Volatile.Read(ref caughtException);
            await Assert.That(exception).IsNotNull();
            await Assert.That(exception).IsTypeOf<ObjectDisposedException>();
        }
        catch (ObjectDisposedException)
        {
            // Disposal during test cleanup is expected
        }
    }

    private static RecordAccumulator CreateAccumulator(ulong bufferMemory, int maxBlockMs = 1000)
    {
        return new RecordAccumulator(new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test-producer",
            BufferMemory = bufferMemory,
            BatchSize = 16384,
            LingerMs = 100,
            MaxBlockMs = maxBlockMs
        });
    }

    private static void AppendOneRecord(RecordAccumulator accumulator)
    {
        var pooledKey = new PooledMemory(null, 0, isNull: true);
        var valueBytes = System.Buffers.ArrayPool<byte>.Shared.Rent(200);
        var pooledValue = new PooledMemory(valueBytes, 200);

        accumulator.Append(
            "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            pooledKey, pooledValue, null, null, null, null);
    }

    /// <summary>
    /// Measures the actual BufferedBytes consumed by a single record append.
    /// This avoids hardcoding record overhead assumptions.
    /// </summary>
    private static int MeasureRecordSize()
    {
        var accumulator = CreateAccumulator(bufferMemory: 10_000_000);
        AppendOneRecord(accumulator);
        var size = (int)accumulator.BufferedBytes;

        // Drain and release so disposal doesn't fail on the unsealed batch
        if (accumulator.TryDrainBatch(out var batch))
        {
            batch!.CompleteDelivery();
            accumulator.OnBatchExitsPipeline(batch);
            accumulator.ReturnReadyBatch(batch);
        }
        else
        {
            // Batch is unsealed — clear it and release the memory
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(size);
        }

        accumulator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return size;
    }
}
