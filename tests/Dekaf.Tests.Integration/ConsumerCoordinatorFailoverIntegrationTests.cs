using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;
using ConfluentKafka = Confluent.Kafka;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
[Category("Resilience")]
[NotInParallel("RackAwareKafkaContainer")]
[ClassDataSource<RackAwareKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ConsumerCoordinatorFailoverIntegrationTests(RackAwareKafkaContainer kafka)
{
    private const int PartitionCount = 6;
    private const int MessagesPerPartition = 20;
    private const int MessageCount = PartitionCount * MessagesPerPartition;
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CoordinatorRecoveryTimeout = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Bound for a group to settle on a complete, non-overlapping assignment, and for consumers
    /// to drain the expected records. Every wait here polls a condition that a failed rebalance
    /// or a wedged consumer never satisfies; unbounded, such a wait burned the whole 240s test
    /// timeout and reported nothing but "timed out". Each bounded wait fails with the state it
    /// was stuck on instead, and three of them still fit inside the test timeout.
    /// </summary>
    private static readonly TimeSpan ConvergenceTimeout = TimeSpan.FromSeconds(60);

    [Test]
    [Timeout(240_000)]
    public async Task CoordinatorFailover_CommitsResumeWithoutLossOrDuplicates(
        CancellationToken cancellationToken)
    {
        var groupId = $"coordinator-failover-{Guid.NewGuid():N}";
        var (topic, expectedCoordinatorId) = await CreateScenarioAsync(groupId, cancellationToken)
            .ConfigureAwait(false);
        int? stoppedBrokerId = null;
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var records = new ConcurrentDictionary<(int Partition, long Offset), int>();

        await using var first = await CreateConsumerAsync(groupId, listener: null, cancellationToken)
            .ConfigureAwait(false);
        await using var second = await CreateConsumerAsync(groupId, listener: null, cancellationToken)
            .ConfigureAwait(false);
        first.Subscribe(topic);
        second.Subscribe(topic);
        var consumeTasks = new[]
        {
            ConsumeAndCommitAsync(first, records, scenarioCancellation.Token),
            ConsumeAndCommitAsync(second, records, scenarioCancellation.Token)
        };

        try
        {
            await ProduceRangeAsync(topic, startPerPartition: 0, countPerPartition: 5, cancellationToken)
                .ConfigureAwait(false);
            await WaitForProgressOrFailureAsync(
                    records,
                    expectedCount: PartitionCount * 5,
                    consumeTasks,
                    cancellationToken)
                .ConfigureAwait(false);

            stoppedBrokerId = await kafka.GetGroupCoordinatorIdAsync(groupId, cancellationToken)
                .ConfigureAwait(false);
            AssertExpectedCoordinator(stoppedBrokerId.Value, expectedCoordinatorId);
            await kafka.StopBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            _ = await kafka.WaitForGroupCoordinatorChangeAsync(
                    groupId,
                    stoppedBrokerId.Value,
                    cancellationToken)
                .ConfigureAwait(false);

            await ProduceRangeAsync(topic, startPerPartition: 5, countPerPartition: 15, cancellationToken)
                .ConfigureAwait(false);
            await WaitForProgressOrFailureAsync(records, MessageCount, consumeTasks, cancellationToken)
                .ConfigureAwait(false);

            await kafka.StartBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            stoppedBrokerId = null;

            scenarioCancellation.Cancel();
            await ObserveCancellationAsync(consumeTasks).ConfigureAwait(false);
            AssertCompleteSequences(records);
            await AssertCommittedOffsetsAsync(topic, groupId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            scenarioCancellation.Cancel();
            await ObserveCancellationAsync(consumeTasks).ConfigureAwait(false);
            if (stoppedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(240_000)]
    public async Task CoordinatorFailover_HeartbeatsRediscoverAndConverge(
        CancellationToken cancellationToken)
    {
        var groupId = $"coordinator-heartbeat-{Guid.NewGuid():N}";
        var (topic, expectedCoordinatorId) = await CreateScenarioAsync(groupId, cancellationToken)
            .ConfigureAwait(false);
        var firstListener = new AssignmentListener();
        var secondListener = new AssignmentListener();
        int? stoppedBrokerId = null;
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await using var first = await CreateConsumerAsync(groupId, firstListener, cancellationToken)
            .ConfigureAwait(false);
        await using var second = await CreateConsumerAsync(groupId, secondListener, cancellationToken)
            .ConfigureAwait(false);
        first.Subscribe(topic);
        second.Subscribe(topic);
        var pollTasks = new[]
        {
            PollAsync(first, scenarioCancellation.Token),
            PollAsync(second, scenarioCancellation.Token)
        };

        try
        {
            await WaitForStableAssignmentAsync(firstListener, secondListener, pollTasks, cancellationToken)
                .ConfigureAwait(false);
            var revocationsBefore = firstListener.RevocationCount + secondListener.RevocationCount;
            stoppedBrokerId = await kafka.GetGroupCoordinatorIdAsync(groupId, cancellationToken)
                .ConfigureAwait(false);
            AssertExpectedCoordinator(stoppedBrokerId.Value, expectedCoordinatorId);

            await kafka.StopBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            _ = await kafka.WaitForGroupCoordinatorChangeAsync(
                    groupId,
                    stoppedBrokerId.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            await AssertMembersSurviveCoordinatorMoveAsync(groupId, pollTasks, cancellationToken)
                .ConfigureAwait(false);
            await WaitForStableAssignmentAsync(firstListener, secondListener, pollTasks, cancellationToken)
                .ConfigureAwait(false);

            var revocationsAfter = firstListener.RevocationCount + secondListener.RevocationCount;
            AssertNoRebalanceStorm(revocationsBefore, revocationsAfter);

            await kafka.StartBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            stoppedBrokerId = null;
        }
        finally
        {
            scenarioCancellation.Cancel();
            await ObserveCancellationAsync(pollTasks).ConfigureAwait(false);
            if (stoppedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(240_000)]
    public async Task CoordinatorFailover_DuringMemberJoin_Converges(
        CancellationToken cancellationToken)
    {
        var groupId = $"coordinator-join-{Guid.NewGuid():N}";
        var (topic, expectedCoordinatorId) = await CreateScenarioAsync(groupId, cancellationToken)
            .ConfigureAwait(false);
        var firstListener = new AssignmentListener();
        var secondListener = new AssignmentListener();
        int? stoppedBrokerId = null;
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await using var first = await CreateConsumerAsync(groupId, firstListener, cancellationToken)
            .ConfigureAwait(false);
        first.Subscribe(topic);
        var firstPoll = PollAsync(first, scenarioCancellation.Token);
        Task? secondPoll = null;
        IKafkaConsumer<string, string>? second = null;

        try
        {
            await WaitForAssignmentCountAsync(firstListener, PartitionCount, [firstPoll], cancellationToken)
                .ConfigureAwait(false);
            stoppedBrokerId = await kafka.GetGroupCoordinatorIdAsync(groupId, cancellationToken)
                .ConfigureAwait(false);
            AssertExpectedCoordinator(stoppedBrokerId.Value, expectedCoordinatorId);
            await kafka.StopBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);

            second = await CreateConsumerAsync(groupId, secondListener, cancellationToken)
                .ConfigureAwait(false);
            second.Subscribe(topic);
            secondPoll = PollAsync(second, scenarioCancellation.Token);
            _ = await kafka.WaitForGroupCoordinatorChangeAsync(
                    groupId,
                    stoppedBrokerId.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            await WaitForStableAssignmentAsync(
                    firstListener,
                    secondListener,
                    [firstPoll, secondPoll],
                    cancellationToken)
                .ConfigureAwait(false);

            var overlap = firstListener.Assignment.Intersect(secondListener.Assignment).ToArray();
            await Assert.That(overlap).IsEmpty();

            await kafka.StartBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            stoppedBrokerId = null;
        }
        finally
        {
            scenarioCancellation.Cancel();
            await ObserveCancellationAsync(secondPoll is null ? [firstPoll] : [firstPoll, secondPoll])
                .ConfigureAwait(false);
            if (second is not null)
                await second.DisposeAsync().ConfigureAwait(false);
            if (stoppedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(240_000)]
    [SkipWhenNativeAot("Confluent.Kafka native delegate binding requires runtime reflection.")]
    public async Task ClassicCoordinatorFailover_CommitsResumeWithoutLossOrDuplicates(
        CancellationToken cancellationToken)
    {
        var groupId = $"classic-coordinator-failover-{Guid.NewGuid():N}";
        var (topic, expectedCoordinatorId) = await CreateScenarioAsync(groupId, cancellationToken)
            .ConfigureAwait(false);
        int? stoppedBrokerId = null;
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var records = new ConcurrentDictionary<(int Partition, long Offset), int>();
        using var first = CreateClassicConsumer(groupId);
        using var second = CreateClassicConsumer(groupId);
        first.Subscribe(topic);
        second.Subscribe(topic);
        var consumeTasks = new[]
        {
            ConsumeAndCommitClassicAsync(first, records, scenarioCancellation.Token),
            ConsumeAndCommitClassicAsync(second, records, scenarioCancellation.Token)
        };

        try
        {
            await ProduceRangeAsync(topic, startPerPartition: 0, countPerPartition: 5, cancellationToken)
                .ConfigureAwait(false);
            await WaitForProgressOrFailureAsync(
                    records,
                    expectedCount: PartitionCount * 5,
                    consumeTasks,
                    cancellationToken)
                .ConfigureAwait(false);

            stoppedBrokerId = await kafka.GetGroupCoordinatorIdAsync(groupId, cancellationToken)
                .ConfigureAwait(false);
            AssertExpectedCoordinator(stoppedBrokerId.Value, expectedCoordinatorId);
            await kafka.StopBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            _ = await kafka.WaitForGroupCoordinatorChangeAsync(
                    groupId,
                    stoppedBrokerId.Value,
                    cancellationToken)
                .ConfigureAwait(false);

            await ProduceRangeAsync(topic, startPerPartition: 5, countPerPartition: 15, cancellationToken)
                .ConfigureAwait(false);
            await WaitForProgressOrFailureAsync(records, MessageCount, consumeTasks, cancellationToken)
                .ConfigureAwait(false);

            await kafka.StartBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            stoppedBrokerId = null;

            scenarioCancellation.Cancel();
            await ObserveCancellationAsync(consumeTasks).ConfigureAwait(false);
            AssertCompleteSequences(records);
            await AssertCommittedOffsetsAsync(topic, groupId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            scenarioCancellation.Cancel();
            await ObserveCancellationAsync(consumeTasks).ConfigureAwait(false);
            if (stoppedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(240_000)]
    [SkipWhenNativeAot("Confluent.Kafka native delegate binding requires runtime reflection.")]
    public async Task ClassicCoordinatorFailover_HeartbeatsRediscoverAndConverge(
        CancellationToken cancellationToken)
    {
        var groupId = $"classic-coordinator-heartbeat-{Guid.NewGuid():N}";
        var (topic, expectedCoordinatorId) = await CreateScenarioAsync(groupId, cancellationToken)
            .ConfigureAwait(false);
        var firstListener = new AssignmentListener();
        var secondListener = new AssignmentListener();
        int? stoppedBrokerId = null;
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var first = CreateClassicConsumer(groupId, firstListener);
        using var second = CreateClassicConsumer(groupId, secondListener);
        first.Subscribe(topic);
        second.Subscribe(topic);
        var pollTasks = new[]
        {
            PollClassicAsync(first, scenarioCancellation.Token),
            PollClassicAsync(second, scenarioCancellation.Token)
        };

        // librdkafka owns the truth about what each classic consumer holds. Deriving it from
        // the rebalance callbacks instead can wedge the wait forever on a set that a missed
        // or reordered callback left stale, with nothing but a test timeout to show for it.
        var firstAssignment = () => ClassicAssignment(first);
        var secondAssignment = () => ClassicAssignment(second);

        try
        {
            await WaitForStableAssignmentAsync(
                    firstAssignment, secondAssignment, pollTasks, cancellationToken)
                .ConfigureAwait(false);
            var revocationsBefore = firstListener.RevocationCount + secondListener.RevocationCount;
            stoppedBrokerId = await kafka.GetGroupCoordinatorIdAsync(groupId, cancellationToken)
                .ConfigureAwait(false);
            AssertExpectedCoordinator(stoppedBrokerId.Value, expectedCoordinatorId);

            await kafka.StopBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            _ = await kafka.WaitForGroupCoordinatorChangeAsync(
                    groupId,
                    stoppedBrokerId.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            await AssertMembersSurviveCoordinatorMoveAsync(groupId, pollTasks, cancellationToken)
                .ConfigureAwait(false);
            await WaitForStableAssignmentAsync(
                    firstAssignment, secondAssignment, pollTasks, cancellationToken)
                .ConfigureAwait(false);

            var revocationsAfter = firstListener.RevocationCount + secondListener.RevocationCount;
            AssertNoRebalanceStorm(revocationsBefore, revocationsAfter);

            await kafka.StartBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            stoppedBrokerId = null;
        }
        finally
        {
            scenarioCancellation.Cancel();
            await ObserveCancellationAsync(pollTasks).ConfigureAwait(false);
            if (stoppedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Test]
    [Timeout(240_000)]
    [SkipWhenNativeAot("Confluent.Kafka native delegate binding requires runtime reflection.")]
    public async Task ClassicCoordinatorFailover_DuringMemberJoin_Converges(
        CancellationToken cancellationToken)
    {
        var groupId = $"classic-coordinator-join-{Guid.NewGuid():N}";
        var (topic, expectedCoordinatorId) = await CreateScenarioAsync(groupId, cancellationToken)
            .ConfigureAwait(false);
        var firstListener = new AssignmentListener();
        var secondListener = new AssignmentListener();
        int? stoppedBrokerId = null;
        using var scenarioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var first = CreateClassicConsumer(groupId, firstListener);
        first.Subscribe(topic);
        var firstPoll = PollClassicAsync(first, scenarioCancellation.Token);
        Task? secondPoll = null;
        ConfluentKafka.IConsumer<string, string>? second = null;

        try
        {
            // As above: read the assignments from librdkafka, not from the callbacks.
            await WaitForAssignmentCountAsync(
                    () => ClassicAssignment(first), PartitionCount, [firstPoll], cancellationToken)
                .ConfigureAwait(false);
            stoppedBrokerId = await kafka.GetGroupCoordinatorIdAsync(groupId, cancellationToken)
                .ConfigureAwait(false);
            AssertExpectedCoordinator(stoppedBrokerId.Value, expectedCoordinatorId);
            await kafka.StopBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);

            second = CreateClassicConsumer(groupId, secondListener);
            second.Subscribe(topic);
            secondPoll = PollClassicAsync(second, scenarioCancellation.Token);
            _ = await kafka.WaitForGroupCoordinatorChangeAsync(
                    groupId,
                    stoppedBrokerId.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            await WaitForStableAssignmentAsync(
                    () => ClassicAssignment(first),
                    () => ClassicAssignment(second!),
                    [firstPoll, secondPoll],
                    cancellationToken)
                .ConfigureAwait(false);

            var overlap = ClassicAssignment(first).Intersect(ClassicAssignment(second)).ToArray();
            await Assert.That(overlap).IsEmpty();

            await kafka.StartBrokerAsync(stoppedBrokerId.Value, cancellationToken).ConfigureAwait(false);
            stoppedBrokerId = null;
        }
        finally
        {
            scenarioCancellation.Cancel();
            await ObserveCancellationAsync(secondPoll is null ? [firstPoll] : [firstPoll, secondPoll])
                .ConfigureAwait(false);
            second?.Dispose();
            if (stoppedBrokerId is { } brokerId)
                await kafka.StartBrokerAsync(brokerId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<(string Topic, int CoordinatorId)> CreateScenarioAsync(
        string groupId,
        CancellationToken cancellationToken)
    {
        var coordinatorId = await kafka.FindGroupCoordinatorIdAsync(groupId, cancellationToken)
            .ConfigureAwait(false);
        var topic = await kafka.CreateDistributedReplicatedTopicAsync(
                PartitionCount,
                excludedLeaderId: coordinatorId)
            .ConfigureAwait(false);
        return (topic, coordinatorId);
    }

    private static void AssertExpectedCoordinator(int actualCoordinatorId, int expectedCoordinatorId)
    {
        if (actualCoordinatorId != expectedCoordinatorId)
        {
            throw new InvalidOperationException(
                $"FindCoordinator changed before failover: expected broker {expectedCoordinatorId}, " +
                $"actual broker {actualCoordinatorId}.");
        }
    }

    private static void AssertNoRebalanceStorm(int revocationsBefore, int revocationsAfter)
    {
        var failoverRevocations = revocationsAfter - revocationsBefore;
        if (failoverRevocations > 2)
        {
            throw new InvalidOperationException(
                $"Coordinator failover caused a rebalance storm: {failoverRevocations} revocations.");
        }
    }

    private async Task<IKafkaConsumer<string, string>> CreateConsumerAsync(
        string groupId,
        IRebalanceListener? listener,
        CancellationToken cancellationToken)
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithGroupId(groupId)
            .WithClientId($"coordinator-failover-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithSessionTimeout(SessionTimeout)
            .WithHeartbeatInterval(TimeSpan.FromSeconds(1))
            .WithDefaultApiTimeout(TimeSpan.FromSeconds(45))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());
        if (listener is not null)
            builder.WithRebalanceListener(listener);

        return await builder.BuildAsync(cancellationToken).ConfigureAwait(false);
    }

    private ConfluentKafka.IConsumer<string, string> CreateClassicConsumer(
        string groupId,
        AssignmentListener? listener = null)
    {
        var builder = new ConfluentKafka.ConsumerBuilder<string, string>(new ConfluentKafka.ConsumerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            GroupId = groupId,
            ClientId = $"classic-coordinator-failover-{Guid.NewGuid():N}",
            GroupProtocol = ConfluentKafka.GroupProtocol.Classic,
            PartitionAssignmentStrategy = ConfluentKafka.PartitionAssignmentStrategy.Range,
            AutoOffsetReset = ConfluentKafka.AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            SessionTimeoutMs = (int)SessionTimeout.TotalMilliseconds,
            HeartbeatIntervalMs = 1_000
        });
        if (listener is not null)
        {
            builder
                .SetPartitionsAssignedHandler((_, partitions) => listener.Assign(
                    partitions.Select(static partition =>
                        new TopicPartition(partition.Topic, partition.Partition.Value))))
                .SetPartitionsRevokedHandler((_, partitions) => listener.Revoke(
                    partitions.Select(static partition =>
                        new TopicPartition(partition.Topic, partition.Partition.Value))))
                .SetPartitionsLostHandler((_, partitions) => listener.Lose(
                    partitions.Select(static partition =>
                        new TopicPartition(partition.Topic, partition.Partition.Value))));
        }

        return builder.Build();
    }

    private async Task ProduceRangeAsync(
        string topic,
        int startPerPartition,
        int countPerPartition,
        CancellationToken cancellationToken)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithAcks(Acks.All)
            .WithIdempotence(true)
            .WithRequestTimeout(TimeSpan.FromSeconds(5))
            .WithDeliveryTimeout(TimeSpan.FromSeconds(60))
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);

        for (var partition = 0; partition < PartitionCount; partition++)
        {
            for (var offset = startPerPartition; offset < startPerPartition + countPerPartition; offset++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = partition,
                    Key = $"{partition}:{offset}",
                    Value = offset.ToString()
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        await producer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ConsumeAndCommitAsync(
        IKafkaConsumer<string, string> consumer,
        ConcurrentDictionary<(int Partition, long Offset), int> records,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(2), cancellationToken)
                .ConfigureAwait(false);
            if (result is not { } record)
                continue;

            records.AddOrUpdate((record.Partition, record.Offset), 1, static (_, count) => count + 1);
            await consumer.CommitAsync([
                new TopicPartitionOffset(record.Topic, record.Partition, record.Offset + 1)
            ], cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task ConsumeAndCommitClassicAsync(
        ConfluentKafka.IConsumer<string, string> consumer,
        ConcurrentDictionary<(int Partition, long Offset), int> records,
        CancellationToken cancellationToken) =>
        Task.Factory.StartNew(
            () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = ConsumeClassicTolerant(consumer, TimeSpan.FromMilliseconds(200));
                    if (result is null)
                        continue;

                    if (!CommitClassicWithRetry(
                            consumer,
                            result.TopicPartitionOffset,
                            cancellationToken))
                    {
                        continue;
                    }

                    records.AddOrUpdate(
                        (result.Partition.Value, result.Offset.Value),
                        1,
                        static (_, count) => count + 1);
                }
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

    private static ConfluentKafka.ConsumeResult<string, string>? ConsumeClassicTolerant(
        ConfluentKafka.IConsumer<string, string> consumer,
        TimeSpan timeout)
    {
        try
        {
            return consumer.Consume(timeout);
        }
        catch (ConfluentKafka.ConsumeException exception)
            when (IsUnknownTopicError(exception.Error.Code))
        {
            // Metadata for the freshly created topic has not reached every broker yet.
            // librdkafka surfaces this as an error event but keeps refreshing metadata,
            // so treat it like an empty poll instead of a fatal consume failure.
            return null;
        }
    }

    private static bool CommitClassicWithRetry(
        ConfluentKafka.IConsumer<string, string> consumer,
        ConfluentKafka.TopicPartitionOffset consumed,
        CancellationToken cancellationToken)
    {
        var committed = new ConfluentKafka.TopicPartitionOffset(
            consumed.TopicPartition,
            consumed.Offset + 1);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                consumer.Commit([committed]);
                return true;
            }
            catch (ConfluentKafka.TopicPartitionOffsetException exception)
                when (exception.Results.All(static result =>
                    IsTransientCoordinatorError(result.Error.Code)))
            {
                Thread.Sleep(50);
            }
            catch (ConfluentKafka.KafkaException exception)
                when (IsTransientCoordinatorError(exception.Error.Code))
            {
                Thread.Sleep(50);
            }
            catch (ConfluentKafka.KafkaException exception)
                when (IsRejoinError(exception.Error.Code))
            {
                try
                {
                    consumer.Seek(consumed);
                }
                catch (Exception seekException) when (
                    seekException is ConfluentKafka.KafkaException or InvalidOperationException)
                {
                    // An eager rebalance already revoked the partition. Its next owner resumes
                    // from the last successful commit, so this record will be delivered again.
                }

                return false;
            }
        }
    }

    private static bool IsUnknownTopicError(ConfluentKafka.ErrorCode errorCode) =>
        errorCode.ToString() is
            "UnknownTopicOrPart" or
            "Local_UnknownTopic";

    private static bool IsTransientCoordinatorError(ConfluentKafka.ErrorCode errorCode) =>
        errorCode.ToString() is
            "CoordinatorLoadInProgress" or
            "CoordinatorNotAvailable" or
            "NotCoordinator" or
            "Local_AllBrokersDown" or
            "Local_Transport" or
            "Local_TimedOut" or
            "Local_WaitCoord";

    private static bool IsRejoinError(ConfluentKafka.ErrorCode errorCode) =>
        errorCode.ToString() is
            "IllegalGeneration" or
            "UnknownMemberId" or
            "RebalanceInProgress";

    private static async Task PollAsync(
        IKafkaConsumer<string, string> consumer,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
            _ = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
    }

    private static Task PollClassicAsync(
        ConfluentKafka.IConsumer<string, string> consumer,
        CancellationToken cancellationToken) =>
        Task.Factory.StartNew(
            () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                    _ = ConsumeClassicTolerant(consumer, TimeSpan.FromMilliseconds(200));
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

    /// <summary>
    /// Polls <paramref name="condition"/> until it holds, one of the background tasks faults,
    /// or <see cref="ConvergenceTimeout"/> elapses. On timeout the failure carries
    /// <paramref name="describeState"/>, so a stuck group names the state it was stuck in.
    /// </summary>
    private static async Task WaitForConvergenceAsync(
        Func<bool> condition,
        Func<string> describeState,
        IReadOnlyList<Task> backgroundTasks,
        CancellationToken cancellationToken)
    {
        var startedAt = TimeProvider.System.GetTimestamp();
        while (!condition())
        {
            var failed = backgroundTasks.FirstOrDefault(static task => task.IsFaulted);
            if (failed is not null)
                await failed.ConfigureAwait(false);

            if (TimeProvider.System.GetElapsedTime(startedAt) >= ConvergenceTimeout)
            {
                throw new TimeoutException(
                    $"Did not converge within {ConvergenceTimeout.TotalSeconds:F0}s: {describeState()}");
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Task WaitForProgressOrFailureAsync(
        ConcurrentDictionary<(int Partition, long Offset), int> records,
        int expectedCount,
        IReadOnlyList<Task> consumeTasks,
        CancellationToken cancellationToken) =>
        WaitForConvergenceAsync(
            () => records.Count >= expectedCount,
            () => $"consumed {records.Count} of {expectedCount} expected records",
            consumeTasks,
            cancellationToken);

    /// <summary>
    /// Waits until the two members between them own every partition exactly once. The
    /// assignment snapshots are taken from the consumers themselves where the client exposes
    /// them, so a missed rebalance callback cannot leave the wait chasing a stale set.
    /// </summary>
    private static Task WaitForStableAssignmentAsync(
        AssignmentListener first,
        AssignmentListener second,
        IReadOnlyList<Task> pollTasks,
        CancellationToken cancellationToken) =>
        WaitForStableAssignmentAsync(
            () => first.Assignment, () => second.Assignment, pollTasks, cancellationToken);

    private static Task WaitForAssignmentCountAsync(
        AssignmentListener listener,
        int expectedCount,
        IReadOnlyList<Task> pollTasks,
        CancellationToken cancellationToken) =>
        WaitForAssignmentCountAsync(
            () => listener.Assignment, expectedCount, pollTasks, cancellationToken);

    private static Task WaitForStableAssignmentAsync(
        Func<HashSet<TopicPartition>> first,
        Func<HashSet<TopicPartition>> second,
        IReadOnlyList<Task> pollTasks,
        CancellationToken cancellationToken) =>
        WaitForConvergenceAsync(
            () =>
            {
                var firstAssignment = first();
                var secondAssignment = second();
                return firstAssignment.Count + secondAssignment.Count == PartitionCount
                       && !firstAssignment.Overlaps(secondAssignment);
            },
            () => $"assignment never settled on {PartitionCount} distinct partitions " +
                  $"(first: [{Describe(first())}], second: [{Describe(second())}])",
            pollTasks,
            cancellationToken);

    private static string Describe(IEnumerable<TopicPartition> assignment) =>
        string.Join(", ", assignment.Select(static tp => tp.Partition).Order());

    private static HashSet<TopicPartition> ClassicAssignment(
        ConfluentKafka.IConsumer<string, string> consumer) =>
        consumer.Assignment
            .Select(static topicPartition =>
                new TopicPartition(topicPartition.Topic, topicPartition.Partition.Value))
            .ToHashSet();

    private async Task AssertMembersSurviveCoordinatorMoveAsync(
        string groupId,
        IReadOnlyList<Task> pollTasks,
        CancellationToken cancellationToken)
    {
        await using var admin = kafka.CreateAdminClient();
        var startedAt = TimeProvider.System.GetTimestamp();
        var memberCount = 0;

        while (TimeProvider.System.GetElapsedTime(startedAt) < CoordinatorRecoveryTimeout)
        {
            var failed = pollTasks.FirstOrDefault(static task => task.IsFaulted);
            if (failed is not null)
                await failed.ConfigureAwait(false);

            var groups = await admin.DescribeConsumerGroupsAsync([groupId], cancellationToken)
                .ConfigureAwait(false);
            memberCount = groups.TryGetValue(groupId, out var group) ? group.Members.Count : 0;
            if (memberCount == 2)
                return;

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Expected both group members to survive coordinator failover; actual count {memberCount}.");
    }

    private static Task WaitForAssignmentCountAsync(
        Func<HashSet<TopicPartition>> assignment,
        int expectedCount,
        IReadOnlyList<Task> pollTasks,
        CancellationToken cancellationToken) =>
        WaitForConvergenceAsync(
            () => assignment().Count == expectedCount,
            () => $"expected {expectedCount} assigned partitions, actual [{Describe(assignment())}]",
            pollTasks,
            cancellationToken);

    private static async Task ObserveCancellationAsync(IEnumerable<Task> tasks)
    {
        foreach (var task in tasks)
        {
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    private static void AssertCompleteSequences(
        IReadOnlyDictionary<(int Partition, long Offset), int> records)
    {
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            for (var offset = 0; offset < MessagesPerPartition; offset++)
            {
                if (!records.TryGetValue((partition, offset), out var count) || count != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected {partition}:{offset} exactly once; actual count {count}.");
                }
            }
        }
    }

    private async Task AssertCommittedOffsetsAsync(
        string topic,
        string groupId,
        CancellationToken cancellationToken)
    {
        await using var admin = kafka.CreateAdminClient();
        var committed = await admin.ListConsumerGroupOffsetsAsync(groupId, cancellationToken)
            .ConfigureAwait(false);
        for (var partition = 0; partition < PartitionCount; partition++)
        {
            var topicPartition = new TopicPartition(topic, partition);
            if (!committed.TryGetValue(topicPartition, out var offset) || offset != MessagesPerPartition)
            {
                throw new InvalidOperationException(
                    $"Committed offset for {topicPartition} expected {MessagesPerPartition}, actual " +
                    $"{(committed.TryGetValue(topicPartition, out offset) ? offset : -1)}.");
            }
        }
    }

    private sealed class AssignmentListener : IRebalanceListener
    {
        private readonly ConcurrentDictionary<TopicPartition, byte> _assignment = new();
        private int _revocationCount;

        public HashSet<TopicPartition> Assignment => _assignment.Keys.ToHashSet();
        public int RevocationCount => Volatile.Read(ref _revocationCount);

        public void Assign(IEnumerable<TopicPartition> partitions)
        {
            foreach (var partition in partitions)
                _assignment[partition] = 0;
        }

        public void Revoke(IEnumerable<TopicPartition> partitions)
        {
            Interlocked.Increment(ref _revocationCount);
            Remove(partitions);
        }

        public void Lose(IEnumerable<TopicPartition> partitions) => Remove(partitions);

        public ValueTask OnPartitionsAssignedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Assign(partitions);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Revoke(partitions);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Lose(partitions);
            return ValueTask.CompletedTask;
        }

        private void Remove(IEnumerable<TopicPartition> partitions)
        {
            foreach (var partition in partitions)
                _assignment.TryRemove(partition, out _);
        }
    }
}
