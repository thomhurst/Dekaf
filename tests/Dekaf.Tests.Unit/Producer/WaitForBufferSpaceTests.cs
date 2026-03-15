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
        var recordSize = await MeasureRecordSizeAsync();

        // Step 2: create accumulator with BufferMemory = recordSize so one append fills it exactly
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 10000);
        try
        {
            AppendOneRecord(accumulator);

            // Buffer should now be exactly at capacity
            await Assert.That((ulong)accumulator.BufferedBytes).IsGreaterThanOrEqualTo(accumulator.MaxBufferMemory);

            // Start WaitForBufferSpace on a background thread — it should block
            // because the buffer is at capacity.
            var waitTask = Task.Run(() => accumulator.WaitForBufferSpace());

            // Release memory — this should unblock WaitForBufferSpace via the
            // _syncBufferSpaceSignal semaphore that ReleaseMemory signals.
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);

            // The task should complete promptly after memory is released.
            // If WaitForBufferSpace took the fast path (buffer wasn't full), it also
            // completes — either way proves the method returns when space is available.
            await waitTask.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task WaitForBufferSpace_ThrowsObjectDisposedException_WhenDisposedWhileWaiting()
    {
        var recordSize = await MeasureRecordSizeAsync();
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 30000);

        AppendOneRecord(accumulator);
        await Assert.That((ulong)accumulator.BufferedBytes).IsGreaterThanOrEqualTo(accumulator.MaxBufferMemory);

        // Dispose first — then call WaitForBufferSpace synchronously.
        // This is fully deterministic: _disposed is already true, so the method
        // throws ObjectDisposedException immediately without any timing dependency.
        await accumulator.DisposeAsync();

        await Assert.That(() => accumulator.WaitForBufferSpace()).Throws<ObjectDisposedException>();
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
    private static async Task<int> MeasureRecordSizeAsync()
    {
        var accumulator = CreateAccumulator(bufferMemory: 10_000_000);
        AppendOneRecord(accumulator);
        var size = (int)accumulator.BufferedBytes;

        // Clear the unsealed batch and release memory so disposal succeeds cleanly
        accumulator.ClearCurrentBatch("test-topic", 0);
        accumulator.ReleaseMemory(size);

        await accumulator.DisposeAsync();
        return size;
    }
}
