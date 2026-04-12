using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Unit tests for KIP-848 consumer group coordinator path.
/// Verifies the ConsumerGroupHeartbeat-based state machine, assignment handling,
/// error recovery, leave, and static membership.
/// </summary>
public sealed class ConsumerCoordinatorKip848Tests : IAsyncDisposable
{
    private static readonly Guid TestTopicId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IConnectionPool _connectionPool;
    private readonly IKafkaConnection _connection;
    private readonly MetadataManager _metadataManager;

    public ConsumerCoordinatorKip848Tests()
    {
        _connectionPool = Substitute.For<IConnectionPool>();
        _connection = Substitute.For<IKafkaConnection>();

        _connectionPool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_connection));

        _connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_connection));

        _metadataManager = new MetadataManager(_connectionPool, ["localhost:9092"]);

        // Seed cluster metadata with a broker and topic (including TopicId for UUID resolution)
        _metadataManager.Metadata.Update(new MetadataResponse
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
    }

    public async ValueTask DisposeAsync()
    {
        await _metadataManager.DisposeAsync();
    }

    private static ConsumerOptions CreateConsumerProtocolOptions(
        string groupId = "test-group",
        IRebalanceListener? rebalanceListener = null,
        string? groupInstanceId = null,
        string? groupRemoteAssignor = null,
        int heartbeatIntervalMs = 3000,
        int rebalanceTimeoutMs = 30000) => new()
    {
        BootstrapServers = ["localhost:9092"],
        GroupId = groupId,
        GroupRemoteAssignor = groupRemoteAssignor,
        GroupInstanceId = groupInstanceId,
        RebalanceListener = rebalanceListener,
        HeartbeatIntervalMs = heartbeatIntervalMs,
        RebalanceTimeoutMs = rebalanceTimeoutMs
    };

    private void SetupFindCoordinator()
    {
        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator
                    {
                        Key = "test-group",
                        NodeId = 0,
                        Host = "localhost",
                        Port = 9092,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));
    }

    private void SetupConsumerGroupHeartbeat(
        string memberId = "member-1",
        int memberEpoch = 1,
        int heartbeatIntervalMs = 5000,
        ConsumerGroupHeartbeatAssignment? assignment = null,
        ErrorCode errorCode = ErrorCode.None)
    {
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
            {
                ErrorCode = errorCode,
                MemberId = memberId,
                MemberEpoch = memberEpoch,
                HeartbeatIntervalMs = heartbeatIntervalMs,
                Assignment = assignment
            }));
    }

    private void SetupSuccessfulConsumerProtocolJoin(
        string memberId = "member-1",
        int memberEpoch = 1,
        ConsumerGroupHeartbeatAssignment? assignment = null)
    {
        SetupFindCoordinator();
        SetupConsumerGroupHeartbeat(memberId, memberEpoch, assignment: assignment);
    }

    private static ConsumerGroupHeartbeatAssignment CreateAssignment(
        Guid topicId, params int[] partitions)
    {
        return new ConsumerGroupHeartbeatAssignment
        {
            AssignedTopicPartitions =
            [
                new ConsumerGroupHeartbeatTopicPartitions
                {
                    TopicId = topicId,
                    Partitions = partitions
                }
            ],
            PendingTopicPartitions = []
        };
    }

    #region Initial Join Tests

    [Test]
    public async Task ConsumerProtocol_SuccessfulJoin_TransitionsToStable()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
    }

    [Test]
    public async Task ConsumerProtocol_SuccessfulJoin_SetsMemberId()
    {
        SetupSuccessfulConsumerProtocolJoin(memberId: "kip848-member-42");
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.MemberId).IsEqualTo("kip848-member-42");
    }

    [Test]
    public async Task ConsumerProtocol_SuccessfulJoin_SetsMemberEpochAsGenerationId()
    {
        SetupSuccessfulConsumerProtocolJoin(memberEpoch: 5);
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        // MemberEpoch is stored in GenerationId for offset commit compatibility
        await Assert.That(coordinator.GenerationId).IsEqualTo(5);
    }

    [Test]
    public async Task ConsumerProtocol_SuccessfulJoin_WithAssignment_SetsPartitions()
    {
        var assignment = CreateAssignment(TestTopicId, 0, 1);
        SetupSuccessfulConsumerProtocolJoin(assignment: assignment);
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.Assignment).Count().IsEqualTo(2);
        await Assert.That(coordinator.Assignment).Contains(new TopicPartition("test-topic", 0));
        await Assert.That(coordinator.Assignment).Contains(new TopicPartition("test-topic", 1));
    }

    [Test]
    public async Task ConsumerProtocol_WhenAlreadyStable_ReturnsImmediately()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);

        _connection.ClearReceivedCalls();
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);

        await _connection.DidNotReceive().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Any<ConsumerGroupHeartbeatRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumerProtocol_InitialJoin_SendsMemberEpochZero()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(r => r.MemberEpoch == 0),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumerProtocol_InitialJoin_SendsSubscribedTopics()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(r =>
                r.SubscribedTopicNames != null && r.SubscribedTopicNames.Contains("test-topic")),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Assignment Tests

    [Test]
    public async Task ConsumerProtocol_UnknownTopicId_SkipsPartitions()
    {
        var unknownTopicId = Guid.Parse("00000000-0000-0000-0000-999999999999");
        var assignment = CreateAssignment(unknownTopicId, 0, 1);
        SetupSuccessfulConsumerProtocolJoin(assignment: assignment);
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        // Unknown topic ID partitions are skipped
        await Assert.That(coordinator.Assignment).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ConsumerProtocol_AssignmentWithRebalanceListener_FiresOnPartitionsAssigned()
    {
        var assignment = CreateAssignment(TestTopicId, 0, 1);
        SetupSuccessfulConsumerProtocolJoin(assignment: assignment);

        var assignedPartitions = new List<TopicPartition>();
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                assignedPartitions.AddRange(ci.Arg<IEnumerable<TopicPartition>>());
                return ValueTask.CompletedTask;
            });

        var options = CreateConsumerProtocolOptions(rebalanceListener: listener);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(assignedPartitions).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ConsumerProtocol_PendingPartitions_NotAddedToAssignment()
    {
        // Assignment with partitions in pending only (not yet released by other member)
        var assignment = new ConsumerGroupHeartbeatAssignment
        {
            AssignedTopicPartitions = [],
            PendingTopicPartitions =
            [
                new ConsumerGroupHeartbeatTopicPartitions
                {
                    TopicId = TestTopicId,
                    Partitions = [0, 1]
                }
            ]
        };
        SetupSuccessfulConsumerProtocolJoin(assignment: assignment);
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        // Pending partitions must NOT be consumed
        await Assert.That(coordinator.Assignment).Count().IsEqualTo(0);
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task ConsumerProtocol_FencedMemberEpoch_RetriesJoin()
    {
        SetupFindCoordinator();

        // First call: FencedMemberEpoch, second call: success
        var callCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.FencedMemberEpoch,
                        ErrorMessage = "Fenced",
                        MemberEpoch = 0,
                        HeartbeatIntervalMs = 5000
                    });
                }

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = 2,
                    HeartbeatIntervalMs = 5000
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(callCount).IsEqualTo(2);
    }

    [Test]
    public async Task ConsumerProtocol_UnreleasedInstanceId_ThrowsAfterRetries()
    {
        SetupFindCoordinator();
        SetupConsumerGroupHeartbeat(errorCode: ErrorCode.UnreleasedInstanceId);

        var options = CreateConsumerProtocolOptions(groupInstanceId: "static-1", rebalanceTimeoutMs: 1000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cts.Token))
            .ThrowsException()
            .OfType<KafkaTimeoutException>();
    }

    [Test]
    public async Task ConsumerProtocol_UnsupportedAssignor_Throws()
    {
        SetupFindCoordinator();
        SetupConsumerGroupHeartbeat(errorCode: ErrorCode.UnsupportedAssignor);

        var options = CreateConsumerProtocolOptions(groupRemoteAssignor: "invalid-assignor");
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        GroupException? caught = null;
        try
        {
            await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        }
        catch (GroupException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.ErrorCode).IsEqualTo(ErrorCode.UnsupportedAssignor);
    }

    #endregion

    #region Leave Group Tests

    [Test]
    public async Task ConsumerProtocol_LeaveGroup_SendsMemberEpochNegativeOne()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        _connection.ClearReceivedCalls();

        // Re-setup for the leave heartbeat
        SetupConsumerGroupHeartbeat();

        await coordinator.LeaveGroupAsync(cancellationToken: CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(r => r.MemberEpoch == -1),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumerProtocol_LeaveGroup_ResetsState()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        SetupConsumerGroupHeartbeat();
        await coordinator.LeaveGroupAsync(cancellationToken: CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.MemberId).IsNull();
        await Assert.That(coordinator.GenerationId).IsEqualTo(-1);
    }

    #endregion

    #region Remote Assignor Tests

    [Test]
    public async Task ConsumerProtocol_WithRemoteAssignor_SendsServerAssignor()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(groupRemoteAssignor: "uniform");
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(r => r.ServerAssignor == "uniform"),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumerProtocol_FencedDuringHeartbeat_RejoinsWithEpochZero()
    {
        SetupFindCoordinator();

        var callCount = 0;
        var fencingProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ConsumerGroupHeartbeatRequest? lastRequest = null;

        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var count = Interlocked.Increment(ref callCount);
                Volatile.Write(ref lastRequest, ci.Arg<ConsumerGroupHeartbeatRequest>());

                if (count == 1)
                {
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-1",
                        MemberEpoch = 5,
                        HeartbeatIntervalMs = 100
                    });
                }

                if (count == 2)
                {
                    fencingProcessed.TrySetResult();
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.FencedMemberEpoch,
                        ErrorMessage = "Fenced",
                        MemberEpoch = 0,
                        HeartbeatIntervalMs = 5000
                    });
                }

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = 6,
                    HeartbeatIntervalMs = 60000
                });
            });

        var options = CreateConsumerProtocolOptions(heartbeatIntervalMs: 100);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        // Initial join succeeds (epoch=5)
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await Assert.That(coordinator.GenerationId).IsEqualTo(5);

        // Wait for heartbeat loop to process the fencing response (deterministic signal)
        await fencingProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Brief yield to let the heartbeat loop complete its state transition after the mock returns
        await Task.Delay(50);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        // With fix: _generationId reset to 0 by fencing handler (not stale 5)
        await Assert.That(coordinator.GenerationId).IsEqualTo(0);
        // _memberId preserved (member is known, just epoch-stale)
        await Assert.That(coordinator.MemberId).IsEqualTo("member-1");

        // Rejoin — should send MemberEpoch=0, not the stale 5
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);

        var rejoinReq = Volatile.Read(ref lastRequest);
        await Assert.That(rejoinReq).IsNotNull();
        await Assert.That(rejoinReq!.MemberEpoch).IsEqualTo(0);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.GenerationId).IsEqualTo(6);
    }

    [Test]
    public async Task ConsumerProtocol_StaticMember_FencedDuringHeartbeat_RejoinsWithEpochNegativeTwo()
    {
        SetupFindCoordinator();

        var callCount = 0;
        var fencingProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ConsumerGroupHeartbeatRequest? lastRequest = null;

        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var count = Interlocked.Increment(ref callCount);
                Volatile.Write(ref lastRequest, ci.Arg<ConsumerGroupHeartbeatRequest>());

                if (count == 1)
                {
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-1",
                        MemberEpoch = 3,
                        HeartbeatIntervalMs = 100
                    });
                }

                if (count == 2)
                {
                    fencingProcessed.TrySetResult();
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.FencedMemberEpoch,
                        ErrorMessage = "Fenced",
                        MemberEpoch = 0,
                        HeartbeatIntervalMs = 5000
                    });
                }

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = 4,
                    HeartbeatIntervalMs = 60000
                });
            });

        var options = CreateConsumerProtocolOptions(groupInstanceId: "static-1", heartbeatIntervalMs: 100);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        // Initial join succeeds (epoch=3)
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await Assert.That(coordinator.GenerationId).IsEqualTo(3);

        // Wait for heartbeat loop to process the fencing response (deterministic signal)
        await fencingProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Brief yield to let the heartbeat loop complete its state transition after the mock returns
        await Task.Delay(50);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        // With fix: _generationId reset to -2 for static member (triggers MemberEpoch=-2 on rejoin)
        await Assert.That(coordinator.GenerationId).IsEqualTo(-2);

        // Rejoin — static member should send MemberEpoch=-2
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);

        var rejoinReq = Volatile.Read(ref lastRequest);
        await Assert.That(rejoinReq).IsNotNull();
        await Assert.That(rejoinReq!.MemberEpoch).IsEqualTo(-2);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.GenerationId).IsEqualTo(4);
    }

    [Test]
    public async Task ConsumerProtocol_UnknownMemberId_InJoinPath_ResetsAndRetries()
    {
        SetupFindCoordinator();

        var callCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return callCount switch
                {
                    // First: UnknownMemberId (broker forgot this member)
                    1 => ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.UnknownMemberId,
                        ErrorMessage = "Unknown member",
                        MemberEpoch = 0,
                        HeartbeatIntervalMs = 5000
                    }),
                    // Second: successful fresh join
                    _ => ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-2",
                        MemberEpoch = 1,
                        HeartbeatIntervalMs = 5000
                    })
                };
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.MemberId).IsEqualTo("member-2");
        await Assert.That(callCount).IsEqualTo(2);
    }

    #endregion

    #region Static Membership Tests

    [Test]
    public async Task ConsumerProtocol_StaticMembership_SendsInstanceId()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(groupInstanceId: "static-instance-1");
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(r => r.InstanceId == "static-instance-1"),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
