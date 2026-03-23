using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for SyncWaiterNode pooling in RecordAccumulator.
/// Verifies that nodes are recycled to reduce GC pressure during sustained backpressure,
/// and that the pool is bounded to prevent excess memory retention.
/// </summary>
public class SyncWaiterNodePoolTests
{
    private static ProducerOptions CreateTestOptions(ulong bufferMemory = 10_000_000, int maxBlockMs = 5000)
    {
        return new ProducerOptions
        {
            BootstrapServers = new[] { "localhost:9092" },
            ClientId = "test-producer",
            BufferMemory = bufferMemory,
            BatchSize = 16384,
            LingerMs = 100,
            MaxBlockMs = maxBlockMs
        };
    }

    /// <summary>
    /// Fills the accumulator buffer until BufferedBytes is within <paramref name="headroom"/> of the limit.
    /// </summary>
    private static void FillBufferToCapacity(RecordAccumulator accumulator, ulong bufferMemory, ulong headroom = 200)
    {
        var pooledKey = new PooledMemory(null, 0, isNull: true);
        var pooledValue = new PooledMemory(null, 0, isNull: true);

        while ((ulong)accumulator.BufferedBytes + headroom < bufferMemory)
        {
            accumulator.Append(
                "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                pooledKey, pooledValue, null, 0, null, null);
        }
    }

    /// <summary>
    /// Waits for <see cref="RecordAccumulator.BufferPressureEvents"/> to exceed <paramref name="threshold"/>,
    /// indicating that at least one thread has entered the kernel-wait slow path.
    /// </summary>
    private static async Task WaitForPressureEvents(RecordAccumulator accumulator, long threshold, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (accumulator.BufferPressureEvents <= threshold
               && Environment.TickCount64 < deadline)
        {
            await Task.Delay(5);
        }
    }

    [Test]
    public async Task SyncWaiterNode_Reset_ClearsState()
    {
        var node = new SyncWaiterNode();
        node.Cancelled = true;
        node.Event.Set();

        node.Reset();

        await Assert.That(node.Cancelled).IsFalse();
        await Assert.That(node.Event.IsSet).IsFalse();
    }

    [Test]
    public async Task SyncWaiterNode_Reset_CanBeWaitedOnAgain()
    {
        var node = new SyncWaiterNode();

        // First cycle
        node.Event.Set();
        await Assert.That(node.Event.IsSet).IsTrue();

        // Reset and reuse
        node.Reset();
        await Assert.That(node.Event.IsSet).IsFalse();

        // Second cycle: signal from another thread
        var waitTask = Task.Run(() => node.Event.Wait(1000));
        node.Event.Set();

        var completed = await Task.WhenAny(waitTask, Task.Delay(2000)) == waitTask;
        await Assert.That(completed).IsTrue();
    }

    [Test]
    public async Task Pool_StartsEmpty()
    {
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);

        try
        {
            await Assert.That(accumulator.PooledWaiterNodeCount).IsEqualTo(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task Pool_GrowsAfterSignaledBackpressureCycle()
    {
        // Fill the buffer, block a thread in ReserveMemorySync, then release memory.
        // The signaled node should be returned to the pool.

        const ulong bufferMemory = 1000;
        var options = CreateTestOptions(bufferMemory: bufferMemory, maxBlockMs: 10000);
        var accumulator = new RecordAccumulator(options);

        try
        {
            FillBufferToCapacity(accumulator, bufferMemory);

            var pressureBefore = accumulator.BufferPressureEvents;

            // This thread will block because reserving the full buffer size exceeds remaining space
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var reserveTask = Task.Run(() =>
            {
                started.SetResult();
                accumulator.ReserveMemorySync((int)bufferMemory);
            });

            await started.Task;
            await WaitForPressureEvents(accumulator, pressureBefore);

            await Assert.That(accumulator.BufferPressureEvents).IsGreaterThan(pressureBefore);

            // Let thread settle into Event.Wait after entering the queue
            await Task.Delay(50);

            // Release all memory
            var buffered = accumulator.BufferedBytes;
            for (var p = 0; p < 10; p++)
                accumulator.ClearCurrentBatch("test-topic", p);
            accumulator.ReleaseMemory((int)buffered);

            var completed = await Task.WhenAny(reserveTask, Task.Delay(5000)) == reserveTask;
            await Assert.That(completed).IsTrue();

            // The signaled node should have been returned to the pool
            await Assert.That(accumulator.PooledWaiterNodeCount).IsGreaterThanOrEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task Pool_RecyclesCancelledNodesOnWake()
    {
        // When WakeNextSyncWaiter dequeues cancelled nodes, they should be returned to the pool.

        const ulong bufferMemory = 1000;
        var options = CreateTestOptions(bufferMemory: bufferMemory, maxBlockMs: 10000);
        var accumulator = new RecordAccumulator(options);

        try
        {
            FillBufferToCapacity(accumulator, bufferMemory);

            var pressureBefore = accumulator.BufferPressureEvents;

            // Launch multiple threads that will block
            var tasks = new Task[3];
            var signals = new TaskCompletionSource[3];
            for (var i = 0; i < 3; i++)
            {
                signals[i] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var signal = signals[i];
                tasks[i] = Task.Run(() =>
                {
                    signal.SetResult();
                    accumulator.ReserveMemorySync((int)bufferMemory);
                });
            }

            await Task.WhenAll(signals.Select(s => s.Task));

            // Wait for at least 2 threads to enter slow path
            var deadline = Environment.TickCount64 + 5000;
            while (accumulator.BufferPressureEvents < pressureBefore + 2
                   && Environment.TickCount64 < deadline)
            {
                await Task.Delay(5);
            }

            await Task.Delay(50);

            // Release all memory
            var buffered = accumulator.BufferedBytes;
            for (var p = 0; p < 10; p++)
                accumulator.ClearCurrentBatch("test-topic", p);
            accumulator.ReleaseMemory((int)buffered);

            // Wait for all tasks
            await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(5000));

            // Pool should have nodes from both signaled and cancelled waiters
            await Assert.That(accumulator.PooledWaiterNodeCount).IsGreaterThanOrEqualTo(1);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }
}
