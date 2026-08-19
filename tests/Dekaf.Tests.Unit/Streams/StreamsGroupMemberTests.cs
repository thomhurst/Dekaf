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

    [Arguments(null, -1)]
    [Arguments("instance-1", -2)]
    [Test]
    public async Task CloseAsync_DefaultUsesMembershipSpecificTerminalEpoch(
        string? instanceId,
        int expectedEpoch)
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        connection.EnqueueHeartbeat(Success(epoch: expectedEpoch));
        await using var fixture = CreateFixture(connection, instanceId);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await fixture.Member.CloseAsync();

        await Assert.That(connection.HeartbeatRequests[1].MemberEpoch).IsEqualTo(expectedEpoch);
        await Assert.That(fixture.Member.Snapshot.IsClosed).IsTrue();
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
    public async Task BackgroundHeartbeat_AppliesIntervalChangeWithoutRace()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1, heartbeatIntervalMs: 1));
        connection.EnqueueHeartbeat(Success(epoch: 1, heartbeatIntervalMs: 60_000));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());
        var joinedSnapshot = fixture.Member.Snapshot;

        await connection.SecondHeartbeatCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
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
    public async Task CloseAsync_WhenTerminalHeartbeatFailsStillStopsWorker()
    {
        var connection = new ScriptedConnection();
        connection.EnqueueHeartbeat(Success(epoch: 1));
        await using var fixture = CreateFixture(connection);
        await fixture.Member.JoinAsync(CreateInitialUpdate());

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await fixture.Member.CloseAsync());
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
                var response = await _heartbeatResponses.Dequeue().WaitAsync(cancellationToken);
                if (HeartbeatRequests.Count == 2)
                    SecondHeartbeatCompleted.TrySetResult();
                return (TResponse)(IKafkaResponse)response;
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
