using System.Buffers;
using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for adaptive connection scaling features:
/// FIFO waiter queue behavior, buffer pressure tracking, and builder validation.
/// </summary>
public class AdaptiveScalingTests
{
    #region Helpers

    private static RecordAccumulator CreateAccumulator(ulong bufferMemory, int maxBlockMs = 10_000)
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
        var valueBytes = ArrayPool<byte>.Shared.Rent(200);
        var pooledValue = new PooledMemory(valueBytes, 200);

        accumulator.Append(
            "test-topic", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            pooledKey, pooledValue, null, 0, null, null);
    }

    /// <summary>
    /// Measures the actual BufferedBytes consumed by a single record append.
    /// Avoids hardcoding record overhead assumptions.
    /// </summary>
    private static async Task<int> MeasureRecordSizeAsync()
    {
        var accumulator = CreateAccumulator(bufferMemory: 10_000_000);
        AppendOneRecord(accumulator);
        var size = (int)accumulator.BufferedBytes;

        accumulator.ClearCurrentBatch("test-topic", 0);
        accumulator.ReleaseMemory(size);

        await accumulator.DisposeAsync();
        return size;
    }

    #endregion

    #region FIFO Waiter Queue Tests

    [Test]
    [Timeout(30_000)]
    public async Task ReserveMemorySync_WakesExactlyOneWaiterPerRelease(CancellationToken cancellationToken)
    {
        // Arrange: create a buffer that fits exactly one record
        var recordSize = await MeasureRecordSizeAsync();
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 15_000);

        try
        {
            // Fill the buffer
            AppendOneRecord(accumulator);
            await Assert.That((ulong)accumulator.BufferedBytes).IsGreaterThanOrEqualTo(accumulator.MaxBufferMemory);

            // Start N threads that will block in ReserveMemorySync
            const int waiterCount = 3;
            var wokeUp = new int[waiterCount];
            var allStarted = new CountdownEvent(waiterCount);
            var tasks = new Task[waiterCount];

            for (int i = 0; i < waiterCount; i++)
            {
                var index = i;
                tasks[i] = Task.Run(() =>
                {
                    allStarted.Signal();
                    // This will block because buffer is full
                    accumulator.ReserveMemorySync(recordSize);
                    Interlocked.Exchange(ref wokeUp[index], 1);
                }, cancellationToken);
            }

            // Wait until all threads are started and likely blocked
            allStarted.Wait(TimeSpan.FromSeconds(5), cancellationToken);
            // Small delay to ensure threads have entered the wait
            await Task.Delay(200, cancellationToken);

            // Release memory for one record — should wake exactly one waiter
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);

            // Wait a bit for exactly one thread to wake
            await Task.Delay(300, cancellationToken);

            var wokenCount = wokeUp.Count(w => Volatile.Read(ref w) == 1);
            // Exactly one waiter should have been woken and reserved memory
            await Assert.That(wokenCount).IsEqualTo(1);

            // Release again to wake the next waiter (the woken thread reserved recordSize bytes)
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);
            await Task.Delay(300, cancellationToken);

            wokenCount = wokeUp.Count(w => Volatile.Read(ref w) == 1);
            await Assert.That(wokenCount).IsEqualTo(2);

            // Release once more for the last waiter
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);

            // All tasks should complete
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

            wokenCount = wokeUp.Count(w => Volatile.Read(ref w) == 1);
            await Assert.That(wokenCount).IsEqualTo(3);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task WakeAllSyncWaiters_ViaDispose_UnblocksAllThreads(CancellationToken cancellationToken)
    {
        // Arrange: create a buffer that fits exactly one record
        var recordSize = await MeasureRecordSizeAsync();
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 30_000);

        // Fill the buffer
        AppendOneRecord(accumulator);
        await Assert.That((ulong)accumulator.BufferedBytes).IsGreaterThanOrEqualTo(accumulator.MaxBufferMemory);

        // Start N threads that will block in ReserveMemorySync
        const int waiterCount = 3;
        var allStarted = new CountdownEvent(waiterCount);
        var exceptions = new Exception?[waiterCount];
        var tasks = new Task[waiterCount];

        for (int i = 0; i < waiterCount; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                allStarted.Signal();
                try
                {
                    accumulator.ReserveMemorySync(recordSize);
                }
                catch (Exception ex)
                {
                    exceptions[index] = ex;
                }
            }, cancellationToken);
        }

        // Wait until all threads are started and likely blocked
        allStarted.Wait(TimeSpan.FromSeconds(5), cancellationToken);
        await Task.Delay(200, cancellationToken);

        // Dispose triggers WakeAllSyncWaiters — all threads should unblock
        await accumulator.DisposeAsync();

        // All tasks should complete (threads unblocked by disposal)
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        // Each thread should have gotten ObjectDisposedException
        var disposedExceptions = exceptions.Count(e => e is ObjectDisposedException);
        await Assert.That(disposedExceptions).IsEqualTo(waiterCount);
    }

    [Test]
    [Timeout(30_000)]
    public async Task CancelledWaiterNode_IsSkippedByWakeNextSyncWaiter(CancellationToken cancellationToken)
    {
        // Arrange: create a buffer that fits exactly one record with short timeout
        var recordSize = await MeasureRecordSizeAsync();
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 500);

        try
        {
            // Fill the buffer
            AppendOneRecord(accumulator);
            await Assert.That((ulong)accumulator.BufferedBytes).IsGreaterThanOrEqualTo(accumulator.MaxBufferMemory);

            // Waiter 1: will time out (short maxBlockMs), leaving a cancelled node in the queue
            var timedOutTask = Task.Run(() =>
            {
                try
                {
                    accumulator.ReserveMemorySync(recordSize);
                }
                catch
                {
                    // Expected: KafkaTimeoutException
                }
            }, cancellationToken);

            // Wait for waiter 1 to time out
            await timedOutTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            // Now the FIFO queue has a cancelled node from the timed-out waiter.
            // Start waiter 2 with WaitForBufferSpace (uses same FIFO queue).
            var waiter2Completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var waiter2Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var waiter2Task = Task.Run(() =>
            {
                waiter2Started.SetResult();
                accumulator.WaitForBufferSpace();
                waiter2Completed.SetResult();
            }, cancellationToken);

            await waiter2Started.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            await Task.Delay(200, cancellationToken); // ensure waiter 2 is blocked

            // Release memory — WakeNextSyncWaiter should skip the cancelled node and wake waiter 2
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);

            // Waiter 2 should complete (proving the cancelled node was skipped)
            await waiter2Completed.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await waiter2Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    #endregion

    #region Pressure Tracking Tests

    [Test]
    [Timeout(30_000)]
    public async Task BufferPressureEvents_Increments_WhenReserveMemorySyncEntersSlowPath(CancellationToken cancellationToken)
    {
        var recordSize = await MeasureRecordSizeAsync();
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 10_000);

        try
        {
            // Fill the buffer
            AppendOneRecord(accumulator);
            await Assert.That((ulong)accumulator.BufferedBytes).IsGreaterThanOrEqualTo(accumulator.MaxBufferMemory);

            var pressureBefore = accumulator.BufferPressureEvents;

            // Start a thread that will block in ReserveMemorySync (slow path)
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var reserveTask = Task.Run(() =>
            {
                started.SetResult();
                accumulator.ReserveMemorySync(recordSize);
            }, cancellationToken);

            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            await Task.Delay(200, cancellationToken); // Let the thread enter the slow path

            // Pressure should have incremented
            var pressureAfter = accumulator.BufferPressureEvents;
            await Assert.That(pressureAfter).IsGreaterThan(pressureBefore);

            // Release to unblock the thread
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);
            await reserveTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(30_000)]
    public async Task BufferPressureEvents_Increments_WhenWaitForBufferSpaceEntersSlowPath(CancellationToken cancellationToken)
    {
        var recordSize = await MeasureRecordSizeAsync();
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 10_000);

        try
        {
            // Fill the buffer
            AppendOneRecord(accumulator);
            await Assert.That((ulong)accumulator.BufferedBytes).IsGreaterThanOrEqualTo(accumulator.MaxBufferMemory);

            var pressureBefore = accumulator.BufferPressureEvents;

            // Start a thread that will block in WaitForBufferSpace (slow path)
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var waitTask = Task.Run(() =>
            {
                started.SetResult();
                accumulator.WaitForBufferSpace();
            }, cancellationToken);

            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            await Task.Delay(200, cancellationToken); // Let the thread enter the slow path

            // Pressure should have incremented
            var pressureAfter = accumulator.BufferPressureEvents;
            await Assert.That(pressureAfter).IsGreaterThan(pressureBefore);

            // Release to unblock the thread
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);
            await waitTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task BufferUtilization_ReturnsCorrectRatio()
    {
        var recordSize = await MeasureRecordSizeAsync();
        // Buffer that can hold exactly 2 records
        var accumulator = CreateAccumulator(bufferMemory: (ulong)(recordSize * 2));

        try
        {
            // Empty buffer: utilization should be 0
            await Assert.That(accumulator.BufferUtilization).IsEqualTo(0.0);

            // Append one record: utilization should be ~0.5
            AppendOneRecord(accumulator);
            var utilization = accumulator.BufferUtilization;
            await Assert.That(utilization).IsGreaterThan(0.0);
            await Assert.That(utilization).IsLessThanOrEqualTo(1.0);

            // The utilization should be approximately 0.5 (one record in a 2-record buffer)
            await Assert.That(utilization).IsGreaterThanOrEqualTo(0.4);
            await Assert.That(utilization).IsLessThanOrEqualTo(0.6);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    #endregion

    #region Builder Validation Tests

    [Test]
    public async Task WithAdaptiveConnections_MaxLessThanConnectionsPerBroker_ThrowsOnBuild()
    {
        await Assert.That(() =>
        {
            Kafka.CreateProducer<string, string>()
                .WithBootstrapServers("localhost:9092")
                .WithConnectionsPerBroker(5)
                .WithAdaptiveConnections(maxConnections: 3)
                .Build();
        }).Throws<InvalidOperationException>();
    }

    #endregion
}
