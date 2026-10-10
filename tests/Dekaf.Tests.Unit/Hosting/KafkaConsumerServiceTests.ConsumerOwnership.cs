using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Hosting;

/// <summary>
/// The hosted service decides record ownership from the real consumer: the record's fetch
/// generation against the partition's current ownership, and the coordinator's pending
/// revocations. A rebalance at any point after the fetch, including between delivery and the
/// start of processing, is seen.
/// </summary>
public sealed partial class KafkaConsumerServiceTests
{
    private static readonly TopicPartition FetchedPartition = new("orders", 1);

    [Test]
    public async Task RealConsumer_RevokedAndReassignedBetweenDeliveryAndProcessing_RoutesAndStoresNothing()
    {
        // The record was delivered, then the partition was revoked and assigned back to this member
        // (both callbacks ran, both syncs completed) before processing started. The record belongs
        // to the ended ownership: a DLQ copy would duplicate the new ownership's, and its stored
        // offset would rewind the new ownership's progress.
        var consumer = CreateGroupConsumer();
        AssignAndInitialize(consumer, FetchedPartition);
        using var fetch = PendingFetchData.Create(FetchedPartition.Topic, FetchedPartition.Partition, Array.Empty<RecordBatch>());
        var record = CreateFetchedRecord(fetch, offset: 42);
        var producer = CreateRoutingProducer();
        await using var service = CreateRebalancingService(consumer, deadLetterOptions: new DeadLetterOptions());
        SetDlqProducer(service, producer);
        var listener = CreatePostponementListener(service);

        RevokeBySync(consumer, FetchedPartition);
        await listener.OnPartitionsRevokedAsync([FetchedPartition], CancellationToken.None);
        AssignAndInitialize(consumer, FetchedPartition);
        await listener.OnPartitionsAssignedAsync([FetchedPartition], CancellationToken.None);

        await ProcessWithRetriesAsync(service, record, CancellationToken.None);

        await producer.DidNotReceive().ProduceAsync(
            Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>());
        await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();
        await Assert.That(consumer.Paused.Contains(FetchedPartition)).IsFalse();
    }

