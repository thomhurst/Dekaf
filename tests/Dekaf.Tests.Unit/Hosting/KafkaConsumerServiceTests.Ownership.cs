using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.Hosting;
using Dekaf.Producer;
using Dekaf.Retry;
using NSubstitute;

namespace Dekaf.Tests.Unit.Hosting;

/// <summary>
/// The consumer runs rebalance callbacks and synchronizes its assignment on the prefetch loop,
/// concurrently with record processing. A failure that resolves after the record's partition
/// was revoked must not pause, rewind, or store offsets for a partition another member (or a
/// newer ownership of this one) now consumes.
/// </summary>
public sealed partial class KafkaConsumerServiceTests
{
    private static readonly TopicPartition OwnedPartition = new("orders", 1);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Redeliver_PartitionRevokedAndSynchronizedDuringProcessing_LeavesPartitionUntouched(bool lost)
    {
        var (consumer, assignment) = CreateOwnershipConsumer(OwnedPartition);
        await using var service = CreateRebalancingService(consumer);
        var listener = CreatePostponementListener(service);
        service.DuringProcessing = async () =>
        {
            await RevokeAsync(listener, lost, OwnedPartition);
            assignment.Remove(OwnedPartition);
        };

        await ProcessOwnedRecordAsync(service);

        consumer.Partitions.DidNotReceive().Pause(Arg.Any<TopicPartition[]>());
        consumer.Positions.DidNotReceive().Seek(Arg.Any<TopicPartitionOffset>());
        await Assert.That(GetPostponedPartitions(service)).IsEmpty();
        await Assert.That(GetRedeliveries(service)).IsEmpty();
    }

