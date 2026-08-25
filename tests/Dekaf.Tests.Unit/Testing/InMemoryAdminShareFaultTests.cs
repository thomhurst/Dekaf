using System.Threading.Tasks.Sources;
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
    public async Task AdminFault_EmptyCreateTopicsConsumesGenericFault()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreateTopicsAsync([]).AsTask());
        await admin.CreateTopicsAsync([]);

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AdminFault_EmptyResourceBatchesConsumeGenericFault()
    {
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DeleteTopicsAsync(Array.Empty<string>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DeleteTopicsAsync(Array.Empty<Guid>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DescribeTopicsAsync(Array.Empty<string>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DescribeTopicsAsync(Array.Empty<Guid>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DescribeTopicPartitionsPageAsync(Array.Empty<string>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DescribeConsumerGroupsAsync(Array.Empty<string>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DeleteConsumerGroupsAsync(Array.Empty<string>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DeleteRecordsAsync(new Dictionary<TopicPartition, long>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.CreatePartitionsAsync(new Dictionary<string, int>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.AlterPartitionReassignmentsAsync(
                new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.ListPartitionReassignmentsAsync(Array.Empty<TopicPartition>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DescribeConfigsAsync(Array.Empty<ConfigResource>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.AlterConfigsAsync(
                new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.IncrementalAlterConfigsAsync(
                new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.CreateAclsAsync(Array.Empty<AclBinding>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DeleteAclsAsync(Array.Empty<AclBindingFilter>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DeleteConsumerGroupOffsetsAsync(
                "workers",
                Array.Empty<TopicPartition>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.ListOffsetsAsync(Array.Empty<TopicPartitionOffsetSpec>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.ElectLeadersAsync(
                ElectionType.Preferred,
                Array.Empty<TopicPartition>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.AlterClientQuotasAsync(Array.Empty<ClientQuotaAlteration>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DescribeTransactionsAsync(Array.Empty<string>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DescribeProducersAsync(Array.Empty<TopicPartition>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.FenceProducersAsync(Array.Empty<string>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DescribeLogDirsAsync(
                Array.Empty<int>(),
                Array.Empty<TopicPartition>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.AlterReplicaLogDirsAsync(
                new Dictionary<TopicPartitionReplica, string>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DescribeReplicaLogDirsAsync(Array.Empty<TopicPartitionReplica>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DescribeStreamsGroupsAsync(Array.Empty<string>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DescribeShareGroupsAsync(Array.Empty<string>()).AsTask());
    }

    [Test]
    public async Task AdminFault_TopicIdOperationsUseResolvedTopicScope()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        cluster.CreateTopic("orders");
        var topicId = cluster.TopicListings(includeInternal: true).Single().TopicId;
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Admin, topic: "orders"),
            failure,
            occurrenceCount: 2);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.DescribeTopicsAsync([topicId]).AsTask());
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.DeleteTopicsAsync([topicId]).AsTask());

        await Assert.That(cluster.ListTopics()).IsEquivalentTo(["orders"]);
    }

    [Test]
    public async Task AdminFault_InvalidRequestDoesNotConsumeScriptedFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Admin, topic: "orders"),
            failure);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            admin.CreateTopicsAsync([new NewTopic { Name = "orders", NumPartitions = 0 }]).AsTask());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreateTopicsAsync([new NewTopic { Name = "orders", NumPartitions = 1 }]).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AdminFault_InvalidLaterTopicDoesNotConsumeFaultOrMutateBatch()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            admin.CreateTopicsAsync(
                [
                    new NewTopic { Name = "customers", NumPartitions = 1 },
                    new NewTopic { Name = "orders", NumPartitions = 0 }
                ]).AsTask());

        await Assert.That(cluster.ListTopics()).IsEmpty();
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreateTopicsAsync([new NewTopic { Name = "customers", NumPartitions = 1 }]).AsTask());
        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AdminFault_EmptyTopicIdDoesNotConsumeScriptedFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        cluster.CreateTopic("orders");
        var topicId = cluster.TopicListings(includeInternal: true).Single().TopicId;
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
            admin.DescribeTopicsAsync([Guid.Empty]).AsTask());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.DescribeTopicsAsync([topicId]).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AdminFault_InvalidPartitionCountDoesNotConsumeScriptedFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        cluster.CreateTopic("orders");
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Admin, topic: "orders"),
            failure);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            admin.CreatePartitionsAsync(new Dictionary<string, int> { ["orders"] = 0 }).AsTask());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreatePartitionsAsync(new Dictionary<string, int> { ["orders"] = 2 }).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AdminFault_InvalidLaterPartitionCountDoesNotConsumeFaultOrMutateBatch()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        cluster.CreateTopic("customers");
        cluster.CreateTopic("orders");
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            admin.CreatePartitionsAsync(new Dictionary<string, int>
            {
                ["customers"] = 2,
                ["orders"] = 0
            }).AsTask());

        var customers = cluster.DescribeTopics(["customers"])["customers"];
        await Assert.That(customers.Partitions).Count().IsEqualTo(1);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreatePartitionsAsync(new Dictionary<string, int> { ["customers"] = 2 }).AsTask());
        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AdminFault_InvalidLaterReassignmentDoesNotConsumeScriptedFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            admin.AlterPartitionReassignmentsAsync(
                new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
                {
                    [new("orders", 0)] = NewPartitionReassignment.ToReplicas(0),
                    [new("payments", 0)] = NewPartitionReassignment.ToReplicas(-1)
                }).AsTask());
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.AlterPartitionReassignmentsAsync(
                new Dictionary<TopicPartition, Optional<NewPartitionReassignment>>
                {
                    [new("orders", 0)] = NewPartitionReassignment.ToReplicas(0)
                }).AsTask());
        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AdminFault_InvalidLaterPartitionDoesNotConsumeScriptedFailure()
    {
        var validPartition = new TopicPartition("orders", 0);
        var invalidPartition = new TopicPartition("payments", -1);
        var validReplica = new TopicPartitionReplica("orders", 0, 0);
        var invalidReplica = new TopicPartitionReplica("payments", -1, 0);

        await AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
            admin => admin.DeleteRecordsAsync(new Dictionary<TopicPartition, long>
            {
                [validPartition] = 0,
                [invalidPartition] = 0
            }).AsTask(),
            admin => admin.DeleteRecordsAsync(new Dictionary<TopicPartition, long>
            {
                [validPartition] = 0
            }).AsTask());
        await AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
            admin => admin.DescribeProducersAsync([validPartition, invalidPartition]).AsTask(),
            admin => admin.DescribeProducersAsync([validPartition]).AsTask());
        await AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
            admin => admin.AlterReplicaLogDirsAsync(
                new Dictionary<TopicPartitionReplica, string>
                {
                    [validReplica] = "in-memory",
                    [invalidReplica] = "in-memory"
                }).AsTask(),
            admin => admin.AlterReplicaLogDirsAsync(
                new Dictionary<TopicPartitionReplica, string>
                {
                    [validReplica] = "in-memory"
                }).AsTask());
        await AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
            admin => admin.DescribeReplicaLogDirsAsync([validReplica, invalidReplica]).AsTask(),
            admin => admin.DescribeReplicaLogDirsAsync([validReplica]).AsTask());
    }

    [Test]
    public async Task AdminFault_InvalidBrokerIdDoesNotConsumeScriptedFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            admin.DescribeLogDirsAsync([-1]).AsTask());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.DescribeLogDirsAsync([0]).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AdminFault_InvalidPartitionWithEmptyBrokerBatchDoesNotConsumeScriptedFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            admin.DescribeLogDirsAsync(
                Array.Empty<int>(),
                [new TopicPartition("orders", -1)]).AsTask());
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.DescribeLogDirsAsync(
                Array.Empty<int>(),
                Array.Empty<TopicPartition>()).AsTask());
        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AdminFault_InvalidCreateDelegationTokenDurationDoesNotConsumeScriptedFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            admin.CreateDelegationTokenAsync(maxLifetime: TimeSpan.FromSeconds(-1)).AsTask());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreateDelegationTokenAsync().AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AdminFault_InvalidRenewDelegationTokenDurationDoesNotConsumeScriptedFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var token = await admin.CreateDelegationTokenAsync();
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            admin.RenewDelegationTokenAsync(token.Hmac, TimeSpan.FromSeconds(-1)).AsTask());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.RenewDelegationTokenAsync(token.Hmac).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AdminFault_InvalidExpireDelegationTokenDurationDoesNotConsumeScriptedFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var token = await admin.CreateDelegationTokenAsync();
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            admin.ExpireDelegationTokenAsync(token.Hmac, TimeSpan.FromSeconds(-1)).AsTask());
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.ExpireDelegationTokenAsync(token.Hmac).AsTask());

        await Assert.That(actual).IsSameReferenceAs(failure);
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
    public async Task ShareFaultIndex_UnrelatedSelectorsLeaveDeliveryAndCommitOnFastPath()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("shared", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers" });
        consumer.Subscribe("shared");
        var consumedFaults = 0;
        cluster.FaultPlan.FaultConsumed += _ => consumedFaults++;
        cluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.ShareConsume, "other", 0, "workers"),
            new InvalidOperationException("unrelated delivery"));
        cluster.FaultPlan.FailPersistently(
            new KafkaFaultScope(KafkaFaultOperation.ShareAcknowledge, "shared", 0, "other-group"),
            new InvalidOperationException("unrelated acknowledgement"));

        var record = await consumer.PollAsync().FirstAsync();
        consumer.Acknowledge(record);
        await consumer.CommitAsync();

        await Assert.That(record.Offset).IsEqualTo(0);
        await Assert.That(consumedFaults).IsEqualTo(0);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(2);
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

    [Test]
    public async Task ShareAcknowledgeBarrier_SerializesConcurrentCommits()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("shared", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers" });
        consumer.Subscribe("shared");
        consumer.Acknowledge(await consumer.PollAsync().FirstAsync());
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.ShareAcknowledge));

        var first = consumer.CommitAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        var second = consumer.CommitAsync().AsTask();

        await Assert.That(second.IsCompleted).IsFalse();
        await Assert.That(cluster.GetCommittedOffset("workers", new TopicPartition("shared", 0)))
            .IsNull();
        await Assert.That(barrier.Release()).IsTrue();
        await Task.WhenAll(first, second);
        await Assert.That(cluster.GetCommittedOffset("workers", new TopicPartition("shared", 0)))
            .IsEqualTo(1);
    }

    [Test]
    public async Task ShareAcknowledgeFault_ConsumesSynchronouslyCompletedValueTaskSource()
    {
        var faultPlan = new CompletedSourceFaultPlan();
        var cluster = new InMemoryKafkaCluster(faultPlan);
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("shared", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers" });
        consumer.Subscribe("shared");
        consumer.Acknowledge(await consumer.PollAsync().FirstAsync());

        await consumer.CommitAsync();

        await Assert.That(faultPlan.GetResultCount).IsEqualTo(1);
    }

    private static async Task AssertEmptyAdminBatchConsumesFaultAsync(
        Func<InMemoryAdminClient, Task> operation)
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => operation(admin));
        await operation(admin);

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(0);
    }

    private static async Task AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
        Func<InMemoryAdminClient, Task> invalidOperation,
        Func<InMemoryAdminClient, Task> validOperation)
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => invalidOperation(admin));
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => validOperation(admin));
        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    private sealed class CompletedSourceFaultPlan : IKafkaFaultPlan, IValueTaskSource
    {
        public event Action<KafkaFaultObservation>? FaultConsumed
        {
            add { }
            remove { }
        }

        public int Count => 0;

        public int GetResultCount { get; private set; }

        public void Fail(KafkaFaultScope scope, Exception exception, int occurrenceCount = 1) =>
            throw new NotSupportedException();

        public void FailPersistently(KafkaFaultScope scope, Exception exception) =>
            throw new NotSupportedException();

        public KafkaFaultBarrier PauseNext(KafkaFaultScope scope) =>
            throw new NotSupportedException();

        public ValueTask ApplyAsync(
            KafkaFaultScope operationScope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return operationScope.Operation == KafkaFaultOperation.ShareAcknowledge
                ? new ValueTask(this, token: 0)
                : ValueTask.CompletedTask;
        }

        public int Clear(KafkaFaultScope scope) => 0;

        public int Clear() => 0;

        void IValueTaskSource.GetResult(short token) => GetResultCount++;

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) =>
            ValueTaskSourceStatus.Succeeded;

        void IValueTaskSource.OnCompleted(
            Action<object?> continuation,
            object? state,
            short token,
            ValueTaskSourceOnCompletedFlags flags) => throw new InvalidOperationException();
    }
}
