using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Position initialization on the poll path does not wait out a committed offset it cannot read
/// yet (issue #3796): the partition stays without a position, every other partition is
/// initialized, and a later poll retries the lookup after a backoff.
/// </summary>
public sealed partial class ConsumerAssignmentFastPathTests
{
    private static readonly FieldInfo DeferredPositionRetryDueField = typeof(KafkaConsumer<string, string>).GetField(
            "_deferredPositionRetryDueMs",
            BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("_deferredPositionRetryDueMs field not found.");

    [Test]
    public async Task EnsureAssignmentAsync_UnstableOffsetCommit_InitializesStablePartitionsAndRetriesTheUnstableOneLater()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0, 1));

        // Partition 1's offsets were committed by a transaction that is still open.
        var requests = 0;
        var transactionOpen = 1;
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref requests);
                return ValueTask.FromResult(Volatile.Read(ref transactionOpen) != 0
                    ? CreateOffsetFetchResponseWithUnstablePartition1()
                    : CreateSuccessfulOffsetFetchResponse());
            });

        // A backoff far longer than the test, so only the forced retry below can refetch.
        await using var consumer = CreateGroupConsumer(
            connectionPool,
            metadataManager,
            retryBackoffMs: 600_000,
            retryBackoffMaxMs: 600_000);
        consumer.Subscribe("test-topic");

        // Waiting the transaction out (until the 30 s request timeout) would cancel this.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await consumer.EnsureAssignmentAsync(timeout.Token);

        var stablePartition = new TopicPartition("test-topic", 0);
        var unstablePartition = new TopicPartition("test-topic", 1);
        await Assert.That(consumer.Assignment).Contains(unstablePartition);
        await Assert.That(GetFetchPositions(consumer)[stablePartition]).IsEqualTo(10L);
        await Assert.That(GetFetchPositions(consumer).ContainsKey(unstablePartition)).IsFalse();
        await Assert.That(Volatile.Read(ref requests)).IsEqualTo(1);

        // Before the retry is due, a poll takes the fast path and does not refetch.
        await consumer.EnsureAssignmentAsync(timeout.Token);
        await Assert.That(Volatile.Read(ref requests)).IsEqualTo(1);

        Volatile.Write(ref transactionOpen, 0);
        DeferredPositionRetryDueField.SetValue(consumer, 1L);
        await consumer.EnsureAssignmentAsync(timeout.Token);

        await Assert.That(Volatile.Read(ref requests)).IsEqualTo(2);
        await Assert.That(GetFetchPositions(consumer)[unstablePartition]).IsEqualTo(20L);
        await Assert.That(GetFetchPositions(consumer)[stablePartition]).IsEqualTo(10L);
        await Assert.That((long)DeferredPositionRetryDueField.GetValue(consumer)!).IsEqualTo(0L);
    }

    [Test]
    public async Task StageRebalanceSeek_InitializedPartitionWhileDeferredRetryIsDue_AppliesSeekAtOnce()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0, 1));
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(CreateOffsetFetchResponseWithUnstablePartition1()));

        await using var consumer = CreateGroupConsumer(
            connectionPool,
            metadataManager,
            retryBackoffMs: 600_000,
            retryBackoffMaxMs: 600_000);
        consumer.Subscribe("test-topic");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await consumer.EnsureAssignmentAsync(timeout.Token);

        // Partition 1 is still deferred and its retry is due; partition 0 is initialized. A retry
        // pass initializes only partition 1, so a seek left staged for partition 0 would never apply.
        var stablePartition = new TopicPartition("test-topic", 0);
        DeferredPositionRetryDueField.SetValue(consumer, 1L);
        consumer.StageRebalanceSeek(new TopicPartitionOffset("test-topic", 0, 99));

        await Assert.That(GetFetchPositions(consumer)[stablePartition]).IsEqualTo(99L);
        await Assert.That(consumer.PendingRebalanceSeekCountForTest).IsEqualTo(0);

        await consumer.EnsureAssignmentAsync(timeout.Token);
        await Assert.That(GetFetchPositions(consumer)[stablePartition]).IsEqualTo(99L);
    }

    [Test]
    [Arguments(ErrorCode.CoordinatorLoadInProgress, false)]
    [Arguments(ErrorCode.NotCoordinator, false)]
    [Arguments(ErrorCode.CoordinatorNotAvailable, false)]
    [Arguments(ErrorCode.NetworkException, false)]
    [Arguments(ErrorCode.NetworkException, true)]
    public async Task EnsureAssignmentAsync_RetriableLookup_DefersAfterOneAttemptAndKeepsDelivering(
        ErrorCode errorCode, bool transportFailure)
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0, 1));
        var requests = 0;
        var failing = false;
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref requests);
                if (failing && transportFailure)
                    return ValueTask.FromException<OffsetFetchResponse>(new IOException("Connection reset during offset fetch"));
                return ValueTask.FromResult(failing
                    ? new OffsetFetchResponse { ErrorCode = errorCode }
                    : CreateOffsetFetchResponseWithUnstablePartition1());
            });

        await using var consumer = CreateGroupConsumer(
            connectionPool,
            metadataManager,
            retryBackoffMs: 600_000,
            retryBackoffMaxMs: 600_000);
        consumer.Subscribe("test-topic");
        SetInitialized(consumer);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await consumer.EnsureAssignmentAsync(timeout.Token);
        failing = true;

        // Each due retry must return after one failed request, rather than enter a retry delay
        // or wait for the 30 s request timeout. Already positioned records remain deliverable.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            DeferredPositionRetryDueField.SetValue(consumer, 1L);
            await consumer.EnsureAssignmentForPollAsync(timeout.Token);
            await Assert.That(Volatile.Read(ref requests)).IsEqualTo(attempt + 2);
            await Assert.That(GetAssignmentLock(consumer).CurrentCount).IsEqualTo(1);
            GetPendingFetches(consumer).Enqueue(CreateFetch(0, 10 + attempt, $"record-{attempt}"));
            var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1), timeout.Token);
            await Assert.That(record).IsNotNull();
            await Assert.That(record!.Value.Value).IsEqualTo($"record-{attempt}");
            await Assert.That(Volatile.Read(ref requests)).IsEqualTo(attempt + 2);
        }

        await Assert.That(consumer.Assignment).Count().IsEqualTo(2);
        await Assert.That(GetFetchPositions(consumer).ContainsKey(new TopicPartition("test-topic", 1))).IsFalse();
        await Assert.That((long)DeferredPositionRetryDueField.GetValue(consumer)!).IsNotEqualTo(0L);

        SetupOffsetFetch(connection);
        DeferredPositionRetryDueField.SetValue(consumer, 1L);
        await consumer.EnsureAssignmentForPollAsync(timeout.Token);
        await Assert.That(GetFetchPositions(consumer)[new TopicPartition("test-topic", 1)]).IsEqualTo(20L);
        await Assert.That((long)DeferredPositionRetryDueField.GetValue(consumer)!).IsEqualTo(0L);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EnsureAssignmentAsync_CancellationDuringOffsetLookup_PropagatesAndReleasesAssignmentLock(bool transportFailure)
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);
        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0));
        using var cancellation = new CancellationTokenSource();
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cancellation.Cancel();
                return transportFailure
                    ? ValueTask.FromException<OffsetFetchResponse>(new IOException("Connection closed during cancellation"))
                    : ValueTask.FromCanceled<OffsetFetchResponse>(call.Arg<CancellationToken>());
            });
        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");

        await Assert.That(async () => await consumer.EnsureAssignmentForPollAsync(cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(GetAssignmentLock(consumer).CurrentCount).IsEqualTo(1);
        await Assert.That((long)DeferredPositionRetryDueField.GetValue(consumer)!).IsEqualTo(0L);
    }

    [Test]
    public async Task EnsureAssignmentAsync_DeferredLookupDiscoveryFailure_MakesOneDiscoveryAttempt()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);
        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0, 1));
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateOffsetFetchResponseWithUnstablePartition1()));
        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager,
            retryBackoffMs: 600_000, retryBackoffMaxMs: 600_000);
        consumer.Subscribe("test-topic");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await consumer.EnsureAssignmentAsync(timeout.Token);

        var discoveries = 0;
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref discoveries);
                return ValueTask.FromResult(new FindCoordinatorResponse
                {
                    Coordinators = [new Coordinator
                    {
                        Key = "group-a", NodeId = 0, Host = "localhost", Port = 9092,
                        ErrorCode = ErrorCode.CoordinatorLoadInProgress
                    }]
                });
            });
        typeof(ConsumerCoordinator).GetField("_coordinatorId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(GetCoordinator(consumer), -1);
        DeferredPositionRetryDueField.SetValue(consumer, 1L);
        await consumer.EnsureAssignmentForPollAsync(timeout.Token);
        await Assert.That(Volatile.Read(ref discoveries)).IsEqualTo(1);
        await Assert.That(GetAssignmentLock(consumer).CurrentCount).IsEqualTo(1);
        await Assert.That(GetFetchPositions(consumer)[new TopicPartition("test-topic", 0)]).IsEqualTo(10L);
    }

    [Test]
    public async Task EnsureAssignmentAsync_NonRetriableLookupError_StillThrows()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        SetupConnectionPool(connectionPool, connection);

        await using var metadataManager = CreateMetadataManager(connectionPool);
        SetupFindCoordinator(connection);
        SetupConsumerGroupHeartbeat(connection, CreateAssignment(0, 1));
        SetupOffsetFetchError(connection, ErrorCode.GroupAuthorizationFailed);

        await using var consumer = CreateGroupConsumer(connectionPool, metadataManager);
        consumer.Subscribe("test-topic");

        var exception = await Assert.ThrowsAsync<GroupException>(async () =>
            await consumer.EnsureAssignmentAsync(CancellationToken.None));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That((long)DeferredPositionRetryDueField.GetValue(consumer)!).IsEqualTo(0L);
    }

    private static void SetupOffsetFetchError(IKafkaConnection connection, ErrorCode errorCode)
    {
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new OffsetFetchResponse { ErrorCode = errorCode }));
    }

    private static OffsetFetchResponse CreateOffsetFetchResponseWithUnstablePartition1() => new()
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
                        CommittedOffset = -1,
                        ErrorCode = ErrorCode.UnstableOffsetCommit
                    }
                ]
            }
        ]
    };
}
