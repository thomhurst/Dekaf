using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class PartitionedConsumerRuntimeTests
{
    [Test]
    public async Task SeekAsync_ReplaysPartitionFromEarlierOffset()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 5).Select(offset => CreateResult(partition, offset)).ToArray());

        var control = new PartitionedProcessingControl();
        var received = new ConcurrentQueue<long>();
        var firstPass = NewCompletionSource();
        var replayed = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                received.Enqueue(message.Offset);
                if (message.Offset == 4 && !firstPass.TrySetResult())
                    replayed.TrySetResult();
                return ValueTask.CompletedTask;
            },
            new PartitionedProcessingOptions { Control = control, CommitPolicy = PartitionCommitPolicy.UserManaged },
            cts.Token).AsTask();

        await firstPass.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 2), cts.Token)
            .ConfigureAwait(false);
        await replayed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(string.Join(",", received)).IsEqualTo("0,1,2,3,4,2,3,4");
        await Assert.That(string.Join(",", consumer.SeekCalls.Select(offset => offset.Offset))).IsEqualTo("2");
    }

    [Test]
    public async Task SeekAsync_DrainsInFlightHandlerAndDiscardsBufferedRecords()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 10).Select(offset => CreateResult(partition, offset)).ToArray());

        var control = new PartitionedProcessingControl();
        var received = new ConcurrentQueue<long>();
        var completed = new ConcurrentQueue<long>();
        var firstRecordStarted = NewCompletionSource();
        var releaseFirstRecord = NewCompletionSource();
        var replayed = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (_, message, cancellationToken) =>
            {
                received.Enqueue(message.Offset);
                if (message.Offset == 0)
                {
                    firstRecordStarted.TrySetResult();
                    await releaseFirstRecord.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                completed.Enqueue(message.Offset);
                if (message.Offset == 9)
                    replayed.TrySetResult();
            },
            new PartitionedProcessingOptions
            {
                Control = control,
                CommitPolicy = PartitionCommitPolicy.UserManaged,
                StopPolicy = PartitionStopPolicy.Drain,
                SeekDiscardingLaneForTest = _ => releaseFirstRecord.TrySetResult()
            },
            cts.Token).AsTask();

        await firstRecordStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 7), cts.Token)
            .ConfigureAwait(false);
        await replayed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        // Drain lets the in-flight record finish; records 1-6 were buffered and are discarded.
        await Assert.That(string.Join(",", received)).IsEqualTo("0,7,8,9");
        await Assert.That(string.Join(",", completed)).IsEqualTo("0,7,8,9");
    }

    [Test]
    [Arguments(PartitionedProcessingOrder.Partition)]
    [Arguments(PartitionedProcessingOrder.Key)]
    public async Task SeekAsync_DiscardedRecordsReleaseConsumerWideBuffer(PartitionedProcessingOrder ordering)
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 10).Select(offset => CreateResult(partition, offset)).ToArray());

        var control = new PartitionedProcessingControl();
        var received = new ConcurrentQueue<long>();
        var firstRecordStarted = NewCompletionSource();
        var releaseFirstRecord = NewCompletionSource();
        var replayed = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (_, message, cancellationToken) =>
            {
                received.Enqueue(message.Offset);
                if (message.Offset == 0 && firstRecordStarted.TrySetResult())
                    await releaseFirstRecord.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (message.Offset == 9 && received.Count > 1)
                    replayed.TrySetResult();
            },
            new PartitionedProcessingOptions
            {
                Control = control,
                Ordering = ordering,
                CommitPolicy = PartitionCommitPolicy.UserManaged,
                StopPolicy = PartitionStopPolicy.Drain,
                MaxBufferedRecords = 5,
                SeekDiscardingLaneForTest = _ => releaseFirstRecord.TrySetResult()
            },
            cts.Token).AsTask();

        await firstRecordStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 0), cts.Token)
            .ConfigureAwait(false);

        // The replay needs the whole consumer-wide buffer again, so it completes only when the
        // seek released every record it discarded.
        await replayed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(string.Join(",", received)).IsEqualTo("0,0,1,2,3,4,5,6,7,8,9");
    }

    [Test]
    public async Task SeekAsync_CancelPolicyCancelsInFlightHandler()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 10).Select(offset => CreateResult(partition, offset)).ToArray());

        var control = new PartitionedProcessingControl();
        var received = new ConcurrentQueue<long>();
        var firstRecordStarted = NewCompletionSource();
        var firstRecordCancelled = NewCompletionSource();
        var replayed = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (_, message, cancellationToken) =>
            {
                received.Enqueue(message.Offset);
                if (message.Offset == 0)
                {
                    firstRecordStarted.TrySetResult();
                    try
                    {
                        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        firstRecordCancelled.TrySetResult();
                    }
                }

                if (message.Offset == 9)
                    replayed.TrySetResult();
            },
            new PartitionedProcessingOptions
            {
                Control = control,
                CommitPolicy = PartitionCommitPolicy.UserManaged,
                StopPolicy = PartitionStopPolicy.Cancel
            },
            cts.Token).AsTask();

        await firstRecordStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 7), cts.Token)
            .ConfigureAwait(false);
        await firstRecordCancelled.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await replayed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(string.Join(",", received)).IsEqualTo("0,7,8,9");
    }

    [Test]
    public async Task SeekAsync_KeyOrderedDispatcherDiscardsRecordsItBuffered()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        // One key: the dispatcher reads the records into that key's queue behind the in-flight one.
        consumer.Enqueue(Enumerable.Range(0, 10).Select(offset => CreateResult(partition, offset, key: "k")).ToArray());

        var control = new PartitionedProcessingControl();
        var received = new ConcurrentQueue<long>();
        var firstRecordStarted = NewCompletionSource();
        var releaseFirstRecord = NewCompletionSource();
        var replayed = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (_, message, cancellationToken) =>
            {
                received.Enqueue(message.Offset);
                if (message.Offset == 0)
                {
                    firstRecordStarted.TrySetResult();
                    await releaseFirstRecord.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                if (message.Offset == 9)
                    replayed.TrySetResult();
            },
            new PartitionedProcessingOptions
            {
                Control = control,
                Ordering = PartitionedProcessingOrder.Key,
                MaxConcurrentHandlersPerPartition = 2,
                CommitPolicy = PartitionCommitPolicy.UserManaged,
                SeekDiscardingLaneForTest = _ => releaseFirstRecord.TrySetResult()
            },
            cts.Token).AsTask();

        await firstRecordStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 7), cts.Token)
            .ConfigureAwait(false);
        await replayed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(string.Join(",", received)).IsEqualTo("0,7,8,9");
    }

    [Test]
    public async Task SeekAsync_CommitsSeekPositionUnderRuntimeManagedPolicy()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 5).Select(offset => CreateResult(partition, offset)).ToArray());

        var control = new PartitionedProcessingControl();
        var firstPass = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                if (message.Offset == 4)
                    firstPass.TrySetResult();
                return ValueTask.CompletedTask;
            },
            new PartitionedProcessingOptions { Control = control, CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke },
            cts.Token).AsTask();

        await firstPass.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 2), cts.Token)
            .ConfigureAwait(false);
        var seekCommits = consumer.CommitCalls.SelectMany(offsets => offsets).Select(offset => offset.Offset).ToArray();
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        // Completed offset 5 is past the target, so the seek commits the target instead.
        await Assert.That(string.Join(",", seekCommits)).IsEqualTo("2");
    }

    [Test]
    public async Task SeekAsync_ForwardSeekCommitsCompletedOffsetBelowTarget()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 5).Select(offset => CreateResult(partition, offset)).ToArray());

        var control = new PartitionedProcessingControl();
        var firstPass = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                if (message.Offset == 4)
                    firstPass.TrySetResult();
                return ValueTask.CompletedTask;
            },
            new PartitionedProcessingOptions { Control = control, CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke },
            cts.Token).AsTask();

        await firstPass.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 8), cts.Token)
            .ConfigureAwait(false);
        var seekCommits = consumer.CommitCalls.SelectMany(offsets => offsets).Select(offset => offset.Offset).ToArray();
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(string.Join(",", seekCommits)).IsEqualTo("5");
        await Assert.That(string.Join(",", consumer.SeekCalls.Select(offset => offset.Offset))).IsEqualTo("8");
    }

    [Test]
    [Arguments(7L, 0L, "")]
    [Arguments(2L, 5L, "2")]
    public async Task SeekAsync_WithoutCompletedRecordsCommitsOnlyBehindCommittedOffset(
        long target,
        long committed,
        string expectedCommits)
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true, CommittedOffset = committed };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 10).Select(offset => CreateResult(partition, offset)).ToArray());

        var control = new PartitionedProcessingControl();
        var firstRecordStarted = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (_, message, cancellationToken) =>
            {
                if (message.Offset != 0)
                    return;

                // Seeking cancels the only record the lane received, so nothing completes.
                firstRecordStarted.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            },
            new PartitionedProcessingOptions
            {
                Control = control,
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
                StopPolicy = PartitionStopPolicy.Cancel
            },
            cts.Token).AsTask();

        await firstRecordStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, target), cts.Token)
            .ConfigureAwait(false);
        var seekCommits = consumer.CommitCalls.SelectMany(offsets => offsets).Select(offset => offset.Offset).ToArray();
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(string.Join(",", seekCommits)).IsEqualTo(expectedCommits);
    }

    [Test]
    public async Task SeekAsync_ProcessorReturningOnDiscardIsNotAFailure()
    {
        // A key-ordered dispatcher returns as soon as it sees the discard flag, which can be
        // before the seek's stop completes the lane. That exit must not count as a failure.
        Exception? failure = null;
        var lane = new PartitionLane<string, string>(
            new TopicPartition("topic-a", 0), 16,
            static (_, _) => default, static _ => { }, (_, exception) => failure = exception);
        var discarding = NewCompletionSource();
        lane.Start(async (context, cancellationToken) =>
        {
            await discarding.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!context.IsDiscardingQueuedRecords)
                throw new InvalidOperationException("The discard flag was not published.");
        });

        lane.DiscardQueuedRecords();
        discarding.TrySetResult();
        var processorTask = (Task)typeof(PartitionLane<string, string>)
            .GetField("_processorTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(lane)!;
        await processorTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        await Assert.That(failure).IsNull();
        await Assert.That(await lane.StopAsync(PartitionStopPolicy.Drain, TimeSpan.FromSeconds(10)).ConfigureAwait(false))
            .IsNull();
    }

    [Test]
    public async Task SeekAsync_RestartsPartitionStoppedByFailure()
    {
        var partition = new TopicPartition("topic-a", 0);
        var failed = NewCompletionSource();
        var consumer = new TestConsumer
        {
            RedeliverUnenumeratedBatches = true,
            PausedCallback = _ => failed.TrySetResult()
        };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 3).Select(offset => CreateResult(partition, offset)).ToArray());

        var control = new PartitionedProcessingControl();
        var received = new ConcurrentQueue<long>();
        var failures = 0;
        var replayed = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                received.Enqueue(message.Offset);
                if (message.Offset == 1 && Interlocked.Increment(ref failures) == 1)
                {
                    throw new InvalidOperationException("handler bug");
                }

                if (message.Offset == 2)
                    replayed.TrySetResult();
                return ValueTask.CompletedTask;
            },
            new PartitionedProcessingOptions
            {
                Control = control,
                CommitPolicy = PartitionCommitPolicy.UserManaged,
                ErrorPolicy = PartitionWorkerErrorPolicy.StopPartition
            },
            cts.Token).AsTask();

        await failed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        // StopPartition pauses the partition once the runtime has stopped its failed lane.
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 0), cts.Token)
            .ConfigureAwait(false);
        await replayed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(string.Join(",", received)).IsEqualTo("0,1,0,1,2");
        await Assert.That(consumer.Paused.Contains(partition)).IsFalse();
    }

    [Test]
    public async Task SeekAsync_FailureOfReplacedLaneDoesNotStopItsReplacement()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 10).Select(offset => CreateResult(partition, offset)).ToArray());

        var control = new PartitionedProcessingControl();
        var received = new ConcurrentQueue<long>();
        var firstRecordStarted = NewCompletionSource();
        var releaseFirstRecord = NewCompletionSource();
        var replayed = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (_, message, cancellationToken) =>
            {
                received.Enqueue(message.Offset);
                if (message.Offset == 0)
                {
                    firstRecordStarted.TrySetResult();
                    await releaseFirstRecord.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException("old lane fails while the seek drains it");
                }

                if (message.Offset == 9)
                    replayed.TrySetResult();
            },
            new PartitionedProcessingOptions
            {
                Control = control,
                CommitPolicy = PartitionCommitPolicy.UserManaged,
                ErrorPolicy = PartitionWorkerErrorPolicy.StopPartition,
                SeekDiscardingLaneForTest = _ => releaseFirstRecord.TrySetResult()
            },
            cts.Token).AsTask();

        await firstRecordStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 7), cts.Token)
            .ConfigureAwait(false);
        await replayed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(string.Join(",", received)).IsEqualTo("0,7,8,9");
        await Assert.That(consumer.Paused.Contains(partition)).IsFalse();
    }

    [Test]
    public async Task SeekAsync_TimestampResolvesOffsetAndFallsBackToEnd()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer
        {
            RedeliverUnenumeratedBatches = true,
            OffsetForTime = timestamp => timestamp.Timestamp switch
            {
                1_000 => 3,
                TopicPartitionTimestamp.Latest => 5,
                _ => -1
            }
        };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 5).Select(offset => CreateResult(partition, offset)).ToArray());

        var control = new PartitionedProcessingControl();
        var firstPass = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, message, _) =>
            {
                if (message.Offset == 4)
                    firstPass.TrySetResult();
                return ValueTask.CompletedTask;
            },
            new PartitionedProcessingOptions { Control = control, CommitPolicy = PartitionCommitPolicy.UserManaged },
            cts.Token).AsTask();

        await firstPass.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionTimestamp(partition, 1_000), cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionTimestamp(partition, 9_999), cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(string.Join(",", consumer.SeekCalls.Select(offset => offset.Offset))).IsEqualTo("3,5");
    }

    [Test]
    public async Task SeekAsync_UnassignedPartitionThrows()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);

        var control = new PartitionedProcessingControl();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, _, _) => ValueTask.CompletedTask,
            new PartitionedProcessingOptions { Control = control },
            cts.Token).AsTask();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await control.SeekAsync(new TopicPartitionOffset("topic-a", 1, 0), cts.Token).ConfigureAwait(false));
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(exception!.Message).Contains("not assigned");
        await Assert.That(consumer.SeekCalls).IsEmpty();
    }

    [Test]
    public async Task SeekAsync_RequiresOneActiveRun()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var control = new PartitionedProcessingControl();
        var options = new PartitionedProcessingOptions { Control = control };
        var offset = new TopicPartitionOffset(partition.Topic, partition.Partition, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await control.SeekAsync(offset).ConfigureAwait(false));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await control.SeekAsync(offset with { Offset = -1 }).ConfigureAwait(false));

        var started = NewCompletionSource();
        consumer.Enqueue(CreateResult(partition, 0));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, _, _) =>
            {
                started.TrySetResult();
                return ValueTask.CompletedTask;
            },
            options,
            cts.Token).AsTask();
        await started.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        // The rejected run must not leave its monitor attached.
        var monitor = new PartitionedProcessingMonitor();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new TestConsumer().RunPartitionedAsync(
                    (_, _, _) => ValueTask.CompletedTask, new PartitionedProcessingOptions { Control = control, Monitor = monitor }, cts.Token)
                .ConfigureAwait(false));
        monitor.Attach();

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await control.SeekAsync(offset).ConfigureAwait(false));
    }
}