    [Test]
    public async Task RealConsumer_RevocationPendingWhileCopyIsProduced_DoesNotStoreOffset()
    {
        // The coordinator revokes the partition while the DLQ copy is produced; the consumer has
        // not synchronized yet. The revocation commit has run or is running, so an offset stored
        // now would be committed after it, over the next owner's progress.
        var consumer = CreateGroupConsumer();
        AssignAndInitialize(consumer, FetchedPartition);
        using var fetch = PendingFetchData.Create(FetchedPartition.Topic, FetchedPartition.Partition, Array.Empty<RecordBatch>());
        var record = CreateFetchedRecord(fetch, offset: 42);
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                SetCoordinatorAssignment(consumer);
                return new ValueTask<RecordMetadata>(default(RecordMetadata));
            });
        await using var service = CreateRebalancingService(consumer, deadLetterOptions: new DeadLetterOptions());
        SetDlqProducer(service, producer);

        await ProcessWithRetriesAsync(service, record, CancellationToken.None);

        await producer.Received(1).ProduceAsync(
            Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>());
        await Assert.That(GetDirtyStoredOffsets(consumer)).IsEmpty();
    }

    [Test]
    public async Task RealConsumer_RecordOfCurrentOwnership_IsRoutedAndStored()
    {
        var consumer = CreateGroupConsumer();
        AssignAndInitialize(consumer, FetchedPartition);
        using var fetch = PendingFetchData.Create(FetchedPartition.Topic, FetchedPartition.Partition, Array.Empty<RecordBatch>());
        var record = CreateFetchedRecord(fetch, offset: 42);
        var producer = CreateRoutingProducer();
        await using var service = CreateRebalancingService(consumer, deadLetterOptions: new DeadLetterOptions());
        SetDlqProducer(service, producer);

        await ProcessWithRetriesAsync(service, record, CancellationToken.None);

        await producer.Received(1).ProduceAsync(
            Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>());
        await Assert.That(GetDirtyStoredOffsets(consumer)[FetchedPartition]).IsEqualTo(43);
    }

    [Test]
    [Arguments("retryPolicy")]
    [Arguments("deadLetter")]
    public async Task DecoratedConsumer_PartitionRevokedAndSynchronizedDuringProcessing_StopsRetryingRoutingAndStoring(
        string failureHandling)
    {
        // An application's decorator cannot implement the consumer's internal ownership interface:
        // the service falls back to the decorator's public Assignment.
        await using var inner = CreateGroupConsumer();
        AssignAndInitialize(inner, FetchedPartition);
        var consumer = Decorate(inner);
        using var fetch = PendingFetchData.Create(FetchedPartition.Topic, FetchedPartition.Partition, Array.Empty<RecordBatch>());
        var producer = CreateRoutingProducer();
        await using var service = CreateRebalancingService(
            consumer,
            retryPolicy: failureHandling == "retryPolicy" ? new BackoffPolicy(TimeSpan.FromHours(1)) : null,
            deadLetterOptions: failureHandling == "deadLetter" ? new DeadLetterOptions() : null);
        if (failureHandling == "deadLetter")
            SetDlqProducer(service, producer);
        service.DuringProcessing = () =>
        {
            RevokeBySync(inner, FetchedPartition);
            return ValueTask.CompletedTask;
        };

        await ProcessWithRetriesAsync(service, CreateFetchedRecord(fetch, offset: 42), CancellationToken.None)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(service.Attempts).IsEqualTo(1);
        await producer.DidNotReceive().ProduceAsync(
            Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>());
        await Assert.That(GetDirtyStoredOffsets(inner)).IsEmpty();
    }

    [Test]
    public async Task DecoratedConsumer_RevocationPendingDuringProcessing_WrappedConsumerStillRefusesTheStore()
    {
        // A decorator cannot report a revocation that is not synchronized yet, so the service
        // still routes the record; the wrapped consumer's own check keeps its offset from being
        // stored for the revoked partition.
        await using var inner = CreateGroupConsumer();
        AssignAndInitialize(inner, FetchedPartition);
        var consumer = Decorate(inner);
        using var fetch = PendingFetchData.Create(FetchedPartition.Topic, FetchedPartition.Partition, Array.Empty<RecordBatch>());
        var producer = CreateRoutingProducer();
        await using var service = CreateRebalancingService(consumer, deadLetterOptions: new DeadLetterOptions());
        SetDlqProducer(service, producer);
        service.DuringProcessing = () =>
        {
            SetCoordinatorAssignment(inner);
            return ValueTask.CompletedTask;
        };

        await ProcessWithRetriesAsync(service, CreateFetchedRecord(fetch, offset: 42), CancellationToken.None);

        await producer.Received(1).ProduceAsync(
            Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>());
        await Assert.That(GetDirtyStoredOffsets(inner)).IsEmpty();
    }

    [Test]
    [NotInParallel("HostedOwnershipSeam")]
    public async Task RealConsumer_RevokedAndReassignedBetweenOwnershipCheckAndRewind_DoesNotRewindTheNewOwnership()
    {
        // The failure path finds the revocation pending and would rewind to the record; before it
        // acts, the partition is assigned back and synchronized. The old record's offset must not
        // be sought into, nor the partition paused for, the new ownership.
        await using var inner = CreateGroupConsumer();
        AssignAndInitialize(inner, FetchedPartition);
        using var fetch = PendingFetchData.Create(FetchedPartition.Topic, FetchedPartition.Partition, Array.Empty<RecordBatch>());
        var producer = CreateRoutingProducer();
        await using var service = CreateRebalancingService(inner, deadLetterOptions: new DeadLetterOptions());
        SetDlqProducer(service, producer);
        service.DuringProcessing = () =>
        {
            SetCoordinatorAssignment(inner);
            return ValueTask.CompletedTask;
        };
        var reassigned = 0;
        Action<object> reassignBeforeRewind = instance =>
        {
            if (!ReferenceEquals(instance, service) || Interlocked.Exchange(ref reassigned, 1) != 0)
                return;

            RevokeBySync(inner, FetchedPartition);
            AssignAndInitialize(inner, FetchedPartition);
        };

        Dekaf.Extensions.Hosting.KafkaConsumerService<string, string>.AfterOwnershipCheckedForTest += reassignBeforeRewind;
        try
        {
            await ProcessWithRetriesAsync(service, CreateFetchedRecord(fetch, offset: 42), CancellationToken.None);
        }
        finally
        {
            Dekaf.Extensions.Hosting.KafkaConsumerService<string, string>.AfterOwnershipCheckedForTest -= reassignBeforeRewind;
        }

        await Assert.That(Volatile.Read(ref reassigned)).IsEqualTo(1);
        await Assert.That(inner.GetPosition(FetchedPartition)).IsEqualTo(0);
        await Assert.That(inner.Paused.Contains(FetchedPartition)).IsFalse();
        await producer.DidNotReceive().ProduceAsync(
            Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An application's decorator: public interfaces only, forwarding to <paramref name="inner"/>.
    /// </summary>
    private static IKafkaConsumer<string, string> Decorate(KafkaConsumer<string, string> inner)
    {
        var consumer = Substitute.For<IKafkaConsumer<string, string>, IConsumerOffsetStoreTimingConfiguration>();
        var configuration = (IConsumerOffsetStoreTimingConfiguration)consumer;
        configuration.OffsetCommitMode.Returns(OffsetCommitMode.Manual);
        configuration.EnableAutoOffsetStore.Returns(false);
        configuration.HasConsumerGroup.Returns(true);
        configuration.StoresOffsetsOnDelivery.Returns(false);
        consumer.Assignment.Returns(_ => inner.Assignment);
        consumer.Positions.Returns(_ => inner.Positions);
        consumer.Partitions.Returns(_ => inner.Partitions);
        consumer
            .When(decorated => decorated.StoreOffset(Arg.Any<ConsumeResult<string, string>>()))
            .Do(call => inner.StoreOffset(call.Arg<ConsumeResult<string, string>>()));
        return consumer;
    }

    /// <summary>A subscribed, strict manual-store group consumer that never reaches a broker.</summary>
    private static KafkaConsumer<string, string> CreateGroupConsumer()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "hosted-ownership",
                OffsetCommitMode = OffsetCommitMode.Manual,
                EnableAutoOffsetStore = false
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        consumer.Subscribe(FetchedPartition.Topic);
        return consumer;
    }

    /// <summary>As the coordinator and assignment sync leave an owned partition.</summary>
    private static void AssignAndInitialize(KafkaConsumer<string, string> consumer, TopicPartition partition)
    {
        SetCoordinatorAssignment(consumer, partition);
        GetConsumerAssignment(consumer).Add(partition);
        PublishAsSync(consumer, GetConsumerCoordinator(consumer));
        InvokeConsumer(consumer, "SetPosition", partition, 0L, false);
        InvokeConsumer(consumer, "SetFetchPosition", partition, 0L);
        CompleteSync(consumer);
    }

    /// <summary>The revocation part of assignment sync.</summary>
    private static void RevokeBySync(KafkaConsumer<string, string> consumer, TopicPartition partition)
    {
        SetCoordinatorAssignment(consumer);
        GetConsumerAssignment(consumer).Remove(partition);
        PublishAsSync(consumer, GetConsumerCoordinator(consumer));
        InvokeConsumer(consumer, "RemovePartitionState", new[] { partition }, null);
        CompleteSync(consumer);
    }

    /// <summary>Assignment sync has applied every coordinator change.</summary>
    private static void CompleteSync(KafkaConsumer<string, string> consumer)
        => typeof(KafkaConsumer<string, string>)
            .GetField("_lastCoordinatorAssignmentVersion", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(consumer, GetConsumerCoordinator(consumer).AssignmentVersion);

    private static ConsumerCoordinator GetConsumerCoordinator(KafkaConsumer<string, string> consumer)
        => (ConsumerCoordinator)typeof(KafkaConsumer<string, string>)
            .GetField("_coordinator", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer)!;

    /// <summary>The coordinator's assignment, which a heartbeat changes (with a new version) before sync.</summary>
    private static void SetCoordinatorAssignment(KafkaConsumer<string, string> consumer, params TopicPartition[] partitions)
    {
        var coordinator = GetConsumerCoordinator(consumer);
        var assigned = typeof(ConsumerCoordinator)
            .GetField("_assignedPartitions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        // A heartbeat publishes the revocation of dropped partitions before the new assignment.
        var revoked = ((HashSet<TopicPartition>)assigned.GetValue(coordinator)!)
            .Where(partition => !partitions.Contains(partition))
            .ToList();
        if (revoked.Count != 0)
            RevokeInCoordinator(coordinator, revoked);

        assigned.SetValue(coordinator, new HashSet<TopicPartition>(partitions));
        var version = typeof(ConsumerCoordinator)
            .GetField("_assignmentVersion", BindingFlags.NonPublic | BindingFlags.Instance)!;
        version.SetValue(coordinator, (int)version.GetValue(coordinator)! + 1);
    }

    /// <summary>
    /// The coordinator revokes partitions: it publishes the revocation (taking its generation),
    /// then enqueues it for assignment sync.
    /// </summary>
    private static void RevokeInCoordinator(ConsumerCoordinator coordinator, List<TopicPartition> revoked)
    {
        var (sequence, generation) = ((long, long))typeof(ConsumerCoordinator)
            .GetMethod("NotifyRevoking", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(coordinator, [revoked])!;
        typeof(ConsumerCoordinator)
            .GetMethod("EnqueueRevokedPartitions", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(coordinator, [revoked, generation, sequence]);
    }

    /// <summary>The publication of assignment sync: publish, then apply the drained revocations.</summary>
    private static void PublishSynchronized(
        KafkaConsumer<string, string> consumer,
        TopicPartition[]? reassigned,
        IReadOnlyDictionary<TopicPartition, long>? drained)
    {
        typeof(KafkaConsumer<string, string>)
            .GetMethod("PublishAssignmentSnapshotCore", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(consumer, [reassigned]);
        if (drained is not null)
        {
            typeof(KafkaConsumer<string, string>)
                .GetMethod("ForgetDrainedRevocations", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(consumer, [drained]);
        }
    }

    /// <summary>Assignment sync: drains the coordinator's revocations and publishes, applying them.</summary>
    private static void PublishAsSync(KafkaConsumer<string, string> consumer, ConsumerCoordinator coordinator)
    {
        coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        PublishSynchronized(consumer, null, coordinator.DrainedRevocationGenerations);
    }

    private static HashSet<TopicPartition> GetConsumerAssignment(KafkaConsumer<string, string> consumer)
        => (HashSet<TopicPartition>)typeof(KafkaConsumer<string, string>)
            .GetField("_assignment", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(consumer)!;

    private static void InvokeConsumer(KafkaConsumer<string, string> consumer, string method, params object?[] arguments)
        => typeof(KafkaConsumer<string, string>)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == arguments.Length)
            .Invoke(consumer, arguments);

    private static IReadOnlyDictionary<TopicPartition, long> GetDirtyStoredOffsets(
        KafkaConsumer<string, string> consumer)
        => consumer.DirtyStoredOffsetsForTest;

    /// <summary>A record as the consumer delivers it from <paramref name="fetch"/>.</summary>
    private static ConsumeResult<string, string> CreateFetchedRecord(PendingFetchData fetch, long offset)
        => new(
            fetch.Topic,
            fetch.PartitionIndex,
            offset,
            ReadOnlyMemory<byte>.Empty,
            isKeyNull: true,
            ReadOnlyMemory<byte>.Empty,
            isValueNull: true,
            pooledHeaders: null,
            pooledHeaderCount: 0,
            headerOwner: fetch,
            timestampMs: 0,
            TimestampType.NotAvailable,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);
}
