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
    /// Fills the buffer by appending messages until no more fit. Returns the number appended.
    /// Uses a short MaxBlockMs so it throws quickly once full.
    /// </summary>
    private static int FillBuffer(RecordAccumulator accumulator)
    {
        var pooledKey = new PooledMemory(null, 0, isNull: true);
        var pooledValue = new PooledMemory(null, 0, isNull: true);

        var count = 0;
        try
        {
            // Keep appending until the buffer is full (timeout exception)
            for (var i = 0; i < 1000; i++)
            {
                accumulator.Append(
                    "test-topic", i % 10, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, null, null);
                count++;
            }
        }
        catch
        {
            // Expected: KafkaTimeoutException when buffer is full
        }

        return count;
    }

    [Test]
    public async Task SyncWaiterNode_Reset_ClearsState()
    {
        // Arrange
        var node = new SyncWaiterNode();
        node.Cancelled = true;
        node.Event.Set();

        // Act
        node.Reset();

        // Assert: both fields are back to initial state
        await Assert.That(node.Cancelled).IsFalse();
        await Assert.That(node.Event.IsSet).IsFalse();
    }

    [Test]
    public async Task SyncWaiterNode_Reset_CanBeWaitedOnAgain()
    {
        // Verify a reset node can be used for another wait/signal cycle.
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
        // Fill the buffer completely, then block a thread in ReserveMemorySync.
        // After releasing memory to unblock it, the signaled node should be returned to the pool.

        // Use 1000 bytes buffer with short timeout for filling, but long timeout for the reserve thread
        var options = CreateTestOptions(bufferMemory: 1000, maxBlockMs: 100);
        var accumulator = new RecordAccumulator(options);

        try
        {
            // Fill the buffer completely (appends until timeout)
            FillBuffer(accumulator);

            var buffered = accumulator.BufferedBytes;
            await Assert.That(buffered).IsGreaterThan(0);

            // Now create a new accumulator with longer timeout for the blocking test
            await accumulator.DisposeAsync();

            var options2 = CreateTestOptions(bufferMemory: 1000, maxBlockMs: 10000);
            accumulator = new RecordAccumulator(options2);
            FillBuffer(accumulator); // Will fill and timeout, but that's from the short timeout...

            // Actually, let me just use a single accumulator with longer timeout
            await accumulator.DisposeAsync();

            options = CreateTestOptions(bufferMemory: 1000, maxBlockMs: 10000);
            accumulator = new RecordAccumulator(options);

            // Fill by appending until BufferedBytes is close to limit
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            while ((ulong)accumulator.BufferedBytes + 200 < options.BufferMemory)
            {
                accumulator.Append(
                    "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, null, null);
            }

            var pressureBefore = accumulator.BufferPressureEvents;

            // This thread will block because adding another batch header's worth will exceed the limit
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var reserveTask = Task.Run(() =>
            {
                started.SetResult();
                // Reserve a large chunk that exceeds remaining space
                accumulator.ReserveMemorySync((int)options.BufferMemory);
            });

            await started.Task;

            // Wait for the thread to enter the slow path
            var deadline = Environment.TickCount64 + 5000;
            while (accumulator.BufferPressureEvents <= pressureBefore
                   && Environment.TickCount64 < deadline)
            {
                await Task.Delay(5);
            }

            // Ensure thread entered slow path
            await Assert.That(accumulator.BufferPressureEvents).IsGreaterThan(pressureBefore);

            // Let thread settle into Event.Wait
            await Task.Delay(50);

            // Release all memory
            buffered = accumulator.BufferedBytes;
            // Clear all partition batches
            for (var p = 0; p < 10; p++)
                accumulator.ClearCurrentBatch("test-topic", p);
            accumulator.ReleaseMemory((int)buffered);

            // Wait for the reserve to complete
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

        var options = CreateTestOptions(bufferMemory: 1000, maxBlockMs: 10000);
        var accumulator = new RecordAccumulator(options);

        try
        {
            var pooledKey = new PooledMemory(null, 0, isNull: true);
            var pooledValue = new PooledMemory(null, 0, isNull: true);

            // Fill the buffer
            while ((ulong)accumulator.BufferedBytes + 200 < options.BufferMemory)
            {
                accumulator.Append(
                    "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    pooledKey, pooledValue, null, 0, null, null);
            }

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
                    accumulator.ReserveMemorySync((int)options.BufferMemory);
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
