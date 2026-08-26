using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
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
    public async Task ConsumeOneAsync_GroupMemberIgnoresFaultForForeignPartition()
    {
        var innerPlan = new KafkaFaultPlan();
        var faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        cluster.CreateTopic(Topic, partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 0,
            Key = "key",
            Value = "zero"
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 1,
            Key = "key",
            Value = "one"
        });
        await using var first = CreateConsumer(cluster);
        await using var second = CreateConsumer(cluster);
        first.Subscribe(Topic);
        second.Subscribe(Topic);
        var foreignPartition = second.Assignment.Single();
        innerPlan.FailPersistently(
            new KafkaFaultScope(
                KafkaFaultOperation.Fetch,
                foreignPartition.Topic,
                foreignPartition.Partition,
                GroupId),
            new InvalidOperationException("foreign partition"));

        var result = await first.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Partition).IsNotEqualTo(foreignPartition.Partition);
        await Assert.That(faultPlan.MatchingProbeCount).IsEqualTo(0);
        await Assert.That(innerPlan.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeOneAsync_CustomPlanCachesUnrelatedPersistentFault()
    {
        var innerPlan = new KafkaFaultPlan();
        var faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "first");
        await producer.ProduceAsync(Topic, "key", "second");
        await using var consumer = CreateConsumer(cluster);
        consumer.Subscribe(Topic);
        innerPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Produce, Topic, 0),
            new InvalidOperationException("producer only"));

        var first = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        var probeCount = faultPlan.PotentialProbeCount;
        var second = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNotNull();
        await Assert.That(probeCount).IsEqualTo(2);
        await Assert.That(faultPlan.PotentialProbeCount).IsEqualTo(probeCount);
    }

    [Test]
    public async Task ConsumeOneAsync_PausedPartitionFaultDoesNotDivertActivePartition()
    {
        var innerPlan = new KafkaFaultPlan();
        var faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        cluster.CreateTopic(Topic, partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 0,
            Key = "key",
            Value = "paused"
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 1,
            Key = "key",
            Value = "active"
        });
        await using var consumer = CreateConsumer(cluster);
        var pausedPartition = new TopicPartition(Topic, 0);
        consumer.Assign(pausedPartition, new TopicPartition(Topic, 1));
        consumer.Pause(pausedPartition);
        var failure = new InvalidOperationException("paused partition");
        innerPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Consume, Topic, 0, GroupId),
            failure);

        var operation = consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(operation.IsCompletedSuccessfully).IsTrue();
        await Assert.That(operation.Result).IsNotNull();
        await Assert.That(operation.Result!.Value.Partition).IsEqualTo(1);
        await Assert.That(faultPlan.MatchingProbeCount).IsEqualTo(0);

        consumer.Resume(pausedPartition);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task ConsumeOneAsync_GroupOwnershipChangeInvalidatesFaultCache()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 0,
            Key = "key",
            Value = "zero"
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 1,
            Key = "key",
            Value = "one"
        });
        await using var first = CreateConsumer(cluster);
        var second = CreateConsumer(cluster);
        first.Subscribe(Topic);
        second.Subscribe(Topic);
        var foreignPartition = second.Assignment.Single();
        var failure = new InvalidOperationException("newly owned partition");
        cluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(
                KafkaFaultOperation.Fetch,
                foreignPartition.Topic,
                foreignPartition.Partition,
                GroupId),
            failure);

        var firstResult = await first.ConsumeOneAsync(TimeSpan.Zero);
        await second.DisposeAsync();
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => first.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(firstResult).IsNotNull();
        await Assert.That(firstResult!.Value.Partition).IsNotEqualTo(foreignPartition.Partition);
        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    [Arguments(KafkaFaultOperation.Fetch)]
    [Arguments(KafkaFaultOperation.Consume)]
    public async Task ConsumeOneAsync_GroupOwnershipChangeDuringFaultBarrierReselectsRecord(
        KafkaFaultOperation operation)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 0,
            Key = "key",
            Value = "zero"
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 1,
            Key = "key",
            Value = "one"
        });
        await using var first = CreateConsumer(cluster, memberId: "z-member");
        first.Subscribe(Topic);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(operation, Topic, 0, GroupId));

        var firstConsume = first.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await barrier.WaitUntilEnteredAsync();
        await using var second = CreateConsumer(cluster, memberId: "a-member");
        second.Subscribe(Topic);
        await Assert.That(barrier.Release()).IsTrue();

        var firstResult = await firstConsume.WaitAsync(TimeSpan.FromSeconds(5));
        var secondResult = await second.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(firstResult).IsNotNull();
        await Assert.That(firstResult!.Value.Partition).IsEqualTo(1);
        await Assert.That(secondResult).IsNotNull();
        await Assert.That(secondResult!.Value.Partition).IsEqualTo(0);
        await Assert.That(first.GetPosition(new TopicPartition(Topic, 0))).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_EmptyCustomPlanSkipsRecordProbes()
    {
        var innerPlan = new KafkaFaultPlan();
        var faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(cluster);
        consumer.Subscribe(Topic);

        var records = new List<ConsumeResult<string, string>>();
        await foreach (var record in consumer.ConsumeSnapshotAsync())
            records.Add(record);

        await Assert.That(records).Count().IsEqualTo(1);
        await Assert.That(faultPlan.MatchingProbeCount).IsEqualTo(0);
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
    public async Task CommitAsync_InjectedPlanMatchesConcreteStoredOffset()
    {
        var innerPlan = new KafkaFaultPlan();
        IKafkaFaultPlan faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        cluster.CreateTopic(Topic);
        var consumer = CreateConsumer(cluster, enableAutoOffsetStore: false);
        consumer.Assign(Partition);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var failure = new InvalidOperationException("resource commit failed");
        innerPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync().AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();
    }

    [Test]
    public async Task CommitAsync_InjectedPlanMatchesLaterExplicitOffset()
    {
        var innerPlan = new KafkaFaultPlan();
        IKafkaFaultPlan faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        var consumer = CreateConsumer(cluster, enableAutoOffsetStore: false);
        var otherPartition = new TopicPartition(Topic, 1);
        var failure = new InvalidOperationException("later resource commit failed");
        innerPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 1, GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync(
                [
                    new TopicPartitionOffset(Topic, 0, 1),
                    new TopicPartitionOffset(Topic, 1, 1)
                ]).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(await consumer.GetCommittedOffsetAsync(otherPartition)).IsNull();
    }

    [Test]
    public async Task CommitAsync_InjectedPlanPreservesResourceBeforeGroupOrder()
    {
        var innerPlan = new KafkaFaultPlan();
        IKafkaFaultPlan faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        var consumer = CreateConsumer(cluster, enableAutoOffsetStore: false);
        var resourceFailure = new InvalidOperationException("resource commit failed");
        innerPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId),
            resourceFailure);
        innerPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            new InvalidOperationException("group commit failed"));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync([new TopicPartitionOffset(Topic, 0, 1)]).AsTask());

        await Assert.That(actual).IsSameReferenceAs(resourceFailure);
        await Assert.That(innerPlan.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CommitAsync_ConcurrentCommitsReserveDistinctOrderedFaults()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var firstConsumer = CreateConsumer(cluster, enableAutoOffsetStore: false);
        await using var secondConsumer = CreateConsumer(cluster, enableAutoOffsetStore: false);
        var firstPartition = new TopicPartitionOffset(Topic, 0, 1);
        var secondPartition = new TopicPartitionOffset(Topic, 1, 1);
        var firstBarrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId));
        var secondFailure = new InvalidOperationException("second partition");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 1, GroupId),
            secondFailure);

        var firstCommit = firstConsumer.CommitAsync([firstPartition, secondPartition]).AsTask();
        await firstBarrier.WaitUntilEnteredAsync();
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            secondConsumer.CommitAsync([secondPartition]).AsTask());
        await Assert.That(firstBarrier.Release()).IsTrue();
        await firstCommit;

        await Assert.That(actual).IsSameReferenceAs(secondFailure);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CommitAsync_ConcurrentStoredCommitsReserveDistinctOrderedFaults()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 2);
        await using var firstConsumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            groupId: "stored-a");
        await using var secondConsumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            groupId: "stored-b");
        var secondPartition = new TopicPartition(Topic, 1);
        var offsets = new[]
        {
            new TopicPartitionOffset(Topic, 0, 1),
            new TopicPartitionOffset(Topic, 1, 1)
        };
        firstConsumer.Assign([Partition, secondPartition]);
        secondConsumer.Assign([Partition, secondPartition]);
        firstConsumer.StoreOffsets(offsets);
        secondConsumer.StoreOffsets(offsets);
        var firstBarrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0));
        var secondFailure = new InvalidOperationException("second partition");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 1),
            secondFailure);

        var firstCommit = firstConsumer.CommitAsync().AsTask();
        await firstBarrier.WaitUntilEnteredAsync();
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            secondConsumer.CommitAsync().AsTask());
        await Assert.That(firstBarrier.Release()).IsTrue();
        await firstCommit;

        await Assert.That(actual).IsSameReferenceAs(secondFailure);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CommitAsync_AssignmentChangeDuringBarrierSkipsCapturedOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 2);
        await using var consumer = CreateConsumer(cluster, enableAutoOffsetStore: false);
        var secondPartition = new TopicPartition(Topic, 1);
        consumer.Assign(Partition, secondPartition);
        consumer.StoreOffsets([
            new TopicPartitionOffset(Topic, 0, 1),
            new TopicPartitionOffset(Topic, 1, 1)
        ]);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId));

        var commit = consumer.CommitAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        consumer.IncrementalUnassign([Partition]);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 1, 9));
        await Assert.That(barrier.Release()).IsTrue();
        await commit;

        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsNull();
        await Assert.That(cluster.GetCommittedOffset(GroupId, secondPartition)).IsNull();

        await consumer.CommitAsync();

        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsNull();
        await Assert.That(cluster.GetCommittedOffset(GroupId, secondPartition)).IsEqualTo(9);
    }

    [Test]
    public async Task CommitAsync_PauseChangeDuringBarrierCommitsCapturedOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 2);
        await using var consumer = CreateConsumer(cluster, enableAutoOffsetStore: false);
        var secondPartition = new TopicPartition(Topic, 1);
        consumer.Assign(Partition, secondPartition);
        consumer.StoreOffsets([
            new TopicPartitionOffset(Topic, 0, 1),
            new TopicPartitionOffset(Topic, 1, 1)
        ]);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId));

        var commit = consumer.CommitAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        consumer.Pause(secondPartition);
        consumer.Resume(secondPartition);
        await Assert.That(barrier.Release()).IsTrue();
        await commit;

        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(1);
        await Assert.That(cluster.GetCommittedOffset(GroupId, secondPartition)).IsEqualTo(1);
    }

    [Test]
    public async Task CommitAsync_GroupChangeDuringBarrierDoesNotOverwriteNewOwnerOffset()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic);
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            memberId: "z-member");
        consumer.Subscribe(Topic);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var staleCommit = consumer.CommitAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        await using var newOwner = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            memberId: "a-member");
        newOwner.Subscribe(Topic);
        await newOwner.CommitAsync([new TopicPartitionOffset(Topic, 0, 9)]);
        await Assert.That(barrier.Release()).IsTrue();
        await staleCommit;

        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(9);
    }

    [Test]
    public async Task CommitAsync_BarrierCommitsExactCallerOffsets()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        TopicPartitionOffset[] offsets = [new(Topic, 0, 1)];
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId));
        cluster.FaultPlan.FaultConsumed += _ =>
            offsets[0] = new TopicPartitionOffset(Topic, 0, 9);

        var commit = consumer.CommitAsync(offsets).AsTask();
        await barrier.WaitUntilEnteredAsync();
        offsets[0] = new TopicPartitionOffset(Topic, 0, 10);
        await Assert.That(barrier.Release()).IsTrue();
        await commit;

        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task OnDeliveryAutoCommit_BarrierCommitsExactCapturedOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 0,
            Key = "key",
            Value = "value"
        });
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        var secondPartition = new TopicPartition(Topic, 1);
        consumer.Assign(Partition, secondPartition);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 1, 1));
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId));

        var consume = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await barrier.WaitUntilEnteredAsync();
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 1, 9));
        await Assert.That(barrier.Release()).IsTrue();
        _ = await consume;

        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(1);
        await Assert.That(cluster.GetCommittedOffset(GroupId, secondPartition)).IsEqualTo(1);
    }

    [Test]
    public async Task OnDeliveryAutoCommit_GroupChangeDuringBarrierDoesNotOverwriteNewOwnerOffset()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto,
            memberId: "z-member");
        consumer.Subscribe(Topic);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var staleConsume = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await barrier.WaitUntilEnteredAsync();
        await using var newOwner = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            memberId: "a-member");
        newOwner.Subscribe(Topic);
        await newOwner.CommitAsync([new TopicPartitionOffset(Topic, 0, 9)]);
        await Assert.That(barrier.Release()).IsTrue();

        await Assert.That(await staleConsume).IsNull();
        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(9);
    }

    [Test]
    public async Task OnDeliveryAutoCommit_BarrierAllowsPausingUnrelatedPartition()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 0,
            Key = "key",
            Value = "value"
        });
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        var secondPartition = new TopicPartition(Topic, 1);
        consumer.Assign(Partition, secondPartition);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 1, 1));
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId));

        var consume = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await barrier.WaitUntilEnteredAsync();
        consumer.Pause(secondPartition);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 1, 9));
        await Assert.That(barrier.Release()).IsTrue();
        _ = await consume;

        await Assert.That(consumer.Paused).Contains(secondPartition);
        await Assert.That(consumer.Paused).DoesNotContain(Partition);
        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(1);
        await Assert.That(cluster.GetCommittedOffset(GroupId, secondPartition)).IsEqualTo(1);
    }

    [Test]
    public async Task OnDeliveryAutoCommit_UnrelatedCommitFaultDoesNotReserve()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var deserializer = new AsyncStringDeserializer();
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            deserializer,
            deserializer,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = true,
                OffsetCommitMode = OffsetCommitMode.Auto,
                OffsetStoreTiming = OffsetStoreTiming.OnDelivery
            });
        consumer.Subscribe(Topic);
        cluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(
                KafkaFaultOperation.Commit,
                topic: "payments",
                partition: 0,
                groupId: GroupId),
            new InvalidOperationException("unrelated"));

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result).IsNotNull();
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task OnDeliveryAutoCommit_FaultObserverCannotReentrantlyCommitCurrentRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            failure);
        ConsumeResult<string, string>? reentrantResult = null;
        cluster.FaultPlan.FaultConsumed += _ =>
            reentrantResult = consumer.ConsumeOneAsync(TimeSpan.Zero).GetAwaiter().GetResult();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(reentrantResult).IsNull();
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);
        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsNull();
    }

    [Test]
    public async Task OnDeliveryAutoCommit_FaultObserverCannotWidenCapturedOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 0,
            Key = "key",
            Value = "value"
        });
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        var secondPartition = new TopicPartition(Topic, 1);
        consumer.Assign(Partition, secondPartition);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 1, 1));
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId));
        cluster.FaultPlan.FaultConsumed += _ =>
            consumer.StoreOffset(new TopicPartitionOffset(Topic, 1, 9));

        var consume = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await barrier.WaitUntilEnteredAsync();
        await Assert.That(barrier.Release()).IsTrue();
        _ = await consume;

        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(1);
        await Assert.That(cluster.GetCommittedOffset(GroupId, secondPartition)).IsEqualTo(1);
    }

    [Test]
    public async Task OnDeliveryAutoCommit_FaultObserverPauseRemainsPaused()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            failure);
        cluster.FaultPlan.FaultConsumed += _ => consumer.Pause(Partition);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(consumer.Paused).Contains(Partition);
    }

    [Test]
    public async Task OnDeliveryAutoCommit_BarrierKeepsFaultObserverPauseVisible()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        cluster.FaultPlan.FaultConsumed += _ => consumer.Pause(Partition);

        var consume = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await barrier.WaitUntilEnteredAsync();

        await Assert.That(consumer.Paused).Contains(Partition);
        await Assert.That(barrier.Release()).IsTrue();
        _ = await consume;
        await Assert.That(consumer.Paused).Contains(Partition);
    }

    [Test]
    public async Task OnDeliveryAutoCommit_FaultObserverClosePreventsReservation()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Assign(Partition);
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId),
            failure);
        Task? closeTask = null;
        cluster.FaultPlan.FaultConsumed += _ => closeTask = consumer.CloseAsync().AsTask();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(closeTask).IsNotNull();
        await closeTask!;
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CommitAsync_InjectedPlanPreservesStoredResourceBeforeGroupOrder()
    {
        var innerPlan = new KafkaFaultPlan();
        IKafkaFaultPlan faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        cluster.CreateTopic(Topic);
        var consumer = CreateConsumer(cluster, enableAutoOffsetStore: false);
        consumer.Assign(Partition);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var resourceFailure = new InvalidOperationException("stored resource commit failed");
        innerPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId),
            resourceFailure);
        innerPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            new InvalidOperationException("stored group commit failed"));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync().AsTask());

        await Assert.That(actual).IsSameReferenceAs(resourceFailure);
        await Assert.That(innerPlan.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CommitAsync_EmptyListConsumesGroupFault()
    {
        var cluster = new InMemoryKafkaCluster();
        var consumer = CreateConsumer(cluster, enableAutoOffsetStore: false);
        var failure = new InvalidOperationException("empty commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync(Array.Empty<TopicPartitionOffset>()).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task CommitAsync_EmptyListSkipsResourceFaultBeforeGroupFault()
    {
        var cluster = new InMemoryKafkaCluster();
        var consumer = CreateConsumer(cluster, enableAutoOffsetStore: false);
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId),
            new InvalidOperationException("resource commit failed"));
        var groupFailure = new InvalidOperationException("group commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            groupFailure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync(Array.Empty<TopicPartitionOffset>()).AsTask());

        await Assert.That(actual).IsSameReferenceAs(groupFailure);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
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
    public async Task Snapshot_WaitsForConcurrentAutoCommitReservation()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var snapshotFetchBarrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, Topic, 0, GroupId));
        var concurrentCommitBarrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();

        var snapshotMove = snapshot.MoveNextAsync().AsTask();
        await snapshotFetchBarrier.WaitUntilEnteredAsync();
        var concurrentConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await concurrentCommitBarrier.WaitUntilEnteredAsync();
        await Assert.That(snapshotFetchBarrier.Release()).IsTrue();
        await Assert.That(concurrentCommitBarrier.Release()).IsTrue();

        await Assert.That(await concurrentConsume).IsNotNull();
        await Assert.That(await snapshotMove).IsFalse();
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task Snapshot_CloseReleasesConcurrentAutoCommitReservationWait()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var snapshotFetchBarrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, Topic, 0, GroupId));
        var concurrentCommitBarrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();

        var snapshotMove = snapshot.MoveNextAsync().AsTask();
        await snapshotFetchBarrier.WaitUntilEnteredAsync();
        var concurrentConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await concurrentCommitBarrier.WaitUntilEnteredAsync();
        var snapshotWaitEntered = consumer.WaitUntilAutoCommitAdvancementWaiterEnteredAsync();
        await Assert.That(snapshotFetchBarrier.Release()).IsTrue();
        await snapshotWaitEntered;

        await consumer.CloseAsync();
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await snapshotMove.WaitAsync(TimeSpan.FromSeconds(5)));

        await Assert.That(concurrentCommitBarrier.Release()).IsTrue();
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => concurrentConsume);
    }

    [Test]
    public async Task CloseAsync_WakesInfiniteConsumeWaitingBehindAutoCommitReservation()
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

        var reservedConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await barrier.WaitUntilEnteredAsync();
        var waitingConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await Assert.That(waitingConsume.IsCompleted).IsFalse();

        await consumer.CloseAsync();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            waitingConsume.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.That(barrier.Release()).IsTrue();
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => reservedConsume);
    }

    [Test]
    public async Task AutoCommitAdvancementWaitHook_IsReusableAndCloseCompletesIt()
    {
        var consumer = CreateConsumer(new InMemoryKafkaCluster());

        var first = consumer.WaitUntilAutoCommitAdvancementWaiterEnteredAsync();
        var second = consumer.WaitUntilAutoCommitAdvancementWaiterEnteredAsync();

        await Assert.That(second).IsSameReferenceAs(first);
        await consumer.CloseAsync();
        await first;
        await Assert.That(consumer.WaitUntilAutoCommitAdvancementWaiterEnteredAsync().IsCompletedSuccessfully)
            .IsTrue();
    }

    [Test]
    public async Task AutoCommit_BarrierRejectsSeekUntilPositionAdvances()
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

        var actual = Assert.Throws<InvalidOperationException>(() =>
            consumer.Seek(new TopicPartitionOffset(Topic, 0, 1)));

        await Assert.That(actual.Message).Contains("automatic commit fault");
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);
        await Assert.That(consumer.Paused).IsEmpty();
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
        await Assert.That(barrier.Release()).IsTrue();

        var result = await operation;

        await Assert.That(result).IsNotNull();
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(1);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);

        consumer.Seek(new TopicPartitionOffset(Topic, 0, 0));
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);
    }

    [Test]
    public async Task AutoCommit_BarrierCancellationReleasesPositionReservation()
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
        using var cancellation = new CancellationTokenSource();

        var operation = consumer
            .ConsumeOneAsync(Timeout.InfiniteTimeSpan, cancellation.Token)
            .AsTask();
        await barrier.WaitUntilEnteredAsync();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
        consumer.Seek(new TopicPartitionOffset(Topic, 0, 1));

        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(1);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();
        await Assert.That(barrier.Release()).IsTrue();
    }

    [Test]
    public async Task AutoCommit_BarrierCancellationWakesConcurrentInfiniteConsume()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        using var cancellation = new CancellationTokenSource();

        var reservedConsume = consumer
            .ConsumeOneAsync(Timeout.InfiniteTimeSpan, cancellation.Token)
            .AsTask();
        await barrier.WaitUntilEnteredAsync();
        var waitingConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => reservedConsume);
        var result = await waitingConsume.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("key");
        await Assert.That(barrier.Release()).IsTrue();
    }

    [Test]
    public async Task AutoCommit_BarrierCancellationInvalidatesPausedFaultCache()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        using var cancellation = new CancellationTokenSource();

        var reservedConsume = consumer
            .ConsumeOneAsync(Timeout.InfiniteTimeSpan, cancellation.Token)
            .AsTask();
        await barrier.WaitUntilEnteredAsync();
        var failure = new InvalidOperationException("fetch failed");
        cluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Fetch, Topic, 0, GroupId),
            failure);

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
        cancellation.Cancel();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => reservedConsume);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(barrier.Release()).IsTrue();
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
    public async Task AutoCommit_PauseBeforeAdvancementPreservesFault()
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

        var operation = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await deserializer.WaitUntilEnteredAsync();
        consumer.Pause(Partition);
        deserializer.Release();

        await Assert.That(await operation).IsNull();
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
        consumer.Resume(Partition);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task ConsumeOneAsync_GroupChangeAfterDeserializationPreservesConsumeFault()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var deserializer = new BlockingAsyncDeserializer();
        await using var consumer = CreateAsyncConsumer(cluster, deserializer, "z-member");
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException("consume failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Consume, Topic, 0, GroupId),
            failure);

        var operation = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await deserializer.WaitUntilEnteredAsync();
        await using var joiningConsumer = CreateConsumer(cluster, memberId: "a-member");
        joiningConsumer.Subscribe(Topic);
        deserializer.Release();

        await Assert.That(await operation).IsNull();
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            joiningConsumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());
        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task ConsumeOneAsync_SynchronousDeserializerPausePreventsStaleDelivery()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        InMemoryConsumer<string, string>? consumer = null;
        var deserializer = new CallbackOnceDeserializer(() => consumer!.Pause(Partition));
        await using (consumer = new InMemoryConsumer<string, string>(
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
            }))
        {
            consumer.Subscribe(Topic);

            var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

            await Assert.That(result).IsNull();
            await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);
            await Assert.That(consumer.Paused).Contains(Partition);
            await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();
        }
    }

    [Test]
    public async Task ConsumeOneAsync_GroupChangeDuringCommitProbePreservesFault()
    {
        var innerPlan = new KafkaFaultPlan();
        var faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var deserializer = new AsyncStringDeserializer();
        await using var consumer = CreateAsyncConsumer(cluster, deserializer, "z-member");
        consumer.Subscribe(Topic);
        var commitFailure = new InvalidOperationException("commit failed");
        var commitScope = new KafkaFaultScope(
            KafkaFaultOperation.Commit,
            groupId: GroupId);
        innerPlan.Fail(commitScope, commitFailure);
        InMemoryConsumer<string, string>? joiningConsumer = null;
        faultPlan.ObserveNextCommitProbe(() =>
        {
            joiningConsumer = CreateConsumer(cluster, memberId: "a-member");
            joiningConsumer.Subscribe(Topic);
        });

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result).IsNull();
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);
        await Assert.That(joiningConsumer).IsNotNull();
        await Assert.That(innerPlan.Count).IsEqualTo(1);
        await Assert.That(innerPlan.Clear(commitScope)).IsEqualTo(1);
        var reassignedConsumer = joiningConsumer!;
        await using (reassignedConsumer)
        {
            var reassignedResult = await reassignedConsumer.ConsumeOneAsync(TimeSpan.Zero);
            await Assert.That(reassignedResult).IsNotNull();
            await Assert.That(reassignedResult!.Value.Offset).IsEqualTo(0);
        }
    }

    [Test]
    public async Task ConsumeOneAsync_DisposalDuringDeserializationPreservesConsumeFault()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var deserializer = new BlockingAsyncDeserializer();
        var consumer = CreateAsyncConsumer(cluster, deserializer);
        consumer.Subscribe(Topic);
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Consume, Topic, 0, GroupId),
            new InvalidOperationException("consume failed"));

        var operation = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await deserializer.WaitUntilEnteredAsync();
        var disposal = consumer.DisposeAsync().AsTask();
        deserializer.Release();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => operation);
        await disposal;
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments(KafkaFaultOperation.Consume)]
    [Arguments(KafkaFaultOperation.Commit)]
    public async Task Snapshot_GroupChangeAfterAsyncDeserializationPreservesFault(
        KafkaFaultOperation operation)
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var deserializer = new BlockingAsyncDeserializer();
        await using var consumer = CreateAsyncConsumer(cluster, deserializer);
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException("record fault failed");
        cluster.FaultPlan.Fail(
            operation == KafkaFaultOperation.Consume
                ? new KafkaFaultScope(operation, Topic, 0, GroupId)
                : new KafkaFaultScope(operation, groupId: GroupId),
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
    public async Task Snapshot_GroupChangeDuringSynchronousDeserializationPreservesConsumeFault()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var joiningConsumer = CreateConsumer(cluster);
        var deserializer = new CallbackOnceDeserializer(() => joiningConsumer.Subscribe(Topic));
        await using var consumer = new InMemoryConsumer<string, string>(
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
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException("record fault failed");
        var faultScope = new KafkaFaultScope(
            KafkaFaultOperation.Consume,
            Topic,
            0,
            GroupId);
        cluster.FaultPlan.Fail(faultScope, failure);
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            snapshot.MoveNextAsync().AsTask());

        await Assert.That(actual).IsNotSameReferenceAs(failure);
        await Assert.That(actual!.Message).Contains("snapshot enumeration");
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
        await Assert.That(cluster.FaultPlan.Clear(faultScope)).IsEqualTo(1);
    }

    [Test]
    public async Task SnapshotCompletion_BarrierCommitsExactCapturedOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic);
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId));
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();

        var completion = snapshot.MoveNextAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 9));
        await Assert.That(barrier.Release()).IsTrue();

        await Assert.That(await completion).IsFalse();
        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task Snapshot_GroupChangeDuringCommitBarrierThrowsBeforePublishingRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();

        var moveNext = snapshot.MoveNextAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        await using var joiningConsumer = CreateConsumer(cluster);
        joiningConsumer.Subscribe(Topic);
        await Assert.That(barrier.Release()).IsTrue();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => moveNext);

        await Assert.That(actual!.Message).Contains("snapshot enumeration");
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Snapshot_GroupChangeAfterYieldPreservesCommitFault()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();

        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var failure = new InvalidOperationException("commit failed");
        var faultScope = new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId);
        cluster.FaultPlan.Fail(faultScope, failure);
        await using var joiningConsumer = CreateConsumer(cluster);
        joiningConsumer.Subscribe(Topic);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            snapshot.MoveNextAsync().AsTask());

        await Assert.That(actual).IsNotSameReferenceAs(failure);
        await Assert.That(actual!.Message).Contains("snapshot enumeration");
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
        await Assert.That(cluster.FaultPlan.Clear(faultScope)).IsEqualTo(1);
    }

    [Test]
    public async Task Snapshot_FaultAddedDuringAdvancementWaitAppliesToNextRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        var deserializer = new FirstCallBlockingAsyncDeserializer();
        await using var consumer = CreateAsyncConsumer(cluster, deserializer);
        consumer.Subscribe(Topic);
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();
        var snapshotMove = snapshot.MoveNextAsync().AsTask();
        await deserializer.WaitUntilEnteredAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var commitBarrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        var concurrentConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await commitBarrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var waitEntered = consumer.WaitUntilAutoCommitAdvancementWaiterEnteredAsync();
        deserializer.Release();
        await waitEntered.WaitAsync(TimeSpan.FromSeconds(5));
        var failure = new InvalidOperationException("consume failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Consume, Topic, 0, GroupId),
            failure);
        await Assert.That(commitBarrier.Release()).IsTrue();

        await Assert.That(await concurrentConsume.WaitAsync(TimeSpan.FromSeconds(5))).IsNotNull();
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => snapshotMove);

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Snapshot_FaultAddedAfterFirstRecordAppliesToNextRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        await using var consumer = CreateConsumer(cluster);
        consumer.Subscribe(Topic);
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();

        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var failure = new InvalidOperationException("consume failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Consume, Topic, 0, GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            snapshot.MoveNextAsync().AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Snapshot_FaultAddedDuringDeserializationAppliesToCurrentRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var deserializer = new BlockingAsyncDeserializer();
        await using var consumer = CreateAsyncConsumer(cluster, deserializer);
        consumer.Subscribe(Topic);
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();

        var moveNext = snapshot.MoveNextAsync().AsTask();
        await deserializer.WaitUntilEnteredAsync();
        var failure = new InvalidOperationException("consume failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Consume, Topic, 0, GroupId),
            failure);
        deserializer.Release();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => moveNext);

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);

        var retry = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(retry).IsNotNull();
        await Assert.That(retry!.Value.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task Snapshot_StopsProbingAfterOneShotFaultIsConsumed()
    {
        var innerPlan = new KafkaFaultPlan();
        var faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        await using var consumer = CreateConsumer(cluster);
        consumer.Subscribe(Topic);
        var barrier = innerPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Consume, Topic, 0, GroupId));
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();

        var firstMove = snapshot.MoveNextAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        await Assert.That(barrier.Release()).IsTrue();
        await Assert.That(await firstMove).IsTrue();
        var probesAfterFault = faultPlan.MatchingProbeCount;

        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();

        await Assert.That(faultPlan.MatchingProbeCount).IsEqualTo(probesAfterFault);
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
    public async Task ConsumeOneAsync_FaultAddedDuringSynchronousDeserializationAppliesToCurrentRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var failure = new InvalidOperationException("consume failed");
        var deserializer = new CallbackOnceDeserializer(() => cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Consume, Topic, 0, GroupId),
            failure));
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            deserializer,
            deserializer,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Manual,
                EnableAutoOffsetStore = false
            });
        consumer.Subscribe(Topic);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(0);
        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task ManualCommit_InjectedCommitFaultDoesNotEnterAsyncFaultPath()
    {
        var innerPlan = new KafkaFaultPlan();
        var faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Manual);
        consumer.Subscribe(Topic);
        innerPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            new InvalidOperationException("commit failed"));

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(result).IsNotNull();
        await Assert.That(faultPlan.MatchingProbeCount).IsEqualTo(0);
        await Assert.That(innerPlan.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AutoCommit_WithoutCommittableOffsetStaysOnSynchronousPath()
    {
        var innerPlan = new KafkaFaultPlan();
        var faultPlan = new DelegatingFaultPlan(innerPlan);
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions(), faultPlan);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var failure = new InvalidOperationException("commit failed");
        innerPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            failure);

        var operation = consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(operation.IsCompletedSuccessfully).IsTrue();
        var result = operation.Result;
        await Assert.That(result).IsNotNull();
        await Assert.That(faultPlan.MatchingProbeCount).IsEqualTo(0);

        consumer.StoreOffset(result!.Value);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

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
    public async Task AutoCommit_AfterProcessingBarrierReservesInDoubtAdvancement()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto,
            offsetStoreTiming: OffsetStoreTiming.AfterProcessing);
        consumer.Subscribe(Topic);
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var reservedConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await barrier.WaitUntilEnteredAsync();
        var concurrentResult = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        await Assert.That(concurrentResult).IsNull();
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(1);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();
        await Assert.That(barrier.Release()).IsTrue();

        var result = await reservedConsume;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("key-1");
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(2);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task AutoCommit_AfterProcessingGroupChangeDoesNotOverwriteNewOwnerOffset()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto,
            offsetStoreTiming: OffsetStoreTiming.AfterProcessing,
            memberId: "z-member");
        consumer.Subscribe(Topic);
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var staleConsume = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await barrier.WaitUntilEnteredAsync();
        await using var newOwner = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            memberId: "a-member");
        newOwner.Subscribe(Topic);
        await newOwner.CommitAsync([new TopicPartitionOffset(Topic, 0, 9)]);
        await Assert.That(barrier.Release()).IsTrue();

        await Assert.That(await staleConsume).IsNull();
        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(9);
    }

    [Test]
    public async Task AutoCommit_AfterProcessingFaultObserverClosePreventsLateReservation()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto,
            offsetStoreTiming: OffsetStoreTiming.AfterProcessing);
        consumer.Subscribe(Topic);
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        Task? closeTask = null;
        cluster.FaultPlan.FaultConsumed += observation =>
        {
            if (observation.OperationScope.Operation == KafkaFaultOperation.Commit)
                closeTask = consumer.CloseAsync().AsTask();
        };

        var consume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();

        try
        {
            await Assert.That(consume.IsCompleted).IsTrue();
            _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => consume);
            await Assert.That(closeTask).IsNotNull();
            await closeTask!;
        }
        finally
        {
            barrier.Release();
        }
    }

    [Test]
    public async Task AutoCommit_AfterProcessingFaultObserverCannotWidenCapturedOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto,
            offsetStoreTiming: OffsetStoreTiming.AfterProcessing);
        var secondPartition = new TopicPartition(Topic, 1);
        consumer.Assign(Partition, secondPartition);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 1, 1));
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId));
        cluster.FaultPlan.FaultConsumed += _ =>
            consumer.StoreOffset(new TopicPartitionOffset(Topic, 1, 9));

        var consume = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await barrier.WaitUntilEnteredAsync();
        await Assert.That(barrier.Release()).IsTrue();
        _ = await consume;

        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
        await Assert.That(await consumer.GetCommittedOffsetAsync(secondPartition)).IsEqualTo(1);
    }

    [Test]
    public async Task AutoCommit_AfterProcessingSharedWaiterCannotProveNextRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        var deserializer = new AsyncStringDeserializer();
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            deserializer,
            deserializer,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Auto,
                EnableAutoOffsetStore = true,
                OffsetStoreTiming = OffsetStoreTiming.AfterProcessing
            });
        consumer.Subscribe(Topic);
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var provingConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await barrier.WaitUntilEnteredAsync();
        var sharedWaiter = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await Assert.That(sharedWaiter.IsCompleted).IsFalse();
        await Assert.That(barrier.Release()).IsTrue();

        var results = await Task.WhenAll(provingConsume, sharedWaiter);

        await Assert.That(results.Count(static result => result is not null)).IsEqualTo(1);
        await Assert.That(results.Single(static result => result is not null)!.Value.Key)
            .IsEqualTo("key-1");
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);

        var commitFault = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            commitFault);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());

        await Assert.That(actual).IsSameReferenceAs(commitFault);
    }

    [Test]
    public async Task ConsumeAsync_AfterProcessingSharedWaiterContinuesEnumeration()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        await producer.ProduceAsync(Topic, "key-2", "value-2");
        var deserializer = new AsyncStringDeserializer();
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            deserializer,
            deserializer,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Auto,
                EnableAutoOffsetStore = true,
                OffsetStoreTiming = OffsetStoreTiming.AfterProcessing
            });
        consumer.Subscribe(Topic);
        await using var stream = consumer.ConsumeAsync().GetAsyncEnumerator();
        await Assert.That(await stream.MoveNextAsync()).IsTrue();
        await Assert.That(stream.Current.Key).IsEqualTo("key-0");
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var provingConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await barrier.WaitUntilEnteredAsync();
        var sharedMove = stream.MoveNextAsync().AsTask();
        await Assert.That(sharedMove.IsCompleted).IsFalse();
        await Assert.That(barrier.Release()).IsTrue();

        var concurrent = await provingConsume;
        await Assert.That(concurrent).IsNotNull();
        await Assert.That(concurrent!.Value.Key).IsEqualTo("key-1");
        await Assert.That(sharedMove.IsCompleted).IsFalse();
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);

        await consumer.CommitAsync();

        await Assert.That(await sharedMove).IsTrue();
        await Assert.That(stream.Current.Key).IsEqualTo("key-2");
    }

    [Test]
    public async Task ConsumeAsync_FreshSharedWaiterAwaitsOwnerDelivery()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        await producer.ProduceAsync(Topic, "key-2", "value-2");
        var deserializer = new AsyncStringDeserializer();
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            deserializer,
            deserializer,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Auto,
                EnableAutoOffsetStore = true,
                OffsetStoreTiming = OffsetStoreTiming.AfterProcessing
            });
        consumer.Subscribe(Topic);
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var ownerConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await barrier.WaitUntilEnteredAsync();
        await using var stream = consumer.ConsumeAsync().GetAsyncEnumerator();
        var sharedMove = stream.MoveNextAsync().AsTask();
        await Assert.That(sharedMove.IsCompleted).IsFalse();
        await Assert.That(barrier.Release()).IsTrue();

        var ownerResult = await ownerConsume;
        await Assert.That(ownerResult).IsNotNull();
        await Assert.That(ownerResult!.Value.Key).IsEqualTo("key-1");
        await Assert.That(sharedMove.IsCompleted).IsFalse();

        await consumer.CommitAsync();

        await Assert.That(await sharedMove).IsTrue();
        await Assert.That(stream.Current.Key).IsEqualTo("key-2");
    }

    [Test]
    public async Task ConsumeAsync_AfterProcessingOwnerCompletesSharedWaiter()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        var deserializer = new AsyncStringDeserializer();
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            deserializer,
            deserializer,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Auto,
                EnableAutoOffsetStore = true,
                OffsetStoreTiming = OffsetStoreTiming.AfterProcessing
            });
        consumer.Subscribe(Topic);
        await using var stream = consumer.ConsumeAsync().GetAsyncEnumerator();
        await Assert.That(await stream.MoveNextAsync()).IsTrue();
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var ownerMove = stream.MoveNextAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        var sharedConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await Assert.That(sharedConsume.IsCompleted).IsFalse();
        await Assert.That(barrier.Release()).IsTrue();

        await Assert.That(await ownerMove.WaitAsync(TimeSpan.FromSeconds(5))).IsTrue();
        await Assert.That(stream.Current.Key).IsEqualTo("key-1");
        await Assert.That(await sharedConsume.WaitAsync(TimeSpan.FromSeconds(5))).IsNull();
    }

    [Test]
    public async Task Snapshot_AfterProcessingOwnerCompletesSharedWaiter()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key-0", "value-0");
        await producer.ProduceAsync(Topic, "key-1", "value-1");
        var deserializer = new AsyncStringDeserializer();
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            deserializer,
            deserializer,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Auto,
                EnableAutoOffsetStore = true,
                OffsetStoreTiming = OffsetStoreTiming.AfterProcessing
            });
        consumer.Subscribe(Topic);
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var ownerMove = snapshot.MoveNextAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        var sharedConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await Assert.That(sharedConsume.IsCompleted).IsFalse();
        await Assert.That(barrier.Release()).IsTrue();

        await Assert.That(await ownerMove.WaitAsync(TimeSpan.FromSeconds(5))).IsTrue();
        await Assert.That(snapshot.Current.Key).IsEqualTo("key-1");
        await Assert.That(await sharedConsume.WaitAsync(TimeSpan.FromSeconds(5))).IsNull();
    }

    [Test]
    public async Task Snapshot_AfterProcessingSharedWaiterFailsInsteadOfTruncating()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 2);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 0,
            Key = "snapshot-0",
            Value = "value-0"
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 1,
            Key = "concurrent",
            Value = "value-1"
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = Topic,
            Partition = 1,
            Key = "remaining",
            Value = "value-2"
        });
        var deserializer = new AsyncStringDeserializer();
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            deserializer,
            deserializer,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Auto,
                EnableAutoOffsetStore = true,
                OffsetStoreTiming = OffsetStoreTiming.AfterProcessing
            });
        consumer.Assign(Partition, new TopicPartition(Topic, 1));
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        await Assert.That(snapshot.Current.Key).IsEqualTo("snapshot-0");
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var provingConsume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        await barrier.WaitUntilEnteredAsync();
        var sharedMove = snapshot.MoveNextAsync().AsTask();
        await Assert.That(sharedMove.IsCompleted).IsFalse();
        await Assert.That(barrier.Release()).IsTrue();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => sharedMove);
        var concurrent = await provingConsume;

        await Assert.That(failure!.Message).Contains("snapshot enumeration");
        await Assert.That(concurrent).IsNotNull();
        await Assert.That(concurrent!.Value.Key).IsEqualTo("concurrent");
        await consumer.CommitAsync();
        var remaining = new List<string>();
        await foreach (var record in consumer.ConsumeSnapshotAsync())
            remaining.Add(record.Key!);
        await Assert.That(remaining).IsEquivalentTo(["remaining"]);
    }

    [Test]
    public async Task AutoCommit_AfterProcessingBarrierPreservesCallerPause()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto,
            offsetStoreTiming: OffsetStoreTiming.AfterProcessing);
        consumer.Subscribe(Topic);
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        consumer.Pause(Partition);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var operation = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await barrier.WaitUntilEnteredAsync();
        await Assert.That(barrier.Release()).IsTrue();

        await Assert.That(await operation).IsNull();
        await Assert.That(consumer.Paused).Contains(Partition);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumeAsync_DisposedAfterYieldPreservesCommitFault()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto,
            offsetStoreTiming: OffsetStoreTiming.AfterProcessing);
        consumer.Subscribe(Topic);
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            new InvalidOperationException("commit failed"));
        await using var records = consumer.ConsumeAsync().GetAsyncEnumerator();

        await Assert.That(await records.MoveNextAsync()).IsTrue();
        await consumer.CloseAsync();
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            records.MoveNextAsync().AsTask());

        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
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
    public async Task CloseAsync_ConcurrentCallersShareCommitBarrier()
    {
        var cluster = new InMemoryKafkaCluster();
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var firstClose = consumer.CloseAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        var secondClose = consumer.CloseAsync().AsTask();

        await Assert.That(secondClose).IsSameReferenceAs(firstClose);
        await Assert.That(secondClose.IsCompleted).IsFalse();
        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsNull();
        await Assert.That(barrier.Release()).IsTrue();
        await Task.WhenAll(firstClose, secondClose);

        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task CloseAsync_BarrierCommitsExactCapturedOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic);
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId));

        var close = consumer.CloseAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 9));
        await Assert.That(barrier.Release()).IsTrue();
        await close;

        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task CloseAsync_GroupChangeDuringBarrierDoesNotOverwriteNewOwnerOffset()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic);
        var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            offsetCommitMode: OffsetCommitMode.Auto,
            memberId: "z-member");
        consumer.Subscribe(Topic);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));

        var staleClose = consumer.CloseAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        await using var newOwner = CreateConsumer(
            cluster,
            enableAutoOffsetStore: false,
            memberId: "a-member");
        newOwner.Subscribe(Topic);
        await newOwner.CommitAsync([new TopicPartitionOffset(Topic, 0, 9)]);
        await Assert.That(barrier.Release()).IsTrue();
        await staleClose;

        await Assert.That(cluster.GetCommittedOffset(GroupId, Partition)).IsEqualTo(9);
    }

    [Test]
    public async Task CommitAsync_SnapshotsLazyOffsetsBeforeFaultObserver()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        var offsets = new List<TopicPartitionOffset> { new(Topic, 0, 1) };
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        cluster.FaultPlan.FaultConsumed += _ =>
        {
            offsets[0] = new TopicPartitionOffset(Topic, 0, 9);
            barrier.Release();
        };

        await consumer.CommitAsync(offsets.Select(static offset => offset));

        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    public async Task CommitAsync_LazyOffsetsPreserveResourceRuleOrder()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync();
        var resourceFailure = new InvalidOperationException("resource failed");
        var groupFailure = new InvalidOperationException("group failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, Topic, 0, GroupId),
            resourceFailure);
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            groupFailure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync(YieldOffset(new TopicPartitionOffset(Topic, 0, 1))).AsTask());

        await Assert.That(actual).IsSameReferenceAs(resourceFailure);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);

        actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync(YieldOffset(new TopicPartitionOffset(Topic, 0, 1))).AsTask());

        await Assert.That(actual).IsSameReferenceAs(groupFailure);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(0);
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
    public async Task StoreOffsets_FaultObserverCannotMutateValidatedBatch()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync(enableAutoOffsetStore: false);
        TopicPartitionOffset[] offsets = [new(Topic, 0, 1)];
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.StoreOffset, Topic, 0, GroupId));
        cluster.FaultPlan.FaultConsumed += _ =>
        {
            offsets[0] = new TopicPartitionOffset(Topic, 0, 9);
            barrier.Release();
        };

        consumer.StoreOffsets(offsets);
        await consumer.CommitAsync();

        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
    }

    [Test]
    [Arguments("Single")]
    [Arguments("List")]
    [Arguments("Span")]
    public async Task StoreOffsets_FaultObserverClosePreventsDisposedMutationAndLaterFaultConsumption(
        string overload)
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync(enableAutoOffsetStore: false);
        var firstOffset = new TopicPartitionOffset(Topic, 0, 1);
        var secondOffset = new TopicPartitionOffset(Topic, 1, 2);
        var firstBarrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.StoreOffset, Topic, 0, GroupId));
        if (overload != "Single")
        {
            _ = cluster.FaultPlan.PauseNext(
                new KafkaFaultScope(KafkaFaultOperation.StoreOffset, Topic, 1, GroupId));
        }
        await Assert.That(firstBarrier.Release()).IsTrue();
        Task? closeTask = null;
        cluster.FaultPlan.FaultConsumed += _ => closeTask ??= consumer.CloseAsync().AsTask();
        Action store = overload switch
        {
            "Single" => () => consumer.StoreOffset(firstOffset),
            "List" => () => consumer.StoreOffsets(new[] { firstOffset, secondOffset }),
            "Span" => () => consumer.StoreOffsets(new[] { firstOffset, secondOffset }.AsSpan()),
            _ => throw new ArgumentOutOfRangeException(nameof(overload), overload, null)
        };

        _ = Assert.Throws<ObjectDisposedException>(store);

        await Assert.That(closeTask).IsNotNull();
        await closeTask!;
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(overload == "Single" ? 0 : 1);
    }

    [Test]
    [Arguments("Subscribe")]
    [Arguments("SubscribePattern")]
    [Arguments("Unsubscribe")]
    public async Task GroupTransition_AutoCommitBarrierPreservesQueuedFault(string transition)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic);
        cluster.CreateTopic("payments");
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var commitBarrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        var consume = consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask();
        await commitBarrier.WaitUntilEnteredAsync();
        var groupOperation = transition == "Unsubscribe"
            ? KafkaFaultOperation.Rebalance
            : KafkaFaultOperation.JoinGroup;
        var groupFailure = new InvalidOperationException("group transition failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(groupOperation, groupId: GroupId),
            groupFailure);
        Action transitionAction = transition switch
        {
            "Subscribe" => () => consumer.Subscribe("payments"),
            "SubscribePattern" => () => consumer.SubscribePattern("^payments$"),
            "Unsubscribe" => consumer.Unsubscribe,
            _ => throw new ArgumentOutOfRangeException(nameof(transition))
        };

        var pending = Assert.Throws<InvalidOperationException>(transitionAction);

        await Assert.That(pending.Message).Contains("automatic commit fault");
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);
        await Assert.That(commitBarrier.Release()).IsTrue();
        _ = await consume;

        var actual = Assert.Throws<InvalidOperationException>(transitionAction);

        await Assert.That(actual).IsSameReferenceAs(groupFailure);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(KafkaFaultOperation.JoinGroup)]
    [Arguments(KafkaFaultOperation.SyncGroup)]
    [Arguments(KafkaFaultOperation.Rebalance)]
    public async Task Subscribe_GroupTransitionObserverCannotCreateLateAutoCommitReservation(
        KafkaFaultOperation operation)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic);
        cluster.CreateTopic("payments");
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Topic, "key", "value");
        await using var consumer = CreateConsumer(
            cluster,
            enableAutoOffsetStore: true,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe(Topic);
        var commitBarrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId));
        var transitionBarrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(operation, groupId: GroupId));
        await Assert.That(transitionBarrier.Release()).IsTrue();
        Task<ConsumeResult<string, string>?>? consume = null;
        cluster.FaultPlan.FaultConsumed += observation =>
        {
            if (observation.OperationScope.Operation == operation)
                consume = consumer.ConsumeOneAsync(Timeout.InfiniteTimeSpan).AsTask();
        };

        var actual = Assert.Throws<InvalidOperationException>(() => consumer.Subscribe("payments"));

        await Assert.That(actual.Message).Contains("automatic commit fault");
        await Assert.That(consume is not null).IsTrue();
        await Assert.That(consumer.Subscription).IsEquivalentTo([Topic]);
        await Assert.That(consumer.Assignment).IsEquivalentTo([Partition]);
        await Assert.That(commitBarrier.Release()).IsTrue();
        await consume!;
    }

    [Test]
    public async Task Subscribe_GroupTransitionObserverCannotMutateTopicSnapshot()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic);
        cluster.CreateTopic("payments");
        await using var consumer = CreateConsumer(cluster);
        var topics = new[] { Topic };
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.JoinGroup, groupId: GroupId));
        await Assert.That(barrier.Release()).IsTrue();
        cluster.FaultPlan.FaultConsumed += observation =>
        {
            if (observation.OperationScope.Operation == KafkaFaultOperation.JoinGroup)
                topics[0] = "payments";
        };

        consumer.Subscribe(topics);

        await Assert.That(consumer.Subscription).IsEquivalentTo([Topic]);
        await Assert.That(consumer.Assignment).IsEquivalentTo([Partition]);
    }

    [Arguments("Subscribe")]
    [Arguments("SubscribePattern")]
    [Test]
    public async Task Subscribe_GroupTransitionObserverCloseCannotReregisterDisposedConsumer(
        string transition)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic);
        cluster.CreateTopic("payments");
        var consumer = CreateConsumer(cluster);
        consumer.Subscribe(Topic);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.JoinGroup, groupId: GroupId));
        await Assert.That(barrier.Release()).IsTrue();
        Task? closeTask = null;
        cluster.FaultPlan.FaultConsumed += _ => closeTask = consumer.CloseAsync().AsTask();
        Action subscribe = transition == "Subscribe"
            ? () => consumer.Subscribe("payments")
            : () => consumer.SubscribePattern("^payments$");

        _ = Assert.Throws<ObjectDisposedException>(subscribe);

        await Assert.That(closeTask).IsNotNull();
        await closeTask!;
        await Assert.That(consumer.Assignment).IsEmpty();
        await using var replacement = CreateConsumer(cluster);
        replacement.Subscribe("payments");
        await Assert.That(replacement.Assignment)
            .IsEquivalentTo([new TopicPartition("payments", 0)]);
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
        OffsetStoreTiming offsetStoreTiming = OffsetStoreTiming.OnDelivery,
        string groupId = GroupId,
        string? memberId = null) =>
        new(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = offsetCommitMode,
                EnableAutoOffsetStore = enableAutoOffsetStore,
                OffsetStoreTiming = offsetStoreTiming,
                MemberId = memberId
            });

    private static InMemoryConsumer<string, string> CreateAsyncConsumer(
        InMemoryKafkaCluster cluster,
        IAsyncDeserializer<string> deserializer,
        string? memberId = null) =>
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
                OffsetStoreTiming = OffsetStoreTiming.OnDelivery,
                MemberId = memberId
            });

    private static IEnumerable<TopicPartitionOffset> YieldOffset(TopicPartitionOffset offset)
    {
        yield return offset;
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

    private sealed class DelegatingFaultPlan(KafkaFaultPlan inner) : IKafkaFaultPlan
    {
        private Action? _countObserved;
        private Action? _matchingObserved;
        private Action? _commitProbeObserved;

        public int MatchingProbeCount { get; private set; }

        public int PotentialProbeCount { get; private set; }

        public event Action<KafkaFaultObservation>? FaultConsumed
        {
            add => inner.FaultConsumed += value;
            remove => inner.FaultConsumed -= value;
        }

        public int Count
        {
            get
            {
                Interlocked.Exchange(ref _countObserved, null)?.Invoke();
                return inner.Count;
            }
        }

        public long Version => inner.Version;

        public void ObserveNextCount(Action callback) =>
            _countObserved = callback;

        public void ObserveNextMatchingProbe(Action callback) =>
            _matchingObserved = callback;

        public void ObserveNextCommitProbe(Action callback) =>
            _commitProbeObserved = callback;

        public bool HasMatchingFault(in KafkaFaultScope operationScope)
        {
            Interlocked.Exchange(ref _matchingObserved, null)?.Invoke();
            MatchingProbeCount++;
            return inner.HasMatchingFault(operationScope);
        }

        public bool HasPotentialFault(
            KafkaFaultOperation operation,
            string? groupId,
            IReadOnlySet<TopicPartition> resources)
        {
            PotentialProbeCount++;
            return inner.HasPotentialFault(operation, groupId, resources);
        }

        public bool TryGetFirstMatchingFaultScope(
            ReadOnlySpan<KafkaFaultScope> operationScopes,
            out KafkaFaultScope operationScope)
        {
            Interlocked.Exchange(ref _commitProbeObserved, null)?.Invoke();
            Interlocked.Exchange(ref _matchingObserved, null)?.Invoke();
            return inner.TryGetFirstMatchingFaultScope(operationScopes, out operationScope);
        }

        public bool TryApplyFirstMatchingFault(
            ReadOnlySpan<KafkaFaultScope> operationScopes,
            out ValueTask application,
            CancellationToken cancellationToken = default) =>
            inner.TryApplyFirstMatchingFault(operationScopes, out application, cancellationToken);

        public void Fail(KafkaFaultScope scope, Exception exception, int occurrenceCount = 1) =>
            inner.Fail(scope, exception, occurrenceCount);

        public void FailPersistently(KafkaFaultScope scope, Exception exception) =>
            inner.FailPersistently(scope, exception);

        public KafkaFaultBarrier PauseNext(KafkaFaultScope scope) => inner.PauseNext(scope);

        public ValueTask ApplyAsync(
            KafkaFaultScope operationScope,
            CancellationToken cancellationToken = default) =>
            inner.ApplyAsync(operationScope, cancellationToken);

        public int Clear(KafkaFaultScope scope) => inner.Clear(scope);

        public int Clear() => inner.Clear();
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

    private sealed class AsyncStringDeserializer : IAsyncDeserializer<string>
    {
        public ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Encoding.UTF8.GetString(data.Span));
    }

    private sealed class CallbackOnceDeserializer(Action callback) : IDeserializer<string>
    {
        private int _invokeCallback = 1;

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            if (Interlocked.Exchange(ref _invokeCallback, 0) != 0)
                callback();

            return Encoding.UTF8.GetString(data.Span);
        }
    }

    private sealed class FirstCallBlockingAsyncDeserializer : IAsyncDeserializer<string>
    {
        private readonly TaskCompletionSource _entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _blockNext = 1;

        public async ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _blockNext, 0) != 0)
            {
                _entered.TrySetResult();
                await _release.Task.WaitAsync(cancellationToken);
            }

            return Encoding.UTF8.GetString(data.Span);
        }

        public Task WaitUntilEnteredAsync() => _entered.Task;

        public void Release() => _release.TrySetResult();
    }
}
