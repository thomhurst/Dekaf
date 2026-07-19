using System.Diagnostics;
using System.Reflection;
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
    [Test]
    public async Task PublicConstructor_PreservesSixParameterBinarySignature()
    {
        var constructor = typeof(ConsumerCoordinator).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            [
                typeof(ConsumerOptions),
                typeof(IConnectionPool),
                typeof(MetadataManager),
                typeof(Microsoft.Extensions.Logging.ILogger<ConsumerCoordinator>),
                typeof(Func<int>),
                typeof(Action<IReadOnlyList<TopicPartition>>)
            ],
            modifiers: null);

        await Assert.That(constructor).IsNotNull();
    }

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
        int rebalanceTimeoutMs = 30000,
        int maxPollIntervalMs = 300000) => new()
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = groupId,
            GroupRemoteAssignor = groupRemoteAssignor,
            GroupInstanceId = groupInstanceId,
            ClientRack = clientRack,
            RebalanceListener = rebalanceListener,
            HeartbeatIntervalMs = heartbeatIntervalMs,
            RebalanceTimeoutMs = rebalanceTimeoutMs,
            MaxPollIntervalMs = maxPollIntervalMs
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

    private static async ValueTask InvokeSteadyConsumerGroupHeartbeatAsync(ConsumerCoordinator coordinator)
    {
        var method = typeof(ConsumerCoordinator).GetMethod(
            "SendConsumerGroupHeartbeatAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var result = method!.Invoke(coordinator, [false, true, CancellationToken.None])!;
        var task = (Task)result.GetType().GetMethod("AsTask")!.Invoke(result, null)!;
        await task;
    }

    private static Task InvokeConsumerProtocolHeartbeatLoopAsync(
        ConsumerCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var method = typeof(ConsumerCoordinator).GetMethod(
            "ConsumerProtocolHeartbeatLoopAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        return (Task)method!.Invoke(coordinator, [cancellationToken])!;
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

    private static long GetCoordinatorLongField(ConsumerCoordinator coordinator, string fieldName)
    {
        var field = typeof(ConsumerCoordinator).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{fieldName} field not found.");

        return (long)field.GetValue(coordinator)!;
    }

    private static void SetCoordinatorLongField(
        ConsumerCoordinator coordinator,
        string fieldName,
        long value)
    {
        var field = typeof(ConsumerCoordinator).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{fieldName} field not found.");

        field.SetValue(coordinator, value);
    }

    private static async Task AssertOwnedTopicPartitionsAsync(
        IReadOnlyList<ConsumerGroupHeartbeatTopicPartitions>? topicPartitions,
        Guid topicId,
        params int[] partitions)
    {
        await Assert.That(topicPartitions).IsNotNull();
        await Assert.That(topicPartitions!).Count().IsEqualTo(1);
        await Assert.That(topicPartitions![0].TopicId).IsEqualTo(topicId);
        await Assert.That(topicPartitions![0].Partitions).Count().IsEqualTo(partitions.Length);

        for (var i = 0; i < partitions.Length; i++)
            await Assert.That(topicPartitions![0].Partitions[i]).IsEqualTo(partitions[i]);
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
        SetupFindCoordinator();

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
    [NotInParallel]
    public async Task ConsumerProtocol_SuccessfulSlowJoin_RefreshesPollDeadline()
    {
        SetupFindCoordinator();
        var options = CreateConsumerProtocolOptions(maxPollIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var staleTimestamp = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                SetCoordinatorLongField(coordinator, "_lastPollTimestamp", staleTimestamp);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = 1,
                    HeartbeatIntervalMs = 60_000
                });
            });

        coordinator.BeginForegroundPollActivity();
        try
        {
            await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        }
        finally
        {
            coordinator.EndForegroundPollActivity();
        }

        await Assert.That(GetCoordinatorLongField(coordinator, "_lastPollTimestamp"))
            .IsGreaterThan(staleTimestamp);
    }

    [Test]
    public async Task ConsumerProtocol_BackgroundRejoin_DoesNotRefreshPollDeadline()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };
        coordinator.BeginForegroundPollActivity();
        try
        {
            await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        }
        finally
        {
            coordinator.EndForegroundPollActivity();
        }
        await coordinator.StopHeartbeatAsync();

        coordinator.RequestRejoin();
        var staleTimestamp = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
        SetCoordinatorLongField(coordinator, "_lastPollTimestamp", staleTimestamp);

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        await Assert.That(GetCoordinatorLongField(coordinator, "_lastPollTimestamp"))
            .IsEqualTo(staleTimestamp);
    }

    [Test]
    public async Task ConsumerProtocol_InitialJoin_SendsMaxPollIntervalAsRebalanceTimeout()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(
            rebalanceTimeoutMs: 30_000,
            maxPollIntervalMs: 12_345);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(request => request != null && request.RebalanceTimeoutMs == 12_345),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RecordPollAsync_OverdueStableMember_ExpiresAssignmentBeforeReturning()
    {
        SetupFindCoordinator();
        SetupConsumerGroupHeartbeat(
            heartbeatIntervalMs: 60_000,
            assignment: CreateAssignment(TestTopicId, 0, 1));
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            maxPollIntervalMs: 300_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        var overdueTimestamp = Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 600L);
        SetCoordinatorLongField(coordinator, "_lastPollTimestamp", overdueTimestamp);
        var pollVersion = GetCoordinatorLongField(coordinator, "_pollVersion");

        await coordinator.RecordPollAsync(CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.Assignment).IsEmpty();
        await Assert.That(GetCoordinatorLongField(coordinator, "_lastPollTimestamp"))
            .IsGreaterThan(overdueTimestamp);
        await Assert.That(GetCoordinatorLongField(coordinator, "_pollVersion"))
            .IsEqualTo(pollVersion + 1);
        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(request => request != null && request.MemberEpoch == -1),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RecordPollAsync_OverdueUnjoinedMember_RefreshesDeadline()
    {
        var options = CreateConsumerProtocolOptions(maxPollIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var overdueTimestamp = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
        SetCoordinatorLongField(coordinator, "_lastPollTimestamp", overdueTimestamp);
        var pollVersion = GetCoordinatorLongField(coordinator, "_pollVersion");

        await coordinator.RecordPollAsync(CancellationToken.None);

        await Assert.That(GetCoordinatorLongField(coordinator, "_lastPollTimestamp"))
            .IsGreaterThan(overdueTimestamp);
        await Assert.That(GetCoordinatorLongField(coordinator, "_pollVersion"))
            .IsEqualTo(pollVersion + 1);
    }

    [Test]
    [NotInParallel]
    public async Task RecordPollAsync_ActiveForegroundPollActivity_DoesNotExpireMember()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            maxPollIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        coordinator.BeginForegroundPollActivity();
        try
        {
            SetCoordinatorLongField(
                coordinator,
                "_lastPollTimestamp",
                Stopwatch.GetTimestamp() - Stopwatch.Frequency);

            await coordinator.RecordPollAsync(CancellationToken.None);

            await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
            await _connection.DidNotReceive().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Is<ConsumerGroupHeartbeatRequest>(request => request != null && request.MemberEpoch == -1),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            coordinator.EndForegroundPollActivity();
        }
    }

    [Test]
    public async Task ConsumerProtocol_SteadyHeartbeat_CrossesMaxPollWhileAcquiringConnection_DoesNotSend()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            maxPollIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        var connectionRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connectionAvailable = new TaskCompletionSource<IKafkaConnection>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _connectionPool.GetConnectionByIndexAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                connectionRequested.TrySetResult();
                return new ValueTask<IKafkaConnection>(connectionAvailable.Task);
            });

        var heartbeat = InvokeSteadyConsumerGroupHeartbeatAsync(coordinator).AsTask();
        await connectionRequested.Task.WaitAsync(TimeSpan.FromSeconds(1));
        SetCoordinatorLongField(
            coordinator,
            "_lastPollTimestamp",
            Stopwatch.GetTimestamp() - Stopwatch.Frequency);
        connectionAvailable.SetResult(_connection);

        await heartbeat.WaitAsync(TimeSpan.FromSeconds(1));

        await _connection.DidNotReceive().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(request => request != null && request.MemberEpoch > 0),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [Timeout(30_000)]
    [NotInParallel]
    public async Task ConsumerProtocol_SteadyHeartbeat_CrossesMaxPollWhileAwaitingResponse_DiscardsResponse(
        CancellationToken cancellationToken)
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            maxPollIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        var heartbeatSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var heartbeatResponse = new TaskCompletionSource<ConsumerGroupHeartbeatResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                heartbeatSent.TrySetResult();
                return new ValueTask<ConsumerGroupHeartbeatResponse>(heartbeatResponse.Task);
            });

        var heartbeat = InvokeSteadyConsumerGroupHeartbeatAsync(coordinator).AsTask();
        await heartbeatSent.Task.WaitAsync(cancellationToken);
        SetCoordinatorLongField(
            coordinator,
            "_lastPollTimestamp",
            Stopwatch.GetTimestamp() - Stopwatch.Frequency);
        heartbeatResponse.SetResult(new ConsumerGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.None,
            MemberId = "member-1",
            MemberEpoch = 99,
            HeartbeatIntervalMs = 60_000
        });

        await heartbeat.WaitAsync(cancellationToken);

        await Assert.That(coordinator.GenerationId).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumerProtocol_RejoinCommitFence_DoesNotSuppressSteadyHeartbeat()
    {
        SetupFindCoordinator();
        var steadyHeartbeatCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ConsumerGroupHeartbeatRequest>()!;
                if (request.MemberEpoch > 0)
                    Interlocked.Increment(ref steadyHeartbeatCount);

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = request.MemberEpoch > 0 ? request.MemberEpoch : 1,
                    HeartbeatIntervalMs = 60_000,
                    Assignment = CreateAssignment(TestTopicId, 0)
                });
            });

        var options = CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.RecordPollAsync(CancellationToken.None);
        SetCoordinatorLongField(coordinator, "_maxPollExpiredAtPollVersion", 0);
        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic" },
            CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(steadyHeartbeatCount).IsEqualTo(1);
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConsumerProtocol_MaxPollIntervalExceeded_LeavesDynamicMember(
        CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        var leaveRequest = new TaskCompletionSource<ConsumerGroupHeartbeatRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ConsumerGroupHeartbeatRequest>()!;
                if (request.MemberEpoch == -1)
                    leaveRequest.TrySetResult(request);

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = request.MemberEpoch == -1 ? -1 : 1,
                    HeartbeatIntervalMs = 10
                });
            });

        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 10,
            maxPollIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken);

        var request = await leaveRequest.Task.WaitAsync(cancellationToken);
        await Assert.That(request.MemberEpoch).IsEqualTo(-1);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
    }

    [Test]
    [NotInParallel]
    [Timeout(5_000)]
    public async Task ConsumerProtocol_StaticMaxPollExpiry_RejoinsWithNegativeTwoAndSameMemberId(
        CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        var leaveRequest = new TaskCompletionSource<ConsumerGroupHeartbeatRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var rejoinRequest = new TaskCompletionSource<ConsumerGroupHeartbeatRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var rejoining = 0;

        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ConsumerGroupHeartbeatRequest>()!;
                if (Volatile.Read(ref rejoining) == 1)
                {
                    rejoinRequest.TrySetResult(request);
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-1",
                        MemberEpoch = 2,
                        HeartbeatIntervalMs = 60_000
                    });
                }

                if (request.MemberEpoch == -2)
                    leaveRequest.TrySetResult(request);

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = request.MemberEpoch == -2 ? -2 : 1,
                    HeartbeatIntervalMs = 10
                });
            });

        var options = CreateConsumerProtocolOptions(
            groupInstanceId: "static-1",
            heartbeatIntervalMs: 10,
            maxPollIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);
        var leave = await leaveRequest.Task.WaitAsync(cancellationToken);

        await Assert.That(leave.MemberId).IsEqualTo("member-1");
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);

        Volatile.Write(ref rejoining, 1);
        // Prefetch calls EnsureActiveGroupAsync without recording foreground poll progress.
        await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);

        await Assert.That(rejoinRequest.Task.IsCompleted).IsFalse();
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);

        await coordinator.RecordPollAsync(cancellationToken);
        await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);

        var rejoin = await rejoinRequest.Task.WaitAsync(cancellationToken);
        await Assert.That(rejoin.MemberId).IsEqualTo("member-1");
        await Assert.That(rejoin.MemberEpoch).IsEqualTo(-2);
        await Assert.That(rejoin.InstanceId).IsEqualTo("static-1");
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConsumerProtocol_MaxPollIntervalExceeded_RejectsCommitWhenLeaveFails(
        CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        var leaveAttempted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ConsumerGroupHeartbeatRequest>()!;
                if (request.MemberEpoch == -1)
                {
                    leaveAttempted.TrySetResult();
                    return ValueTask.FromException<ConsumerGroupHeartbeatResponse>(
                        new InvalidOperationException("leave failed"));
                }

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = 1,
                    HeartbeatIntervalMs = 10
                });
            });

        _metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            OffsetCommitRequest.HighestSupportedVersion);
        var commitRequestCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitRequestCount);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 10,
            maxPollIntervalMs: 50);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken);
        await leaveAttempted.Task.WaitAsync(cancellationToken);

        var exception = await Assert.That(async () =>
                await coordinator.CommitOffsetsAsync(
                    [new TopicPartitionOffset("test-topic", 0, 1)],
                    cancellationToken))
            .Throws<GroupException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(commitRequestCount).IsEqualTo(0);
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConsumerProtocol_ForegroundExpiry_StopsConcurrentStaleHeartbeat(
        CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        var leaveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var leaveResponse = new TaskCompletionSource<ConsumerGroupHeartbeatResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var steadyHeartbeatCount = 0;

        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ConsumerGroupHeartbeatRequest>()!;
                if (request.MemberEpoch == 0)
                {
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-1",
                        MemberEpoch = 1,
                        HeartbeatIntervalMs = 60_000
                    });
                }

                if (request.MemberEpoch == -1)
                {
                    leaveStarted.TrySetResult();
                    return new ValueTask<ConsumerGroupHeartbeatResponse>(leaveResponse.Task);
                }

                Interlocked.Increment(ref steadyHeartbeatCount);
                return ValueTask.FromException<ConsumerGroupHeartbeatResponse>(
                    new GroupException(ErrorCode.FencedMemberEpoch, "stale heartbeat"));
            });

        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            maxPollIntervalMs: 300_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken);
        SetCoordinatorLongField(
            coordinator,
            "_lastPollTimestamp",
            Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 600L));

        var foregroundPoll = coordinator.RecordPollAsync(cancellationToken).AsTask();
        await leaveStarted.Task.WaitAsync(cancellationToken);
        var competingHeartbeat = InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, cancellationToken);

        leaveResponse.TrySetException(new InvalidOperationException("leave failed"));
        await foregroundPoll;
        await competingHeartbeat.WaitAsync(cancellationToken);

        await Assert.That(steadyHeartbeatCount).IsEqualTo(0);
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConsumerProtocol_ForegroundExpiry_DropsInFlightHeartbeatResponseAfterRejoin(
        CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        var steadyHeartbeatStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var steadyHeartbeatResponse = new TaskCompletionSource<ConsumerGroupHeartbeatResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var joinCount = 0;

        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ConsumerGroupHeartbeatRequest>()!;
                if (request.MemberEpoch == 0)
                {
                    var memberEpoch = Interlocked.Increment(ref joinCount) == 1 ? 1 : 3;
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-1",
                        MemberEpoch = memberEpoch,
                        HeartbeatIntervalMs = 60_000,
                        Assignment = CreateAssignment(TestTopicId, 0)
                    });
                }

                if (request.MemberEpoch == -1)
                {
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-1",
                        MemberEpoch = -1,
                        HeartbeatIntervalMs = 60_000
                    });
                }

                steadyHeartbeatStarted.TrySetResult();
                return new ValueTask<ConsumerGroupHeartbeatResponse>(steadyHeartbeatResponse.Task);
            });

        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            maxPollIntervalMs: 300_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };
        await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);

        var inFlightHeartbeat = InvokeSteadyConsumerGroupHeartbeatAsync(coordinator).AsTask();
        await steadyHeartbeatStarted.Task.WaitAsync(cancellationToken);
        SetCoordinatorLongField(
            coordinator,
            "_lastPollTimestamp",
            Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 600L));

        await coordinator.RecordPollAsync(cancellationToken);
        await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);
        steadyHeartbeatResponse.TrySetResult(new ConsumerGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.None,
            MemberId = "member-1",
            MemberEpoch = 2,
            HeartbeatIntervalMs = 60_000,
            Assignment = CreateAssignment(TestTopicId, 1)
        });
        await inFlightHeartbeat;

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.GenerationId).IsEqualTo(3);
        await Assert.That(coordinator.Assignment).Count().IsEqualTo(1);
        await Assert.That(coordinator.Assignment).Contains(new TopicPartition("test-topic", 0));
        await Assert.That(coordinator.Assignment).DoesNotContain(new TopicPartition("test-topic", 1));
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConsumerProtocol_ExpiryWaitsForPublishedHeartbeatCallbacks(
        CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        var revocationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRevocation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var assignedCallbackCount = 0;
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsRevokedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                revocationStarted.TrySetResult();
                return new ValueTask(releaseRevocation.Task);
            });
        listener.OnPartitionsAssignedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref assignedCallbackCount);
                return ValueTask.CompletedTask;
            });

        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ConsumerGroupHeartbeatRequest>()!;
                if (request.MemberEpoch == -1)
                {
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-1",
                        MemberEpoch = -1,
                        HeartbeatIntervalMs = 60_000
                    });
                }

                var isInitial = request.MemberEpoch == 0;
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = isInitial ? 1 : 2,
                    HeartbeatIntervalMs = 60_000,
                    Assignment = CreateAssignment(TestTopicId, isInitial ? 0 : 1)
                });
            });

        var options = CreateConsumerProtocolOptions(
            rebalanceListener: listener,
            heartbeatIntervalMs: 60_000,
            maxPollIntervalMs: 300_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken);
        await coordinator.StopHeartbeatAsync();

        var steadyHeartbeat = InvokeSteadyConsumerGroupHeartbeatAsync(coordinator).AsTask();
        await revocationStarted.Task.WaitAsync(cancellationToken);
        SetCoordinatorLongField(
            coordinator,
            "_lastPollTimestamp",
            Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 600L));

        var expiration = coordinator.RecordPollAsync(cancellationToken).AsTask();
        await Assert.That(expiration.IsCompleted).IsFalse();

        releaseRevocation.TrySetResult();
        await steadyHeartbeat.WaitAsync(cancellationToken);
        await expiration.WaitAsync(cancellationToken);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.Assignment).IsEmpty();
        await Assert.That(assignedCallbackCount).IsEqualTo(2);
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConsumerProtocol_SteadyHeartbeatListener_ReentersCoordinatorWithoutDeadlock(
        CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        var heartbeatCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var count = Interlocked.Increment(ref heartbeatCount);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = count,
                    HeartbeatIntervalMs = 60_000,
                    Assignment = CreateAssignment(TestTopicId, count - 1)
                });
            });

        var topics = new HashSet<string> { "test-topic" };
        ConsumerCoordinator? coordinator = null;
        var assignmentCallbackCount = 0;
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsAssignedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref assignmentCallbackCount) == 1
                ? ValueTask.CompletedTask
                : coordinator!.EnsureActiveGroupAsync(topics, cancellationToken));

        var options = CreateConsumerProtocolOptions(
            rebalanceListener: listener,
            heartbeatIntervalMs: 60_000);
        await using var ownedCoordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        coordinator = ownedCoordinator;
        await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);
        await coordinator.StopHeartbeatAsync();

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator).AsTask().WaitAsync(cancellationToken);

        await Assert.That(assignmentCallbackCount).IsEqualTo(2);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
    }

    [Test]
    public async Task ConsumerProtocol_SteadyHeartbeat_ReleasesConnectionLeaseBeforeCallbacks()
    {
        var connection = Substitute.For<IKafkaConnection>();
        var retirableConnection = new RetirableTestConnection(connection);
        _connectionPool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(retirableConnection));
        _connectionPool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(retirableConnection));
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
                        Key = "test-group",
                        NodeId = 0,
                        Host = "localhost",
                        Port = 9092,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));
        var heartbeatCount = 0;
        connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var count = Interlocked.Increment(ref heartbeatCount);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = count,
                    HeartbeatIntervalMs = 60_000,
                    Assignment = CreateAssignment(TestTopicId, count - 1)
                });
            });

        var leaseStatesDuringCallbacks = new List<int>();
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsAssignedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                leaseStatesDuringCallbacks.Add(retirableConnection.LeaseCount);
                return ValueTask.CompletedTask;
            });
        var options = CreateConsumerProtocolOptions(
            rebalanceListener: listener,
            heartbeatIntervalMs: 60_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(leaseStatesDuringCallbacks).IsEquivalentTo([0, 0]);
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConsumerProtocol_HeartbeatExpiry_BlocksRejoinUntilPartitionsLostCompletes(
        CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        var lossStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoss = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var assignmentCount = 0;
        var joinRequestCount = 0;
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsAssignedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref assignmentCount);
                return ValueTask.CompletedTask;
            });
        listener.OnPartitionsLostAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                lossStarted.TrySetResult();
                return new ValueTask(releaseLoss.Task);
            });

        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ConsumerGroupHeartbeatRequest>()!;
                if (request.MemberEpoch == -1)
                {
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-1",
                        MemberEpoch = -1,
                        HeartbeatIntervalMs = 60_000
                    });
                }

                var join = Interlocked.Increment(ref joinRequestCount);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = join,
                    HeartbeatIntervalMs = 60_000,
                    Assignment = CreateAssignment(TestTopicId, join - 1)
                });
            });

        var options = CreateConsumerProtocolOptions(
            rebalanceListener: listener,
            heartbeatIntervalMs: 60_000,
            maxPollIntervalMs: 300_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };
        await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);
        SetCoordinatorLongField(
            coordinator,
            "_lastPollTimestamp",
            Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 600L));

        var heartbeatExpiry = InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, cancellationToken);
        await lossStarted.Task.WaitAsync(cancellationToken);

        await coordinator.RecordPollAsync(cancellationToken);
        await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);
        var joinsWhileLossPending = Volatile.Read(ref joinRequestCount);
        var assignmentsWhileLossPending = Volatile.Read(ref assignmentCount);

        releaseLoss.TrySetResult();
        await heartbeatExpiry.WaitAsync(cancellationToken);
        await coordinator.RecordPollAsync(cancellationToken);
        await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);

        await Assert.That(joinsWhileLossPending).IsEqualTo(1);
        await Assert.That(assignmentsWhileLossPending).IsEqualTo(1);
        await Assert.That(joinRequestCount).IsEqualTo(2);
        await Assert.That(assignmentCount).IsEqualTo(2);
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConsumerProtocol_ConcurrentForegroundExpiry_DoesNotRecordPollDuringPartitionsLost(
        CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        var lossStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoss = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsLostAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                lossStarted.TrySetResult();
                return new ValueTask(releaseLoss.Task);
            });

        var joinRequestCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ConsumerGroupHeartbeatRequest>()!;
                if (request.MemberEpoch == -1)
                {
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-1",
                        MemberEpoch = -1,
                        HeartbeatIntervalMs = 60_000
                    });
                }

                var join = Interlocked.Increment(ref joinRequestCount);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = join,
                    HeartbeatIntervalMs = 60_000,
                    Assignment = CreateAssignment(TestTopicId, join - 1)
                });
            });

        var options = CreateConsumerProtocolOptions(
            rebalanceListener: listener,
            heartbeatIntervalMs: 60_000,
            maxPollIntervalMs: 300_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };
        await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);
        SetCoordinatorLongField(
            coordinator,
            "_lastPollTimestamp",
            Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 600L));
        var pollVersion = GetCoordinatorLongField(coordinator, "_pollVersion");
        var coordinatorLock = (SemaphoreSlim)typeof(ConsumerCoordinator).GetField(
            "_lock",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(coordinator)!;

        await coordinatorLock.WaitAsync(cancellationToken);
        Task firstPoll;
        Task secondPoll;
        try
        {
            firstPoll = coordinator.RecordPollAsync(cancellationToken).AsTask();
            secondPoll = coordinator.RecordPollAsync(cancellationToken).AsTask();
        }
        finally
        {
            coordinatorLock.Release();
        }

        try
        {
            await lossStarted.Task.WaitAsync(cancellationToken);
            var nonExpiringPoll = await Task.WhenAny(firstPoll, secondPoll).WaitAsync(cancellationToken);
            await nonExpiringPoll;
            await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);

            await Assert.That(GetCoordinatorLongField(coordinator, "_pollVersion"))
                .IsEqualTo(pollVersion);
            await Assert.That(joinRequestCount).IsEqualTo(1);

            releaseLoss.TrySetResult();
            await Task.WhenAll(firstPoll, secondPoll).WaitAsync(cancellationToken);
            await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);

            await Assert.That(GetCoordinatorLongField(coordinator, "_pollVersion"))
                .IsEqualTo(pollVersion + 1);
            await Assert.That(joinRequestCount).IsEqualTo(2);
        }
        finally
        {
            releaseLoss.TrySetResult();
        }
    }

    [Test]
    public async Task RecordPollIfLossNotificationComplete_PendingLoss_DoesNotAdvancePollVersion()
    {
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var pollVersion = GetCoordinatorLongField(coordinator, "_pollVersion");
        typeof(ConsumerCoordinator).GetField(
            "_maxPollLossNotificationPending",
            BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(coordinator, 1);
        var method = typeof(ConsumerCoordinator).GetMethod(
            "RecordPollIfLossNotificationComplete",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        method.Invoke(coordinator, [Stopwatch.GetTimestamp()]);

        await Assert.That(GetCoordinatorLongField(coordinator, "_pollVersion"))
            .IsEqualTo(pollVersion);
    }

    [Test]
    public async Task CommitOffsetsAsync_UnknownCoordinator_RediscoversBeforeCommit()
    {
        var findCoordinatorCount = 0;
        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref findCoordinatorCount);
                return ValueTask.FromResult(new FindCoordinatorResponse
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
                });
            });
        SetupConsumerGroupHeartbeat();
        _metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            OffsetCommitRequest.HighestSupportedVersion);

        var commitRequestCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitRequestCount);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        SetPrivateField(coordinator, "_coordinatorId", -1);
        _connectionPool.GetConnectionByIndexAsync(
                Arg.Is<int>(brokerId => brokerId < 0),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<IKafkaConnection>>(_ =>
                throw new InvalidOperationException("Unknown broker ID: -1"));

        await coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 0, 1)],
            CancellationToken.None);

        await Assert.That(findCoordinatorCount).IsEqualTo(2);
        await Assert.That(commitRequestCount).IsEqualTo(1);
    }

    [Test]
    public async Task CommitOffsetsAsync_OverdueBeforeHeartbeatExpiry_RejectsCommit()
    {
        SetupFindCoordinator();
        SetupConsumerGroupHeartbeat(heartbeatIntervalMs: 60_000);
        _metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            OffsetCommitRequest.HighestSupportedVersion);

        var commitRequestCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitRequestCount);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        var options = CreateConsumerProtocolOptions(maxPollIntervalMs: 300_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        SetCoordinatorLongField(
            coordinator,
            "_lastPollTimestamp",
            Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 600L));

        var exception = await Assert.That(async () =>
                await coordinator.CommitOffsetsAsync(
                    [new TopicPartitionOffset("test-topic", 0, 1)],
                    CancellationToken.None))
            .Throws<GroupException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(commitRequestCount).IsEqualTo(0);
    }

    [Test]
    [Timeout(5_000)]
    public async Task CommitOffsetsAsync_PollExpiresDuringConnectionWait_RejectsCommit(
        CancellationToken cancellationToken)
    {
        SetupFindCoordinator();
        SetupConsumerGroupHeartbeat(heartbeatIntervalMs: 60_000);
        _metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            OffsetCommitRequest.HighestSupportedVersion);

        var options = CreateConsumerProtocolOptions(maxPollIntervalMs: 300_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken);

        var connectionRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseConnection = new TaskCompletionSource<IKafkaConnection>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _connectionPool.GetConnectionByIndexAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                connectionRequested.TrySetResult();
                return new ValueTask<IKafkaConnection>(releaseConnection.Task);
            });

        var commitRequestCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitRequestCount);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        var commit = coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 0, 1)],
            cancellationToken).AsTask();
        await connectionRequested.Task.WaitAsync(cancellationToken);
        SetCoordinatorLongField(
            coordinator,
            "_lastPollTimestamp",
            Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 600L));
        releaseConnection.TrySetResult(_connection);

        var exception = await Assert.That(async () => await commit).Throws<GroupException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(commitRequestCount).IsEqualTo(0);
    }

    [Test]
    public async Task CommitOffsetsAsync_EstablishedMaxPollFence_RejectsDuringForegroundPollActivity()
    {
        _metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            OffsetCommitRequest.HighestSupportedVersion);
        SetupSuccessfulConsumerProtocolJoin();
        var commitRequestCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitRequestCount);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        SetCoordinatorLongField(
            coordinator,
            "_maxPollExpiredAtPollVersion",
            GetCoordinatorLongField(coordinator, "_pollVersion"));

        coordinator.BeginForegroundPollActivity();
        GroupException? exception;
        try
        {
            exception = await Assert.That(async () =>
                    await coordinator.CommitOffsetsAsync(
                        [new TopicPartitionOffset("test-topic", 0, 1)],
                        CancellationToken.None))
                .Throws<GroupException>();
        }
        finally
        {
            coordinator.EndForegroundPollActivity();
        }

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(commitRequestCount).IsEqualTo(0);
    }

    [Test]
    public async Task CommitOffsetsAsync_RejoinPreservesFenceUntilAssignmentSync()
    {
        SetupSuccessfulConsumerProtocolJoin(assignment: CreateAssignment(TestTopicId, 0));
        _metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            OffsetCommitRequest.HighestSupportedVersion);

        var commitRequestCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitRequestCount);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.RecordPollAsync(CancellationToken.None);
        SetCoordinatorLongField(coordinator, "_maxPollExpiredAtPollVersion", 0);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        var exception = await Assert.That(async () =>
                await coordinator.CommitOffsetsAsync(
                    [new TopicPartitionOffset("test-topic", 0, 1)],
                    CancellationToken.None))
            .Throws<GroupException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(commitRequestCount).IsEqualTo(0);

        var (_, assignmentVersion, _) = await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(
            CancellationToken.None);
        coordinator.AcknowledgeAssignmentSync(assignmentVersion);
        await coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 0, 1)],
            CancellationToken.None);

        await Assert.That(commitRequestCount).IsEqualTo(1);
    }

    [Test]
    public async Task EnsureActiveGroupAsync_StableWithRetainedFence_PreservesMemberEpoch()
    {
        SetupFindCoordinator();
        SetupConsumerGroupHeartbeat(memberEpoch: 7, heartbeatIntervalMs: 60_000);
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.RecordPollAsync(CancellationToken.None);
        SetCoordinatorLongField(coordinator, "_maxPollExpiredAtPollVersion", 0);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.GenerationId).IsEqualTo(7);
    }

    [Test]
    public async Task CommitOffsetsAsync_BackgroundAssignmentSyncPreservesFenceUntilForegroundPoll()
    {
        SetupSuccessfulConsumerProtocolJoin(assignment: CreateAssignment(TestTopicId, 0));
        _metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            OffsetCommitRequest.HighestSupportedVersion);

        var commitRequestCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitRequestCount);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        SetCoordinatorLongField(
            coordinator,
            "_maxPollExpiredAtPollVersion",
            GetCoordinatorLongField(coordinator, "_pollVersion"));

        var (_, assignmentVersion, _) = await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(
            CancellationToken.None);
        coordinator.AcknowledgeAssignmentSync(assignmentVersion);

        var exception = await Assert.That(async () =>
                await coordinator.CommitOffsetsAsync(
                    [new TopicPartitionOffset("test-topic", 0, 1)],
                    CancellationToken.None))
            .Throws<GroupException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(commitRequestCount).IsEqualTo(0);
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
    public async Task ConsumerProtocol_RevocationCallback_ObservesPublishedAssignment()
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
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = count,
                    HeartbeatIntervalMs = 60000,
                    Assignment = count == 1
                        ? CreateAssignment(TestTopicId, 0, 1)
                        : CreateAssignment(TestTopicId, 1)
                });
            });

        ConsumerCoordinator? coordinator = null;
        HashSet<TopicPartition>? assignmentObservedByRevokingCallback = null;
        var versionObservedByRevokingCallback = -1;
        var assignmentSyncObservedByRevokingCallback = true;
        HashSet<TopicPartition>? assignmentObservedByCallback = null;
        var versionObservedByCallback = -1;

        void OnPartitionsRevoking(IReadOnlyList<TopicPartition> _)
        {
            assignmentObservedByRevokingCallback = coordinator!.Assignment.ToHashSet();
            versionObservedByRevokingCallback = coordinator.AssignmentVersion;
            assignmentSyncObservedByRevokingCallback =
                coordinator.IsAssignmentSyncCurrent(versionObservedByRevokingCallback);
        }

        void OnPartitionsRevoked(IReadOnlyList<TopicPartition> _)
        {
            assignmentObservedByCallback = coordinator!.Assignment.ToHashSet();
            versionObservedByCallback = coordinator.AssignmentVersion;
        }

        var options = CreateConsumerProtocolOptions(heartbeatIntervalMs: 60000);
        coordinator = new ConsumerCoordinator(
            options,
            _connectionPool,
            _metadataManager,
            logger: null,
            getConnectionCount: null,
            onPartitionsRevoked: OnPartitionsRevoked,
            onPartitionsRevoking: OnPartitionsRevoking);
        await using var coordinatorLifetime = coordinator;
        var topics = new HashSet<string> { "test-topic" };

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        var versionAfterInitialAssignment = coordinator.AssignmentVersion;

        coordinator.RequestRejoin();
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        await Assert.That(assignmentObservedByRevokingCallback).IsNotNull();
        await Assert.That(assignmentObservedByRevokingCallback!).Contains(new TopicPartition("test-topic", 0));
        await Assert.That(assignmentObservedByRevokingCallback).Contains(new TopicPartition("test-topic", 1));
        await Assert.That(versionObservedByRevokingCallback).IsEqualTo(versionAfterInitialAssignment);
        await Assert.That(assignmentSyncObservedByRevokingCallback).IsFalse();
        await Assert.That(assignmentObservedByCallback).IsNotNull();
        await Assert.That(assignmentObservedByCallback!).Contains(new TopicPartition("test-topic", 1));
        await Assert.That(assignmentObservedByCallback).DoesNotContain(new TopicPartition("test-topic", 0));
        await Assert.That(versionObservedByCallback).IsEqualTo(versionAfterInitialAssignment + 1);
    }

    [Test]
    [Timeout(5_000)]
    public async Task ConsumerProtocol_RevocationHook_CompletesBeforeUserListener(
        CancellationToken cancellationToken)
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
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = count,
                    HeartbeatIntervalMs = 60_000,
                    Assignment = count == 1
                        ? CreateAssignment(TestTopicId, 0, 1)
                        : CreateAssignment(TestTopicId, 1)
                });
            });

        var hookStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHook = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listenerCallCount = 0;
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsRevokedAsync(
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref listenerCallCount);
                return ValueTask.CompletedTask;
            });

        async ValueTask OnPartitionsRevokedAsync(
            IReadOnlyList<TopicPartition> _partitions,
            CancellationToken _cancellationToken)
        {
            hookStarted.TrySetResult();
            await releaseHook.Task;
        }

        var options = CreateConsumerProtocolOptions(
            rebalanceListener: listener,
            heartbeatIntervalMs: 60_000);
        await using var coordinator = new ConsumerCoordinator(
            options,
            _connectionPool,
            _metadataManager,
            logger: null,
            getConnectionCount: null,
            onPartitionsRevoked: null,
            onPartitionsRevoking: null,
            onPartitionsRevokedAsync: OnPartitionsRevokedAsync);
        var topics = new HashSet<string> { "test-topic" };

        await coordinator.EnsureActiveGroupAsync(topics, cancellationToken);
        await coordinator.StopHeartbeatAsync();

        coordinator.RequestRejoin();
        var rejoin = coordinator.EnsureActiveGroupAsync(topics, cancellationToken).AsTask();
        await hookStarted.Task.WaitAsync(cancellationToken);

        await Assert.That(Volatile.Read(ref listenerCallCount)).IsEqualTo(0);

        releaseHook.TrySetResult();
        await rejoin;
        await coordinator.StopHeartbeatAsync();

        await Assert.That(Volatile.Read(ref listenerCallCount)).IsEqualTo(1);
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
    public async Task ConsumerProtocol_WhenStableAndTopicsChange_NextHeartbeatSendsUpdatedTopics()
    {
        SetupFindCoordinator();

        var requests = new List<ConsumerGroupHeartbeatRequest>();
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var request = ci.Arg<ConsumerGroupHeartbeatRequest>()!;
                requests.Add(request);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = request.MemberId.Length == 0 ? "member-1" : request.MemberId,
                    MemberEpoch = request.MemberEpoch == 0 ? 1 : request.MemberEpoch,
                    HeartbeatIntervalMs = 60000
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        requests.Clear();

        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic", "orders" },
            CancellationToken.None);

        await Assert.That(requests).IsEmpty();

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(requests).Count().IsEqualTo(1);
        await Assert.That(requests[0].SubscribedTopicNames).IsNotNull();
        await Assert.That(requests[0].SubscribedTopicNames!).Contains("test-topic");
        await Assert.That(requests[0].SubscribedTopicNames!).Contains("orders");
        await Assert.That(requests[0].SubscribedTopicRegex).IsNull();
    }

    [Test]
    public async Task ConsumerProtocol_SteadyHeartbeat_SendsOwnedPartitionsOnlyWhenAssignmentChanged()
    {
        SetupFindCoordinator();

        var requests = new List<ConsumerGroupHeartbeatRequest>();
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var request = ci.Arg<ConsumerGroupHeartbeatRequest>()!;
                requests.Add(request);
                var call = requests.Count;

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = request.MemberId.Length == 0 ? "member-1" : request.MemberId,
                    MemberEpoch = request.MemberEpoch == 0 ? 1 : request.MemberEpoch,
                    HeartbeatIntervalMs = 60000,
                    Assignment = call == 1 ? CreateAssignment(TestTopicId, 0, 1) : null
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(requests).Count().IsEqualTo(3);
        await Assert.That(requests[0].TopicPartitions).IsNotNull();
        await Assert.That(requests[0].TopicPartitions!).IsEmpty();

        await AssertOwnedTopicPartitionsAsync(requests[1].TopicPartitions, TestTopicId, 0, 1);

        await Assert.That(requests[2].TopicPartitions).IsNull();
    }

    [Test]
    public async Task ConsumerProtocol_SteadyHeartbeat_RetriesOwnedPartitionsAfterFailedHeartbeat()
    {
        SetupFindCoordinator();

        var requests = new List<ConsumerGroupHeartbeatRequest>();
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var request = ci.Arg<ConsumerGroupHeartbeatRequest>()!;
                requests.Add(request);
                var call = requests.Count;

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = call == 2 ? ErrorCode.NotCoordinator : ErrorCode.None,
                    ErrorMessage = call == 2 ? "Coordinator moved" : null,
                    MemberId = request.MemberId.Length == 0 ? "member-1" : request.MemberId,
                    MemberEpoch = request.MemberEpoch == 0 ? 1 : request.MemberEpoch,
                    HeartbeatIntervalMs = 60000,
                    Assignment = call == 1 ? CreateAssignment(TestTopicId, 0, 1) : null
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        GroupException? caught = null;
        try
        {
            await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        }
        catch (GroupException ex)
        {
            caught = ex;
        }

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.ErrorCode).IsEqualTo(ErrorCode.NotCoordinator);
        await Assert.That(requests).Count().IsEqualTo(3);
        await AssertOwnedTopicPartitionsAsync(requests[1].TopicPartitions, TestTopicId, 0, 1);
        await AssertOwnedTopicPartitionsAsync(requests[2].TopicPartitions, TestTopicId, 0, 1);
    }

    [Test]
    public async Task ConsumerProtocol_InitialJoin_SendsMemberEpochZero()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(r => r != null && r.MemberEpoch == 0),
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
                r != null && r.SubscribedTopicNames != null && r.SubscribedTopicNames.Contains("test-topic")),
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
                assignedPartitions.AddRange(ci.Arg<IEnumerable<TopicPartition>>()!);
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
        SetupFindCoordinator();
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, OffsetCommitRequest.LowestSupportedVersion, OffsetCommitRequest.HighestSupportedVersion);

        var topicSnapshots = new List<(string Name, int PartitionCount)[]>();
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var request = ci.Arg<OffsetCommitRequest>()!;
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
                var request = ci.Arg<OffsetFetchRequest>()!;
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

    [Test]
    public async Task FetchOffsetsAsync_UnknownCoordinator_RediscoversBeforeFetch()
    {
        var findCoordinatorCount = 0;
        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref findCoordinatorCount);
                return ValueTask.FromResult(new FindCoordinatorResponse
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
                });
            });
        SetupConsumerGroupHeartbeat();
        _metadataManager.SetApiVersion(
            ApiKey.OffsetFetch,
            OffsetFetchRequest.LowestSupportedVersion,
            OffsetFetchRequest.HighestSupportedVersion);

        var fetchRequestCount = 0;
        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref fetchRequestCount);
                return ValueTask.FromResult(new OffsetFetchResponse
                {
                    Topics = [],
                    ErrorCode = ErrorCode.None
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        SetPrivateField(coordinator, "_coordinatorId", -1);
        _connectionPool.GetConnectionByIndexAsync(
                Arg.Is<int>(brokerId => brokerId < 0),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<IKafkaConnection>>(_ =>
                throw new InvalidOperationException("Unknown broker ID: -1"));

        await coordinator.FetchOffsetsAsync(
            [new TopicPartition("test-topic", 0)],
            CancellationToken.None);

        await Assert.That(findCoordinatorCount).IsEqualTo(2);
        await Assert.That(fetchRequestCount).IsEqualTo(1);
    }

    [Test]
    public async Task FetchOffsetsAsync_Kip848Member_SendsMemberIdentity()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, 9, 9);
        SetupSuccessfulConsumerProtocolJoin(memberId: "member-42", memberEpoch: 7);

        OffsetFetchRequest? capturedRequest = null;
        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.Arg<OffsetFetchRequest>()!;
                return ValueTask.FromResult(new OffsetFetchResponse
                {
                    Groups =
                    [
                        new OffsetFetchResponseGroup
                        {
                            GroupId = "test-group",
                            Topics = [],
                            ErrorCode = ErrorCode.None
                        }
                    ]
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await coordinator.FetchOffsetsAsync([new TopicPartition("test-topic", 0)], CancellationToken.None);

        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.Groups).IsNotNull();
        await Assert.That(capturedRequest.Groups!).Count().IsEqualTo(1);
        await Assert.That(capturedRequest.Groups![0].MemberId).IsEqualTo("member-42");
        await Assert.That(capturedRequest.Groups[0].MemberEpoch).IsEqualTo(7);
    }

    [Test]
    public async Task FetchOffsetsAsync_UnknownMemberAfterMaxPollExpiry_PreservesCommitFence()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, 9, 9);
        _metadataManager.SetApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            OffsetCommitRequest.HighestSupportedVersion);
        SetupSuccessfulConsumerProtocolJoin();

        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new OffsetFetchResponse
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
            }));

        var commitRequestCount = 0;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitRequestCount);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        SetCoordinatorLongField(
            coordinator,
            "_maxPollExpiredAtPollVersion",
            GetCoordinatorLongField(coordinator, "_pollVersion"));

        await Assert.That(async () =>
                await coordinator.FetchOffsetsAsync(
                    [new TopicPartition("test-topic", 0)],
                    CancellationToken.None))
            .Throws<GroupException>();

        var exception = await Assert.That(async () =>
                await coordinator.CommitOffsetsAsync(
                    [new TopicPartitionOffset("test-topic", 0, 1)],
                    CancellationToken.None))
            .Throws<GroupException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(commitRequestCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(ErrorCode.StaleMemberEpoch)]
    [Arguments(ErrorCode.UnknownMemberId)]
    [Arguments(ErrorCode.CoordinatorLoadInProgress)]
    [Arguments(ErrorCode.CoordinatorNotAvailable)]
    [Arguments(ErrorCode.NotCoordinator)]
    public async Task FetchOffsetsAsync_RetriableGroupError_RecoversAndRetries(ErrorCode errorCode)
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, 9, 9);
        SetupFindCoordinator();
        SetupConsumerGroupHeartbeat(heartbeatIntervalMs: 60_000);

        var requestCount = 0;
        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(Interlocked.Increment(ref requestCount) == 1
                ? new OffsetFetchResponse
                {
                    Groups =
                [
                    new OffsetFetchResponseGroup
                    {
                        GroupId = "test-group",
                        Topics = [],
                        ErrorCode = errorCode
                    }
                ]
                }
                : new OffsetFetchResponse
                {
                    Groups =
                    [
                        new OffsetFetchResponseGroup
                        {
                            GroupId = "test-group",
                            Topics = [],
                            ErrorCode = ErrorCode.None
                        }
                    ]
                }));

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);

        var result = await coordinator.FetchOffsetsAsync(
            [new TopicPartition("test-topic", 0)],
            CancellationToken.None);

        await Assert.That(result).IsEmpty();
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.MemberId).IsEqualTo("member-1");
        await Assert.That(requestCount).IsEqualTo(2);
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
            Arg.Is<ConsumerGroupHeartbeatRequest>(r => r != null && r.MemberEpoch == -1),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumerProtocol_StaticMemberLeaveGroup_SendsMemberEpochNegativeTwo()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(groupInstanceId: "static-instance-1");
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        _connection.ClearReceivedCalls();

        // Re-setup for the leave heartbeat
        SetupConsumerGroupHeartbeat();

        await coordinator.LeaveGroupAsync(cancellationToken: CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(r =>
                r != null && r.MemberEpoch == -2 && r.InstanceId == "static-instance-1"),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumerProtocol_EmptyStaticMemberLeaveGroup_SendsMemberEpochNegativeTwo()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(groupInstanceId: string.Empty);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        _connection.ClearReceivedCalls();
        SetupConsumerGroupHeartbeat();

        await coordinator.LeaveGroupAsync(cancellationToken: CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(r =>
                r != null && r.MemberEpoch == -2 && r.InstanceId == string.Empty),
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

    [Test]
    public async Task ConsumerProtocol_LeaveGroup_ObservesCancellationWhileWaitingForStateLock()
    {
        SetupSuccessfulConsumerProtocolJoin();
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(), _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        SetupConsumerGroupHeartbeat();
        var coordinatorLock = (SemaphoreSlim)typeof(ConsumerCoordinator).GetField(
            "_lock",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(coordinator)!;

        await coordinatorLock.WaitAsync(CancellationToken.None);
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.That(async () => await coordinator.LeaveGroupAsync(cancellation.Token))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            coordinatorLock.Release();
        }
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
            Arg.Is<ConsumerGroupHeartbeatRequest>(r => r != null && r.ServerAssignor == "uniform"),
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
                Volatile.Write(ref lastRequest, ci.Arg<ConsumerGroupHeartbeatRequest>()!);

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
                Volatile.Write(ref lastRequest, ci.Arg<ConsumerGroupHeartbeatRequest>()!);

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
    [Arguments(ErrorCode.GroupAuthorizationFailed)]
    [Arguments(ErrorCode.InvalidGroupId)]
    public async Task ConsumerProtocol_FatalGroupErrorDuringHeartbeat_PropagatesOnNextEnsureActiveGroup(
        ErrorCode errorCode)
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

                if (count == 1)
                {
                    return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                    {
                        ErrorCode = ErrorCode.None,
                        MemberId = "member-1",
                        MemberEpoch = 5,
                        HeartbeatIntervalMs = 60_000
                    });
                }

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = errorCode,
                    ErrorMessage = "fatal group error",
                    HeartbeatIntervalMs = 60000
                });
            });

        var options = CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);

        await coordinator.StopHeartbeatAsync();
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        await InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, CancellationToken.None);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);

        GroupException? caught = null;
        try
        {
            await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        }
        catch (GroupException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.ErrorCode).IsEqualTo(errorCode);
        await Assert.That(caught.GroupId).IsEqualTo("test-group");
        await Assert.That(callCount).IsEqualTo(2);
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
            Arg.Is<ConsumerGroupHeartbeatRequest>(r => r != null && r.InstanceId == "static-instance-1"),
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
            Arg.Is<ConsumerGroupHeartbeatRequest>(r => r != null && r.RackId == "rack-a"),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Server-side Regex Subscription Tests

    [Test]
    public async Task ConsumerProtocol_ServerSideRegex_SendsRegexInsteadOfTopicNames()
    {
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 1);
        SetupFindCoordinator();

        ConsumerGroupHeartbeatRequest? capturedRequest = null;
        short capturedVersion = -1;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedRequest = ci.Arg<ConsumerGroupHeartbeatRequest>()!;
                capturedVersion = ci.ArgAt<short>(1);
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

        await coordinator.EnsureActiveGroupAsync(new HashSet<string>(), "orders-.*", CancellationToken.None);

        await Assert.That(capturedVersion).IsEqualTo((short)1);
        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.SubscribedTopicNames).IsNotNull();
        await Assert.That(capturedRequest.SubscribedTopicNames!).IsEmpty();
        await Assert.That(capturedRequest.SubscribedTopicRegex).IsEqualTo("orders-.*");
    }

    [Test]
    public async Task ConsumerProtocol_SwitchFromRegexToTopics_ClearsRegex()
    {
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 1);
        SetupFindCoordinator();

        var requests = new List<ConsumerGroupHeartbeatRequest>();
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var request = ci.Arg<ConsumerGroupHeartbeatRequest>()!;
                requests.Add(request);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = request.MemberId,
                    MemberEpoch = request.MemberEpoch == 0 ? 1 : request.MemberEpoch,
                    HeartbeatIntervalMs = 60000
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string>(), "orders-.*", CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        requests.Clear();

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, null, CancellationToken.None);
        await Assert.That(requests).IsEmpty();

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(requests).Count().IsEqualTo(1);
        await Assert.That(requests[0].SubscribedTopicNames).IsNotNull();
        await Assert.That(requests[0].SubscribedTopicNames!).Contains("test-topic");
        await Assert.That(requests[0].SubscribedTopicRegex).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ConsumerProtocol_SwitchFromTopicsToRegex_ClearsTopics()
    {
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 1);
        SetupFindCoordinator();

        var requests = new List<ConsumerGroupHeartbeatRequest>();
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var request = ci.Arg<ConsumerGroupHeartbeatRequest>()!;
                requests.Add(request);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = request.MemberId,
                    MemberEpoch = request.MemberEpoch == 0 ? 1 : request.MemberEpoch,
                    HeartbeatIntervalMs = 60000
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        requests.Clear();

        await coordinator.EnsureActiveGroupAsync(new HashSet<string>(), "orders-.*", CancellationToken.None);
        await Assert.That(requests).IsEmpty();

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(requests).Count().IsEqualTo(1);
        await Assert.That(requests[0].SubscribedTopicNames).IsNotNull();
        await Assert.That(requests[0].SubscribedTopicNames!).IsEmpty();
        await Assert.That(requests[0].SubscribedTopicRegex).IsEqualTo("orders-.*");
    }

    [Test]
    public async Task ConsumerProtocol_ServerSideRegex_BrokerWithoutV1_ThrowsBrokerVersionException()
    {
        SetupFindCoordinator();
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string>(), "orders-.*", CancellationToken.None))
            .Throws<BrokerVersionException>()
            .WithMessageContaining("Kafka 4.1");
    }

    [Test]
    public async Task ConsumerProtocol_StableSubscription_RejectsRegexBeforeAcceptingItOnV0Broker()
    {
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 1);
        SetupFindCoordinator();
        SetupConsumerGroupHeartbeat(heartbeatIntervalMs: 60_000);

        var options = CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic" },
            CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        _connection.ClearReceivedCalls();
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0);

        await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(
                    new HashSet<string>(),
                    "orders-.*",
                    CancellationToken.None))
            .Throws<BrokerVersionException>()
            .WithMessageContaining("Kafka 4.1");

        await _connection.DidNotReceive().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Any<ConsumerGroupHeartbeatRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumerProtocol_StableRegex_CapabilityLossDuringHeartbeat_PropagatesToPoll()
    {
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 1);
        SetupFindCoordinator();
        SetupConsumerGroupHeartbeat(heartbeatIntervalMs: 60_000);

        var options = CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string>();

        await coordinator.EnsureActiveGroupAsync(topics, "orders-.*", CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0);
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);

        await InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(topics, "orders-.*", CancellationToken.None))
            .Throws<BrokerVersionException>()
            .WithMessageContaining("Kafka 4.1");
    }

    [Test]
    public async Task ConsumerProtocol_InvalidRegularExpression_ThrowsGroupException()
    {
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 1);
        SetupFindCoordinator();
        SetupConsumerGroupHeartbeat(errorCode: ErrorCode.InvalidRegularExpression);

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        GroupException? caught = null;
        try
        {
            await coordinator.EnsureActiveGroupAsync(new HashSet<string>(), "orders-(", CancellationToken.None);
        }
        catch (GroupException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.ErrorCode).IsEqualTo(ErrorCode.InvalidRegularExpression);
        await Assert.That(caught.Message).Contains("orders-(");
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
                capturedRequest = ci.Arg<ConsumerGroupHeartbeatRequest>()!;
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
                capturedRequest = ci.Arg<ConsumerGroupHeartbeatRequest>()!;
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
                var req = ci.Arg<ConsumerGroupHeartbeatRequest>()!;
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
                var req = ci.Arg<ConsumerGroupHeartbeatRequest>()!;
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
                capturedRequest = ci.Arg<ConsumerGroupHeartbeatRequest>()!;
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

    [Test]
    public async Task JoinRetryDelay_DoesNotExceedRemainingRebalanceTimeout()
    {
        var delay = ConsumerCoordinator.GetJoinRetryDelay(
            retryDelayMs: 10_000,
            elapsed: TimeSpan.FromMilliseconds(900),
            rebalanceTimeout: TimeSpan.FromSeconds(1));

        await Assert.That(delay).IsEqualTo(TimeSpan.FromMilliseconds(100));
    }

    [Test]
    public async Task JoinRetryDelay_WhenDeadlinePassed_ReturnsZero()
    {
        var delay = ConsumerCoordinator.GetJoinRetryDelay(
            retryDelayMs: 10_000,
            elapsed: TimeSpan.FromSeconds(1),
            rebalanceTimeout: TimeSpan.FromSeconds(1));

        await Assert.That(delay).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task IsAssignmentSyncCurrent_PendingFatalHeartbeat_ReturnsFalse()
    {
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        const int assignmentVersion = 7;
        SetPrivateField(coordinator, "_state", CoordinatorState.Stable);
        SetPrivateField(coordinator, "_assignmentVersion", assignmentVersion);

        await Assert.That(coordinator.IsAssignmentSyncCurrent(assignmentVersion)).IsTrue();

        SetPrivateField(
            coordinator,
            "_fatalHeartbeatException",
            new GroupException(ErrorCode.GroupAuthorizationFailed, "fatal heartbeat"));

        await Assert.That(coordinator.IsAssignmentSyncCurrent(assignmentVersion)).IsFalse();
    }

    [Test]
    public async Task TryRecordPollFast_PendingFatalHeartbeat_ThrowsBeforeLockPath()
    {
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await Assert.That(coordinator.TryRecordPollFast()).IsTrue();

        SetPrivateField(
            coordinator,
            "_fatalHeartbeatException",
            new GroupException(ErrorCode.GroupAuthorizationFailed, "fatal heartbeat"));

        await Assert.That(() => coordinator.TryRecordPollFast())
            .Throws<GroupException>();
    }

    [Test]
    public async Task IsAssignmentSyncCurrent_HeartbeatAssignmentProcessing_ReturnsFalse()
    {
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        const int assignmentVersion = 7;
        SetPrivateField(coordinator, "_state", CoordinatorState.Stable);
        SetPrivateField(coordinator, "_assignmentVersion", assignmentVersion);

        await Assert.That(coordinator.IsAssignmentSyncCurrent(assignmentVersion)).IsTrue();

        SetPrivateField(coordinator, "_assignmentProcessingCount", 1);

        await Assert.That(coordinator.IsAssignmentSyncCurrent(assignmentVersion)).IsFalse();
    }

    #endregion

    private static void SetPrivateField<T>(ConsumerCoordinator coordinator, string fieldName, T value)
    {
        var field = typeof(ConsumerCoordinator).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{fieldName} field not found.");
        field.SetValue(coordinator, value);
    }

    private sealed class RetirableTestConnection(IKafkaConnection inner) :
        IKafkaConnection,
        IRetirableKafkaConnection
    {
        private int _leaseCount;

        public int BrokerId => inner.BrokerId;
        public string Host => inner.Host;
        public int Port => inner.Port;
        public bool IsConnected => inner.IsConnected;
        public int LeaseCount => Volatile.Read(ref _leaseCount);
        public int ActiveOperationCount => 0;

        public bool TryAcquireLease() => Interlocked.CompareExchange(ref _leaseCount, 1, 0) == 0;

        public void ReleaseLease() => Interlocked.Decrement(ref _leaseCount);

        public void BeginRetirement()
        {
        }

        public void CompleteRetirement()
        {
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendFireAndForgetAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendPipelinedAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
                request,
                apiVersion,
                cancellationToken);

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => inner.SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
                request,
                apiVersion,
                cancellationToken);

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
            => inner.ConnectAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
