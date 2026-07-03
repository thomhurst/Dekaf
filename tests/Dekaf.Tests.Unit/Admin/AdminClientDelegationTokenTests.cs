using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientDelegationTokenTests
{
    [Test]
    public async Task CreateDelegationTokenAsync_SendsOwnerRenewersAndMapsResponse()
    {
        await using var context = new AdminTestContext();
        context.EnqueueCreate(new CreateDelegationTokenResponse
        {
            ErrorCode = ErrorCode.None,
            PrincipalType = "User",
            PrincipalName = "owner",
            TokenRequesterPrincipalType = "User",
            TokenRequesterPrincipalName = "requester",
            IssueTimestampMs = 1000,
            ExpiryTimestampMs = 2000,
            MaxTimestampMs = 3000,
            TokenId = "token-id",
            Hmac = [1, 2, 3],
            ThrottleTimeMs = 5
        });

        var token = await context.Client.CreateDelegationTokenAsync(
            owner: new DelegationTokenPrincipal("User", "owner"),
            renewers: [new DelegationTokenPrincipal("User", "renewer")],
            maxLifetime: TimeSpan.FromMinutes(5));

        var request = context.SingleRequest<CreateDelegationTokenRequest>();
        await Assert.That(request.Owner!.PrincipalName).IsEqualTo("owner");
        await Assert.That(request.Renewers![0].PrincipalName).IsEqualTo("renewer");
        await Assert.That(request.MaxLifetimeMs).IsEqualTo(300000);
        await Assert.That(token.Owner.PrincipalName).IsEqualTo("owner");
        await Assert.That(token.TokenRequester!.Value.PrincipalName).IsEqualTo("requester");
        await Assert.That(token.TokenId).IsEqualTo("token-id");
        await Assert.That(token.Hmac).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(token.Renewers.Single().PrincipalName).IsEqualTo("renewer");
    }

    [Test]
    public async Task CreateDelegationTokenAsync_WithOwnerAndV2_ThrowsNotSupported()
    {
        await using var context = new AdminTestContext(createDelegationTokenMaxVersion: 2);

        await Assert.That(async () => await context.Client.CreateDelegationTokenAsync(
                owner: new DelegationTokenPrincipal("User", "owner")))
            .Throws<NotSupportedException>();

        await context.Connection.DidNotReceive().SendAsync<CreateDelegationTokenRequest, CreateDelegationTokenResponse>(
            Arg.Any<CreateDelegationTokenRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RenewDelegationTokenAsync_SendsHmacAndMapsExpiry()
    {
        await using var context = new AdminTestContext();
        context.EnqueueRenew(new RenewDelegationTokenResponse
        {
            ErrorCode = ErrorCode.None,
            ExpiryTimestampMs = 5000,
            ThrottleTimeMs = 1
        });

        var expiry = await context.Client.RenewDelegationTokenAsync([9, 8], TimeSpan.FromSeconds(30));

        var request = context.SingleRequest<RenewDelegationTokenRequest>();
        await Assert.That(request.Hmac).IsEquivalentTo(new byte[] { 9, 8 });
        await Assert.That(request.RenewPeriodMs).IsEqualTo(30000);
        await Assert.That(expiry).IsEqualTo(DateTimeOffset.FromUnixTimeMilliseconds(5000));
    }

    [Test]
    public async Task ExpireDelegationTokenAsync_SendsHmacAndMapsExpiry()
    {
        await using var context = new AdminTestContext();
        context.EnqueueExpire(new ExpireDelegationTokenResponse
        {
            ErrorCode = ErrorCode.None,
            ExpiryTimestampMs = 6000,
            ThrottleTimeMs = 1
        });

        var expiry = await context.Client.ExpireDelegationTokenAsync([7, 6], TimeSpan.Zero);

        var request = context.SingleRequest<ExpireDelegationTokenRequest>();
        await Assert.That(request.Hmac).IsEquivalentTo(new byte[] { 7, 6 });
        await Assert.That(request.ExpiryTimePeriodMs).IsEqualTo(0);
        await Assert.That(expiry).IsEqualTo(DateTimeOffset.FromUnixTimeMilliseconds(6000));
    }

    [Test]
    public async Task DescribeDelegationTokensAsync_SendsOwnersAndMapsTokens()
    {
        await using var context = new AdminTestContext();
        context.EnqueueDescribe(new DescribeDelegationTokenResponse
        {
            ErrorCode = ErrorCode.None,
            Tokens =
            [
                new DescribedDelegationTokenData
                {
                    PrincipalType = "User",
                    PrincipalName = "owner",
                    TokenRequesterPrincipalType = "User",
                    TokenRequesterPrincipalName = "requester",
                    IssueTimestampMs = 1000,
                    ExpiryTimestampMs = 2000,
                    MaxTimestampMs = 3000,
                    TokenId = "token-id",
                    Hmac = [4, 5],
                    Renewers =
                    [
                        new DelegationTokenPrincipalData
                        {
                            PrincipalType = "User",
                            PrincipalName = "renewer"
                        }
                    ]
                }
            ],
            ThrottleTimeMs = 2
        });

        var tokens = await context.Client.DescribeDelegationTokensAsync(
            [new DelegationTokenPrincipal("User", "owner")]);

        var request = context.SingleRequest<DescribeDelegationTokenRequest>();
        await Assert.That(request.Owners![0].PrincipalName).IsEqualTo("owner");
        await Assert.That(tokens.Count).IsEqualTo(1);
        await Assert.That(tokens[0].Owner.PrincipalName).IsEqualTo("owner");
        await Assert.That(tokens[0].TokenRequester!.Value.PrincipalName).IsEqualTo("requester");
        await Assert.That(tokens[0].Renewers[0].PrincipalName).IsEqualTo("renewer");
    }

    private sealed class AdminTestContext : IAsyncDisposable
    {
        private readonly IConnectionPool _pool;
        private readonly MetadataManager _metadataManager;
        private readonly Queue<CreateDelegationTokenResponse> _createResponses = new();
        private readonly Queue<RenewDelegationTokenResponse> _renewResponses = new();
        private readonly Queue<ExpireDelegationTokenResponse> _expireResponses = new();
        private readonly Queue<DescribeDelegationTokenResponse> _describeResponses = new();
        private readonly List<object> _requests = [];

        public AdminTestContext(short createDelegationTokenMaxVersion = 3)
        {
            Connection = Substitute.For<IKafkaConnection>();
            Connection.BrokerId.Returns(1);
            Connection.Host.Returns("localhost");
            Connection.Port.Returns(9092);
            Connection.IsConnected.Returns(true);

            Connection.SendAsync<CreateDelegationTokenRequest, CreateDelegationTokenResponse>(
                    Arg.Any<CreateDelegationTokenRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    _requests.Add(callInfo.ArgAt<CreateDelegationTokenRequest>(0));
                    return new ValueTask<CreateDelegationTokenResponse>(_createResponses.Dequeue());
                });

            Connection.SendAsync<RenewDelegationTokenRequest, RenewDelegationTokenResponse>(
                    Arg.Any<RenewDelegationTokenRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    _requests.Add(callInfo.ArgAt<RenewDelegationTokenRequest>(0));
                    return new ValueTask<RenewDelegationTokenResponse>(_renewResponses.Dequeue());
                });

            Connection.SendAsync<ExpireDelegationTokenRequest, ExpireDelegationTokenResponse>(
                    Arg.Any<ExpireDelegationTokenRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    _requests.Add(callInfo.ArgAt<ExpireDelegationTokenRequest>(0));
                    return new ValueTask<ExpireDelegationTokenResponse>(_expireResponses.Dequeue());
                });

            Connection.SendAsync<DescribeDelegationTokenRequest, DescribeDelegationTokenResponse>(
                    Arg.Any<DescribeDelegationTokenRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    _requests.Add(callInfo.ArgAt<DescribeDelegationTokenRequest>(0));
                    return new ValueTask<DescribeDelegationTokenResponse>(_describeResponses.Dequeue());
                });

            _pool = Substitute.For<IConnectionPool>();
            _pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<IKafkaConnection>(Connection));

            _metadataManager = new MetadataManager(_pool, ["localhost:9092"]);
            _metadataManager.Metadata.Update(new MetadataResponse
            {
                Brokers =
                [
                    new BrokerMetadata
                    {
                        NodeId = 1,
                        Host = "localhost",
                        Port = 9092
                    }
                ],
                ClusterId = "test-cluster",
                ControllerId = 1,
                Topics = []
            });
            _metadataManager.SetApiVersion(ApiKey.CreateDelegationToken, 1, createDelegationTokenMaxVersion);
            _metadataManager.SetApiVersion(ApiKey.RenewDelegationToken, 1, 2);
            _metadataManager.SetApiVersion(ApiKey.ExpireDelegationToken, 1, 2);
            _metadataManager.SetApiVersion(ApiKey.DescribeDelegationToken, 1, 3);

            Client = new AdminClient(
                new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
                _pool,
                _metadataManager);
        }

        public AdminClient Client { get; }
        public IKafkaConnection Connection { get; }

        public void EnqueueCreate(CreateDelegationTokenResponse response) => _createResponses.Enqueue(response);
        public void EnqueueRenew(RenewDelegationTokenResponse response) => _renewResponses.Enqueue(response);
        public void EnqueueExpire(ExpireDelegationTokenResponse response) => _expireResponses.Enqueue(response);
        public void EnqueueDescribe(DescribeDelegationTokenResponse response) => _describeResponses.Enqueue(response);

        public T SingleRequest<T>() => _requests.OfType<T>().Single();

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync().ConfigureAwait(false);
            await _metadataManager.DisposeAsync().ConfigureAwait(false);
        }
    }
}
