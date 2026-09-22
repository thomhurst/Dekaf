using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Membership loss (KIP-848 fencing). When the coordinator fences the member, by
/// FENCED_MEMBER_EPOCH or UNKNOWN_MEMBER_ID on a heartbeat or on the rejoin after a coordinator
/// outage, the member no longer owns its partitions: listeners see them as lost before any new
/// assignment, and commits are rejected until the member has rejoined.
/// </summary>
public sealed partial class ConsumerCoordinatorKip848Tests
{
    [Test]
    public async Task MembershipLoss_RejoinAfterSessionExpiry_UnknownMember_FiresLostBeforeAssigned()
    {
        var script = new HeartbeatScript(this);
        var (listener, calls) = CreateRecordingListener();
        await using var coordinator = await JoinThenLoseCoordinatorAsync(script, listener);
        calls.Clear();

        // The broker expired the member during the outage.
        script.Respond = (count, _) => count == 1
            ? Error(ErrorCode.UnknownMemberId)
            : Joined("member-2", memberEpoch: 1, CreateAssignment(TestTopicId, 1));

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1 | assigned:test-topic-1");
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(1);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
    }

    [Test]
    public async Task MembershipLoss_RejoinAfterSessionExpiry_FencedEpoch_TreatsKeptPartitionsAsLost()
    {
        var script = new HeartbeatScript(this);
        var (listener, calls) = CreateRecordingListener();
        await using var coordinator = await JoinThenLoseCoordinatorAsync(script, listener);
        calls.Clear();

        // The member still exists but its epoch moved on: the partitions were reassigned in the
        // meantime and may come back, but their committed offsets can have advanced.
        script.Respond = (count, _) => count == 1
            ? Error(ErrorCode.FencedMemberEpoch)
            : Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 0, 1));

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1 | assigned:test-topic-0,test-topic-1");

        var rejoin = script.Requests[^1];
        await Assert.That(rejoin.MemberEpoch).IsEqualTo(0);
        await Assert.That(rejoin.TopicPartitions).IsNotNull();
        await Assert.That(rejoin.TopicPartitions!.Count).IsEqualTo(0);
        await Assert.That(coordinator.GenerationId).IsEqualTo(6);
    }

    [Test]
    [Arguments(ErrorCode.FencedMemberEpoch)]
    [Arguments(ErrorCode.UnknownMemberId)]
    public async Task MembershipLoss_FencedHeartbeat_ClearsAssignmentAndFiresLost(ErrorCode errorCode)
    {
        var script = new HeartbeatScript(this);
        var (listener, calls) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener);
        calls.Clear();

        script.Respond = (_, _) => Error(errorCode);
        await RunHeartbeatLoopUntilItStopsAsync(coordinator);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1");
    }

    [Test]
    public async Task MembershipLoss_CommitAfterFencedHeartbeat_FailsFastWithoutSendingUntilRejoin()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 9, 9);
        var script = new HeartbeatScript(this);
        var (listener, _) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener);

        var committedEpochs = new List<int>();
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<OffsetCommitRequest>()!;
                lock (committedEpochs)
                    committedEpochs.Add(request.GenerationIdOrMemberEpoch);

                // Until the member rejoins, the coordinator knows it only at a newer epoch.
                var errorCode = request.GenerationIdOrMemberEpoch <= 0
                    ? ErrorCode.StaleMemberEpoch
                    : ErrorCode.None;
                return ValueTask.FromResult(new OffsetCommitResponse
                {
                    Topics =
                    [
                        new OffsetCommitResponseTopic
                        {
                            Name = "test-topic",
                            Partitions = [new OffsetCommitResponsePartition { PartitionIndex = 0, ErrorCode = errorCode }]
                        }
                    ]
                });
            });

        script.Respond = (_, _) => Error(ErrorCode.FencedMemberEpoch);
        await RunHeartbeatLoopUntilItStopsAsync(coordinator);

        // Without a local fence the commit went out at epoch 0 and kept waiting for an epoch
        // refresh from a heartbeat loop that had already stopped.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var stopwatch = Stopwatch.StartNew();
        var exception = await Assert.That(async () =>
                await coordinator.CommitOffsetsAsync(
                    [new TopicPartitionOffset("test-topic", 0, 10)],
                    retryUntilApiTimeout: true,
                    timeout.Token))
            .Throws<GroupException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(exception.IsRetriable).IsFalse();
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
        await Assert.That(committedEpochs.Count).IsEqualTo(0);

        script.Respond = (_, _) => Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 0));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 0, 10)],
            retryUntilApiTimeout: true,
            timeout.Token);

        await Assert.That(committedEpochs).IsEquivalentTo([6]);
    }

    [Test]
    public async Task MembershipLoss_CommitWaitingForEpochRefresh_StopsWhenTheHeartbeatIsFenced()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 9, 9);
        var script = new HeartbeatScript(this);
        var (listener, _) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener);

        var firstCommitAnswered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commitCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitCount);
                firstCommitAnswered.TrySetResult();
                return ValueTask.FromResult(new OffsetCommitResponse
                {
                    Topics =
                    [
                        new OffsetCommitResponseTopic
                        {
                            Name = "test-topic",
                            Partitions =
                            [
                                new OffsetCommitResponsePartition
                                {
                                    PartitionIndex = 0,
                                    ErrorCode = ErrorCode.StaleMemberEpoch
                                }
                            ]
                        }
                    ]
                });
            });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var commit = coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 0, 10)],
            retryUntilApiTimeout: true,
            timeout.Token).AsTask();
        await firstCommitAnswered.Task.WaitAsync(timeout.Token);

        // The next heartbeat is fenced instead of delivering a refreshed epoch.
        script.Respond = (_, _) => Error(ErrorCode.FencedMemberEpoch);
        await RunHeartbeatLoopUntilItStopsAsync(coordinator);

        var exception = await Assert.That(async () => await commit).Throws<GroupException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(Volatile.Read(ref commitCount)).IsEqualTo(1);
    }

    [Test]
    public async Task MembershipLoss_FailedRejoin_StillFiresLost()
    {
        var script = new HeartbeatScript(this);
        var (listener, calls) = CreateRecordingListener();
        await using var coordinator = await JoinThenLoseCoordinatorAsync(
            script,
            listener,
            rebalanceTimeoutMs: 300);
        calls.Clear();

        // Fenced, then the coordinator goes away for longer than the rebalance timeout.
        script.Respond = (count, _) => count == 1
            ? Error(ErrorCode.FencedMemberEpoch)
            : ValueTask.FromException<ConsumerGroupHeartbeatResponse>(new IOException("coordinator gone"));

        await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None))
            .Throws<KafkaTimeoutException>();

        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1");
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
    }

    private async Task<ConsumerCoordinator> JoinAsync(
        HeartbeatScript script,
        IRebalanceListener listener,
        int sessionTimeoutMs = 45000,
        int rebalanceTimeoutMs = 30000)
    {
        SetupFindCoordinator();
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 5, CreateAssignment(TestTopicId, 0, 1));
        var options = CreateConsumerProtocolOptions(
            rebalanceListener: listener,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1,
            sessionTimeoutMs: sessionTimeoutMs,
            rebalanceTimeoutMs: rebalanceTimeoutMs);
        var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        return coordinator;
    }

    /// <summary>
    /// Joins with [p0, p1], then fails every heartbeat with a transport error for longer than
    /// the session timeout, so the heartbeat loop hands the member back to the foreground rejoin
    /// path still holding its assignment, as it does after a coordinator outage.
    /// </summary>
    private async Task<ConsumerCoordinator> JoinThenLoseCoordinatorAsync(
        HeartbeatScript script,
        IRebalanceListener listener,
        int rebalanceTimeoutMs = 30000)
    {
        var coordinator = await JoinAsync(script, listener, sessionTimeoutMs: 100, rebalanceTimeoutMs: rebalanceTimeoutMs);
        script.Respond = (_, _) =>
            ValueTask.FromException<ConsumerGroupHeartbeatResponse>(new IOException("coordinator connection closed"));
        await RunHeartbeatLoopUntilItStopsAsync(coordinator);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(2);
        script.Reset();
        return coordinator;
    }

    private static async Task RunHeartbeatLoopUntilItStopsAsync(ConsumerCoordinator coordinator)
    {
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, CancellationToken.None).WaitAsync(timeout.Token);
    }

    private static (IRebalanceListener Listener, List<string> Calls) CreateRecordingListener()
    {
        var calls = new List<string>();
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsLostAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Record("lost", callInfo.Arg<IEnumerable<TopicPartition>>()!));
        listener.OnPartitionsRevokedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Record("revoked", callInfo.Arg<IEnumerable<TopicPartition>>()!));
        listener.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Record("assigned", callInfo.Arg<IEnumerable<TopicPartition>>()!));
        return (listener, calls);

        ValueTask Record(string callback, IEnumerable<TopicPartition> partitions)
        {
            var names = partitions
                .OrderBy(static partition => partition.Partition)
                .Select(static partition => $"{partition.Topic}-{partition.Partition}");
            lock (calls)
                calls.Add($"{callback}:{string.Join(',', names)}");
            return ValueTask.CompletedTask;
        }
    }

    private static ValueTask<ConsumerGroupHeartbeatResponse> Joined(
        string memberId,
        int memberEpoch,
        ConsumerGroupHeartbeatAssignment assignment) =>
        ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.None,
            MemberId = memberId,
            MemberEpoch = memberEpoch,
            HeartbeatIntervalMs = 60_000,
            Assignment = assignment
        });

    private static ValueTask<ConsumerGroupHeartbeatResponse> Error(ErrorCode errorCode) =>
        ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
        {
            ErrorCode = errorCode,
            ErrorMessage = errorCode.ToString(),
            MemberEpoch = 0,
            HeartbeatIntervalMs = 60_000
        });

    /// <summary>
    /// Answers ConsumerGroupHeartbeat from a replaceable script. The count passed to the script
    /// restarts at 1 on <see cref="Reset"/>, so each phase of a test scripts its own sequence.
    /// </summary>
    private sealed class HeartbeatScript
    {
        private int _count;

        public HeartbeatScript(ConsumerCoordinatorKip848Tests fixture)
        {
            fixture._connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                    Arg.Any<ConsumerGroupHeartbeatRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var request = callInfo.Arg<ConsumerGroupHeartbeatRequest>()!;
                    lock (Requests)
                        Requests.Add(request);
                    return Respond(Interlocked.Increment(ref _count), request);
                });
        }

        public Func<int, ConsumerGroupHeartbeatRequest, ValueTask<ConsumerGroupHeartbeatResponse>> Respond { get; set; } =
            static (_, _) => throw new InvalidOperationException("No heartbeat response scripted.");

        public List<ConsumerGroupHeartbeatRequest> Requests { get; } = [];

        public void Reset() => Interlocked.Exchange(ref _count, 0);
    }
}
