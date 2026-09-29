using System.Net.Sockets;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;
using Dekaf.Telemetry;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed partial class ShareConsumerCoordinatorTests
{
    // Regression test for #3339 (share-group counterpart): FindCoordinator succeeds on a healthy
    // broker and names a coordinator whose connection is reset during setup. The join loop must
    // re-discover the coordinator and retry instead of propagating the raw IOException.
    [Test]
    public async Task EnsureActiveGroupAsync_CoordinatorConnectionReset_RediscoversAndRetries()
    {
        var topicId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["broker-0:9092"],
            GroupId = "share-join",
            RetryBackoffMs = 1,
            RetryBackoffMaxMs = 1
        };
        var pool = Substitute.For<IConnectionPool>();
        var metadataConnection = Substitute.For<IKafkaConnection>();
        var coordinatorConnection = Substitute.For<IKafkaConnection>();
        var findCoordinatorCount = 0;
        var coordinatorLeaseAttempts = 0;
        metadataConnection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref findCoordinatorCount);
                return ValueTask.FromResult(new FindCoordinatorResponse
                {
                    Coordinators = [new Coordinator
                    {
                        Key = options.GroupId, NodeId = 1, Host = "broker-1", Port = 9092, ErrorCode = ErrorCode.None
                    }]
                });
            });
        coordinatorConnection.SendAsync<ShareGroupHeartbeatRequest, ShareGroupHeartbeatResponse>(
                Arg.Any<ShareGroupHeartbeatRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ShareGroupHeartbeatResponse
            {
                ErrorCode = ErrorCode.None,
                MemberId = "member-1",
                MemberEpoch = 1,
                HeartbeatIntervalMs = 60_000,
                Assignment = new ShareGroupHeartbeatAssignment
                {
                    TopicPartitions = [new ShareGroupHeartbeatTopicPartitions { TopicId = topicId, Partitions = [0] }]
                }
            }));
        pool.GetConnectionByIndexAsync(Arg.Is(0), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(metadataConnection));
        pool.GetConnectionByIndexAsync(Arg.Is(1), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref coordinatorLeaseAttempts) == 1
                ? ValueTask.FromException<IKafkaConnection>(new IOException(
                    "Received an unexpected EOF or 0 bytes from the transport stream.",
                    new SocketException((int)SocketError.ConnectionReset)))
                : ValueTask.FromResult(coordinatorConnection));
        await using var metadata = new MetadataManager(pool, options.BootstrapServers);
        metadata.SetApiVersion(ApiKey.ShareGroupHeartbeat, 0, 1);
        metadata.SetApiVersion(ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion, FindCoordinatorRequest.HighestSupportedVersion);
        // Broker 0 is registered first so FindCoordinator starts on the reachable broker.
        metadata.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "broker-0", Port = 9092 },
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9092 }
            ],
            Topics = [new TopicMetadata
            {
                ErrorCode = ErrorCode.None, Name = "first", TopicId = topicId,
                Partitions = [new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 1, ReplicaNodes = [1], IsrNodes = [1]
                }]
            }]
        });
        await using var coordinator = new ShareConsumerCoordinator(options, pool, metadata);
        coordinator.UpdateSubscription(["first"]);

        await coordinator.EnsureActiveGroupAsync(CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinatorLeaseAttempts).IsEqualTo(2);
        await Assert.That(findCoordinatorCount).IsEqualTo(2);
        await Assert.That(coordinator.CaptureGroupStatus().LastHeartbeatFailure).IsNull();
    }

    // One transport failure used to end the heartbeat loop; only the next poll restarted it. An
    // application busy in a handler longer than the session timeout was fenced. The loop must
    // re-discover the coordinator and keep beating on its own.
    [Test]
    public async Task HeartbeatLoop_TransportFailure_RediscoversAndKeepsBeating(CancellationToken cancellationToken)
    {
        var topicId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["broker-0:9092"],
            GroupId = "share-heartbeat",
            RetryBackoffMs = 1,
            RetryBackoffMaxMs = 1
        };
        var pool = Substitute.For<IConnectionPool>();
        var metadataConnection = Substitute.For<IKafkaConnection>();
        var coordinatorConnection = Substitute.For<IKafkaConnection>();
        var findCoordinatorCount = 0;
        var heartbeatCount = 0;
        var recovered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        metadataConnection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref findCoordinatorCount);
                return ValueTask.FromResult(new FindCoordinatorResponse
                {
                    Coordinators = [new Coordinator
                    {
                        Key = options.GroupId, NodeId = 1, Host = "broker-1", Port = 9092, ErrorCode = ErrorCode.None
                    }]
                });
            });
        coordinatorConnection.SendAsync<ShareGroupHeartbeatRequest, ShareGroupHeartbeatResponse>(
                Arg.Any<ShareGroupHeartbeatRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var call = Interlocked.Increment(ref heartbeatCount);
                if (call == 2)
                {
                    return ValueTask.FromException<ShareGroupHeartbeatResponse>(
                        new IOException("coordinator connection closed"));
                }

                if (call == 3)
                    recovered.TrySetResult(true);

                return ValueTask.FromResult(new ShareGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = 1,
                    // The join answer starts a fast loop; the recovery answer parks it again.
                    HeartbeatIntervalMs = call == 1 ? 1 : 60_000,
                    Assignment = call == 1
                        ? new ShareGroupHeartbeatAssignment
                        {
                            TopicPartitions =
                                [new ShareGroupHeartbeatTopicPartitions { TopicId = topicId, Partitions = [0] }]
                        }
                        : null
                });
            });
        pool.GetConnectionByIndexAsync(Arg.Is(0), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(metadataConnection));
        pool.GetConnectionByIndexAsync(Arg.Is(1), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(coordinatorConnection));
        await using var metadata = new MetadataManager(pool, options.BootstrapServers);
        metadata.SetApiVersion(ApiKey.ShareGroupHeartbeat, 0, 1);
        metadata.SetApiVersion(ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion, FindCoordinatorRequest.HighestSupportedVersion);
        metadata.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "broker-0", Port = 9092 },
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9092 }
            ],
            Topics = [new TopicMetadata
            {
                ErrorCode = ErrorCode.None, Name = "first", TopicId = topicId,
                Partitions = [new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 1, ReplicaNodes = [1], IsrNodes = [1]
                }]
            }]
        });
        await using var coordinator = new ShareConsumerCoordinator(options, pool, metadata);
        coordinator.UpdateSubscription(["first"]);

        await coordinator.EnsureActiveGroupAsync(cancellationToken);
        // No further foreground call: only the background loop can send the third heartbeat.
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(Volatile.Read(ref findCoordinatorCount)).IsEqualTo(2);
    }

    // Authentication failures are fatal: a TLS handshake that the coordinator rejects must
    // propagate on the first attempt instead of being retried until the join timeout.
    [Test]
    public async Task EnsureActiveGroupAsync_CoordinatorTlsHandshakeFailure_PropagatesWithoutRetry()
    {
        var topicId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["broker-0:9092"],
            GroupId = "share-join",
            RetryBackoffMs = 1,
            RetryBackoffMaxMs = 1
        };
        var handshakeFailure = AuthenticationException.FromTlsHandshake(
            "TLS handshake failed: The remote certificate is invalid according to the validation procedure.",
            new System.Security.Authentication.AuthenticationException("The remote certificate is invalid."));
        var pool = Substitute.For<IConnectionPool>();
        var metadataConnection = Substitute.For<IKafkaConnection>();
        var findCoordinatorCount = 0;
        var coordinatorLeaseAttempts = 0;
        metadataConnection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref findCoordinatorCount);
                return ValueTask.FromResult(new FindCoordinatorResponse
                {
                    Coordinators = [new Coordinator
                    {
                        Key = options.GroupId, NodeId = 1, Host = "broker-1", Port = 9092, ErrorCode = ErrorCode.None
                    }]
                });
            });
        pool.GetConnectionByIndexAsync(Arg.Is(0), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(metadataConnection));
        pool.GetConnectionByIndexAsync(Arg.Is(1), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref coordinatorLeaseAttempts);
                return ValueTask.FromException<IKafkaConnection>(handshakeFailure);
            });
        await using var metadata = new MetadataManager(pool, options.BootstrapServers);
        metadata.SetApiVersion(ApiKey.ShareGroupHeartbeat, 0, 1);
        metadata.SetApiVersion(ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion, FindCoordinatorRequest.HighestSupportedVersion);
        metadata.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "broker-0", Port = 9092 },
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9092 }
            ],
            Topics = [new TopicMetadata
            {
                ErrorCode = ErrorCode.None, Name = "first", TopicId = topicId,
                Partitions = [new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 1, ReplicaNodes = [1], IsrNodes = [1]
                }]
            }]
        });
        await using var coordinator = new ShareConsumerCoordinator(options, pool, metadata);
        coordinator.UpdateSubscription(["first"]);

        var exception = await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(CancellationToken.None))
            .Throws<AuthenticationException>();

        await Assert.That(exception!.Message).IsEqualTo(handshakeFailure.Message);
        await Assert.That(coordinatorLeaseAttempts).IsEqualTo(1);
        await Assert.That(findCoordinatorCount).IsEqualTo(1);
    }

    // A member without partitions (its subscribed topic does not exist yet, or the group has more
    // members than partitions) is live. Its join used to wait for a non-empty assignment and
    // failed with a join timeout, although the regular consumer counts itself joined after its
    // first successful heartbeat.
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task EnsureActiveGroupAsync_NoPartitionsAssigned_JoinsAndPublishesLaterAssignment(
        bool joinCarriesEmptyAssignment, CancellationToken cancellationToken)
    {
        var topicId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var heartbeatCount = 0;
        await using var harness = CoordinatorHarness.Create(
            // The join deadline passes long before the broker cadence would deliver anything.
            new ShareConsumerOptions { BootstrapServers = ["broker-0:9092"], GroupId = "share-idle", SessionTimeoutMs = 1 },
            topicId,
            _ => Interlocked.Increment(ref heartbeatCount) == 1
                ? Success(epoch: 1, joinCarriesEmptyAssignment ? new ShareGroupHeartbeatAssignment { TopicPartitions = [] } : null)
                : Success(epoch: 1, new ShareGroupHeartbeatAssignment
                {
                    TopicPartitions = [new ShareGroupHeartbeatTopicPartitions { TopicId = topicId, Partitions = [0] }]
                }));
        var coordinator = harness.Coordinator;

        await coordinator.EnsureActiveGroupAsync(cancellationToken);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.MemberEpoch).IsEqualTo(1);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
        await Assert.That(heartbeatCount).IsEqualTo(1);
        var status = coordinator.CaptureGroupStatus();
        await Assert.That(status.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(status.Assignment.Count).IsEqualTo(0);

        // A later heartbeat publishes the assignment and wakes an idle poll.
        var changed = coordinator.GetAssignmentChangeTask();
        await SendHeartbeatAsync(coordinator, cancellationToken);

        await Assert.That(changed.IsCompleted).IsTrue();
        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("first", 0)]);
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
    }

    // Every completed join counts one rebalance, whether its heartbeat carried partitions, an
    // empty assignment or none; a join that carried partitions must not count twice. A later
    // assignment (the topic appeared) is another rebalance.
    [Test]
    [Arguments(JoinAssignment.None)]
    [Arguments(JoinAssignment.Empty)]
    [Arguments(JoinAssignment.Partitions)]
    public async Task EnsureActiveGroupAsync_CountsOneRebalancePerJoin(
        JoinAssignment joinAssignment, CancellationToken cancellationToken)
    {
        var topicId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var partitions = new ShareGroupHeartbeatAssignment
        {
            TopicPartitions = [new ShareGroupHeartbeatTopicPartitions { TopicId = topicId, Partitions = [0] }]
        };
        var heartbeatCount = 0;
        var metrics = new ShareConsumerTelemetryMetrics();
        metrics.Subscribe([RebalanceTotal]);
        await using var harness = CoordinatorHarness.Create(
            new ShareConsumerOptions { BootstrapServers = ["broker-0:9092"], GroupId = "share-rebalance" },
            topicId,
            _ => Interlocked.Increment(ref heartbeatCount) == 1
                ? Success(epoch: 1, joinAssignment switch
                {
                    JoinAssignment.None => null,
                    JoinAssignment.Empty => new ShareGroupHeartbeatAssignment { TopicPartitions = [] },
                    _ => partitions
                })
                : Success(epoch: 1, partitions),
            metrics);
        var coordinator = harness.Coordinator;

        await coordinator.EnsureActiveGroupAsync(cancellationToken);

        await Assert.That(RebalanceCount(metrics)).IsEqualTo(1d);

        await SendHeartbeatAsync(coordinator, cancellationToken);

        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("first", 0)]);
        await Assert.That(RebalanceCount(metrics))
            .IsEqualTo(joinAssignment == JoinAssignment.Partitions ? 1d : 2d);
    }

    public enum JoinAssignment
    {
        None,
        Empty,
        Partitions
    }

    // Joining without partitions must not swallow join failures: a join with no successful
    // heartbeat still reports the timeout, and a non-retriable group error still propagates.
    [Test]
    public async Task EnsureActiveGroupAsync_NoSuccessfulHeartbeat_StillTimesOut(CancellationToken cancellationToken)
    {
        await using var harness = CoordinatorHarness.Create(
            new ShareConsumerOptions
            {
                BootstrapServers = ["broker-0:9092"], GroupId = "share-timeout",
                SessionTimeoutMs = 50, RetryBackoffMs = 1, RetryBackoffMaxMs = 1
            },
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            _ => new ShareGroupHeartbeatResponse { ErrorCode = ErrorCode.CoordinatorNotAvailable });

        await Assert.That(async () => await harness.Coordinator.EnsureActiveGroupAsync(cancellationToken))
            .Throws<KafkaTimeoutException>();
        await Assert.That(harness.Coordinator.State).IsNotEqualTo(CoordinatorState.Stable);
    }

    [Test]
    public async Task EnsureActiveGroupAsync_NonRetriableHeartbeatError_Propagates(CancellationToken cancellationToken)
    {
        await using var harness = CoordinatorHarness.Create(
            new ShareConsumerOptions { BootstrapServers = ["broker-0:9092"], GroupId = "share-denied" },
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            _ => new ShareGroupHeartbeatResponse { ErrorCode = ErrorCode.GroupAuthorizationFailed });

        var exception = await Assert.That(async () => await harness.Coordinator.EnsureActiveGroupAsync(cancellationToken))
            .Throws<GroupException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(harness.Coordinator.State).IsNotEqualTo(CoordinatorState.Stable);
    }

    // An idle member is fenced like any other: the heartbeat loop leaves Stable, wakes the
    // waiting poll, and the next join starts over from epoch 0.
    [Test]
    public async Task HeartbeatLoop_IdleMemberFenced_RejoinsFromEpochZero(CancellationToken cancellationToken)
    {
        var heartbeatCount = 0;
        var sentEpochs = new System.Collections.Concurrent.ConcurrentQueue<int>();
        await using var harness = CoordinatorHarness.Create(
            new ShareConsumerOptions { BootstrapServers = ["broker-0:9092"], GroupId = "share-idle-fenced" },
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            request =>
            {
                sentEpochs.Enqueue(request.MemberEpoch);
                return Interlocked.Increment(ref heartbeatCount) switch
                {
                    // The join answer starts a fast loop so the fence arrives without a poll.
                    1 => Success(epoch: 1, new ShareGroupHeartbeatAssignment { TopicPartitions = [] }, heartbeatIntervalMs: 1),
                    2 => new ShareGroupHeartbeatResponse { ErrorCode = ErrorCode.FencedMemberEpoch },
                    _ => Success(epoch: 2, new ShareGroupHeartbeatAssignment { TopicPartitions = [] })
                };
            });
        var coordinator = harness.Coordinator;

        await coordinator.EnsureActiveGroupAsync(cancellationToken);
        var changed = coordinator.GetAssignmentChangeTask();
        if (coordinator.State == CoordinatorState.Stable)
            await changed.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.MemberEpoch).IsEqualTo(0);

        await coordinator.EnsureActiveGroupAsync(cancellationToken);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.MemberEpoch).IsEqualTo(2);
        await Assert.That(sentEpochs.ToArray()).IsEquivalentTo([0, 1, 0]);
    }

    [Test]
    public async Task TelemetryMemberId_OmitsUnjoinedFencedAndDisposedIdentities()
    {
        var pool = Substitute.For<IConnectionPool>();
        await using var metadata = new MetadataManager(pool, ["localhost:9092"]);
        await using var coordinator = new ShareConsumerCoordinator(
            new ShareConsumerOptions { BootstrapServers = ["localhost:9092"], GroupId = "group" }, pool, metadata);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(ShareConsumerCoordinator);
        type.GetField("_memberId", flags)!.SetValue(coordinator, "member-a");
        type.GetField("_memberEpoch", flags)!.SetValue(coordinator, 1);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsNull();
        type.GetField("_state", flags)!.SetValue(coordinator, CoordinatorState.Stable);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsEqualTo("member-a");
        type.GetField("_memberEpoch", flags)!.SetValue(coordinator, 0);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsNull();
        type.GetField("_memberId", flags)!.SetValue(coordinator, "member-b");
        type.GetField("_memberEpoch", flags)!.SetValue(coordinator, 2);
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsEqualTo("member-b");
        await coordinator.DisposeAsync();
        await Assert.That(coordinator.CaptureTelemetryMemberId()).IsNull();
    }

    [Test]
    public async Task WaitForAssignmentDelay_UsesHeartbeatInterval()
    {
        var delayMs = ShareConsumerCoordinator.GetWaitForAssignmentDelayMs(heartbeatIntervalMs: 3000);

        await Assert.That(delayMs).IsEqualTo(3000);
    }

    [Test]
    public async Task WaitForAssignmentDelay_NormalizesNonPositiveInterval()
    {
        var delayMs = ShareConsumerCoordinator.GetWaitForAssignmentDelayMs(heartbeatIntervalMs: 0);

        await Assert.That(delayMs).IsEqualTo(1);
    }

    [Test]
    public async Task JoinRetryDelay_UsesCalculatedDelayWhenDeadlineIsFartherAway()
    {
        var delay = ShareConsumerCoordinator.GetJoinRetryDelay(
            retryDelayMs: 500,
            elapsed: TimeSpan.FromSeconds(1),
            joinTimeout: TimeSpan.FromSeconds(5));

        await Assert.That(delay).IsEqualTo(TimeSpan.FromMilliseconds(500));
    }

    [Test]
    public async Task JoinRetryDelay_IsCappedToRemainingDeadline()
    {
        var delay = ShareConsumerCoordinator.GetJoinRetryDelay(
            retryDelayMs: 5_000,
            elapsed: TimeSpan.FromMilliseconds(4_750),
            joinTimeout: TimeSpan.FromSeconds(5));

        await Assert.That(delay).IsEqualTo(TimeSpan.FromMilliseconds(250));
    }

    [Test]
    public async Task JoinRetryDelay_IsZeroAfterDeadline()
    {
        var delay = ShareConsumerCoordinator.GetJoinRetryDelay(
            retryDelayMs: 500,
            elapsed: TimeSpan.FromSeconds(6),
            joinTimeout: TimeSpan.FromSeconds(5));

        await Assert.That(delay).IsEqualTo(TimeSpan.Zero);
    }

    private const string RebalanceTotal = "org.apache.kafka.consumer.share.coordinator.rebalance.total";

    private static double RebalanceCount(ShareConsumerTelemetryMetrics metrics)
    {
        var snapshot = new List<ClientTelemetryMetric>();
        metrics.Collect(new ClientTelemetrySubscription(Guid.Empty, 1, 0, 60_000, 4096, false, [RebalanceTotal]), snapshot);
        return snapshot.Single(metric => metric.Name == RebalanceTotal).Value;
    }

    private static ShareGroupHeartbeatResponse Success(
        int epoch, ShareGroupHeartbeatAssignment? assignment, int heartbeatIntervalMs = 60_000) => new()
    {
        ErrorCode = ErrorCode.None,
        MemberId = "member-1",
        MemberEpoch = epoch,
        HeartbeatIntervalMs = heartbeatIntervalMs,
        Assignment = assignment
    };

    /// <summary>
    /// A coordinator subscribed to topic "first" on a single broker that answers every
    /// ShareGroupHeartbeat with <c>respond</c>.
    /// </summary>
    private sealed class CoordinatorHarness : IAsyncDisposable
    {
        private readonly MetadataManager _metadata;

        private CoordinatorHarness(MetadataManager metadata, ShareConsumerCoordinator coordinator)
        {
            _metadata = metadata;
            Coordinator = coordinator;
        }

        public ShareConsumerCoordinator Coordinator { get; }

        public static CoordinatorHarness Create(
            ShareConsumerOptions options,
            Guid topicId,
            Func<ShareGroupHeartbeatRequest, ShareGroupHeartbeatResponse> respond,
            ShareConsumerTelemetryMetrics? telemetryMetrics = null)
        {
            var pool = Substitute.For<IConnectionPool>();
            var connection = Substitute.For<IKafkaConnection>();
            connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                    Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(new FindCoordinatorResponse
                {
                    Coordinators = [new Coordinator
                    {
                        Key = options.GroupId, NodeId = 0, Host = "broker-0", Port = 9092, ErrorCode = ErrorCode.None
                    }]
                }));
            connection.SendAsync<ShareGroupHeartbeatRequest, ShareGroupHeartbeatResponse>(
                    Arg.Any<ShareGroupHeartbeatRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => ValueTask.FromResult(respond(call.Arg<ShareGroupHeartbeatRequest>())));
            pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(connection));
            var metadata = new MetadataManager(pool, options.BootstrapServers);
            metadata.SetApiVersion(ApiKey.ShareGroupHeartbeat, 0, 1);
            metadata.SetApiVersion(ApiKey.FindCoordinator,
                FindCoordinatorRequest.LowestSupportedVersion, FindCoordinatorRequest.HighestSupportedVersion);
            metadata.Metadata.Update(new MetadataResponse
            {
                Brokers = [new BrokerMetadata { NodeId = 0, Host = "broker-0", Port = 9092 }],
                Topics = [new TopicMetadata
                {
                    ErrorCode = ErrorCode.None, Name = "first", TopicId = topicId,
                    Partitions = [new PartitionMetadata
                    {
                        ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 0, ReplicaNodes = [0], IsrNodes = [0]
                    }]
                }]
            });
            var coordinator = new ShareConsumerCoordinator(options, pool, metadata, telemetryMetrics: telemetryMetrics);
            coordinator.UpdateSubscription(["first"]);
            return new CoordinatorHarness(metadata, coordinator);
        }

        public async ValueTask DisposeAsync()
        {
            await Coordinator.DisposeAsync();
            await _metadata.DisposeAsync();
        }
    }
}
