using Dekaf.Consumer;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Leaving the group on Unsubscribe or a switch to manual assignment
/// (<see cref="ConsumerCoordinator.RequestLeaveGroup"/>): the owned partitions are revoked, the
/// KIP-848 leave heartbeat is sent, the heartbeat stops, nothing is published or joined while the
/// leave is requested, and a later subscription joins with a fresh membership.
/// </summary>
public sealed partial class ConsumerCoordinatorKip848Tests
{
    private static readonly HashSet<string> LeaveTestTopics = ["test-topic"];

    [Test]
    [Timeout(10_000)]
    public async Task RequestLeaveGroup_RevokesOwnedPartitionsThenSendsLeaveAndStopsHeartbeat(
        CancellationToken cancellationToken)
    {
        var script = new HeartbeatScript(this);
        SetupFindCoordinator();
        var (listener, calls) = CreateRecordingListener();
        script.Respond = (_, request) => RespondRecordingLeave(calls, request, CreateAssignment(TestTopicId, 0, 1));
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(rebalanceListener: listener), _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(LeaveTestTopics, cancellationToken);
        await Assert.That(GetPrivateField<Task?>(coordinator, "_heartbeatTask") is not null).IsTrue();

        coordinator.RequestLeaveGroup();
        await GetPrivateField<Task>(coordinator, "_pendingLeave").WaitAsync(cancellationToken);

        await Assert.That(string.Join(" | ", calls))
            .IsEqualTo("assigned:test-topic-0,test-topic-1 | revoked:test-topic-0,test-topic-1 | leave");
        var leave = script.Requests[^1];
        await Assert.That(leave.MemberEpoch).IsEqualTo(-1);
        await Assert.That(leave.MemberId).IsEqualTo("member-1");
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.MemberId).IsNull();
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
        await Assert.That(GetPrivateField<Task?>(coordinator, "_heartbeatTask") is null).IsTrue();
    }

    [Test]
    [Timeout(10_000)]
    public async Task RequestLeaveGroup_StaticMember_SendsPermanentLeave(CancellationToken cancellationToken)
    {
        var script = new HeartbeatScript(this);
        SetupFindCoordinator();
        script.Respond = (_, request) => RespondRecordingLeave([], request, CreateAssignment(TestTopicId, 0));
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(groupInstanceId: "static-1"), _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(LeaveTestTopics, cancellationToken);
        coordinator.RequestLeaveGroup();
        await GetPrivateField<Task>(coordinator, "_pendingLeave").WaitAsync(cancellationToken);

        var leave = script.Requests[^1];
        await Assert.That(leave.MemberEpoch).IsEqualTo(-1);
        await Assert.That(leave.InstanceId).IsEqualTo("static-1");
    }

    [Test]
    [Timeout(10_000)]
    public async Task RequestLeaveGroup_NoJoinUntilResumed_ThenRejoinsWithFreshMembership(
        CancellationToken cancellationToken)
    {
        var script = new HeartbeatScript(this);
        SetupFindCoordinator();
        var (listener, calls) = CreateRecordingListener();
        script.Respond = (_, request) => RespondRecordingLeave(calls, request, CreateAssignment(TestTopicId, 0));
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(rebalanceListener: listener), _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(LeaveTestTopics, cancellationToken);
        coordinator.RequestLeaveGroup();
        await GetPrivateField<Task>(coordinator, "_pendingLeave").WaitAsync(cancellationToken);
        var requestsAfterLeave = script.Requests.Count;

        // A poll or prefetch that read the subscription before it was cleared must not rejoin.
        await coordinator.EnsureActiveGroupAsync(LeaveTestTopics, cancellationToken);
        await Assert.That(script.Requests.Count).IsEqualTo(requestsAfterLeave);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);

        coordinator.ResumeGroupMembership();
        await coordinator.EnsureActiveGroupAsync(LeaveTestTopics, cancellationToken);

        var rejoin = script.Requests[^1];
        await Assert.That(rejoin.MemberEpoch).IsEqualTo(0);
        await Assert.That(rejoin.SubscribedTopicNames!.Single()).IsEqualTo("test-topic");
        await Assert.That(rejoin.TopicPartitions).IsEmpty();
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(GetPrivateField<Task?>(coordinator, "_heartbeatTask") is not null).IsTrue();
        await Assert.That(string.Join(" | ", calls))
            .IsEqualTo("assigned:test-topic-0 | revoked:test-topic-0 | leave | assigned:test-topic-0");
    }

    [Test]
    [Timeout(10_000)]
    public async Task RequestLeaveGroup_DuringInFlightHeartbeat_DiscardsItsAssignment(
        CancellationToken cancellationToken)
    {
        var script = new HeartbeatScript(this);
        SetupFindCoordinator();
        var (listener, calls) = CreateRecordingListener();
        var heartbeatSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHeartbeat = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Respond = (_, request) =>
        {
            if (request.MemberEpoch == 1)
            {
                // The steady heartbeat: its response grows the assignment, after the leave.
                heartbeatSent.TrySetResult();
                return RespondAfterAsync(releaseHeartbeat.Task, CreateAssignment(TestTopicId, 0, 1));
            }

            return RespondRecordingLeave(calls, request, CreateAssignment(TestTopicId, 0));
        };
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(rebalanceListener: listener), _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(LeaveTestTopics, cancellationToken);
        await coordinator.StopHeartbeatAsync();

        var heartbeat = InvokeSteadyConsumerGroupHeartbeatAsync(coordinator).AsTask();
        await heartbeatSent.Task.WaitAsync(cancellationToken);

        coordinator.RequestLeaveGroup();
        await GetPrivateField<Task>(coordinator, "_pendingLeave").WaitAsync(cancellationToken);
        releaseHeartbeat.SetResult();
        await heartbeat.WaitAsync(cancellationToken);

        await Assert.That(string.Join(" | ", calls))
            .IsEqualTo("assigned:test-topic-0 | revoked:test-topic-0 | leave");
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
    }

    [Test]
    [Timeout(10_000)]
    public async Task RequestLeaveGroup_DuringInFlightJoin_LeavesAfterJoinWithoutHeartbeat(
        CancellationToken cancellationToken)
    {
        var script = new HeartbeatScript(this);
        SetupFindCoordinator();
        var (listener, calls) = CreateRecordingListener();
        var joinSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseJoin = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        script.Respond = (_, request) =>
        {
            if (request.MemberEpoch == 0)
            {
                joinSent.TrySetResult();
                return RespondAfterAsync(releaseJoin.Task, CreateAssignment(TestTopicId, 0, 1));
            }

            return RespondRecordingLeave(calls, request, CreateAssignment(TestTopicId, 0, 1));
        };
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(rebalanceListener: listener), _connectionPool, _metadataManager);

        // The poll's join is in flight when the application unsubscribes.
        var join = coordinator.EnsureActiveGroupAsync(LeaveTestTopics, cancellationToken).AsTask();
        await joinSent.Task.WaitAsync(cancellationToken);
        coordinator.RequestLeaveGroup();
        var pendingLeave = GetPrivateField<Task>(coordinator, "_pendingLeave");
        releaseJoin.SetResult();

        await join.WaitAsync(cancellationToken);
        await pendingLeave.WaitAsync(cancellationToken);

        await Assert.That(string.Join(" | ", calls))
            .IsEqualTo("assigned:test-topic-0,test-topic-1 | revoked:test-topic-0,test-topic-1 | leave");
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
        await Assert.That(GetPrivateField<Task?>(coordinator, "_heartbeatTask") is null).IsTrue();
    }

    [Test]
    [Timeout(10_000)]
    public async Task RequestLeaveGroup_FromInsideAssignedCallback_RevokesAfterCallbackReturns(
        CancellationToken cancellationToken)
    {
        var script = new HeartbeatScript(this);
        SetupFindCoordinator();
        var calls = new List<string>();
        ConsumerCoordinator? coordinator = null;
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                // Unsubscribe from the callback: the leave must wait for the callback, not deadlock.
                coordinator!.RequestLeaveGroup();
                lock (calls)
                    calls.Add("assigned");
                return ValueTask.CompletedTask;
            });
        listener.OnPartitionsRevokedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                lock (calls)
                    calls.Add("revoked");
                return ValueTask.CompletedTask;
            });
        script.Respond = (_, request) => RespondRecordingLeave(calls, request, CreateAssignment(TestTopicId, 0));
        coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(rebalanceListener: listener), _connectionPool, _metadataManager);
        await using var disposeCoordinator = coordinator;

        await coordinator.EnsureActiveGroupAsync(LeaveTestTopics, cancellationToken);
        await GetPrivateField<Task>(coordinator, "_pendingLeave").WaitAsync(cancellationToken);

        await Assert.That(string.Join(" | ", calls)).IsEqualTo("assigned | revoked | leave");
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(GetPrivateField<Task?>(coordinator, "_heartbeatTask") is null).IsTrue();
    }

    [Test]
    [Timeout(10_000)]
    public async Task LeaveGroupAsync_AfterRequestLeaveGroup_SendsOneLeave(CancellationToken cancellationToken)
    {
        var script = new HeartbeatScript(this);
        SetupFindCoordinator();
        var calls = new List<string>();
        script.Respond = (_, request) => RespondRecordingLeave(calls, request, CreateAssignment(TestTopicId, 0));
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(), _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(LeaveTestTopics, cancellationToken);

        // Close right after unsubscribing waits for that leave instead of sending a second one.
        coordinator.RequestLeaveGroup();
        await coordinator.LeaveGroupAsync(cancellationToken);

        await Assert.That(calls.Count(call => call == "leave")).IsEqualTo(1);
        await Assert.That(GetPrivateField<Task>(coordinator, "_pendingLeave").IsCompleted).IsTrue();
    }

    [Test]
    public async Task RequestLeaveGroup_NeverJoined_StartsNoLeave()
    {
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(), _connectionPool, _metadataManager);

        coordinator.RequestLeaveGroup();

        await Assert.That(ReferenceEquals(GetPrivateField<Task>(coordinator, "_pendingLeave"), Task.CompletedTask)).IsTrue();
        await _connection.DidNotReceive().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Any<ConsumerGroupHeartbeatRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    private static ValueTask<ConsumerGroupHeartbeatResponse> RespondRecordingLeave(
        List<string> calls,
        ConsumerGroupHeartbeatRequest request,
        ConsumerGroupHeartbeatAssignment assignment)
    {
        if (request.MemberEpoch != -1)
            return Joined("member-1", memberEpoch: 1, assignment);

        lock (calls)
            calls.Add("leave");
        return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
        {
            ErrorCode = Dekaf.Protocol.ErrorCode.None,
            MemberId = request.MemberId,
            MemberEpoch = -1,
            HeartbeatIntervalMs = 60_000
        });
    }

    private static async ValueTask<ConsumerGroupHeartbeatResponse> RespondAfterAsync(
        Task gate,
        ConsumerGroupHeartbeatAssignment assignment)
    {
        await gate;
        return await Joined("member-1", memberEpoch: 1, assignment);
    }
}
