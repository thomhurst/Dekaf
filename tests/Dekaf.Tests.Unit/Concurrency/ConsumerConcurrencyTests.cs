using System.Collections.Concurrent;
using System.Threading.Channels;
using Dekaf.Consumer;
using Dekaf.Networking;

namespace Dekaf.Tests.Unit.Concurrency;

/// <summary>
/// Concurrency tests for consumer subsystem components: ConsumerCoordinator
/// state machine, assignment locking, and position tracking. Tests that
/// concurrent operations on shared consumer state do not corrupt data or
/// deadlock.
/// </summary>
public class ConsumerConcurrencyTests
{
    [Test]
    public async Task ConcurrentPositionUpdates_NoLostWrites()
    {
        // ConcurrentDictionary position updates from prefetch thread
        // and main thread must not lose writes.

        const int threadCount = 4;
        const int updatesPerThread = 500;
        var positions = new ConcurrentDictionary<TopicPartition, long>();
        // Use only 2 partitions so threads 0,2 share partition 0 and threads 1,3
        // share partition 1, creating real contention on the same keys.
        var partitions = Enumerable.Range(0, 2)
            .Select(p => new TopicPartition("test-topic", p))
            .ToArray();

        var barrier = new Barrier(threadCount);
        var tasks = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(() =>
        {
            barrier.SignalAndWait();
            var tp = partitions[threadIndex % partitions.Length];
            for (var i = 0; i < updatesPerThread; i++)
            {
                // Simulate monotonically increasing offset updates
                positions.AddOrUpdate(tp, i, (_, existing) => Math.Max(existing, i));
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Each partition should have the maximum offset written by any thread sharing it
        foreach (var tp in partitions)
        {
            await Assert.That(positions.TryGetValue(tp, out var pos)).IsTrue();
            await Assert.That(pos).IsEqualTo(updatesPerThread - 1);
        }
    }

    [Test]
    public async Task ConcurrentAssignAndUnassign_NoCorruption()
    {
        // Verifies the lock-protected assignment pattern used by KafkaConsumer
        // (pattern test, not integration test). Tests that concurrent assign/unassign
        // operations on a shared HashSet<TopicPartition> (protected by lock) do not
        // corrupt state.

        const int iterations = 200;
        var assignment = new HashSet<TopicPartition>();
        var lockObj = new object();
        var partitions = Enumerable.Range(0, 8)
            .Select(p => new TopicPartition("test-topic", p))
            .ToArray();

        for (var i = 0; i < iterations; i++)
        {
            var barrier = new Barrier(2);

            // One thread adds partitions
            var addTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                lock (lockObj)
                {
                    foreach (var tp in partitions)
                        assignment.Add(tp);
                }
            });

            // Another thread removes them
            var removeTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                lock (lockObj)
                {
                    foreach (var tp in partitions)
                        assignment.Remove(tp);
                }
            });

            await Task.WhenAll(addTask, removeTask);

            // After both complete, assignment should be either empty or full
            // depending on which ran last
            int count;
            lock (lockObj)
            {
                count = assignment.Count;
            }
            await Assert.That(count == 0 || count == partitions.Length).IsTrue();
        }
    }

    [Test]
    public async Task PrefetchChannel_ConcurrentWritersAndSingleReader_NoDataLoss()
    {
        // Multiple broker prefetch threads write to a bounded channel;
        // a single consumer poll loop reads from it. No data should be lost.

        const int writerCount = 4;
        const int itemsPerWriter = 100;
        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(10)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        var writtenCount = 0;
        var readCount = 0;

        // Multiple concurrent writers
        var writerTasks = Enumerable.Range(0, writerCount).Select(async w =>
        {
            for (var i = 0; i < itemsPerWriter; i++)
            {
                await channel.Writer.WriteAsync(w * itemsPerWriter + i);
                Interlocked.Increment(ref writtenCount);
            }
        }).ToArray();

        // Single reader
        var readerTask = Task.Run(async () =>
        {
            var totalExpected = writerCount * itemsPerWriter;
            while (Volatile.Read(ref readCount) < totalExpected)
            {
                if (channel.Reader.TryRead(out _))
                {
                    Interlocked.Increment(ref readCount);
                }
                else
                {
                    await Task.Delay(1);
                }
            }
        });

        await Task.WhenAll(writerTasks);
        await readerTask;

        await Assert.That(readCount).IsEqualTo(writerCount * itemsPerWriter);
    }

    [Test]
    public async Task CoordinatorStateMachine_ConcurrentStateTransitions_ProtectedByLock()
    {
        // Verifies the SemaphoreSlim-guarded state machine pattern used by
        // ConsumerCoordinator (pattern test, not integration test). Simulates
        // the coordinator's state machine transitions under concurrent access.
        // The SemaphoreSlim(1,1) must prevent concurrent state transitions.

        const int threadCount = 4;
        const int transitionsPerThread = 100;
        var state = CoordinatorState.Unjoined;
        var stateGuard = new SemaphoreSlim(1, 1);
        var invalidTransitionCount = 0;

        var validTransitions = new Dictionary<CoordinatorState, CoordinatorState[]>
        {
            [CoordinatorState.Unjoined] = [CoordinatorState.Joining],
            [CoordinatorState.Joining] = [CoordinatorState.Syncing, CoordinatorState.Unjoined],
            [CoordinatorState.Syncing] = [CoordinatorState.Stable, CoordinatorState.Unjoined],
            [CoordinatorState.Stable] = [CoordinatorState.Unjoined]
        };

        var barrier = new Barrier(threadCount);
        var tasks = Enumerable.Range(0, threadCount).Select(t => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < transitionsPerThread; i++)
            {
                await stateGuard.WaitAsync();
                try
                {
                    var currentState = state;
                    if (validTransitions.TryGetValue(currentState, out var nextStates) && nextStates.Length > 0)
                    {
                        var nextState = nextStates[i % nextStates.Length];
                        state = nextState;
                    }
                    else
                    {
                        Interlocked.Increment(ref invalidTransitionCount);
                    }
                }
                finally
                {
                    stateGuard.Release();
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // No invalid transitions should have occurred
        await Assert.That(invalidTransitionCount).IsEqualTo(0);
        // Final state should be valid
        await Assert.That(Enum.IsDefined(state)).IsTrue();
    }

    [Test]
    public async Task ConcurrentOffsetCommitTracking_ConsistentState()
    {
        // Verifies the SemaphoreSlim-guarded offset commit pattern used by
        // KafkaConsumer's auto-commit and manual CommitAsync paths (pattern test,
        // not integration test). Simulates concurrent offset tracking between
        // auto-commit background task and manual CommitAsync calls. The _committed
        // dictionary must remain consistent.

        const int threadCount = 4;
        const int commitsPerThread = 200;
        var committed = new Dictionary<TopicPartition, long>();
        var commitLock = new SemaphoreSlim(1, 1);
        var partitions = Enumerable.Range(0, 4)
            .Select(p => new TopicPartition("test-topic", p))
            .ToArray();

        var barrier = new Barrier(threadCount);
        var tasks = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            var tp = partitions[threadIndex];
            for (var i = 0; i < commitsPerThread; i++)
            {
                await commitLock.WaitAsync();
                try
                {
                    // Simulate: only advance offset, never go backwards
                    if (!committed.TryGetValue(tp, out var current) || i > current)
                    {
                        committed[tp] = i;
                    }
                }
                finally
                {
                    commitLock.Release();
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Each partition should have committed to its maximum offset
        for (var p = 0; p < partitions.Length; p++)
        {
            await Assert.That(committed.ContainsKey(partitions[p])).IsTrue();
            await Assert.That(committed[partitions[p]]).IsEqualTo(commitsPerThread - 1);
        }
    }

    [Test]
    public async Task ConcurrentSeekAndPosition_ThreadSafe()
    {
        // Seek updates _positions and _fetchPositions via ConcurrentDictionary.
        // Concurrent seeks from different threads must not corrupt the dictionaries.

        const int threadCount = 8;
        const int seeksPerThread = 200;
        var positions = new ConcurrentDictionary<TopicPartition, long>();
        var fetchPositions = new ConcurrentDictionary<TopicPartition, long>();
        var partitions = Enumerable.Range(0, 4)
            .Select(p => new TopicPartition("test-topic", p))
            .ToArray();

        var barrier = new Barrier(threadCount);
        var tasks = Enumerable.Range(0, threadCount).Select(threadIndex => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < seeksPerThread; i++)
            {
                var tp = partitions[threadIndex % partitions.Length];
                var offset = (long)(threadIndex * seeksPerThread + i);

                // Simulate Seek() operation
                positions[tp] = offset;
                fetchPositions[tp] = offset;
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // All partitions should have valid positions
        foreach (var tp in partitions)
        {
            await Assert.That(positions.ContainsKey(tp)).IsTrue();
            await Assert.That(fetchPositions.ContainsKey(tp)).IsTrue();
            await Assert.That(positions[tp]).IsGreaterThanOrEqualTo(0);
            await Assert.That(fetchPositions[tp]).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task DisposeWhileConsuming_NoDeadlock()
    {
        // Simulates the pattern where DisposeAsync races with an active
        // ConsumeAsync loop (via channel drain). Must not deadlock.

        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = false
        });

        var disposed = false;
        var consumedCount = 0;
        var consumedAtLeastOne = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();

        // Writer task (simulates prefetch)
        var writerTask = Task.Run(async () =>
        {
            var i = 0;
            while (!Volatile.Read(ref disposed))
            {
                try
                {
                    await channel.Writer.WriteAsync(i++, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ChannelClosedException)
                {
                    break;
                }
            }
        });

        // Reader task (simulates consume loop)
        var readerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in channel.Reader.ReadAllAsync(cts.Token))
                {
                    Interlocked.Increment(ref consumedCount);
                    consumedAtLeastOne.TrySetResult();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during disposal
            }
        });

        // Wait for the consumer to process at least one message, then "dispose"
        await consumedAtLeastOne.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Volatile.Write(ref disposed, true);
        cts.Cancel();
        channel.Writer.TryComplete();

        // Must complete within timeout (no deadlock)
        // Use async wait to avoid thread pool starvation when running 25x in parallel
        await Task.WhenAll(writerTask, readerTask).WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(consumedCount).IsGreaterThan(0);
    }
}
