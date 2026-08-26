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
    public async Task StreamsAdminFaults_HonorResourceScopeBeforeReadOrMutation()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        const string groupId = "streams-app";
        var partition = new TopicPartition("input", 0);
        var scope = new KafkaFaultScope(
            KafkaFaultOperation.Admin,
            partition.Topic,
            partition.Partition,
            groupId);
        cluster.CreateTopic(partition.Topic);
        _ = await admin.AlterStreamsGroupOffsetsAsync(
            groupId,
            [new TopicPartitionOffset(partition.Topic, partition.Partition, 42)]);

        var listFailure = new InvalidOperationException("list blocked");
        cluster.FaultPlan.Fail(scope, listFailure);
        var actualListFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.ListStreamsGroupOffsetsAsync(
                new Dictionary<string, ListStreamsGroupOffsetsSpec>
                {
                    [groupId] = new() { TopicPartitions = [partition] }
                }).AsTask());

        var alterFailure = new InvalidOperationException("alter blocked");
        cluster.FaultPlan.Fail(scope, alterFailure);
        var actualAlterFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.AlterStreamsGroupOffsetsAsync(
                groupId,
                [new TopicPartitionOffset(partition.Topic, partition.Partition, 84)]).AsTask());

        var deleteOffsetsFailure = new InvalidOperationException("offset deletion blocked");
        cluster.FaultPlan.Fail(scope, deleteOffsetsFailure);
        var actualDeleteOffsetsFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.DeleteStreamsGroupOffsetsAsync(groupId, [partition]).AsTask());

        var deleteGroupFailure = new InvalidOperationException("group deletion blocked");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: groupId),
            deleteGroupFailure);
        var actualDeleteGroupFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.DeleteStreamsGroupsAsync([groupId]).AsTask());

        await Assert.That(actualListFailure).IsSameReferenceAs(listFailure);
        await Assert.That(actualAlterFailure).IsSameReferenceAs(alterFailure);
        await Assert.That(actualDeleteOffsetsFailure).IsSameReferenceAs(deleteOffsetsFailure);
        await Assert.That(actualDeleteGroupFailure).IsSameReferenceAs(deleteGroupFailure);
        await Assert.That(cluster.GetGroupOffsetDetails(groupId)[partition].Offset).IsEqualTo(42);
        await Assert.That(cluster.ListGroups()).Contains(groupId);
    }

    [Test]
    public async Task StreamsAdminBarrier_CancellationPreventsOffsetDeletion()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        const string groupId = "streams-app";
        var partition = new TopicPartition("input", 0);
        cluster.CreateTopic(partition.Topic);
        _ = await admin.AlterStreamsGroupOffsetsAsync(
            groupId,
            [new TopicPartitionOffset(partition.Topic, partition.Partition, 42)]);
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(
                KafkaFaultOperation.Admin,
                partition.Topic,
                partition.Partition,
                groupId));
        using var cancellation = new CancellationTokenSource();

        var pending = admin.DeleteStreamsGroupOffsetsAsync(
            groupId,
            [partition],
            cancellationToken: cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        await Assert.That(cluster.GetGroupOffsetDetails(groupId)[partition].Offset).IsEqualTo(42);
        await Assert.That(barrier.Release()).IsTrue();
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
            static admin => admin.ListStreamsGroupOffsetsAsync(
                new Dictionary<string, ListStreamsGroupOffsetsSpec>()).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.AlterStreamsGroupOffsetsAsync("streams", []).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DeleteStreamsGroupOffsetsAsync("streams", []).AsTask());
        await AssertEmptyAdminBatchConsumesFaultAsync(
            static admin => admin.DeleteStreamsGroupsAsync([]).AsTask());
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
    public async Task AdminFault_InvalidLaterNameDoesNotConsumeScriptedFailure()
    {
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DeleteTopicsAsync(["orders", ""]).AsTask(),
            admin => admin.DeleteTopicsAsync(["orders"]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeTopicsAsync(["orders", ""]).AsTask(),
            admin => admin.DescribeTopicsAsync(["orders"]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeTopicPartitionsPageAsync(["orders", ""]).AsTask(),
            admin => admin.DescribeTopicPartitionsPageAsync(["orders"]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DeleteConsumerGroupsAsync(["workers", ""]).AsTask(),
            admin => admin.DeleteConsumerGroupsAsync(["workers"]).AsTask());
    }

    [Test]
    public async Task AdminFault_DuplicateResultKeysDoNotConsumeScriptedFailure()
    {
        var partition = new TopicPartition("orders", 0);
        var offsetSpec = new TopicPartitionOffsetSpec
        {
            TopicPartition = partition,
            Spec = OffsetSpec.Latest
        };
        var configResource = new ConfigResource
        {
            Type = ConfigResourceType.Topic,
            Name = "orders"
        };

        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeTopicsAsync(["orders", "orders"]).AsTask(),
            admin => admin.DescribeTopicsAsync(["orders"]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeTopicPartitionsPageAsync(["orders", "orders"]).AsTask(),
            admin => admin.DescribeTopicPartitionsPageAsync(["orders"]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeConsumerGroupsAsync(["workers", "workers"]).AsTask(),
            admin => admin.DescribeConsumerGroupsAsync(["workers"]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeStreamsGroupsAsync(["workers", "workers"]).AsTask(),
            admin => admin.DescribeStreamsGroupsAsync(["workers"]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeShareGroupsAsync(["workers", "workers"]).AsTask(),
            admin => admin.DescribeShareGroupsAsync(["workers"]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeUserScramCredentialsAsync(["alice", "alice"]).AsTask(),
            admin => admin.DescribeUserScramCredentialsAsync(["alice"]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeConfigsAsync([configResource, configResource]).AsTask(),
            admin => admin.DescribeConfigsAsync([configResource]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.ListOffsetsAsync([offsetSpec, offsetSpec]).AsTask(),
            admin => admin.ListOffsetsAsync([offsetSpec]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.ElectLeadersAsync(ElectionType.Preferred, [partition, partition]).AsTask(),
            admin => admin.ElectLeadersAsync(ElectionType.Preferred, [partition]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeTransactionsAsync(["tx-1", "tx-1"]).AsTask(),
            admin => admin.DescribeTransactionsAsync(["tx-1"]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeProducersAsync([partition, partition]).AsTask(),
            admin => admin.DescribeProducersAsync([partition]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.FenceProducersAsync(["tx-1", "tx-1"]).AsTask(),
            admin => admin.FenceProducersAsync(["tx-1"]).AsTask());
    }

    [Test]
    public async Task AdminFault_InvalidConfigResourceDoesNotConsumeScriptedFailure()
    {
        var valid = ConfigResource.Topic("orders");
        var invalidTopic = ConfigResource.Topic(string.Empty);
        var invalidGroup = new ConfigResource
        {
            Type = ConfigResourceType.Group,
            Name = " "
        };

        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.DescribeConfigsAsync([valid, invalidTopic]).AsTask(),
            admin => admin.DescribeConfigsAsync([valid]).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.AlterConfigsAsync(
                new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>>
                {
                    [valid] = [],
                    [invalidGroup] = []
                }).AsTask(),
            admin => admin.AlterConfigsAsync(
                new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>> { [valid] = [] }).AsTask());
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentException>(
            admin => admin.IncrementalAlterConfigsAsync(
                new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
                {
                    [valid] = [],
                    [invalidTopic] = []
                }).AsTask(),
            admin => admin.IncrementalAlterConfigsAsync(
                new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>> { [valid] = [] }).AsTask());
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
        await AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
            admin => admin.ListPartitionReassignmentsAsync([validPartition, invalidPartition]).AsTask(),
            admin => admin.ListPartitionReassignmentsAsync([validPartition]).AsTask());
        await AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
            admin => admin.AlterConsumerGroupOffsetsAsync(
                "workers",
                [
                    new TopicPartitionOffset(validPartition.Topic, validPartition.Partition, 1),
                    new TopicPartitionOffset(invalidPartition.Topic, invalidPartition.Partition, 1)
                ]).AsTask(),
            admin => admin.AlterConsumerGroupOffsetsAsync(
                "workers",
                [new TopicPartitionOffset(validPartition.Topic, validPartition.Partition, 1)]).AsTask());
        await AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
            admin => admin.DeleteConsumerGroupOffsetsAsync(
                "workers",
                [validPartition, invalidPartition]).AsTask(),
            admin => admin.DeleteConsumerGroupOffsetsAsync("workers", [validPartition]).AsTask());
        await AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
            admin => admin.ListOffsetsAsync(
                [
                    new TopicPartitionOffsetSpec { TopicPartition = validPartition, Spec = OffsetSpec.Latest },
                    new TopicPartitionOffsetSpec { TopicPartition = invalidPartition, Spec = OffsetSpec.Latest }
                ]).AsTask(),
            admin => admin.ListOffsetsAsync(
                [new TopicPartitionOffsetSpec { TopicPartition = validPartition, Spec = OffsetSpec.Latest }]).AsTask());
        await AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
            admin => admin.ElectLeadersAsync(
                ElectionType.Preferred,
                [validPartition, invalidPartition]).AsTask(),
            admin => admin.ElectLeadersAsync(ElectionType.Preferred, [validPartition]).AsTask());
        await AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
            admin => admin.DescribeShareGroupOffsetsAsync(
                "workers",
                [validPartition, invalidPartition]).AsTask(),
            admin => admin.DescribeShareGroupOffsetsAsync("workers", [validPartition]).AsTask());
        await AssertInvalidAdminBatchDoesNotConsumeFaultAsync(
            admin => admin.AlterShareGroupOffsetsAsync(
                "workers",
                [
                    new ShareGroupOffsetAlteration { TopicPartition = validPartition, StartOffset = 1 },
                    new ShareGroupOffsetAlteration { TopicPartition = invalidPartition, StartOffset = 1 }
                ]).AsTask(),
            admin => admin.AlterShareGroupOffsetsAsync(
                "workers",
                [new ShareGroupOffsetAlteration { TopicPartition = validPartition, StartOffset = 1 }]).AsTask());
    }

    [Test]
    public async Task AdminFault_InvalidLaterShareTopicDoesNotConsumeScriptedFailure()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
            admin.DeleteShareGroupOffsetsAsync("workers", ["orders", ""]).AsTask());
        await Assert.That(cluster.FaultPlan.Count).IsEqualTo(1);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.DeleteShareGroupOffsetsAsync("workers", ["orders"]).AsTask());
        await Assert.That(actual).IsSameReferenceAs(failure);
    }

    [Test]
    public async Task AdminFault_AclOperationsHonorResourceScope()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var topicFailure = new InvalidOperationException("topic blocked");
        var groupFailure = new InvalidOperationException("group blocked");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Admin, topic: "orders"),
            topicFailure,
            occurrenceCount: 2);
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: "workers"),
            groupFailure);

        var createFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.CreateAclsAsync(
                [AclBinding.Allow(ResourcePattern.Topic("orders"), "User:alice", AclOperation.Read)]).AsTask());
        var deleteFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.DeleteAclsAsync(
                [AclBindingFilter.ForResource(ResourceType.Topic, "orders")]).AsTask());
        var describeFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.DescribeAclsAsync(
                AclBindingFilter.ForResource(ResourceType.Group, "workers")).AsTask());

        await Assert.That(createFailure).IsSameReferenceAs(topicFailure);
        await Assert.That(deleteFailure).IsSameReferenceAs(topicFailure);
        await Assert.That(describeFailure).IsSameReferenceAs(groupFailure);
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
    public async Task AdminFault_EmptyBrokerBatchPreservesPartitionScope()
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("orders", 0);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(
            new KafkaFaultScope(
                KafkaFaultOperation.Admin,
                partition.Topic,
                partition.Partition),
            failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            admin.DescribeLogDirsAsync([], [partition]).AsTask());
        var retry = await admin.DescribeLogDirsAsync([], [partition]);

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(retry).IsEmpty();
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
    public async Task ShareConsumeFault_StaleRollbackPreservesNewRegistrationLease()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("shared", "key", "value");
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers", MemberId = "first" });
        consumer.Subscribe("shared");
        var scope = new KafkaFaultScope(
            KafkaFaultOperation.ShareConsume,
            "shared",
            partition: 0,
            groupId: "workers");
        var barrier = cluster.FaultPlan.PauseNext(scope);
        using var cancellation = new CancellationTokenSource();
        var stalePoll = consumer.PollAsync(cancellation.Token).FirstAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();

        consumer.Unsubscribe().Subscribe("shared");
        var current = await consumer.PollAsync().FirstAsync();
        cancellation.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => stalePoll);
        await Assert.That(barrier.Release()).IsTrue();
        await using var otherConsumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers", MemberId = "second" });
        otherConsumer.Subscribe("shared");
        await using var duplicatePoll = otherConsumer.PollAsync().GetAsyncEnumerator();

        await Assert.That(current.Offset).IsEqualTo(0);
        await Assert.That(current.DeliveryCount).IsEqualTo(1);
        await Assert.That(await duplicatePoll.MoveNextAsync()).IsFalse();
    }

    [Test]
    public async Task ShareConsumeBarrier_DropsRecordAfterSubscriptionSwitch()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("topic-a", "key-a", "value-a");
        await producer.ProduceAsync("topic-b", "key-b", "value-b");
        await using var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers" });
        consumer.Subscribe("topic-a");
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(
                KafkaFaultOperation.ShareConsume,
                "topic-a",
                partition: 0,
                groupId: "workers"));
        await using var stalePoll = consumer.PollAsync().GetAsyncEnumerator();
        var staleMove = stalePoll.MoveNextAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();

        consumer.Subscribe("topic-b");
        var current = await consumer.PollAsync().FirstAsync();
        await Assert.That(barrier.Release()).IsTrue();

        await Assert.That(await staleMove).IsFalse();
        await Assert.That(current.Topic).IsEqualTo("topic-b");
        await Assert.That(current.Offset).IsEqualTo(0);
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
        var topicPartition = new TopicPartition("shared", 0);
        var beforeRetry = cluster.GetShareGroupOffsets("workers");
        await consumer.CommitAsync();

        await Assert.That(actual).IsSameReferenceAs(failure);
        await Assert.That(beforeRetry.ContainsKey(topicPartition)).IsFalse();
        await Assert.That(cluster.GetShareGroupOffsets("workers")[topicPartition])
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
        var topicPartition = new TopicPartition("shared", 0);
        await Assert.That(cluster.GetShareGroupOffsets("workers").ContainsKey(topicPartition)).IsFalse();
        await Assert.That(barrier.Release()).IsTrue();

        await consumer.CommitAsync();
        await Assert.That(cluster.GetShareGroupOffsets("workers")[topicPartition])
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
        var topicPartition = new TopicPartition("shared", 0);
        await Assert.That(cluster.GetShareGroupOffsets("workers").ContainsKey(topicPartition)).IsFalse();
        await Assert.That(barrier.Release()).IsTrue();
        await Task.WhenAll(first, second);
        await Assert.That(cluster.GetShareGroupOffsets("workers")[topicPartition])
            .IsEqualTo(1);
    }

    [Test]
    public async Task ShareAcknowledgeBarrier_AllowsConcurrentIdempotentClose()
    {
        var cluster = new InMemoryKafkaCluster();
        var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync("shared", "key", "value");
        var consumer = new InMemoryShareConsumer<string, string>(
            cluster,
            new InMemoryShareConsumerOptions { GroupId = "workers" });
        consumer.Subscribe("shared");
        consumer.Acknowledge(await consumer.PollAsync().FirstAsync());
        var barrier = cluster.FaultPlan.PauseNext(
            new KafkaFaultScope(KafkaFaultOperation.ShareAcknowledge));

        var first = consumer.CloseAsync().AsTask();
        await barrier.WaitUntilEnteredAsync();
        var concurrent = new Task[8];
        for (var index = 0; index < concurrent.Length; index++)
            concurrent[index] = consumer.CloseAsync().AsTask();

        await Assert.That(barrier.Release()).IsTrue();
        await first;
        await Task.WhenAll(concurrent);
        await consumer.DisposeAsync();
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
        await AssertInvalidAdminRequestDoesNotConsumeFaultAsync<ArgumentOutOfRangeException>(
            invalidOperation,
            validOperation);
    }

    private static async Task AssertInvalidAdminRequestDoesNotConsumeFaultAsync<TException>(
        Func<InMemoryAdminClient, Task> invalidOperation,
        Func<InMemoryAdminClient, Task> validOperation)
        where TException : Exception
    {
        var cluster = new InMemoryKafkaCluster();
        var admin = new InMemoryAdminClient(cluster);
        var failure = new InvalidOperationException("blocked");
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), failure);

        _ = await Assert.ThrowsAsync<TException>(() => invalidOperation(admin));
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
