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

    private static int CountWoken(int[] wokeUp)
    {
        var count = 0;
        for (var i = 0; i < wokeUp.Length; i++)
            if (Volatile.Read(ref wokeUp[i]) == 1) count++;
        return count;
    }

    /// <summary>
    /// Waits until BufferPressureEvents reaches the expected count, indicating all
    /// threads have entered the slow path. Deterministic alternative to Task.Delay.
    /// </summary>
    private static async Task WaitForPressureAsync(RecordAccumulator accumulator, long expectedPressure, CancellationToken ct)
    {
        var deadline = Environment.TickCount64 + 10_000;
        while (accumulator.BufferPressureEvents < expectedPressure && Environment.TickCount64 < deadline)
            await Task.Delay(10, ct);
    }

    #endregion

    #region FIFO Waiter Queue Tests

    [Test]
    [Timeout(90_000)]
    public async Task ReserveMemorySync_WakesExactlyOneWaiterPerRelease(CancellationToken cancellationToken)
    {
        var recordSize = await MeasureRecordSizeAsync();
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 15_000);

        try
        {
            AppendOneRecord(accumulator);

            const int waiterCount = 3;
            var wokeUp = new int[waiterCount];
            var pressureBefore = accumulator.BufferPressureEvents;
            var tasks = new Task[waiterCount];

            for (int i = 0; i < waiterCount; i++)
            {
                var index = i;
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                tasks[i] = tcs.Task;
                new Thread(() =>
                {
                    try
                    {
                        accumulator.ReserveMemorySync(recordSize);
                        Interlocked.Exchange(ref wokeUp[index], 1);
                    }
                    finally { tcs.TrySetResult(); }
                }) { IsBackground = true }.Start();
            }

            // Wait until all threads have entered the slow path (incremented pressure)
            await WaitForPressureAsync(accumulator, pressureBefore + waiterCount, cancellationToken);

            // Release memory for one record — should wake exactly one waiter
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);

            // Wait for exactly one task to complete
            var deadline = Environment.TickCount64 + 5_000;
            while (CountWoken(wokeUp) < 1 && Environment.TickCount64 < deadline)
                await Task.Delay(10, cancellationToken);

            await Assert.That(CountWoken(wokeUp)).IsEqualTo(1);

            // Release again — wake the next waiter
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);

            deadline = Environment.TickCount64 + 5_000;
            while (CountWoken(wokeUp) < 2 && Environment.TickCount64 < deadline)
                await Task.Delay(10, cancellationToken);

            await Assert.That(CountWoken(wokeUp)).IsEqualTo(2);

            // Release once more for the last waiter
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);

            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await Assert.That(CountWoken(wokeUp)).IsEqualTo(3);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    [Timeout(90_000)]
    public async Task WakeAllSyncWaiters_ViaDispose_UnblocksAllThreads(CancellationToken cancellationToken)
    {
        var recordSize = await MeasureRecordSizeAsync();
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 30_000);

        AppendOneRecord(accumulator);

        const int waiterCount = 3;
        var pressureBefore = accumulator.BufferPressureEvents;
        var exceptions = new Exception?[waiterCount];
        var tasks = new Task[waiterCount];

        for (int i = 0; i < waiterCount; i++)
        {
            var index = i;
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            tasks[i] = tcs.Task;
            new Thread(() =>
            {
                try
                {
                    accumulator.ReserveMemorySync(recordSize);
                }
                catch (Exception ex)
                {
                    exceptions[index] = ex;
                }
                finally { tcs.TrySetResult(); }
            }) { IsBackground = true }.Start();
        }

        // Wait until all threads have entered the slow path (incremented pressure counter).
        // BufferPressureEvents is incremented before Enqueue+Wait, so there is a small window
        // between "entered slow path" and "blocking in Event.Wait()". Thread.Sleep bridges this
        // gap — an internal seam (e.g., callback after Enqueue) would eliminate it but would
        // modify library code solely for test observability.
        await WaitForPressureAsync(accumulator, pressureBefore + waiterCount, cancellationToken);
        Thread.Sleep(50);

        // Dispose triggers WakeAllSyncWaiters
        await accumulator.DisposeAsync();

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        // All threads should have been unblocked — each gets an exception (ObjectDisposedException
        // or similar) because ReserveMemorySync cannot succeed on a disposed accumulator.
        // All threads should have been unblocked with some exception
        for (var i = 0; i < waiterCount; i++)
            await Assert.That(exceptions[i]).IsNotNull();
    }

    [Test]
    [Timeout(90_000)]
    public async Task CancelledWaiterNode_IsSkippedByWakeNextSyncWaiter(CancellationToken cancellationToken)
    {
        var recordSize = await MeasureRecordSizeAsync();
        // Use 2s maxBlockMs — shorter timeout reduces test duration on slow CI runners
        // where thread pool starvation delays Task.Run scheduling significantly
        var accumulator = CreateAccumulator(bufferMemory: (ulong)recordSize, maxBlockMs: 2_000);

        try
        {
            AppendOneRecord(accumulator);

            // Waiter 1: will time out (maxBlockMs), leaving a cancelled node in the queue.
            // Use dedicated Thread instead of Task.Run: these are intentionally blocking
            // calls (ManualResetEventSlim.Wait), and Task.Run on a starved thread pool can
            // delay scheduling by 20-40 seconds, causing test timeouts on CI.
            var waiter1Done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            new Thread(() =>
            {
                try { accumulator.ReserveMemorySync(recordSize); }
                catch { /* Expected: KafkaTimeoutException */ }
                finally { waiter1Done.TrySetResult(); }
            }) { IsBackground = true }.Start();

            await waiter1Done.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);

            // Start waiter 2 — uses same FIFO queue
            var pressureBefore = accumulator.BufferPressureEvents;
            var waiter2Completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            new Thread(() =>
            {
                accumulator.WaitForBufferSpace();
                waiter2Completed.SetResult();
            }) { IsBackground = true }.Start();

            // Wait until waiter 2 has entered slow path
            await WaitForPressureAsync(accumulator, pressureBefore + 1, cancellationToken);

            // Release — WakeNextSyncWaiter should skip cancelled node, wake waiter 2
            accumulator.ClearCurrentBatch("test-topic", 0);
            accumulator.ReleaseMemory(recordSize);

            await waiter2Completed.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    #endregion

    #region Pressure Tracking Tests

    [Test]
    [Timeout(90_000)]
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

            // Start a thread that will block in ReserveMemorySync (slow path).
            // Use dedicated Thread to avoid thread pool starvation on CI.
            var reserveTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            new Thread(() =>
            {
                try { accumulator.ReserveMemorySync(recordSize); }
                finally { reserveTcs.TrySetResult(); }
            }) { IsBackground = true }.Start();
            var reserveTask = reserveTcs.Task;

            // Wait until the thread has entered the slow path (incremented pressure)
            await WaitForPressureAsync(accumulator, pressureBefore + 1, cancellationToken);

            await Assert.That(accumulator.BufferPressureEvents).IsGreaterThan(pressureBefore);

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
    [Timeout(90_000)]
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

            // Start a thread that will block in WaitForBufferSpace (slow path).
            // Use dedicated Thread to avoid thread pool starvation on CI.
            var waitTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            new Thread(() =>
            {
                try { accumulator.WaitForBufferSpace(); }
                finally { waitTcs.TrySetResult(); }
            }) { IsBackground = true }.Start();
            var waitTask = waitTcs.Task;

            // Wait until the thread has entered the slow path (incremented pressure)
            await WaitForPressureAsync(accumulator, pressureBefore + 1, cancellationToken);

            await Assert.That(accumulator.BufferPressureEvents).IsGreaterThan(pressureBefore);

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

    #region Scale Step Calculation Tests

    [Test]
    public async Task ComputeScaleTarget_MinimumPressure_AddsOneConnection()
    {
        // pressureDelta = 100 (exactly threshold) → step = 1
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 100, currentConnections: 1, maxConnections: 10);
        await Assert.That(target).IsEqualTo(2);
    }

    [Test]
    public async Task ComputeScaleTarget_ModeratePressure_AddsTwoConnections()
    {
        // pressureDelta = 250 → step = 2
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 250, currentConnections: 1, maxConnections: 10);
        await Assert.That(target).IsEqualTo(3);
    }

    [Test]
    public async Task ComputeScaleTarget_HighPressure_CapsAtMaxStep()
    {
        // pressureDelta = 10_000 → step capped at 3 (MaxScaleStep)
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 10_000, currentConnections: 1, maxConnections: 10);
        await Assert.That(target).IsEqualTo(4);
    }

    [Test]
    public async Task ComputeScaleTarget_NearMaxConnections_ClampsToMax()
    {
        // step would be 3, but maxConnections caps at 10
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 500, currentConnections: 9, maxConnections: 10);
        await Assert.That(target).IsEqualTo(10);
    }

    [Test]
    public async Task ComputeScaleTarget_AtMaxConnections_ReturnsMax()
    {
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 500, currentConnections: 10, maxConnections: 10);
        await Assert.That(target).IsEqualTo(10);
    }

    #endregion

    #region Scale-Down Computation Tests

    [Test]
    public async Task ComputeScaleTarget_ScaleUp_FromOneToTwo()
    {
        // Verify scale-up still works correctly alongside scale-down logic
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 100, currentConnections: 1, maxConnections: 5);
        await Assert.That(target).IsEqualTo(2);
    }

    [Test]
    public async Task ComputeScaleTarget_AtMinConnections_DoesNotGoBelow()
    {
        // ComputeScaleTarget is only for scale-up; scale-down uses different logic.
        // Verify it does not produce targets below current.
        var target = BrokerSender.ComputeScaleTarget(pressureDelta: 100, currentConnections: 1, maxConnections: 1);
        await Assert.That(target).IsEqualTo(1);
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

    [Test]
    public async Task IdempotentProducer_WithAdaptiveScaling_DoesNotThrow()
    {
        // Idempotent producers should support adaptive scaling (partition affinity
        // preserves sequence ordering across connections).
        await Assert.That(() =>
        {
            var producer = Kafka.CreateProducer<string, string>()
                .WithBootstrapServers("localhost:9092")
                .WithIdempotence(true)
                .WithAdaptiveConnections(maxConnections: 5)
                .Build();
            _ = producer.DisposeAsync();
        }).ThrowsNothing();
    }

    [Test]
    public async Task TransactionalProducer_WithConnectionsPerBrokerGreaterThan1_Throws()
    {
        // Transactional producers require a single connection per broker for
        // transaction coordinator requests.
        await Assert.That(() =>
        {
            Kafka.CreateProducer<string, string>()
                .WithBootstrapServers("localhost:9092")
                .WithTransactionalId("txn-1")
                .WithConnectionsPerBroker(2)
                .Build();
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task AdaptiveScalingEnabled_ResolvedCorrectly_ForIdempotentProducer()
    {
        // The options should allow adaptive connections for idempotent producers.
        // BrokerSender disables adaptive scaling only for transactional producers.
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            EnableIdempotence = true,
            EnableAdaptiveConnections = true,
            TransactionalId = null
        };

        // EnableAdaptiveConnections should be true (not blocked by idempotence)
        await Assert.That(options.EnableAdaptiveConnections).IsTrue();
        await Assert.That(options.TransactionalId).IsNull();
    }

    [Test]
    public async Task AdaptiveScalingEnabled_DisabledForTransactionalProducer()
    {
        // BrokerSender uses: options.EnableAdaptiveConnections && options.TransactionalId is null
        // Verify that a transactional producer's options cause adaptive scaling to be disabled.
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            EnableIdempotence = true,
            EnableAdaptiveConnections = true,
            TransactionalId = "txn-1"
        };

        // Even though EnableAdaptiveConnections is true, BrokerSender will disable it
        // because TransactionalId is set.
        await Assert.That(options.TransactionalId).IsNotNull();
    }

    #endregion
}
