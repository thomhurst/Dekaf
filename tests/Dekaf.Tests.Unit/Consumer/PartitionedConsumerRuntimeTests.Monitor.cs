using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class PartitionedConsumerRuntimeTests
{
    [Test]
    public async Task Monitor_StuckPartition_ReportsItsRecordWhileOtherPartitionsProgress()
    {
        var stuck = new TopicPartition("topic-a", 0);
        var healthy = new TopicPartition("topic-a", 1);
        var consumer = new TestConsumer();
        consumer.SetAssignment(stuck, healthy);
        var monitor = new PartitionedProcessingMonitor();
        var stuckStarted = NewCompletionSource();
        var releaseStuck = NewCompletionSource();
        var healthyProcessed = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await Assert.That(monitor.OldestOutstandingRecordAt).IsNull();
        var before = DateTimeOffset.UtcNow;
        var runTask = consumer.RunPartitionedAsync(
            async (context, message, cancellationToken) =>
            {
                if (context.TopicPartition == stuck)
                {
                    stuckStarted.TrySetResult();
                    await releaseStuck.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    Interlocked.Increment(ref healthyProcessed);
                }
            },
            new PartitionedProcessingOptions { Monitor = monitor },
            cts.Token).AsTask();

        try
        {
            consumer.Enqueue(CreateResult(stuck, 0));
            await stuckStarted.Task.WaitAsync(cts.Token);

            var oldest = monitor.OldestOutstandingRecordAt;
            await Assert.That(oldest).IsNotNull();
            await Assert.That(oldest!.Value).IsGreaterThanOrEqualTo(before);
            await Assert.That(oldest.Value).IsLessThanOrEqualTo(DateTimeOffset.UtcNow);

            consumer.Enqueue(Enumerable.Range(0, 10).Select(offset => CreateResult(healthy, offset)).ToArray());
            await Assert.That(() => Volatile.Read(ref healthyProcessed))
                .Eventually(count => count.IsEqualTo(10), TimeSpan.FromSeconds(10));

            await Assert.That(monitor.OldestOutstandingRecordAt).IsEqualTo(oldest);

            releaseStuck.TrySetResult();
            await Assert.That(() => monitor.OldestOutstandingRecordAt)
                .Eventually(value => value.IsNull(), TimeSpan.FromSeconds(10));
        }
        finally
        {
            releaseStuck.TrySetResult();
            await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
        }

        await Assert.That(monitor.OldestOutstandingRecordAt).IsNull();
    }

    [Test]
    public async Task Monitor_PartitionProcessor_RecordStaysOutstandingUntilMarkedProcessed()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var monitor = new PartitionedProcessingMonitor();
        var received = NewCompletionSource();
        var mark = NewCompletionSource();
        var marked = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    received.TrySetResult();
                    await mark.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    context.MarkProcessed(message);
                    marked.TrySetResult();
                }
            },
            new PartitionedProcessingOptions
            {
                Monitor = monitor,
                CommitPolicy = PartitionCommitPolicy.UserManaged
            },
            cts.Token).AsTask();

        try
        {
            consumer.Enqueue(CreateResult(partition, 0));
            await received.Task.WaitAsync(cts.Token);
            await Assert.That(monitor.OldestOutstandingRecordAt).IsNotNull();

            mark.TrySetResult();
            await marked.Task.WaitAsync(cts.Token);
            await Assert.That(monitor.OldestOutstandingRecordAt).IsNull();
        }
        finally
        {
            mark.TrySetResult();
            await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
        }
    }

    [Test]
    public async Task Monitor_AttachedToRunningConsumer_SecondRunThrows()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var secondConsumer = new TestConsumer();
        secondConsumer.SetAssignment(partition);
        var monitor = new PartitionedProcessingMonitor();
        var options = new PartitionedProcessingOptions { Monitor = monitor };
        var started = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var runTask = consumer.RunPartitionedAsync(
            (_, _, _) =>
            {
                started.TrySetResult();
                return ValueTask.CompletedTask;
            },
            options,
            cts.Token).AsTask();

        try
        {
            consumer.Enqueue(CreateResult(partition, 0));
            await started.Task.WaitAsync(cts.Token);

            await Assert.That(async () => await secondConsumer.RunPartitionedAsync(
                    (_, _, _) => ValueTask.CompletedTask,
                    options,
                    cts.Token))
                .Throws<InvalidOperationException>();
        }
        finally
        {
            await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
        }

        // The rejected run leaves the first run attached, and the first run detaches when it
        // ends, so the same monitor can observe a later run.
        using var secondCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var secondStarted = NewCompletionSource();
        var secondRun = secondConsumer.RunPartitionedAsync(
            (_, _, _) =>
            {
                secondStarted.TrySetResult();
                return ValueTask.CompletedTask;
            },
            options,
            secondCts.Token).AsTask();
        secondConsumer.Enqueue(CreateResult(partition, 0));
        await secondStarted.Task.WaitAsync(secondCts.Token);
        await StopRuntimeAsync(secondCts, secondRun).ConfigureAwait(false);
    }

    [Test]
    public async Task Monitor_OffsetGapAfterCompletedBatch_ReportsTheNewBatchArrival()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var monitor = new PartitionedProcessingMonitor();
        var firstStarted = NewCompletionSource();
        var releaseFirst = NewCompletionSource();
        var gapStarted = NewCompletionSource();
        var releaseGap = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var runTask = consumer.RunPartitionedAsync(
            async (_, message, cancellationToken) =>
            {
                var (started, release) = message.Offset == 0 ? (firstStarted, releaseFirst) : (gapStarted, releaseGap);
                started.TrySetResult();
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            },
            new PartitionedProcessingOptions { Monitor = monitor },
            cts.Token).AsTask();

        try
        {
            consumer.Enqueue(CreateResult(partition, 0));
            await firstStarted.Task.WaitAsync(cts.Token);
            var firstArrival = monitor.OldestOutstandingRecordAt;
            await Assert.That(firstArrival).IsNotNull();

            releaseFirst.TrySetResult();
            await Assert.That(() => monitor.OldestOutstandingRecordAt)
                .Eventually(value => value.IsNull(), TimeSpan.FromSeconds(10));

            // Let the clock move past the first arrival so the two batches have distinct times.
            var spinner = new SpinWait();
            while (DateTimeOffset.UtcNow <= firstArrival!.Value)
                spinner.SpinOnce();

            // Offsets 1-9 are never delivered, so the commit offset 1 is not a record offset.
            consumer.Enqueue(CreateResult(partition, 10));
            await gapStarted.Task.WaitAsync(cts.Token);

            var oldest = monitor.OldestOutstandingRecordAt;
            await Assert.That(oldest).IsNotNull();
            await Assert.That(oldest!.Value).IsGreaterThan(firstArrival.Value);
        }
        finally
        {
            releaseFirst.TrySetResult();
            releaseGap.TrySetResult();
            await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
        }
    }

    [Test]
    public async Task OutstandingRecordTimeline_ResolvesArrivalByBatchLastOffset()
    {
        var timeline = new OutstandingRecordTimeline();
        timeline.Append(lastEnqueuedOffset: -1, utcTicks: 100); // offsets 0-4
        timeline.Append(lastEnqueuedOffset: 4, utcTicks: 200);  // offsets 5-8
        timeline.Append(lastEnqueuedOffset: 8, utcTicks: 300);  // offsets 9-12, still open

        await Assert.That(Arrival(timeline, -1)).IsEqualTo(100L);
        await Assert.That(Arrival(timeline, 4)).IsEqualTo(100L);
        await Assert.That(Arrival(timeline, 5)).IsEqualTo(200L);
        await Assert.That(Arrival(timeline, 12)).IsEqualTo(300L);

        timeline.Prune(nextUnprocessedOffset: 6, lastEnqueuedOffset: 12);
        await Assert.That(Arrival(timeline, 6)).IsEqualTo(200L);
        await Assert.That(Arrival(timeline, 9)).IsEqualTo(300L);

        timeline.Prune(nextUnprocessedOffset: 13, lastEnqueuedOffset: 12);
        await Assert.That(timeline.TryGetArrivalUtcTicks(13, out _)).IsFalse();
    }

    [Test]
    public async Task OutstandingRecordTimeline_OffsetGapBetweenBatches_SkipsTheCompletedBatch()
    {
        var timeline = new OutstandingRecordTimeline();
        timeline.Append(lastEnqueuedOffset: -1, utcTicks: 100); // offset 0, completed
        timeline.Append(lastEnqueuedOffset: 0, utcTicks: 200);  // offset 10 after a gap

        // The commit offset after offset 0 completes is 1, which no record has.
        await Assert.That(Arrival(timeline, 1)).IsEqualTo(200L);
    }

    [Test]
    public async Task OutstandingRecordTimeline_GrowsAcrossWrappedEntries()
    {
        var timeline = new OutstandingRecordTimeline();
        for (var i = 0; i < 6; i++)
            timeline.Append(i * 10 - 1, i);
        // Advance the head so later appends wrap around the ring before it grows.
        timeline.Prune(nextUnprocessedOffset: 40, lastEnqueuedOffset: 1_000);
        for (var i = 6; i < 30; i++)
            timeline.Append(i * 10 - 1, i);

        await Assert.That(Arrival(timeline, 0)).IsEqualTo(4L);
        for (var i = 4; i < 30; i++)
            await Assert.That(Arrival(timeline, i * 10 + 5)).IsEqualTo((long)i);
    }

    [Test]
    public async Task OutstandingRecordTimeline_StalledFrontier_StaysBoundedAndNeverReportsLater()
    {
        var timeline = new OutstandingRecordTimeline();
        const int batches = OutstandingRecordTimeline.MaximumCapacity * 4;
        // One record per batch at offset i, received at tick i. Offset 0 never completes, so no
        // entry can be pruned while later batches keep arriving.
        for (var i = 0; i < batches; i++)
        {
            if (timeline.IsFull)
                timeline.Prune(nextUnprocessedOffset: 0, lastEnqueuedOffset: i - 1);
            timeline.Append(lastEnqueuedOffset: i - 1, utcTicks: i);
        }

        await Assert.That(timeline.Capacity).IsEqualTo(OutstandingRecordTimeline.MaximumCapacity);
        await Assert.That(Arrival(timeline, 0)).IsEqualTo(0L);
        for (var offset = 1; offset < batches; offset += 97)
        {
            var arrival = Arrival(timeline, offset);
            await Assert.That(arrival).IsGreaterThanOrEqualTo(0L);
            await Assert.That(arrival).IsLessThanOrEqualTo((long)offset);
        }
    }

    [Test]
    public async Task OutstandingRecordTimeline_ConcurrentReader_ReadsConsistentSnapshots()
    {
        var timeline = new OutstandingRecordTimeline();
        const int batches = 200_000;
        var failures = 0;
        var done = 0;

        var reader = Task.Run(() =>
        {
            while (Volatile.Read(ref done) == 0)
            {
                // Every record arrives at a tick equal to its offset, so a consistent snapshot
                // never reports a negative or future tick while the ring grows, wraps and merges.
                if (timeline.TryGetArrivalUtcTicks(0, out var ticks) && (ticks < 0 || ticks >= batches))
                    Interlocked.Increment(ref failures);
            }
        });

        for (var i = 0; i < batches; i++)
        {
            if (timeline.IsFull)
                timeline.Prune(nextUnprocessedOffset: i / 2, lastEnqueuedOffset: i - 1);
            timeline.Append(lastEnqueuedOffset: i - 1, utcTicks: i);
        }

        Volatile.Write(ref done, 1);
        await reader;
        await Assert.That(failures).IsEqualTo(0);
    }

    private static long Arrival(OutstandingRecordTimeline timeline, long offset)
        => timeline.TryGetArrivalUtcTicks(offset, out var ticks) ? ticks : -1;
}
