using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

public sealed class PartitionInflightTrackerTests
{
    private static readonly TopicPartition Tp0 = new("test-topic", 0);
    private static readonly TopicPartition Tp1 = new("test-topic", 1);

    [Test]
    public async Task Register_SingleBatch_ReturnsEntryWithCorrectProperties()
    {
        var tracker = new PartitionInflightTracker();

        var entry = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);

        await Assert.That(entry.TopicPartition).IsEqualTo(Tp0);
        await Assert.That(entry.BaseSequence).IsEqualTo(0);
        await Assert.That(entry.RecordCount).IsEqualTo(10);
    }

    [Test]
    public async Task Register_MultipleBatches_MaintainsInsertionOrder()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);
        var entry3 = tracker.Register(Tp0, baseSequence: 15, recordCount: 8);

        // entry1 is head: no previous, next is entry2
        await Assert.That(entry1.Previous).IsNull();
        await Assert.That(entry1.Next).IsEqualTo(entry2);

        // entry2 is middle
        await Assert.That(entry2.Previous).IsEqualTo(entry1);
        await Assert.That(entry2.Next).IsEqualTo(entry3);

        // entry3 is tail: previous is entry2, no next
        await Assert.That(entry3.Previous).IsEqualTo(entry2);
        await Assert.That(entry3.Next).IsNull();
    }

    [Test]
    public async Task Register_DifferentPartitions_AreIndependent()
    {
        var tracker = new PartitionInflightTracker();

        var entry0 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry1 = tracker.Register(Tp1, baseSequence: 0, recordCount: 5);

        // Each partition has its own list — no cross-links
        await Assert.That(entry0.Previous).IsNull();
        await Assert.That(entry0.Next).IsNull();
        await Assert.That(entry1.Previous).IsNull();
        await Assert.That(entry1.Next).IsNull();

        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(1);
        await Assert.That(tracker.GetInflightCount(Tp1)).IsEqualTo(1);
    }

    [Test]
    public async Task Complete_HeadEntry_RemovesFromList()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        tracker.Complete(entry1);

        // entry2 is now head
        await Assert.That(entry2.Previous).IsNull();
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(1);
    }

    [Test]
    public async Task Complete_MiddleEntry_UnlinksCorrectly()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);
        var entry3 = tracker.Register(Tp0, baseSequence: 15, recordCount: 8);

        tracker.Complete(entry2);

        // entry1 and entry3 are now linked directly
        await Assert.That(entry1.Next).IsEqualTo(entry3);
        await Assert.That(entry3.Previous).IsEqualTo(entry1);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(2);
    }

    [Test]
    public async Task Complete_TailEntry_UpdatesTailPointer()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        tracker.Complete(entry2);

        // entry1 is now both head and tail
        await Assert.That(entry1.Next).IsNull();
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(1);
    }

    [Test]
    public async Task Complete_OnlyEntry_LeavesEmptyList()
    {
        var tracker = new PartitionInflightTracker();

        var entry = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);

        tracker.Complete(entry);

        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }

    [Test]
    public async Task Complete_SignalsPredecessorTCS_WakesSuccessor()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        // Successor starts waiting for predecessor
        var waitTask = tracker.WaitForPredecessorAsync(entry2, CancellationToken.None);

        // Not complete yet
        await Assert.That(waitTask.IsCompleted).IsFalse();

        // Complete predecessor — should wake successor
        tracker.Complete(entry1);

        // Wait should complete promptly
        await waitTask.AsTask().WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Test]
    public async Task WaitForPredecessor_NoPredecessor_CompletesImmediately()
    {
        var tracker = new PartitionInflightTracker();

        var entry = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);

        // No predecessor — should complete immediately
        var waitTask = tracker.WaitForPredecessorAsync(entry, CancellationToken.None);

        await Assert.That(waitTask.IsCompleted).IsTrue();
    }

    [Test]
    public async Task WaitForPredecessor_PredecessorAlreadyComplete_CompletesImmediately()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        // Complete predecessor first
        tracker.Complete(entry1);

        // entry2 now has no predecessor (entry1 was unlinked) — completes immediately
        var waitTask = tracker.WaitForPredecessorAsync(entry2, CancellationToken.None);
        await Assert.That(waitTask.IsCompleted).IsTrue();
    }

    [Test]
    public async Task WaitForPredecessor_PredecessorPending_BlocksUntilComplete()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        // WaitForPredecessorAsync enters the lock synchronously, finds the predecessor,
        // gets its TCS task, then hits the first real await — so the returned ValueTask
        // is guaranteed to be incomplete at this point (predecessor hasn't been completed).
        var waitTask = tracker.WaitForPredecessorAsync(entry2, CancellationToken.None);

        await Assert.That(waitTask.IsCompleted).IsFalse();

        // Complete predecessor — signals the TCS, which unblocks the wait
        tracker.Complete(entry1);

        // Should complete promptly now
        await waitTask.AsTask().WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task WaitForPredecessor_Cancellation_ThrowsOperationCancelled()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        using var cts = new CancellationTokenSource();
        var waitTask = tracker.WaitForPredecessorAsync(entry2, cts.Token);

        // Cancel while waiting
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await waitTask;
        });
    }

    [Test]
    public async Task WaitForPredecessor_LazyTCS_OnlyCreatedWhenNeeded()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        // Complete entry1 without anyone waiting — TCS should never be created
        tracker.Complete(entry1);

        // entry2 has no predecessor now, so WaitForPredecessor returns immediately
        // without creating a TCS on entry2
        await tracker.WaitForPredecessorAsync(entry2, CancellationToken.None);

        // Clean up
        tracker.Complete(entry2);
    }

    [Test]
    public async Task FailAll_SignalsAllEntriesWithException()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);
        var entry3 = tracker.Register(Tp0, baseSequence: 15, recordCount: 8);

        // Start waiting on predecessors
        var wait2 = tracker.WaitForPredecessorAsync(entry2, CancellationToken.None);
        var wait3 = tracker.WaitForPredecessorAsync(entry3, CancellationToken.None);

        var testException = new InvalidOperationException("Fatal error");
        tracker.FailAll(Tp0, testException);

        // entry2 was waiting on entry1 — should get the exception
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await wait2;
        });

        // entry3 was waiting on entry2 — should also get the exception
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await wait3;
        });

        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }

    [Test]
    public async Task FailAll_EmptyPartition_NoOp()
    {
        var tracker = new PartitionInflightTracker();

        // Should not throw
        tracker.FailAll(Tp0, new InvalidOperationException("test"));

        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }

    [Test]
    public async Task GetInflightCount_ReflectsRegistrationsAndCompletions()
    {
        var tracker = new PartitionInflightTracker();

        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(1);

        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(2);

        tracker.Complete(entry1);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(1);

        tracker.Complete(entry2);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }

    [Test]
    public async Task ConcurrentRegisterAndComplete_NoCorruption()
    {
        var tracker = new PartitionInflightTracker();
        const int iterations = 1000;

        var tasks = new List<Task>();

        for (var i = 0; i < iterations; i++)
        {
            var seq = i;
            tasks.Add(Task.Run(() =>
            {
                var entry = tracker.Register(Tp0, baseSequence: seq, recordCount: 1);
                // Simulate brief work
                Thread.SpinWait(10);
                tracker.Complete(entry);
            }));
        }

        await Task.WhenAll(tasks);

        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }

    [Test]
    public async Task EntryPooling_RentAndReturn_ReusesObjects()
    {
        var pool = new InflightEntryPool(maxPoolSize: 4);

        // Rent entries
        var entry1 = pool.Rent();
        var entry2 = pool.Rent();

        // Return them
        pool.Return(entry1);
        pool.Return(entry2);

        // Rent again — should get the same objects back (from pool)
        var rented1 = pool.Rent();
        var rented2 = pool.Rent();

        // At least one should be reused (ConcurrentStack is LIFO)
        var reused = ReferenceEquals(rented1, entry2) || ReferenceEquals(rented1, entry1)
                  || ReferenceEquals(rented2, entry2) || ReferenceEquals(rented2, entry1);

        await Assert.That(reused).IsTrue();
    }

    [Test]
    public async Task EntryPooling_SustainedRentReturnAtCapacity_ReusesAllEntries()
    {
        // Reproduces the failure mode that previously caused Gen2 promotion in single-broker
        // idempotent stress: when sustained working set fits within MaxPoolSize, every Rent
        // after warm-up must be a pool hit (no fresh allocations).
        const int capacity = 16;
        var pool = new InflightEntryPool(maxPoolSize: capacity);

        // Warm the pool to capacity.
        var warmRented = new InflightEntry[capacity];
        for (var i = 0; i < capacity; i++) warmRented[i] = pool.Rent();
        for (var i = 0; i < capacity; i++) pool.Return(warmRented[i]);

        var missesBefore = pool.Misses;

        // Sustained churn at capacity: rent N, return N, repeated.
        for (var iter = 0; iter < 100; iter++)
        {
            var batch = new InflightEntry[capacity];
            for (var i = 0; i < capacity; i++) batch[i] = pool.Rent();
            for (var i = 0; i < capacity; i++) pool.Return(batch[i]);
        }

        // Zero misses after warm-up — nothing should fall through to `new InflightEntry()`.
        await Assert.That(pool.Misses).IsEqualTo(missesBefore);
    }

    [Test]
    public async Task ReadyBatch_Reset_ClearsInflightEntry()
    {
        var pool = new ReadyBatchPool();
        var batch = pool.Rent();

        var entry = new InflightEntry();
        entry.Initialize(Tp0, baseSequence: 0, recordCount: 10);
        batch.InflightEntry = entry;

        await Assert.That(batch.InflightEntry).IsNotNull();

        pool.Return(batch); // Return calls Reset()

        // After reset, InflightEntry should be null
        await Assert.That(batch.InflightEntry).IsNull();
    }

    // --- Pruning tests ---

    [Test]
    public async Task GetTrackedPartitionCount_ReflectsRegisteredPartitions()
    {
        var tracker = new PartitionInflightTracker();

        await Assert.That(tracker.GetTrackedPartitionCount()).IsEqualTo(0);

        tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        await Assert.That(tracker.GetTrackedPartitionCount()).IsEqualTo(1);

        tracker.Register(Tp1, baseSequence: 0, recordCount: 5);
        await Assert.That(tracker.GetTrackedPartitionCount()).IsEqualTo(2);
    }

    [Test]
    public async Task PruneWithCutoff_RemovesIdlePartitions()
    {
        var tracker = new PartitionInflightTracker();

        var entry = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        tracker.Complete(entry);

        // Partition is now idle (Count == 0). Prune with a cutoff far in the future
        // to guarantee the idle timestamp is older than the cutoff.
        var cutoff = long.MaxValue;
        tracker.PruneWithCutoff(cutoff);

        await Assert.That(tracker.GetTrackedPartitionCount()).IsEqualTo(0);
    }

    [Test]
    public async Task PruneWithCutoff_DoesNotRemoveActivePartitions()
    {
        var tracker = new PartitionInflightTracker();

        // Register but do NOT complete — partition has inflight entries
        tracker.Register(Tp0, baseSequence: 0, recordCount: 10);

        tracker.PruneWithCutoff(long.MaxValue);

        // Should still be tracked because Count > 0
        await Assert.That(tracker.GetTrackedPartitionCount()).IsEqualTo(1);
    }

    [Test]
    public async Task PruneWithCutoff_DoesNotRemoveRecentlyIdlePartitions()
    {
        var tracker = new PartitionInflightTracker();

        var entry = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        tracker.Complete(entry);

        // Prune with cutoff of 0 — the idle timestamp will be newer than this
        tracker.PruneWithCutoff(0);

        // Should still be tracked because it became idle after the cutoff
        await Assert.That(tracker.GetTrackedPartitionCount()).IsEqualTo(1);
    }

    [Test]
    public async Task PruneWithCutoff_RegisterAfterPrune_CreatesNewState()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        tracker.Complete(entry1);

        tracker.PruneWithCutoff(long.MaxValue);
        await Assert.That(tracker.GetTrackedPartitionCount()).IsEqualTo(0);

        // Re-register on the same partition — should create fresh state
        var entry2 = tracker.Register(Tp0, baseSequence: 100, recordCount: 5);
        await Assert.That(tracker.GetTrackedPartitionCount()).IsEqualTo(1);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(1);

        tracker.Complete(entry2);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }

    [Test]
    public async Task Complete_MultipleEntries_WorksWithStoredState()
    {
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        tracker.Complete(entry1);
        tracker.Complete(entry2);

        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }

    [Test]
    public async Task PruneWithCutoff_ConcurrentRegisterRace_DoesNotPruneActivePartition()
    {
        // Verifies that if a Register happens between the pruner reading Count==0
        // and attempting removal, the partition is NOT removed.
        var tracker = new PartitionInflightTracker();

        var entry = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        tracker.Complete(entry);

        // Re-register immediately (simulating concurrent Register)
        tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        // Now prune — should NOT remove because Count is now 1
        tracker.PruneWithCutoff(long.MaxValue);

        await Assert.That(tracker.GetTrackedPartitionCount()).IsEqualTo(1);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(1);
    }

    [Test]
    public async Task Dispose_PreventsTimerFromFiring()
    {
        var tracker = new PartitionInflightTracker();
        tracker.Dispose();

        // After dispose, register/complete should still work (no timer needed)
        var entry = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        tracker.Complete(entry);

        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }
}
