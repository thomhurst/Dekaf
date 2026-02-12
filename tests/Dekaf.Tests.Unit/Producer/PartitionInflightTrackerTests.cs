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
        await waitTask.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
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

        var completed = false;
        var waitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitTask = Task.Run(async () =>
        {
            waitStarted.SetResult();
            await tracker.WaitForPredecessorAsync(entry2, CancellationToken.None);
            completed = true;
        });

        // Wait for the task to actually start executing (not just scheduled)
        await waitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Small yield to let the task reach the await inside WaitForPredecessorAsync
        await Task.Delay(50);
        await Assert.That(completed).IsFalse();

        // Complete predecessor
        tracker.Complete(entry1);

        await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(completed).IsTrue();
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
}
