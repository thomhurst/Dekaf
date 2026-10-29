using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;
using Dekaf.Testing;
using Dekaf.Tests.Unit.Hosting;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryShareRenewalTests
{
    [Test]
    [Arguments(AcknowledgeType.Accept, false)]
    [Arguments(AcknowledgeType.Accept, true)]
    [Arguments(AcknowledgeType.Release, false)]
    [Arguments(AcknowledgeType.Release, true)]
    [Arguments(AcknowledgeType.Reject, false)]
    [Arguments(AcknowledgeType.Reject, true)]
    public async Task Renew_AtDeliveryLimitRetainsLeaseUntilFinalAcknowledgement(AcknowledgeType finalType, bool withFault)
    {
        var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions { DeliveryCountLimit = 1 });
        cluster.CreateTopic("dead-letters", configs: new Dictionary<string, string>
        {
            ["errors.deadletterqueue.group.enable"] = "true"
        });
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster,
            new InMemoryShareConsumerOptions
            {
                AutoOffsetReset = AutoOffsetReset.Earliest,
                GroupId = "group",
                AcknowledgementMode = ShareAcknowledgementMode.Explicit,
                DeadLetterQueueTopic = "dead-letters"
            });
        consumer.Subscribe("orders");
        var record = await consumer.PollAsync().FirstAsync();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            consumer.Acknowledge(record, AcknowledgeType.Renew);
            if (withFault)
                cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.ShareAcknowledge)).Release();
            await consumer.CommitAsync();
            var replay = await consumer.PollAsync().FirstAsync();
            await Assert.That(replay).IsSameReferenceAs(record);
            await Assert.That(replay.DeliveryCount).IsEqualTo(1);
            await Assert.That(cluster.GetCommittedShareOffset("group", new("orders", 0))).IsEqualTo(0);
            await Assert.That(cluster.ReadRecords("dead-letters")).IsEmpty();
        }
        consumer.Acknowledge(record, finalType);
        if (withFault)
            cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.ShareAcknowledge)).Release();
        await consumer.CommitAsync();
        await Assert.That(await consumer.PollAsync().ToListAsync()).IsEmpty();
        await Assert.That(cluster.GetCommittedShareOffset("group", new("orders", 0))).IsEqualTo(1);
        await Assert.That(cluster.ReadRecords("dead-letters").Count).IsEqualTo(finalType == AcknowledgeType.Reject ? 1 : 0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Renew_CommitRetainsLeaseAndAllowsFinalAccept(bool withFault)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "value");
        await using var consumer = ExplicitConsumer(cluster);
        await using var other = ExplicitConsumer(cluster);
        consumer.Subscribe("orders");
        other.Subscribe("orders");
        var record = await consumer.PollAsync().FirstAsync();
        consumer.Acknowledge(record, AcknowledgeType.Renew);
        if (withFault)
        {
            cluster.FaultPlan.Fail(new(KafkaFaultOperation.ShareAcknowledge), new InvalidOperationException("retry"));
            await Assert.That(async () => await consumer.CommitAsync()).Throws<InvalidOperationException>();
        }
        await consumer.CommitAsync();
        await Assert.That(await other.PollAsync().ToListAsync()).IsEmpty();
        consumer.Acknowledge(record);
        await consumer.CommitAsync();
        await Assert.That(await consumer.PollAsync().ToListAsync()).IsEmpty();
        await Assert.That(cluster.GetCommittedShareOffset("group", new("orders", 0))).IsEqualTo(1);
    }

    [Test]
    [Arguments(AcknowledgeType.Accept)]
    [Arguments(AcknowledgeType.Release)]
    [Arguments(AcknowledgeType.Reject)]
    public async Task Renew_ReplaysSameDeliveryAndPreservesLaterAccept(AcknowledgeType finalType)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "first");
        await producer.ProduceAsync("orders", "key", "second");
        await using var consumer = ExplicitConsumer(cluster);
        consumer.Subscribe("orders");
        var records = await consumer.PollAsync().ToListAsync();
        consumer.Acknowledge(records[0], AcknowledgeType.Renew);
        consumer.Acknowledge(records[1]);
        var replay = await consumer.PollAsync().ToListAsync();
        await Assert.That(replay.Count).IsEqualTo(1);
        await Assert.That(replay[0].Offset).IsEqualTo(records[0].Offset);
        await Assert.That(replay[0].Value).IsEqualTo("first");
        await Assert.That(replay[0].DeliveryCount).IsEqualTo(records[0].DeliveryCount);
        // A commit with no new disposition must not implicitly accept the renewal.
        await consumer.CommitAsync();
        var repeated = await consumer.PollAsync().ToListAsync();
        await Assert.That(repeated.Count).IsEqualTo(1);
        await Assert.That(repeated[0].DeliveryCount).IsEqualTo(records[0].DeliveryCount);
        consumer.Acknowledge(replay[0], finalType);
        await consumer.CommitAsync();
        var remaining = await consumer.PollAsync().ToListAsync();
        if (finalType == AcknowledgeType.Release)
        {
            await Assert.That(remaining.Count).IsEqualTo(1);
            await Assert.That(remaining[0].Offset).IsEqualTo(records[0].Offset);
            await Assert.That(remaining[0].DeliveryCount).IsEqualTo(records[0].DeliveryCount + 1);
            consumer.Acknowledge(remaining[0]);
            await consumer.CommitAsync();
        }
        else
        {
            await Assert.That(remaining).IsEmpty();
        }
        await Assert.That(cluster.GetCommittedShareOffset("group", new("orders", 0))).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Renew_DepartureReleasesLeaseWithoutLosingLaterAccept(bool close)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "first");
        await producer.ProduceAsync("orders", "key", "second");
        await using var consumer = ExplicitConsumer(cluster);
        consumer.Subscribe("orders");
        var records = await consumer.PollAsync().ToListAsync();
        consumer.Acknowledge(records[0], AcknowledgeType.Renew);
        consumer.Acknowledge(records[1]);
        await consumer.CommitAsync();
        if (close)
            await consumer.CloseAsync();
        else
            consumer.Unsubscribe();
        await using var other = ExplicitConsumer(cluster);
        other.Subscribe("orders");
        var redelivered = await other.PollAsync().ToListAsync();
        await Assert.That(redelivered.Count).IsEqualTo(1);
        await Assert.That(redelivered[0].Value).IsEqualTo("first");
        await Assert.That(redelivered[0].DeliveryCount).IsEqualTo(2);
        other.Acknowledge(redelivered[0]);
        await other.CommitAsync();
        await Assert.That(cluster.GetCommittedShareOffset("group", new("orders", 0))).IsEqualTo(2);
    }

    [Test]
    [Arguments(ShareAcknowledgementMode.Implicit)]
    [Arguments(ShareAcknowledgementMode.Explicit)]
    public async Task Configuration_ReportsConfiguredMode(ShareAcknowledgementMode mode)
    {
        await using var consumer = new InMemoryShareConsumer<string, string>(new InMemoryKafkaCluster(),
            new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest, AcknowledgementMode = mode });
        await Assert.That(((IShareConsumerConfiguration)consumer).AcknowledgementMode).IsEqualTo(mode);
        await Assert.That(new InMemoryShareConsumerOptions().AcknowledgementMode).IsEqualTo(ShareAcknowledgementMode.Implicit);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Renew_ReplaySharesPollBudgetWithFreshRecords(int maxPollRecords)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "first");
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster,
            new InMemoryShareConsumerOptions
            {
                AutoOffsetReset = AutoOffsetReset.Earliest,
                AcknowledgementMode = ShareAcknowledgementMode.Explicit,
                MaxPollRecords = maxPollRecords
            });
        consumer.Subscribe("orders");
        var record = await consumer.PollAsync().FirstAsync();
        consumer.Acknowledge(record, AcknowledgeType.Renew);
        await producer.ProduceAsync("orders", "key", "second");
        var next = await consumer.PollAsync().ToListAsync();
        await Assert.That(next.Count).IsEqualTo(maxPollRecords);
        await Assert.That(next[0].Value).IsEqualTo("second");
        consumer.Acknowledge(next[0]);
        if (maxPollRecords == 2)
            await Assert.That(next[1].Offset).IsEqualTo(record.Offset);
        await Assert.That((await consumer.PollAsync().FirstAsync()).Offset).IsEqualTo(record.Offset);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task OffsetReset_ForgetsTerminalRecordsBeyondRenewedGap(bool delete, bool detailed)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "first");
        await producer.ProduceAsync("orders", "key", "second");
        await using (var consumer = ExplicitConsumer(cluster))
        {
            consumer.Subscribe("orders");
            var records = await consumer.PollAsync().ToListAsync();
            consumer.Acknowledge(records[0], AcknowledgeType.Renew);
            consumer.Acknowledge(records[1]);
            await consumer.CommitAsync();
        }
        await using var admin = new InMemoryAdminClient(cluster);
        if (detailed)
        {
            if (delete)
            {
                var results = await admin.DeleteShareGroupOffsetsDetailedAsync("group", ["orders"]);
                await Assert.That(results["orders"].IsSuccess).IsTrue();
            }
            else
            {
                var results = await admin.AlterShareGroupOffsetsDetailedAsync("group", [new() { TopicPartition = new("orders", 0), StartOffset = 0 }]);
                await Assert.That(results[new("orders", 0)].IsSuccess).IsTrue();
            }
        }
        else if (delete)
            await admin.DeleteShareGroupOffsetsAsync("group", ["orders"]);
        else
            await admin.AlterShareGroupOffsetsAsync("group", [new() { TopicPartition = new("orders", 0), StartOffset = 0 }]);
        await using var restarted = ExplicitConsumer(cluster);
        restarted.Subscribe("orders");
        await Assert.That(await restarted.PollAsync().ToListAsync()).Count().IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Release_BelowRenewedRecordCanBeRedelivered(bool withFault)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "first");
        await producer.ProduceAsync("orders", "key", "second");
        await using var consumer = ExplicitConsumer(cluster);
        consumer.Subscribe("orders");
        var records = await consumer.PollAsync().ToListAsync();
        consumer.Acknowledge(records[0], AcknowledgeType.Release);
        consumer.Acknowledge(records[1], AcknowledgeType.Renew);
        await consumer.CommitAsync();
        if (withFault)
            cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.ShareConsume, "orders", 0, "group")).Release();
        var next = await consumer.PollAsync().ToListAsync();
        await Assert.That(next.Count).IsEqualTo(2);
        await Assert.That(next[0].Offset).IsEqualTo(records[0].Offset);
        await Assert.That(next[0].DeliveryCount).IsEqualTo(2);
        await Assert.That(next[1]).IsSameReferenceAs(records[1]);
        await Assert.That(next[1].DeliveryCount).IsEqualTo(1);
        consumer.Acknowledge(next[0]);
        consumer.Acknowledge(next[1]);
        await consumer.CommitAsync();
        await Assert.That(await consumer.PollAsync().ToListAsync()).IsEmpty();
        await Assert.That(cluster.GetCommittedShareOffset("group", new("orders", 0))).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TopicRecreation_ForgetsCompletedRecordsAndDeliveryCounts(bool deleteById)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "old-first");
        await producer.ProduceAsync("orders", "key", "old-second");
        await using (var consumer = ExplicitConsumer(cluster))
        {
            consumer.Subscribe("orders");
            var records = await consumer.PollAsync().ToListAsync();
            consumer.Acknowledge(records[0], AcknowledgeType.Renew);
            consumer.Acknowledge(records[1]);
            await consumer.CommitAsync();
        }
        await using var admin = new InMemoryAdminClient(cluster);
        if (deleteById)
            await admin.DeleteTopicsAsync([cluster.TopicListings(includeInternal: true).Single().TopicId]);
        else
            await admin.DeleteTopicsAsync(["orders"]);
        cluster.CreateTopic("orders");
        await producer.ProduceAsync("orders", "key", "new-first");
        await producer.ProduceAsync("orders", "key", "new-second");
        await using var restarted = ExplicitConsumer(cluster);
        restarted.Subscribe("orders");
        var recreated = await restarted.PollAsync().ToListAsync();
        await Assert.That(recreated.Count).IsEqualTo(2);
        await Assert.That(recreated[0].Value).IsEqualTo("new-first");
        await Assert.That(recreated[1].Value).IsEqualTo("new-second");
        await Assert.That(recreated[0].DeliveryCount).IsEqualTo(1);
        await Assert.That(recreated[1].DeliveryCount).IsEqualTo(1);
        foreach (var record in recreated)
            restarted.Acknowledge(record);
        await restarted.CommitAsync();
        await Assert.That(cluster.GetCommittedShareOffset("group", new("orders", 0))).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PeerLease_BelowRenewedRecordDoesNotBlockFreshRecords(bool withFault)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "first");
        await producer.ProduceAsync("orders", "key", "second");
        await using var consumer = ExplicitConsumer(cluster);
        await using var other = ExplicitConsumer(cluster);
        consumer.Subscribe("orders");
        other.Subscribe("orders");
        var records = await consumer.PollAsync().ToListAsync();
        consumer.Acknowledge(records[0], AcknowledgeType.Release);
        consumer.Acknowledge(records[1], AcknowledgeType.Renew);
        await consumer.CommitAsync();
        var peerRecord = await other.PollAsync().FirstAsync();
        await producer.ProduceAsync("orders", "key", "third");
        if (withFault)
            cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.ShareConsume, "orders", 0, "group")).Release();

        var next = await consumer.PollAsync().ToListAsync();

        await Assert.That(next.Count).IsEqualTo(2);
        await Assert.That(next[0].Value).IsEqualTo("third");
        await Assert.That(next[1]).IsSameReferenceAs(records[1]);
        foreach (var record in next)
            consumer.Acknowledge(record);
        await consumer.CommitAsync();
        other.Acknowledge(peerRecord);
        await other.CommitAsync();
        await Assert.That(cluster.GetCommittedShareOffset("group", new("orders", 0))).IsEqualTo(3);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task TopicDeletion_InvalidatesOpenConsumersRenewal(bool deleteById, bool recreateBeforePoll)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "old");
        await using var consumer = ExplicitConsumer(cluster);
        consumer.Subscribe("orders");
        var old = await consumer.PollAsync().FirstAsync();
        consumer.Acknowledge(old, AcknowledgeType.Renew);
        await consumer.CommitAsync();
        await using var admin = new InMemoryAdminClient(cluster);
        if (deleteById)
            await admin.DeleteTopicsAsync([cluster.TopicListings(includeInternal: true).Single().TopicId]);
        else
            await admin.DeleteTopicsAsync(["orders"]);
        if (!recreateBeforePoll)
            await Assert.That(await consumer.PollAsync().ToListAsync()).IsEmpty();
        cluster.CreateTopic("orders");
        await producer.ProduceAsync("orders", "key", "new");

        var next = await consumer.PollAsync().ToListAsync();

        await Assert.That(next.Count).IsEqualTo(1);
        await Assert.That(next[0].Value).IsEqualTo("new");
        await Assert.That(next[0].DeliveryCount).IsEqualTo(1);
        await Assert.That(() => consumer.Acknowledge(old)).Throws<InvalidOperationException>();
        consumer.Acknowledge(next[0]);
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedShareOffset("group", new("orders", 0))).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TopicDeletion_DuringFinalAcknowledgementDoesNotCompleteRecreatedRecord(bool withFault)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "old");
        await using var consumer = ExplicitConsumer(cluster);
        consumer.Subscribe("orders");
        var old = await consumer.PollAsync().FirstAsync();
        consumer.Acknowledge(old, AcknowledgeType.Renew);
        await consumer.CommitAsync();
        consumer.Acknowledge(old);
        var barrier = withFault ? cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.ShareAcknowledge)) : null;
        var commit = withFault ? consumer.CommitAsync().AsTask() : Task.CompletedTask;
        if (barrier is not null)
            await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            cluster.DeleteTopic("orders");
            cluster.CreateTopic("orders");
            await producer.ProduceAsync("orders", "key", "new");
        }
        finally
        {
            barrier?.Release();
        }
        if (withFault)
            await commit;
        else
            await consumer.CommitAsync();

        var next = await consumer.PollAsync().ToListAsync();

        await Assert.That(next.Count).IsEqualTo(1);
        await Assert.That(next[0].Value).IsEqualTo("new");
        await Assert.That(next[0].DeliveryCount).IsEqualTo(1);
    }

    [Test]
    public async Task TopicDeletion_DuringAcquisitionDoesNotReleaseRecreatedLease()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "old");
        await using var consumer = ExplicitConsumer(cluster);
        consumer.Subscribe("orders");
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.ShareConsume));
        var poll = consumer.PollAsync().ToListAsync();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        ShareConsumeResult<string, string> current;
        try
        {
            cluster.DeleteTopic("orders");
            cluster.CreateTopic("orders");
            await producer.ProduceAsync("orders", "key", "new");
            current = await consumer.PollAsync().FirstAsync();
        }
        finally
        {
            barrier.Release();
        }

        await Assert.That(await poll).IsEmpty();
        await Assert.That(current.Value).IsEqualTo("new");
        await Assert.That(current.DeliveryCount).IsEqualTo(1);
        consumer.Acknowledge(current);
        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedShareOffset("group", new("orders", 0))).IsEqualTo(1);
    }

    [Test]
    public async Task ImplicitMode_RejectsRenew()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster, new InMemoryShareConsumerOptions { AutoOffsetReset = AutoOffsetReset.Earliest });
        consumer.Subscribe("orders");
        var record = await consumer.PollAsync().FirstAsync();
        await Assert.That(() => consumer.Acknowledge(record, AcknowledgeType.Renew))
            .Throws<InvalidOperationException>().WithMessage("Renew acknowledgements require explicit acknowledgement mode.");
        await consumer.CommitAsync();
        await Assert.That(await consumer.PollAsync().ToListAsync()).IsEmpty();
    }

    [Test]
    public async Task ExplicitMode_UnacknowledgedRecordRemainsAcquiredUntilClose()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "value");
        await using var consumer = ExplicitConsumer(cluster);
        consumer.Subscribe("orders");
        var record = await consumer.PollAsync().FirstAsync();
        await consumer.CommitAsync();
        await Assert.That(await consumer.PollAsync().ToListAsync()).IsEmpty();
        await consumer.CloseAsync();
        await using var other = ExplicitConsumer(cluster);
        other.Subscribe("orders");
        var redelivered = await other.PollAsync().FirstAsync();
        await Assert.That(redelivered.Offset).IsEqualTo(record.Offset);
        await Assert.That(redelivered.DeliveryCount).IsEqualTo(record.DeliveryCount + 1);
    }

    [Test]
    public async Task HostedService_RenewsAndAcceptsInMemoryRecordOnce()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("orders", "key", "value");
        await using var consumer = ExplicitConsumer(cluster);
        var renewal = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.ShareAcknowledge));
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = 0;
        await using var service = new KafkaShareConsumerServiceTests.TestService(consumer, (_, _) =>
        {
            Interlocked.Increment(ref processed);
            return new ValueTask(finish.Task);
        }, new KafkaShareConsumerServiceOptions { RenewalInterval = TimeSpan.FromMilliseconds(1) });
        await service.StartAsync(default);
        try
        {
            await renewal.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            finish.SetResult();
        }
        finally
        {
            finish.TrySetResult();
            renewal.Release();
        }
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await service.StopAsync(default);
        await Assert.That(processed).IsEqualTo(1);
        await Assert.That(service.Errors).IsEmpty();
        await Assert.That(cluster.GetCommittedShareOffset("group", new("orders", 0))).IsEqualTo(1);
    }

    private static InMemoryShareConsumer<string, string> ExplicitConsumer(InMemoryKafkaCluster cluster) =>
        new(cluster, new InMemoryShareConsumerOptions
        {
            AutoOffsetReset = AutoOffsetReset.Earliest,
            GroupId = "group",
            AcknowledgementMode = ShareAcknowledgementMode.Explicit
        });
}
