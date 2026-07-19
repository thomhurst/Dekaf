using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

// These tests coordinate background consumer work with Task continuations. Running thousands of
// test cases concurrently can starve those continuations long enough to create false timeouts.
[NotInParallel]
public sealed class ConsumerAssignmentFastPathTests
{
    private static readonly Guid TestTopicId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly FieldInfo PollVersionField = typeof(ConsumerCoordinator).GetField(
        "_pollVersion",
        BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("_pollVersion field not found.");
    private static readonly FieldInfo LastPollTimestampField = typeof(ConsumerCoordinator).GetField(
        "_lastPollTimestamp",
        BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("_lastPollTimestamp field not found.");

    [Test]
    [Timeout(120_000)]
    public async Task ConsumeAsync_IdleLoop_RecordsForegroundPollProgress(CancellationToken testTimeout)
    {
        await AssertIdleLoopRecordsPollProgressAsync(
            static (consumer, token) => consumer.ConsumeAsync(token),
            testTimeout);
    }

    [Test]
    [Timeout(120_000)]
    public async Task ConsumeBatchAsync_IdleLoop_RecordsForegroundPollProgress(CancellationToken testTimeout)
    {
        await AssertIdleLoopRecordsPollProgressAsync(
            static (consumer, token) => consumer.ConsumeBatchAsync(token),
            testTimeout);
    }

    [Test]
    [Timeout(120_000)]
    public async Task ConsumeRawBatchAsync_IdleLoop_RecordsForegroundPollProgress(CancellationToken testTimeout)
    {
        await AssertIdleLoopRecordsPollProgressAsync(
            static (consumer, token) => consumer.ConsumeRawBatchAsync(token),
            testTimeout);
    }

    [Test]
    public async Task ConsumeAsync_LargePendingBatch_RefreshesPollProgress()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        await using var metadataManager = CreateMetadataManager(connectionPool);
        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        SetInitialized(consumer);
        consumer.IncrementalAssign([new TopicPartitionOffset("test-topic", 0, 0)]);
        GetPendingFetches(consumer).Enqueue(CreateFetchWithRecords(recordCount: 33));
        var initialPollVersion = GetPollVersion(consumer);
        var consumed = 0;

        await foreach (var _ in consumer.ConsumeAsync())
        {
            if (++consumed == 33)
                break;
        }

        await Assert.That(GetPollVersion(consumer)).IsGreaterThanOrEqualTo(initialPollVersion + 2);
    }

    private static async Task AssertIdleLoopRecordsPollProgressAsync<T>(
        Func<KafkaConsumer<string, string>, CancellationToken, IAsyncEnumerable<T>> consume,
        CancellationToken testTimeout)
    {
        await using var consumer = CreatePausedGroupConsumer();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(testTimeout);
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        await using var enumerator = consume(consumer, cts.Token).GetAsyncEnumerator(testTimeout);
        var moveNext = enumerator.MoveNextAsync().AsTask();

        try
        {
            // Poll progress is driven by the consume loop, but its continuation can be delayed
            // by full-suite ThreadPool contention. Wait for the state transition directly;
            // TUnit's test timeout remains the backstop for a genuine stalled loop.
            await TestWait.UntilAsync(
                () => GetPollVersion(consumer) >= 3,
                testTimeout);
        }
        finally
        {
            cts.Cancel();
            await IgnoreCancellationAsync(moveNext);
        }
    }

    [Test]
    public async Task EnsureAssignmentAsync_UnchangedManualAssignment_SkipsAssignmentLock()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset("topic-a", 0, 10)]);
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var assignmentLock = GetAssignmentLock(consumer);
        await assignmentLock.WaitAsync(CancellationToken.None);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await consumer.EnsureAssignmentAsync(cts.Token);
        }
        finally
        {
            assignmentLock.Release();
        }
    }

    [Test]
    public async Task EnsureAssignmentAsync_ChangedManualAssignment_RequiresAssignmentLock()
    {
        await using var consumer = CreateConsumer();
        consumer.IncrementalAssign([new TopicPartitionOffset("topic-a", 0, 10)]);
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        consumer.IncrementalAssign([new TopicPartitionOffset("topic-a", 1, 20)]);

        var assignmentLock = GetAssignmentLock(consumer);
        await assignmentLock.WaitAsync(CancellationToken.None);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.That(async () => await consumer.EnsureAssignmentAsync(cts.Token))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            assignmentLock.Release();
        }
    }

    [Test]
    public async Task EnsureAssignmentAsync_UnchangedCoordinatorAssignment_SkipsAssignmentLock()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetch(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var assignmentLock = GetAssignmentLock(consumer);
        await assignmentLock.WaitAsync(CancellationToken.None);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await consumer.EnsureAssignmentAsync(cts.Token);
        }
        finally
        {
            assignmentLock.Release();
        }
    }

    [Test]
    public async Task StageRebalanceSeek_SyncedAssignment_DiscardsStalePendingFetch()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetch(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var partition = new TopicPartition("test-topic", 0);
        GetPendingFetches(consumer).Enqueue(
            CreateFetch(partition: 0, baseOffset: 10, value: "stale"));

        consumer.StageRebalanceSeek(new TopicPartitionOffset(partition.Topic, partition.Partition, 42));

        await Assert.That(GetPendingFetches(consumer)).IsEmpty();
        await Assert.That(GetFetchPositions(consumer)[partition]).IsEqualTo(42L);
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(42L);
    }

    [Test]
    public async Task StageRebalanceSeek_RevokedBeforeSync_DiscardsPendingSeek()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        await using var metadataManager = CreateMetadataManager(connectionPool);
        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        var partition = new TopicPartition("test-topic", 0);

        consumer.StageRebalanceSeek(new TopicPartitionOffset(partition.Topic, partition.Partition, 42));
        QueueCoordinatorRevokedPartitionsForFetchClear(consumer, [partition]);

        await Assert.That(consumer.GetRebalancePosition(partition)).IsNull();
    }

    [Test]
    public async Task EnsureAssignmentAsync_ChangedCoordinatorAssignment_RequiresAssignmentLock()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupChangingConsumerGroupHeartbeat(connection);
        SetupOffsetFetch(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        GetCoordinator(consumer).RequestRejoin();

        var assignmentLock = GetAssignmentLock(consumer);
        await assignmentLock.WaitAsync(CancellationToken.None);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.That(async () => await consumer.EnsureAssignmentAsync(cts.Token))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            assignmentLock.Release();
        }

        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(consumer.Assignment).Contains(new TopicPartition("test-topic", 0));
        await Assert.That(consumer.Assignment).Contains(new TopicPartition("test-topic", 1));
    }

    [Test]
    [Timeout(120_000)]
    public async Task EnsureAssignmentForPollAsync_SlowPositionInitialization_DoesNotExpireMember(
        CancellationToken testTimeout)
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetch(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager, maxPollIntervalMs: 100);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var offsetFetchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOffsetFetch = new TaskCompletionSource<OffsetFetchResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0, 1));
        SetupBlockingOffsetFetch(connection, offsetFetchStarted, releaseOffsetFetch);

        var coordinator = GetCoordinator(consumer);
        coordinator.RequestRejoin();
        var assignment = consumer.EnsureAssignmentForPollAsync(CancellationToken.None).AsTask();
        CoordinatorState stateDuringInitialization;
        try
        {
            // The mock is the deterministic synchronization point. A wall-clock cap here races
            // the assignment continuation under full-suite ThreadPool contention; TUnit's test
            // timeout remains the backstop for a genuine failure to start position initialization.
            await offsetFetchStarted.Task.WaitAsync(testTimeout);
            LastPollTimestampField.SetValue(
                coordinator,
                Stopwatch.GetTimestamp() - Stopwatch.Frequency);
            await coordinator.RecordPollAsync(CancellationToken.None);
            stateDuringInitialization = coordinator.State;
        }
        finally
        {
            releaseOffsetFetch.TrySetResult(CreateSuccessfulOffsetFetchResponse());
            await assignment;
        }

        await Assert.That(stateDuringInitialization).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(consumer.Assignment).Contains(new TopicPartition("test-topic", 1));
    }

    [Test]
    public async Task DelayForForegroundPollAsync_EmptyAssignment_DoesNotExpireMember()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment());

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var coordinator = GetCoordinator(consumer);
        await coordinator.StopHeartbeatAsync();
        using var delayCancellation = new CancellationTokenSource();
        var delay = consumer.DelayForForegroundPollAsync(
            Timeout.Infinite,
            delayCancellation.Token).AsTask();
        try
        {
            LastPollTimestampField.SetValue(
                coordinator,
                Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 600L));
            await coordinator.RecordPollAsync(CancellationToken.None);

            await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        }
        finally
        {
            delayCancellation.Cancel();
            await IgnoreCancellationAsync(delay);
        }
    }

    [Test]
    public async Task ConsumeOneAsync_CoordinatorRevocation_ClearsPendingAndPrefetchedRecordsForRemovedPartitions()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupRevokingConsumerGroupHeartbeat(connection);
        SetupOffsetFetch(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager, queuedMinMessages: 2);
        SetInitialized(consumer);
        SetPrefetchStarted(consumer);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var revokedPartition = new TopicPartition("test-topic", 0);
        var retainedPartition = new TopicPartition("test-topic", 1);
        var revokedPending = CreateFetch(partition: 0, baseOffset: 10, value: "revoked-pending");
        var retainedPending = CreateFetch(partition: 1, baseOffset: 20, value: "retained-pending");
        var revokedPrefetched = CreateFetch(partition: 0, baseOffset: 30, value: "revoked-prefetched");
        var retainedPrefetched = CreateFetch(partition: 1, baseOffset: 40, value: "retained-prefetched");

        GetPendingFetches(consumer).Enqueue(revokedPending);
        GetPendingFetches(consumer).Enqueue(retainedPending);
        await Assert.That(GetPrefetchBuffer(consumer).TryWrite(revokedPrefetched)).IsTrue();
        await Assert.That(GetPrefetchBuffer(consumer).TryWrite(retainedPrefetched)).IsTrue();
        SetPrefetchedBytes(
            consumer,
            KafkaConsumer<string, string>.EstimatePendingFetchBytes(revokedPrefetched)
            + KafkaConsumer<string, string>.EstimatePendingFetchBytes(retainedPrefetched));

        GetCoordinator(consumer).RequestRejoin();

        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Partition).IsEqualTo(1);
        await Assert.That(result.Value.Offset).IsEqualTo(20L);
        await Assert.That(result.Value.Value).IsEqualTo("retained-pending");
        await Assert.That(consumer.Assignment).DoesNotContain(revokedPartition);
        await Assert.That(consumer.Assignment).Contains(retainedPartition);
        await Assert.That(GetPrefetchedBytes(consumer)).IsEqualTo(0L);
        await Assert.That(GetPendingFetches(consumer).Any(p => p.TopicPartition == revokedPartition)).IsFalse();
    }

    [Test]
    public async Task EnsureAssignmentAsync_RevokeAndReassignBetweenPolls_InvalidatesPrefetchAndReinitializesPosition()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupAssignmentAbaConsumerGroupHeartbeat(connection);
        SetupOffsetFetch(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var reassignedPartition = new TopicPartition("test-topic", 1);
        var stalePendingFetch = CreateFetch(partition: 1, baseOffset: 0, value: "stale-pending");
        var stalePrefetch = CreateFetch(partition: 1, baseOffset: 1_999_999, value: "stale-prefetch");
        var staleFetchBufferEpoch = GetFetchBufferEpoch(consumer);
        GetPendingFetches(consumer).Enqueue(stalePendingFetch);
        GetFetchPositions(consumer)[reassignedPartition] = 0;
        StageDivergingEpochReset(
            consumer,
            reassignedPartition,
            endOffset: 42,
            epoch: 7,
            staleFetchBufferEpoch);
        CompleteDivergingEpochResets(consumer);

        var coordinator = GetCoordinator(consumer);
        coordinator.RequestRejoin();
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClear(consumer))
            .ContainsKey(reassignedPartition);

        coordinator.RequestRejoin();
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await consumer.EnsureAssignmentAsync(CancellationToken.None);
        await WritePrefetchedItemsAsync(consumer, [stalePrefetch], staleFetchBufferEpoch);

        await Assert.That(consumer.Assignment).Contains(reassignedPartition);
        await Assert.That(GetFetchPositions(consumer)[reassignedPartition]).IsEqualTo(20L);
        await Assert.That(GetPrefetchBuffer(consumer).TryRead(out _)).IsFalse();
        await Assert.That(GetPrefetchedBytes(consumer)).IsEqualTo(0L);
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClear(consumer))
            .ContainsKey(reassignedPartition);

        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(GetPendingFetches(consumer)).IsEmpty();
        await Assert.That(GetLastConsumedLeaderEpoch(consumer, reassignedPartition)).IsEqualTo(5);
    }

    [Test]
    public async Task EnsureAssignmentAsync_RevocationDiscardsPendingDivergenceReset()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupRevokingConsumerGroupHeartbeat(connection);
        SetupOffsetFetch(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var revokedPartition = new TopicPartition("test-topic", 0);
        StageDivergingEpochReset(
            consumer,
            revokedPartition,
            endOffset: 42,
            epoch: 7,
            GetFetchBufferEpoch(consumer));
        CompleteDivergingEpochResets(consumer);

        GetCoordinator(consumer).RequestRejoin();
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(consumer.Assignment).DoesNotContain(revokedPartition);
        await Assert.That(GetFetchPositions(consumer).ContainsKey(revokedPartition)).IsFalse();
    }

    [Test]
    public async Task EnsureAssignmentAsync_AbaStaleMemberEpoch_RetriesRevocationRecovery()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupAssignmentAbaConsumerGroupHeartbeat(connection);
        SetupOffsetFetchFailureAfterInitialAssignment(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var reassignedPartition = new TopicPartition("test-topic", 1);
        var coordinator = GetCoordinator(consumer);
        coordinator.RequestRejoin();
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        coordinator.RequestRejoin();
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(GetFetchPositions(consumer)[reassignedPartition]).IsEqualTo(20L);
    }

    [Test]
    public async Task EnsureAssignmentAsync_InitialPositionStaleMemberEpoch_RetriesUnchangedAssignment()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetchFailureThenSuccess(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");

        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var partition = new TopicPartition("test-topic", 0);
        await Assert.That(consumer.Assignment).Contains(partition);
        await Assert.That(GetFetchPositions(consumer)[partition]).IsEqualTo(10L);
    }

    [Test]
    public async Task EnsureAssignmentAsync_InitialPositionRejoinRestartsChangedAssignment()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupChangingConsumerGroupHeartbeat(connection);
        SetupOffsetFetchFailureThenSuccess(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");

        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var initialPartition = new TopicPartition("test-topic", 0);
        var addedPartition = new TopicPartition("test-topic", 1);
        await Assert.That(consumer.Assignment).Contains(initialPartition);
        await Assert.That(consumer.Assignment).Contains(addedPartition);
        await Assert.That(GetFetchPositions(consumer)[initialPartition]).IsEqualTo(10L);
        await Assert.That(GetFetchPositions(consumer)[addedPartition]).IsEqualTo(20L);
    }

    [Test]
    public async Task GetCommittedOffsetAsync_StaleMemberEpoch_RejoinsAndRetriesWithUpdatedEpoch()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupIncrementingConsumerGroupHeartbeat(connection, CreateAssignment(0));
        var requestedEpochs = new ConcurrentQueue<int>();
        SetupOffsetFetchStaleAfterInitialSuccess(connection, requestedEpochs);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var committed = await consumer.GetCommittedOffsetAsync(
            new TopicPartition("test-topic", 1),
            CancellationToken.None);

        await Assert.That(committed).IsEqualTo(20L);
        await Assert.That(requestedEpochs).IsEquivalentTo([1, 1, 2]);
    }

    [Test]
    public async Task GetCommittedOffsetAsync_StaleMemberEpoch_UsesAggregateRequestTimeout()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        var rejoinStarted = SetupBlockingRejoinConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetchStaleAfterInitialSuccess(connection, new ConcurrentQueue<int>());

        await using var consumer = CreateGroupConsumer(
            connectionPool,
            metadataManager,
            requestTimeoutMs: 100);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        _ = await Assert.That(async () => await consumer.GetCommittedOffsetAsync(
                new TopicPartition("test-topic", 1),
                CancellationToken.None))
            .Throws<KafkaTimeoutException>();
        await Assert.That(rejoinStarted.Task.IsCompleted).IsTrue();
    }

    [Test]
    public async Task GetCommittedOffsetAsync_StaleMemberEpoch_UsesDefaultApiTimeout()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        var rejoinStarted = SetupBlockingRejoinConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetchStaleAfterInitialSuccess(connection, new ConcurrentQueue<int>());

        await using var consumer = CreateGroupConsumer(
            connectionPool,
            metadataManager,
            requestTimeoutMs: 5000,
            defaultApiTimeoutMs: 100);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var exception = await Assert.That(async () => await consumer.GetCommittedOffsetAsync(
                new TopicPartition("test-topic", 1),
                CancellationToken.None))
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.Configured).IsEqualTo(TimeSpan.FromMilliseconds(100));
        await Assert.That(rejoinStarted.Task.IsCompleted).IsTrue();
    }

    [Test]
    public async Task GetCommittedOffsetAsync_StaleMemberEpoch_PreservesCallerCancellation()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        var rejoinStarted = SetupBlockingRejoinConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetchStaleAfterInitialSuccess(connection, new ConcurrentQueue<int>());

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        var committed = consumer.GetCommittedOffsetAsync(
            new TopicPartition("test-topic", 1),
            cancellation.Token).AsTask();
        await rejoinStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        _ = await Assert.That(async () => await committed)
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task EnsureAssignmentAsync_AssignmentChangesBeforeRevocationDrain_UsesMatchingSnapshot()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetch(connection);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var coordinator = GetCoordinator(consumer);
        var revokedPartition = new TopicPartition("test-topic", 0);
        var assignedPartition = new TopicPartition("test-topic", 1);
        InvalidateCoordinatorAssignmentSnapshot(consumer);
        consumer.BeforeCoordinatorAssignmentSnapshotForTest = () =>
        {
            consumer.BeforeCoordinatorAssignmentSnapshotForTest = null;
            ProcessCoordinatorAssignment(coordinator, CreateAssignment(1));
        };

        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        await Assert.That(consumer.Assignment).DoesNotContain(revokedPartition);
        await Assert.That(consumer.Assignment).Contains(assignedPartition);
        await Assert.That(GetFetchPositions(consumer)[assignedPartition]).IsEqualTo(20L);
    }

    [Test]
    public async Task EnsureAssignmentAsync_LateNewPartitionClassification_ReinitializesPosition()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        metadataManager.SetApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetchWithoutCommits(connection);
        var requestedTimestamps = new ConcurrentQueue<long>();
        connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                Arg.Any<ListOffsetsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<ListOffsetsRequest>(0);
                var partition = request.Topics[0].Partitions[0];
                requestedTimestamps.Enqueue(partition.Timestamp);
                var offset = partition.Timestamp == TopicPartitionTimestamp.Earliest ? 0 : 100;
                return ValueTask.FromResult(CreateListOffsetsResponse(partition.PartitionIndex, offset));
            });

        await using var consumer = CreateGroupConsumer(
            connectionPool,
            metadataManager,
            autoOffsetResetNewPartitions: AutoOffsetReset.Earliest);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var partition = new TopicPartition("test-topic", 0);
        await Assert.That(GetFetchPositions(consumer)[partition]).IsEqualTo(100L);
        var staleFetchBufferEpoch = GetFetchBufferEpoch(consumer);
        var stalePendingFetch = CreateFetch(partition: 0, baseOffset: 100, value: "stale-pending");
        var stalePrefetchedFetch = CreateFetch(partition: 0, baseOffset: 101, value: "stale-prefetched");
        GetPendingFetches(consumer).Enqueue(stalePendingFetch);
        await Assert.That(GetPrefetchBuffer(consumer).TryWrite(stalePrefetchedFetch)).IsTrue();
        SetPrefetchedBytes(
            consumer,
            KafkaConsumer<string, string>.EstimatePendingFetchBytes(stalePrefetchedFetch));

        var coordinator = GetCoordinator(consumer);
        ProcessCoordinatorAssignment(
            coordinator,
            CreateAssignmentWithNewPartitions([0], [0]));
        await consumer.EnsureAssignmentAsync(CancellationToken.None);
        var staleInFlightFetch = CreateFetch(partition: 0, baseOffset: 102, value: "stale-in-flight");
        await WritePrefetchedItemsAsync(consumer, [staleInFlightFetch], staleFetchBufferEpoch);
        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        var (_, _, _, pendingClassifications) =
            await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);

        await Assert.That(GetFetchPositions(consumer)[partition]).IsEqualTo(0L);
        await Assert.That(GetPendingFetches(consumer)).IsEmpty();
        await Assert.That(GetPrefetchBuffer(consumer).TryRead(out _)).IsFalse();
        await Assert.That(GetPrefetchedBytes(consumer)).IsEqualTo(0L);
        await Assert.That(requestedTimestamps).IsEquivalentTo(
            [TopicPartitionTimestamp.Latest, TopicPartitionTimestamp.Earliest]);
        await Assert.That(pendingClassifications).IsEmpty();
    }

    [Test]
    public async Task EnsureAssignmentAsync_LateClassificationWithoutPolicy_PreservesPositionAndBuffers()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        metadataManager.SetApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetchWithoutCommits(connection);
        var requestedTimestamps = new ConcurrentQueue<long>();
        connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                Arg.Any<ListOffsetsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<ListOffsetsRequest>(0);
                var partition = request.Topics[0].Partitions[0];
                requestedTimestamps.Enqueue(partition.Timestamp);
                return ValueTask.FromResult(CreateListOffsetsResponse(partition.PartitionIndex, 100));
            });

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(CancellationToken.None);

        var partition = new TopicPartition("test-topic", 0);
        var pendingFetch = CreateFetch(partition: 0, baseOffset: 100, value: "pending");
        var prefetchedFetch = CreateFetch(partition: 0, baseOffset: 101, value: "prefetched");
        GetPendingFetches(consumer).Enqueue(pendingFetch);
        await Assert.That(GetPrefetchBuffer(consumer).TryWrite(prefetchedFetch)).IsTrue();
        SetPrefetchedBytes(
            consumer,
            KafkaConsumer<string, string>.EstimatePendingFetchBytes(prefetchedFetch));

        var coordinator = GetCoordinator(consumer);
        ProcessCoordinatorAssignment(coordinator, CreateAssignmentWithNewPartitions([0], [0]));
        await consumer.EnsureAssignmentAsync(CancellationToken.None);
        var (_, _, _, pendingClassifications) =
            await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);

        await Assert.That(GetFetchPositions(consumer)[partition]).IsEqualTo(100L);
        await Assert.That(GetPendingFetches(consumer)).Contains(pendingFetch);
        await Assert.That(GetPrefetchBuffer(consumer).TryRead(out var retainedPrefetch)).IsTrue();
        await Assert.That(retainedPrefetch).IsSameReferenceAs(prefetchedFetch);
        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsFalse();
        await Assert.That(requestedTimestamps).IsEquivalentTo([TopicPartitionTimestamp.Latest]);
        await Assert.That(pendingClassifications).IsEmpty();

        retainedPrefetch!.Dispose();
        SetPrefetchedBytes(consumer, 0);
    }

    [Test]
    public async Task EnsureAssignmentAsync_PartialInitializationFailure_AcknowledgesSuccessfulPartitions()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        metadataManager.SetApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(
            connection,
            CreateAssignmentWithNewPartitions([0, 1], [0, 1]));
        SetupOffsetFetchWithSingleCommit(connection, committedPartition: 0, committedOffset: 10);
        connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                Arg.Any<ListOffsetsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<ListOffsetsResponse>>(_ => throw new KafkaException(
                ErrorCode.InvalidRequest,
                "Injected ListOffsets failure"));

        await using var consumer = CreateGroupConsumer(
            connectionPool,
            metadataManager,
            autoOffsetResetNewPartitions: AutoOffsetReset.Earliest);
        consumer.Subscribe("test-topic");

        _ = await Assert.That(async () => await consumer.EnsureAssignmentAsync(CancellationToken.None))
            .Throws<KafkaException>();

        var coordinator = GetCoordinator(consumer);
        var (_, _, _, pendingClassifications) =
            await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);

        await Assert.That(GetFetchPositions(consumer)[new TopicPartition("test-topic", 0)]).IsEqualTo(10L);
        await Assert.That(pendingClassifications).IsEquivalentTo(
            [new TopicPartition("test-topic", 1)]);
    }

    [Test]
    [Timeout(30_000)]
    public async Task EnsureAssignmentAsync_RevocationCommitPending_WaitsForCommit(
        CancellationToken testTimeout)
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);
        await using var metadataManager = CreateMetadataManager(connectionPool);
        metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            9);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0));
        SetupOffsetFetch(connection);
        var commitStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCommit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        OffsetCommitRequest? commitRequest = null;
        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call => BlockOffsetCommitAsync(
                call.ArgAt<OffsetCommitRequest>(0),
                call.ArgAt<CancellationToken>(2)));

        await using var consumer = CreateGroupConsumer(
            connectionPool,
            metadataManager,
            offsetCommitMode: OffsetCommitMode.Auto);
        consumer.Subscribe("test-topic");
        await consumer.EnsureAssignmentAsync(testTimeout);
        var coordinator = GetCoordinator(consumer);
        var revokedPartition = new TopicPartition("test-topic", 0);
        var assignedPartition = new TopicPartition("test-topic", 1);
        consumer.StoreOffset(new TopicPartitionOffset(revokedPartition.Topic, revokedPartition.Partition, 10));

        var revokedResult = ProcessCoordinatorAssignment(coordinator, CreateAssignment(1));
        var listenerTask = FireConsumerProtocolRebalanceListenersAsync(
            coordinator,
            revokedResult,
            testTimeout).AsTask();
        await commitStarted.Task.WaitAsync(testTimeout);
        var assignmentTask = consumer.EnsureAssignmentAsync(testTimeout).AsTask();

        await Assert.That(assignmentTask.IsCompleted).IsFalse();
        await Assert.That(consumer.Assignment).Contains(revokedPartition);

        releaseCommit.TrySetResult(true);
        await listenerTask;
        await assignmentTask;

        await Assert.That(commitRequest).IsNotNull();
        await Assert.That(consumer.Assignment).DoesNotContain(revokedPartition);
        await Assert.That(consumer.Assignment).Contains(assignedPartition);

        async ValueTask<OffsetCommitResponse> BlockOffsetCommitAsync(
            OffsetCommitRequest request,
            CancellationToken cancellationToken)
        {
            commitRequest = request;
            commitStarted.TrySetResult(true);
            await releaseCommit.Task.WaitAsync(cancellationToken);
            return new OffsetCommitResponse { Topics = [] };
        }
    }

    [Test]
    public async Task CoordinatorRevocationFetchClear_ConcurrentQueueAndDrain_KeepsPendingFlagConsistent()
    {
        await using var consumer = CreateConsumer();
        var tasks = new List<Task>();

        for (var i = 0; i < 500; i++)
        {
            var partition = new TopicPartition("test-topic", i);
            tasks.Add(Task.Run(() => QueueCoordinatorRevokedPartitionsForFetchClear(consumer, [partition])));
            tasks.Add(Task.Run(() => ClearFetchBufferForPendingCoordinatorRevocations(consumer)));
        }

        await Task.WhenAll(tasks);

        var pendingRevocations = GetCoordinatorRevokedPartitionsPendingFetchClear(consumer);
        var pendingFlag = GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer);

        await Assert.That(pendingFlag == 1 || pendingRevocations.IsEmpty).IsTrue();

        while (ClearFetchBufferForPendingCoordinatorRevocations(consumer))
        {
        }

        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClear(consumer)).IsEmpty();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer)).IsEqualTo(0);
    }

    [Test]
    public async Task PendingFetchClear_MissingMarkers_RepairsAndDrains()
    {
        await using var consumer = CreateConsumer();
        var partition = new TopicPartition("test-topic", 0);
        consumer.IncrementalAssign([new TopicPartitionOffset(partition.Topic, partition.Partition, 0)]);

        GetCoordinatorRevokedPartitionsPendingFetchClear(consumer)[partition] = 0;

        var filtered = ExcludePartitionsPendingFetchClear(
            consumer,
            new Dictionary<int, List<TopicPartition>> { [0] = [partition] });

        await Assert.That(filtered).IsEmpty();
        await Assert.That(ShouldDropStaleFetchPartition(consumer, partition, GetFetchBufferEpoch(consumer))).IsTrue();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearMarkerPresent(consumer)).IsEqualTo(1);
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer)).IsEqualTo(1);
        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClear(consumer)).IsEmpty();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearMarkerPresent(consumer)).IsEqualTo(0);
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer)).IsEqualTo(0);
    }

    [Test]
    public async Task PendingFetchClear_MissingPendingFlag_RepairsAndDrains()
    {
        await using var consumer = CreateConsumer();
        var partition = new TopicPartition("test-topic", 0);
        consumer.IncrementalAssign([new TopicPartitionOffset(partition.Topic, partition.Partition, 0)]);

        GetCoordinatorRevokedPartitionsPendingFetchClear(consumer)[partition] = 0;
        SetCoordinatorRevokedPartitionsPendingFetchClearMarkerPresent(consumer, 1);

        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer)).IsEqualTo(0);
        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClear(consumer)).IsEmpty();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearMarkerPresent(consumer)).IsEqualTo(0);
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer)).IsEqualTo(0);
    }

    [Test]
    public async Task PendingFetchClear_StagedDivergence_WaitsForCompletion()
    {
        await using var consumer = CreateConsumer();
        var partition = new TopicPartition("test-topic", 0);
        consumer.IncrementalAssign([new TopicPartitionOffset(partition.Topic, partition.Partition, 0)]);

        StageDivergingEpochReset(
            consumer,
            partition,
            endOffset: 42,
            epoch: 7,
            GetFetchBufferEpoch(consumer));

        // Recovery must restore a missing marker without making a staged reset drainable
        // before the fetch response is complete.
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearMarkerPresent(consumer)).IsEqualTo(1);
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer)).IsEqualTo(0);
        var batchIterationVersion = GetBatchIterationVersion(consumer);
        SetCoordinatorRevokedPartitionsPendingFetchClearMarkerPresent(consumer, 0);
        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsFalse();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearMarkerPresent(consumer)).IsEqualTo(1);
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer)).IsEqualTo(0);
        await Assert.That(GetBatchIterationVersion(consumer)).IsEqualTo(batchIterationVersion + 2);
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClear(consumer)).ContainsKey(partition);

        CompleteDivergingEpochResets(consumer);

        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClear(consumer)).IsEmpty();
    }

    [Test]
    public async Task PendingFetchClear_CompletedDivergence_RepairsLostPendingFlag()
    {
        await using var consumer = CreateConsumer();
        var partition = new TopicPartition("test-topic", 0);
        consumer.IncrementalAssign([new TopicPartitionOffset(partition.Topic, partition.Partition, 0)]);

        StageDivergingEpochReset(
            consumer,
            partition,
            endOffset: 42,
            epoch: 7,
            GetFetchBufferEpoch(consumer));
        CompleteDivergingEpochResets(consumer);
        SetCoordinatorRevokedPartitionsPendingFetchClearPending(consumer, 0);

        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClear(consumer)).IsEmpty();
    }

    [Test]
    public async Task PendingFetchClear_MultipleStagedBatches_WaitsForAllCompletions()
    {
        await using var consumer = CreateConsumer();
        var first = new TopicPartition("test-topic", 0);
        var second = new TopicPartition("test-topic", 1);
        consumer.IncrementalAssign(
        [
            new TopicPartitionOffset(first.Topic, first.Partition, 0),
            new TopicPartitionOffset(second.Topic, second.Partition, 0)
        ]);

        StageDivergingEpochReset(consumer, first, 42, 7, GetFetchBufferEpoch(consumer));
        StageDivergingEpochReset(consumer, second, 84, 8, GetFetchBufferEpoch(consumer));

        CompleteDivergingEpochResets(consumer);
        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsFalse();

        CompleteDivergingEpochResets(consumer);
        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(GetCoordinatorRevokedPartitionsPendingFetchClear(consumer)).IsEmpty();
    }

    [Test]
    public async Task IncrementalUnassign_InvalidatesOnlyRemovedPartitionFetches()
    {
        await using var consumer = CreateConsumer();
        var removedPartition = new TopicPartition("test-topic", 0);
        var retainedPartition = new TopicPartition("test-topic", 1);
        consumer.IncrementalAssign(
        [
            new TopicPartitionOffset(removedPartition.Topic, removedPartition.Partition, 0),
            new TopicPartitionOffset(retainedPartition.Topic, retainedPartition.Partition, 0)
        ]);
        var globalMinimumEpoch = GetMinimumFetchBufferEpoch(consumer);

        consumer.IncrementalUnassign([removedPartition]);

        var partitionMinimumEpochs = GetMinimumFetchBufferEpochsByPartition(consumer);
        await Assert.That(GetMinimumFetchBufferEpoch(consumer)).IsEqualTo(globalMinimumEpoch);
        await Assert.That(partitionMinimumEpochs.ContainsKey(removedPartition)).IsTrue();
        await Assert.That(partitionMinimumEpochs.ContainsKey(retainedPartition)).IsFalse();
    }

    [Test]
    public async Task PendingRevocationDrain_InvalidatesOnlyAffectedPartitionFetches()
    {
        await using var consumer = CreateConsumer();
        var divergingPartition = new TopicPartition("test-topic", 0);
        var revokedPartition = new TopicPartition("test-topic", 1);
        var retainedPartition = new TopicPartition("test-topic", 2);
        consumer.IncrementalAssign(
        [
            new TopicPartitionOffset(divergingPartition.Topic, divergingPartition.Partition, 0),
            new TopicPartitionOffset(revokedPartition.Topic, revokedPartition.Partition, 0),
            new TopicPartitionOffset(retainedPartition.Topic, retainedPartition.Partition, 0)
        ]);
        StageDivergingEpochReset(
            consumer,
            divergingPartition,
            endOffset: 42,
            epoch: 7,
            GetFetchBufferEpoch(consumer));
        CompleteDivergingEpochResets(consumer);
        QueueCoordinatorRevokedPartitionsForFetchClear(consumer, [revokedPartition]);
        var globalMinimumEpoch = GetMinimumFetchBufferEpoch(consumer);

        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();

        var partitionMinimumEpochs = GetMinimumFetchBufferEpochsByPartition(consumer);
        await Assert.That(GetMinimumFetchBufferEpoch(consumer)).IsEqualTo(globalMinimumEpoch);
        await Assert.That(partitionMinimumEpochs.ContainsKey(divergingPartition)).IsTrue();
        await Assert.That(partitionMinimumEpochs.ContainsKey(revokedPartition)).IsTrue();
        await Assert.That(partitionMinimumEpochs.ContainsKey(retainedPartition)).IsFalse();
    }

    [Test]
    public async Task RemovePartitionState_RemovesPartitionFetchEpochMinimum()
    {
        await using var consumer = CreateConsumer();
        var removedPartition = new TopicPartition("test-topic", 0);
        var retainedPartition = new TopicPartition("test-topic", 1);
        var minimumEpochs = GetMinimumFetchBufferEpochsByPartition(consumer);
        minimumEpochs[removedPartition] = 2;
        minimumEpochs[retainedPartition] = 3;

        RemovePartitionState(consumer, [removedPartition]);

        await Assert.That(minimumEpochs.ContainsKey(removedPartition)).IsFalse();
        await Assert.That(minimumEpochs[retainedPartition]).IsEqualTo(3);
    }

    [Test]
    public async Task WritePrefetchedItemsAsync_DropsUnassignedPartitionsBeforeAdvancingFetchPosition()
    {
        await using var consumer = CreateConsumer();
        var revokedPartition = new TopicPartition("test-topic", 0);
        var retainedPartition = new TopicPartition("test-topic", 1);
        var revokedFetch = CreateFetch(partition: 0, baseOffset: 10, value: "revoked-prefetch");
        var retainedFetch = CreateFetch(partition: 1, baseOffset: 20, value: "retained-prefetch");

        consumer.IncrementalAssign([new TopicPartitionOffset("test-topic", 1, 20)]);

        await WritePrefetchedItemsAsync(consumer, [revokedFetch, retainedFetch]);

        var fetchPositions = GetFetchPositions(consumer);
        await Assert.That(fetchPositions.ContainsKey(revokedPartition)).IsFalse();
        await Assert.That(fetchPositions[retainedPartition]).IsEqualTo(21L);
        await Assert.That(GetPrefetchBuffer(consumer).TryRead(out var prefetched)).IsTrue();
        await Assert.That(prefetched!.TopicPartition).IsEqualTo(retainedPartition);
        await Assert.That(GetPrefetchBuffer(consumer).TryRead(out _)).IsFalse();

        prefetched.Dispose();
        SetPrefetchedBytes(consumer, 0);
    }

    [Test]
    public async Task WritePrefetchedItemsAsync_StaleEpochAfterSeek_DropsWithoutAdvancingFetchPosition()
    {
        await using var consumer = CreateConsumer();
        var partition = new TopicPartition("test-topic", 0);

        consumer.IncrementalAssign([new TopicPartitionOffset("test-topic", 0, 0)]);
        var staleEpoch = GetFetchBufferEpoch(consumer);
        var staleFetch = CreateFetch(partition: 0, baseOffset: 1_999_999, value: "stale-prefetch");
        consumer.SeekToBeginning(partition);

        await WritePrefetchedItemsAsync(consumer, [staleFetch], staleEpoch);

        var fetchPositions = GetFetchPositions(consumer);
        await Assert.That(fetchPositions[partition]).IsEqualTo(0L);
        await Assert.That(GetPrefetchBuffer(consumer).TryRead(out _)).IsFalse();
        await Assert.That(GetPrefetchedBytes(consumer)).IsEqualTo(0L);
    }

    [Test]
    public async Task WritePrefetchedItemsAsync_SeekWhileWaitingToWrite_DropsStaleFetch()
    {
        await using var consumer = CreateConsumer();
        var partition = new TopicPartition("test-topic", 0);
        consumer.IncrementalAssign([new TopicPartitionOffset(partition.Topic, partition.Partition, 0)]);

        var prefetchBuffer = GetPrefetchBuffer(consumer);
        while (true)
        {
            var filler = CreateFetch(partition: 0, baseOffset: 0, value: "filler");
            if (prefetchBuffer.TryWrite(filler))
                continue;

            filler.Dispose();
            break;
        }

        var staleEpoch = GetFetchBufferEpoch(consumer);
        var staleFetch = CreateFetch(partition: 0, baseOffset: 1_999_999, value: "stale-prefetch");
        var writeTask = StartWritePrefetchedItemsAsync(consumer, [staleFetch], staleEpoch).AsTask();

        await Assert.That(writeTask.IsCompleted).IsFalse();

        consumer.SeekToBeginning(partition);
        await writeTask.WaitAsync(TimeSpan.FromSeconds(2));

        await Assert.That(GetFetchPositions(consumer)[partition]).IsEqualTo(0L);
        await Assert.That(prefetchBuffer.TryRead(out _)).IsFalse();
        SetPrefetchedBytes(consumer, 0);
    }

    [Test]
    public async Task WritePrefetchedItemsAsync_CancellationDisposesUnpublishedSharedMemory()
    {
        await using var consumer = CreateConsumer();
        var partition = new TopicPartition("test-topic", 0);
        consumer.IncrementalAssign([new TopicPartitionOffset(partition.Topic, partition.Partition, 0)]);

        var prefetchBuffer = GetPrefetchBuffer(consumer);
        while (true)
        {
            var filler = CreateFetch(partition: 0, baseOffset: 0, value: "filler");
            if (prefetchBuffer.TryWrite(filler))
                continue;

            filler.Dispose();
            break;
        }

        var memory = new TrackingPooledMemory();
        var shared = RefCountedMemoryOwner.Create(memory, initialRefCount: 2);
        var first = CreateFetch(partition: 0, baseOffset: 10, value: "first");
        var second = CreateFetch(partition: 0, baseOffset: 11, value: "second");
        first.SetMemoryOwner(shared);
        second.SetMemoryOwner(shared);
        using var cts = new CancellationTokenSource();

        var writeTask = StartWritePrefetchedItemsAsync(
            consumer,
            [first, second],
            GetFetchBufferEpoch(consumer),
            cts.Token).AsTask();
        await Assert.That(writeTask.IsCompleted).IsFalse();

        cts.Cancel();
        await Assert.That(async () => await writeTask).Throws<OperationCanceledException>();

        await Assert.That(memory.DisposeCount).IsEqualTo(1);
        await Assert.That(GetPrefetchedBytes(consumer)).IsEqualTo(0L);
    }

    private static KafkaConsumer<string, string> CreateConsumer()
    {
        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1
            },
            Serializers.String,
            Serializers.String);
    }

    private static KafkaConsumer<string, string> CreatePausedGroupConsumer()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var metadataManager = CreateMetadataManager(connectionPool);
        var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        SetInitialized(consumer);

        var partition = new TopicPartition("test-topic", 0);
        consumer.IncrementalAssign([new TopicPartitionOffset(partition.Topic, partition.Partition, 0)]);
        consumer.Pause(partition);
        return consumer;
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static KafkaConsumer<string, string> CreateGroupConsumer(
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        int queuedMinMessages = 1,
        int maxPollIntervalMs = 300_000,
        OffsetCommitMode offsetCommitMode = OffsetCommitMode.Manual,
        int requestTimeoutMs = 30_000,
        int defaultApiTimeoutMs = 60_000,
        AutoOffsetReset? autoOffsetResetNewPartitions = null)
    {
        return new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "group-a",
                OffsetCommitMode = offsetCommitMode,
                QueuedMinMessages = queuedMinMessages,
                MaxPollIntervalMs = maxPollIntervalMs,
                RequestTimeoutMs = requestTimeoutMs,
                DefaultApiTimeoutMs = defaultApiTimeoutMs,
                AutoOffsetResetNewPartitions = autoOffsetResetNewPartitions
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
    }

    private static MetadataManager CreateMetadataManager(IConnectionPool connectionPool)
    {
        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0);
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadataManager.SetApiVersion(ApiKey.OffsetFetch, OffsetFetchRequest.LowestSupportedVersion, 9);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    Name = "test-topic",
                    TopicId = TestTopicId,
                    ErrorCode = ErrorCode.None,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            PartitionIndex = 0,
                            LeaderId = 0,
                            ErrorCode = ErrorCode.None,
                            ReplicaNodes = [0],
                            IsrNodes = [0]
                        },
                        new PartitionMetadata
                        {
                            PartitionIndex = 1,
                            LeaderId = 0,
                            ErrorCode = ErrorCode.None,
                            ReplicaNodes = [0],
                            IsrNodes = [0]
                        }
                    ]
                }
            ]
        });

        return metadataManager;
    }

    private static void SetupConnectionPool(IConnectionPool connectionPool, IKafkaConnection connection)
    {
        connectionPool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
        connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
    }

    private static void SetupFindCoordinator(IKafkaConnection connection)
    {
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator
                    {
                        Key = "group-a",
                        NodeId = 0,
                        Host = "localhost",
                        Port = 9092,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));
    }

    private static void SetupConsumerGroupHeartbeat(
        IKafkaConnection connection,
        ConsumerGroupHeartbeatAssignment assignment)
    {
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                MemberEpoch = 1,
                HeartbeatIntervalMs = 60000,
                Assignment = assignment
            }));
    }

    private static void SetupChangingConsumerGroupHeartbeat(IKafkaConnection connection)
    {
        var callCount = 0;
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var count = Interlocked.Increment(ref callCount);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = count,
                    HeartbeatIntervalMs = 60000,
                    Assignment = count == 1 ? CreateAssignment(0) : CreateAssignment(0, 1)
                });
            });
    }

    private static void SetupIncrementingConsumerGroupHeartbeat(
        IKafkaConnection connection,
        ConsumerGroupHeartbeatAssignment assignment)
    {
        var callCount = 0;
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(CreateHeartbeatResponse(
                assignment,
                Interlocked.Increment(ref callCount))));
    }

    private static TaskCompletionSource SetupBlockingRejoinConsumerGroupHeartbeat(
        IKafkaConnection connection,
        ConsumerGroupHeartbeatAssignment assignment)
    {
        var callCount = 0;
        var rejoinStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var count = Interlocked.Increment(ref callCount);
                return count == 2
                    ? WaitForRejoinCancellationAsync(
                        rejoinStarted,
                        call.ArgAt<CancellationToken>(2))
                    : ValueTask.FromResult(CreateHeartbeatResponse(assignment, count));
            });
        return rejoinStarted;
    }

    private static ConsumerGroupHeartbeatResponse CreateHeartbeatResponse(
        ConsumerGroupHeartbeatAssignment assignment,
        int memberEpoch) => new()
        {
            ErrorCode = ErrorCode.None,
            MemberId = "member-1",
            MemberEpoch = memberEpoch,
            HeartbeatIntervalMs = 60000,
            Assignment = assignment
        };

    private static async ValueTask<ConsumerGroupHeartbeatResponse> WaitForRejoinCancellationAsync(
        TaskCompletionSource rejoinStarted,
        CancellationToken cancellationToken)
    {
        rejoinStarted.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Cancellation wait completed without cancellation");
    }

    private static void SetupRevokingConsumerGroupHeartbeat(IKafkaConnection connection)
    {
        var callCount = 0;
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var count = Interlocked.Increment(ref callCount);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = count,
                    HeartbeatIntervalMs = 60000,
                    Assignment = count == 1 ? CreateAssignment(0, 1) : CreateAssignment(1)
                });
            });
    }

    private static void SetupAssignmentAbaConsumerGroupHeartbeat(IKafkaConnection connection)
    {
        var callCount = 0;
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var count = Interlocked.Increment(ref callCount);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = count,
                    HeartbeatIntervalMs = 60000,
                    Assignment = count == 2 ? CreateAssignment(0) : CreateAssignment(0, 1)
                });
            });
    }

    private static void SetupOffsetFetch(IKafkaConnection connection)
    {
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateSuccessfulOffsetFetchResponse()));
    }

    private static void SetupOffsetFetchWithoutCommits(IKafkaConnection connection)
    {
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateOffsetFetchResponse(
                (0, -1L),
                (1, -1L))));
    }

    private static void SetupOffsetFetchWithSingleCommit(
        IKafkaConnection connection,
        int committedPartition,
        long committedOffset)
    {
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateOffsetFetchResponse(
                (committedPartition, committedOffset),
                (committedPartition == 0 ? 1 : 0, -1L))));
    }

    private static void SetupBlockingOffsetFetch(
        IKafkaConnection connection,
        TaskCompletionSource offsetFetchStarted,
        TaskCompletionSource<OffsetFetchResponse> releaseOffsetFetch)
    {
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                offsetFetchStarted.TrySetResult();
                return new ValueTask<OffsetFetchResponse>(releaseOffsetFetch.Task);
            });
    }

    private static void SetupOffsetFetchFailureAfterInitialAssignment(IKafkaConnection connection)
    {
        var callCount = 0;
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref callCount) == 2
                ? ValueTask.FromResult(new OffsetFetchResponse
                {
                    Groups =
                    [
                        new OffsetFetchResponseGroup
                        {
                            GroupId = "group-a",
                            Topics = [],
                            ErrorCode = ErrorCode.StaleMemberEpoch
                        }
                    ]
                })
                : ValueTask.FromResult(CreateSuccessfulOffsetFetchResponse()));
    }

    private static void SetupOffsetFetchFailureThenSuccess(IKafkaConnection connection)
    {
        var callCount = 0;
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref callCount) == 1
                ? ValueTask.FromResult(new OffsetFetchResponse
                {
                    Groups =
                    [
                        new OffsetFetchResponseGroup
                        {
                            GroupId = "group-a",
                            Topics = [],
                            ErrorCode = ErrorCode.StaleMemberEpoch
                        }
                    ]
                })
                : ValueTask.FromResult(CreateSuccessfulOffsetFetchResponse()));
    }

    private static void SetupOffsetFetchStaleAfterInitialSuccess(
        IKafkaConnection connection,
        ConcurrentQueue<int> requestedEpochs)
    {
        var callCount = 0;
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.ArgAt<OffsetFetchRequest>(0);
                requestedEpochs.Enqueue(request.Groups![0].MemberEpoch);
                return Interlocked.Increment(ref callCount) == 2
                    ? ValueTask.FromResult(new OffsetFetchResponse
                    {
                        Groups =
                        [
                            new OffsetFetchResponseGroup
                            {
                                GroupId = "group-a",
                                Topics = [],
                                ErrorCode = ErrorCode.StaleMemberEpoch
                            }
                        ]
                    })
                    : ValueTask.FromResult(CreateSuccessfulOffsetFetchResponse());
            });
    }

    private static OffsetFetchResponse CreateSuccessfulOffsetFetchResponse() => new()
    {
        ErrorCode = ErrorCode.None,
        Topics =
        [
            new OffsetFetchResponseTopic
            {
                Name = "test-topic",
                Partitions =
                [
                    new OffsetFetchResponsePartition
                    {
                        PartitionIndex = 0,
                        CommittedOffset = 10,
                        CommittedLeaderEpoch = 4,
                        ErrorCode = ErrorCode.None
                    },
                    new OffsetFetchResponsePartition
                    {
                        PartitionIndex = 1,
                        CommittedOffset = 20,
                        CommittedLeaderEpoch = 5,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }
        ]
    };

    private static OffsetFetchResponse CreateOffsetFetchResponse(
        params (int Partition, long Offset)[] partitions) => new()
        {
            ErrorCode = ErrorCode.None,
            Topics =
            [
                new OffsetFetchResponseTopic
                {
                    Name = "test-topic",
                    Partitions = partitions.Select(static partition => new OffsetFetchResponsePartition
                    {
                        PartitionIndex = partition.Partition,
                        CommittedOffset = partition.Offset,
                        CommittedLeaderEpoch = -1,
                        ErrorCode = ErrorCode.None
                    }).ToArray()
                }
            ]
        };

    private static ListOffsetsResponse CreateListOffsetsResponse(int partition, long offset) => new()
    {
        Topics =
        [
            new ListOffsetsResponseTopic
            {
                Name = "test-topic",
                Partitions =
                [
                    new ListOffsetsResponsePartition
                    {
                        PartitionIndex = partition,
                        ErrorCode = ErrorCode.None,
                        Offset = offset
                    }
                ]
            }
        ]
    };

    private static ConsumerGroupHeartbeatAssignment CreateAssignment(params int[] partitions)
    {
        return new ConsumerGroupHeartbeatAssignment
        {
            AssignedTopicPartitions =
            [
                new ConsumerGroupHeartbeatTopicPartitions
                {
                    TopicId = TestTopicId,
                    Partitions = partitions
                }
            ],
            PendingTopicPartitions = []
        };
    }

    private static ConsumerGroupHeartbeatAssignment CreateAssignmentWithNewPartitions(
        int[] partitions,
        int[] newPartitions) => new()
        {
            AssignedTopicPartitions =
            [
                new ConsumerGroupHeartbeatTopicPartitions
                {
                    TopicId = TestTopicId,
                    Partitions = partitions,
                    NewPartitions = newPartitions
                }
            ],
            PendingTopicPartitions = []
        };

    private static SemaphoreSlim GetAssignmentLock(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_assignmentLock",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_assignmentLock field not found.");

        return (SemaphoreSlim)field.GetValue(consumer)!;
    }

    private static Queue<PendingFetchData> GetPendingFetches(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_pendingFetches",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_pendingFetches field not found.");

        return (Queue<PendingFetchData>)field.GetValue(consumer)!;
    }

    private static MpscFetchBuffer GetPrefetchBuffer(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_prefetchBuffer",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchBuffer field not found.");

        return (MpscFetchBuffer)field.GetValue(consumer)!;
    }

    private static long GetPrefetchedBytes(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_prefetchedBytes",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchedBytes field not found.");

        return (long)field.GetValue(consumer)!;
    }

    private static int GetFetchBufferEpoch(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_fetchBufferEpoch",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_fetchBufferEpoch field not found.");

        return (int)field.GetValue(consumer)!;
    }

    private static int GetBatchIterationVersion(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_batchIterationEpoch",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_batchIterationEpoch field not found.");

        return ((BatchIterationEpoch)field.GetValue(consumer)!).Version;
    }

    private static int GetMinimumFetchBufferEpoch(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_minimumFetchBufferEpoch",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_minimumFetchBufferEpoch field not found.");

        return (int)field.GetValue(consumer)!;
    }

    private static ConcurrentDictionary<TopicPartition, int> GetMinimumFetchBufferEpochsByPartition(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_minimumFetchBufferEpochsByPartition",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_minimumFetchBufferEpochsByPartition field not found.");

        return (ConcurrentDictionary<TopicPartition, int>)field.GetValue(consumer)!;
    }

    private static ConcurrentDictionary<TopicPartition, byte> GetCoordinatorRevokedPartitionsPendingFetchClear(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_coordinatorRevokedPartitionsPendingFetchClear",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_coordinatorRevokedPartitionsPendingFetchClear field not found.");

        return (ConcurrentDictionary<TopicPartition, byte>)field.GetValue(consumer)!;
    }

    private static int GetCoordinatorRevokedPartitionsPendingFetchClearPending(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_coordinatorRevokedPartitionsPendingFetchClearPending",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_coordinatorRevokedPartitionsPendingFetchClearPending field not found.");

        return (int)field.GetValue(consumer)!;
    }

    private static int GetCoordinatorRevokedPartitionsPendingFetchClearMarkerPresent(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_coordinatorRevokedPartitionsPendingFetchClearMarkerPresent",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_coordinatorRevokedPartitionsPendingFetchClearMarkerPresent field not found.");

        return (int)field.GetValue(consumer)!;
    }

    private static void SetCoordinatorRevokedPartitionsPendingFetchClearMarkerPresent(
        KafkaConsumer<string, string> consumer,
        int value)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_coordinatorRevokedPartitionsPendingFetchClearMarkerPresent",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_coordinatorRevokedPartitionsPendingFetchClearMarkerPresent field not found.");

        field.SetValue(consumer, value);
    }

    private static void SetCoordinatorRevokedPartitionsPendingFetchClearPending(
        KafkaConsumer<string, string> consumer,
        int value)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_coordinatorRevokedPartitionsPendingFetchClearPending",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_coordinatorRevokedPartitionsPendingFetchClearPending field not found.");

        field.SetValue(consumer, value);
    }

    private static Dictionary<int, List<TopicPartition>> ExcludePartitionsPendingFetchClear(
        KafkaConsumer<string, string> consumer,
        Dictionary<int, List<TopicPartition>> partitionsByBroker)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "ExcludePartitionsPendingFetchClear",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ExcludePartitionsPendingFetchClear method not found.");

        return (Dictionary<int, List<TopicPartition>>)method.Invoke(consumer, [partitionsByBroker])!;
    }

    private static bool ShouldDropStaleFetchPartition(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition,
        int fetchBufferEpoch)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "ShouldDropStaleFetchPartition",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ShouldDropStaleFetchPartition method not found.");

        return (bool)method.Invoke(consumer, [partition, fetchBufferEpoch])!;
    }

    private static ConcurrentDictionary<TopicPartition, long> GetFetchPositions(
        KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_fetchPositions",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_fetchPositions field not found.");

        return (ConcurrentDictionary<TopicPartition, long>)field.GetValue(consumer)!;
    }

    private static int GetLastConsumedLeaderEpoch(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "GetLastConsumedLeaderEpoch",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GetLastConsumedLeaderEpoch method not found.");

        return (int)method.Invoke(consumer, [partition])!;
    }

    private static void QueueCoordinatorRevokedPartitionsForFetchClear(
        KafkaConsumer<string, string> consumer,
        TopicPartition[] partitions)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "QueueCoordinatorRevokedPartitionsForFetchClear",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("QueueCoordinatorRevokedPartitionsForFetchClear method not found.");

        method.Invoke(consumer, [partitions]);
    }

    private static void RemovePartitionState(
        KafkaConsumer<string, string> consumer,
        TopicPartition[] partitions)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "RemovePartitionState",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RemovePartitionState method not found.");

        method.Invoke(consumer, [partitions]);
    }

    private static void StageDivergingEpochReset(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition,
        long endOffset,
        int epoch,
        int fetchBufferEpoch)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "StageDivergingEpochReset",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("StageDivergingEpochReset method not found.");

        method.Invoke(consumer, [partition, endOffset, epoch, fetchBufferEpoch, true]);
    }

    private static void CompleteDivergingEpochResets(KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "CompleteDivergingEpochResets",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CompleteDivergingEpochResets method not found.");

        method.Invoke(consumer, []);
    }

    private static bool ClearFetchBufferForPendingCoordinatorRevocations(KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "RecoverAndClearFetchBufferForPendingCoordinatorRevocations",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RecoverAndClearFetchBufferForPendingCoordinatorRevocations method not found.");

        return (bool)method.Invoke(consumer, [])!;
    }

    private static async ValueTask WritePrefetchedItemsAsync(
        KafkaConsumer<string, string> consumer,
        IReadOnlyList<PendingFetchData> pendingItems,
        int? fetchBufferEpoch = null)
    {
        await StartWritePrefetchedItemsAsync(
            consumer,
            pendingItems,
            fetchBufferEpoch ?? GetFetchBufferEpoch(consumer));
    }

    private static ValueTask StartWritePrefetchedItemsAsync(
        KafkaConsumer<string, string> consumer,
        IReadOnlyList<PendingFetchData> pendingItems,
        int fetchBufferEpoch,
        CancellationToken cancellationToken = default)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "WritePrefetchedItemsAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("WritePrefetchedItemsAsync method not found.");

        return (ValueTask)method.Invoke(
            consumer,
            [pendingItems, fetchBufferEpoch, cancellationToken])!;
    }

    private sealed class TrackingPooledMemory : IPooledMemory
    {
        public ReadOnlyMemory<byte> Memory => ReadOnlyMemory<byte>.Empty;
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    private static void SetPrefetchedBytes(KafkaConsumer<string, string> consumer, long bytes)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_prefetchedBytes",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchedBytes field not found.");

        field.SetValue(consumer, bytes);
    }

    private static void SetPrefetchStarted(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_prefetchTask",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_prefetchTask field not found.");

        field.SetValue(consumer, Task.CompletedTask);
    }

    private static void SetInitialized(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_initialized",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found.");

        field.SetValue(consumer, true);
    }

    private static ConsumerCoordinator GetCoordinator(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_coordinator",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_coordinator field not found.");

        return (ConsumerCoordinator)field.GetValue(consumer)!;
    }

    private static long GetPollVersion(KafkaConsumer<string, string> consumer)
    {
        return (long)PollVersionField.GetValue(GetCoordinator(consumer))!;
    }

    private static void InvalidateCoordinatorAssignmentSnapshot(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_lastCoordinatorAssignmentVersion",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_lastCoordinatorAssignmentVersion field not found.");

        field.SetValue(consumer, -1);
    }

    private static object ProcessCoordinatorAssignment(
        ConsumerCoordinator coordinator,
        ConsumerGroupHeartbeatAssignment assignment)
    {
        var method = typeof(ConsumerCoordinator).GetMethod(
            "ProcessConsumerGroupAssignment",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ProcessConsumerGroupAssignment method not found.");

        return method.Invoke(coordinator, [assignment])
            ?? throw new InvalidOperationException("ProcessConsumerGroupAssignment returned null.");
    }

    private static ValueTask FireConsumerProtocolRebalanceListenersAsync(
        ConsumerCoordinator coordinator,
        object result,
        CancellationToken cancellationToken)
    {
        var method = typeof(ConsumerCoordinator).GetMethod(
            "FireConsumerProtocolRebalanceListenersAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("FireConsumerProtocolRebalanceListenersAsync method not found.");

        return (ValueTask)(method.Invoke(coordinator, [result, cancellationToken])
            ?? throw new InvalidOperationException("FireConsumerProtocolRebalanceListenersAsync returned null."));
    }

    private static PendingFetchData CreateFetch(int partition, long baseOffset, string value)
    {
        return PendingFetchData.Create("test-topic", partition,
        [
            new RecordBatch
            {
                BaseOffset = baseOffset,
                BaseTimestamp = 1700000000000L,
                Attributes = 0,
                Records =
                [
                    new Record
                    {
                        OffsetDelta = 0,
                        TimestampDelta = 0,
                        Key = Encoding.UTF8.GetBytes($"key-{partition}"),
                        IsKeyNull = false,
                        Value = Encoding.UTF8.GetBytes(value),
                        IsValueNull = false,
                        Headers = null,
                        HeaderCount = 0
                    }
                ]
            }
        ]);
    }

    private static PendingFetchData CreateFetchWithRecords(int recordCount)
    {
        var records = new List<Record>(recordCount);
        for (var i = 0; i < recordCount; i++)
        {
            records.Add(new Record
            {
                OffsetDelta = i,
                TimestampDelta = i,
                Key = Encoding.UTF8.GetBytes($"key-{i}"),
                IsKeyNull = false,
                Value = Encoding.UTF8.GetBytes($"value-{i}"),
                IsValueNull = false,
                Headers = null,
                HeaderCount = 0
            });
        }

        return PendingFetchData.Create("test-topic", 0,
        [
            new RecordBatch
            {
                BaseOffset = 0,
                BaseTimestamp = 1700000000000L,
                Attributes = 0,
                Records = records
            }
        ]);
    }
}
