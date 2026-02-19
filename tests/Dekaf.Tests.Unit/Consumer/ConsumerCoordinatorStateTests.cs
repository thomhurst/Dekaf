using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Unit tests for ConsumerCoordinator state transitions.
/// Verifies heartbeat error handling, rebalance listener callbacks,
/// member/generation ID consistency, coordinator reconnection, and group error responses.
/// </summary>
public sealed class ConsumerCoordinatorStateTests : IAsyncDisposable
{
    private readonly IConnectionPool _connectionPool;
    private readonly IKafkaConnection _connection;
    private readonly MetadataManager _metadataManager;

    public ConsumerCoordinatorStateTests()
    {
        _connectionPool = Substitute.For<IConnectionPool>();
        _connection = Substitute.For<IKafkaConnection>();

        _connectionPool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_connection));

        _metadataManager = new MetadataManager(_connectionPool, ["localhost:9092"]);

        // Seed cluster metadata with a broker so FindCoordinator has a broker to connect to
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

    private static ConsumerOptions CreateOptions(
        string groupId = "test-group",
        IRebalanceListener? rebalanceListener = null,
        int heartbeatIntervalMs = 3000,
        int rebalanceTimeoutMs = 10000) => new()
    {
        BootstrapServers = ["localhost:9092"],
        GroupId = groupId,
        RebalanceListener = rebalanceListener,
        HeartbeatIntervalMs = heartbeatIntervalMs,
        RebalanceTimeoutMs = rebalanceTimeoutMs
    };

    /// <summary>
    /// Sets up the mock connection to return successful FindCoordinator, JoinGroup, and SyncGroup responses.
    /// </summary>
    private void SetupSuccessfulJoinFlow(
        string memberId = "member-1",
        int generationId = 1,
        string leaderId = "member-1")
    {
        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                ErrorCode = ErrorCode.None,
                NodeId = 0,
                Host = "localhost",
                Port = 9092
            }));

        _connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
                Arg.Any<JoinGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new JoinGroupResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = memberId,
                GenerationId = generationId,
                Leader = leaderId,
                Members = []
            }));

        _connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
                Arg.Any<SyncGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new SyncGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Assignment = []
            }));

        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new HeartbeatResponse
            {
                ErrorCode = ErrorCode.None
            }));
    }

    #region Initial State Tests

    [Test]
    public async Task NewCoordinator_State_IsUnjoined()
    {
        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
    }

    [Test]
    public async Task NewCoordinator_MemberId_IsNull()
    {
        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await Assert.That(coordinator.MemberId).IsNull();
    }

    [Test]
    public async Task NewCoordinator_GenerationId_IsNegativeOne()
    {
        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await Assert.That(coordinator.GenerationId).IsEqualTo(-1);
    }

    #endregion

    #region Successful Join Flow - State Transitions

    [Test]
    public async Task EnsureActiveGroupAsync_SuccessfulJoin_TransitionsToStable()
    {
        SetupSuccessfulJoinFlow();
        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
    }

    [Test]
    public async Task EnsureActiveGroupAsync_SuccessfulJoin_SetsMemberId()
    {
        SetupSuccessfulJoinFlow(memberId: "test-member-42");
        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.MemberId).IsEqualTo("test-member-42");
    }

    [Test]
    public async Task EnsureActiveGroupAsync_SuccessfulJoin_SetsGenerationId()
    {
        SetupSuccessfulJoinFlow(generationId: 7);
        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.GenerationId).IsEqualTo(7);
    }

    [Test]
    public async Task EnsureActiveGroupAsync_WhenAlreadyStable_ReturnsImmediately()
    {
        SetupSuccessfulJoinFlow();
        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        // First call joins the group
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);

        // Second call should return immediately without sending new requests
        _connection.ClearReceivedCalls();
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);

        // Verify no FindCoordinator request was sent on the second call
        await _connection.DidNotReceive().SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Any<FindCoordinatorRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EnsureActiveGroupAsync_NoGroupId_ReturnsWithoutJoining()
    {
        var options = new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = null
        };
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
    }

    #endregion

    #region Member ID and Generation ID Consistency

    [Test]
    public async Task EnsureActiveGroupAsync_IsLeader_WhenMemberIdMatchesLeaderId()
    {
        SetupSuccessfulJoinFlow(memberId: "leader-1", leaderId: "leader-1");
        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.IsLeader).IsTrue();
    }

    [Test]
    public async Task EnsureActiveGroupAsync_IsNotLeader_WhenMemberIdDiffersFromLeaderId()
    {
        SetupSuccessfulJoinFlow(memberId: "follower-1", leaderId: "leader-1");
        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.IsLeader).IsFalse();
    }

    #endregion

    #region Group Error Responses

    [Test]
    public async Task EnsureActiveGroupAsync_NotCoordinator_RetriesAndRecovers()
    {
        var callCount = 0;
        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                ErrorCode = ErrorCode.None,
                NodeId = 0,
                Host = "localhost",
                Port = 9092
            }));

        _connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
                Arg.Any<JoinGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new GroupException(ErrorCode.NotCoordinator, "Not coordinator")
                    {
                        GroupId = "test-group"
                    };
                }

                return ValueTask.FromResult(new JoinGroupResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    GenerationId = 1,
                    Leader = "member-1",
                    Members = []
                });
            });

        _connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
                Arg.Any<SyncGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new SyncGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Assignment = []
            }));

        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new HeartbeatResponse
            {
                ErrorCode = ErrorCode.None
            }));

        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
    }

    [Test]
    public async Task EnsureActiveGroupAsync_CoordinatorNotAvailable_RetriesAndRecovers()
    {
        var callCount = 0;
        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                ErrorCode = ErrorCode.None,
                NodeId = 0,
                Host = "localhost",
                Port = 9092
            }));

        _connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
                Arg.Any<JoinGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new GroupException(ErrorCode.CoordinatorNotAvailable, "Coordinator not available")
                    {
                        GroupId = "test-group"
                    };
                }

                return ValueTask.FromResult(new JoinGroupResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    GenerationId = 2,
                    Leader = "member-1",
                    Members = []
                });
            });

        _connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
                Arg.Any<SyncGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new SyncGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Assignment = []
            }));

        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new HeartbeatResponse
            {
                ErrorCode = ErrorCode.None
            }));

        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.GenerationId).IsEqualTo(2);
    }

    [Test]
    public async Task EnsureActiveGroupAsync_CoordinatorLoadInProgress_RetriesAndRecovers()
    {
        var callCount = 0;
        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                ErrorCode = ErrorCode.None,
                NodeId = 0,
                Host = "localhost",
                Port = 9092
            }));

        _connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
                Arg.Any<JoinGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new GroupException(ErrorCode.CoordinatorLoadInProgress, "Coordinator loading")
                    {
                        GroupId = "test-group"
                    };
                }

                return ValueTask.FromResult(new JoinGroupResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    GenerationId = 1,
                    Leader = "member-1",
                    Members = []
                });
            });

        _connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
                Arg.Any<SyncGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new SyncGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Assignment = []
            }));

        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new HeartbeatResponse
            {
                ErrorCode = ErrorCode.None
            }));

        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
    }

    [Test]
    public async Task EnsureActiveGroupAsync_ConnectionDisposed_RetriesAndRecovers()
    {
        var callCount = 0;
        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new ObjectDisposedException("Connection disposed");
                }

                return ValueTask.FromResult(new FindCoordinatorResponse
                {
                    ErrorCode = ErrorCode.None,
                    NodeId = 0,
                    Host = "localhost",
                    Port = 9092
                });
            });

        _connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
                Arg.Any<JoinGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new JoinGroupResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                GenerationId = 1,
                Leader = "member-1",
                Members = []
            }));

        _connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
                Arg.Any<SyncGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new SyncGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Assignment = []
            }));

        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new HeartbeatResponse
            {
                ErrorCode = ErrorCode.None
            }));

        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
    }

    [Test]
    public async Task EnsureActiveGroupAsync_Disposed_ThrowsObjectDisposedException()
    {
        var options = CreateOptions();
        var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.DisposeAsync();

        await Assert.That(async () =>
        {
            await coordinator.EnsureActiveGroupAsync(
                new HashSet<string> { "test-topic" }, CancellationToken.None);
        }).Throws<ObjectDisposedException>();
    }

    #endregion

    #region Heartbeat Error Handling and State Transitions

    [Test]
    public async Task HeartbeatLoop_RebalanceInProgress_TransitionsToUnjoined()
    {
        SetupSuccessfulJoinFlow();

        var options = CreateOptions(heartbeatIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        // Join the group first
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);

        // Now make heartbeat fail with RebalanceInProgress
        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Throws(new GroupException(ErrorCode.RebalanceInProgress, "Rebalance in progress")
            {
                GroupId = "test-group"
            });

        // Wait for heartbeat to fire and detect the error
        await Task.Delay(200);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
    }

    [Test]
    public async Task HeartbeatLoop_UnknownMemberId_TransitionsToUnjoined()
    {
        SetupSuccessfulJoinFlow();

        var options = CreateOptions(heartbeatIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);

        // Make heartbeat fail with UnknownMemberId
        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Throws(new GroupException(ErrorCode.UnknownMemberId, "Unknown member")
            {
                GroupId = "test-group"
            });

        await Task.Delay(200);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
    }

    [Test]
    public async Task HeartbeatLoop_IllegalGeneration_TransitionsToUnjoined()
    {
        SetupSuccessfulJoinFlow();

        var options = CreateOptions(heartbeatIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);

        // Make heartbeat fail with IllegalGeneration
        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Throws(new GroupException(ErrorCode.IllegalGeneration, "Illegal generation")
            {
                GroupId = "test-group"
            });

        await Task.Delay(200);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
    }

    [Test]
    public async Task HeartbeatLoop_NotCoordinator_MarksCoordinatorUnknown()
    {
        SetupSuccessfulJoinFlow();

        var options = CreateOptions(heartbeatIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);

        // Make heartbeat fail with NotCoordinator (retriable coordinator error)
        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Throws(new GroupException(ErrorCode.NotCoordinator, "Not coordinator")
            {
                GroupId = "test-group"
            });

        await Task.Delay(200);

        // NotCoordinator is a retriable coordinator error, so it calls MarkCoordinatorUnknown
        // which sets state to Unjoined
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
    }

    [Test]
    public async Task StopHeartbeatAsync_StopsHeartbeatLoop()
    {
        SetupSuccessfulJoinFlow();

        var options = CreateOptions(heartbeatIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        // Stop heartbeat - should complete without throwing
        await coordinator.StopHeartbeatAsync();

        // State should still be Stable (stopping heartbeat doesn't change state)
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
    }

    #endregion

    #region Rebalance Listener Callback Tests

    [Test]
    public async Task EnsureActiveGroupAsync_WithRebalanceListener_CallsOnPartitionsAssigned()
    {
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsAssignedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        listener.OnPartitionsRevokedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Build an assignment with partitions
        var assignmentData = BuildAssignmentData("test-topic", [0, 1]);

        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                ErrorCode = ErrorCode.None,
                NodeId = 0,
                Host = "localhost",
                Port = 9092
            }));

        _connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
                Arg.Any<JoinGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new JoinGroupResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                GenerationId = 1,
                Leader = "member-1",
                Members = []
            }));

        _connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
                Arg.Any<SyncGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new SyncGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Assignment = assignmentData
            }));

        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new HeartbeatResponse
            {
                ErrorCode = ErrorCode.None
            }));

        var options = CreateOptions(rebalanceListener: listener);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        // Verify the rebalance listener was called with the assigned partitions
        await listener.Received(1).OnPartitionsAssignedAsync(
            Arg.Any<IEnumerable<TopicPartition>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EnsureActiveGroupAsync_RebalanceListenerThrows_PropagatesException()
    {
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsAssignedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Listener error"));
        listener.OnPartitionsRevokedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        var assignmentData = BuildAssignmentData("test-topic", [0]);

        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                ErrorCode = ErrorCode.None,
                NodeId = 0,
                Host = "localhost",
                Port = 9092
            }));

        _connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
                Arg.Any<JoinGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new JoinGroupResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                GenerationId = 1,
                Leader = "member-1",
                Members = []
            }));

        _connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
                Arg.Any<SyncGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new SyncGroupResponse
            {
                ErrorCode = ErrorCode.None,
                Assignment = assignmentData
            }));

        _connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
                Arg.Any<HeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new HeartbeatResponse
            {
                ErrorCode = ErrorCode.None
            }));

        var options = CreateOptions(rebalanceListener: listener);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        // The listener throws, which should propagate to the caller
        await Assert.That(async () =>
        {
            await coordinator.EnsureActiveGroupAsync(
                new HashSet<string> { "test-topic" }, CancellationToken.None);
        }).Throws<InvalidOperationException>();

        // State should be Stable because the exception happens AFTER the lock-protected state transition
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
    }

    [Test]
    public async Task EnsureActiveGroupAsync_NoPartitionChanges_DoesNotCallListener()
    {
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsAssignedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        listener.OnPartitionsRevokedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        SetupSuccessfulJoinFlow(); // Empty assignment

        var options = CreateOptions(rebalanceListener: listener);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        // No partitions assigned or revoked, so listener should not be called
        await listener.DidNotReceive().OnPartitionsAssignedAsync(
            Arg.Any<IEnumerable<TopicPartition>>(),
            Arg.Any<CancellationToken>());
        await listener.DidNotReceive().OnPartitionsRevokedAsync(
            Arg.Any<IEnumerable<TopicPartition>>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region LeaveGroup State Reset Tests

    [Test]
    public async Task LeaveGroupAsync_ResetsStateToUnjoined()
    {
        SetupSuccessfulJoinFlow();

        _connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
                Arg.Any<LeaveGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LeaveGroupResponse
            {
                ErrorCode = ErrorCode.None
            }));

        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);

        await coordinator.LeaveGroupAsync("leaving");

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.MemberId).IsNull();
        await Assert.That(coordinator.GenerationId).IsEqualTo(-1);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
    }

    [Test]
    public async Task LeaveGroupAsync_WhenLeaveRequestFails_StillResetsState()
    {
        SetupSuccessfulJoinFlow();

        _connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
                Arg.Any<LeaveGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Network failure"));

        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        // Even if LeaveGroup fails, state should still be reset
        await coordinator.LeaveGroupAsync("leaving");

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.MemberId).IsNull();
        await Assert.That(coordinator.GenerationId).IsEqualTo(-1);
    }

    [Test]
    public async Task LeaveGroupAsync_WhenNotInGroup_DoesNothing()
    {
        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        // Should not throw when not in a group
        await coordinator.LeaveGroupAsync("leaving");

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
    }

    #endregion

    #region Rapid State Transition Tests

    [Test]
    public async Task EnsureActiveGroupAsync_AfterLeaveGroup_CanRejoin()
    {
        SetupSuccessfulJoinFlow(memberId: "member-1", generationId: 1);

        _connection.SendAsync<LeaveGroupRequest, LeaveGroupResponse>(
                Arg.Any<LeaveGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new LeaveGroupResponse
            {
                ErrorCode = ErrorCode.None
            }));

        var options = CreateOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        // Join -> Stable
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.GenerationId).IsEqualTo(1);

        // Leave -> Unjoined
        await coordinator.LeaveGroupAsync();

        // Update mock to return a new generation for the rejoin
        _connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
                Arg.Any<JoinGroupRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new JoinGroupResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-2",
                GenerationId = 2,
                Leader = "member-2",
                Members = []
            }));

        // Rejoin -> Stable with new generation
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.MemberId).IsEqualTo("member-2");
        await Assert.That(coordinator.GenerationId).IsEqualTo(2);
    }

    #endregion

    #region Disposal Tests

    [Test]
    public async Task DisposeAsync_IsIdempotent()
    {
        var options = CreateOptions();
        var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.DisposeAsync();
        await coordinator.DisposeAsync(); // Second dispose should not throw
    }

    [Test]
    public async Task DisposeAsync_CancelsHeartbeat()
    {
        SetupSuccessfulJoinFlow();

        var options = CreateOptions(heartbeatIntervalMs: 50);
        var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        // Dispose should cancel the heartbeat and complete without hanging
        await coordinator.DisposeAsync();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Builds a consumer protocol assignment byte array containing the given topic-partitions.
    /// Uses the same wire format as ConsumerCoordinator.BuildAssignmentData.
    /// </summary>
    private static byte[] BuildAssignmentData(string topic, int[] partitions)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        // Version
        writer.WriteInt16(0);

        // Topics array
        var topicAssignments = new List<(string Topic, int[] Partitions)> { (topic, partitions) };
        writer.WriteArray(
            topicAssignments,
            (ref KafkaProtocolWriter w, (string Topic, int[] Partitions) tp) =>
            {
                w.WriteString(tp.Topic);
                w.WriteArray(
                    tp.Partitions,
                    (ref KafkaProtocolWriter w2, int partition) => w2.WriteInt32(partition));
            });

        // User data
        writer.WriteBytes([]);

        return buffer.WrittenSpan.ToArray();
    }

    #endregion
}
