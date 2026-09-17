using System.Net.Sockets;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerCoordinatorTests
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
}
