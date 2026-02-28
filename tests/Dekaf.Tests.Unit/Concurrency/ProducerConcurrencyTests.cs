using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Concurrency;

/// <summary>
/// Concurrency tests for producer-level components: ValueTaskSourcePool and
/// PartitionInflightTracker. Tests real race conditions using multiple
/// concurrent tasks hitting the same objects simultaneously.
/// </summary>
public class ProducerConcurrencyTests
{
    [Test]
    public async Task ValueTaskSourcePool_ConcurrentRent_EachInstanceUnique()
    {
        // Multiple threads renting from the pool concurrently must never
        // receive the same instance simultaneously. We rent a batch at once,
        // verify uniqueness, then return them all.

        const int batchSize = 16;
        const int iterations = 100;
        var pool = new ValueTaskSourcePool<int>();

        try
        {
            for (var iter = 0; iter < iterations; iter++)
            {
                var sources = new PooledValueTaskSource<int>[batchSize];
                var barrier = new Barrier(batchSize);

                // All threads rent concurrently
                var rentTasks = Enumerable.Range(0, batchSize).Select(i => Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    sources[i] = pool.Rent();
                })).ToArray();

                await Task.WhenAll(rentTasks);

                // Verify all are distinct instances
                var distinct = sources.Distinct().Count();
                await Assert.That(distinct).IsEqualTo(batchSize);

                // Return all to pool (proper lifecycle: we haven't completed them,
                // so just set and return properly)
                foreach (var source in sources)
                {
                    source.SetResult(0);
                }
            }
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task ValueTaskSourcePool_ConcurrentRent_AllDistinctInstances()
    {
        // When multiple threads rent concurrently without returning,
        // each thread must get a distinct instance.

        const int threadCount = 16;
        var pool = new ValueTaskSourcePool<int>();

        try
        {
            var sources = new PooledValueTaskSource<int>[threadCount];
            var barrier = new Barrier(threadCount);

            var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
            {
                barrier.SignalAndWait();
                sources[i] = pool.Rent();
            })).ToArray();

            await Task.WhenAll(tasks);

            // All sources should be distinct objects
            var distinct = sources.Distinct().Count();
            await Assert.That(distinct).IsEqualTo(threadCount);

            // Clean up - complete each source (no pool return needed, they'll be GC'd)
            foreach (var source in sources)
            {
                source.SetResult(0);
            }
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task PartitionInflightTracker_ConcurrentRegisterAndComplete_NoCorruption()
    {
        // Register and Complete from different threads (simulating send loop
        // and response handlers) must maintain list integrity.

        const int iterations = 200;
        var tracker = new PartitionInflightTracker();
        var tp = new TopicPartition("test-topic", 0);

        for (var i = 0; i < iterations; i++)
        {
            // Simulate send loop: register batches
            var entry1 = tracker.Register(tp, baseSequence: i * 2, recordCount: 1);
            var entry2 = tracker.Register(tp, baseSequence: i * 2 + 1, recordCount: 1);

            // Simulate response handler completing concurrently
            var t1 = Task.Run(() => tracker.Complete(entry1));
            var t2 = Task.Run(() => tracker.Complete(entry2));

            await Task.WhenAll(t1, t2);

            // After completing both, the list should be empty
            await Assert.That(tracker.GetInflightCount(tp)).IsEqualTo(0);
        }
    }

    [Test]
    public async Task PartitionInflightTracker_ConcurrentRegisterOnDifferentPartitions_Independent()
    {
        // Registrations on different partitions should not interfere with each other.

        const int partitionCount = 8;
        const int batchesPerPartition = 50;
        var tracker = new PartitionInflightTracker();

        var barrier = new Barrier(partitionCount);
        var tasks = Enumerable.Range(0, partitionCount).Select(p => Task.Run(() =>
        {
            var tp = new TopicPartition("test-topic", p);
            barrier.SignalAndWait();

            for (var i = 0; i < batchesPerPartition; i++)
            {
                var entry = tracker.Register(tp, baseSequence: i, recordCount: 1);
                tracker.Complete(entry);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // All partitions should have empty in-flight lists
        for (var p = 0; p < partitionCount; p++)
        {
            var tp = new TopicPartition("test-topic", p);
            await Assert.That(tracker.GetInflightCount(tp)).IsEqualTo(0);
        }
    }

    [Test]
    public async Task EnqueueAppend_ConcurrentWithDisposal_CompletesOrFails()
    {
        // EnqueueAppend racing with DisposeAsync should either succeed
        // or throw ObjectDisposedException, never hang or silently lose messages.

        const int taskCount = 8;
        const int messagesPerTask = 50;
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "test",
            BufferMemory = ulong.MaxValue,
            BatchSize = 16384,
            LingerMs = 1000
        };
        var accumulator = new RecordAccumulator(options);
        var pool = new ValueTaskSourcePool<RecordMetadata>();
        var successCount = 0;
        var failedCount = 0;

        // Start append workers before enqueuing
        using var workerCts = new CancellationTokenSource();
        accumulator.StartAppendWorkers(workerCts.Token);

        try
        {
            using var startGate = new ManualResetEventSlim(false);

            var enqueueTasks = Enumerable.Range(0, taskCount).Select(taskIndex => Task.Run(() =>
            {
                startGate.Wait();
                for (var i = 0; i < messagesPerTask; i++)
                {
                    try
                    {
                        var completion = pool.Rent();
                        accumulator.EnqueueAppend(
                            "test-topic",
                            taskIndex,
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            new PooledMemory(null, 0, isNull: true),
                            new PooledMemory(null, 0, isNull: true),
                            null,
                            null,
                            completion,
                            CancellationToken.None);

                        Interlocked.Increment(ref successCount);
                    }
                    catch (ObjectDisposedException)
                    {
                        Interlocked.Increment(ref failedCount);
                    }
                }
            })).ToArray();

            // Release all tasks then dispose shortly after
            startGate.Set();
            await Task.Delay(2);
            workerCts.Cancel();
            await accumulator.DisposeAsync();

            await Task.WhenAll(enqueueTasks).WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(successCount + failedCount).IsEqualTo(taskCount * messagesPerTask);
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task PooledValueTaskSource_ConcurrentTrySetResult_OnlyFirstWins()
    {
        // When two threads race to complete the same PooledValueTaskSource,
        // only one should succeed (TrySetResult returns true), the other
        // should safely fail (returns false).
        // Important: PooledValueTaskSource is NOT designed for concurrent completion
        // of the SAME instance from different threads. TrySetResult uses a CAS guard
        // for this purpose. We test that the CAS guard works correctly.

        const int iterations = 200;

        for (var i = 0; i < iterations; i++)
        {
            // Use a raw PooledValueTaskSource (not pooled) to avoid pool recycling
            var source = new PooledValueTaskSource<int>();
            var result0 = 0;
            var result1 = 0;

            var barrier = new Barrier(2);
            var t1 = Task.Run(() =>
            {
                barrier.SignalAndWait();
                Volatile.Write(ref result0, source.TrySetResult(1) ? 1 : 0);
            });
            var t2 = Task.Run(() =>
            {
                barrier.SignalAndWait();
                Volatile.Write(ref result1, source.TrySetResult(2) ? 1 : 0);
            });

            await Task.WhenAll(t1, t2);

            // Exactly one should have succeeded
            var total = Volatile.Read(ref result0) + Volatile.Read(ref result1);
            await Assert.That(total).IsEqualTo(1);
        }
    }
}
