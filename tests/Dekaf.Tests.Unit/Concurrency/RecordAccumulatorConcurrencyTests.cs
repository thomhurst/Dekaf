using System.Collections.Concurrent;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Concurrency;

/// <summary>
/// Concurrency tests for RecordAccumulator verifying thread-safety of
/// AppendAsync, TryAppendSync, ReleaseMemory, FlushAsync, and batch rotation
/// under contention from multiple concurrent producers.
/// </summary>
public class RecordAccumulatorConcurrencyTests
{
    private static ProducerOptions CreateTestOptions(
        int batchSize = 16384,
        ulong bufferMemory = ulong.MaxValue,
        int lingerMs = 1000)
    {
        return new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "concurrency-test",
            BufferMemory = bufferMemory,
            BatchSize = batchSize,
            LingerMs = lingerMs
        };
    }

    [Test]
    public async Task ConcurrentAppendAsync_MultiplePartitions_NoLostMessages()
    {
        // Verify that concurrent AppendAsync calls to different partitions
        // all succeed without losing any messages.

        const int taskCount = 8;
        const int messagesPerTask = 100;
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var successCount = 0;

        try
        {
            var tasks = Enumerable.Range(0, taskCount).Select(async taskIndex =>
            {
                for (var i = 0; i < messagesPerTask; i++)
                {
                    var partition = taskIndex; // Each task gets its own partition
                    var completion = pool.Rent();
                    var key = new PooledMemory(null, 0, isNull: true);
                    var value = new PooledMemory(null, 0, isNull: true);

                    var result = await accumulator.AppendAsync(
                        "test-topic",
                        partition,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        key,
                        value,
                        null,
                        null,
                        completion,
                        CancellationToken.None);

                    if (result.Success)
                        Interlocked.Increment(ref successCount);
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            await Assert.That(successCount).IsEqualTo(taskCount * messagesPerTask);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ConcurrentAppendAsync_SamePartition_AllSucceed()
    {
        // Multiple threads appending to the SAME partition tests the CAS-based
        // batch rotation logic under contention.

        const int taskCount = 8;
        const int messagesPerTask = 50;
        var options = CreateTestOptions(batchSize: 512); // Small batch to force rotation
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var successCount = 0;

        try
        {
            // No Barrier here: with [Repeat(25)] running 25 instances in parallel,
            // Barrier(8) across 25 reps = 200 threads blocking on SignalAndWait,
            // causing thread pool starvation and deadlock. The concurrent Tasks
            // still race against each other without the barrier.
            var tasks = Enumerable.Range(0, taskCount).Select(async taskIndex =>
            {
                for (var i = 0; i < messagesPerTask; i++)
                {
                    var completion = pool.Rent();
                    var key = new PooledMemory(null, 0, isNull: true);
                    var value = new PooledMemory(null, 0, isNull: true);

                    var result = await accumulator.AppendAsync(
                        "test-topic",
                        0, // All tasks target partition 0
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        key,
                        value,
                        null,
                        null,
                        completion,
                        CancellationToken.None);

                    if (result.Success)
                        Interlocked.Increment(ref successCount);
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            await Assert.That(successCount).IsEqualTo(taskCount * messagesPerTask);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ConcurrentTryAppendSync_SamePartition_AllSucceed()
    {
        // Test fire-and-forget sync append path under contention.
        // TryAppendSync uses lock-free CAS for memory reservation.

        const int taskCount = 8;
        const int messagesPerTask = 100;
        var options = CreateTestOptions(batchSize: 512);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var successCount = 0;

        try
        {
            var barrier = new Barrier(taskCount);
            var tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < messagesPerTask; i++)
                {
                    var completion = pool.Rent();
                    var key = new PooledMemory(null, 0, isNull: true);
                    var value = new PooledMemory(null, 0, isNull: true);

                    var appended = accumulator.TryAppendSync(
                        "test-topic",
                        0,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        key,
                        value,
                        null,
                        null,
                        completion);

                    if (appended)
                        Interlocked.Increment(ref successCount);
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            await Assert.That(successCount).IsEqualTo(taskCount * messagesPerTask);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ConcurrentTryReserveAndReleaseMemory_AccountingRemainsConsistent()
    {
        // TryReserveMemory and ReleaseMemory use CAS loops. Under contention,
        // the final BufferedBytes must equal (reserved - released).

        const int taskCount = 8;
        const int opsPerTask = 200;
        const int recordSize = 100;
        var options = CreateTestOptions(bufferMemory: (ulong)(taskCount * opsPerTask * recordSize * 2));
        var accumulator = new RecordAccumulator(options);

        try
        {
            var totalReserved = 0L;
            var totalReleased = 0L;

            var barrier = new Barrier(taskCount);
            var tasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < opsPerTask; i++)
                {
                    if (accumulator.TryReserveMemory(recordSize))
                    {
                        Interlocked.Add(ref totalReserved, recordSize);

                        // Release half the time to create contention
                        if (i % 2 == 0)
                        {
                            accumulator.ReleaseMemory(recordSize);
                            Interlocked.Add(ref totalReleased, recordSize);
                        }
                    }
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            var expectedBuffered = Interlocked.Read(ref totalReserved) - Interlocked.Read(ref totalReleased);
            await Assert.That(accumulator.BufferedBytes).IsEqualTo(expectedBuffered);
        }
        finally
        {
            await accumulator.DisposeAsync();
        }
    }

    [Test]
    public async Task BufferMemoryContention_MultiplePartitions_BackpressureWorks()
    {
        // With a small buffer, multiple partitions under pressure should
        // experience backpressure but not deadlock. Messages should eventually
        // succeed or the cancellation token should fire.

        const int taskCount = 4;
        const int messagesPerTask = 20;
        // Very small buffer to trigger backpressure
        var options = CreateTestOptions(batchSize: 256, bufferMemory: 2048);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var successCount = 0;
        var cancelledCount = 0;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var cancelled = false;

            // Start a background drain that continuously releases memory
            // to prevent permanent blocking. Don't pass cts.Token to Task.Run
            // to avoid TaskCanceledException on the outer task.
            var drainTask = Task.Run(async () =>
            {
                try
                {
                    while (!Volatile.Read(ref cancelled))
                    {
                        // Read and drain ready batches to release memory
                        if (accumulator.ReadyBatches.TryRead(out var readyBatch))
                        {
                            accumulator.ReleaseMemory(readyBatch.DataSize);
                            accumulator.OnBatchExitsPipeline();
                        }
                        else
                        {
                            await Task.Delay(1).ConfigureAwait(false);
                        }
                    }
                }
                catch (ObjectDisposedException) { }
            });

            var tasks = Enumerable.Range(0, taskCount).Select(async taskIndex =>
            {
                for (var i = 0; i < messagesPerTask; i++)
                {
                    try
                    {
                        var completion = pool.Rent();
                        var key = new PooledMemory(null, 0, isNull: true);
                        var value = new PooledMemory(null, 0, isNull: true);

                        var result = await accumulator.AppendAsync(
                            "test-topic",
                            taskIndex,
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            key,
                            value,
                            null,
                            null,
                            completion,
                            cts.Token);

                        if (result.Success)
                            Interlocked.Increment(ref successCount);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Increment(ref cancelledCount);
                    }
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            // Signal and observe the drain task
            Volatile.Write(ref cancelled, true);
            await drainTask;

            // All messages should have succeeded or been cancelled - none should be lost
            await Assert.That(successCount + cancelledCount).IsEqualTo(taskCount * messagesPerTask);
            // At least some messages should have succeeded
            await Assert.That(successCount).IsGreaterThan(0);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ConcurrentAppendAndExpireLinger_NoBatchesLost()
    {
        // Tests the race between AppendAsync adding messages and ExpireLingerAsync
        // sealing batches for delivery. No batches should be lost or double-sealed.

        const int appendTasks = 4;
        const int messagesPerTask = 50;
        var options = CreateTestOptions(batchSize: 512, lingerMs: 1); // Very short linger
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var successCount = 0;
        var readyBatchCount = 0;

        try
        {
            var cancelled = false;

            // Background linger expiration loop. Don't pass cts.Token to Task.Run
            // to avoid TaskCanceledException on the outer task.
            var lingerTask = Task.Run(async () =>
            {
                try
                {
                    while (!Volatile.Read(ref cancelled))
                    {
                        await accumulator.ExpireLingerAsync(CancellationToken.None);
                        await Task.Delay(1);
                    }
                }
                catch (ObjectDisposedException) { }
            });

            // Background drain to count ready batches
            var drainTask = Task.Run(async () =>
            {
                try
                {
                    while (!Volatile.Read(ref cancelled))
                    {
                        if (accumulator.ReadyBatches.TryRead(out var batch))
                        {
                            Interlocked.Increment(ref readyBatchCount);
                            accumulator.ReleaseMemory(batch.DataSize);
                            accumulator.OnBatchExitsPipeline();
                        }
                        else
                        {
                            await Task.Delay(1).ConfigureAwait(false);
                        }
                    }
                }
                catch (ObjectDisposedException) { }
            });

            var appendTaskList = Enumerable.Range(0, appendTasks).Select(async taskIndex =>
            {
                for (var i = 0; i < messagesPerTask; i++)
                {
                    var completion = pool.Rent();
                    var key = new PooledMemory(null, 0, isNull: true);
                    var value = new PooledMemory(null, 0, isNull: true);

                    var result = await accumulator.AppendAsync(
                        "test-topic",
                        taskIndex % 2, // Two partitions
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        key,
                        value,
                        null,
                        null,
                        completion,
                        CancellationToken.None);

                    if (result.Success)
                        Interlocked.Increment(ref successCount);
                }
            }).ToArray();

            await Task.WhenAll(appendTaskList);

            // Signal and observe background tasks
            Volatile.Write(ref cancelled, true);
            await lingerTask;
            await drainTask;

            await Assert.That(successCount).IsEqualTo(appendTasks * messagesPerTask);
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task DisposeAsync_RacingWithAppendAsync_DoesNotHang()
    {
        // DisposeAsync racing with in-flight AppendAsync calls must not
        // deadlock or lose messages. Appends should either succeed or throw
        // ObjectDisposedException / OperationCanceledException.

        const int taskCount = 4;
        const int messagesPerTask = 20;
        var options = CreateTestOptions();
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var successCount = 0;
        var failedCount = 0;

        try
        {
            // Use CancellationToken with timeout to prevent indefinite blocking
            // after disposal, in case AppendAsync doesn't throw ObjectDisposedException
            // on all code paths.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var appendTasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(async () =>
            {
                for (var i = 0; i < messagesPerTask; i++)
                {
                    try
                    {
                        var completion = pool.Rent();
                        var key = new PooledMemory(null, 0, isNull: true);
                        var value = new PooledMemory(null, 0, isNull: true);

                        var result = await accumulator.AppendAsync(
                            "test-topic",
                            taskIndex,
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            key,
                            value,
                            null,
                            null,
                            completion,
                            cts.Token);

                        if (result.Success)
                            Interlocked.Increment(ref successCount);
                    }
                    catch (ObjectDisposedException)
                    {
                        Interlocked.Increment(ref failedCount);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Increment(ref failedCount);
                    }
                }
            })).ToArray();

            // Let some appends start, then dispose
            await Task.Delay(2);
            await accumulator.DisposeAsync();

            // Wait for all tasks to complete - should NOT hang
            await Task.WhenAll(appendTasks).WaitAsync(TimeSpan.FromSeconds(30));
            // Every message was either appended or failed cleanly
            await Assert.That(successCount + failedCount).IsEqualTo(taskCount * messagesPerTask);
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task FlushAsync_ConcurrentWithAppend_CompletesWithoutDeadlock()
    {
        // FlushAsync seals all batches and waits for in-flight batches to complete.
        // Concurrent appends during flush should not deadlock.

        var options = CreateTestOptions(batchSize: 512, lingerMs: 1000);
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();

        try
        {
            var cancelled = false;

            // Drain ready batches to let flush complete. Don't pass cts.Token
            // to Task.Run to avoid TaskCanceledException on the outer task.
            var drainTask = Task.Run(async () =>
            {
                try
                {
                    while (!Volatile.Read(ref cancelled))
                    {
                        if (accumulator.ReadyBatches.TryRead(out var batch))
                        {
                            accumulator.ReleaseMemory(batch.DataSize);
                            accumulator.OnBatchExitsPipeline();
                        }
                        else
                        {
                            await Task.Delay(1).ConfigureAwait(false);
                        }
                    }
                }
                catch (ObjectDisposedException) { }
            });

            // Append some messages first
            for (var i = 0; i < 20; i++)
            {
                var completion = pool.Rent();
                await accumulator.AppendAsync(
                    "test-topic",
                    i % 4,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new PooledMemory(null, 0, isNull: true),
                    new PooledMemory(null, 0, isNull: true),
                    null, null, completion, CancellationToken.None);
            }

            // Run flush and more appends concurrently
            var flushTask = accumulator.FlushAsync(CancellationToken.None);

            var appendTask = Task.Run(async () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    try
                    {
                        var completion = pool.Rent();
                        await accumulator.AppendAsync(
                            "test-topic",
                            i % 4,
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            new PooledMemory(null, 0, isNull: true),
                            new PooledMemory(null, 0, isNull: true),
                            null, null, completion, CancellationToken.None);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            });

            // Should complete without deadlock within timeout
            await flushTask;
            await appendTask;

            // Signal and observe background drain
            Volatile.Write(ref cancelled, true);
            await drainTask;
        }
        finally
        {
            await accumulator.DisposeAsync();
            await pool.DisposeAsync();
        }
    }
}
