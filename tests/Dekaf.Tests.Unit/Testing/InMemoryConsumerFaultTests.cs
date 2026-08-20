using System.Text;
using Dekaf.Consumer;
using Dekaf.Serialization;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryConsumerFaultTests
{
    private const string Topic = "orders";
    private const string GroupId = "workers";
    private static readonly TopicPartition Partition = new(Topic, 0);

    [Test]
    public async Task ConsumeOneAsync_FetchFailurePreservesPositionForRetry()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        var failure = new InvalidOperationException("fetch failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, Topic, 0, GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<Exception>(
            () => consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(0);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_ConsumeFailurePreservesPositionAndStoredOffset()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync(enableAutoOffsetStore: true);
        var failure = new InvalidOperationException("delivery failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Consume, Topic, 0, GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<Exception>(
            () => consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());
        await consumer.CommitAsync();

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();

        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        await consumer.CommitAsync();

        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(1);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_ConsumesFetchAndConsumeFaultsInOperationOrder()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        var fetchFailure = new InvalidOperationException("fetch");
        var consumeFailure = new InvalidOperationException("consume");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Fetch), fetchFailure);
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Consume), consumeFailure);

        var first = await Assert.ThrowsAsync<Exception>(
            () => consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());
        var second = await Assert.ThrowsAsync<Exception>(
            () => consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());
        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(first).IsSameReferenceAs(fetchFailure);
        await Assert.That(second).IsSameReferenceAs(consumeFailure);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(0);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_UnrelatedSameGroupResourceCompletesSynchronously()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        cluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(
                KafkaFaultOperation.Fetch,
                topic: "payments",
                partition: 0,
                groupId: GroupId),
            new InvalidOperationException("unrelated"));

        var operation = consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(operation.IsCompletedSuccessfully).IsTrue();
        await Assert.That(operation.Result).IsNotNull();
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_FetchBarrierCancellationPreservesPosition()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, Topic, 0, GroupId));
        using var cancellation = new CancellationTokenSource();

        var operation = consumer
            .ConsumeOneAsync(Timeout.InfiniteTimeSpan, cancellation.Token)
            .AsTask();
        await barrier.WaitUntilEnteredAsync();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);
        await Assert.That(barrier.Release()).IsTrue();

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumeOneAsync_FetchBarrierDisposalDoesNotPublishSelectedRecord()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, Topic, 0, GroupId));

        var operation = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await barrier.WaitUntilEnteredAsync();
        await consumer.DisposeAsync();
        await Assert.That(barrier.Release()).IsTrue();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => operation);
    }

    [Test]
    public async Task CommitAsync_ResourceScopedFailurePreservesStoredOffsetForRetry()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync(enableAutoOffsetStore: false);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<Exception>(() => consumer.CommitAsync().AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();

        await consumer.CommitAsync();

        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task CommitAsync_ExplicitOffsetsConsumeResourceScopedFailure()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        TopicPartitionOffset[] offsets = [new(Topic, 0, 1)];
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync(offsets).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();

        await consumer.CommitAsync(offsets);

        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task CommitAsync_ExplicitOffsetsPreserveFaultScriptOrder()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        TopicPartitionOffset[] offsets =
        [
            new("payments", 0, 1),
            new(Topic, 0, 1)
        ];
        var resourceFailure = new InvalidOperationException("resource first");
        var groupFailure = new InvalidOperationException("group second");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId),
            resourceFailure);
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            groupFailure);

        var first = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync(offsets).AsTask());
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync(offsets).AsTask());

        await Assert.That(first).IsSameReferenceAs(resourceFailure);
        await Assert.That(second).IsSameReferenceAs(groupFailure);
    }

    [Test]
    public async Task CommitAsync_StoredOffsetsPreserveFaultScriptOrder()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic);
        cluster.CreateTopic("payments");
        var consumer = CreateConsumer(cluster, enableAutoOffsetStore: false);
        consumer.Subscribe(["payments", Topic]);
        consumer.StoreOffset(new TopicPartitionOffset("payments", 0, 1));
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var resourceFailure = new InvalidOperationException("resource first");
        var groupFailure = new InvalidOperationException("group second");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId),
            resourceFailure);
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            groupFailure);

        var first = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync().AsTask());
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync().AsTask());

        await Assert.That(first).IsSameReferenceAs(resourceFailure);
        await Assert.That(second).IsSameReferenceAs(groupFailure);
    }

    [Test]
    public async Task CommitAsync_BarrierCancellationPreservesStoredOffsetForRetry()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync(enableAutoOffsetStore: false);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        using var cancellation = new CancellationTokenSource();

        var operation = consumer.CommitAsync(cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();
        await Assert.That(barrier.Release()).IsTrue();

        await consumer.CommitAsync();

        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task AutoCommit_FailurePreservesPositionAndOffsetForRetry()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException("auto commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<Exception>(
            () => consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result).IsNotNull();
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(1);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task AutoCommit_BarrierRunsBeforePositionMutation()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var operation = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await barrier.WaitUntilEnteredAsync();

        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();
        await Assert.That(barrier.Release()).IsTrue();

        var result = await operation;

        await Assert.That(result).IsNotNull();
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(1);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task AutoCommit_PositionChangeBeforeAdvancementPreservesFault()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var deserializer = new BlockingAsyncDeserializer();
        var consumer = CreateAsyncConsumer(cluster, deserializer);
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            failure);

        var operation = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await deserializer.WaitUntilEnteredAsync();
        consumer.Seek(new TopicPartitionOffset(Topic, 0, 1));
        deserializer.Release();

        await Assert.That(await operation).IsNull();
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();

        consumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task Snapshot_GroupChangeBeforeAdvancementPreservesFault()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var deserializer = new BlockingAsyncDeserializer();
        await using var consumer = CreateAsyncConsumer(cluster, deserializer);
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            failure);
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();

        var moveNext = snapshot.MoveNextAsync().AsTask();
        await deserializer.WaitUntilEnteredAsync();
        await using var joiningConsumer = CreateConsumer(cluster);
        joiningConsumer.Subscribe(Topic);
        deserializer.Release();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => moveNext);

        await Assert.That(actual).IsNotSameReferenceAs(failure);
        await Assert.That(actual!.Message).Contains("snapshot enumeration");
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AutoCommit_DoesNotConsumeCommitFaultWithoutStoredOffset()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            failure);

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result).IsNotNull();
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();

        consumer.StoreOffset(result!.Value);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync().AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AutoCommit_AfterProcessingConsumesResourceFaultOnNextPoll()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto,
            offsetStoreTiming: OffsetStoreTiming.AfterProcessing);
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId),
            failure);

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result).IsNotNull();
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();

        var next = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(next).IsNull();
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task CloseAsync_AutoCommitFailureStillDisposesWithoutCommitting()
    {
        var cluster = new InMemoryKafkaCluster();
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CloseAsync().AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsNull();
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());
    }

    [Test]
    public async Task CommitAsync_FaultRunsBeforeFallbackInputEnumeration()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<Exception>(
            () => consumer.CommitAsync(ThrowOnEnumeration()).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task CommitAsync_ReadOnlyListUsesIndexedCommitPath()
    {
        var (_, consumer) = await CreateConsumerWithRecordAsync();
        var offsets = new IndexOnlyOffsets(new TopicPartitionOffset(Topic, 0, 1));

        await consumer.CommitAsync(offsets);

        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task StoreOffset_FailureDoesNotMutateStoredOffsets()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync(enableAutoOffsetStore: false);
        var failure = new InvalidOperationException("store failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.StoreOffset, Topic, 0, GroupId),
            failure);

        var actual = Assert.Throws<InvalidOperationException>(
            () => consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1)));
        await consumer.CommitAsync();

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();

        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        await consumer.CommitAsync();

        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task StoreOffset_UnrelatedRuleRemainsQueued()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync(enableAutoOffsetStore: false);
        cluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(
                KafkaFaultOperation.StoreOffset,
                topic: "payments",
                partition: 0,
                groupId: GroupId),
            new InvalidOperationException("unrelated"));

        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        await consumer.CommitAsync();

        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    [Arguments(KafkaFaultOperation.JoinGroup)]
    [Arguments(KafkaFaultOperation.SyncGroup)]
    [Arguments(KafkaFaultOperation.Rebalance)]
    public async Task Subscribe_GroupTransitionFailurePreservesExistingAssignment(
        KafkaFaultOperation operation)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 1);
        cluster.CreateTopic("payments", partitionCount: 1);
        var consumer = CreateConsumer(cluster);
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException(operation.ToString());
        cluster.FaultPlan.Fail(new KafkaFaultScope(operation, groupId: GroupId), failure);

        var actual = Assert.Throws<InvalidOperationException>(() => consumer.Subscribe("payments"));

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(consumer.Subscription).IsEquivalentTo([Topic]);
        await Assert.That(consumer.Assignment).IsEquivalentTo([Partition]);

        consumer.Subscribe("payments");

        await Assert.That(consumer.Subscription).IsEquivalentTo(["payments"]);
        await Assert.That(consumer.Assignment)
            .IsEquivalentTo([new TopicPartition("payments", 0)]);
    }

    [Test]
    public async Task Subscribe_GroupTransitionFailureDoesNotAutoCreateTopic()
    {
        var cluster = new InMemoryKafkaCluster();
        var consumer = CreateConsumer(cluster);
        var failure = new InvalidOperationException("join failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.JoinGroup, groupId: GroupId),
            failure);

        var actual = Assert.Throws<InvalidOperationException>(() => consumer.Subscribe("missing"));

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    [Test]
    public async Task ManualAssignment_DoesNotConsumeGroupTransitionFaults()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic);
        var consumer = CreateConsumer(cluster);
        var failure = new InvalidOperationException("group transition");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.JoinGroup, groupId: GroupId),
            failure);
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.SyncGroup, groupId: GroupId),
            failure);
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Rebalance),
            failure);

        consumer.Assign(Partition);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 0)]);
        consumer.IncrementalUnassign([Partition]);
        consumer.Unassign();

        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(3);
    }

    [Test]
    public async Task FaultSelectors_DoNotConsumeRuleForDifferentGroup()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        var failure = new InvalidOperationException("other group");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, Topic, 0, "other-workers"),
            failure);

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result).IsNotNull();
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
    }

    private static async Task<(InMemoryKafkaCluster Cluster, InMemoryConsumer<string, string> Consumer)>
        CreateConsumerWithRecordAsync(bool enableAutoOffsetStore = true)
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var consumer = CreateConsumer(cluster, enableAutoOffsetStore);
        consumer.Subscribe(Topic);
        return (cluster, consumer);
    }

    private static InMemoryConsumer<string, string> CreateConsumer(
        InMemoryKafkaCluster cluster,
        bool enableAutoOffsetStore = true,
        OffsetCommitMode offsetCommitMode = OffsetCommitMode.Manual,
        OffsetStoreTiming offsetStoreTiming = OffsetStoreTiming.OnDelivery) =>
        new(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = offsetCommitMode,
                EnableAutoOffsetStore = enableAutoOffsetStore,
                OffsetStoreTiming = offsetStoreTiming
            });

    private static InMemoryConsumer<string, string> CreateAsyncConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<string> deserializer) =>
        new(
            cluster,
            deserializer,
            deserializer,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Auto,
                EnableAutoOffsetStore = true,
                OffsetStoreTiming = OffsetStoreTiming.OnDelivery
            });

    private static IEnumerable<TopicPartitionOffset> ThrowOnEnumeration()
    {
        throw new InvalidOperationException("Offsets were enumerated before the fault ran.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private sealed class IndexOnlyOffsets(TopicPartitionOffset offset) : IReadOnlyList<TopicPartitionOffset>
    {
        public int Count => 1;

        public TopicPartitionOffset this[int index] => index == 0
            ? offset
            : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<TopicPartitionOffset> GetEnumerator() =>
            throw new InvalidOperationException("Indexed collection was enumerated.");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class BlockingAsyncDeserializer : IAsyncDeserializer<string>
    {
        private readonly TaskCompletionSource _entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return Encoding.UTF8.GetString(data.Span);
        }

        public Task WaitUntilEnteredAsync() => _entered.Task;

        public void Release() => _release.TrySetResult();
    }
}
