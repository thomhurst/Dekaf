using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Streams;
using NSubstitute;

namespace Dekaf.Tests.Unit.Streams;

public sealed class StreamsGroupMemberTests
{
    [Test]
    public async Task JoinAsync_ReconcilesAllTaskRolesAndEpochs()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.None,
            MemberId = "member-1",
            MemberEpoch = 7,
            HeartbeatIntervalMs = 60_000,
            AcceptableRecoveryLag = 10_000,
            TaskOffsetIntervalMs = 5_000,
            EndpointInformationEpoch = 4,
            ActiveTasks = [TaskIds("active", 1, 2)],
            StandbyTasks = [TaskIds("standby", 3)],
            WarmupTasks = [TaskIds("warmup", 4)],
            Status = [new StreamsGroupHeartbeatStatus { StatusCode = 1, StatusDetail = "ready" }]
        });
        await using var fixture = CreateFixture(connection);

        var result = await fixture.Member.JoinAsync(CreateInitialUpdate(topologyEpoch: 3));

        var request = connection.HeartbeatRequests[0];
        await Assert.That(request.MemberEpoch).IsEqualTo(0);
        await Assert.That(request.Topology!.Epoch).IsEqualTo(3);
        await Assert.That(request.ActiveTasks).IsEmpty();
        await Assert.That(request.StandbyTasks).IsEmpty();
        await Assert.That(request.WarmupTasks).IsEmpty();
        await Assert.That(result.MemberEpoch).IsEqualTo(7);
        await Assert.That(fixture.Member.Snapshot.MemberEpoch).IsEqualTo(7);
        await Assert.That(fixture.Member.Snapshot.Assignment.ActiveTasks[0].SubtopologyId)
            .IsEqualTo("active");
        await Assert.That(fixture.Member.Snapshot.Assignment.StandbyTasks[0].SubtopologyId)
            .IsEqualTo("standby");
        await Assert.That(fixture.Member.Snapshot.Assignment.WarmupTasks[0].SubtopologyId)
            .IsEqualTo("warmup");
        await Assert.That(fixture.Member.Snapshot.EndpointInformationEpoch).IsEqualTo(4);
    }

    [Test]
    public async Task UpdateAsync_SendsAssignmentAcknowledgementAndOnlyChangedMetadata()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1, active: [TaskIds("topology", 0)]));
        connection.EnqueueHeartbeat(Success(epoch: 2));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            EndpointInformationEpoch = 2,
            ActiveTasks =
            [
                new StreamsGroupTaskSet { SubtopologyId = "topology", Partitions = [0] }
            ],
            ClientTags = [new StreamsGroupKeyValue { Key = "zone", Value = "a" }]
        });

        var request = connection.HeartbeatRequests[1];
        await Assert.That(request.MemberEpoch).IsEqualTo(1);
        await Assert.That(request.Topology).IsNull();
        await Assert.That(request.ProcessId).IsNull();
        await Assert.That(request.ClientTags).Count().IsEqualTo(1);
        await Assert.That(request.ActiveTasks![0].Partitions).IsEquivalentTo([0]);
        await Assert.That(request.StandbyTasks).IsNull();
        await Assert.That(request.WarmupTasks).IsNull();
    }

    [Test]
    public async Task UpdateAsync_WhenMemberFenced_RejoinsWithCompleteState()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.FencedMemberEpoch,
            ErrorMessage = "lost response",
            MemberId = "member-1"
        });
        connection.EnqueueHeartbeat(Success(epoch: 2));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate(topologyEpoch: 5));

        var result = await fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            EndpointInformationEpoch = 6,
            ProcessId = "process-2"
        });

        var rejoin = connection.HeartbeatRequests[2];
        await Assert.That(rejoin.MemberEpoch).IsEqualTo(0);
        await Assert.That(rejoin.Topology!.Epoch).IsEqualTo(5);
        await Assert.That(rejoin.ProcessId).IsEqualTo("process-2");
        await Assert.That(result.MemberEpoch).IsEqualTo(2);
    }

    [Test]
    public async Task UpdateAsync_WhenStaticMemberFenced_RejoinsWithStaticEpoch()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.FencedMemberEpoch,
            MemberId = "member-1"
        });
        connection.EnqueueHeartbeat(Success(epoch: 2));
        await using var fixture = CreateFixture(connection, instanceId: "instance-1");
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate { ProcessId = "process-2" });

        await Assert.That(connection.HeartbeatRequests[2].MemberEpoch).IsEqualTo(-2);
    }

    [Test]
    public async Task ReportTaskOffsetsAsync_SendsCurrentAndEndOffsets()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(Success(epoch: 1));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await fixture.Member.ReportTaskOffsetsAsync(new StreamsGroupTaskOffsetReport
        {
            TaskOffsets =
            [
                new StreamsGroupTaskOffset { SubtopologyId = "0", Partition = 1, Offset = 42 }
            ],
            TaskEndOffsets =
            [
                new StreamsGroupTaskOffset { SubtopologyId = "0", Partition = 1, Offset = 50 }
            ]
        });

        var request = connection.HeartbeatRequests[1];
        await Assert.That(request.TaskOffsets![0].Offset).IsEqualTo(42);
        await Assert.That(request.TaskEndOffsets![0].Offset).IsEqualTo(50);
    }

    [Test]
    public async Task UpdateAsync_NotCoordinatorRediscoversAndRetriesCompleteMetadata()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.NotCoordinator,
            MemberId = "member-1"
        });
        connection.EnqueueHeartbeat(Success(epoch: 2));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        var result = await fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            EndpointInformationEpoch = 1,
            ProcessId = "process-2"
        });

        var retry = connection.HeartbeatRequests[2];
        await Assert.That(retry.ProcessId).IsEqualTo("process-2");
        await Assert.That(retry.ClientTags).IsNotNull();
        await Assert.That(result.MemberEpoch).IsEqualTo(2);
    }

    [Test]
    public async Task JoinAsync_WhenCoordinatorDiscoveryTransportFails_RetriesDiscovery()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueFindCoordinator(Task.FromException<FindCoordinatorResponse>(
            new IOException("transient discovery failure")));
        connection.EnqueueFindCoordinator(ScriptedConnection.SuccessfulCoordinator());
        connection.EnqueueHeartbeat(Success(epoch: 1));
        await using var fixture = CreateFixture(connection);

        var result = await fixture.Member.JoinAsync(CreateInitialUpdate());

        await Assert.That(connection.FindCoordinatorRequestCount).IsEqualTo(2);
        await Assert.That(result.MemberEpoch).IsEqualTo(1);
    }

    [Arguments(ErrorCode.StreamsInvalidTopology)]
    [Arguments(ErrorCode.StreamsInvalidTopologyEpoch)]
    [Arguments(ErrorCode.StreamsTopologyFenced)]
    [Test]
    public async Task JoinAsync_TopologyErrorPreservesKafkaErrorCode(ErrorCode errorCode)
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = errorCode,
            ErrorMessage = "topology rejected",
            MemberId = "member-1"
        });
        await using var fixture = CreateFixture(connection);

        var exception = await Assert.ThrowsAsync<GroupException>(
            async () => await fixture.Member.JoinAsync(CreateInitialUpdate()));

        await Assert.That(exception!.ErrorCode).IsEqualTo(errorCode);
        await Assert.That(fixture.Member.Snapshot.IsJoined).IsFalse();
    }

    [Test]
    public async Task JoinAsync_UnsupportedVersionExplainsBrokerFeatureGate()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.UnsupportedVersion,
            MemberId = "member-1"
        });
        await using var fixture = CreateFixture(connection);

        var exception = await Assert.ThrowsAsync<BrokerVersionException>(
            async () => await fixture.Member.JoinAsync(CreateInitialUpdate()));

        await Assert.That(exception!.Message).Contains("streams.version >= 1");
        await Assert.That(exception.Message).Contains("group.coordinator.rebalance.protocols");
    }

    [Test]
    public async Task JoinAsync_StaticMemberRetriesUnreleasedInstanceId()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.UnreleasedInstanceId,
            MemberId = "member-1"
        });
        connection.EnqueueHeartbeat(Success(epoch: 1));
        await using var fixture = CreateFixture(connection, instanceId: "instance-1");

        var result = await fixture.Member.JoinAsync(CreateInitialUpdate());

        await Assert.That(result.MemberEpoch).IsEqualTo(1);
        await Assert.That(connection.HeartbeatRequests).Count().IsEqualTo(2);
    }

    [Arguments(null, -1, false)]
    [Arguments("instance-1", -2, true)]
    [Test]
    public async Task CloseAsync_DefaultUsesMembershipSpecificTerminalEpoch(
        string? instanceId,
        int expectedEpoch,
        bool expectedJoined)
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(Success(epoch: expectedEpoch));
        await using var fixture = CreateFixture(connection, instanceId);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await fixture.Member.CloseAsync();

        await Assert.That(connection.HeartbeatRequests[1].MemberEpoch).IsEqualTo(expectedEpoch);
        await Assert.That(fixture.Member.Snapshot.IsClosed).IsTrue();
        await Assert.That(fixture.Member.Snapshot.IsJoined).IsEqualTo(expectedJoined);
    }

    [Arguments(false)]
    [Arguments(true)]
    [Test]
    public async Task CloseAsync_RetryPreservesTerminalEpoch(bool transportFailure)
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        if (transportFailure)
        {
            connection.EnqueueHeartbeat(Task.FromException<StreamsGroupHeartbeatResponse>(
                new IOException("connection lost")));
        }
        else
        {
            connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
            {
                ErrorCode = ErrorCode.NotCoordinator,
                MemberId = "member-1"
            });
        }
        connection.EnqueueHeartbeat(Success(epoch: -1));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await fixture.Member.CloseAsync();

        await Assert.That(connection.HeartbeatRequests[1].MemberEpoch).IsEqualTo(-1);
        await Assert.That(connection.HeartbeatRequests[2].MemberEpoch).IsEqualTo(-1);
    }

    [Test]
    public async Task CloseAsync_RemainInGroupSendsNoTerminalHeartbeat()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        await using var fixture = CreateFixture(connection, instanceId: "instance-1");
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await fixture.Member.CloseAsync(new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = StreamsGroupMembershipOperation.RemainInGroup
        });

        await Assert.That(connection.HeartbeatRequests).Count().IsEqualTo(1);
        await Assert.That(fixture.Member.Snapshot.IsJoined).IsTrue();
    }

    [Test]
    public async Task CloseAsync_InvalidMembershipOperationDoesNotLeaveOrClose()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(Success(epoch: -1));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await fixture.Member.CloseAsync(new StreamsGroupCloseOptions
            {
                GroupMembershipOperation = (StreamsGroupMembershipOperation)int.MaxValue
            }));

        await Assert.That(connection.HeartbeatRequests).Count().IsEqualTo(1);
        await Assert.That(fixture.Member.Snapshot.IsClosed).IsFalse();
        await Assert.That(fixture.Member.Snapshot.IsJoined).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_WaitsForInFlightHeartbeatWithoutDeadlock()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1, heartbeatIntervalMs: 1));
        var pendingHeartbeat = new TaskCompletionSource<StreamsGroupHeartbeatResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.EnqueueHeartbeat(pendingHeartbeat.Task);
        connection.EnqueueHeartbeat(Success(epoch: -2));
        var fixture = CreateFixture(connection, instanceId: "instance-1");
        await fixture.Member.JoinAsync(CreateInitialUpdate());
        await connection.SecondHeartbeatStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var dispose = fixture.Member.DisposeAsync().AsTask();
        await Assert.That(dispose.IsCompleted).IsFalse();
        pendingHeartbeat.SetResult(Success(epoch: 1));
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(fixture.Member.Snapshot.IsClosed).IsTrue();
        await fixture.DisposeAsync();
    }

    [Test]
    public async Task DisposeAsync_WhenTerminalHeartbeatFailsStillCompletes()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(Task.FromException<StreamsGroupHeartbeatResponse>(
            new NotSupportedException("terminal heartbeat failed")));
        var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await fixture.Member.DisposeAsync();

        await Assert.That(fixture.Member.Snapshot.IsClosed).IsTrue();
        await fixture.DisposeAsync();
    }

    [Test]
    public async Task BackgroundHeartbeat_AppliesIntervalChangeWithoutRace()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1, heartbeatIntervalMs: 1));
        var pendingHeartbeat = new TaskCompletionSource<StreamsGroupHeartbeatResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.EnqueueHeartbeat(pendingHeartbeat.Task);
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());
        var joinedSnapshot = fixture.Member.Snapshot;

        await connection.SecondHeartbeatStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        pendingHeartbeat.SetResult(Success(epoch: 1, heartbeatIntervalMs: 60_000));
        await Assert.That(() => fixture.Member.Snapshot.HeartbeatInterval)
            .Eventually(interval => interval.IsEqualTo(TimeSpan.FromSeconds(60)), TimeSpan.FromSeconds(5));

        var heartbeatSnapshot = fixture.Member.Snapshot;
        await Assert.That(heartbeatSnapshot).IsNotSameReferenceAs(joinedSnapshot);
    }

    [Test]
    public async Task BackgroundHeartbeat_DoesNotAcknowledgeUnreconciledTargetAssignment()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(
            epoch: 1,
            heartbeatIntervalMs: 1,
            active: [TaskIds("topology", 0)]));
        connection.EnqueueHeartbeat(Success(epoch: 1, heartbeatIntervalMs: 60_000));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await connection.SecondHeartbeatCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(connection.HeartbeatRequests[1].ActiveTasks).IsNull();
    }

    [Test]
    public async Task BackgroundHeartbeat_TransientFailureDoesNotBlockForegroundOperation()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1, heartbeatIntervalMs: 1));
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.UnknownServerError,
            MemberId = "member-1"
        });
        var pendingHeartbeat = new TaskCompletionSource<StreamsGroupHeartbeatResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.EnqueueHeartbeat(pendingHeartbeat.Task);
        connection.EnqueueHeartbeat(Success(epoch: 2));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());
        await connection.ThirdHeartbeatStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var update = fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            EndpointInformationEpoch = 1
        }).AsTask();
        pendingHeartbeat.SetResult(Success(epoch: 1, heartbeatIntervalMs: 60_000));
        var result = await update;

        await Assert.That(result.MemberEpoch).IsEqualTo(2);
    }

    [Test]
    public async Task BackgroundHeartbeat_PermanentProtocolFailureBlocksForegroundOperation()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1, heartbeatIntervalMs: 1));
        var pendingHeartbeat = new TaskCompletionSource<StreamsGroupHeartbeatResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.EnqueueHeartbeat(pendingHeartbeat.Task);
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());
        await connection.SecondHeartbeatStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var update = fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            ProcessId = "process-2"
        }).AsTask();
        pendingHeartbeat.SetResult(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.GroupAuthorizationFailed,
            MemberId = "member-1"
        });

        var failure = await Assert.ThrowsAsync<GroupException>(async () => await update);
        await Assert.That(failure!.ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(connection.HeartbeatRequests).Count().IsEqualTo(2);
    }

    [Test]
    public async Task UpdateAsync_FailedTopologyChangePreservesJoinedEpoch()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.StreamsInvalidTopology,
            ErrorMessage = "topology rejected",
            MemberId = "member-1"
        });
        connection.EnqueueHeartbeat(Success(epoch: 2));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await Assert.ThrowsAsync<GroupException>(async () =>
            await fixture.Member.UpdateAsync(CreateInitialUpdate(topologyEpoch: 2)));

        await Assert.That(fixture.Member.Snapshot.MemberEpoch).IsEqualTo(1);
        var result = await fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            ProcessId = "process-2"
        });
        await Assert.That(connection.HeartbeatRequests[2].MemberEpoch).IsEqualTo(1);
        await Assert.That(result.MemberEpoch).IsEqualTo(2);
    }

    [Test]
    public async Task UpdateAsync_TopologyChangeRetryRemainsJoinShaped()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.NotCoordinator,
            MemberId = "member-1"
        });
        connection.EnqueueHeartbeat(Success(epoch: 2));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await fixture.Member.UpdateAsync(CreateInitialUpdate(topologyEpoch: 2));

        var retry = connection.HeartbeatRequests[2];
        await Assert.That(retry.MemberEpoch).IsEqualTo(0);
        await Assert.That(retry.Topology!.Epoch).IsEqualTo(2);
    }

    [Test]
    public async Task UpdateAsync_WhenFencedRejoinFails_PreservesJoinedEpoch()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.FencedMemberEpoch,
            MemberId = "member-1"
        });
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.StreamsInvalidTopology,
            ErrorMessage = "topology rejected",
            MemberId = "member-1"
        });
        connection.EnqueueHeartbeat(Success(epoch: 2));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await Assert.ThrowsAsync<GroupException>(async () =>
            await fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate { ProcessId = "process-2" }));
        var result = await fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate { ProcessId = "process-3" });

        await Assert.That(connection.HeartbeatRequests[3].MemberEpoch).IsEqualTo(1);
        await Assert.That(result.MemberEpoch).IsEqualTo(2);
    }

    [Test]
    public async Task UpdateAsync_RejectedTopologyIsNotUsedForLaterFencedRecovery()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.StreamsInvalidTopology,
            ErrorMessage = "topology rejected",
            MemberId = "member-1"
        });
        connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.FencedMemberEpoch,
            MemberId = "member-1"
        });
        connection.EnqueueHeartbeat(Success(epoch: 2));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate(topologyEpoch: 1));

        await Assert.ThrowsAsync<GroupException>(async () =>
            await fixture.Member.UpdateAsync(CreateInitialUpdate(topologyEpoch: 2)));
        await fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate { ProcessId = "process-2" });

        await Assert.That(connection.HeartbeatRequests[3].Topology!.Epoch).IsEqualTo(1);
    }

    [Test]
    public async Task UpdateAsync_TopologyChangePreservesShutdownApplication()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(Success(epoch: 2));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            Topology = CreateInitialUpdate(topologyEpoch: 2).Topology,
            ShutdownApplication = true
        });

        await Assert.That(connection.HeartbeatRequests[1].ShutdownApplication).IsTrue();
    }

    [Arguments(false)]
    [Arguments(true)]
    [Test]
    public async Task UpdateAsync_RetryPreservesShutdownApplication(bool transportFailure)
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        if (transportFailure)
        {
            connection.EnqueueHeartbeat(Task.FromException<StreamsGroupHeartbeatResponse>(
                new IOException("connection lost")));
        }
        else
        {
            connection.EnqueueHeartbeat(new StreamsGroupHeartbeatResponse
            {
                ErrorCode = ErrorCode.NotCoordinator,
                MemberId = "member-1"
            });
        }
        connection.EnqueueHeartbeat(Success(epoch: 2));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await fixture.Member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            ProcessId = "process-2",
            ShutdownApplication = true
        });

        await Assert.That(connection.HeartbeatRequests[1].ShutdownApplication).IsTrue();
        await Assert.That(connection.HeartbeatRequests[2].ShutdownApplication).IsTrue();
    }

    [Test]
    public async Task SteadyRequestCache_RebuildsWhenIdentityChanges()
    {
        var cache = new StreamsGroupHeartbeatRequestCache();
        var initial = cache.Get("group", "member-1", 1, 2, "instance-1");

        var unchanged = cache.Get("group", "member-1", 1, 2, "instance-1");
        var changed = cache.Get("group", "member-2", 2, 3, "instance-2");

        await Assert.That(unchanged).IsSameReferenceAs(initial);
        await Assert.That(changed).IsNotSameReferenceAs(initial);
        await Assert.That(changed.MemberId).IsEqualTo("member-2");
        await Assert.That(changed.MemberEpoch).IsEqualTo(2);
        await Assert.That(changed.EndpointInformationEpoch).IsEqualTo(3);
        await Assert.That(changed.InstanceId).IsEqualTo("instance-2");
    }

    [Test]
    public async Task CloseAsync_WhenTerminalHeartbeatFailsStillStopsWorker()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(Task.FromException<StreamsGroupHeartbeatResponse>(
            new NotSupportedException("terminal heartbeat failed")));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await Assert.ThrowsAsync<NotSupportedException>(
            async () => await fixture.Member.CloseAsync());
        await fixture.Member.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(fixture.Member.Snapshot.IsClosed).IsTrue();
    }

    [Test]
    public async Task CloseAsync_WhenCallerCancelsObservesLaterTerminalFailure()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        var terminalHeartbeat = new TaskCompletionSource<StreamsGroupHeartbeatResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.EnqueueHeartbeat(terminalHeartbeat.Task);
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());
        using var cancellation = new CancellationTokenSource();

        var close = fixture.Member.CloseAsync(cancellation.Token).AsTask();
        await connection.SecondHeartbeatStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => close);
        terminalHeartbeat.SetException(new NotSupportedException("terminal heartbeat failed"));

        await fixture.Member.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(fixture.Member.Snapshot.IsClosed).IsTrue();
    }

    private static Fixture CreateFixture(ScriptedConnection connection, string? instanceId = null)
    {
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion,
            FindCoordinatorRequest.HighestSupportedVersion);
        metadataManager.SetApiVersion(
            ApiKey.StreamsGroupHeartbeat,
            StreamsGroupHeartbeatRequest.LowestSupportedVersion,
            StreamsGroupHeartbeatRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }],
            Topics = []
        });
        var member = new StreamsGroupMember(
            new StreamsGroupMemberOptions
            {
                GroupId = "streams-group",
                InstanceId = instanceId,
                RackId = "rack-a"
            },
            pool,
            metadataManager,
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0);
        member.MarkInitializedForTesting();
        return new Fixture(member, metadataManager);
    }

    private static StreamsGroupMemberUpdate CreateInitialUpdate(int topologyEpoch = 0) => new()
    {
        EndpointInformationEpoch = 0,
        Topology = new StreamsGroupTopology
        {
            Epoch = topologyEpoch,
            Subtopologies = []
        },
        ActiveTasks = [],
        StandbyTasks = [],
        WarmupTasks = [],
        ProcessId = "process-1",
        ClientTags = [],
        TaskOffsets = [],
        TaskEndOffsets = []
    };

    private static StreamsGroupHeartbeatTaskIds TaskIds(string id, params int[] partitions) => new()
    {
        SubtopologyId = id,
        Partitions = partitions
    };

    private static StreamsGroupHeartbeatResponse Success(
        int epoch,
        int heartbeatIntervalMs = 60_000,
        IReadOnlyList<StreamsGroupHeartbeatTaskIds>? active = null) => new()
        {
            ErrorCode = ErrorCode.None,
            MemberId = "member-1",
            MemberEpoch = epoch,
            HeartbeatIntervalMs = heartbeatIntervalMs,
            ActiveTasks = active
        };

    private sealed class Fixture(
        StreamsGroupMember member,
        MetadataManager metadataManager) : IAsyncDisposable
    {
        public StreamsGroupMember Member { get; } = member;

        public async ValueTask DisposeAsync()
        {
            await Member.CloseAsync(new StreamsGroupCloseOptions
            {
                GroupMembershipOperation = StreamsGroupMembershipOperation.RemainInGroup
            });
            await Member.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    private sealed class ScriptedConnection : IKafkaConnection
    {
        private readonly Queue<Task<FindCoordinatorResponse>> _findCoordinatorResponses = new();
        private readonly Queue<Task<StreamsGroupHeartbeatResponse>> _heartbeatResponses = new();

        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public int FindCoordinatorRequestCount { get; private set; }
        public List<StreamsGroupHeartbeatRequest> HeartbeatRequests { get; } = [];
        public TaskCompletionSource SecondHeartbeatStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondHeartbeatCompleted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ThirdHeartbeatStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public void EnqueueFindCoordinator(FindCoordinatorResponse response) =>
            _findCoordinatorResponses.Enqueue(Task.FromResult(response));

        public void EnqueueFindCoordinator(Task<FindCoordinatorResponse> response) =>
            _findCoordinatorResponses.Enqueue(response);

        public void EnqueueHeartbeat(StreamsGroupHeartbeatResponse response) =>
            _heartbeatResponses.Enqueue(Task.FromResult(response));

        public void EnqueueHeartbeat(Task<StreamsGroupHeartbeatResponse> response) =>
            _heartbeatResponses.Enqueue(response);

        public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (request is FindCoordinatorRequest)
            {
                FindCoordinatorRequestCount++;
                var response = _findCoordinatorResponses.Count == 0
                    ? SuccessfulCoordinator()
                    : await _findCoordinatorResponses.Dequeue().WaitAsync(cancellationToken);
                return (TResponse)(IKafkaResponse)response;
            }

            if (request is StreamsGroupHeartbeatRequest heartbeat)
            {
                HeartbeatRequests.Add(heartbeat);
                if (HeartbeatRequests.Count == 2)
                    SecondHeartbeatStarted.TrySetResult();
                if (HeartbeatRequests.Count == 3)
                    ThirdHeartbeatStarted.TrySetResult();
                try
                {
                    var response = await _heartbeatResponses.Dequeue().WaitAsync(cancellationToken);
                    return (TResponse)(IKafkaResponse)response;
                }
                finally
                {
                    if (HeartbeatRequests.Count == 2)
                        SecondHeartbeatCompleted.TrySetResult();
                }
            }

            throw new NotSupportedException(typeof(TRequest).Name);
        }

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
            throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
            throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request,
            short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
            throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request,
            short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
            throw new NotSupportedException();

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        internal static FindCoordinatorResponse SuccessfulCoordinator() => new()
        {
            Coordinators =
            [
                new Coordinator
                {
                    Key = "streams-group",
                    NodeId = 1,
                    Host = "localhost",
                    Port = 9092,
                    ErrorCode = ErrorCode.None
                }
            ]
        };
    }
}
