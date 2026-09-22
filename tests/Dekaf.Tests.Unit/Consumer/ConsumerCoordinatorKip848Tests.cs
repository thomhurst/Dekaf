using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;
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

    [Test]
    public async Task TelemetryMemberId_OnlyReportsJoinedCurrentIdentity()
    {
        await using var coordinator = new ConsumerCoordinator(
            new ConsumerOptions { BootstrapServers = ["localhost:9092"], GroupId = "telemetry" },
            _connectionPool, _metadataManager);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(ConsumerCoordinator);
        type.GetField("_memberId", flags)!.SetValue(coordinator, "old-member");
        type.GetField("_generationId", flags)!.SetValue(coordinator, 1);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsNull();
        type.GetField("_state", flags)!.SetValue(coordinator, CoordinatorState.Stable);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsEqualTo("old-member");
        type.GetField("_generationId", flags)!.SetValue(coordinator, -2);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsNull();
        type.GetField("_memberId", flags)!.SetValue(coordinator, "new-member");
        type.GetField("_generationId", flags)!.SetValue(coordinator, 2);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsEqualTo("new-member");
        await coordinator.DisposeAsync();
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsNull();
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
        int maxPollIntervalMs = 300000,
        int retryBackoffMs = 100,
        int retryBackoffMaxMs = 1000,
        IConsumerAwareRebalanceListener? consumerAwareRebalanceListener = null,
        IRebalanceListener[]? additionalRebalanceListeners = null,
        int sessionTimeoutMs = 45000,
        int defaultApiTimeoutMs = 60000,
        int requestTimeoutMs = 30000) => new()
        {
            SessionTimeoutMs = sessionTimeoutMs,
            DefaultApiTimeoutMs = defaultApiTimeoutMs,
            RequestTimeoutMs = requestTimeoutMs,
            BootstrapServers = ["localhost:9092"],
            GroupId = groupId,
            GroupRemoteAssignor = groupRemoteAssignor,
            GroupInstanceId = groupInstanceId,
            ClientRack = clientRack,
            RebalanceListener = rebalanceListener,
            ConsumerAwareRebalanceListener = consumerAwareRebalanceListener,
            AdditionalRebalanceListeners = additionalRebalanceListeners,
            HeartbeatIntervalMs = heartbeatIntervalMs,
            RebalanceTimeoutMs = rebalanceTimeoutMs,
            MaxPollIntervalMs = maxPollIntervalMs,
            RetryBackoffMs = retryBackoffMs,
            RetryBackoffMaxMs = retryBackoffMaxMs
        };

    [Test]
    public async Task FindCoordinator_GroupAuthorizationFailureIsTypedAndNeverRetried()
    {
        _connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators = [new Coordinator
                {
                    Key = "test-group", NodeId = -1, Host = string.Empty, Port = -1,
                    ErrorCode = ErrorCode.GroupAuthorizationFailed
                }]
            }));
        await using var coordinator = new ConsumerCoordinator(CreateConsumerProtocolOptions(), _connectionPool, _metadataManager);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var exception = await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, timeout.Token))
            .Throws<AuthorizationException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await _connection.Received(1).SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

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

        var coordinatorId = GetPrivateField<int>(coordinator, "_coordinatorId");
        var result = method!.Invoke(coordinator, [coordinatorId, false, true, CancellationToken.None])!;
        var task = (Task)result.GetType().GetMethod("AsTask")!.Invoke(result, null)!;
        await task;
    }

    private static ValueTask InvokePartitionsLostAsync(
        ConsumerCoordinator coordinator,
        IReadOnlyList<TopicPartition> partitions)
    {
        var method = typeof(ConsumerCoordinator).GetMethod(
            "InvokePartitionsLostAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("InvokePartitionsLostAsync method not found.");

        return (ValueTask)(method.Invoke(coordinator, [partitions])
            ?? throw new InvalidOperationException("InvokePartitionsLostAsync returned null."));
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

    private static ValueTask InvokeFindCoordinatorAsync(
        ConsumerCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var method = typeof(ConsumerCoordinator).GetMethod(
            "FindCoordinatorAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        return (ValueTask)method!.Invoke(coordinator, [cancellationToken])!;
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

    private static ConsumerGroupHeartbeatAssignment CreateAssignmentWithNewPartitions(
        Guid topicId,
        IReadOnlyList<int> partitions,
        IReadOnlyList<int> newPartitions) => new()
    {
        AssignedTopicPartitions =
        [
            new ConsumerGroupHeartbeatTopicPartitions
            {
                TopicId = topicId,
                Partitions = partitions,
                NewPartitions = newPartitions
            }
        ],
        PendingTopicPartitions = []
    };

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
        await Assert.That(coordinator.CaptureGroupStatus().LastHeartbeatFailure)
            .Contains("Kafka 4.0");

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
    public async Task ConsumerProtocol_BrokerSupportsV2_UsesV2()
    {
        _metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 2);
        SetupSuccessfulConsumerProtocolJoin();
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(), _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Any<ConsumerGroupHeartbeatRequest>(),
            Arg.Is<short>(version => version == 2),
            Arg.Any<CancellationToken>());
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
    public async Task ConsumerProtocol_InitialJoin_SendsConfiguredRebalanceTimeout()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(
            rebalanceTimeoutMs: 30_000,
            maxPollIntervalMs: 12_345);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(request => request != null && request.RebalanceTimeoutMs == 30_000),
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
        // Isolate the explicit heartbeat below from calls completed before the background loop stopped.
        _connection.ClearReceivedCalls();

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
            heartbeatIntervalMs: 60_000,
            maxPollIntervalMs: 300_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken);
        await coordinator.StopHeartbeatAsync();
        SetCoordinatorLongField(
            coordinator,
            "_lastPollTimestamp",
            Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 600L));

        await InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, cancellationToken);
        await Assert.That(leaveAttempted.Task.IsCompleted).IsTrue();

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
    public async Task CommitOffsetsAsync_StaleMemberEpoch_RetriesWithRefreshedEpoch()
    {
        SetupFindCoordinator();
        // Long heartbeat interval keeps the background loop quiet so the epoch transition
        // below is driven solely by the commit stub.
        SetupConsumerGroupHeartbeat(memberEpoch: 7, heartbeatIntervalMs: 60_000);
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 9, 9);

        var options = CreateConsumerProtocolOptions(retryBackoffMs: 0, retryBackoffMaxMs: 0);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        var capturedEpochs = new List<int>();
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<OffsetCommitRequest>()!;
                capturedEpochs.Add(request.GenerationIdOrMemberEpoch);
                if (capturedEpochs.Count == 1)
                {
                    // The coordinator bumped the member epoch (reassignment) after this
                    // request was built; the client learns the new epoch from a heartbeat
                    // while the commit retry waits for the refresh.
                    SetPrivateField(coordinator, "_generationId", 8);
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
                }

                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 0, 1)],
            CancellationToken.None);

        await Assert.That(capturedEpochs).IsEquivalentTo([7, 8]);
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

        var (_, assignmentVersion, _, _) = await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(
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

        var (_, assignmentVersion, _, _) = await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(
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
    public async Task ConsumerProtocol_Assignment_PublishesNewPartitionClassification()
    {
        var assignment = CreateAssignmentWithNewPartitions(TestTopicId, [0, 1], [1]);
        SetupSuccessfulConsumerProtocolJoin(assignment: assignment);
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        var (_, _, _, newlyExpandedPartitions) =
            await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);

        await Assert.That(newlyExpandedPartitions).IsEquivalentTo(
            [new TopicPartition("test-topic", 1)]);
    }

    [Test]
    public async Task ConsumerProtocol_NewPartitionClassification_PersistsUntilInitializationAcknowledged()
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
                    Assignment = count == 1
                        ? CreateAssignmentWithNewPartitions(TestTopicId, [0, 1], [1])
                        : CreateAssignment(TestTopicId, 0, 1)
                });
            });
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000),
            _connectionPool,
            _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        var (_, firstVersion, _, _) = await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(
            CancellationToken.None);
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        var (_, secondVersion, _, classifications) =
            await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);

        var expandedPartition = new TopicPartition("test-topic", 1);
        await Assert.That(secondVersion).IsEqualTo(firstVersion);
        await Assert.That(classifications).IsEquivalentTo([expandedPartition]);

        coordinator.AcknowledgeInitializedPartitions([expandedPartition], secondVersion);
        var (_, acknowledgedVersion, _, acknowledgedClassifications) =
            await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);

        await Assert.That(acknowledgedVersion).IsEqualTo(firstVersion);
        await Assert.That(acknowledgedClassifications).IsEmpty();
    }

    [Test]
    public async Task ConsumerProtocol_StaleInitializationCannotAcknowledgeNewerClassification()
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
                    Assignment = count == 1
                        ? CreateAssignmentWithNewPartitions(TestTopicId, [0, 1], [1])
                        : CreateAssignmentWithNewPartitions(TestTopicId, [0, 1], [0, 1])
                });
            });
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000),
            _connectionPool,
            _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        var (_, staleVersion, _, _) = await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(
            CancellationToken.None);
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        var (_, currentVersion, _, currentClassifications) =
            await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);

        var partition = new TopicPartition("test-topic", 1);
        coordinator.AcknowledgeInitializedPartitions([partition], staleVersion);
        var (_, _, _, afterStaleAcknowledgement) =
            await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);

        await Assert.That(currentVersion).IsGreaterThan(staleVersion);
        await Assert.That(currentClassifications).IsEquivalentTo(
            [new TopicPartition("test-topic", 0), partition]);
        await Assert.That(afterStaleAcknowledgement).IsEquivalentTo(currentClassifications);

        coordinator.AcknowledgeInitializedPartitions([partition], currentVersion);
        var (_, _, _, afterCurrentAcknowledgement) =
            await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);

        await Assert.That(afterCurrentAcknowledgement).IsEquivalentTo(
            [new TopicPartition("test-topic", 0)]);
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
    public async Task ConsumerProtocol_Assignment_InvokesAdditiveListenersInOrderAndIsolatesFailure()
    {
        var assignment = CreateAssignment(TestTopicId, 0);
        SetupSuccessfulConsumerProtocolJoin(assignment: assignment);
        var calls = new List<string>();
        var configured = CreateListener("configured");
        var failing = CreateListener("failing", fail: true);
        var additional = CreateListener("additional");
        var runtime = CreateListener("runtime");
        var options = CreateConsumerProtocolOptions(
            rebalanceListener: configured,
            additionalRebalanceListeners: [failing, additional]);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        using var runtimeRegistration = coordinator.RegisterRuntimeRebalanceListener(runtime);

        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic" },
            CancellationToken.None);

        await Assert.That(calls).Count().IsEqualTo(4);
        await Assert.That(calls[0]).IsEqualTo("configured");
        await Assert.That(calls[1]).IsEqualTo("failing");
        await Assert.That(calls[2]).IsEqualTo("additional");
        await Assert.That(calls[3]).IsEqualTo("runtime");

        IRebalanceListener CreateListener(string name, bool fail = false)
        {
            var listener = Substitute.For<IRebalanceListener>();
            listener.OnPartitionsAssignedAsync(
                    Arg.Any<IEnumerable<TopicPartition>>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    calls.Add(name);
                    return fail
                        ? ValueTask.FromException(new InvalidOperationException("listener failed"))
                        : ValueTask.CompletedTask;
                });
            return listener;
        }
    }

    [Test]
    public async Task ConsumerProtocol_Revocation_InvokesAllAdditiveListenersInOrder()
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
                    Assignment = count == 1
                        ? CreateAssignment(TestTopicId, 0, 1)
                        : CreateAssignment(TestTopicId, 1)
                });
            });
        var calls = new List<string>();
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            rebalanceListener: CreateListener("configured"),
            additionalRebalanceListeners:
            [
                CreateListener("first"),
                CreateListener("second")
            ]);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic" },
            CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(calls).Count().IsEqualTo(3);
        await Assert.That(calls[0]).IsEqualTo("configured");
        await Assert.That(calls[1]).IsEqualTo("first");
        await Assert.That(calls[2]).IsEqualTo("second");

        IRebalanceListener CreateListener(string name)
        {
            var listener = Substitute.For<IRebalanceListener>();
            listener.OnPartitionsRevokedAsync(
                    Arg.Any<IEnumerable<TopicPartition>>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    calls.Add(name);
                    return ValueTask.CompletedTask;
                });
            return listener;
        }
    }

    [Test]
    public async Task ConsumerProtocol_Lost_InvokesAllAdditiveListenersInOrder()
    {
        var calls = new List<string>();
        var options = CreateConsumerProtocolOptions(
            rebalanceListener: CreateListener("configured"),
            additionalRebalanceListeners:
            [
                CreateListener("first"),
                CreateListener("second")
            ]);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await InvokePartitionsLostAsync(
            coordinator,
            [new TopicPartition("test-topic", 0)]);

        await Assert.That(calls).Count().IsEqualTo(3);
        await Assert.That(calls[0]).IsEqualTo("configured");
        await Assert.That(calls[1]).IsEqualTo("first");
        await Assert.That(calls[2]).IsEqualTo("second");

        IRebalanceListener CreateListener(string name)
        {
            var listener = Substitute.For<IRebalanceListener>();
            listener.OnPartitionsLostAsync(
                    Arg.Any<IEnumerable<TopicPartition>>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    calls.Add(name);
                    return ValueTask.CompletedTask;
                });
            return listener;
        }
    }

    [Test]
    public async Task ConsumerProtocol_ConsumerAwareAssignment_UsesAndInvalidatesScopedView()
    {
        var assignment = CreateAssignment(TestTopicId, 0, 1);
        SetupSuccessfulConsumerProtocolJoin(assignment: assignment);
        var listener = Substitute.For<IConsumerAwareRebalanceListener>();
        IRebalanceConsumer? capturedView = null;
        listener.OnPartitionsAssignedAsync(
                Arg.Any<IRebalanceConsumer>(),
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedView = callInfo.Arg<IRebalanceConsumer>();
                return ValueTask.CompletedTask;
            });
        var consumer = Substitute.For<IKafkaConsumer<byte[], byte[]>>();
        consumer.Positions.Returns(Substitute.For<IConsumerPositions>());
        RebalanceConsumerScope<byte[], byte[]>? scope = null;
        TopicPartition[]? callbackAssignment = null;
        TopicPartition[]? newlyAssigned = null;
        var options = CreateConsumerProtocolOptions(
            consumerAwareRebalanceListener: listener);
        await using var coordinator = new ConsumerCoordinator(
            options,
            _connectionPool,
            _metadataManager,
            logger: null,
            getConnectionCount: null,
            onPartitionsRevoked: null,
            onPartitionsRevoking: null,
            onPartitionsRevokedAsync: null,
            createRebalanceConsumerScope: (current, added) =>
            {
                callbackAssignment = current.ToArray();
                newlyAssigned = added.ToArray();
                return scope = new RebalanceConsumerScope<byte[], byte[]>(
                    consumer,
                    current,
                    added);
            });

        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic" },
            CancellationToken.None);

        await Assert.That(capturedView).IsSameReferenceAs(scope);
        await Assert.That(callbackAssignment).Count().IsEqualTo(2);
        await Assert.That(newlyAssigned).Count().IsEqualTo(2);
        Assert.Throws<InvalidOperationException>(() => _ = scope!.Assignment);
    }

    [Test]
    public async Task ConsumerProtocol_ConsumerAwareFailure_StillInvalidatesScopedView()
    {
        var assignment = CreateAssignment(TestTopicId, 0);
        SetupSuccessfulConsumerProtocolJoin(assignment: assignment);
        var listener = Substitute.For<IConsumerAwareRebalanceListener>();
        listener.OnPartitionsAssignedAsync(
                Arg.Any<IRebalanceConsumer>(),
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new InvalidOperationException("callback failed")));
        var consumer = Substitute.For<IKafkaConsumer<byte[], byte[]>>();
        consumer.Positions.Returns(Substitute.For<IConsumerPositions>());
        RebalanceConsumerScope<byte[], byte[]>? scope = null;
        var options = CreateConsumerProtocolOptions(
            consumerAwareRebalanceListener: listener);
        await using var coordinator = new ConsumerCoordinator(
            options,
            _connectionPool,
            _metadataManager,
            logger: null,
            getConnectionCount: null,
            onPartitionsRevoked: null,
            onPartitionsRevoking: null,
            onPartitionsRevokedAsync: null,
            createRebalanceConsumerScope: (current, added) =>
                scope = new RebalanceConsumerScope<byte[], byte[]>(consumer, current, added));

        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic" },
            CancellationToken.None);

        Assert.Throws<InvalidOperationException>(() => _ = scope!.Assignment);
    }

    [Test]
    public async Task ConsumerProtocol_ConsumerAwareRevocation_ReceivesScopedView()
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
                    Assignment = count == 1
                        ? CreateAssignment(TestTopicId, 0, 1)
                        : CreateAssignment(TestTopicId, 1)
                });
            });
        IRebalanceConsumer? revokedView = null;
        TopicPartition[]? revokedPartitions = null;
        var listener = Substitute.For<IConsumerAwareRebalanceListener>();
        listener.OnPartitionsRevokedAsync(
                Arg.Any<IRebalanceConsumer>(),
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                revokedView = callInfo.Arg<IRebalanceConsumer>();
                revokedPartitions = callInfo.Arg<IEnumerable<TopicPartition>>()!.ToArray();
                return ValueTask.CompletedTask;
            });
        var consumer = Substitute.For<IKafkaConsumer<byte[], byte[]>>();
        consumer.Positions.Returns(Substitute.For<IConsumerPositions>());
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            consumerAwareRebalanceListener: listener);
        await using var coordinator = new ConsumerCoordinator(
            options,
            _connectionPool,
            _metadataManager,
            logger: null,
            getConnectionCount: null,
            onPartitionsRevoked: null,
            onPartitionsRevoking: null,
            onPartitionsRevokedAsync: null,
            createRebalanceConsumerScope: (current, added) =>
                new RebalanceConsumerScope<byte[], byte[]>(consumer, current, added));

        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic" },
            CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(revokedPartitions).IsEquivalentTo(
            [new TopicPartition("test-topic", 0)]);
        Assert.Throws<InvalidOperationException>(() => _ = revokedView!.Assignment);
    }

    [Test]
    public async Task ConsumerProtocol_ConsumerAwareLostCancellation_InvalidatesScopedView()
    {
        IRebalanceConsumer? lostView = null;
        var listener = Substitute.For<IConsumerAwareRebalanceListener>();
        listener.OnPartitionsLostAsync(
                Arg.Any<IRebalanceConsumer>(),
                Arg.Any<IEnumerable<TopicPartition>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                lostView = callInfo.Arg<IRebalanceConsumer>();
                return ValueTask.FromException(new OperationCanceledException("cancelled"));
            });
        var consumer = Substitute.For<IKafkaConsumer<byte[], byte[]>>();
        consumer.Positions.Returns(Substitute.For<IConsumerPositions>());
        var options = CreateConsumerProtocolOptions(
            consumerAwareRebalanceListener: listener);
        await using var coordinator = new ConsumerCoordinator(
            options,
            _connectionPool,
            _metadataManager,
            logger: null,
            getConnectionCount: null,
            onPartitionsRevoked: null,
            onPartitionsRevoking: null,
            onPartitionsRevokedAsync: null,
            createRebalanceConsumerScope: (current, added) =>
                new RebalanceConsumerScope<byte[], byte[]>(consumer, current, added));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await InvokePartitionsLostAsync(
                coordinator,
                [new TopicPartition("test-topic", 0)]));

        Assert.Throws<InvalidOperationException>(() => _ = lostView!.Assignment);
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
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 9, 9);

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
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, 9, 9);
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
    public async Task CommitOffsetsAsync_V10_UsesTopicIdFromRequestSnapshot()
    {
        SetupFindCoordinator();
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 10, 10);

        OffsetCommitRequest? capturedRequest = null;
        short capturedVersion = -1;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.ArgAt<OffsetCommitRequest>(0);
                capturedVersion = callInfo.ArgAt<short>(1);
                return ValueTask.FromResult(new OffsetCommitResponse
                {
                    Topics =
                    [
                        new OffsetCommitResponseTopic
                        {
                            TopicId = TestTopicId,
                            Partitions =
                            [
                                new OffsetCommitResponsePartition
                                {
                                    PartitionIndex = 0,
                                    ErrorCode = ErrorCode.None
                                }
                            ]
                        }
                    ]
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 0, 42)],
            CancellationToken.None);

        await Assert.That(capturedVersion).IsEqualTo((short)10);
        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.Topics[0].TopicId).IsEqualTo(TestTopicId);
    }

    [Test]
    public async Task CommitOffsetsAsync_V10_MissingTopicId_RefreshesBeforeSend()
    {
        SetupFindCoordinator();
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 10, 10);
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
                    TopicId = Guid.Empty,
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
                        }
                    ]
                }
            ]
        });

        _connectionPool.GetConnectionAsync(
                "localhost",
                9092,
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_connection));
        _connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            }));

        var metadataRequestCount = 0;
        _connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref metadataRequestCount);
                return ValueTask.FromResult(new MetadataResponse
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
                                }
                            ]
                        }
                    ]
                });
            });

        OffsetCommitRequest? capturedRequest = null;
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.ArgAt<OffsetCommitRequest>(0);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 0, 42)],
            CancellationToken.None);

        await Assert.That(metadataRequestCount).IsEqualTo(1);
        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.Topics[0].TopicId).IsEqualTo(TestTopicId);
    }

    [Test]
    public async Task FetchOffsetsAsync_V10_MapsResponseTopicIdToName()
    {
        _metadataManager.SetApiVersion(ApiKey.OffsetFetch, 10, 10);
        SetupSuccessfulConsumerProtocolJoin();

        OffsetFetchRequest? capturedRequest = null;
        short capturedVersion = -1;
        _connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.ArgAt<OffsetFetchRequest>(0);
                capturedVersion = callInfo.ArgAt<short>(1);
                return ValueTask.FromResult(new OffsetFetchResponse
                {
                    Groups =
                    [
                        new OffsetFetchResponseGroup
                        {
                            GroupId = "test-group",
                            ErrorCode = ErrorCode.None,
                            Topics =
                            [
                                new OffsetFetchResponseTopic
                                {
                                    TopicId = TestTopicId,
                                    Partitions =
                                    [
                                        new OffsetFetchResponsePartition
                                        {
                                            PartitionIndex = 0,
                                            CommittedOffset = 42,
                                            CommittedLeaderEpoch = 3,
                                            ErrorCode = ErrorCode.None
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                });
            });

        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        var result = await coordinator.FetchOffsetsAsync(
            [new TopicPartition("test-topic", 0)],
            CancellationToken.None);

        await Assert.That(capturedVersion).IsEqualTo((short)10);
        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.Groups![0].Topics![0].TopicId).IsEqualTo(TestTopicId);
        await Assert.That(result[new TopicPartition("test-topic", 0)].Offset).IsEqualTo(42);
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

        var options = CreateConsumerProtocolOptions(
            retryBackoffMs: 0,
            retryBackoffMaxMs: 0);
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
    public async Task ConsumerProtocol_StaticMemberPermanentLeave_SendsMemberEpochNegativeOne()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(groupInstanceId: "static-instance-1");
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        _connection.ClearReceivedCalls();
        SetupConsumerGroupHeartbeat();

        await coordinator.LeaveGroupAsync(
            ConsumerGroupMembershipOperation.LeaveGroup,
            CancellationToken.None);

        await _connection.Received().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Is<ConsumerGroupHeartbeatRequest>(r =>
                r != null && r.MemberEpoch == -1 && r.InstanceId == "static-instance-1"),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumerProtocol_RemainInGroup_SendsNoTerminalHeartbeat()
    {
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions();
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        _connection.ClearReceivedCalls();

        await coordinator.LeaveGroupAsync(
            ConsumerGroupMembershipOperation.RemainInGroup,
            CancellationToken.None);

        await _connection.DidNotReceive().SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
            Arg.Any<ConsumerGroupHeartbeatRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
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

    private static IRebalanceListener CreateRecordingRebalanceListener(List<string> events)
    {
        var listener = Substitute.For<IRebalanceListener>();
        listener.OnPartitionsAssignedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => RecordRebalanceEvent(events, "assigned", callInfo.Arg<IEnumerable<TopicPartition>>()));
        listener.OnPartitionsRevokedAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => RecordRebalanceEvent(events, "revoked", callInfo.Arg<IEnumerable<TopicPartition>>()));
        listener.OnPartitionsLostAsync(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => RecordRebalanceEvent(events, "lost", callInfo.Arg<IEnumerable<TopicPartition>>()));
        return listener;
    }

    private static ValueTask RecordRebalanceEvent(
        List<string> events,
        string callback,
        IEnumerable<TopicPartition> partitions)
    {
        var text = string.Join(",", partitions.Select(p => p.Partition).Order());
        lock (events)
            events.Add($"{callback}:{text}");
        return ValueTask.CompletedTask;
    }

    private static string SnapshotRebalanceEvents(List<string> events)
    {
        lock (events)
            return string.Join("|", events);
    }

    private static ConsumerGroupHeartbeatResponse HeartbeatResponse(
        int memberEpoch,
        ConsumerGroupHeartbeatAssignment? assignment = null,
        ErrorCode errorCode = ErrorCode.None) => new()
    {
        ErrorCode = errorCode,
        ErrorMessage = errorCode == ErrorCode.None ? null : errorCode.ToString(),
        MemberId = errorCode == ErrorCode.None ? "member-1" : null,
        MemberEpoch = memberEpoch,
        HeartbeatIntervalMs = 60_000,
        Assignment = assignment
    };

    private void SetupHeartbeatResponses(Func<ConsumerGroupHeartbeatRequest, ValueTask<ConsumerGroupHeartbeatResponse>> respond)
    {
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => respond(callInfo.Arg<ConsumerGroupHeartbeatRequest>()!));
    }

    [Test]
    [Arguments(ErrorCode.UnknownMemberId)]
    [Arguments(ErrorCode.FencedMemberEpoch)]
    public async Task ConsumerProtocol_SessionLostThenRejoinRejected_ReportsPartitionsLostBeforeAssigned(
        ErrorCode rejoinError)
    {
        // The heartbeat loop cannot reach the coordinator for a whole session timeout and hands
        // the member back to the foreground. The broker has expired it meanwhile, so the rejoin
        // is rejected: every partition the member owned was lost, and must be reported as lost
        // before the fresh assignment is reported.
        SetupFindCoordinator();
        SetupHeartbeatResponses(_ => ValueTask.FromResult(
            HeartbeatResponse(5, CreateAssignment(TestTopicId, 0, 1))));
        var events = new List<string>();
        var options = CreateConsumerProtocolOptions(
            rebalanceListener: CreateRecordingRebalanceListener(events),
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1,
            sessionTimeoutMs: 100);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        SetupHeartbeatResponses(_ => ValueTask.FromException<ConsumerGroupHeartbeatResponse>(
            new IOException("coordinator connection closed")));
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        await InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, CancellationToken.None);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);

        var rejoinAttempts = 0;
        SetupHeartbeatResponses(request =>
        {
            if (request.MemberEpoch == -1)
                return ValueTask.FromResult(HeartbeatResponse(-1));

            return ValueTask.FromResult(Interlocked.Increment(ref rejoinAttempts) == 1
                ? HeartbeatResponse(0, errorCode: rejoinError)
                : HeartbeatResponse(7, CreateAssignment(TestTopicId, 1)));
        });

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);

        await Assert.That(SnapshotRebalanceEvents(events))
            .IsEqualTo("assigned:0,1|lost:0,1|assigned:1");
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(1);
        await Assert.That(coordinator.Assignment.Contains(new TopicPartition("test-topic", 1))).IsTrue();
    }

    [Test]
    [Arguments(ErrorCode.UnknownMemberId)]
    [Arguments(ErrorCode.FencedMemberEpoch)]
    public async Task ConsumerProtocol_MembershipLostDuringHeartbeat_ReportsPartitionsLostAndClearsAssignment(
        ErrorCode heartbeatError)
    {
        SetupFindCoordinator();
        SetupHeartbeatResponses(_ => ValueTask.FromResult(
            HeartbeatResponse(5, CreateAssignment(TestTopicId, 0, 1))));
        var events = new List<string>();
        var options = CreateConsumerProtocolOptions(
            rebalanceListener: CreateRecordingRebalanceListener(events),
            heartbeatIntervalMs: 60_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        SetupHeartbeatResponses(_ => ValueTask.FromResult(HeartbeatResponse(0, errorCode: heartbeatError)));
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        await InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
        await Assert.That(SnapshotRebalanceEvents(events))
            .IsEqualTo("assigned:0,1|lost:0,1");

        ConsumerGroupHeartbeatRequest? rejoinRequest = null;
        SetupHeartbeatResponses(request =>
        {
            if (request.MemberEpoch == -1)
                return ValueTask.FromResult(HeartbeatResponse(-1));

            Volatile.Write(ref rejoinRequest, request);
            return ValueTask.FromResult(HeartbeatResponse(6, CreateAssignment(TestTopicId, 1)));
        });

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);

        // The fresh membership owns nothing it was not given: p1 is newly assigned and p0 is
        // not "revoked" a second time.
        await Assert.That(Volatile.Read(ref rejoinRequest)!.MemberEpoch).IsEqualTo(0);
        await Assert.That(SnapshotRebalanceEvents(events))
            .IsEqualTo("assigned:0,1|lost:0,1|assigned:1");
    }

    [Test]
    public async Task CommitOffsetsAsync_AfterHeartbeatFence_FailsFastUntilAssignmentIsResynchronized()
    {
        SetupFindCoordinator();
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 9, 9);
        SetupHeartbeatResponses(_ => ValueTask.FromResult(
            HeartbeatResponse(5, CreateAssignment(TestTopicId, 0, 1))));
        var commitEpochs = new List<int>();
        _connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                lock (commitEpochs)
                    commitEpochs.Add(callInfo.Arg<OffsetCommitRequest>()!.GenerationIdOrMemberEpoch);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });
        var options = CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        SetupHeartbeatResponses(_ => ValueTask.FromResult(
            HeartbeatResponse(0, errorCode: ErrorCode.FencedMemberEpoch)));
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        await InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, CancellationToken.None);

        // Offsets consumed under the lost membership must not be committed: another member may
        // own the partition and have committed past them.
        var fenced = await Assert.That(async () => await coordinator.CommitOffsetsAsync(
                [new TopicPartitionOffset("test-topic", 0, 10)],
                retryUntilApiTimeout: true,
                CancellationToken.None))
            .Throws<GroupException>();
        await Assert.That(fenced!.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
        await Assert.That(fenced.IsRetriable).IsFalse();

        // Rejoining alone does not lift the fence: the consumer has not yet dropped the
        // offsets it stored for the lost partitions.
        SetupHeartbeatResponses(request => ValueTask.FromResult(request.MemberEpoch == -1
            ? HeartbeatResponse(-1)
            : HeartbeatResponse(6, CreateAssignment(TestTopicId, 1))));
        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await Assert.That(async () => await coordinator.CommitOffsetsAsync(
                [new TopicPartitionOffset("test-topic", 1, 10)],
                retryUntilApiTimeout: true,
                CancellationToken.None))
            .Throws<GroupException>();

        var sync = await coordinator.GetAssignmentSnapshotAndDrainRevocationsAsync(CancellationToken.None);
        await Assert.That(sync.Revocations).IsNotNull();
        coordinator.AcknowledgeAssignmentSync(sync.Version);

        await coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 1, 10)],
            retryUntilApiTimeout: true,
            CancellationToken.None);
        await Assert.That(commitEpochs).IsEquivalentTo([6]);
    }

    [Test]
    public async Task CommitOffsetsAsync_StaleMemberEpochAfterHeartbeatStopped_FailsFastWithoutRetrying()
    {
        // The heartbeat loop gave up after a session timeout without transport, so no refreshed
        // epoch will ever arrive. Waiting for one and retrying until the API timeout only
        // delays a failure the application has to handle by polling to rejoin.
        SetupFindCoordinator();
        _metadataManager.SetApiVersion(ApiKey.OffsetCommit, 9, 9);
        SetupHeartbeatResponses(_ => ValueTask.FromResult(HeartbeatResponse(5)));
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
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1,
            sessionTimeoutMs: 100,
            defaultApiTimeoutMs: 5_000);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        SetupHeartbeatResponses(_ => ValueTask.FromException<ConsumerGroupHeartbeatResponse>(
            new IOException("coordinator connection closed")));
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);
        await InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, CancellationToken.None);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);

        var exception = await Assert.That(async () => await coordinator.CommitOffsetsAsync(
                [new TopicPartitionOffset("test-topic", 0, 10)],
                retryUntilApiTimeout: true,
                CancellationToken.None))
            .Throws<GroupException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.StaleMemberEpoch);
        await Assert.That(exception.IsRetriable).IsFalse();
        await Assert.That(commitRequestCount).IsEqualTo(1);
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
    public async Task ConsumerProtocol_TransportFailureDuringHeartbeat_RediscoversAndKeepsBeating(
        CancellationToken cancellationToken)
    {
        // The coordinator connection drops once. Nothing polls (the application is busy in a
        // handler), so only the heartbeat loop can keep the membership alive: it must re-discover
        // the coordinator and send the next heartbeat itself instead of waiting for a foreground
        // EnsureActiveGroup to restart it.
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
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken);
        await coordinator.StopHeartbeatAsync();
        var findCoordinatorCountAfterJoin = Volatile.Read(ref findCoordinatorCount);

        var heartbeatCount = 0;
        var recovered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref heartbeatCount) == 1)
                {
                    return ValueTask.FromException<ConsumerGroupHeartbeatResponse>(
                        new IOException("coordinator connection closed"));
                }

                recovered.TrySetResult(true);
                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = 1,
                    HeartbeatIntervalMs = 60_000
                });
            });
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);

        using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loop = InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, loopCts.Token);
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        await loopCts.CancelAsync();
        await loop.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(Volatile.Read(ref findCoordinatorCount)).IsEqualTo(findCoordinatorCountAfterJoin + 1);
        await Assert.That(GetPrivateField<int>(coordinator, "_coordinatorId")).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumerProtocol_PersistentTransportFailureDuringHeartbeat_HandsBackAfterSessionTimeout()
    {
        // Once a whole session timeout passes without a successful heartbeat the broker has
        // expired the member anyway: the loop stops and the foreground rejoin path takes over.
        SetupSuccessfulConsumerProtocolJoin();
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1,
            sessionTimeoutMs: 100);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic" },
            CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<ConsumerGroupHeartbeatResponse>(
                new IOException("coordinator connection closed")));
        SetPrivateField(coordinator, "_heartbeatIntervalMs", 1);

        await InvokeConsumerProtocolHeartbeatLoopAsync(coordinator, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(GetPrivateField<int>(coordinator, "_coordinatorId")).IsEqualTo(-1);
    }

    [Test]
    public async Task ConsumerProtocol_CancelledHeartbeatTransportFailure_DoesNotInvalidateRejoin()
    {
        SetupFindCoordinator();
        var oldHeartbeatStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldHeartbeatCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldHeartbeatResponse = new TaskCompletionSource<ConsumerGroupHeartbeatResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var heartbeatCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var call = Interlocked.Increment(ref heartbeatCount);
                if (call == 2)
                {
                    var heartbeatToken = callInfo.ArgAt<CancellationToken>(2);
                    heartbeatToken.Register(
                        static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                        oldHeartbeatCancelled);
                    oldHeartbeatStarted.TrySetResult(true);
                    return new ValueTask<ConsumerGroupHeartbeatResponse>(oldHeartbeatResponse.Task);
                }

                return ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = call == 1 ? 1 : 2,
                    HeartbeatIntervalMs = call == 1 ? 1 : 60_000
                });
            });

        var options = CreateConsumerProtocolOptions(heartbeatIntervalMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, _connectionPool, _metadataManager);
        var topics = new HashSet<string> { "test-topic" };

        await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        await oldHeartbeatStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        coordinator.RequestRejoin();
        var rejoin = coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None).AsTask();
        await oldHeartbeatCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        oldHeartbeatResponse.TrySetException(new IOException("retired coordinator connection closed"));
        await rejoin.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.GenerationId).IsEqualTo(2);
    }

    [Test]
    public async Task ConsumerProtocol_FindCoordinator_TransportFailureTriesNextBroker()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var availableConnection = Substitute.For<IKafkaConnection>();
        connectionPool.GetConnectionByIndexAsync(
                Arg.Is(0),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<IKafkaConnection>(
                new SocketException((int)SocketError.ConnectionRefused)));
        connectionPool.GetConnectionByIndexAsync(
                Arg.Is(1),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(availableConnection));
        availableConnection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
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
                        NodeId = 1,
                        Host = "broker-1",
                        Port = 9092,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));
        availableConnection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                MemberEpoch = 1,
                HeartbeatIntervalMs = 60_000
            }));

        await using var metadataManager = new MetadataManager(connectionPool, ["broker-0:9092"]);
        metadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0);
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "broker-0", Port = 9092 },
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9092 }
            ],
            Topics = []
        });
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000),
            connectionPool,
            metadataManager);

        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic" },
            CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(GetPrivateField<int>(coordinator, "_coordinatorId")).IsEqualTo(1);
        await connectionPool.Received().GetConnectionByIndexAsync(
            Arg.Is(1),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConsumerProtocol_FindCoordinator_TransportFailureExhaustionThrowsGroupException()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        connectionPool.GetConnectionByIndexAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<IKafkaConnection>(
                new SocketException((int)SocketError.ConnectionRefused)));

        await using var metadataManager = new MetadataManager(connectionPool, ["broker-0:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "broker-0", Port = 9092 },
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9092 }
            ],
            Topics = []
        });
        var options = CreateConsumerProtocolOptions(retryBackoffMs: 1, retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, connectionPool, metadataManager);

        var exception = await Assert.That(async () =>
                await InvokeFindCoordinatorAsync(coordinator, CancellationToken.None))
            .Throws<GroupException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.CoordinatorNotAvailable);
        await Assert.That(exception.InnerException).IsTypeOf<SocketException>();
        await connectionPool.Received(5).GetConnectionByIndexAsync(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    public enum CoordinatorTransportFailure
    {
        ConnectionResetDuringTlsHandshake,
        ConnectionRefused,
        DnsResolutionFailed,
        ConnectionSetupTimedOut
    }

    private static Exception CreateCoordinatorTransportFailure(CoordinatorTransportFailure failure) => failure switch
    {
        CoordinatorTransportFailure.ConnectionResetDuringTlsHandshake => new IOException(
            "Received an unexpected EOF or 0 bytes from the transport stream.",
            new SocketException((int)SocketError.ConnectionReset)),
        CoordinatorTransportFailure.ConnectionRefused => new SocketException((int)SocketError.ConnectionRefused),
        CoordinatorTransportFailure.DnsResolutionFailed => new DnsResolutionException(
            "broker-1",
            9092,
            new SocketException((int)SocketError.HostNotFound)),
        CoordinatorTransportFailure.ConnectionSetupTimedOut => new TimeoutException("Connection setup timed out."),
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null)
    };

    // Regression tests for #3339: a broker that resets or refuses connections while the consumer
    // joins must not fail the poll. FindCoordinator succeeds on a healthy broker and names a
    // coordinator whose connection cannot be set up; the join loop must re-discover and retry
    // instead of propagating the raw transport exception to the caller.
    [Test]
    [Arguments(CoordinatorTransportFailure.ConnectionResetDuringTlsHandshake)]
    [Arguments(CoordinatorTransportFailure.ConnectionRefused)]
    [Arguments(CoordinatorTransportFailure.DnsResolutionFailed)]
    [Arguments(CoordinatorTransportFailure.ConnectionSetupTimedOut)]
    public async Task ConsumerProtocol_Join_CoordinatorConnectionFailure_RediscoversAndRetries(
        CoordinatorTransportFailure failure)
    {
        await using var topology = new SplitCoordinatorTopology();
        var transportFailure = CreateCoordinatorTransportFailure(failure);
        topology.FailCoordinatorLease(attempt => attempt == 1 ? transportFailure : null);
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(GetPrivateField<int>(coordinator, "_coordinatorId"))
            .IsEqualTo(SplitCoordinatorTopology.CoordinatorBrokerId);
        await Assert.That(topology.CoordinatorLeaseAttempts).IsEqualTo(2);
        // The coordinator may have moved while it was unreachable, so the retry re-discovers it.
        await Assert.That(topology.FindCoordinatorCount).IsEqualTo(2);
        await Assert.That(GetPrivateField<string?>(coordinator, "_lastHeartbeatFailure")).IsNull();
    }

    [Test]
    public async Task ConsumerProtocol_Join_HeartbeatTransportFailure_RediscoversAndRetries()
    {
        await using var topology = new SplitCoordinatorTopology();
        var heartbeatCount = 0;
        topology.CoordinatorConnection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref heartbeatCount) == 1
                ? ValueTask.FromException<ConsumerGroupHeartbeatResponse>(
                    new IOException("Socket closed while writing a Kafka request frame."))
                : ValueTask.FromResult(SplitCoordinatorTopology.CreateJoinResponse()));
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(heartbeatCount).IsEqualTo(2);
        await Assert.That(topology.FindCoordinatorCount).IsEqualTo(2);
    }

    [Test]
    public async Task ConsumerProtocol_Join_PersistentCoordinatorConnectionFailure_FailsAfterRebalanceTimeout()
    {
        await using var topology = new SplitCoordinatorTopology();
        var transportFailure = new IOException(
            "Received an unexpected EOF or 0 bytes from the transport stream.",
            new SocketException((int)SocketError.ConnectionReset));
        topology.FailCoordinatorLease(_ => transportFailure);
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            rebalanceTimeoutMs: 300,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 5);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);

        var exception = await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None))
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Rebalance);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(topology.CoordinatorLeaseAttempts).IsGreaterThan(1);
        await Assert.That(GetPrivateField<string?>(coordinator, "_lastHeartbeatFailure"))
            .IsEqualTo(transportFailure.Message);
    }

    [Test]
    public async Task ConsumerProtocol_Join_PersistentCoordinatorConnectionFailure_TimeoutCarriesTheTransportCause()
    {
        await using var topology = new SplitCoordinatorTopology();
        var transportFailure = new SocketException((int)SocketError.ConnectionRefused);
        topology.FailCoordinatorLease(_ => transportFailure);
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            rebalanceTimeoutMs: 300,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 5);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);

        var exception = await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None))
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.InnerException).IsSameReferenceAs(transportFailure);
    }

    [Test]
    public async Task ConsumerProtocol_Join_AttemptBlockedPastRebalanceTimeout_IsBoundedByTheJoinDeadline(
        CancellationToken cancellationToken)
    {
        // A coordinator that black-holes packets: the lease never completes on its own. The
        // deadline must end the attempt; the check between attempts never runs.
        await using var topology = new SplitCoordinatorTopology();
        topology.Pool.GetConnectionByIndexAsync(
                Arg.Is(SplitCoordinatorTopology.CoordinatorBrokerId),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var leaseToken = callInfo.ArgAt<CancellationToken>(2);
                var blocked = new TaskCompletionSource<IKafkaConnection>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                leaseToken.Register(() => blocked.TrySetCanceled(leaseToken));
                return new ValueTask<IKafkaConnection>(blocked.Task);
            });
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            rebalanceTimeoutMs: 200,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);

        var join = coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken).AsTask();
        var exception = await Assert.That(async () => await join.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken))
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Rebalance);
    }

    [Test]
    public async Task ConsumerProtocol_Join_TransportFailureAfterCallerCancellation_ReportsCancellation()
    {
        // A hosted service shutting down during an outage: the socket failure lands after the
        // caller's token fired. The caller must see its cancellation, not a raw IOException.
        await using var topology = new SplitCoordinatorTopology();
        using var callerCancellation = new CancellationTokenSource();
        topology.FailCoordinatorLease(_ =>
        {
            callerCancellation.Cancel();
            return new IOException("connection reset");
        });
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);

        await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, callerCancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(topology.CoordinatorLeaseAttempts).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumerProtocol_Join_PoolDisposedAfterCoordinatorDisposal_ExitsPromptly(
        CancellationToken cancellationToken)
    {
        // A disposed pool keeps throwing ObjectDisposedException. While the coordinator is alive
        // that is a connection retired by pool churn and is retried; once the coordinator itself
        // is disposed the join must stop instead of spinning under the state lock until the
        // rebalance timeout.
        await using var topology = new SplitCoordinatorTopology();
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            rebalanceTimeoutMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);
        var retried = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        topology.FailCoordinatorLease(attempt =>
        {
            if (attempt >= 3)
                retried.TrySetResult(true);
            return new ObjectDisposedException(nameof(ConnectionPool));
        });

        var join = coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellationToken).AsTask();
        await retried.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        SetPrivateField(coordinator, "_disposed", 1);

        await Assert.That(async () => await join.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ConsumerProtocol_Join_UnknownBroker_RefreshesMetadataAndRetries()
    {
        // FindCoordinator named a broker the pool has no route for yet. The typed routing
        // failure is retried; a plain InvalidOperationException would have escaped the join.
        await using var topology = new SplitCoordinatorTopology();
        topology.FailCoordinatorLease(attempt =>
            attempt == 1 ? new UnknownBrokerException(SplitCoordinatorTopology.CoordinatorBrokerId) : null);
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(topology.CoordinatorLeaseAttempts).IsEqualTo(2);
    }

    [Test]
    public async Task ConsumerProtocol_Join_WrappedTransportFailure_RediscoversAndRetries()
    {
        await using var topology = new SplitCoordinatorTopology();
        topology.FailCoordinatorLease(attempt => attempt == 1
            ? new MetadataRefreshFailedException(new SocketException((int)SocketError.ConnectionRefused))
            : null);
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(topology.FindCoordinatorCount).IsEqualTo(2);
    }

    [Test]
    public async Task CommitOffsetsAsync_RetryUntilApiTimeout_RidesOutTheStaleMetadataWindow()
    {
        // The coordinator was killed; FindCoordinator keeps naming it and its connection is
        // refused far more often than the count-bounded retry allows for.
        await using var topology = new SplitCoordinatorTopology();
        topology.MetadataManager.SetApiVersion(ApiKey.OffsetCommit, 8, 8);
        var commitRequests = 0;
        topology.CoordinatorConnection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref commitRequests);
                return ValueTask.FromResult(new OffsetCommitResponse { Topics = [] });
            });
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        var leasesAfterJoin = topology.CoordinatorLeaseAttempts;
        topology.FailCoordinatorLease(attempt => attempt <= leasesAfterJoin + 10
            ? new SocketException((int)SocketError.ConnectionRefused)
            : null);

        await coordinator.CommitOffsetsAsync(
            [new TopicPartitionOffset("test-topic", 0, 1)],
            retryUntilApiTimeout: true,
            CancellationToken.None);

        await Assert.That(commitRequests).IsEqualTo(1);
        await Assert.That(topology.CoordinatorLeaseAttempts).IsEqualTo(leasesAfterJoin + 11);
    }

    [Test]
    public async Task CommitOffsetsAsync_RetryUntilApiTimeout_PersistentFailure_ThrowsTypedTimeoutWithTheCause()
    {
        await using var topology = new SplitCoordinatorTopology();
        topology.MetadataManager.SetApiVersion(ApiKey.OffsetCommit, 8, 8);
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 5,
            defaultApiTimeoutMs: 300);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        var transportFailure = new SocketException((int)SocketError.ConnectionRefused);
        topology.FailCoordinatorLease(_ => transportFailure);

        var exception = await Assert.That(async () =>
                await coordinator.CommitOffsetsAsync(
                    [new TopicPartitionOffset("test-topic", 0, 1)],
                    retryUntilApiTimeout: true,
                    CancellationToken.None))
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Api);
        await Assert.That(exception.InnerException).IsSameReferenceAs(transportFailure);
    }

    [Test]
    public async Task CommitOffsetsAsync_BackgroundCommit_KeepsTheCountBoundedRetry()
    {
        // Auto-commit, rebalance and close-path commits swallow the failure; they must not hold
        // the commit lock or delay a shutdown for the length of an outage.
        await using var topology = new SplitCoordinatorTopology();
        topology.MetadataManager.SetApiVersion(ApiKey.OffsetCommit, 8, 8);
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        var leasesAfterJoin = topology.CoordinatorLeaseAttempts;
        topology.FailCoordinatorLease(_ => new SocketException((int)SocketError.ConnectionRefused));

        await Assert.That(async () =>
                await coordinator.CommitOffsetsAsync(
                    [new TopicPartitionOffset("test-topic", 0, 1)],
                    CancellationToken.None))
            .Throws<SocketException>();
        await Assert.That(topology.CoordinatorLeaseAttempts).IsEqualTo(leasesAfterJoin + RetryHelper.MaxRetries + 1);
    }

    [Test]
    public async Task FetchOffsetsAsync_CoordinatorRefusesConnections_RidesOutTheStaleMetadataWindow()
    {
        // Position initialization on the application's poll: the crash in #3339's sibling path.
        await using var topology = new SplitCoordinatorTopology();
        topology.MetadataManager.SetApiVersion(ApiKey.OffsetFetch, 7, 7);
        var fetchRequests = 0;
        topology.CoordinatorConnection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref fetchRequests);
                return ValueTask.FromResult(new OffsetFetchResponse { Topics = [], ErrorCode = ErrorCode.None });
            });
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        var leasesAfterJoin = topology.CoordinatorLeaseAttempts;
        topology.FailCoordinatorLease(attempt => attempt <= leasesAfterJoin + 6
            ? new SocketException((int)SocketError.ConnectionRefused)
            : null);

        await coordinator.FetchOffsetsAsync([new TopicPartition("test-topic", 0)], CancellationToken.None);

        await Assert.That(fetchRequests).IsEqualTo(1);
    }

    [Test]
    public async Task FetchOffsetsAsync_PersistentCoordinatorConnectionFailure_ThrowsTypedTimeoutWithTheCause()
    {
        await using var topology = new SplitCoordinatorTopology();
        topology.MetadataManager.SetApiVersion(ApiKey.OffsetFetch, 7, 7);
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 5,
            requestTimeoutMs: 300);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);
        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        var transportFailure = new IOException(
            "connection reset",
            new SocketException((int)SocketError.ConnectionReset));
        topology.FailCoordinatorLease(_ => transportFailure);

        var exception = await Assert.That(async () =>
                await coordinator.FetchOffsetsAsync([new TopicPartition("test-topic", 0)], CancellationToken.None))
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.InnerException).IsSameReferenceAs(transportFailure);
    }

    [Test]
    public async Task ConsumerProtocol_FindCoordinator_NextLookupStartsAtTheBrokerThatAnswered()
    {
        // Broker 0 refuses connections. The first lookup pays for that once; the second must
        // not try the dead broker first again.
        var connectionPool = Substitute.For<IConnectionPool>();
        var availableConnection = Substitute.For<IKafkaConnection>();
        var deadBrokerAttempts = 0;
        connectionPool.GetConnectionByIndexAsync(Arg.Is(0), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref deadBrokerAttempts);
                return ValueTask.FromException<IKafkaConnection>(
                    new SocketException((int)SocketError.ConnectionRefused));
            });
        connectionPool.GetConnectionByIndexAsync(Arg.Is(1), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(availableConnection));
        availableConnection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
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
                        NodeId = 1,
                        Host = "broker-1",
                        Port = 9092,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));
        await using var metadataManager = new MetadataManager(connectionPool, ["broker-0:9092"]);
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "broker-0", Port = 9092 },
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9092 }
            ],
            Topics = []
        });
        var options = CreateConsumerProtocolOptions(retryBackoffMs: 1, retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, connectionPool, metadataManager);

        await InvokeFindCoordinatorAsync(coordinator, CancellationToken.None);
        await InvokeFindCoordinatorAsync(coordinator, CancellationToken.None);

        await Assert.That(deadBrokerAttempts).IsEqualTo(1);
    }

    [Test]
    public async Task ConsumerProtocol_Join_TlsHandshakeFailure_PropagatesWithoutRetry()
    {
        await using var topology = new SplitCoordinatorTopology();
        var handshakeFailure = AuthenticationException.FromTlsHandshake(
            "TLS handshake failed: The remote certificate is invalid according to the validation procedure.",
            new System.Security.Authentication.AuthenticationException("The remote certificate is invalid."));
        topology.FailCoordinatorLease(_ => handshakeFailure);
        var options = CreateConsumerProtocolOptions(
            heartbeatIntervalMs: 60_000,
            retryBackoffMs: 1,
            retryBackoffMaxMs: 1);
        await using var coordinator = new ConsumerCoordinator(options, topology.Pool, topology.MetadataManager);

        var exception = await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None))
            .Throws<AuthenticationException>();

        await Assert.That(exception!.Message).IsEqualTo(handshakeFailure.Message);
        await Assert.That(topology.CoordinatorLeaseAttempts).IsEqualTo(1);
        await Assert.That(topology.FindCoordinatorCount).IsEqualTo(1);
    }

    [Test]
    [Arguments(ErrorCode.GroupAuthorizationFailed)]
    [Arguments(ErrorCode.TopicAuthorizationFailed)]
    [Arguments(ErrorCode.ClusterAuthorizationFailed)]
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

        KafkaException? caught = null;
        try
        {
            await coordinator.EnsureActiveGroupAsync(topics, CancellationToken.None);
        }
        catch (KafkaException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.ErrorCode).IsEqualTo(errorCode);
        if (errorCode == ErrorCode.InvalidGroupId)
        {
            await Assert.That(caught).IsTypeOf<GroupException>();
            await Assert.That(((GroupException)caught).GroupId).IsEqualTo("test-group");
        }
        else
            await Assert.That(caught.GetType()).IsEqualTo(typeof(AuthorizationException));
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

    private static T GetPrivateField<T>(ConsumerCoordinator coordinator, string fieldName)
    {
        var field = typeof(ConsumerCoordinator).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{fieldName} field not found.");
        return (T)field.GetValue(coordinator)!;
    }

    private static void SetPrivateField<T>(ConsumerCoordinator coordinator, string fieldName, T value)
    {
        var field = typeof(ConsumerCoordinator).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{fieldName} field not found.");
        field.SetValue(coordinator, value);
    }

    /// <summary>
    /// Two-broker topology for join transport tests. Broker 0 is always reachable and answers
    /// FindCoordinator with broker 1; the caller controls whether broker 1's connection lease
    /// succeeds, mirroring a healthy cluster whose group coordinator is restarting.
    /// </summary>
    private sealed class SplitCoordinatorTopology : IAsyncDisposable
    {
        public const int MetadataBrokerId = 0;
        public const int CoordinatorBrokerId = 1;

        private int _findCoordinatorCount;
        private int _coordinatorLeaseAttempts;

        public IConnectionPool Pool { get; } = Substitute.For<IConnectionPool>();
        public IKafkaConnection CoordinatorConnection { get; } = Substitute.For<IKafkaConnection>();
        public MetadataManager MetadataManager { get; }
        public int FindCoordinatorCount => Volatile.Read(ref _findCoordinatorCount);
        public int CoordinatorLeaseAttempts => Volatile.Read(ref _coordinatorLeaseAttempts);

        public SplitCoordinatorTopology()
        {
            var metadataConnection = Substitute.For<IKafkaConnection>();
            metadataConnection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                    Arg.Any<FindCoordinatorRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    Interlocked.Increment(ref _findCoordinatorCount);
                    return ValueTask.FromResult(new FindCoordinatorResponse
                    {
                        Coordinators =
                        [
                            new Coordinator
                            {
                                Key = "test-group",
                                NodeId = CoordinatorBrokerId,
                                Host = "broker-1",
                                Port = 9092,
                                ErrorCode = ErrorCode.None
                            }
                        ]
                    });
                });
            CoordinatorConnection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                    Arg.Any<ConsumerGroupHeartbeatRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(CreateJoinResponse()));

            Pool.GetConnectionByIndexAsync(
                    Arg.Is(MetadataBrokerId),
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(metadataConnection));
            FailCoordinatorLease(static _ => null);

            MetadataManager = new MetadataManager(Pool, ["broker-0:9092"]);
            MetadataManager.SetApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0);
            MetadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
            // Broker 0 is registered first so FindCoordinator's broker cycling starts on the
            // reachable broker and every coordinator lease attempt belongs to the heartbeat.
            MetadataManager.Metadata.Update(new MetadataResponse
            {
                Brokers =
                [
                    new BrokerMetadata { NodeId = MetadataBrokerId, Host = "broker-0", Port = 9092 },
                    new BrokerMetadata { NodeId = CoordinatorBrokerId, Host = "broker-1", Port = 9092 }
                ],
                Topics = []
            });
        }

        public static ConsumerGroupHeartbeatResponse CreateJoinResponse() => new()
        {
            ErrorCode = ErrorCode.None,
            MemberId = "member-1",
            MemberEpoch = 1,
            HeartbeatIntervalMs = 60_000
        };

        /// <summary>
        /// Controls the coordinator's connection lease: <paramref name="failureForAttempt"/>
        /// receives the 1-based attempt number and returns the exception to fault with, or
        /// null to hand out the coordinator connection.
        /// </summary>
        public void FailCoordinatorLease(Func<int, Exception?> failureForAttempt)
        {
            Pool.GetConnectionByIndexAsync(
                    Arg.Is(CoordinatorBrokerId),
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    var failure = failureForAttempt(Interlocked.Increment(ref _coordinatorLeaseAttempts));
                    return failure is null
                        ? ValueTask.FromResult(CoordinatorConnection)
                        : ValueTask.FromException<IKafkaConnection>(failure);
                });
        }

        public ValueTask DisposeAsync() => MetadataManager.DisposeAsync();
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
