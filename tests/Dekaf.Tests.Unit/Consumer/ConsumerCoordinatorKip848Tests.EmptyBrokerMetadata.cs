using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class ConsumerCoordinatorKip848Tests
{
    [Test]
    public async Task ConsumerProtocol_Join_EmptyBrokerMetadata_RefreshesBootstrapAndJoins()
    {
        var refreshes = 0;
        SetupEmptyBrokerMetadata(_ => ValueTask.FromResult(++refreshes == 1
            ? new MetadataResponse { Brokers = [], Topics = [] }
            : RecoveredBrokerMetadata()));
        SetupSuccessfulConsumerProtocolJoin();
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(retryBackoffMs: 1, retryBackoffMaxMs: 1),
            _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(refreshes).IsEqualTo(2);
    }

    [Test]
    public async Task ConsumerProtocol_Join_EmptyBrokerMetadata_CancellationStopsRefresh()
    {
        var refreshing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetupEmptyBrokerMetadata(token =>
        {
            refreshing.TrySetResult();
            return WaitForMetadataCancellationAsync(token);
        });
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(), _connectionPool, _metadataManager);
        using var cancellation = new CancellationTokenSource();

        var join = coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, cancellation.Token).AsTask();
        try
        {
            // Also observe an unexpected early fault instead of waiting for a test timeout.
            await Task.WhenAny(refreshing.Task, join);
            if (join.IsCompleted) await join;
            cancellation.Cancel();
            await Assert.That(async () => await join).Throws<OperationCanceledException>();
            await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        }
        finally { cancellation.Cancel(); }
    }

    [Test]
    public async Task ConsumerProtocol_Join_EmptyBrokerMetadata_UsesExistingRebalanceDeadline()
    {
        var refreshes = 0;
        SetupEmptyBrokerMetadata(_ =>
        {
            Interlocked.Increment(ref refreshes);
            return ValueTask.FromResult(new MetadataResponse { Brokers = [], Topics = [] });
        });
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(rebalanceTimeoutMs: 100, retryBackoffMs: 1, retryBackoffMaxMs: 5),
            _connectionPool, _metadataManager);

        var failure = await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(10)))
            .Throws<KafkaTimeoutException>();

        await Assert.That(failure!.TimeoutKind).IsEqualTo(TimeoutKind.Rebalance);
        await Assert.That(failure.InnerException).IsTypeOf<GroupException>();
        await Assert.That(((GroupException)failure.InnerException!).ErrorCode).IsEqualTo(ErrorCode.CoordinatorNotAvailable);
        await Assert.That(refreshes).IsGreaterThan(1);
    }

    [Test]
    public async Task FetchOffsetsAsync_EmptyBrokerMetadata_SingleAttemptDoesNotWaitForRefresh()
    {
        SetupEmptyBrokerMetadata(WaitForMetadataCancellationAsync);
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(), _connectionPool, _metadataManager);

        // Poll position initialization holds the assignment lock. Recovery is queued by
        // its existing single-attempt path; an empty cache must not add a network wait.
        var failure = await Assert.That(async () =>
                await coordinator.FetchOffsetsAsync([new TopicPartition("test-topic", 0)],
                        rejoinOnMembershipLoss: false, unstablePartitions: null,
                        retryOnFailure: false, CancellationToken.None)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(10)))
            .Throws<GroupException>();

        await Assert.That(failure!.ErrorCode).IsEqualTo(ErrorCode.CoordinatorNotAvailable);
    }

    [Test]
    public async Task ConsumerProtocol_Join_EmptyBrokerMetadata_PreservesFatalRefreshFailure()
    {
        var authorization = KafkaException.FromErrorCode(ErrorCode.ClusterAuthorizationFailed, "Metadata authorization denied");
        SetupEmptyBrokerMetadata(_ => ValueTask.FromException<MetadataResponse>(authorization));
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(), _connectionPool, _metadataManager);

        var failure = await Assert.That(async () =>
                await coordinator.EnsureActiveGroupAsync(new HashSet<string> { "test-topic" }, CancellationToken.None))
            .Throws<KafkaException>();

        await Assert.That(failure).IsSameReferenceAs(authorization);
    }

    private void SetupEmptyBrokerMetadata(Func<CancellationToken, ValueTask<MetadataResponse>> refresh)
    {
        // A valid response during graceful broker shutdown replaces an earlier populated snapshot.
        _metadataManager.Metadata.Update(new MetadataResponse { Brokers = [], Topics = [] });
        _connectionPool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_connection));
        _connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(ApiKey.Metadata, MetadataRequest.LowestSupportedVersion, MetadataRequest.HighestSupportedVersion),
                    new ApiVersion(ApiKey.FindCoordinator, 4, 5),
                    new ApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0)
                ]
            }));
        _connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(call => refresh(call.ArgAt<CancellationToken>(2)));
    }

    private static MetadataResponse RecoveredBrokerMetadata() => new()
    {
        Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
        Topics = []
    };

    private static async ValueTask<MetadataResponse> WaitForMetadataCancellationAsync(CancellationToken token)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        throw new InvalidOperationException("Cancellation should end the metadata refresh.");
    }
}