    [Test]
    public async Task Redeliver_RevocationSynchronizedBeforeItsCallback_LeavesPartitionUntouched()
    {
        // Assignment synchronization waits for the revocation commit, not for the listeners,
        // so the snapshot can drop the partition before the service hears of the revocation.
        var (consumer, assignment) = CreateOwnershipConsumer(OwnedPartition);
        await using var service = CreateRebalancingService(consumer);
        service.DuringProcessing = () =>
        {
            assignment.Remove(OwnedPartition);
            return ValueTask.CompletedTask;
        };

        await ProcessOwnedRecordAsync(service);

        consumer.Partitions.DidNotReceive().Pause(Arg.Any<TopicPartition[]>());
        consumer.Positions.DidNotReceive().Seek(Arg.Any<TopicPartitionOffset>());
        await Assert.That(GetPostponedPartitions(service)).IsEmpty();
        await Assert.That(GetRedeliveries(service)).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Redeliver_PartitionRevokedButNotYetSynchronized_RewindsWithoutSchedulingResume(bool lost)
    {
        // The member owns the partition until the consumer synchronizes the revocation, so the
        // rewind still keeps the record uncommitted; the synchronization drops pause and position.
        // A postponement recorded after the revocation callback would outlive it.
        var (consumer, _) = CreateOwnershipConsumer(OwnedPartition);
        await using var service = CreateRebalancingService(consumer);
        var listener = CreatePostponementListener(service);
        service.DuringProcessing = () => RevokeAsync(listener, lost, OwnedPartition);

        await ProcessOwnedRecordAsync(service);

        consumer.Positions.Received(1).Seek(Arg.Is<TopicPartitionOffset>(offset =>
            offset.Topic == OwnedPartition.Topic && offset.Partition == OwnedPartition.Partition && offset.Offset == 42));
        await Assert.That(GetPostponedPartitions(service)).IsEmpty();
        await Assert.That(GetRedeliveries(service)).IsEmpty();
    }

    [Test]
    public async Task Redeliver_PartitionRevokedAndReassignedDuringProcessing_DoesNotRewindNewOwnership()
    {
        var (consumer, _) = CreateOwnershipConsumer(OwnedPartition);
        await using var service = CreateRebalancingService(consumer);
        var listener = CreatePostponementListener(service);
        service.DuringProcessing = async () =>
        {
            await listener.OnPartitionsRevokedAsync([OwnedPartition], CancellationToken.None);
            await listener.OnPartitionsAssignedAsync([OwnedPartition], CancellationToken.None);
        };

        await ProcessOwnedRecordAsync(service);

        consumer.Partitions.DidNotReceive().Pause(Arg.Any<TopicPartition[]>());
        consumer.Positions.DidNotReceive().Seek(Arg.Any<TopicPartitionOffset>());
        await Assert.That(GetPostponedPartitions(service)).IsEmpty();
    }

    [Test]
    public async Task DecoratedConsumer_RevocationSynchronizedDuringPause_UndoesPause()
    {
        // A Dekaf consumer decides and applies a rewind in one step; a decorator cannot, so the
        // service undoes a pause whose partition left the decorator's assignment meanwhile.
        var consumer = Substitute.For<IKafkaConsumer<string, string>, IConsumerOffsetStoreTimingConfiguration>();
        var assignment = new HashSet<TopicPartition> { OwnedPartition };
        consumer.Assignment.Returns(_ => assignment);
        consumer.Positions.Returns(Substitute.For<IConsumerPositions>());
        consumer.Partitions.Returns(Substitute.For<IConsumerPartitions>());
        consumer.Partitions
            .When(partitions => partitions.Pause(Arg.Any<TopicPartition[]>()))
            .Do(_ => assignment.Remove(OwnedPartition));
        await using var service = CreateRebalancingService(consumer);

        await ProcessOwnedRecordAsync(service);

        consumer.Partitions.Received(1).Resume(Arg.Is<TopicPartition[]>(partitions =>
            partitions.Length == 1 && partitions[0] == OwnedPartition));
        await Assert.That(GetPostponedPartitions(service)).IsEmpty();
        await Assert.That(GetRedeliveries(service)).IsEmpty();
    }

    [Test]
    public async Task Redeliver_OtherPartitionRevokedDuringProcessing_StillPostponesOwnedPartition()
    {
        var other = new TopicPartition("orders", 2);
        var (consumer, assignment) = CreateOwnershipConsumer(OwnedPartition, other);
        await using var service = CreateRebalancingService(consumer);
        var listener = CreatePostponementListener(service);
        service.DuringProcessing = async () =>
        {
            await listener.OnPartitionsRevokedAsync([other], CancellationToken.None);
            assignment.Remove(other);
        };

        await ProcessOwnedRecordAsync(service);

        consumer.Partitions.Received(1).Pause(Arg.Is<TopicPartition[]>(partitions =>
            partitions.Length == 1 && partitions[0] == OwnedPartition));
        consumer.Positions.Received(1).Seek(Arg.Is<TopicPartitionOffset>(offset =>
            offset.Topic == OwnedPartition.Topic && offset.Partition == OwnedPartition.Partition && offset.Offset == 42));
        await Assert.That(GetPostponedPartitions(service)).IsEquivalentTo([OwnedPartition]);
        await Assert.That(GetRedeliveries(service)).IsEquivalentTo([(OwnedPartition, 42L, 1)]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Discard_StrictManualStore_StoresOffsetOnlyWhileOwned(bool revoked)
    {
        var (consumer, assignment) = CreateOwnershipConsumer(OwnedPartition);
        consumer.OffsetCommitMode = OffsetCommitMode.Manual;
        consumer.EnableAutoOffsetStore = false;
        consumer.HasConsumerGroup = true;
        await using var service = CreateRebalancingService(consumer, MessageFailureDisposition.Discard);
        var listener = CreatePostponementListener(service);
        if (revoked)
        {
            service.DuringProcessing = async () =>
            {
                await listener.OnPartitionsRevokedAsync([OwnedPartition], CancellationToken.None);
                assignment.Remove(OwnedPartition);
            };
        }

        await ProcessOwnedRecordAsync(service);

        consumer.Inner.Received(revoked ? 0 : 1).StoreOffset(Arg.Any<ConsumeResult<string, string>>());
    }

    [Test]
    public async Task OnPartitionsAssigned_DropsPostponementLeftFromEarlierOwnership()
    {
        // An unobserved revocation (or one observed before the postponement was recorded) can
        // leave a postponement behind. Its delayed resume must not resume the new ownership,
        // which may have been paused by the application.
        var (consumer, _) = CreateOwnershipConsumer(OwnedPartition);
        await using var service = CreateRebalancingService(consumer);
        var listener = CreatePostponementListener(service);
        await ProcessOwnedRecordAsync(service);
        var postponements = GetPostponementsDictionary(service);
        var stale = postponements[OwnedPartition]!;

        await listener.OnPartitionsAssignedAsync([OwnedPartition], CancellationToken.None);

        await Assert.That(postponements.Count).IsEqualTo(0);
        var complete = typeof(KafkaConsumerService<string, string>).GetMethod(
            "TryCompletePartitionPostponement",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        await Assert.That((bool)complete.Invoke(service, [OwnedPartition, stale])!).IsFalse();
        consumer.Partitions.DidNotReceive().Resume(Arg.Any<TopicPartition[]>());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RetryPolicy_PartitionRevokedDuringFirstAttempt_StopsRetrying(bool lost)
    {
        // Without the ownership check the hour-long backoff starts, and the next owner
        // processes the record as well.
        var (consumer, assignment) = CreateOwnershipConsumer(OwnedPartition);
        var policy = new BackoffPolicy(TimeSpan.FromHours(1));
        await using var service = CreateRebalancingService(consumer, retryPolicy: policy);
        var listener = CreatePostponementListener(service);
        service.DuringProcessing = async () =>
        {
            await RevokeAsync(listener, lost, OwnedPartition);
            assignment.Remove(OwnedPartition);
        };

        await ProcessOwnedRecordAsync(service).WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(service.Attempts).IsEqualTo(1);
        consumer.Partitions.DidNotReceive().Pause(Arg.Any<TopicPartition[]>());
        consumer.Positions.DidNotReceive().Seek(Arg.Any<TopicPartitionOffset>());
        await Assert.That(GetPostponedPartitions(service)).IsEmpty();
    }

    [Test]
    [Arguments("revoked")]
    [Arguments("lost")]
    [Arguments("reassigned")]
    public async Task RetryPolicy_PartitionOwnershipChangesDuringBackoff_EndsBackoffWithoutAnotherAttempt(string change)
    {
        var (consumer, assignment) = CreateOwnershipConsumer(OwnedPartition);
        var policy = new BackoffPolicy(TimeSpan.FromHours(1));
        await using var service = CreateRebalancingService(consumer, retryPolicy: policy);
        var listener = CreatePostponementListener(service);
        using var stopping = new CancellationTokenSource();
        var processing = StartOwnedRecord(service, stopping.Token);
        await policy.DelayRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));

        switch (change)
        {
            case "revoked":
                assignment.Remove(OwnedPartition);
                await listener.OnPartitionsRevokedAsync([OwnedPartition], CancellationToken.None);
                break;
            case "lost":
                assignment.Remove(OwnedPartition);
                await listener.OnPartitionsLostAsync([OwnedPartition], CancellationToken.None);
                break;
            case "reassigned":
                // Both rebalances completed before the woken backoff checks ownership.
                consumer.ReassignedSinceFetch.Add(OwnedPartition);
                await listener.OnPartitionsRevokedAsync([OwnedPartition], CancellationToken.None);
                await listener.OnPartitionsAssignedAsync([OwnedPartition], CancellationToken.None);
                break;
        }

        try
        {
            await processing.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await stopping.CancelAsync();
        }

        await Assert.That(service.Attempts).IsEqualTo(1);
        consumer.Partitions.DidNotReceive().Pause(Arg.Any<TopicPartition[]>());
        consumer.Positions.DidNotReceive().Seek(Arg.Any<TopicPartitionOffset>());
        await Assert.That(GetPostponedPartitions(service)).IsEmpty();
        await Assert.That(GetRetryBackoff(service)).IsNull();
    }

    [Test]
    public async Task RetryPolicy_PartitionRevokedButNotYetSynchronizedDuringBackoff_RewindsWithoutAnotherAttempt()
    {
        // The member still owns the position until the consumer synchronizes the revocation:
        // the rewind keeps a commit before then from moving past the abandoned record.
        var (consumer, _) = CreateOwnershipConsumer(OwnedPartition);
        var policy = new BackoffPolicy(TimeSpan.FromHours(1));
        await using var service = CreateRebalancingService(consumer, retryPolicy: policy);
        var listener = CreatePostponementListener(service);
        using var stopping = new CancellationTokenSource();
        var processing = StartOwnedRecord(service, stopping.Token);
        await policy.DelayRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await listener.OnPartitionsRevokedAsync([OwnedPartition], CancellationToken.None);
        try
        {
            await processing.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await stopping.CancelAsync();
        }

        await Assert.That(service.Attempts).IsEqualTo(1);
        consumer.Positions.Received(1).Seek(Arg.Is<TopicPartitionOffset>(offset =>
            offset.Topic == OwnedPartition.Topic && offset.Partition == OwnedPartition.Partition && offset.Offset == 42));
        await Assert.That(GetPostponedPartitions(service)).IsEmpty();
        await Assert.That(GetRedeliveries(service)).IsEmpty();
    }

    [Test]
    public async Task RetryPolicy_OtherPartitionRevokedDuringBackoff_KeepsRetrying()
    {
        var other = new TopicPartition("orders", 2);
        var (consumer, assignment) = CreateOwnershipConsumer(OwnedPartition, other);
        var policy = new BackoffPolicy(TimeSpan.FromMilliseconds(200));
        await using var service = CreateRebalancingService(consumer, retryPolicy: policy);
        service.SucceedFromAttempt = 2;
        var listener = CreatePostponementListener(service);
        var processing = StartOwnedRecord(service, CancellationToken.None);
        await policy.DelayRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await listener.OnPartitionsRevokedAsync([other], CancellationToken.None);
        assignment.Remove(other);
        await processing.WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(service.Attempts).IsEqualTo(2);
        consumer.Positions.DidNotReceive().Seek(Arg.Any<TopicPartitionOffset>());
    }

    [Test]
    public async Task RetryPolicy_ShutdownDuringBackoff_PropagatesCancellationWithoutRewind()
    {
        var (consumer, _) = CreateOwnershipConsumer(OwnedPartition);
        var policy = new BackoffPolicy(TimeSpan.FromHours(1));
        await using var service = CreateRebalancingService(consumer, retryPolicy: policy);
        using var stopping = new CancellationTokenSource();
        var processing = StartOwnedRecord(service, stopping.Token);
        await policy.DelayRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await stopping.CancelAsync();

        await Assert.That(async () => await processing.WaitAsync(TimeSpan.FromSeconds(10)))
            .Throws<OperationCanceledException>();
        await Assert.That(service.Attempts).IsEqualTo(1);
        consumer.Positions.DidNotReceive().Seek(Arg.Any<TopicPartitionOffset>());
        await Assert.That(GetRetryBackoff(service)).IsNull();
    }

    [Test]
    public async Task MaxFailuresWithoutPolicy_PartitionRevokedDuringFirstAttempt_StopsRetryingAndDoesNotDeadLetter()
    {
        var (consumer, assignment) = CreateOwnershipConsumer(OwnedPartition);
        var producer = CreateRoutingProducer();
        await using var service = CreateRebalancingService(
            consumer,
            deadLetterOptions: new DeadLetterOptions { MaxFailures = 3 });
        SetDlqProducer(service, producer);
        var listener = CreatePostponementListener(service);
        service.DuringProcessing = async () =>
        {
            await listener.OnPartitionsRevokedAsync([OwnedPartition], CancellationToken.None);
            assignment.Remove(OwnedPartition);
        };

        await ProcessOwnedRecordAsync(service);

        await Assert.That(service.Attempts).IsEqualTo(1);
        await producer.DidNotReceive().ProduceAsync(
            Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task DurableRouting_PartitionRevokedDuringProcessing_ProducesNoCopyAndStoresNothing(
        bool retryTopics,
        bool synchronized)
    {
        // The next owner routes the record itself, so a copy from here would be a duplicate.
        var (consumer, assignment) = CreateOwnershipConsumer(OwnedPartition);
        ConfigureStrictManualStore(consumer);
        var producer = CreateRoutingProducer();
        await using var service = CreateRebalancingService(
            consumer,
            deadLetterOptions: CreateRoutingOptions(retryTopics));
        SetDlqProducer(service, producer);
        var listener = CreatePostponementListener(service);
        service.DuringProcessing = async () =>
        {
            await listener.OnPartitionsRevokedAsync([OwnedPartition], CancellationToken.None);
            if (synchronized)
                assignment.Remove(OwnedPartition);
        };

        await ProcessOwnedRecordAsync(service);

        await producer.DidNotReceive().ProduceAsync(
            Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>());
        consumer.Inner.DidNotReceive().StoreOffset(Arg.Any<ConsumeResult<string, string>>());
        // Not yet synchronized: rewound so nothing commits past the record before then.
        consumer.Positions.Received(synchronized ? 0 : 1).Seek(Arg.Any<TopicPartitionOffset>());
        await Assert.That(GetPostponedPartitions(service)).IsEmpty();
    }

    [Test]
    [Arguments(false, true)]
    [Arguments(true, true)]
    [Arguments(false, false)]
    [Arguments(true, false)]
    public async Task DurableRouting_PartitionRevokedWhileCopyIsProduced_DoesNotStoreOffset(bool retryTopics, bool synchronized)
    {
        // The window the routing check cannot close: the copy was produced, so the next owner
        // may produce a second one (at-least-once), but the offset is not stored. Also while the
        // revocation awaits synchronization: the revocation commit has run or is running, so a
        // stored offset would be committed after it.
        var (consumer, assignment) = CreateOwnershipConsumer(OwnedPartition);
        ConfigureStrictManualStore(consumer);
        IRebalanceListener? listener = null;
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(_ => RevokeWhileProducingAsync());
        await using var service = CreateRebalancingService(
            consumer,
            deadLetterOptions: CreateRoutingOptions(retryTopics));
        SetDlqProducer(service, producer);
        listener = CreatePostponementListener(service);

        await ProcessOwnedRecordAsync(service);

        await producer.Received(1).ProduceAsync(
            Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>());
        consumer.Inner.DidNotReceive().StoreOffset(Arg.Any<ConsumeResult<string, string>>());

        async ValueTask<RecordMetadata> RevokeWhileProducingAsync()
        {
            await listener!.OnPartitionsRevokedAsync([OwnedPartition], CancellationToken.None);
            if (synchronized)
                assignment.Remove(OwnedPartition);
            return default;
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DurableRouting_WhileOwned_ProducesCopyAndStoresOffset(bool retryTopics)
    {
        var (consumer, _) = CreateOwnershipConsumer(OwnedPartition);
        ConfigureStrictManualStore(consumer);
        var producer = CreateRoutingProducer();
        await using var service = CreateRebalancingService(
            consumer,
            deadLetterOptions: CreateRoutingOptions(retryTopics));
        SetDlqProducer(service, producer);

        await ProcessOwnedRecordAsync(service);

        await producer.Received(1).ProduceAsync(
            Arg.Is<ProducerMessage<byte[]?, byte[]?>>(message =>
                message.Topic == (retryTopics ? "orders-retry-1s" : "orders.DLQ")),
            Arg.Any<CancellationToken>());
        consumer.Inner.Received(1).StoreOffset(Arg.Any<ConsumeResult<string, string>>());
    }

    [Test]
    [Arguments(MessageFailureDisposition.Redeliver)]
    [Arguments(MessageFailureDisposition.Discard)]
    public async Task AutoCommitConsumer_PartitionRevokedButNotYetSynchronized_KeepsFailedRecordUncommittedOnlyForRedeliver(
        MessageFailureDisposition disposition)
    {
        // An auto-commit consumer may commit before the revocation is synchronized. Redeliver
        // rewinds so that commit holds the failed record; Discard acknowledges it as while owned.
        var (consumer, _) = CreateOwnershipConsumer(OwnedPartition);
        consumer.OffsetCommitMode = OffsetCommitMode.Auto;
        consumer.EnableAutoOffsetStore = true;
        consumer.HasConsumerGroup = true;
        await using var service = CreateRebalancingService(consumer, disposition);
        var listener = CreatePostponementListener(service);
        service.DuringProcessing = () => listener.OnPartitionsRevokedAsync([OwnedPartition], CancellationToken.None);

        await ProcessOwnedRecordAsync(service);

        consumer.Positions.Received(disposition == MessageFailureDisposition.Redeliver ? 1 : 0)
            .Seek(Arg.Any<TopicPartitionOffset>());
        consumer.Inner.DidNotReceive().StoreOffset(Arg.Any<ConsumeResult<string, string>>());
        await Assert.That(GetPostponedPartitions(service)).IsEmpty();
    }

    [Test]
    [Arguments("redeliver")]
    [Arguments("retryPolicy")]
    [Arguments("deadLetter")]
    [Arguments("retryTopic")]
    public async Task RevocationCallbackBetweenDeliveryAndProcessing_IsNotMistakenForOwnership(string failureHandling)
    {
        // The consumer delivered the record, then the revocation callback ran before the service
        // read the ownership epoch: the record arrived under the revoked ownership even though
        // its epoch is the revocation's. The assignment snapshot still holds the partition.
        var (consumer, _) = CreateOwnershipConsumer(OwnedPartition);
        var producer = CreateRoutingProducer();
        await using var service = CreateRebalancingService(
            consumer,
            retryPolicy: failureHandling == "retryPolicy" ? new BackoffPolicy(TimeSpan.FromHours(1)) : null,
            deadLetterOptions: failureHandling switch
            {
                "deadLetter" => CreateRoutingOptions(retryTopics: false),
                "retryTopic" => CreateRoutingOptions(retryTopics: true),
                _ => null
            });
        if (failureHandling is "deadLetter" or "retryTopic")
            SetDlqProducer(service, producer);
        var listener = CreatePostponementListener(service);
        await listener.OnPartitionsRevokedAsync([OwnedPartition], CancellationToken.None);

        await ProcessOwnedRecordAsync(service).WaitAsync(TimeSpan.FromSeconds(10));

        await Assert.That(service.Attempts).IsEqualTo(1);
        await producer.DidNotReceive().ProduceAsync(
            Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>());
        // Still owned until synchronized: rewound so nothing commits past the record meanwhile,
        // but no postponement outlives the revocation.
        consumer.Positions.Received(1).Seek(Arg.Is<TopicPartitionOffset>(offset =>
            offset.Topic == OwnedPartition.Topic && offset.Partition == OwnedPartition.Partition && offset.Offset == 42));
        await Assert.That(GetPostponedPartitions(service)).IsEmpty();
        await Assert.That(GetRedeliveries(service)).IsEmpty();
    }

    private static Task StartOwnedRecord(RebalancingFailureService service, CancellationToken stoppingToken)
        => ProcessWithRetriesAsync(
            service,
            CreateResult(OwnedPartition.Topic, OwnedPartition.Partition, 42),
            stoppingToken).AsTask();

    private static DeadLetterOptions CreateRoutingOptions(bool retryTopics)
        => retryTopics
            ? new DeadLetterOptions { RetryTopics = new RetryTopicOptions { Delays = [TimeSpan.FromSeconds(1)] } }
            : new DeadLetterOptions();

    private static IKafkaProducer<byte[]?, byte[]?> CreateRoutingProducer()
    {
        var producer = Substitute.For<IKafkaProducer<byte[]?, byte[]?>>();
        producer.ProduceAsync(Arg.Any<ProducerMessage<byte[]?, byte[]?>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<RecordMetadata>(default(RecordMetadata)));
        return producer;
    }

    private static void ConfigureStrictManualStore(RebalanceTestConsumer consumer)
    {
        consumer.OffsetCommitMode = OffsetCommitMode.Manual;
        consumer.EnableAutoOffsetStore = false;
        consumer.HasConsumerGroup = true;
    }

    private static object? GetRetryBackoff(KafkaConsumerService<string, string> service)
        => typeof(KafkaConsumerService<string, string>)
            .GetField("_retryBackoff", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service);

    /// <summary>Retries after the given delay on the first failure only.</summary>
    private sealed class BackoffPolicy(TimeSpan delay) : IRetryPolicy
    {
        public TaskCompletionSource DelayRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TimeSpan? GetNextDelay(int attemptNumber, Exception exception)
        {
            if (attemptNumber > 1)
                return null;

            DelayRequested.TrySetResult();
            return delay;
        }
    }

    private static (RebalanceTestConsumer Consumer, HashSet<TopicPartition> Assignment) CreateOwnershipConsumer(
        params TopicPartition[] assigned)
    {
        var consumer = new RebalanceTestConsumer { Register = _ => Substitute.For<IDisposable>() };
        var assignment = new HashSet<TopicPartition>(assigned);
        consumer.Inner.Assignment.Returns(_ => assignment);
        consumer.OwnershipAssignment = assignment;
        consumer.Inner.Positions.Returns(Substitute.For<IConsumerPositions>());
        consumer.Inner.Partitions.Returns(Substitute.For<IConsumerPartitions>());
        return (consumer, assignment);
    }

    private static RebalancingFailureService CreateRebalancingService(
        IKafkaConsumer<string, string> consumer,
        MessageFailureDisposition disposition = MessageFailureDisposition.Redeliver,
        IRetryPolicy? retryPolicy = null,
        DeadLetterOptions? deadLetterOptions = null)
        => new(consumer, disposition, retryPolicy, deadLetterOptions);

    private static async Task ProcessOwnedRecordAsync(RebalancingFailureService service)
    {
        // Backoffs are an hour long, so no delayed resume runs while the test asserts.
        using var stopping = new CancellationTokenSource();
        try
        {
            await ProcessWithRetriesAsync(
                service,
                CreateResult(OwnedPartition.Topic, OwnedPartition.Partition, 42),
                stopping.Token);
        }
        finally
        {
            await stopping.CancelAsync();
        }
    }

    private static ValueTask RevokeAsync(IRebalanceListener listener, bool lost, TopicPartition partition)
        => lost
            ? listener.OnPartitionsLostAsync([partition], CancellationToken.None)
            : listener.OnPartitionsRevokedAsync([partition], CancellationToken.None);

    private static IRebalanceListener CreatePostponementListener(KafkaConsumerService<string, string> service)
    {
        // The same listener ExecuteAsync registers with the consumer.
        var listenerType = typeof(KafkaConsumerService<,>)
            .GetNestedType("PostponementRebalanceListener", BindingFlags.NonPublic)!
            .MakeGenericType(typeof(string), typeof(string));
        var listener = (IRebalanceListener)Activator.CreateInstance(listenerType, service)!;
        var consumer = typeof(KafkaConsumerService<string, string>)
            .GetField("_consumer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service);
        return consumer is RebalanceTestConsumer { OwnershipAssignment: not null } ownership
            ? new CoordinatorEmulatingListener(listener, ownership)
            : listener;
    }

    /// <summary>
    /// The coordinator publishes a revocation before any revocation callback runs, and an
    /// assignment of a revoked partition starts a new ownership the in-flight record predates.
    /// </summary>
    private sealed class CoordinatorEmulatingListener(IRebalanceListener inner, RebalanceTestConsumer consumer)
        : IRebalanceListener
    {
        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            foreach (var partition in partitions)
            {
                if (consumer.PendingRevocations.Remove(partition))
                    consumer.ReassignedSinceFetch.Add(partition);
            }

            return inner.OnPartitionsAssignedAsync(partitions, cancellationToken);
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            consumer.PendingRevocations.UnionWith(partitions);
            return inner.OnPartitionsRevokedAsync(partitions, cancellationToken);
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            consumer.PendingRevocations.UnionWith(partitions);
            return inner.OnPartitionsLostAsync(partitions, cancellationToken);
        }
    }

    private static System.Collections.IDictionary GetPostponementsDictionary(KafkaConsumerService<string, string> service)
        => (System.Collections.IDictionary)typeof(KafkaConsumerService<string, string>)
            .GetField("_postponements", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;

    private static List<TopicPartition> GetPostponedPartitions(KafkaConsumerService<string, string> service)
        => GetPostponementsDictionary(service).Keys.Cast<TopicPartition>().ToList();

    private sealed class RebalancingFailureService(
        IKafkaConsumer<string, string> consumer,
        MessageFailureDisposition disposition,
        IRetryPolicy? retryPolicy = null,
        DeadLetterOptions? deadLetterOptions = null)
        : TestableKafkaConsumerService(
            consumer,
            ["orders"],
            new KafkaConsumerServiceOptions
            {
                PollRetryBackoff = TimeSpan.FromHours(1),
                MaxPollRetryBackoff = TimeSpan.FromHours(1)
            },
            deadLetterOptions,
            retryPolicy)
    {
        private int _attempts;

        /// <summary>Runs inside the first ProcessAsync, before the record fails: a rebalance mid-processing.</summary>
        public Func<ValueTask>? DuringProcessing { get; set; }

        /// <summary>The attempt from which processing succeeds; 0 means it always fails.</summary>
        public int SucceedFromAttempt { get; set; }

        public int Attempts => Volatile.Read(ref _attempts);

        protected override async ValueTask ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _attempts);
            if (attempt == 1 && DuringProcessing is { } duringProcessing)
                await duringProcessing();

            if (SucceedFromAttempt > 0 && attempt >= SucceedFromAttempt)
                return;

            throw new InvalidOperationException("Processing failed");
        }

        protected override ValueTask<MessageFailureDisposition> GetFailureDispositionAsync(
            MessageFailureContext<string, string> context,
            CancellationToken cancellationToken)
            => new(disposition);
    }
}
