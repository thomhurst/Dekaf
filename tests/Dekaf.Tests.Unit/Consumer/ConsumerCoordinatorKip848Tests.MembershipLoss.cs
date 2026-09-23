using System.Diagnostics;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Networking;
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
        await SynchronizeAssignmentAsync(coordinator);

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

    [Test]
    public async Task MembershipLoss_OffsetFetchUnknownMember_FiresLostBeforeTheRejoinAssignment()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, 9, 9);
        var script = new HeartbeatScript(this);
        var (listener, calls) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener);
        calls.Clear();
        script.Reset();
        script.Respond = (_, _) => Joined("member-2", memberEpoch: 1, CreateAssignment(TestTopicId, 1));
        SetupOffsetFetch(firstErrorCode: ErrorCode.UnknownMemberId);

        await coordinator.FetchOffsetsAsync([new TopicPartition("test-topic", 1)], CancellationToken.None);

        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1 | assigned:test-topic-1");
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.MemberId).IsEqualTo("member-2");
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(1);
    }

    [Test]
    public async Task MembershipLoss_OffsetFetchUnknownMemberReceivedAsTheCallerCancels_IsStillApplied()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, 9, 9);
        var script = new HeartbeatScript(this);
        var (listener, calls) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener);
        calls.Clear();

        // The coordinator answers UNKNOWN_MEMBER_ID, and the caller cancels before the fence
        // takes the state lock.
        using var caller = new CancellationTokenSource();
        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                caller.Cancel();
                return ValueTask.FromResult(new OffsetFetchResponse
                {
                    Groups =
                    [
                        new OffsetFetchResponseGroup
                        {
                            GroupId = "test-group",
                            Topics = [],
                            ErrorCode = ErrorCode.UnknownMemberId
                        }
                    ]
                });
            });

        await Assert.That(async () => await coordinator.FetchOffsetsAsync(
                [new TopicPartition("test-topic", 1)],
                caller.Token))
            .Throws<OperationCanceledException>();

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
        script.Respond = (_, _) => Joined("member-2", memberEpoch: 1, CreateAssignment(TestTopicId, 1));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1 | assigned:test-topic-1");
    }

    [Test]
    public async Task MembershipLoss_LostCallbackCancelledBeforeTheRejoin_IsDeliveredBeforeTheAssignment()
    {
        var script = new HeartbeatScript(this);
        var (recording, calls) = CreateRecordingListener();
        var lostEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interruptNextLost = 1;
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsLostAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Interlocked.Exchange(ref interruptNextLost, 0) == 1
                ? WaitForCancellationAsync(callInfo.Arg<CancellationToken>())
                : recording.OnPartitionsLostAsync(
                    callInfo.Arg<IEnumerable<TopicPartition>>()!,
                    callInfo.Arg<CancellationToken>()));
        listener.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => recording.OnPartitionsAssignedAsync(
                callInfo.Arg<IEnumerable<TopicPartition>>()!,
                callInfo.Arg<CancellationToken>()));
        await using var coordinator = await JoinAsync(script, listener);
        calls.Clear();

        // A fenced member rejoins. The loss is reported before the join publishes anything, and
        // the caller cancels while OnPartitionsLost runs.
        typeof(ConsumerCoordinator)
            .GetMethod("FenceMembership", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(coordinator, [false]);
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var join = coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, caller.Token).AsTask();
        await lostEntered.Task.WaitAsync(timeout.Token);
        caller.Cancel();
        await Assert.That(async () => await join).Throws<OperationCanceledException>();
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);

        // The next poll reports the loss again, then rejoins and reports the assignment.
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, timeout.Token);

        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1 | assigned:test-topic-0");

        async ValueTask WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            lostEntered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    [Test]
    public async Task MembershipLoss_AssignedCallbackCancelledAfterTheJoin_IsResumedByTheNextPoll()
    {
        var script = new HeartbeatScript(this);
        var (recording, calls) = CreateRecordingListener();
        var assignedEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interruptNextAssigned = 0;
        var firstAssignedCount = 0;
        var first = Substitute.For<IRebalanceListener>();
        first.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref firstAssignedCount);
                return ValueTask.CompletedTask;
            });
        var second = Substitute.For<IRebalanceListener>();
        second.OnPartitionsLostAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => recording.OnPartitionsLostAsync(
                callInfo.Arg<IEnumerable<TopicPartition>>()!,
                callInfo.Arg<CancellationToken>()));
        second.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Interlocked.Exchange(ref interruptNextAssigned, 0) == 1
                ? WaitForCancellationAsync(callInfo.Arg<CancellationToken>())
                : recording.OnPartitionsAssignedAsync(
                    callInfo.Arg<IEnumerable<TopicPartition>>()!,
                    callInfo.Arg<CancellationToken>()));
        await using var coordinator = await JoinAsync(script, first, additionalRebalanceListeners: [second]);
        calls.Clear();
        Interlocked.Exchange(ref firstAssignedCount, 0);

        // A fenced member rejoins; the loss is delivered, then the caller cancels while the
        // second listener runs OnPartitionsAssigned for the assignment the join made current.
        typeof(ConsumerCoordinator)
            .GetMethod("FenceMembership", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(coordinator, [false]);
        Volatile.Write(ref interruptNextAssigned, 1);
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var join = coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, caller.Token).AsTask();
        await assignedEntered.Task.WaitAsync(timeout.Token);
        caller.Cancel();
        await Assert.That(async () => await join).Throws<OperationCanceledException>();
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(Volatile.Read(ref firstAssignedCount)).IsEqualTo(1);

        // The next poll resumes at the interrupted listener without repeating the first.
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, timeout.Token);

        await Assert.That(Volatile.Read(ref firstAssignedCount)).IsEqualTo(1);
        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1 | assigned:test-topic-0");

        async ValueTask WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            assignedEntered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    [Test]
    public async Task MembershipLoss_DeferredAssignedCallback_RunsBeforeAHeartbeatPublishesANewerAssignment()
    {
        var script = new HeartbeatScript(this);
        var calls = new List<string>();
        var assignedEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interruptNextAssigned = 0;
        ConsumerCoordinator? observed = null;
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Interlocked.Exchange(ref interruptNextAssigned, 0) == 1
                ? WaitForCancellationAsync(callInfo.Arg<CancellationToken>())
                : RecordAssigned(callInfo.Arg<IEnumerable<TopicPartition>>()!));
        await using var coordinator = await JoinAsync(script, listener);
        observed = coordinator;
        lock (calls)
            calls.Clear();

        // A fenced member rejoins with [p0]; cancellation interrupts its OnPartitionsAssigned,
        // which stays queued.
        typeof(ConsumerCoordinator)
            .GetMethod("FenceMembership", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(coordinator, [false]);
        Volatile.Write(ref interruptNextAssigned, 1);
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var join = coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, caller.Token).AsTask();
        await assignedEntered.Task.WaitAsync(timeout.Token);
        caller.Cancel();
        await Assert.That(async () => await join).Throws<OperationCanceledException>();

        // Before any poll, a steady heartbeat grows the assignment to [p0, p1]. The queued
        // callback is delivered while [p0] is still the published assignment.
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 7, CreateAssignment(TestTopicId, 0, 1));
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(string.Join(" | ", calls))
            .IsEqualTo("assigned:test-topic-0 owned:test-topic-0 | assigned:test-topic-1 owned:test-topic-0,test-topic-1");

        async ValueTask WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            assignedEntered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        ValueTask RecordAssigned(IEnumerable<TopicPartition> partitions)
        {
            var owned = observed?.Assignment ?? (IEnumerable<TopicPartition>)[];
            lock (calls)
                calls.Add($"assigned:{Names(partitions)} owned:{Names(owned)}");
            return ValueTask.CompletedTask;
        }

        static string Names(IEnumerable<TopicPartition> partitions) => string.Join(',', partitions
            .OrderBy(static partition => partition.Partition)
            .Select(static partition => $"{partition.Topic}-{partition.Partition}"));
    }

    [Test]
    public async Task MembershipLoss_FenceFromAHeartbeatOfTheReplacedMembership_IsIgnored()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, 9, 9);
        var script = new HeartbeatScript(this);
        var (listener, calls) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener);
        calls.Clear();
        script.Reset();

        // The old membership's heartbeat stays in flight while an offset fetch finds the member
        // unknown and rejoins it; that heartbeat's fence then answers for a membership that is gone.
        var staleHeartbeatSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleHeartbeatResponse = new TaskCompletionSource<ConsumerGroupHeartbeatResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var nextStaleLoopHeartbeat = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Respond = (count, _) =>
        {
            switch (count)
            {
                case 1:
                    staleHeartbeatSent.TrySetResult();
                    return new ValueTask<ConsumerGroupHeartbeatResponse>(staleHeartbeatResponse.Task);
                case 2:
                    return Joined("member-2", memberEpoch: 1, CreateAssignment(TestTopicId, 1));
                default:
                    // The stale loop survived its discarded response and beat again.
                    nextStaleLoopHeartbeat.TrySetResult();
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-2",
                        MemberEpoch = 1,
                        HeartbeatIntervalMs = 60_000
                    });
            }
        };
        SetupOffsetFetch(firstErrorCode: ErrorCode.UnknownMemberId);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var staleLoopStop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        var staleLoop = InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, staleLoopStop.Token);
        await staleHeartbeatSent.Task.WaitAsync(timeout.Token);

        await coordinator.FetchOffsetsAsync([new TopicPartition("test-topic", 1)], timeout.Token);
        await Assert.That(coordinator.MemberId).IsEqualTo("member-2");

        // The rejoined membership's own loop is parked on the 60 s interval; only the stale loop
        // beats again at 1 ms, and only after it has handled the fenced response.
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        staleHeartbeatResponse.SetResult(await Error(ErrorCode.FencedMemberEpoch));
        await Task.WhenAny(nextStaleLoopHeartbeat.Task, staleLoop).WaitAsync(timeout.Token);
        staleLoopStop.Cancel();
        await staleLoop.WaitAsync(timeout.Token);

        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1 | assigned:test-topic-1");
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.MemberId).IsEqualTo("member-2");
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(1);
    }

    [Test]
    public async Task MembershipLoss_FenceForAMembershipThatJoinedWhileTheHeartbeatLeasedItsConnection_IsApplied()
    {
        var script = new HeartbeatScript(this);
        var (listener, calls) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener);
        calls.Clear();

        // The heartbeat loop reaches its connection lease under member-1; the member is fenced
        // and rejoins as member-2 before the lease completes.
        var leaseReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdNextLease = 1;
        _connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Exchange(ref holdNextLease, 0) == 1
                ? HoldLeaseAsync()
                : ValueTask.FromResult(_connection));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var loopStop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        var loop = InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, loopStop.Token);
        await leaseReached.Task.WaitAsync(timeout.Token);

        typeof(ConsumerCoordinator)
            .GetMethod("FenceMembership", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(coordinator, [false]);
        script.Respond = (_, _) => Joined("member-2", memberEpoch: 1, CreateAssignment(TestTopicId, 1));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, timeout.Token);
        await Assert.That(coordinator.MemberId).IsEqualTo("member-2");

        // The loop's heartbeat is sent for member-2, and the coordinator fences member-2.
        script.Respond = (_, _) => Error(ErrorCode.FencedMemberEpoch);
        releaseLease.SetResult();
        await loop.WaitAsync(timeout.Token);
        loopStop.Cancel();

        ConsumerGroupHeartbeatRequest lastRequest;
        lock (script.Requests)
            lastRequest = script.Requests[^1];
        await Assert.That(lastRequest.MemberId).IsEqualTo("member-2");
        await Assert.That(lastRequest.MemberEpoch).IsEqualTo(1);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
        await Assert.That(string.Join(" | ", calls))
            .IsEqualTo("lost:test-topic-0,test-topic-1 | assigned:test-topic-1 | lost:test-topic-1");

        async ValueTask<IKafkaConnection> HoldLeaseAsync()
        {
            leaseReached.TrySetResult();
            await releaseLease.Task;
            return _connection;
        }
    }

    [Test]
    public async Task MembershipLoss_LostCallbackRetry_DoesNotRepeatListenersThatCompleted()
    {
        var script = new HeartbeatScript(this);
        var (recording, calls) = CreateRecordingListener();
        var lostEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interruptNextLost = 1;
        var firstLostCount = 0;
        var first = Substitute.For<IRebalanceListener>();
        first.OnPartitionsLostAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref firstLostCount);
                return ValueTask.CompletedTask;
            });
        var second = Substitute.For<IRebalanceListener>();
        second.OnPartitionsLostAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Interlocked.Exchange(ref interruptNextLost, 0) == 1
                ? WaitForCancellationAsync(callInfo.Arg<CancellationToken>())
                : recording.OnPartitionsLostAsync(
                    callInfo.Arg<IEnumerable<TopicPartition>>()!,
                    callInfo.Arg<CancellationToken>()));
        second.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => recording.OnPartitionsAssignedAsync(
                callInfo.Arg<IEnumerable<TopicPartition>>()!,
                callInfo.Arg<CancellationToken>()));
        await using var coordinator = await JoinAsync(script, first, additionalRebalanceListeners: [second]);
        calls.Clear();

        // The first listener completes OnPartitionsLost; the heartbeat stop interrupts the second.
        script.Respond = (_, _) => Error(ErrorCode.FencedMemberEpoch);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var loopStop = new CancellationTokenSource();
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        var loop = InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, loopStop.Token);
        await lostEntered.Task.WaitAsync(timeout.Token);
        loopStop.Cancel();
        await loop.WaitAsync(timeout.Token);
        await Assert.That(Volatile.Read(ref firstLostCount)).IsEqualTo(1);

        // The retry before the rejoin assignment resumes at the interrupted listener.
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 0));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, timeout.Token);

        await Assert.That(Volatile.Read(ref firstLostCount)).IsEqualTo(1);
        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1 | assigned:test-topic-0");

        async ValueTask WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            lostEntered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    [Test]
    public async Task MembershipLoss_StoppingTheHeartbeat_CancelsAPendingLostCallback()
    {
        var script = new HeartbeatScript(this);
        var lostEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lostCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsLostAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellationAsync(callInfo.Arg<CancellationToken>()));
        await using var coordinator = await JoinAsync(script, listener);

        script.Respond = (_, _) => Error(ErrorCode.FencedMemberEpoch);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var loopStop = new CancellationTokenSource();
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        var loop = InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, loopStop.Token);
        await lostEntered.Task.WaitAsync(timeout.Token);

        loopStop.Cancel();

        await lostCancelled.Task.WaitAsync(timeout.Token);
        await loop.WaitAsync(timeout.Token);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);

        async ValueTask WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            lostEntered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                lostCancelled.TrySetResult();
                throw;
            }
        }
    }

    [Test]
    public async Task MembershipLoss_LostCallbackInterruptedByHeartbeatStop_IsReportedBeforeTheRejoinAssignment()
    {
        var script = new HeartbeatScript(this);
        var (recording, calls) = CreateRecordingListener();
        var lostEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interruptNextLost = 1;
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsLostAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Interlocked.Exchange(ref interruptNextLost, 0) == 1
                ? WaitForCancellationAsync(callInfo.Arg<CancellationToken>())
                : recording.OnPartitionsLostAsync(
                    callInfo.Arg<IEnumerable<TopicPartition>>()!,
                    callInfo.Arg<CancellationToken>()));
        listener.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => recording.OnPartitionsAssignedAsync(
                callInfo.Arg<IEnumerable<TopicPartition>>()!,
                callInfo.Arg<CancellationToken>()));
        await using var coordinator = await JoinAsync(script, listener);
        calls.Clear();

        script.Respond = (_, _) => Error(ErrorCode.FencedMemberEpoch);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var loopStop = new CancellationTokenSource();
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        var loop = InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, loopStop.Token);
        await lostEntered.Task.WaitAsync(timeout.Token);
        loopStop.Cancel();
        await loop.WaitAsync(timeout.Token);

        // The interrupted callback never completed: the loss is still reported, before the
        // rejoin's assignment.
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 0));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, timeout.Token);

        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1 | assigned:test-topic-0");

        async ValueTask WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            lostEntered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    [Test]
    public async Task MembershipLoss_FenceReceivedWhileTheHeartbeatStops_IsStillApplied()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 9, 9);
        var script = new HeartbeatScript(this);
        var (listener, calls) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener);
        calls.Clear();
        var commitCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitCount);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        // The fence arrives while another operation holds the state lock, and the heartbeat is
        // stopped before the loop can take it.
        var coordinatorLock = GetPrivateField<SemaphoreSlim>(coordinator, "_lock");
        var fenceReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Respond = (_, _) =>
        {
            coordinatorLock.Wait();
            fenceReceived.TrySetResult();
            return Error(ErrorCode.FencedMemberEpoch);
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var loopStop = new CancellationTokenSource();
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        var loop = InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, loopStop.Token);
        await fenceReceived.Task.WaitAsync(timeout.Token);
        loopStop.Cancel();
        coordinatorLock.Release();
        await loop.WaitAsync(timeout.Token);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
        var fenced = await Assert.That(async () => await coordinator.CommitOffsetsAsync(
                [new TopicPartitionOffset("test-topic", 0, 10)],
                retryUntilApiTimeout: true,
                timeout.Token))
            .Throws<GroupException>();
        await Assert.That(fenced!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(Volatile.Read(ref commitCount)).IsEqualTo(0);

        script.Respond = (_, _) => Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 0));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, timeout.Token);
        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1 | assigned:test-topic-0");
    }

    [Test]
    public async Task CommitOffsetsAsync_StartedBeforeAFenceAndRejoin_IsNotSentUnderTheNewMembership()
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
                lock (committedEpochs)
                    committedEpochs.Add(callInfo.Arg<OffsetCommitRequest>()!.GenerationIdOrMemberEpoch);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        // The commit has its offsets but waits for the commit lock while the member is fenced,
        // rejoins and resynchronizes its assignment.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var commitLock = GetPrivateField<SemaphoreSlim>(coordinator, "_commitLock");
        await commitLock.WaitAsync(timeout.Token);
        Task commit;
        try
        {
            commit = coordinator.CommitOffsetsAsync(
                [new TopicPartitionOffset("test-topic", 0, 10)],
                retryUntilApiTimeout: true,
                timeout.Token).AsTask();

            script.Respond = (_, _) => Error(ErrorCode.FencedMemberEpoch);
            await RunHeartbeatLoopUntilItStopsAsync(coordinator);
            script.Respond = (_, _) => Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 1));
            await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, timeout.Token);
            await SynchronizeAssignmentAsync(coordinator);
        }
        finally
        {
            commitLock.Release();
        }

        // Its offsets were taken under the lost membership; epoch 6 must not carry them.
        var exception = await Assert.That(async () => await commit).Throws<GroupException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(exception.IsRetriable).IsFalse();
        await Assert.That(committedEpochs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CommitOffsetsAsync_BuiltWhileARejoinWritesTheNewIdentity_IsNotSent()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 9, 9);
        var script = new HeartbeatScript(this);
        var commitCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitCount);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        // A commit (the auto-commit loop, say) starts under member-1 and is held at its
        // connection lease. It builds its request while the rejoin is processing its response:
        // the new member id and epoch are already written, the join has not yet returned.
        var commitLeaseReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCommitLease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdNextLease = 0;
        _connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Exchange(ref holdNextLease, 0) == 1
                ? HoldLeaseAsync()
                : ValueTask.FromResult(_connection));
        Task? commit = null;
        var commitDuringRejoin = 0;
        void OnPartitionsRevoking(IReadOnlyList<TopicPartition> revoked)
        {
            if (Interlocked.Exchange(ref commitDuringRejoin, 0) != 1)
                return;

            releaseCommitLease.SetResult();
            try
            {
                commit!.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException)
            {
                // Asserted below.
            }
        }

        async ValueTask<IKafkaConnection> HoldLeaseAsync()
        {
            commitLeaseReached.TrySetResult();
            await releaseCommitLease.Task;
            return _connection;
        }

        SetupFindCoordinator();
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 5, CreateAssignment(TestTopicId, 0, 1));
        var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(),
            _connectionPool,
            _metadataManager,
            logger: null,
            getConnectionCount: null,
            onPartitionsRevoked: null,
            onPartitionsRevoking: OnPartitionsRevoking);
        await using var coordinatorLifetime = coordinator;
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Volatile.Write(ref holdNextLease, 1);
        commit = coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 0, 10)],
            CancellationToken.None).AsTask();
        await commitLeaseReached.Task.WaitAsync(timeout.Token);

        // The coordinator was lost; the member rejoins as member-2 and loses partition 0.
        coordinator.RequestRejoin();
        Volatile.Write(ref commitDuringRejoin, 1);
        script.Respond = (_, _) => Joined("member-2", memberEpoch: 9, CreateAssignment(TestTopicId, 1));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, timeout.Token);

        await Assert.That(coordinator.MemberId).IsEqualTo("member-2");
        var failure = await Assert.That(async () => await commit.WaitAsync(timeout.Token)).Throws<GroupException>();
        await Assert.That(failure!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(Volatile.Read(ref commitCount)).IsEqualTo(0);
    }

    [Test]
    public async Task CommitOffsetsAsync_DuringARejoinThatFailsWithARetriableError_IsStillSent()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 9, 9);
        var script = new HeartbeatScript(this);
        var (listener, _) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener);
        var commitCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitCount);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        // A commit under member-1 is held at its connection lease while the coordinator is lost.
        var commitLeaseReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCommitLease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdNextLease = 1;
        _connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Exchange(ref holdNextLease, 0) == 1
                ? HoldLeaseAsync()
                : ValueTask.FromResult(_connection));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var commit = coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 0, 10)],
            CancellationToken.None).AsTask();
        await commitLeaseReached.Task.WaitAsync(timeout.Token);

        // The rejoin's first attempt fails with a retriable error, which changes nothing about
        // the membership. The commit completes during the retry, then the rejoin succeeds.
        coordinator.RequestRejoin();
        script.Reset();
        script.Respond = (count, _) =>
        {
            if (count == 1)
                return Error(ErrorCode.CoordinatorNotAvailable);

            releaseCommitLease.TrySetResult();
            try
            {
                commit.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException)
            {
                // Asserted below.
            }

            return Joined("member-1", memberEpoch: 5, CreateAssignment(TestTopicId, 0, 1));
        };
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, timeout.Token);

        await Assert.That(async () => await commit.WaitAsync(timeout.Token)).ThrowsNothing();
        await Assert.That(Volatile.Read(ref commitCount)).IsEqualTo(1);

        async ValueTask<IKafkaConnection> HoldLeaseAsync()
        {
            commitLeaseReached.TrySetResult();
            await releaseCommitLease.Task;
            return _connection;
        }
    }

    [Test]
    public async Task MembershipLoss_LostCallbackInterruptedByDisposal_IsDeliveredBeforeTheLocksAreDisposed()
    {
        var script = new HeartbeatScript(this);
        var (recording, calls) = CreateRecordingListener();
        var lostEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interruptNextLost = 1;
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsLostAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Interlocked.Exchange(ref interruptNextLost, 0) == 1
                ? WaitForCancellationAsync(callInfo.Arg<CancellationToken>())
                : recording.OnPartitionsLostAsync(
                    callInfo.Arg<IEnumerable<TopicPartition>>()!,
                    callInfo.Arg<CancellationToken>()));
        var coordinator = await JoinAsync(script, listener);
        calls.Clear();

        // The coordinator's own heartbeat is fenced, and disposal stops it while
        // OnPartitionsLost runs.
        script.Respond = (_, _) => Error(ErrorCode.FencedMemberEpoch);
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        await (ValueTask)typeof(ConsumerCoordinator)
            .GetMethod("StartConsumerProtocolHeartbeatAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(coordinator, null)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await lostEntered.Task.WaitAsync(timeout.Token);

        await coordinator.DisposeAsync().AsTask().WaitAsync(timeout.Token);

        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1");

        async ValueTask WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            lostEntered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    [Test]
    public async Task MembershipLoss_FenceDuringTheRejoinRetryLoop_LostScopeShowsTheAssignmentItWasQueuedUnder()
    {
        var script = new HeartbeatScript(this);
        var calls = new List<string>();
        TopicPartition[] scopeAssignment = [];
        var listener = Substitute.For<IConsumerAwareRebalanceListener>();
        listener.OnPartitionsLostAsync(
                Arg.Any<IRebalanceConsumer>(),
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => Record("lost", callInfo.Arg<IEnumerable<TopicPartition>>()!));
        listener.OnPartitionsAssignedAsync(
                Arg.Any<IRebalanceConsumer>(),
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => Record("assigned", callInfo.Arg<IEnumerable<TopicPartition>>()!));
        var consumer = Substitute.For<IKafkaConsumer<byte[], byte[]>>();
        consumer.Positions.Returns(Substitute.For<IConsumerPositions>());
        SetupFindCoordinator();
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 5, CreateAssignment(TestTopicId, 0, 1));
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(
                consumerAwareRebalanceListener: listener,
                retryBackoffMs: 1,
                retryBackoffMaxMs: 1),
            _connectionPool,
            _metadataManager,
            logger: null,
            getConnectionCount: null,
            onPartitionsRevoked: null,
            onPartitionsRevoking: null,
            onPartitionsRevokedAsync: null,
            createRebalanceConsumerScope: (current, added) =>
            {
                scopeAssignment = current.ToArray();
                return new RebalanceConsumerScope<byte[], byte[]>(consumer, current, added);
            });
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        lock (calls)
            calls.Clear();

        // After a coordinator outage the rejoin's first attempt is fenced, which queues [p0, p1]
        // as lost; the immediate retry succeeds with [p1] and publishes it before the loss is
        // delivered.
        coordinator.RequestRejoin();
        script.Reset();
        script.Respond = (count, _) => count == 1
            ? Error(ErrorCode.FencedMemberEpoch)
            : Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 1));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(string.Join(" | ", calls)).IsEqualTo(
            "lost:test-topic-0,test-topic-1@[] | assigned:test-topic-1@[test-topic-1]");

        ValueTask Record(string callback, IEnumerable<TopicPartition> partitions)
        {
            static string Names(IEnumerable<TopicPartition> tps) => string.Join(
                ',',
                tps.OrderBy(static partition => partition.Partition)
                    .Select(static partition => $"{partition.Topic}-{partition.Partition}"));
            lock (calls)
                calls.Add($"{callback}:{Names(partitions)}@[{Names(scopeAssignment)}]");
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task MembershipLoss_FenceWhileAPublishedAssignmentAwaitsDelivery_ReportsItAfterTheAssignment()
    {
        var script = new HeartbeatScript(this);
        var calls = new List<string>();
        TopicPartition[] scopeAssignment = [];
        var listener = Substitute.For<IConsumerAwareRebalanceListener>();
        listener.OnPartitionsLostAsync(
                Arg.Any<IRebalanceConsumer>(),
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => Record("lost", callInfo.Arg<IEnumerable<TopicPartition>>()!));
        listener.OnPartitionsAssignedAsync(
                Arg.Any<IRebalanceConsumer>(),
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => Record("assigned", callInfo.Arg<IEnumerable<TopicPartition>>()!));
        var consumer = Substitute.For<IKafkaConsumer<byte[], byte[]>>();
        consumer.Positions.Returns(Substitute.For<IConsumerPositions>());
        SetupFindCoordinator();
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(consumerAwareRebalanceListener: listener),
            _connectionPool,
            _metadataManager,
            logger: null,
            getConnectionCount: null,
            onPartitionsRevoked: null,
            onPartitionsRevoking: null,
            onPartitionsRevokedAsync: null,
            createRebalanceConsumerScope: (current, added) =>
            {
                scopeAssignment = current.ToArray();
                return new RebalanceConsumerScope<byte[], byte[]>(consumer, current, added);
            });

        // The join publishes [p0, p1], then waits for the listener lock (another delivery holds
        // it). Meanwhile an OffsetFetch fences the member, and the join's caller cancels.
        var listenerLock = GetPrivateField<SemaphoreSlim>(coordinator, "_rebalanceListenerLock");
        var coordinatorLock = GetPrivateField<SemaphoreSlim>(coordinator, "_lock");
        await listenerLock.WaitAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Respond = (_, _) =>
        {
            published.TrySetResult();
            return Joined("member-1", memberEpoch: 5, CreateAssignment(TestTopicId, 0, 1));
        };
        var join = coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, caller.Token).AsTask();
        await published.Task.WaitAsync(timeout.Token);

        // The join holds the state lock until it has published and become Stable.
        await coordinatorLock.WaitAsync(timeout.Token);
        try
        {
            typeof(ConsumerCoordinator)
                .GetMethod("FenceMembership", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(coordinator, [true]);
        }
        finally
        {
            coordinatorLock.Release();
        }

        caller.Cancel();
        await Assert.That(async () => await join).Throws<OperationCanceledException>();
        listenerLock.Release();
        await coordinator.InvokePendingRebalanceCallbacksUnlessCancelledAsync(timeout.Token);

        // The assignment was published before the fence: it is reported first, with the
        // assignment it published, and the loss follows.
        await Assert.That(string.Join(" | ", calls)).IsEqualTo(
            "assigned:test-topic-0,test-topic-1@[test-topic-0,test-topic-1] | lost:test-topic-0,test-topic-1@[]");

        ValueTask Record(string callback, IEnumerable<TopicPartition> partitions)
        {
            static string Names(IEnumerable<TopicPartition> tps) => string.Join(
                ',',
                tps.OrderBy(static partition => partition.Partition)
                    .Select(static partition => $"{partition.Topic}-{partition.Partition}"));
            lock (calls)
                calls.Add($"{callback}:{Names(partitions)}@[{Names(scopeAssignment)}]");
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task MembershipLoss_CallbacksQueuedBeforeARejoin_SeeTheirOwnAssignment()
    {
        var script = new HeartbeatScript(this);
        var calls = new List<string>();
        ConsumerCoordinator? coordinator = null;
        var throwNextRevoked = 0;
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsLostAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Record("lost", callInfo.Arg<IEnumerable<TopicPartition>>()!));
        listener.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Record("assigned", callInfo.Arg<IEnumerable<TopicPartition>>()!));
        listener.OnPartitionsRevokedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Interlocked.Exchange(ref throwNextRevoked, 0) == 1
                ? ValueTask.FromException(new OperationCanceledException("listener gave up"))
                : Record("revoked", callInfo.Arg<IEnumerable<TopicPartition>>()!));
        coordinator = await JoinAsync(script, listener);
        await using var coordinatorLifetime = coordinator;
        lock (calls)
            calls.Clear();

        // A steady heartbeat revokes p1. The listener throws OperationCanceledException without
        // the heartbeat being stopped, so the revocation stays queued and the loop gives up the
        // coordinator, leaving the member to rejoin before any poll drains the queue.
        Volatile.Write(ref throwNextRevoked, 1);
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 0));
        await RunHeartbeatLoopUntilItStopsAsync(coordinator);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);

        script.Respond = (_, _) => Joined("member-1", memberEpoch: 7, CreateAssignment(TestTopicId, 1));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        // The queued revocation sees the assignment it describes, not the rejoin's newer one.
        await Assert.That(string.Join(" | ", calls)).IsEqualTo(
            "revoked:test-topic-1@[test-topic-0] | " +
            "revoked:test-topic-0@[test-topic-1] | assigned:test-topic-1@[test-topic-1]");

        ValueTask Record(string callback, IEnumerable<TopicPartition> partitions)
        {
            static string Names(IEnumerable<TopicPartition> tps) => string.Join(
                ',',
                tps.OrderBy(static partition => partition.Partition)
                    .Select(static partition => $"{partition.Topic}-{partition.Partition}"));
            lock (calls)
                calls.Add($"{callback}:{Names(partitions)}@[{Names(coordinator!.Assignment)}]");
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task CommitOffsetsAsync_AfterHeartbeatFence_FailsFastUntilAssignmentIsResynchronized()
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
                lock (committedEpochs)
                    committedEpochs.Add(callInfo.Arg<OffsetCommitRequest>()!.GenerationIdOrMemberEpoch);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        script.Respond = (_, _) => Error(ErrorCode.FencedMemberEpoch);
        await RunHeartbeatLoopUntilItStopsAsync(coordinator);

        // Offsets consumed under the lost membership must not be committed: another member may
        // own the partition and have committed past them.
        var fenced = await Assert.That(async () => await coordinator.CommitOffsetsAsync(
                [new TopicPartitionOffset("test-topic", 0, 10)],
                retryUntilApiTimeout: true,
                CancellationToken.None))
            .Throws<GroupException>();
        await Assert.That(fenced!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(fenced.IsRetriable).IsFalse();

        // Rejoining alone does not lift the fence: until the consumer synchronizes the
        // assignment it still holds the offsets it stored for the lost partitions, and a commit
        // under the new epoch would send them.
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 6, CreateAssignment(TestTopicId, 1));
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        var stillFenced = await Assert.That(async () => await coordinator.CommitOffsetsAsync(
                [new TopicPartitionOffset("test-topic", 0, 10)],
                retryUntilApiTimeout: true,
                CancellationToken.None))
            .Throws<GroupException>();
        await Assert.That(stillFenced!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);

        var sync = await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);
        await Assert.That(sync.Revocations).IsNotNull();
        coordinator.AcknowledgeAssignmentSync(sync.Version);

        await coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 1, 10)],
            retryUntilApiTimeout: true,
            CancellationToken.None);
        await Assert.That(committedEpochs).IsEquivalentTo([6]);
    }

    [Test]
    public async Task CommitOffsetsAsync_StaleMemberEpochAfterHeartbeatStopped_FailsFastWithoutRetrying()
    {
        // The heartbeat loop gave up after a session timeout without transport, so no refreshed
        // epoch will ever arrive. Waiting for one and retrying until the API timeout only delays
        // a failure the application has to handle by polling to rejoin.
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 9, 9);
        var script = new HeartbeatScript(this);
        var (listener, _) = CreateRecordingListener();
        await using var coordinator = await JoinThenLoseCoordinatorAsync(
            script,
            listener,
            defaultApiTimeoutMs: 5_000);
        var commitRequestCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitRequestCount);
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

        var exception = await Assert.That(async () => await coordinator.CommitOffsetsAsync(
                [new TopicPartitionOffset("test-topic", 0, 10)],
                retryUntilApiTimeout: true,
                CancellationToken.None))
            .Throws<GroupException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.StaleMemberEpoch);
        await Assert.That(exception.IsRetriable).IsFalse();
        await Assert.That(Volatile.Read(ref commitRequestCount)).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MembershipLoss_LeaveWithAnUnreportedLoss_ReportsThePartitionsLost(bool forgetMember)
    {
        var script = new HeartbeatScript(this);
        var (listener, calls) = CreateRecordingListener();
        await using var coordinator = await JoinAsync(script, listener);
        calls.Clear();

        // A fence whose OnPartitionsLost has not run yet (for example the heartbeat stop
        // interrupted it), followed by a leave. An unknown member has no leave to send.
        typeof(ConsumerCoordinator)
            .GetMethod("FenceMembership", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(coordinator, [forgetMember]);
        await Assert.That(calls.Count).IsEqualTo(0);

        script.Respond = (_, _) => ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.None,
            MemberId = "member-1",
            MemberEpoch = -1,
            HeartbeatIntervalMs = 60_000
        });
        await coordinator.LeaveGroupAsync(cancellationToken: CancellationToken.None);

        await Assert.That(string.Join(" | ", calls)).IsEqualTo("lost:test-topic-0,test-topic-1");
    }

    private void SetupOffsetFetch(ErrorCode firstErrorCode)
    {
        var requestCount = 0;
        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new OffsetFetchResponse
            {
                Groups =
                [
                    new OffsetFetchResponseGroup
                    {
                        GroupId = "test-group",
                        Topics = [],
                        ErrorCode = Interlocked.Increment(ref requestCount) == 1 ? firstErrorCode : ErrorCode.None
                    }
                ]
            }));
    }

    private async Task<ConsumerCoordinator> JoinAsync(
        HeartbeatScript script,
        IRebalanceListener listener,
        int sessionTimeoutMs = 45000,
        int rebalanceTimeoutMs = 30000,
        int defaultApiTimeoutMs = 60000,
        IRebalanceListener[]? additionalRebalanceListeners = null)
    {
        SetupFindCoordinator();
        script.Respond = (_, _) => Joined("member-1", memberEpoch: 5, CreateAssignment(TestTopicId, 0, 1));
        var options = CreateConsumerProtocolOptions(
            rebalanceListener: listener,
            additionalRebalanceListeners: additionalRebalanceListeners,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1,
            sessionTimeoutMs: sessionTimeoutMs,
            rebalanceTimeoutMs: rebalanceTimeoutMs,
            defaultApiTimeoutMs: defaultApiTimeoutMs);
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
        int rebalanceTimeoutMs = 30000,
        int defaultApiTimeoutMs = 60000)
    {
        var coordinator = await JoinAsync(
            script,
            listener,
            sessionTimeoutMs: 100,
            rebalanceTimeoutMs: rebalanceTimeoutMs,
            defaultApiTimeoutMs: defaultApiTimeoutMs);
        script.Respond = (_, _) =>
            ValueTask.FromException<ConsumerGroupHeartbeatResponse>(new IOException("coordinator connection closed"));
        await RunHeartbeatLoopUntilItStopsAsync(coordinator);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(2);
        script.Reset();
        return coordinator;
    }

    private static async Task SynchronizeAssignmentAsync(ConsumerCoordinator coordinator)
    {
        var sync = await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);
        coordinator.AcknowledgeAssignmentSync(sync.Version);
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
