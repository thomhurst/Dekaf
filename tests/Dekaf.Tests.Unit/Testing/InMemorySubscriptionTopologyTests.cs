using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemorySubscriptionTopologyTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Expansion_DiscoversNewPartitionsAndPreservesPosition(bool typedExpansion)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var admin = new InMemoryAdminClient(cluster);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await using var consumer = CreateConsumer(cluster);
        consumer.Subscribe("orders");
        await Produce(producer, "orders", 0, "first");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Value).IsEqualTo("first");
        consumer.Pause(new TopicPartition("orders", 0));

        if (typedExpansion)
            await ((IAdminClient)admin).CreatePartitionsAsync(new Dictionary<string, NewPartitions> { ["orders"] = new() { TotalCount = 3 } });
        else
            await admin.CreatePartitionsAsync(new Dictionary<string, int> { ["orders"] = 3 });
        await Produce(producer, "orders", 1, "second");
        await Produce(producer, "orders", 2, "third");

        // Consume directly: assignment/fault caches must discover the topology without an Assignment query.
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Value).IsEqualTo("second");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Value).IsEqualTo("third");
        await Assert.That(consumer.Assignment).Count().IsEqualTo(3);
        await Assert.That(consumer.Paused.Contains(new TopicPartition("orders", 0))).IsTrue();
        consumer.Resume(new TopicPartition("orders", 0));
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingExplicitTopic_IsAcceptedWithoutAutoCreation(bool autoCreate)
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { AutoCreateTopics = autoCreate });
        await using var consumer = CreateConsumer(cluster);
        consumer.Subscribe("orders");
        await Assert.That(consumer.Subscription.Contains("orders")).IsTrue();
        await Assert.That(consumer.Assignment).IsEmpty();
        await Assert.That(cluster.ListTopics()).IsEmpty();
        cluster.CreateTopic("orders", 2);
        await Assert.That(consumer.Assignment).Count().IsEqualTo(2);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task DynamicSubscription_DiscoversLaterMatchingTopics(bool pattern, bool autoCreate)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var consumer = CreateConsumer(cluster);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        if (pattern)
            consumer.SubscribePattern("orders-.*");
        else
            consumer.Subscribe(static topic => topic.StartsWith("orders-", StringComparison.Ordinal));
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
        if (!autoCreate)
        {
            await using var admin = new InMemoryAdminClient(cluster);
            await admin.CreateTopicsAsync([new NewTopic { Name = "orders-eu", NumPartitions = 1, ReplicationFactor = 1 }]);
        }
        await Produce(producer, "orders-eu", 0, "match");
        await Produce(producer, "other-orders-eu", 0, "no match");
        var record = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.Value.Topic).IsEqualTo("orders-eu");
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
        await Assert.That(consumer.Assignment).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments(AutoOffsetReset.Earliest, false, 0L)]
    [Arguments(AutoOffsetReset.Latest, false, 2L)]
    [Arguments(AutoOffsetReset.Latest, true, 1L)]
    public async Task NewPartition_UsesResetOrCommittedOffset(AutoOffsetReset reset, bool committed, long expected)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var consumer = CreateConsumer(cluster, reset);
        consumer.Subscribe("orders");
        cluster.CreatePartitions("orders", 2);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await Produce(producer, "orders", 1, "zero");
        await Produce(producer, "orders", 1, "one");
        if (committed)
            cluster.CommitOffsets("group", (IEnumerable<TopicPartitionOffset>)[new("orders", 1, 1)]);
        await Assert.That(consumer.Assignment).Count().IsEqualTo(2);
        await Assert.That(consumer.GetPosition(new TopicPartition("orders", 1))).IsEqualTo(expected);
    }

    [Test]
    public async Task ExpandedGroup_AssignsEveryPartitionExactlyOnce()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var first = CreateConsumer(cluster);
        await using var second = CreateConsumer(cluster);
        first.Subscribe("orders");
        second.Subscribe("orders");
        cluster.CreatePartitions("orders", 4);
        _ = first.Assignment;
        var secondAssignment = second.Assignment;
        var firstAssignment = first.Assignment;
        await Assert.That(firstAssignment.Count + secondAssignment.Count).IsEqualTo(4);
        await Assert.That(firstAssignment.Overlaps(secondAssignment)).IsFalse();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        for (var partition = 0; partition < 4; partition++)
            await Produce(producer, "orders", partition, partition.ToString());
        var received = new HashSet<int>();
        foreach (var consumer in new[] { first, second })
        {
            while (await consumer.ConsumeOneAsync(TimeSpan.Zero) is { } record)
                await Assert.That(received.Add(record.Partition)).IsTrue();
        }
        await Assert.That(received).Count().IsEqualTo(4);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DeletedAndRecreatedTopic_DiscardsOldPosition(bool observeDeletion)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var consumer = CreateConsumer(cluster);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        consumer.Subscribe("orders");
        await Produce(producer, "orders", 0, "old");
        _ = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        cluster.DeleteTopic("orders");
        if (observeDeletion)
            await Assert.That(consumer.Assignment).IsEmpty();
        cluster.CreateTopic("orders");
        await Produce(producer, "orders", 0, "new");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Value).IsEqualTo("new");
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, true, false)]
    public async Task RecreatedTopic_PendingAutoCommitDoesNotDeliverOldRecord(
        bool observeDeletion, bool observeRecreation, bool resume)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await using var consumer = new InMemoryConsumer<string, string>(cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Auto,
                OffsetStoreTiming = OffsetStoreTiming.OnDelivery
            });
        consumer.Subscribe("orders");
        await Produce(producer, "orders", 0, "old");
        var partition = new TopicPartition("orders", 0);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: "group"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consume = consumer.ConsumeOneAsync(TimeSpan.Zero, cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync(cancellation.Token);
        try
        {
            cluster.DeleteTopic("orders");
            if (observeDeletion)
                await Assert.That(consumer.Assignment).IsEmpty();
            cluster.CreateTopic("orders");
            await Produce(producer, "orders", 0, "new");
            if (observeRecreation)
            {
                await Assert.That(consumer.Assignment).Contains(partition);
                await Assert.That(consumer.GetPosition(partition)).IsEqualTo(0);
                if (resume)
                    consumer.Resume(partition);
                // A concurrent poll must not take over the invalidated reservation before its owner completes.
                await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero, cancellation.Token)).IsNull();
            }
        }
        finally
        {
            barrier.Release();
        }

        var record = await consume;
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.Value.Value).IsEqualTo("new");
        await Assert.That(record.Value.Offset).IsEqualTo(0);
        await Assert.That(cluster.GetCommittedOffset("group", partition)).IsEqualTo(1);
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
    }

    [Test]
    public async Task UnrelatedTopicCreation_PreservesPendingAutoCommit()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await using var consumer = new InMemoryConsumer<string, string>(cluster,
            new InMemoryConsumerOptions
            {
                GroupId = "group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                OffsetCommitMode = OffsetCommitMode.Auto,
                OffsetStoreTiming = OffsetStoreTiming.OnDelivery
            });
        consumer.Subscribe("orders");
        await Produce(producer, "orders", 0, "old");
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Commit, groupId: "group"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consume = consumer.ConsumeOneAsync(TimeSpan.Zero, cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync(cancellation.Token);
        try
        {
            cluster.CreateTopic("unrelated");
            await Assert.That(consumer.Assignment).Count().IsEqualTo(1);
        }
        finally
        {
            barrier.Release();
        }

        await Assert.That((await consume)!.Value.Value).IsEqualTo("old");
        await Assert.That(cluster.GetCommittedOffset("group", new TopicPartition("orders", 0))).IsEqualTo(1);
    }

    [Test]
    [Arguments("assign")]
    [Arguments("incremental")]
    [Arguments("unsubscribe")]
    [Arguments("unsubscribe-unassign")]
    public async Task LeavingSubscription_DisablesTopologyRefresh(string operation)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        cluster.CreateTopic("manual");
        await using var consumer = CreateConsumer(cluster);
        consumer.Subscribe(static _ => true);
        switch (operation)
        {
            case "assign": consumer.Assign(new TopicPartition("manual", 0)); break;
            case "incremental":
                consumer.IncrementalAssign([new TopicPartitionOffset("manual", 0, 0)]);
                break;
            case "unsubscribe": consumer.Unsubscribe(); break;
            case "unsubscribe-unassign":
                consumer.Unsubscribe();
                consumer.Unassign();
                break;
        }
        var before = consumer.Assignment;
        cluster.CreateTopic("later");
        cluster.CreatePartitions("orders", 2);
        await Assert.That(consumer.Assignment.SetEquals(before)).IsTrue();
    }

    [Test]
    public async Task RemovedMember_TopologyRefreshDoesNotRejoin()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var consumer = new InMemoryConsumer<string, string>(cluster,
            new InMemoryConsumerOptions { GroupId = "group", MemberId = "member", AutoOffsetReset = AutoOffsetReset.Earliest });
        consumer.Subscribe("orders");
        _ = cluster.RemoveConsumerGroupMembers("group", [new() { MemberId = "member" }]);
        cluster.CreatePartitions("orders", 2);
        await Assert.That(consumer.Assignment).IsEmpty();
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group")).IsEmpty();
    }

    [Test]
    public async Task ExpandedPartition_AppliesPreviouslyUnmatchedFault()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using var consumer = CreateConsumer(cluster);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        consumer.Subscribe("orders");
        var failure = new InvalidOperationException("expanded fetch");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Fetch, topic: "orders", partition: 1), failure);
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
        cluster.CreatePartitions("orders", 2);
        await Produce(producer, "orders", 1, "new");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());
        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task FilterWithoutGroup_DiscoversNewTopics()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var consumer = new InMemoryConsumer<string, string>(cluster,
            new InMemoryConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        consumer.Subscribe(static topic => topic == "orders");
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await Produce(producer, "orders", 0, "new");
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Value).IsEqualTo("new");
        await Assert.That(consumer.Subscription.Contains("orders")).IsTrue();
    }

    [Test]
    public async Task NewPartition_WithoutResetFailsOnConsumeAndRecoversAfterResubscription()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        cluster.CommitOffsets("group", (IEnumerable<TopicPartitionOffset>)[new("orders", 0, 0)]);
        await using var consumer = CreateConsumer(cluster, AutoOffsetReset.None);
        consumer.Subscribe("orders");
        cluster.CreatePartitions("orders", 2);
        var addedPartition = new TopicPartition("orders", 1);
        await Assert.That(consumer.Assignment).Count().IsEqualTo(2);
        await Assert.That(consumer.GetPosition(addedPartition)).IsNull();
        var failure = await Assert.ThrowsAsync<KafkaException>(() => consumer.ConsumeOneAsync(TimeSpan.Zero).AsTask());
        await Assert.That(failure!.ErrorCode).IsEqualTo(ErrorCode.OffsetOutOfRange);
        await Assert.That(consumer.GetPosition(addedPartition)).IsNull();

        cluster.CommitOffsets("group", (IEnumerable<TopicPartitionOffset>)[new("orders", 1, 0)]);
        consumer.Subscribe("orders");
        await Assert.That(consumer.Assignment).Count().IsEqualTo(2);
        await Assert.That(consumer.GetPosition(addedPartition)).IsEqualTo(0);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await Produce(producer, "orders", 1, "new");
        var record = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        await Assert.That(record!.Value.Partition).IsEqualTo(1);
        await Assert.That(record.Value.Value).IsEqualTo("new");
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
    }

    private static InMemoryConsumer<string, string> CreateConsumer(InMemoryKafkaCluster cluster, AutoOffsetReset reset = AutoOffsetReset.Earliest) =>
        new(cluster, new InMemoryConsumerOptions { GroupId = "group", AutoOffsetReset = reset, OffsetCommitMode = OffsetCommitMode.Manual });

    private static ValueTask<RecordMetadata> Produce(InMemoryProducer<string, string> producer, string topic, int partition, string value) =>
        producer.ProduceAsync(new ProducerMessage<string, string> { Topic = topic, Partition = partition, Key = "key", Value = value });
}
