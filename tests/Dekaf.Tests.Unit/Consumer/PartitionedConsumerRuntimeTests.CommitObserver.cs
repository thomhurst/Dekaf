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

    private sealed record ObservedCommitStart(
        OffsetCommitReason Reason,
        TopicPartitionOffset[] Offsets,
        object State,
        int AttemptsAtStart);

    private sealed record ObservedCommitCompletion(
        OffsetCommitReason Reason,
        TopicPartitionOffset[] Offsets,
        object? State,
        Exception? Exception);

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
                consumer.CommitAttempts));
            return state;
        }

        public void OnCommitCompleted(OffsetCommitObservation observation, object? state, Exception? exception)
            => Completed.TrySetResult(new ObservedCommitCompletion(
                observation.Reason,
                observation.Offsets.ToArray(),
                state,
                exception));
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
