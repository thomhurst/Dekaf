using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class PartitionedConsumerRuntimeTests
{
    [Test]
    public async Task CommitObserver_PeriodicCommit_ObservesBeforeAndAfterRequest()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var observer = new RecordingCommitObserver(consumer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            static (_, _, _) => ValueTask.CompletedTask,
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedPeriodically,
                CommitInterval = TimeSpan.FromMilliseconds(20),
                CommitObserver = observer
            },
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0));
        var completed = await observer.Completed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        var started = observer.Started.First();
        await Assert.That(started.Reason).IsEqualTo(OffsetCommitReason.Periodic);
        await Assert.That(started.Offsets).IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 1) });
        // The start callback ran before the consumer received the request.
        await Assert.That(started.AttemptsAtStart).IsEqualTo(0);
        await Assert.That(completed.Reason).IsEqualTo(OffsetCommitReason.Periodic);
        await Assert.That(completed.State).IsSameReferenceAs(started.State);
        await Assert.That(completed.Exception).IsNull();
    }

    [Test]
    public async Task CommitObserver_FailedCommit_ReportsException()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var failure = new GroupException(ErrorCode.GroupAuthorizationFailed, "denied");
        consumer.CommitFailures.Enqueue(failure);
        var observer = new RecordingCommitObserver(consumer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            static (_, _, _) => ValueTask.CompletedTask,
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedPeriodically,
                CommitInterval = TimeSpan.FromMilliseconds(20),
                CommitObserver = observer
            },
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0));

        await Assert.ThrowsAsync<GroupException>(async () => await runTask.WaitAsync(cts.Token).ConfigureAwait(false));
        var completed = await observer.Completed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await Assert.That(completed.Exception).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task CommitObserver_RevokeCommit_ReportsRevokeReason()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var observer = new RecordingCommitObserver(consumer);
        var processed = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, _, _) =>
            {
                processed.TrySetResult();
                return ValueTask.CompletedTask;
            },
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
                CommitObserver = observer
            },
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0));
        await processed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await consumer.RevokeFromCoordinatorAsync(partition).ConfigureAwait(false);

        var completed = await observer.Completed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(completed.Reason).IsEqualTo(OffsetCommitReason.Revoke);
        await Assert.That(completed.Offsets).IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 1) });
        await Assert.That(completed.Exception).IsNull();
    }

    [Test]
    public async Task CommitObserver_AssignmentRemovedPartition_ReportsRevokeReason()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var observer = new RecordingCommitObserver(consumer);
        var processed = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, _, _) =>
            {
                processed.TrySetResult();
                return ValueTask.CompletedTask;
            },
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
                CommitObserver = observer
            },
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0));
        await processed.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        // No revoke callback: the runtime finds the removed partition when it syncs the assignment.
        consumer.SetAssignment();

        var completed = await observer.Completed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(completed.Reason).IsEqualTo(OffsetCommitReason.Revoke);
        await Assert.That(completed.Offsets).IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 1) });
    }

    [Test]
    public async Task CommitObserver_ShutdownCommit_ReportsStopReason()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var observer = new RecordingCommitObserver(consumer);
        var processed = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            (_, _, _) =>
            {
                processed.TrySetResult();
                return ValueTask.CompletedTask;
            },
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
                CommitObserver = observer
            },
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0));
        await processed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        var completed = await observer.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        await Assert.That(completed.Reason).IsEqualTo(OffsetCommitReason.Stop);
        await Assert.That(completed.Offsets).IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 1) });
    }

    [Test]
    public async Task CommitObserver_SeekCommit_ReportsSeekReason()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer { RedeliverUnenumeratedBatches = true };
        consumer.SetAssignment(partition);
        consumer.Enqueue(Enumerable.Range(0, 5).Select(offset => CreateResult(partition, offset)).ToArray());
        var observer = new RecordingCommitObserver(consumer);

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
            new PartitionedProcessingOptions
            {
                Control = control,
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
                CommitObserver = observer
            },
            cts.Token).AsTask();

        await firstPass.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await control.SeekAsync(new TopicPartitionOffset(partition.Topic, partition.Partition, 2), cts.Token)
            .ConfigureAwait(false);
        var completed = await observer.Completed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(completed.Reason).IsEqualTo(OffsetCommitReason.Seek);
        await Assert.That(completed.Offsets).IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 2) });
        // The seek target is not the completed frontier, so no record count is tracked for it.
        await Assert.That(completed.CompletedRecordCount).IsEqualTo(0L);
    }

    [Test]
    public async Task CommitObserver_PartitionOrderedRecords_CountsRecordsNotOffsets()
    {
        var observer = await RunUntilStopCommitAsync(
            (consumer, options, lastSeen, token) => consumer.RunPartitionedAsync(
                (_, message, _) => SignalLast(lastSeen, message), options, token)).ConfigureAwait(false);

        await AssertGappedStopCommitAsync(observer).ConfigureAwait(false);
    }

    [Test]
    public async Task CommitObserver_PartitionOrderedBatches_CountsRecordsNotOffsets()
    {
        var observer = await RunUntilStopCommitAsync(
            (consumer, options, lastSeen, token) => consumer.RunPartitionedBatchesAsync(
                (_, messages, _) => SignalLast(lastSeen, messages[messages.Count - 1]),
                options,
                token),
            maxHandlerBatchSize: 2).ConfigureAwait(false);

        await AssertGappedStopCommitAsync(observer).ConfigureAwait(false);
    }

    [Test]
    public async Task CommitObserver_KeyOrderedRecords_CountsRecordsNotOffsets()
    {
        var observer = await RunUntilStopCommitAsync(
            (consumer, options, lastSeen, token) => consumer.RunPartitionedAsync(
                (_, message, _) => SignalLast(lastSeen, message),
                options,
                token),
            PartitionedProcessingOrder.Key,
            maxConcurrentHandlers: 2).ConfigureAwait(false);

        await AssertGappedStopCommitAsync(observer).ConfigureAwait(false);
    }

    [Test]
    public async Task CommitObserver_ManualOutOfOrderCompletion_CountsRecordsNotOffsets()
    {
        var observer = await RunUntilStopCommitAsync(
            (consumer, options, lastSeen, token) => consumer.RunPartitionedAsync(
                async (context, cancellationToken) =>
                {
                    var received = new List<ConsumeResult<string, string>>();
                    await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                    {
                        received.Add(message);
                        if (received.Count < GappedOffsets.Length)
                            continue;

                        // Complete the last record first, then the first, so ranges merge in the tree.
                        context.MarkProcessed(received[2]);
                        context.MarkProcessed(received[0]);
                        context.MarkProcessed(received[1]);
                        lastSeen.TrySetResult();
                    }
                },
                options,
                token)).ConfigureAwait(false);

        await AssertGappedStopCommitAsync(observer).ConfigureAwait(false);
    }

    [Test]
    public async Task CommitObserver_PeriodicCommits_CountOnlyRecordsSincePreviousCommit()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var observer = new RecordingCommitObserver(consumer);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            static (_, _, _) => ValueTask.CompletedTask,
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedPeriodically,
                CommitInterval = TimeSpan.FromMilliseconds(20),
                CommitObserver = observer
            },
            cts.Token).AsTask();

        consumer.Enqueue(GappedOffsets.Select(offset => CreateResult(partition, offset)).ToArray());
        await Assert.That(() => observer.Completions.Any(static c => c.Offsets[0].Offset == 10))
            .Eventually(found => found.IsTrue(), TimeSpan.FromSeconds(5));

        consumer.Enqueue(CreateResult(partition, 20), CreateResult(partition, 25));
        await Assert.That(() => observer.Completions.Any(static c => c.Offsets[0].Offset == 26))
            .Eventually(found => found.IsTrue(), TimeSpan.FromSeconds(5));
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        var completions = observer.Completions.ToArray();
        var firstIndex = Array.FindIndex(completions, static c => c.Offsets[0].Offset == 10);
        await Assert.That(completions.Take(firstIndex + 1).Sum(static c => c.CompletedRecordCount)).IsEqualTo(3L);
        await Assert.That(completions.Skip(firstIndex + 1).Sum(static c => c.CompletedRecordCount)).IsEqualTo(2L);
    }

    // Compacted offsets: three records spanning offsets 0 to 9.
    private static readonly long[] GappedOffsets = [0, 4, 9];

    private static ValueTask SignalLast(TaskCompletionSource lastSeen, ConsumeResult<string, string> message)
    {
        if (message.Offset == GappedOffsets[^1])
            lastSeen.TrySetResult();
        return ValueTask.CompletedTask;
    }

    // Stops after the last record reaches its handler; the default drain stop lets it finish
    // before the stop commit, so that commit covers all three records.
    private static async Task<RecordingCommitObserver> RunUntilStopCommitAsync(
        Func<TestConsumer, PartitionedProcessingOptions, TaskCompletionSource, CancellationToken, ValueTask> run,
        PartitionedProcessingOrder ordering = PartitionedProcessingOrder.Partition,
        int maxConcurrentHandlers = 1,
        int maxHandlerBatchSize = 1)
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var observer = new RecordingCommitObserver(consumer);
        var options = new PartitionedProcessingOptions
        {
            CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
            CommitObserver = observer,
            Ordering = ordering,
            MaxConcurrentHandlersPerPartition = maxConcurrentHandlers,
            MaxHandlerBatchSize = maxHandlerBatchSize
        };

        var lastSeen = NewCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = run(consumer, options, lastSeen, cts.Token).AsTask();
        consumer.Enqueue(GappedOffsets.Select(offset => CreateResult(partition, offset, key: $"key-{offset}")).ToArray());
        await lastSeen.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
        await observer.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        return observer;
    }

    private static async Task AssertGappedStopCommitAsync(RecordingCommitObserver observer)
    {
        var started = observer.Started.Single();
        var completed = await observer.Completed.Task.ConfigureAwait(false);
        await Assert.That(completed.Reason).IsEqualTo(OffsetCommitReason.Stop);
        await Assert.That(completed.Offsets).IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 10) });
        await Assert.That(started.CompletedRecordCount).IsEqualTo(3L);
        await Assert.That(completed.CompletedRecordCount).IsEqualTo(3L);
    }

    [Test]
    public async Task CommitObserver_ThrowingObserver_DoesNotChangeCommit()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var observer = new ThrowingCommitObserver();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            static (_, _, _) => ValueTask.CompletedTask,
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedPeriodically,
                CommitInterval = TimeSpan.FromMilliseconds(20),
                CommitObserver = observer
            },
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0));
        await Assert.That(() => consumer.CommitCalls.SelectMany(static offsets => offsets).ToArray())
            .Eventually(offsets => offsets.IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 1) }), TimeSpan.FromSeconds(5));
        await Assert.That(() => Volatile.Read(ref observer.CompletedCalls)).Eventually(calls => calls.IsEqualTo(1), TimeSpan.FromSeconds(5));
        await Assert.That(runTask.IsCompleted).IsFalse();

        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);
    }

    [Test]
    public async Task CommitObserver_HandlerRequestedCommit_IsNotObserved()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var observer = new RecordingCommitObserver(consumer);
        var committed = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    context.MarkProcessed(message);
                    await context.CommitProcessedAsync(cancellationToken).ConfigureAwait(false);
                    committed.TrySetResult();
                }
            },
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.UserManaged,
                CommitObserver = observer
            },
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0));
        await committed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(consumer.CommitAttempts).IsEqualTo(1);
        await Assert.That(observer.Started).IsEmpty();
    }

    [Test]
    public async Task CommitObserver_CannotChangeCommittedOffsets()
    {
        var partition = new TopicPartition("topic-a", 0);
        var consumer = new TestConsumer();
        consumer.SetAssignment(partition);
        var observer = new MutatingCommitObserver();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var runTask = consumer.RunPartitionedAsync(
            static (_, _, _) => ValueTask.CompletedTask,
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedPeriodically,
                CommitInterval = TimeSpan.FromMilliseconds(20),
                CommitObserver = observer
            },
            cts.Token).AsTask();

        consumer.Enqueue(CreateResult(partition, 0));
        await observer.Completed.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await StopRuntimeAsync(cts, runTask).ConfigureAwait(false);

        await Assert.That(observer.WriteFailure).IsTypeOf<NotSupportedException>();
        await Assert.That(consumer.CommitCalls.First()).IsEquivalentTo(new[] { new TopicPartitionOffset("topic-a", 0, 1) });
    }

    private sealed record ObservedCommitStart(
        OffsetCommitReason Reason,
        TopicPartitionOffset[] Offsets,
        object State,
        int AttemptsAtStart,
        long CompletedRecordCount);

    private sealed record ObservedCommitCompletion(
        OffsetCommitReason Reason,
        TopicPartitionOffset[] Offsets,
        object? State,
        Exception? Exception,
        long CompletedRecordCount);

    private sealed class RecordingCommitObserver(TestConsumer consumer) : IOffsetCommitObserver
    {
        public ConcurrentQueue<ObservedCommitStart> Started { get; } = new();

        public TaskCompletionSource<ObservedCommitCompletion> Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public object? OnCommitStarting(OffsetCommitObservation observation)
        {
            var state = new object();
            Started.Enqueue(new ObservedCommitStart(
                observation.Reason,
                observation.Offsets.ToArray(),
                state,
                consumer.CommitAttempts,
                observation.CompletedRecordCount));
            return state;
        }

        public ConcurrentQueue<ObservedCommitCompletion> Completions { get; } = new();

        public void OnCommitCompleted(OffsetCommitObservation observation, object? state, Exception? exception)
        {
            var completion = new ObservedCommitCompletion(
                observation.Reason,
                observation.Offsets.ToArray(),
                state,
                exception,
                observation.CompletedRecordCount);
            Completions.Enqueue(completion);
            Completed.TrySetResult(completion);
        }
    }

    private sealed class MutatingCommitObserver : IOffsetCommitObserver
    {
        public Exception? WriteFailure;

        public TaskCompletionSource Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public object? OnCommitStarting(OffsetCommitObservation observation)
        {
            try
            {
                ((IList<TopicPartitionOffset>)observation.Offsets)[0] = new TopicPartitionOffset("topic-a", 0, 999);
            }
            catch (Exception exception)
            {
                WriteFailure = exception;
            }

            return null;
        }

        public void OnCommitCompleted(OffsetCommitObservation observation, object? state, Exception? exception)
            => Completed.TrySetResult();
    }

    private sealed class ThrowingCommitObserver : IOffsetCommitObserver
    {
        public int CompletedCalls;

        public object? OnCommitStarting(OffsetCommitObservation observation)
            => throw new InvalidOperationException("start");

        public void OnCommitCompleted(OffsetCommitObservation observation, object? state, Exception? exception)
        {
            Interlocked.Increment(ref CompletedCalls);
            throw new InvalidOperationException("complete");
        }
    }
}
