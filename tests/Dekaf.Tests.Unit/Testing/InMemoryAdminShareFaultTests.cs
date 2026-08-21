using Dekaf.Admin;
using Dekaf.Producer;
using Dekaf.ShareConsumer;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryAdminShareFaultTests
{
    [Test]
    public async Task AdminFaults_HonorTopicScopeAndScriptOrderBeforeMutation()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var firstFailure = new InvalidOperationException("first");
        var secondFailure = new InvalidOperationException("second");
        var scope = new KafkaFaultScope(KafkaFaultOperation.Admin, topic: "orders");
        cluster.FaultPlan.Fail(scope, firstFailure);
        cluster.FaultPlan.Fail(scope, secondFailure);

        await admin.CreateTopicsAsync([new NewTopic { Name = "other", NumPartitions = 1 }]);
        var first = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreateTopicsAsync([new NewTopic { Name = "orders", NumPartitions = 1 }]).AsTask());
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreateTopicsAsync([new NewTopic { Name = "orders", NumPartitions = 1 }]).AsTask());
        await admin.CreateTopicsAsync([new NewTopic { Name = "orders", NumPartitions = 1 }]);

        await Assert.That(first).IsSameReferenceAs(firstFailure);
        await Assert.That(second).IsSameReferenceAs(secondFailure);
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(["orders", "other"]);
    }

    [Test]
    public async Task AdminBarrier_CancellationPreventsMutationAndRetryRecovers()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.Admin, topic: "orders"));
        using var cancellation = new CancellationTokenSource();

        var pending = admin.CreateTopicsAsync(
            [new NewTopic { Name = "orders", NumPartitions = 1 }],
            cancellationToken: cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        await Assert.That(cluster.ListTopics()).IsEmpty();
        await Assert.That(barrier.Release()).IsTrue();

        await admin.CreateTopicsAsync([new NewTopic { Name = "orders", NumPartitions = 1 }]);
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(["orders"]);
    }

    [Test]
    public async Task AdminFault_PreservesPriorBatchSuccessForTargetedRetry()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("orders unavailable");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Admin, topic: "orders"),
            failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreateTopicsAsync(
                [
                    new NewTopic { Name = "customers", NumPartitions = 1 },
                    new NewTopic { Name = "orders", NumPartitions = 1 },
                    new NewTopic { Name = "payments", NumPartitions = 1 }
                ]).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(["customers"]);

        await admin.CreateTopicsAsync(
            [
                new NewTopic { Name = "orders", NumPartitions = 1 },
                new NewTopic { Name = "payments", NumPartitions = 1 }
            ]);
        await Assert.That(cluster.ListTopics()).IsEquivalentTo(["customers", "orders", "payments"]);
    }

    [Test]
    public async Task AdminFault_ClearLeavesGroupMutationAvailable()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var scope = new KafkaFaultScope(
            KafkaFaultOperation.Admin,
            topic: "orders",
            partition: 0,
            groupId: "billing");
        cluster.FaultPlan.FailPersistently(scope, new InvalidOperationException("blocked"));

        var removed = cluster.FaultPlan.Clear(scope);
        await admin.AlterConsumerGroupOffsetsAsync(
            "billing",
            [new TopicPartitionOffset("orders", 0, 7)]);

        await Assert.That(removed).IsEqualTo(1);
        await Assert.That(cluster.GetCommittedOffset("billing", new TopicPartition("orders", 0)))
            .IsEqualTo(7);
    }

    [Test]
    public async Task ShareConsumeFault_RollsBackLeaseAndDeliveryCount()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "shared",
            Partition = 0,
            Key = "key",
            Value = "value"
        });
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers" });
        consumer.Subscribe("shared");
        var failure = new InvalidOperationException("delivery failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(
                KafkaFaultOperation.ShareConsume,
                "shared",
                partition: 0,
                groupId: "workers"),
            failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await consumer.PollAsync().FirstAsync());
        var recovered = await consumer.PollAsync().FirstAsync();

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(recovered.Offset).IsEqualTo(0);
        await Assert.That(recovered.DeliveryCount).IsEqualTo(1);
    }

    [Test]
    public async Task ShareAcknowledgeFault_PreservesPendingRecordForRetry()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("shared", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers" });
        consumer.Subscribe("shared");
        var record = await consumer.PollAsync().FirstAsync();
        consumer.Acknowledge(record);
        var failure = new InvalidOperationException("acknowledgement failed");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(
                KafkaFaultOperation.ShareAcknowledge,
                "shared",
                partition: 0,
                groupId: "workers"),
            failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CommitAsync().AsTask());
        var beforeRetry = cluster.GetCommittedOffset("workers", new TopicPartition("shared", 0));
        await consumer.CommitAsync();

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(beforeRetry).IsNull();
        await Assert.That(cluster.GetCommittedOffset("workers", new TopicPartition("shared", 0)))
            .IsEqualTo(1);
    }

    [Test]
    public async Task ShareAcknowledgeBarrier_CancellationPreservesPendingRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("shared", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers" });
        consumer.Subscribe("shared");
        var record = await consumer.PollAsync().FirstAsync();
        consumer.Acknowledge(record);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(
                KafkaFaultOperation.ShareAcknowledge,
                "shared",
                partition: 0,
                groupId: "workers"));
        using var cancellation = new CancellationTokenSource();

        var pending = consumer.CommitAsync(cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        await Assert.That(cluster.GetCommittedOffset("workers", new TopicPartition("shared", 0)))
            .IsNull();
        await Assert.That(barrier.Release()).IsTrue();

        await consumer.CommitAsync();
        await Assert.That(cluster.GetCommittedOffset("workers", new TopicPartition("shared", 0)))
            .IsEqualTo(1);
    }
}
