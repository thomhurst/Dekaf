using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryConsumerReassignmentTests
{
    [Test]
    [Arguments(OffsetCommitMode.Auto, false, false)]
    [Arguments(OffsetCommitMode.Auto, true, false)]
    [Arguments(OffsetCommitMode.Manual, false, false)]
    [Arguments(OffsetCommitMode.Manual, true, false)]
    [Arguments(OffsetCommitMode.Auto, false, true)]
    [Arguments(OffsetCommitMode.Auto, true, true)]
    [Arguments(OffsetCommitMode.Manual, false, true)]
    [Arguments(OffsetCommitMode.Manual, true, true)]
    public async Task Reassignment_ResumesAtNewOwnersCommitWithoutRegressingOffsets(
        OffsetCommitMode mode, bool observeRevocation, bool manualAssignment)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await Produce(producer, 1, "first");
        await Produce(producer, 1, "second");
        await Produce(producer, 1, "third");
        await using var first = CreateConsumer(cluster, "a", mode);
        if (manualAssignment)
            first.Assign(new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        else
            first.Subscribe("topic");
        await Assert.That((await first.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(0);
        await first.CommitAsync();
        // Leave a dirty stored offset behind as well as the old delivery position.
        first.StoreOffset(new TopicPartitionOffset("topic", 1, 1));
        var moved = new TopicPartition("topic", 1);
        first.Pause(moved);
        await using var second = CreateConsumer(cluster, "b", mode);
        if (manualAssignment)
            second.Assign(new TopicPartition("topic", 0), new TopicPartition("topic", 1));
        else
            second.Subscribe("topic");
        if (observeRevocation)
        {
            await Assert.That(first.Assignment.Contains(moved)).IsFalse();
            await Assert.That(first.GetPosition(moved)).IsNull();
            await Assert.That(first.Paused.Contains(moved)).IsFalse();
        }
        await Assert.That((await second.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(1);
        await second.CommitAsync();
        if (manualAssignment)
            second.Unassign();
        else
            second.Unsubscribe();

        // No Assignment read here: consumption must discover even an unobserved loss and regain.
        await Produce(producer, 0, "unrelated");
        await Assert.That((await first.ConsumeOneAsync(TimeSpan.Zero))!.Value.Partition).IsEqualTo(0);
        await first.CommitAsync();
        await Assert.That(cluster.GetCommittedOffset("group", moved)).IsEqualTo(2);
        var resumed = await first.ConsumeOneAsync(TimeSpan.Zero);
        await Assert.That(resumed!.Value.Partition).IsEqualTo(1);
        await Assert.That(resumed.Value.Offset).IsEqualTo(2);
    }

    [Test]
    public async Task ScaleOut_DoesNotSeedUnownedPartitions_AndPreservesRetainedProgress()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await Produce(producer, 0, "first");
        await Produce(producer, 0, "second");
        await using var first = CreateConsumer(cluster, "a", OffsetCommitMode.Manual);
        first.Subscribe("topic");
        _ = await first.ConsumeOneAsync(TimeSpan.Zero);
        await using var second = CreateConsumer(cluster, "b", OffsetCommitMode.Manual);
        second.Subscribe("topic");
        await Assert.That(second.GetPosition(new TopicPartition("topic", 0))).IsNull();
        await Assert.That((await first.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(1);
        await first.CommitAsync();
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 0))).IsEqualTo(2);
    }

    [Test]
    [Arguments(OffsetStoreTiming.OnDelivery)]
    [Arguments(OffsetStoreTiming.AfterProcessing)]
    public async Task SuccessfulAutoCommit_DoesNotRecommitCleanOffsets(OffsetStoreTiming timing)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await Produce(producer, 0, "first");
        await using var consumer = CreateConsumer(cluster, "a", OffsetCommitMode.Auto, timing);
        consumer.Subscribe("topic");
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        // AfterProcessing proves the first record on this empty poll.
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        var firstPartition = new TopicPartition("topic", 0);
        await Assert.That(cluster.GetCommittedOffset("group", firstPartition)).IsEqualTo(1);
        await consumer.CommitAsync([new("topic", 0, 100)]);
        await Produce(producer, 1, "other");
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        await Assert.That(cluster.GetCommittedOffset("group", firstPartition)).IsEqualTo(100);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SuccessfulExplicitCommit_ClearsOnlyMatchingStoredOffsets(bool explicitOffsets)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var consumer = CreateConsumer(cluster, "a", OffsetCommitMode.Manual);
        consumer.Subscribe("topic");
        consumer.StoreOffset(new TopicPartitionOffset("topic", 0, 5));
        if (explicitOffsets)
            await consumer.CommitAsync([new("topic", 0, 5)]);
        else
            await consumer.CommitAsync();
        cluster.CommitOffsets("group", (IEnumerable<TopicPartitionOffset>)[new("topic", 0, 100)]);
        consumer.StoreOffset(new TopicPartitionOffset("topic", 1, 7));
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 0))).IsEqualTo(100);
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 1))).IsEqualTo(7);

        consumer.StoreOffset(new TopicPartitionOffset("topic", 0, 101));
        await consumer.CommitAsync([new("topic", 0, 100)]);
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 0))).IsEqualTo(101);
    }

    [Test]
    public async Task RevokedInDoubtRecord_AndStoresWithoutOwnership_CannotCommitAfterReassignment()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await Produce(producer, 1, "unproven");
        await using var first = CreateConsumer(cluster, "a", OffsetCommitMode.Manual, OffsetStoreTiming.AfterProcessing);
        first.Subscribe("topic");
        _ = await first.ConsumeOneAsync(TimeSpan.Zero);
        await using var second = CreateConsumer(cluster, "b", OffsetCommitMode.Manual);
        second.Subscribe("topic");
        await first.CommitAsync();
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 1))).IsNull();
        first.StoreOffset(new TopicPartitionOffset("topic", 1, 99));
        await second.CommitAsync([new("topic", 1, 1)]);
        second.Unsubscribe();
        await first.CommitAsync();
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 1))).IsEqualTo(1);
    }

    [Test]
    [Arguments(AutoOffsetReset.Earliest, 0L)]
    [Arguments(AutoOffsetReset.Latest, 2L)]
    public async Task NewlyOwnedPartition_ResolvesResetAtAssignment(AutoOffsetReset reset, long expected)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var first = new InMemoryConsumer<string, string>(cluster, new InMemoryConsumerOptions
        {
            GroupId = "group", MemberId = "a", AutoOffsetReset = reset, OffsetCommitMode = OffsetCommitMode.Manual
        });
        first.Subscribe("topic");
        await using var second = CreateConsumer(cluster, "b", OffsetCommitMode.Manual);
        second.Subscribe("topic");
        _ = first.Assignment;
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await Produce(producer, 1, "first");
        await Produce(producer, 1, "second");
        second.Unsubscribe();
        _ = first.Assignment;
        await Assert.That(first.GetPosition(new("topic", 1))).IsEqualTo(expected);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CommitInFlight_PreservesChangedStoredOffset(bool explicitOffsets)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic");
        await using var consumer = CreateConsumer(cluster, "a", OffsetCommitMode.Manual);
        consumer.Subscribe("topic");
        consumer.StoreOffset(new TopicPartitionOffset("topic", 0, 1));
        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: "group"));
        var commit = (explicitOffsets ? consumer.CommitAsync([new("topic", 0, 1)]) : consumer.CommitAsync()).AsTask();
        await barrier.WaitUntilEnteredAsync();
        consumer.StoreOffset(new TopicPartitionOffset("topic", 0, 2));
        await Assert.That(barrier.Release()).IsTrue();
        await commit;
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 0))).IsEqualTo(1);
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 0))).IsEqualTo(2);
    }

    [Test]
    [Arguments(OffsetCommitMode.Auto)]
    [Arguments(OffsetCommitMode.Manual)]
    public async Task UnobservedReassignment_DiscardsInDoubtProofBeforeNextPoll(OffsetCommitMode mode)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await Produce(producer, 1, "first");
        await Produce(producer, 1, "second");
        await using var first = CreateConsumer(cluster, "a", mode, OffsetStoreTiming.AfterProcessing);
        first.Subscribe("topic");
        _ = await first.ConsumeOneAsync(TimeSpan.Zero);
        await using var second = CreateConsumer(cluster, "b", mode);
        second.Subscribe("topic");
        _ = await second.ConsumeOneAsync(TimeSpan.Zero);
        _ = await second.ConsumeOneAsync(TimeSpan.Zero);
        await second.CommitAsync();
        second.Unsubscribe();
        await Produce(producer, 0, "other");
        _ = await first.ConsumeOneAsync(TimeSpan.Zero);
        await first.CommitAsync();
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 1))).IsEqualTo(2);
    }

    [Test]
    public async Task ManualAssignment_PreservesStoresAndPositionsDuringIncrementalAssignment()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var consumer = CreateConsumer(cluster, "a", OffsetCommitMode.Manual);
        consumer.Assign(new TopicPartition("topic", 0));
        consumer.StoreOffset(new TopicPartitionOffset("topic", 0, 5));
        consumer.StoreOffset(new TopicPartitionOffset("topic", 1, 7));
        consumer.Seek(new TopicPartitionOffset("topic", 0, 4));
        consumer.StoreOffset(new TopicPartitionOffset("topic", 0, 5));
        consumer.IncrementalAssign([new TopicPartitionOffset("topic", 1, 6)]);
        await Assert.That(consumer.GetPosition(new("topic", 0))).IsEqualTo(4);
        await Assert.That(consumer.GetPosition(new("topic", 1))).IsEqualTo(6);
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 0))).IsEqualTo(5);
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 1))).IsEqualTo(7);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ManualIncrementalAssignment_PreservesRetainedPauseAndInDoubtRecord(bool unassign)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await Produce(producer, 0, "first");
        await using var consumer = CreateConsumer(cluster, "a", OffsetCommitMode.Manual, OffsetStoreTiming.AfterProcessing);
        consumer.Assign(unassign ? [new("topic", 0), new("topic", 1)] : [new("topic", 0)]);
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        consumer.Pause(new TopicPartition("topic", 0));
        if (unassign)
            consumer.IncrementalUnassign([new("topic", 1)]);
        else
            consumer.IncrementalAssign([new TopicPartitionOffset("topic", 1, 0)]);
        await Assert.That(consumer.GetPosition(new("topic", 0))).IsEqualTo(1);
        await Assert.That(consumer.Paused.Contains(new("topic", 0))).IsTrue();
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 0))).IsEqualTo(1);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task CommitInFlight_PreservesChangedCheckpointDetails(bool explicitOffsets, bool changeMetadata)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic");
        await using var consumer = CreateConsumer(cluster, "a", OffsetCommitMode.Manual);
        consumer.Subscribe("topic");
        var original = new TopicPartitionOffset("topic", 0, 5, 1) { Metadata = "original" };
        var newer = changeMetadata ? original with { Metadata = "newer" } : original with { LeaderEpoch = 2 };
        consumer.StoreOffset(original);
        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: "group"));
        var commit = (explicitOffsets ? consumer.CommitAsync([original]) : consumer.CommitAsync()).AsTask();
        await barrier.WaitUntilEnteredAsync();
        consumer.StoreOffset(newer);
        await Assert.That(barrier.Release()).IsTrue();
        await commit;
        var partition = new TopicPartition("topic", 0);
        var firstCommit = await consumer.GetCommittedOffsetsAsync([partition]);
        await Assert.That(firstCommit[partition]).IsEqualTo(original);
        await consumer.CommitAsync();
        var secondCommit = await consumer.GetCommittedOffsetsAsync([partition]);
        await Assert.That(secondCommit[partition]).IsEqualTo(newer);
    }

    private static InMemoryConsumer<string, string> CreateConsumer(
        InMemoryKafkaCluster cluster, string member, OffsetCommitMode mode,
        OffsetStoreTiming timing = OffsetStoreTiming.OnDelivery) => new(cluster, new InMemoryConsumerOptions
        {
            GroupId = "group", MemberId = member, AutoOffsetReset = AutoOffsetReset.Earliest,
            OffsetCommitMode = mode, OffsetStoreTiming = timing
        });

    private static ValueTask<RecordMetadata> Produce(InMemoryProducer<string, string> producer, int partition, string value) =>
        producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "topic", Partition = partition, Key = "key", Value = value
        });
}
