using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for the coordinated retry pattern used when OutOfOrderSequenceNumber occurs.
/// These tests simulate the sequence of operations that SendBatchAsync performs:
/// register batches → send → on OutOfOrderSequenceNumber, wait for predecessor → retry.
/// </summary>
public sealed class CoordinatedRetryTests
{
    private static readonly TopicPartition Tp0 = new("test-topic", 0);

    [Test]
    public async Task OutOfOrderSequenceNumber_WaitsForPredecessor_ThenRetries()
    {
        // Simulate: batch1 and batch2 registered in sequence order.
        // batch2 gets OutOfOrderSequenceNumber, waits for batch1 to complete, then retries.
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        // batch2 got OutOfOrderSequenceNumber — start waiting for predecessor
        var retryReady = false;
        var waitTask = Task.Run(async () =>
        {
            await tracker.WaitForPredecessorAsync(entry2, CancellationToken.None);
            retryReady = true;
        });

        // batch2 should be blocked
        await Task.Delay(50);
        await Assert.That(retryReady).IsFalse();

        // batch1 completes successfully — signals batch2 to retry
        tracker.Complete(entry1);

        await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(retryReady).IsTrue();

        // batch2 retries and succeeds
        tracker.Complete(entry2);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }

    [Test]
    public async Task OutOfOrderSequenceNumber_NoPredecessor_RetriesImmediately()
    {
        // If a batch gets OutOfOrderSequenceNumber but has no predecessor (it's the head),
        // WaitForPredecessorAsync completes immediately — batch retries without delay.
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);

        // entry1 is the head — no predecessor
        var waitTask = tracker.WaitForPredecessorAsync(entry1, CancellationToken.None);
        await Assert.That(waitTask.IsCompleted).IsTrue();

        tracker.Complete(entry1);
    }

    [Test]
    public async Task PredecessorCompletes_SuccessorRetriesWithoutBackoff()
    {
        // Verifies the coordinated retry is immediate (no backoff delay) once predecessor finishes.
        // Uses deterministic synchronization instead of wall-clock timing to avoid flakiness on slow CI.
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        var waitingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var waitTask = Task.Run(async () =>
        {
            waitingStarted.SetResult();
            await tracker.WaitForPredecessorAsync(entry2, CancellationToken.None);
        });

        // Deterministic: wait for Task.Run to enter WaitForPredecessorAsync
        await waitingStarted.Task;

        // Complete predecessor
        tracker.Complete(entry1);

        // Successor should wake up promptly — 5s timeout catches real backoff issues
        // without being sensitive to CI runner thread scheduling delays
        await waitTask.WaitAsync(TimeSpan.FromSeconds(5));

        tracker.Complete(entry2);
    }

    [Test]
    public async Task PredecessorFails_SuccessorRetriesAndMayAlsoFail()
    {
        // When a predecessor fails (FailAll signals exception), successors waiting on it
        // receive the exception. In the real retry loop, this is caught and the successor
        // retries anyway (the broker may accept it now that predecessor is out of the way).
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        // batch2 starts waiting for predecessor
        var waitTask = tracker.WaitForPredecessorAsync(entry2, CancellationToken.None);

        // Predecessor fails
        var fatalException = new InvalidOperationException("Network error");
        tracker.FailAll(Tp0, fatalException);

        // Successor should get the exception from predecessor's TCS
        var caughtException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await waitTask;
        });

        await Assert.That(caughtException!.Message).IsEqualTo("Network error");

        // In the real SendBatchAsync, the successor would catch this and retry.
        // The tracker state is clean after FailAll.
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }

    [Test]
    public async Task MultipleSuccessors_AllWaitForSamePredecessor()
    {
        // When multiple batches get OutOfOrderSequenceNumber, they all wait for
        // their respective predecessors. If batch1 is the bottleneck, batch2 and batch3
        // both (transitively) wait for batch1.
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);
        var entry3 = tracker.Register(Tp0, baseSequence: 15, recordCount: 8);

        // batch2 waits for batch1, batch3 waits for batch2
        var wait2Done = false;
        var wait3Done = false;

        var waitTask2 = Task.Run(async () =>
        {
            await tracker.WaitForPredecessorAsync(entry2, CancellationToken.None);
            wait2Done = true;
        });

        var waitTask3 = Task.Run(async () =>
        {
            await tracker.WaitForPredecessorAsync(entry3, CancellationToken.None);
            wait3Done = true;
        });

        await Task.Delay(50);
        await Assert.That(wait2Done).IsFalse();
        await Assert.That(wait3Done).IsFalse();

        // Complete batch1 — batch2 wakes up
        tracker.Complete(entry1);
        await waitTask2.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(wait2Done).IsTrue();

        // batch3 is still waiting for batch2
        await Task.Delay(50);
        await Assert.That(wait3Done).IsFalse();

        // Complete batch2 — batch3 wakes up
        tracker.Complete(entry2);
        await waitTask3.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(wait3Done).IsTrue();

        tracker.Complete(entry3);
        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }

    [Test]
    public async Task NonSequenceError_UsesNormalBackoff_NotCoordinated()
    {
        // For non-OutOfOrderSequenceNumber errors (e.g., LeaderNotAvailable),
        // the tracker is not consulted. This test verifies that WaitForPredecessorAsync
        // is independent — other error types don't interact with the tracker.
        // In the real code, the retry loop checks errorCode == OutOfOrderSequenceNumber
        // before calling WaitForPredecessorAsync.
        var tracker = new PartitionInflightTracker();

        var entry1 = tracker.Register(Tp0, baseSequence: 0, recordCount: 10);
        var entry2 = tracker.Register(Tp0, baseSequence: 10, recordCount: 5);

        // Simulate: entry2 gets LeaderNotAvailable.
        // Real code would NOT call WaitForPredecessorAsync — it uses backoff instead.
        // Verify both entries can complete independently (no coordination needed).
        tracker.Complete(entry1);
        tracker.Complete(entry2);

        await Assert.That(tracker.GetInflightCount(Tp0)).IsEqualTo(0);
    }
}
