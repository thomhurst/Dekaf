using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.ShareConsumer;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryShareRecordStateTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Members_SkipAcquiredRecordsAndPreserveEarlierGap(bool withFault)
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceRecords(cluster, 3);
        if (withFault)
            for (var index = 0; index < 3; index++)
                cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.ShareConsume)).Release();
        await using var first = CreateConsumer(cluster);
        await using var second = CreateConsumer(cluster);
        var firstRecord = await first.PollAsync().FirstAsync();
        var secondRecords = await second.PollAsync().ToArrayAsync();

        await Assert.That(firstRecord.Offset).IsEqualTo(0);
        await Assert.That(secondRecords.Select(record => record.Offset).ToArray()).IsEquivalentTo([1L, 2L]);
        await second.CommitAsync();
        await Assert.That(cluster.GetCommittedShareOffset("workers", new TopicPartition("shared", 0))).IsNull();
        first.Acknowledge(firstRecord, AcknowledgeType.Release);
        await first.CommitAsync();
        var redelivered = await second.PollAsync().ToArrayAsync();
        await Assert.That(redelivered.Select(record => record.Offset).ToArray()).IsEquivalentTo([0L]);
        await Assert.That(redelivered[0].DeliveryCount).IsEqualTo(2);
        await second.CommitAsync();
        await Assert.That(cluster.GetCommittedShareOffset("workers", new TopicPartition("shared", 0))).IsEqualTo(3);
    }

    [Test]
    [Arguments(AcknowledgeType.Accept)]
    [Arguments(AcknowledgeType.Reject)]
    public async Task Commit_ArchivesAcknowledgedRecordAfterReleaseGap(AcknowledgeType acknowledgement)
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceRecords(cluster, 3);
        await using var consumer = CreateConsumer(cluster);
        var records = await consumer.PollAsync().ToArrayAsync();
        consumer.Acknowledge(records[0], AcknowledgeType.Accept);
        consumer.Acknowledge(records[1], AcknowledgeType.Release);
        consumer.Acknowledge(records[2], acknowledgement);
        await consumer.CommitAsync();

        await Assert.That(cluster.GetCommittedShareOffset("workers", new TopicPartition("shared", 0))).IsEqualTo(1);
        var redelivered = await consumer.PollAsync().ToArrayAsync();
        await Assert.That(redelivered.Select(record => record.Offset).ToArray()).IsEquivalentTo([1L]);
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedShareOffset("workers", new TopicPartition("shared", 0))).IsEqualTo(3);
        await Assert.That(await consumer.PollAsync().ToArrayAsync()).IsEmpty();
    }

    [Test]
    public async Task Release_ArchivesAfterFiveDeliveriesByDefault()
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceRecords(cluster, 1);
        await using var consumer = CreateConsumer(cluster);
        for (var delivery = 1; delivery <= 5; delivery++)
        {
            var record = await consumer.PollAsync().FirstAsync();
            await Assert.That(record.DeliveryCount).IsEqualTo(delivery);
            consumer.Acknowledge(record, AcknowledgeType.Release);
            await consumer.CommitAsync();
        }
        await Assert.That(await consumer.PollAsync().ToArrayAsync()).IsEmpty();
        await Assert.That(cluster.GetCommittedShareOffset("workers", new TopicPartition("shared", 0))).IsEqualTo(1);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task Constructor_RejectsInvalidDeliveryCountLimit(int limit)
    {
        await Assert.That(() => new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { DeliveryCountLimit = limit }))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task DeliveryCountLimit_IsSharedAcrossMembersAndCloseReleases()
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { DeliveryCountLimit = 2 });
        await ProduceRecords(cluster, 1);
        await using (var first = CreateConsumer(cluster))
        {
            var record = await first.PollAsync().FirstAsync();
            await Assert.That(record.DeliveryCount).IsEqualTo(1);
        }
        await using (var second = CreateConsumer(cluster))
        {
            var record = await second.PollAsync().FirstAsync();
            await Assert.That(record.DeliveryCount).IsEqualTo(2);
            // Close implicitly releases this unacknowledged delivery at the limit.
        }
        await using var third = CreateConsumer(cluster);
        await Assert.That(await third.PollAsync().ToArrayAsync()).IsEmpty();
        await Assert.That(cluster.GetCommittedShareOffset("workers", new TopicPartition("shared", 0))).IsEqualTo(1);
    }

    [Test]
    public async Task FailedFaultAcquisition_DoesNotSpendFinalDelivery()
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { DeliveryCountLimit = 1 });
        await ProduceRecords(cluster, 1);
        await using var consumer = CreateConsumer(cluster);
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.ShareConsume), new InvalidOperationException("delivery failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await consumer.PollAsync().FirstAsync());
        var record = await consumer.PollAsync().FirstAsync();
        await Assert.That(record.DeliveryCount).IsEqualTo(1);
        consumer.Acknowledge(record, AcknowledgeType.Release);
        await consumer.CommitAsync();
        await Assert.That(await consumer.PollAsync().ToArrayAsync()).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InactiveLease_CanBeAcquiredByDifferentMemberUnlessAtLimit(bool atLimit)
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { DeliveryCountLimit = atLimit ? 1 : 2 });
        await ProduceRecords(cluster, 1);
        var partition = new TopicPartition("shared", 0);
        var first = cluster.RegisterShareGroupMember("workers", "first");
        await Assert.That(cluster.TryAcquireShareRecord("workers", "first", first, partition, 0, out _, out _)).IsTrue();
        cluster.UnregisterShareGroupMember("workers", "first", first);
        var second = cluster.RegisterShareGroupMember("workers", "second");
        var acquired = cluster.TryAcquireShareRecord("workers", "second", second, partition, 0, out _, out var count);
        await Assert.That(acquired).IsEqualTo(!atLimit);
        if (atLimit)
            await Assert.That(cluster.GetCommittedShareOffset("workers", partition)).IsEqualTo(1);
        else
            await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task StaleCompletion_DoesNotArchiveReplacementLease()
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceRecords(cluster, 1);
        var partition = new TopicPartition("shared", 0);
        var first = cluster.RegisterShareGroupMember("workers", "first");
        cluster.TryAcquireShareRecord("workers", "first", first, partition, 0, out _, out _);
        cluster.UnregisterShareGroupMember("workers", "first", first);
        var second = cluster.RegisterShareGroupMember("workers", "second");
        cluster.TryAcquireShareRecord("workers", "second", second, partition, 0, out _, out _);
        cluster.CompleteShareRecords("workers", "first", first,
            [new InMemoryShareRecordCompletion(new TopicPartitionOffset("shared", 0, 0), AcknowledgeType.Accept)], 1);
        await Assert.That(cluster.GetCommittedShareOffset("workers", partition)).IsNull();
        cluster.ReleaseShareRecords("workers", "second", second, [new TopicPartitionOffset("shared", 0, 0)]);
        await Assert.That(cluster.TryAcquireShareRecord("workers", "second", second, partition, 0, out _, out var count)).IsTrue();
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task RejectedRecordAfterGap_IsDeadLetteredOnce()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("dead-letters", configs: new Dictionary<string, string>
        {
            ["errors.deadletterqueue.group.enable"] = "true"
        });
        await ProduceRecords(cluster, 2);
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers", DeadLetterQueueTopic = "dead-letters" });
        consumer.Subscribe("shared");
        var records = await consumer.PollAsync().ToArrayAsync();
        consumer.Acknowledge(records[0], AcknowledgeType.Release);
        consumer.Acknowledge(records[1], AcknowledgeType.Reject);
        await consumer.CommitAsync();
        var redelivered = await consumer.PollAsync().ToArrayAsync();
        await Assert.That(redelivered.Select(record => record.Offset).ToArray()).IsEquivalentTo([0L]);
        await consumer.CommitAsync();
        await Assert.That(cluster.ReadRecords("dead-letters").Count).IsEqualTo(1);
    }

    [Test]
    public async Task InactiveLeaseAtLimit_SkipsArchivedPrefixAfterStartAdvances()
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { DeliveryCountLimit = 1 });
        await ProduceRecords(cluster, 3);
        var partition = new TopicPartition("shared", 0);
        var first = cluster.RegisterShareGroupMember("workers", "first");
        cluster.TryAcquireShareRecord("workers", "first", first, partition, 0, out _, out _);
        var second = cluster.RegisterShareGroupMember("workers", "second");
        cluster.TryAcquireShareRecord("workers", "second", second, partition, 0, out var acknowledged, out _);
        await Assert.That(acknowledged.Offset).IsEqualTo(1);
        cluster.CompleteShareRecords("workers", "second", second,
            [new InMemoryShareRecordCompletion(new TopicPartitionOffset("shared", 0, 1), AcknowledgeType.Accept)], 1);
        cluster.UnregisterShareGroupMember("workers", "first", first);

        var acquired = cluster.TryAcquireShareRecord("workers", "second", second, partition, 0, out var next, out var count);
        await Assert.That(acquired).IsTrue();
        await Assert.That(next.Offset).IsEqualTo(2);
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(cluster.GetCommittedShareOffset("workers", partition)).IsEqualTo(2);
    }

    [Test]
    public async Task Commit_AfterTopicDeletionReleasesRecordState()
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceRecords(cluster, 1);
        await using var consumer = CreateConsumer(cluster);
        var record = await consumer.PollAsync().FirstAsync();
        consumer.Acknowledge(record);
        cluster.DeleteTopic("shared");
        await consumer.CommitAsync();
        await consumer.CloseAsync();
        var admin = new InMemoryAdminClient(cluster);
        var deleted = await admin.DeleteShareGroupsAsync(["workers"]);
        await Assert.That(deleted["workers"].ErrorCode).IsEqualTo(Dekaf.Protocol.ErrorCode.None);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task AdminOffsetMutation_ClearsArchivedRecordsAndDeliveryCounts(bool delete, bool detailed)
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceRecords(cluster, 2);
        await LeaveAcknowledgementGap(cluster);
        await using var admin = new InMemoryAdminClient(cluster);
        Dekaf.Admin.ShareGroupOffsetAlteration[] offsets =
            [new() { TopicPartition = new("shared", 0), StartOffset = 0 }];
        if (delete)
        {
            if (detailed)
                await admin.DeleteShareGroupOffsetsDetailedAsync("workers", ["shared"]);
            else
                await admin.DeleteShareGroupOffsetsAsync("workers", ["shared"]);
        }
        else if (detailed)
            await admin.AlterShareGroupOffsetsDetailedAsync("workers", offsets);
        else
            await admin.AlterShareGroupOffsetsAsync("workers", offsets);

        await using var replacement = CreateConsumer(cluster);
        var replayed = await replacement.PollAsync().ToArrayAsync();
        await Assert.That(replayed.Select(record => record.Offset).ToArray()).IsEquivalentTo([0L, 1L]);
        await Assert.That(replayed.Select(record => record.DeliveryCount).ToArray()).IsEquivalentTo([1, 1]);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task RecreatedTopic_DoesNotInheritShareState(bool deleteById, bool archiveAll)
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceRecords(cluster, 2);
        if (archiveAll)
        {
            await using var original = CreateConsumer(cluster);
            await original.PollAsync().ToArrayAsync();
            await original.CommitAsync();
        }
        else
            await LeaveAcknowledgementGap(cluster);
        if (deleteById)
            cluster.DeleteTopic(cluster.DescribeTopics(["shared"])["shared"].TopicId);
        else
            cluster.DeleteTopic("shared");
        await ProduceRecords(cluster, 2);
        await using var replacement = CreateConsumer(cluster);
        var replayed = await replacement.PollAsync().ToArrayAsync();
        await Assert.That(replayed.Select(record => record.Offset).ToArray()).IsEquivalentTo([0L, 1L]);
        await Assert.That(replayed.Select(record => record.DeliveryCount).ToArray()).IsEquivalentTo([1, 1]);
    }

    [Test]
    public async Task Commit_AdvancesEachPartitionAcrossUnorderedAcknowledgements()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("shared", 2);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        var registration = cluster.RegisterShareGroupMember("workers", "member");
        var completions = new List<InMemoryShareRecordCompletion>();
        for (var partition = 0; partition < 2; partition++)
        {
            var topicPartition = new TopicPartition("shared", partition);
            for (var offset = 0; offset < 3; offset++)
            {
                await producer.ProduceAsync(new Dekaf.Producer.ProducerMessage<string, string>
                    { Topic = "shared", Partition = partition, Key = "key", Value = "value" });
                cluster.TryAcquireShareRecord("workers", "member", registration, topicPartition, offset, out _, out _);
                completions.Add(new(new("shared", partition, offset), AcknowledgeType.Accept));
            }
        }
        completions.Reverse();
        cluster.CompleteShareRecords("workers", "member", registration, completions.ToArray(), completions.Count);
        await Assert.That(cluster.GetCommittedShareOffset("workers", new("shared", 0))).IsEqualTo(3);
        await Assert.That(cluster.GetCommittedShareOffset("workers", new("shared", 1))).IsEqualTo(3);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task LegacyAdminOffsetMutation_RejectsActiveGroupAndPreservesLease(bool delete, bool joinsDuringMutation)
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceRecords(cluster, 2);
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterShareGroupOffsetsAsync("workers",
            [new() { TopicPartition = new("shared", 0), StartOffset = 1 }]);
        var barrier = joinsDuringMutation
            ? cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin, topic: "shared", groupId: "workers"))
            : null;
        Task? pending = null;
        try
        {
            if (barrier is not null)
            {
                pending = MutateAsync();
                await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            }

            await using var first = CreateConsumer(cluster);
            var acquired = await first.PollAsync().FirstAsync();
            await Assert.That(acquired.Offset).IsEqualTo(1);
            await Assert.That(acquired.DeliveryCount).IsEqualTo(1);
            pending ??= MutateAsync();
            barrier?.Release();
            var exception = await Assert.ThrowsAsync<KafkaException>(() => pending);
            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
            await Assert.That(cluster.GetCommittedShareOffset("workers", new("shared", 0))).IsEqualTo(1);

            await using var second = CreateConsumer(cluster);
            await Assert.That(await second.PollAsync().ToArrayAsync()).IsEmpty();
            first.Acknowledge(acquired, AcknowledgeType.Release);
            await first.CommitAsync();
            var redelivered = await second.PollAsync().FirstAsync();
            await Assert.That(redelivered.Offset).IsEqualTo(1);
            await Assert.That(redelivered.DeliveryCount).IsEqualTo(2);

            await first.CloseAsync();
            await second.CloseAsync();
            await MutateAsync();
            await using var replacement = CreateConsumer(cluster);
            var replayed = await replacement.PollAsync().ToArrayAsync();
            await Assert.That(replayed.Select(record => record.Offset).ToArray()).IsEquivalentTo([0L, 1L]);
            await Assert.That(replayed.Select(record => record.DeliveryCount).ToArray()).IsEquivalentTo([1, 1]);
        }
        finally
        {
            barrier?.Release();
        }

        Task MutateAsync() => delete
            ? admin.DeleteShareGroupOffsetsAsync("workers", ["shared"]).AsTask()
            : admin.AlterShareGroupOffsetsAsync("workers",
                [new ShareGroupOffsetAlteration { TopicPartition = new("shared", 0), StartOffset = 0 }]).AsTask();
    }

    private static async Task LeaveAcknowledgementGap(InMemoryKafkaCluster cluster)
    {
        await using var consumer = CreateConsumer(cluster);
        var records = await consumer.PollAsync().ToArrayAsync();
        consumer.Acknowledge(records[0], AcknowledgeType.Release);
        consumer.Acknowledge(records[1], AcknowledgeType.Accept);
        await consumer.CommitAsync();
    }

    private static InMemoryShareConsumer<string, string> CreateConsumer(InMemoryKafkaCluster cluster)
    {
        var consumer = new InMemoryShareConsumer<string, string>(cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers" });
        consumer.Subscribe("shared");
        return consumer;
    }

    private static async Task ProduceRecords(InMemoryKafkaCluster cluster, int count)
    {
        await using var producer = new InMemoryProducer<string, string>(cluster);
        for (var index = 0; index < count; index++)
            await producer.ProduceAsync("shared", "key", $"value-{index}");
    }
}
