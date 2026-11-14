using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Testing;
using TUnit.Assertions.Enums;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryRebalanceListenerTests
{
    private static readonly TopicPartition Partition0 = new("topic", 0);
    private static readonly TopicPartition Partition1 = new("topic", 1);

    [Test]
    public async Task Subscribe_ReportsAssignedPartitionsOnNextConsume()
    {
        var cluster = CreateCluster(partitions: 2);
        var listener = new RecordingListener();
        await using var consumer = CreateConsumer(cluster, "a", listener);

        consumer.Subscribe("topic");
        await Assert.That(listener.Events).IsEmpty();

        await consumer.ConsumeOneAsync(TimeSpan.Zero);
        await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0,topic-1"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task MemberJoiningAndLeaving_RevokesAndReassignsPartitions()
    {
        var cluster = CreateCluster(partitions: 2);
        var firstListener = new RecordingListener();
        var secondListener = new RecordingListener();
        await using var first = CreateConsumer(cluster, "a", firstListener);
        await using var second = CreateConsumer(cluster, "b", secondListener);
        first.Subscribe("topic");
        await first.ConsumeOneAsync(TimeSpan.Zero);

        second.Subscribe("topic");
        await first.ConsumeOneAsync(TimeSpan.Zero);
        await second.ConsumeOneAsync(TimeSpan.Zero);

        var moved = second.Assignment.Single();
        await Assert.That(first.Assignment).DoesNotContain(moved);
        await Assert.That(firstListener.Events).IsEquivalentTo(["assigned topic-0,topic-1", $"revoked {Format(moved)}"], CollectionOrdering.Matching);
        await Assert.That(secondListener.Events).IsEquivalentTo([$"assigned {Format(moved)}"], CollectionOrdering.Matching);

        second.Unsubscribe();
        await first.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(secondListener.Events).IsEquivalentTo([$"assigned {Format(moved)}", $"revoked {Format(moved)}"], CollectionOrdering.Matching);
        await Assert.That(firstListener.Events).IsEquivalentTo(["assigned topic-0,topic-1", $"revoked {Format(moved)}", $"assigned {Format(moved)}"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task BlockedConsume_ReportsRevocationWhenAnotherMemberJoins()
    {
        var cluster = CreateCluster(partitions: 2);
        var firstListener = new RecordingListener();
        await using var first = CreateConsumer(cluster, "a", firstListener);
        await using var second = CreateConsumer(cluster, "b", new RecordingListener());
        first.Subscribe("topic");
        await first.ConsumeOneAsync(TimeSpan.Zero);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var blocked = first.ConsumeOneAsync(Timeout.InfiniteTimeSpan, cancellation.Token).AsTask();
        second.Subscribe("topic");

        await firstListener.Revoked.Task.WaitAsync(cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.That(async () => await blocked).Throws<OperationCanceledException>();
        await Assert.That(firstListener.Events).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Unsubscribe_RevokesBeforeLeavingSoTheListenerCanCommit()
    {
        var cluster = CreateCluster(partitions: 1);
        await ProduceAsync(cluster, 2);
        var listener = new RecordingListener();
        await using var consumer = CreateConsumer(cluster, "a", listener, OffsetCommitMode.Manual);
        listener.OnRevoked = () => consumer.CommitAsync();
        consumer.Subscribe("topic");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(0);

        consumer.Unsubscribe();

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0", "revoked topic-0"], CollectionOrdering.Matching);
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsEqualTo(1);
    }

    [Test]
    public async Task Close_RevokesBeforeLeavingSoTheListenerCanCommit()
    {
        var cluster = CreateCluster(partitions: 1);
        await ProduceAsync(cluster, 2);
        var listener = new RecordingListener();
        var consumer = CreateConsumer(cluster, "a", listener, OffsetCommitMode.Manual);
        listener.OnRevoked = () => consumer.CommitAsync();
        consumer.Subscribe("topic");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(0);

        await consumer.CloseAsync();
        await consumer.DisposeAsync();

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0", "revoked topic-0"], CollectionOrdering.Matching);
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsEqualTo(1);
    }

    [Test]
    public async Task Unsubscribe_AsynchronousRevocationStillCommitsDepartingOffsets()
    {
        var cluster = CreateCluster(partitions: 1);
        await ProduceAsync(cluster, 2);
        var listener = new RecordingListener();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var consumer = CreateConsumer(cluster, "a", listener, OffsetCommitMode.Manual);
        listener.OnRevoked = async () =>
        {
            await release.Task;
            await consumer.CommitAsync();
        };
        consumer.Subscribe("topic");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(0);

        consumer.Unsubscribe();
        release.SetResult();
        await consumer.CloseAsync();

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0", "revoked topic-0"], CollectionOrdering.Matching);
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsEqualTo(1);
    }

    [Test]
    public async Task Unsubscribe_CancelledRevocationDropsDepartingOffsets()
    {
        var cluster = CreateCluster(partitions: 1);
        await ProduceAsync(cluster, 2);
        var listener = new RecordingListener { OnRevoked = static () => throw new OperationCanceledException() };
        await using var consumer = CreateConsumer(cluster, "a", listener, OffsetCommitMode.Manual);
        consumer.Subscribe("topic");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(0);

        consumer.Unsubscribe();
        await consumer.CommitAsync();

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0"], CollectionOrdering.Matching);
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsNull();
    }

    [Test]
    public async Task MemberJoining_ListenerCommitIncludesRevokedPartitionOffsets()
    {
        var cluster = CreateCluster(partitions: 2);
        await ProduceAsync(cluster, 1, partition: 0);
        await ProduceAsync(cluster, 1, partition: 1);
        var listener = new RecordingListener();
        await using var first = CreateConsumer(cluster, "a", listener, OffsetCommitMode.Manual);
        listener.OnRevoked = () => first.CommitAsync();
        first.Subscribe("topic");
        await Assert.That(await first.ConsumeOneAsync(TimeSpan.Zero)).IsNotNull();
        await Assert.That(await first.ConsumeOneAsync(TimeSpan.Zero)).IsNotNull();

        await using var second = CreateConsumer(cluster, "b", new RecordingListener());
        second.Subscribe("topic");
        await first.ConsumeOneAsync(TimeSpan.Zero);

        var moved = second.Assignment.Single();
        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0,topic-1", $"revoked {Format(moved)}"], CollectionOrdering.Matching);
        await Assert.That(cluster.GetCommittedOffset("group", moved)).IsEqualTo(1);
    }

    [Test]
    public async Task SwitchingToManualAssignment_ListenerCommitIncludesRevokedPartitionOffsets()
    {
        var cluster = CreateCluster(partitions: 2);
        await ProduceAsync(cluster, 2);
        var listener = new RecordingListener();
        await using var consumer = CreateConsumer(cluster, "a", listener, OffsetCommitMode.Manual);
        listener.OnRevoked = () => consumer.CommitAsync();
        consumer.Subscribe("topic");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(0);

        consumer.Assign(Partition1);
        await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0,topic-1", "revoked topic-0,topic-1"], CollectionOrdering.Matching);
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsEqualTo(1);
    }

    [Test]
    public async Task ManuallyReassigningAnOwnedPartition_ListenerCommitIncludesItsOffset()
    {
        var cluster = CreateCluster(partitions: 1);
        await ProduceAsync(cluster, 2);
        var listener = new RecordingListener();
        await using var consumer = CreateConsumer(cluster, "a", listener, OffsetCommitMode.Manual);
        listener.OnRevoked = () => consumer.CommitAsync();
        consumer.Subscribe("topic");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(0);

        consumer.Assign(Partition0);
        await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0", "revoked topic-0"], CollectionOrdering.Matching);
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsEqualTo(1);
    }

    [Test]
    public async Task UnsubscribeDuringRevocation_KeepsTheOffsetsItSaved()
    {
        var cluster = CreateCluster(partitions: 2);
        await ProduceAsync(cluster, 1, partition: 0);
        await ProduceAsync(cluster, 1, partition: 1);
        var listener = new RecordingListener();
        var joinRevocationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseJoinRevocation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUnsubscribeRevocation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var first = CreateConsumer(cluster, "a", listener, OffsetCommitMode.Manual);
        var revocations = 0;
        listener.OnRevoked = async () =>
        {
            if (Interlocked.Increment(ref revocations) == 1)
            {
                joinRevocationEntered.SetResult();
                await releaseJoinRevocation.Task;
                return;
            }

            await releaseUnsubscribeRevocation.Task;
            await first.CommitAsync();
        };
        first.Subscribe("topic");
        await Assert.That(await first.ConsumeOneAsync(TimeSpan.Zero)).IsNotNull();
        await Assert.That(await first.ConsumeOneAsync(TimeSpan.Zero)).IsNotNull();

        await using var second = CreateConsumer(cluster, "b", new RecordingListener());
        second.Subscribe("topic");
        var consume = first.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await joinRevocationEntered.Task;
        var kept = first.Assignment.Single(partition => !second.Assignment.Contains(partition));

        first.Unsubscribe();
        releaseJoinRevocation.SetResult();
        await consume;
        releaseUnsubscribeRevocation.SetResult();
        await first.CloseAsync();

        await Assert.That(cluster.GetCommittedOffset("group", kept)).IsEqualTo(1);
    }

    [Test]
    public async Task PendingUnsubscribeRevocation_ConsumeWaitIsCancellable()
    {
        var cluster = CreateCluster(partitions: 1);
        var listener = new RecordingListener();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var consumer = CreateConsumer(cluster, "a", listener);
        listener.OnRevoked = async () => await release.Task;
        consumer.Subscribe("topic");
        await consumer.ConsumeOneAsync(TimeSpan.Zero);
        consumer.Unsubscribe();

        using var cancellation = new CancellationTokenSource();
        var consume = consumer.ConsumeOneAsync(TimeSpan.Zero, cancellation.Token).AsTask();
        await Assert.That(consume.IsCompleted).IsFalse();
        cancellation.Cancel();

        await Assert.That(async () => await consume).Throws<OperationCanceledException>();
        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0"], CollectionOrdering.Matching);

        release.SetResult();
        await consumer.CloseAsync();
        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0", "revoked topic-0"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ListenerException_DoesNotStopConsumption()
    {
        var cluster = CreateCluster(partitions: 1);
        await ProduceAsync(cluster, 1);
        var listener = new RecordingListener { ThrowOnAssigned = true };
        await using var consumer = CreateConsumer(cluster, "a", listener);
        consumer.Subscribe("topic");

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result!.Value.Offset).IsEqualTo(0);
        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task AdministrativeRemoval_ReportsPartitionsLost()
    {
        var cluster = CreateCluster(partitions: 2);
        var listener = new RecordingListener();
        await using var consumer = CreateConsumer(cluster, "a", listener);
        consumer.Subscribe("topic");
        await consumer.ConsumeOneAsync(TimeSpan.Zero);

        cluster.RemoveConsumerGroupMembers("group", [new ConsumerGroupMemberIdentity { MemberId = "a" }]);
        await consumer.ConsumeOneAsync(TimeSpan.Zero);
        await consumer.CloseAsync();

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0,topic-1", "lost topic-0,topic-1"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task AdministrativeRemovalBeforeClose_ReportsPartitionsLostWithoutCommitting()
    {
        var cluster = CreateCluster(partitions: 1);
        await ProduceAsync(cluster, 2);
        var listener = new RecordingListener();
        var consumer = CreateConsumer(cluster, "a", listener, OffsetCommitMode.Manual);
        listener.OnRevoked = () => consumer.CommitAsync();
        consumer.Subscribe("topic");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(0);

        cluster.RemoveConsumerGroupMembers("group", [new ConsumerGroupMemberIdentity { MemberId = "a" }]);
        await consumer.CloseAsync();
        await consumer.DisposeAsync();

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0", "lost topic-0"], CollectionOrdering.Matching);
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsNull();
    }

    [Test]
    public async Task CancelledAssignmentCallback_IsReportedAgainOnNextConsume()
    {
        var cluster = CreateCluster(partitions: 1);
        var listener = new RecordingListener();
        var assignmentEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAssignment = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var consumer = CreateConsumer(cluster, "a", listener);
        var assignments = 0;
        listener.OnAssigned = async () =>
        {
            if (Interlocked.Increment(ref assignments) != 1)
                return;

            assignmentEntered.SetResult();
            await releaseAssignment.Task;
            throw new OperationCanceledException();
        };
        consumer.Subscribe("topic");
        using var cancellation = new CancellationTokenSource();
        var consume = consumer.ConsumeOneAsync(TimeSpan.Zero, cancellation.Token).AsTask();
        await assignmentEntered.Task;
        await cancellation.CancelAsync();
        releaseAssignment.SetResult();
        await Assert.That(async () => await consume).Throws<OperationCanceledException>();

        await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0", "assigned topic-0"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task IncrementalAssignOfAnOwnedPartition_ListenerCommitIncludesItsOffset()
    {
        var cluster = CreateCluster(partitions: 1);
        await ProduceAsync(cluster, 2);
        var listener = new RecordingListener();
        await using var consumer = CreateConsumer(cluster, "a", listener, OffsetCommitMode.Manual);
        listener.OnRevoked = () => consumer.CommitAsync();
        consumer.Subscribe("topic");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(0);

        consumer.IncrementalAssign([new TopicPartitionOffset("topic", 0, 0)]);
        await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0", "revoked topic-0"], CollectionOrdering.Matching);
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsEqualTo(1);
    }

    [Test]
    public async Task CancelledRevocation_IsRetriedOnNextConsumeWithItsOffsets()
    {
        var cluster = CreateCluster(partitions: 1);
        cluster.CreateTopic("other", 1);
        await ProduceAsync(cluster, 2);
        var listener = new RecordingListener();
        var revocationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRevocation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var consumer = CreateConsumer(cluster, "a", listener, OffsetCommitMode.Manual);
        var revocations = 0;
        listener.OnRevoked = async () =>
        {
            if (Interlocked.Increment(ref revocations) != 1)
            {
                await consumer.CommitAsync();
                return;
            }

            revocationEntered.SetResult();
            await releaseRevocation.Task;
            throw new OperationCanceledException();
        };
        consumer.Subscribe("topic");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(0);

        consumer.Subscribe("other");
        using var cancellation = new CancellationTokenSource();
        var consume = consumer.ConsumeOneAsync(TimeSpan.Zero, cancellation.Token).AsTask();
        await revocationEntered.Task;
        await cancellation.CancelAsync();
        releaseRevocation.SetResult();
        await Assert.That(async () => await consume).Throws<OperationCanceledException>();

        await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0", "revoked topic-0", "assigned other-0"], CollectionOrdering.Matching);
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsEqualTo(1);
    }

    [Test]
    public async Task ListenerOwnCancellation_PropagatesAndIsReportedAgain()
    {
        var cluster = CreateCluster(partitions: 1);
        var listener = new RecordingListener();
        await using var consumer = CreateConsumer(cluster, "a", listener);
        var assignments = 0;
        listener.OnAssigned = () => Interlocked.Increment(ref assignments) == 1
            ? throw new OperationCanceledException()
            : ValueTask.CompletedTask;
        consumer.Subscribe("topic");

        await Assert.That(async () => await consumer.ConsumeOneAsync(TimeSpan.Zero)).Throws<OperationCanceledException>();
        await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0", "assigned topic-0"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ManualAssignment_DoesNotInvokeTheListener()
    {
        var cluster = CreateCluster(partitions: 2);
        var listener = new RecordingListener();
        await using var consumer = CreateConsumer(cluster, "a", listener);

        consumer.Assign(Partition0, Partition1);
        await consumer.ConsumeOneAsync(TimeSpan.Zero);
        consumer.Unassign();
        await consumer.CloseAsync();

        await Assert.That(listener.Events).IsEmpty();
    }

    [Test]
    public async Task SwitchingToManualAssignment_RevokesSubscribedPartitions()
    {
        var cluster = CreateCluster(partitions: 2);
        var listener = new RecordingListener();
        await using var consumer = CreateConsumer(cluster, "a", listener);
        consumer.Subscribe("topic");
        await consumer.ConsumeOneAsync(TimeSpan.Zero);

        consumer.Assign(Partition0);
        await consumer.ConsumeOneAsync(TimeSpan.Zero);
        await consumer.CloseAsync();

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0,topic-1", "revoked topic-0,topic-1"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task EmptyIncrementalAssign_RevokesSubscribedPartitions()
    {
        var cluster = CreateCluster(partitions: 1);
        var listener = new RecordingListener();
        await using var consumer = CreateConsumer(cluster, "a", listener);
        consumer.Subscribe("topic");
        await consumer.ConsumeOneAsync(TimeSpan.Zero);

        consumer.IncrementalAssign([]);
        await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(listener.Events).IsEquivalentTo(["assigned topic-0", "revoked topic-0"], CollectionOrdering.Matching);
    }

    private static InMemoryKafkaCluster CreateCluster(int partitions)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", partitions);
        return cluster;
    }

    private static InMemoryConsumer<string, string> CreateConsumer(
        InMemoryKafkaCluster cluster,
        string memberId,
        IRebalanceListener listener,
        OffsetCommitMode mode = OffsetCommitMode.Auto) =>
        new(cluster, new InMemoryConsumerOptions
        {
            GroupId = "group",
            MemberId = memberId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            OffsetCommitMode = mode,
            RebalanceListener = listener
        });

    private static async Task ProduceAsync(InMemoryKafkaCluster cluster, int count, int partition = 0)
    {
        await using var producer = new InMemoryProducer<string, string>(cluster);
        for (var i = 0; i < count; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "topic",
                Partition = partition,
                Key = "key",
                Value = $"value-{i}"
            });
        }
    }

    private static string Format(IEnumerable<TopicPartition> partitions) =>
        string.Join(',', partitions
            .OrderBy(static partition => partition.Topic, StringComparer.Ordinal)
            .ThenBy(static partition => partition.Partition)
            .Select(Format));

    private static string Format(TopicPartition partition) => $"{partition.Topic}-{partition.Partition}";

    private sealed class RecordingListener : IRebalanceListener
    {
        private readonly Lock _gate = new();
        private readonly List<string> _events = [];

        public Func<ValueTask>? OnRevoked { get; set; }

        public Func<ValueTask>? OnAssigned { get; set; }

        public bool ThrowOnAssigned { get; init; }

        public TaskCompletionSource Revoked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<string> Events
        {
            get
            {
                lock (_gate)
                    return _events.ToArray();
            }
        }

        public async ValueTask OnPartitionsAssignedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Record("assigned", partitions);
            if (ThrowOnAssigned)
                throw new InvalidOperationException("listener failure");
            if (OnAssigned is { } onAssigned)
                await onAssigned();
        }

        public async ValueTask OnPartitionsRevokedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            if (OnRevoked is { } onRevoked)
                await onRevoked();
            Record("revoked", partitions);
            Revoked.TrySetResult();
        }

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Record("lost", partitions);
            return ValueTask.CompletedTask;
        }

        private void Record(string kind, IEnumerable<TopicPartition> partitions)
        {
            lock (_gate)
                _events.Add($"{kind} {Format(partitions)}");
        }
    }
}
