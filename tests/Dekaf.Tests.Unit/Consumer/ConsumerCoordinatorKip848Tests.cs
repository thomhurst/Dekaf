using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Tests.Unit;
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

        // Seed broker API versions so the ConsumerGroupHeartbeat version guard passes
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0);
        _metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);

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
        string? clientRack = null,
        int heartbeatIntervalMs = 3000,
        int rebalanceTimeoutMs = 30000) => new()
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = groupId,
            GroupRemoteAssignor = groupRemoteAssignor,
            GroupInstanceId = groupInstanceId,
            ClientRack = clientRack,
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

    #region Broker Version Guard

    [Test]
    public async Task ConsumerProtocol_BrokerWithoutConsumerGroupHeartbeat_ThrowsBrokerVersionException()
    {
        // Create a MetadataManager without ConsumerGroupHeartbeat API seeded
        var noHeartbeatManager = new MetadataManager(_connectionPool, ["localhost:9092"]);
        noHeartbeatManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        // Deliberately NOT setting ConsumerGroupHeartbeat

        noHeartbeatManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
            Topics = []
        });

        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(), _connectionPool, noHeartbeatManager);

        await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None))
            .Throws<BrokerVersionException>()
            .WithMessageContaining("Kafka 4.0");

        await noHeartbeatManager.DisposeAsync();
    }

    #endregion

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
    public async Task ConsumerProtocol_AssignmentVersion_ChangesOnlyWhenAssignmentChanges()
    {
        SetupFindCoordinator();

        var callCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var count = Interlocked.Increment(ref callCount);
                var assignment = count switch
                {
                    1 => CreateAssignment(TestTopicId, 0),
                    2 => CreateAssignment(TestTopicId, 0),
                    _ => CreateAssignment(TestTopicId, 0, 1)
                };

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = count,
                    HeartbeatIntervalMs = 60000,
                    Assignment = assignment
                });
            });

        var options = CreateConsumerProtocolOptions(heartbeatIntervalMs: 60000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        var versionAfterInitialAssignment = coordinator.AssignmentVersion;
        await Assert.That(versionAfterInitialAssignment).IsEqualTo(1);

        coordinator.RequestRejoin();
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        await Assert.That(coordinator.AssignmentVersion).IsEqualTo(versionAfterInitialAssignment);

        coordinator.RequestRejoin();
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        await Assert.That(coordinator.AssignmentVersion).IsEqualTo(versionAfterInitialAssignment + 1);
    }

    [Test]
    public async Task ConsumerProtocol_AssignmentVersion_IncrementsWhenResetClearsAssignment()
    {
        SetupFindCoordinator();

        var callCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var count = Interlocked.Increment(ref callCount);
                if (count == 2)
                {
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.UnknownMemberId,
                        ErrorMessage = "Unknown member",
                        HeartbeatIntervalMs = 60000
                    });
                }

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = $"member-{count}",
                    MemberEpoch = count,
                    HeartbeatIntervalMs = 60000,
                    Assignment = count == 1 ? CreateAssignment(TestTopicId, 0) : null
                });
            });

        var options = CreateConsumerProtocolOptions(heartbeatIntervalMs: 60000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        await Assert.That(coordinator.AssignmentVersion).IsEqualTo(1);

        coordinator.RequestRejoin();
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        await Assert.That(coordinator.AssignmentVersion).IsEqualTo(2);
        await Assert.That(coordinator.Assignment).IsEmpty();
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
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

    #region Offset Request Grouping Tests

    [Test]
    public async Task CommitOffsetsAsync_RemovesEmptyPooledTopicGroups()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, OffsetCommitRequest.LowestSupportedVersion, OffsetCommitRequest.HighestSupportedVersion);

        var topicSnapshots = new List<(string Name, int PartitionCount)[]>();
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var request = ci.Arg<OffsetCommitRequest>();
                topicSnapshots.Add(request.Topics
                    .Select(static topic => (topic.Name, topic.Partitions.Count))
                    .ToArray());

                return ValueTask.FromResult(new OffsetCommitResponse
                {
                    Topics = []
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.CommitOffsetsAsync([new TopicPartitionOffset("alpha", 0, 42)], CancellationToken.None);
        await coordinator.CommitOffsetsAsync([new TopicPartitionOffset("beta", 1, 43)], CancellationToken.None);

        await Assert.That(topicSnapshots.Count).IsEqualTo(2);
        await Assert.That(topicSnapshots[1].Length).IsEqualTo(1);
        await Assert.That(topicSnapshots[1][0].Name).IsEqualTo("beta");
        await Assert.That(topicSnapshots[1][0].PartitionCount).IsEqualTo(1);
    }

    [Test]
    public async Task FetchOffsetsAsync_RemovesEmptyPooledTopicGroups()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, OffsetFetchRequest.LowestSupportedVersion, OffsetFetchRequest.HighestSupportedVersion);
        SetupSuccessfulConsumerProtocolJoin();

        var topicSnapshots = new List<(string Name, int PartitionCount)[]>();
        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var request = ci.Arg<OffsetFetchRequest>();
                topicSnapshots.Add(request.Topics!
                    .Select(static topic => (topic.Name, topic.PartitionIndexes.Count))
                    .ToArray());

                return ValueTask.FromResult(new OffsetFetchResponse
                {
                    Topics = [],
                    ErrorCode = ErrorCode.None
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await coordinator.FetchOffsetsAsync([new TopicPartition("alpha", 0)], CancellationToken.None);
        await coordinator.FetchOffsetsAsync([new TopicPartition("beta", 1)], CancellationToken.None);

        await Assert.That(topicSnapshots.Count).IsEqualTo(2);
        await Assert.That(topicSnapshots[1].Length).IsEqualTo(1);
        await Assert.That(topicSnapshots[1][0].Name).IsEqualTo("beta");
        await Assert.That(topicSnapshots[1][0].PartitionCount).IsEqualTo(1);
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

        await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None))
            .Throws<KafkaTimeoutException>();
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

        // The mock signals fencingProcessed as it returns the fenced response, i.e. BEFORE the
        // heartbeat loop processes it. Wait for that signal, then poll for the actual state
        // transition. Both waits complete deterministically once the loop reaches the 2nd
        // (fenced) heartbeat, so there is no arbitrary wall-clock cap to race the CI thread-pool
        // scheduler; TUnit's test-level timeout remains the backstop for a genuine regression.
        // The loop breaks on FencedMemberEpoch (no auto-rejoin), so the transition is stable
        // until the explicit rejoin below.
        await fencingProcessed.Task;
        await TestWait.UntilAsync(
            () => coordinator.State == CoordinatorState.Unjoined && coordinator.GenerationId == 0,
            CancellationToken.None);

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

        // The mock signals fencingProcessed as it returns the fenced response, i.e. BEFORE the
        // heartbeat loop processes it. Wait for that signal, then poll for the actual state
        // transition. Both waits complete deterministically once the loop reaches the 2nd
        // (fenced) heartbeat, so there is no arbitrary wall-clock cap to race the CI thread-pool
        // scheduler; TUnit's test-level timeout remains the backstop for a genuine regression.
        // The loop breaks on FencedMemberEpoch (no auto-rejoin), so the transition is stable
        // until the explicit rejoin below.
        await fencingProcessed.Task;
        await TestWait.UntilAsync(
            () => coordinator.State == CoordinatorState.Unjoined && coordinator.GenerationId == -2,
            CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        // Static member resets _generationId to -2 (triggers MemberEpoch=-2 on rejoin)
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

    [Test]
    public async Task ConsumerProtocol_InitialJoin_SendsClientRack()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(clientRack: "rack-a");
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(r => r.RackId == "rack-a"),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region KIP-1082 Client-Generated Member ID Tests

    [Test]
    public async Task ConsumerProtocol_V1_InitialJoin_SendsClientGeneratedUuid()
    {
        // Broker supports v1 — client should generate a UUID, not send empty string
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 1);
        SetupFindCoordinator();

        ConsumerGroupHeartbeatRequest? capturedRequest = null;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedRequest = ci.Arg<ConsumerGroupHeartbeatRequest>();
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = capturedRequest.MemberId, // v1: broker echoes client-generated ID
                    MemberEpoch = 1,
                    HeartbeatIntervalMs = 5000
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.MemberId).IsNotEmpty();
        // Must be a valid UUID v4
        await Assert.That(Guid.TryParse(capturedRequest.MemberId, out _)).IsTrue();
    }

    [Test]
    public async Task ConsumerProtocol_V0_InitialJoin_SendsEmptyMemberId()
    {
        // Broker supports only v0 — client must send empty string (server-assigned ID)
        // _metadataManager already seeded with v0 in constructor
        SetupFindCoordinator();

        ConsumerGroupHeartbeatRequest? capturedRequest = null;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedRequest = ci.Arg<ConsumerGroupHeartbeatRequest>();
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "server-assigned-id",
                    MemberEpoch = 1,
                    HeartbeatIntervalMs = 5000
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.MemberId).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ConsumerProtocol_V1_AfterUnknownMemberId_GeneratesNewUuid()
    {
        // After UnknownMemberId reset, a fresh UUID should be generated (not reuse old one)
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 1);
        SetupFindCoordinator();

        var requests = new List<ConsumerGroupHeartbeatRequest>();
        var callCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var req = ci.Arg<ConsumerGroupHeartbeatRequest>();
                requests.Add(req);
                callCount++;

                return callCount switch
                {
                    // First: success
                    1 => ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = req.MemberId,
                        MemberEpoch = 1,
                        HeartbeatIntervalMs = 60000
                    }),
                    // Second: UnknownMemberId (broker forgot this member)
                    2 => ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.UnknownMemberId,
                        ErrorMessage = "Unknown member",
                        MemberEpoch = 0,
                        HeartbeatIntervalMs = 5000
                    }),
                    // Third: fresh join with new UUID
                    _ => ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = req.MemberId,
                        MemberEpoch = 2,
                        HeartbeatIntervalMs = 60000
                    })
                };
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        // First join succeeds
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        var firstUuid = requests[0].MemberId;

        // Force Unjoined so EnsureActiveGroupAsync re-enters the join path.
        // The join path will hit UnknownMemberId, which calls ResetMemberState(),
        // then retries with a fresh UUID.
        coordinator.RequestRejoin();
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);

        // The third request (after reset) should have a different UUID
        await Assert.That(requests).Count().IsGreaterThanOrEqualTo(3);
        var thirdUuid = requests[2].MemberId;

        await Assert.That(firstUuid).IsNotEmpty();
        await Assert.That(thirdUuid).IsNotEmpty();
        await Assert.That(thirdUuid).IsNotEqualTo(firstUuid);
        await Assert.That(Guid.TryParse(thirdUuid, out _)).IsTrue();
    }

    [Test]
    public async Task ConsumerProtocol_V1_AfterFencedMemberEpoch_ReusesSameMemberId()
    {
        // After FencedMemberEpoch, the member ID should be preserved (same member, just stale epoch)
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 1);
        SetupFindCoordinator();

        var requests = new List<ConsumerGroupHeartbeatRequest>();
        var callCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var req = ci.Arg<ConsumerGroupHeartbeatRequest>();
                requests.Add(req);
                callCount++;

                if (callCount == 1)
                {
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = req.MemberId,
                        MemberEpoch = 5,
                        HeartbeatIntervalMs = 60000
                    });
                }

                if (callCount == 2)
                {
                    // FencedMemberEpoch — member exists but epoch is stale
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.FencedMemberEpoch,
                        ErrorMessage = "Fenced",
                        MemberEpoch = 0,
                        HeartbeatIntervalMs = 5000
                    });
                }

                // Rejoin succeeds
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = req.MemberId,
                    MemberEpoch = 6,
                    HeartbeatIntervalMs = 60000
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        // First join
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        var firstUuid = requests[0].MemberId;

        // Force Unjoined so EnsureActiveGroupAsync re-enters the join path
        coordinator.RequestRejoin();
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);

        // After fencing, the rejoin should use the SAME member ID
        var rejoinUuid = requests[^1].MemberId;
        await Assert.That(rejoinUuid).IsEqualTo(firstUuid);
    }

    [Test]
    public async Task ConsumerProtocol_V1_MemberIdStoredOnCoordinator()
    {
        // The client-generated UUID should be exposed as the coordinator's MemberId
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 1);
        SetupFindCoordinator();

        ConsumerGroupHeartbeatRequest? capturedRequest = null;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedRequest = ci.Arg<ConsumerGroupHeartbeatRequest>();
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = capturedRequest.MemberId,
                    MemberEpoch = 1,
                    HeartbeatIntervalMs = 5000
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.MemberId).IsEqualTo(capturedRequest!.MemberId);
        await Assert.That(Guid.TryParse(coordinator.MemberId, out _)).IsTrue();
    }

    #endregion
}
