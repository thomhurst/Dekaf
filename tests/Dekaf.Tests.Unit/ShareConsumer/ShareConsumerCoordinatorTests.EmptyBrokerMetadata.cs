using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

// #4039: a graceful broker shutdown can answer Metadata with zero brokers. Applying that
// snapshot must not permanently fault share-coordinator discovery.
public sealed partial class ShareConsumerCoordinatorTests
{
    [Test]
    public async Task EnsureActiveGroupAsync_EmptyBrokerMetadata_RefreshesBootstrapAndJoins()
    {
        var refreshes = 0;
        await using var harness = EmptyMetadataHarness.Create(
            _ => ValueTask.FromResult(Interlocked.Increment(ref refreshes) == 1
                ? new MetadataResponse { Brokers = [], Topics = [] }
                : RecoveredShareMetadata()),
            sessionTimeoutMs: 30_000);

        await harness.Coordinator.EnsureActiveGroupAsync(CancellationToken.None);

        await Assert.That(harness.Coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(harness.Coordinator.MemberEpoch).IsEqualTo(1);
        await Assert.That(refreshes).IsEqualTo(2);
    }

    [Test]
    public async Task EnsureActiveGroupAsync_PersistentEmptyBrokerMetadata_UsesExistingJoinDeadline()
    {
        var refreshes = 0;
        await using var harness = EmptyMetadataHarness.Create(
            _ =>
            {
                Interlocked.Increment(ref refreshes);
                return ValueTask.FromResult(new MetadataResponse { Brokers = [], Topics = [] });
            },
            sessionTimeoutMs: 100);

        var failure = await Assert.That(async () =>
                await harness.Coordinator.EnsureActiveGroupAsync(CancellationToken.None)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(10)))
            .Throws<KafkaTimeoutException>();

        await Assert.That(failure!.TimeoutKind).IsEqualTo(TimeoutKind.Rebalance);
        await Assert.That(failure.InnerException).IsTypeOf<GroupException>();
        await Assert.That(((GroupException)failure.InnerException!).ErrorCode)
            .IsEqualTo(ErrorCode.CoordinatorNotAvailable);
        // The deadline, not a refresh count, is the contract here: how many refreshes fit in the
        // 100 ms budget depends on scheduling. The test above covers the retry after an empty refresh.
        await Assert.That(refreshes).IsGreaterThanOrEqualTo(1);
        await Assert.That(harness.Coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
    }

    [Test]
    public async Task EnsureActiveGroupAsync_EmptyBrokerMetadata_CancellationStopsRefresh()
    {
        var refreshing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var harness = EmptyMetadataHarness.Create(
            token =>
            {
                refreshing.TrySetResult();
                return WaitForShareMetadataCancellationAsync(token);
            },
            sessionTimeoutMs: 30_000);
        using var cancellation = new CancellationTokenSource();

        var join = harness.Coordinator.EnsureActiveGroupAsync(cancellation.Token).AsTask();
        try
        {
            // Also observe an unexpected early fault instead of waiting for a test timeout.
            await Task.WhenAny(refreshing.Task, join);
            if (join.IsCompleted) await join;
            cancellation.Cancel();
            await Assert.That(async () => await join).Throws<OperationCanceledException>();
            await Assert.That(harness.Coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        }
        finally { cancellation.Cancel(); }
    }

    [Test]
    public async Task EnsureActiveGroupAsync_EmptyBrokerMetadata_PreservesFatalRefreshFailure()
    {
        var authorization = KafkaException.FromErrorCode(
            ErrorCode.ClusterAuthorizationFailed, "Metadata authorization denied");
        await using var harness = EmptyMetadataHarness.Create(
            _ => ValueTask.FromException<MetadataResponse>(authorization),
            sessionTimeoutMs: 30_000);

        var failure = await Assert.That(async () =>
                await harness.Coordinator.EnsureActiveGroupAsync(CancellationToken.None))
            .Throws<KafkaException>();

        await Assert.That(failure).IsSameReferenceAs(authorization);
    }

    private static MetadataResponse RecoveredShareMetadata() => new()
    {
        Brokers = [new BrokerMetadata { NodeId = 0, Host = "broker-0", Port = 9092 }],
        Topics = [new TopicMetadata
        {
            ErrorCode = ErrorCode.None, Name = "first", TopicId = KnownTopicId,
            Partitions = [new PartitionMetadata
            {
                ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 0, ReplicaNodes = [0], IsrNodes = [0]
            }]
        }]
    };

    private static async ValueTask<MetadataResponse> WaitForShareMetadataCancellationAsync(CancellationToken token)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        throw new InvalidOperationException("Cancellation should end the metadata refresh.");
    }

    private sealed class EmptyMetadataHarness : IAsyncDisposable
    {
        private readonly MetadataManager _metadata;

        private EmptyMetadataHarness(MetadataManager metadata, ShareConsumerCoordinator coordinator)
        {
            _metadata = metadata;
            Coordinator = coordinator;
        }

        public ShareConsumerCoordinator Coordinator { get; }

        public static EmptyMetadataHarness Create(
            Func<CancellationToken, ValueTask<MetadataResponse>> refresh,
            int sessionTimeoutMs)
        {
            var options = new ShareConsumerOptions
            {
                BootstrapServers = ["broker-0:9092"],
                GroupId = "share-empty-metadata",
                SessionTimeoutMs = sessionTimeoutMs,
                RetryBackoffMs = 1,
                RetryBackoffMaxMs = 5
            };
            var pool = Substitute.For<IConnectionPool>();
            var connection = Substitute.For<IKafkaConnection>();
            pool.GetConnectionAsync("broker-0", 9092, Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(connection));
            pool.GetConnectionByIndexAsync(Arg.Is(0), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(connection));
            connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                    Arg.Any<ApiVersionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(new ApiVersionsResponse
                {
                    ErrorCode = ErrorCode.None,
                    ApiKeys =
                    [
                        new ApiVersion(ApiKey.Metadata, MetadataRequest.LowestSupportedVersion, MetadataRequest.HighestSupportedVersion),
                        new ApiVersion(ApiKey.FindCoordinator, FindCoordinatorRequest.LowestSupportedVersion, FindCoordinatorRequest.HighestSupportedVersion),
                        new ApiVersion(ApiKey.ShareGroupHeartbeat, 0, 1)
                    ]
                }));
            connection.SendAsync<MetadataRequest, MetadataResponse>(
                    Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => refresh(call.ArgAt<CancellationToken>(2)));
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
                .Returns(ValueTask.FromResult(new ShareGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None,
                    MemberId = "member-1",
                    MemberEpoch = 1,
                    HeartbeatIntervalMs = 60_000,
                    Assignment = new ShareGroupHeartbeatAssignment
                    {
                        TopicPartitions = [new ShareGroupHeartbeatTopicPartitions { TopicId = KnownTopicId, Partitions = [0] }]
                    }
                }));

            var metadata = new MetadataManager(pool, options.BootstrapServers);
            metadata.SetApiVersion(ApiKey.ShareGroupHeartbeat, 0, 1);
            metadata.SetApiVersion(ApiKey.FindCoordinator,
                FindCoordinatorRequest.LowestSupportedVersion, FindCoordinatorRequest.HighestSupportedVersion);
            // A valid response during graceful broker shutdown replaces any populated snapshot.
            metadata.Metadata.Update(new MetadataResponse { Brokers = [], Topics = [] });
            var coordinator = new ShareConsumerCoordinator(options, pool, metadata);
            coordinator.UpdateSubscription(["first"]);
            return new EmptyMetadataHarness(metadata, coordinator);
        }

        public async ValueTask DisposeAsync()
        {
            await Coordinator.DisposeAsync();
            await _metadata.DisposeAsync();
        }
    }
}
