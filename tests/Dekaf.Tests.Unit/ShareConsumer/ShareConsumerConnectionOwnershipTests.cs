using System.Reflection;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerConnectionOwnershipTests
{
    [Test]
    public async Task LeaveGroupAsync_HoldsConnectionLeaseThroughRequest()
    {
        var options = CreateOptions();
        var connection = new LeaseTrackingConnection(
            new ApiVersion(
                ApiKey.ShareGroupHeartbeat,
                ShareGroupHeartbeatRequest.LowestSupportedVersion,
                ShareGroupHeartbeatRequest.HighestSupportedVersion));
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionByIndexAsync(1, 1, Arg.Any<CancellationToken>())
            .Returns(connection);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        await using var coordinator = new ShareConsumerCoordinator(
            options,
            pool,
            metadataManager,
            getConnectionCount: () => 2);

        typeof(ShareConsumerCoordinator).GetField(
            "_memberId",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(coordinator, "member-1");
        typeof(ShareConsumerCoordinator).GetField(
            "_coordinatorId",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(coordinator, 1);

        await coordinator.LeaveGroupAsync();

        await Assert.That(connection.LeaseCountDuringSend).IsEqualTo(1);
        await Assert.That(connection.LeaseCount).IsEqualTo(0);
    }

    [Test]
    public async Task SendShareFetchForPartitionsAsync_HoldsConnectionLeaseThroughRequest()
    {
        var options = CreateOptions();
        var connection = new LeaseTrackingConnection(
            new ApiVersion(
                ApiKey.ShareFetch,
                ShareFetchRequest.LowestSupportedVersion,
                ShareFetchRequest.HighestSupportedVersion));
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(1, Arg.Any<CancellationToken>()).Returns(connection);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        await using var consumer = new KafkaShareConsumer<string, string>(
            options,
            Substitute.For<IDeserializer<string>>(),
            Substitute.For<IDeserializer<string>>(),
            pool,
            metadataManager);

        SetMemberId(consumer, "member-1");

        var sendTask = InvokeSendShareFetchForPartitionsAsync(consumer);

        await sendTask;

        await Assert.That(connection.LeaseCountDuringSend).IsEqualTo(1);
        await Assert.That(connection.LeaseCount).IsEqualTo(0);
    }

    [Test]
    public async Task SendShareFetchForPartitionsAsync_PropagatesBrokerVersionException()
    {
        var options = CreateOptions();
        var connection = new LeaseTrackingConnection(
            new ApiVersion(
                ApiKey.Metadata,
                MetadataRequest.LowestSupportedVersion,
                MetadataRequest.HighestSupportedVersion));
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(1, Arg.Any<CancellationToken>()).Returns(connection);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        await using var consumer = new KafkaShareConsumer<string, string>(
            options,
            Substitute.For<IDeserializer<string>>(),
            Substitute.For<IDeserializer<string>>(),
            pool,
            metadataManager);
        SetMemberId(consumer, "member-1");

        var sendTask = InvokeSendShareFetchForPartitionsAsync(consumer);

        await Assert.That(async () => await sendTask).Throws<BrokerVersionException>();
        await Assert.That(connection.LeaseCount).IsEqualTo(0);
    }

    [Test]
    public async Task SendShareFetchForPartitionsAsync_RetriesRetriableTopLevelError()
    {
        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "share-group",
            ConnectionsPerBroker = 2,
            RetryBackoffMs = 0,
            RetryBackoffMaxMs = 0
        };
        var connection = new LeaseTrackingConnection(
            new ApiVersion(
                ApiKey.ShareFetch,
                ShareFetchRequest.LowestSupportedVersion,
                ShareFetchRequest.HighestSupportedVersion),
            new ApiVersion(
                ApiKey.Metadata,
                MetadataRequest.LowestSupportedVersion,
                MetadataRequest.HighestSupportedVersion))
        {
            ShareFetchResponses = new Queue<ShareFetchResponse>(
            [
                new ShareFetchResponse
                {
                    ErrorCode = ErrorCode.CoordinatorLoadInProgress,
                    Responses = [],
                    NodeEndpoints = []
                },
                new ShareFetchResponse
                {
                    ErrorCode = ErrorCode.None,
                    Responses = [],
                    NodeEndpoints = []
                }
            ])
        };
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(1, Arg.Any<CancellationToken>()).Returns(connection);
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(connection);
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        await using var consumer = new KafkaShareConsumer<string, string>(
            options,
            Substitute.For<IDeserializer<string>>(),
            Substitute.For<IDeserializer<string>>(),
            pool,
            metadataManager);
        SetMemberId(consumer, "member-1");

        var sendTask = InvokeSendShareFetchForPartitionsAsync(consumer);

        await sendTask;

        await Assert.That(connection.ShareFetchSendCount).IsEqualTo(2);
        await Assert.That(connection.LeaseCount).IsEqualTo(0);
    }

    private static Task InvokeSendShareFetchForPartitionsAsync(
        KafkaShareConsumer<string, string> consumer)
    {
        var method = typeof(KafkaShareConsumer<string, string>).GetMethod(
            "SendShareFetchForPartitionsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(
            consumer,
            [
                1,
                new List<TopicPartition> { new("topic", 0) },
                null,
                CancellationToken.None
            ])!;
    }

    private static void SetMemberId(
        KafkaShareConsumer<string, string> consumer,
        string memberId)
    {
        var coordinator = typeof(KafkaShareConsumer<string, string>)
            .GetField("_coordinator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(consumer)!;
        typeof(ShareConsumerCoordinator)
            .GetField("_memberId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(coordinator, memberId);
    }

    private static ShareConsumerOptions CreateOptions() => new()
    {
        BootstrapServers = ["localhost:9092"],
        GroupId = "share-group",
        ConnectionsPerBroker = 2
    };

    private sealed class LeaseTrackingConnection(params ApiVersion[] versions) :
        IKafkaConnection,
        IRetirableKafkaConnection,
        IKafkaCapabilityProvider
    {
        private int _leaseCount;
        private int _retirementState;

        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public KafkaConnectionCapabilities Capabilities { get; } =
            KafkaConnectionCapabilities.Create(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys = versions
            });
        public int LeaseCount => Volatile.Read(ref _leaseCount);
        public int LeaseCountDuringSend { get; private set; }
        public int ShareFetchSendCount { get; private set; }
        public Queue<ShareFetchResponse>? ShareFetchResponses { get; init; }

        int IRetirableKafkaConnection.LeaseCount => LeaseCount;
        int IRetirableKafkaConnection.ActiveOperationCount => 0;

        bool IRetirableKafkaConnection.TryAcquireLease()
        {
            if (Volatile.Read(ref _retirementState) != 0)
                return false;

            Interlocked.Increment(ref _leaseCount);
            if (Volatile.Read(ref _retirementState) == 0)
                return true;

            ((IRetirableKafkaConnection)this).ReleaseLease();
            return false;
        }

        void IRetirableKafkaConnection.ReleaseLease() => Interlocked.Decrement(ref _leaseCount);

        void IRetirableKafkaConnection.BeginRetirement()
            => Interlocked.CompareExchange(ref _retirementState, 1, 0);

        void IRetirableKafkaConnection.CompleteRetirement()
            => Volatile.Write(ref _retirementState, 2);

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            LeaseCountDuringSend = LeaseCount;
            IKafkaResponse response = request switch
            {
                ShareGroupHeartbeatRequest => new ShareGroupHeartbeatResponse
                {
                    ErrorCode = ErrorCode.None
                },
                ShareFetchRequest => GetShareFetchResponse(),
                MetadataRequest => new MetadataResponse { Brokers = [], Topics = [] },
                _ => throw new NotSupportedException(typeof(TRequest).Name)
            };
            return new ValueTask<TResponse>((TResponse)response);
        }

        private ShareFetchResponse GetShareFetchResponse()
        {
            ShareFetchSendCount++;
            return ShareFetchResponses is { Count: > 0 }
                ? ShareFetchResponses.Dequeue()
                : new ShareFetchResponse
                {
                    ErrorCode = ErrorCode.None,
                    Responses = [],
                    NodeEndpoints = []
                };
        }

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => throw new NotSupportedException();

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
