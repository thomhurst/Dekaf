using Dekaf.Consumer;
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
    public async Task CommitAsync_FailurePreservesStoredOffsetForRetry()
    {
        var (cluster, consumer) = await CreateConsumerWithRecordAsync(enableAutoOffsetStore: false);
        consumer.StoreOffset(new TopicPartitionOffset(Topic, 0, 1));
        var failure = new InvalidOperationException("commit failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: GroupId),
            failure);

        var actual = await Assert.ThrowsAsync<Exception>(() => consumer.CommitAsync().AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsNull();

        await consumer.CommitAsync();

        await Assert.That(await consumer.GetCommittedOffsetAsync(Partition)).IsEqualTo(1);
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
        bool enableAutoOffsetStore = true) =>
        new(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Manual,
                EnableAutoOffsetStore = enableAutoOffsetStore,
                OffsetStoreTiming = OffsetStoreTiming.OnDelivery
            });
}
