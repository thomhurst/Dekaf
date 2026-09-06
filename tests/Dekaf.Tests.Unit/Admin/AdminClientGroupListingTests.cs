using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public class AdminClientGroupListingTests
{
    [Test]
    public async Task ConsumerListing_ExcludesOtherProtocolsAndTypes_ButIncludesSimpleConsumers()
    {
        var (admin, connections) = CreateAdmin();
        await using var adminDisposal = admin;
        var groups = new ListGroupsResponseGroup[]
        {
            Group("modern", "Consumer", "consumer"),
            Group("classic", "Classic", "consumer"),
            Group("simple", "Classic", ""),
            Group("connect", "Classic", "connect"),
            Group("share", "Share", "share"),
            Group("streams", "Streams", "streams"),
            Group("future", "Future", "consumer")
        };
        foreach (var connection in connections.Values)
            Respond(connection, groups);

        var result = await admin.ListConsumerGroupsAsync();

        await Assert.That(result.Select(static group => group.GroupId))
            .IsEquivalentTo(["modern", "classic", "simple"]);
    }

    private static ListGroupsResponseGroup Group(string id, string? type, string? protocol, string state = "Stable") => new()
    {
        GroupId = id, GroupType = type, ProtocolType = protocol, GroupState = state
    };

    [Test]
    public async Task UnifiedListing_PreservesUnknownTypesAndCombinesFiltersBeforeDeduplication()
    {
        var (admin, connections) = CreateAdmin();
        await using var adminDisposal = admin;
        Respond(connections[1],
        [
            Group("moving", "Classic", "connect", "Empty"),
            Group("modern", "Consumer", "consumer"),
            Group("future", "FutureV2", "custom")
        ]);
        Respond(connections[2],
        [
            Group("moving", "Classic", "connect"),
            Group("future", "FutureV2", "custom"),
            Group("wrong-protocol", "Classic", "consumer")
        ]);
        var result = await admin.ListGroupsAsync(new ListGroupsOptions
        {
            States = ["stable"], Types = ["classic", "futurev2"], ProtocolTypes = ["connect", "custom"]
        });
        await Assert.That(result.Select(static group => group.GroupId)).IsEquivalentTo(["future", "moving"]);
        await Assert.That(result.Single(static group => group.GroupId == "future").GroupType).IsEqualTo("FutureV2");
        await Assert.That(result.Single(static group => group.GroupId == "moving").State).IsEqualTo("Stable");
        await connections[1].Received(1).SendAsync<ListGroupsRequest, ListGroupsResponse>(
            Arg.Is<ListGroupsRequest>(request => request.StatesFilter!.Count == 1 && request.TypesFilter!.Count == 2),
            5, Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments((short)3, false)]
    [Arguments((short)4, true)]
    public async Task UnsupportedFilter_ThrowsBeforeSendingRequest(short version, bool filterType)
    {
        var (admin, connections) = CreateAdmin(version);
        await using var adminDisposal = admin;
        var options = filterType ? new ListGroupsOptions { Types = ["Consumer"] }
            : new ListGroupsOptions { States = ["Stable"] };
        await Assert.That(async () => await admin.ListGroupsAsync(options)).Throws<KafkaException>();
        foreach (var connection in connections.Values)
            await connection.DidNotReceiveWithAnyArgs().SendAsync<ListGroupsRequest, ListGroupsResponse>(default!, default, default);
    }

    [Test]
    [Arguments((short)3)]
    [Arguments((short)4)]
    public async Task OlderResponse_LeavesTypeUnavailableAndStillFiltersProtocol(short version)
    {
        var (admin, connections) = CreateAdmin(version);
        await using var adminDisposal = admin;
        Respond(connections[1], [Group("connect", null, "connect"), Group("consumer", null, "consumer")]);
        var result = await admin.ListGroupsAsync(new ListGroupsOptions { ProtocolTypes = ["connect"] });
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].GroupId).IsEqualTo("connect");
        await Assert.That(result[0].GroupType).IsNull();
    }

    [Test]
    public async Task ProtocolFilters_AreExactAndDistinguishEmptyFromUnavailable()
    {
        var (admin, connections) = CreateAdmin();
        await using var adminDisposal = admin;
        Respond(connections[1],
        [
            Group("simple", "Classic", ""), Group("missing", "Classic", null),
            Group("upper", "Classic", "Connect"), Group("lower", "Classic", "connect")
        ]);
        var result = await admin.ListGroupsAsync(new ListGroupsOptions { ProtocolTypes = ["", "Connect"] });
        await Assert.That(result.Select(static group => group.GroupId)).IsEquivalentTo(["simple", "upper"]);
    }

    [Test]
    public async Task EmptyFilters_IncludeAllTypesAndCaseDistinctGroupIds()
    {
        var (admin, connections) = CreateAdmin();
        await using var adminDisposal = admin;
        Respond(connections[1], [Group("id", "Classic", "consumer"), Group("ID", "FutureV2", "custom")]);
        Respond(connections[2], [Group("id", "Classic", "consumer")]);
        var result = await admin.ListGroupsAsync(new ListGroupsOptions { States = [], Types = [], ProtocolTypes = [] });
        await Assert.That(result.Select(static group => group.GroupId)).IsEquivalentTo(["id", "ID"]);
        await Assert.That(result.Select(static group => group.GroupType!)).IsEquivalentTo(["Classic", "FutureV2"]);
    }

    [Test]
    public async Task ShareAndStreamsConveniences_DefensivelyFilterBrokerResults()
    {
        var (admin, connections) = CreateAdmin();
        await using var adminDisposal = admin;
        Respond(connections[1],
        [
            Group("share", "Share", "share"), Group("streams", "Streams", "streams"),
            Group("consumer", "Consumer", "consumer")
        ]);
        var share = await admin.ListShareGroupsAsync();
        var streams = await admin.ListStreamsGroupsAsync();
        await Assert.That(share.Select(static group => group.GroupId)).IsEquivalentTo(["share"]);
        await Assert.That(streams.Select(static group => group.GroupId)).IsEquivalentTo(["streams"]);
        await Assert.That(share[0].GroupType).IsEqualTo("Share");
        await Assert.That(streams[0].GroupType).IsEqualTo("Streams");
    }

    [Test]
    public async Task Extension_PreservesCustomAdminCompatibility()
    {
        var legacy = Substitute.For<IAdminClient>();
        await Assert.That(async () => await legacy.ListGroupsAsync()).Throws<NotSupportedException>();
        var capable = Substitute.For<IAdminClient, IGroupListingAdminClient>();
        var expected = new GroupListing { GroupId = "future", GroupType = "FutureV2" };
        ((IGroupListingAdminClient)capable).ListGroupsAsync(null, default)
            .Returns(new ValueTask<IReadOnlyList<GroupListing>>([expected]));
        var result = await capable.ListGroupsAsync();
        await Assert.That(result[0]).IsEqualTo(expected);
    }

    [Test]
    [Arguments((short)4, true)]
    [Arguments((short)3, false)]
    public async Task FilterCapability_IsCheckedOnEachDestination(short secondVersion, bool filterType)
    {
        var (admin, connections) = CreateAdmin(secondVersion: secondVersion);
        await using var adminDisposal = admin;
        var options = filterType ? new ListGroupsOptions { Types = ["Consumer"] }
            : new ListGroupsOptions { States = ["Stable"] };
        await Assert.That(async () => await admin.ListGroupsAsync(options))
            .Throws<KafkaException>();
        await connections[1].Received(1).SendAsync<ListGroupsRequest, ListGroupsResponse>(
            Arg.Any<ListGroupsRequest>(), 5, Arg.Any<CancellationToken>());
        await connections[2].DidNotReceiveWithAnyArgs().SendAsync<ListGroupsRequest, ListGroupsResponse>(default!, default, default);
    }

    [Test]
    [Arguments((short)3)]
    [Arguments((short)4)]
    public async Task ConvenienceTypeFilters_RejectOlderResponses(short version)
    {
        var (admin, _) = CreateAdmin(version);
        await using var adminDisposal = admin;
        await Assert.That(async () => await admin.ListConsumerGroupsAsync()).Throws<KafkaException>();
        await Assert.That(async () => await admin.ListShareGroupsAsync()).Throws<KafkaException>();
        await Assert.That(async () => await admin.ListStreamsGroupsAsync()).Throws<KafkaException>();
    }

    [Test]
    public async Task BrokersAreQueriedConcurrentlyAndAllResultsAreObserved()
    {
        var (admin, connections) = CreateAdmin();
        await using var adminDisposal = admin;
        var pending = new TaskCompletionSource<ListGroupsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connections[1].SendAsync<ListGroupsRequest, ListGroupsResponse>(Arg.Any<ListGroupsRequest>(),
                Arg.Any<short>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<ListGroupsResponse>(pending.Task));
        connections[2].SendAsync<ListGroupsRequest, ListGroupsResponse>(Arg.Any<ListGroupsRequest>(),
                Arg.Any<short>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                secondStarted.TrySetResult();
                return new ValueTask<ListGroupsResponse>(new ListGroupsResponse { Groups = [Group("second", "Consumer", "consumer")] });
            });
        var operation = admin.ListGroupsAsync();
        try
        {
            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.That(operation.IsCompleted).IsFalse();
        }
        finally
        {
            pending.TrySetResult(new ListGroupsResponse { Groups = [Group("first", "Share", "share")] });
        }
        var result = await operation;
        await Assert.That(result.Select(static group => group.GroupId)).IsEquivalentTo(["first", "second"]);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task InvalidFilters_AreRejectedBeforeBrokerQueries(int filter)
    {
        var (admin, connections) = CreateAdmin();
        await using var adminDisposal = admin;
        var options = filter switch
        {
            0 => new ListGroupsOptions { States = [" "] },
            1 => new ListGroupsOptions { Types = [""] },
            _ => new ListGroupsOptions { ProtocolTypes = [null!] }
        };
        await Assert.That(async () => await admin.ListGroupsAsync(options)).Throws<ArgumentException>();
        foreach (var connection in connections.Values)
            await connection.DidNotReceiveWithAnyArgs().SendAsync<ListGroupsRequest, ListGroupsResponse>(default!, default, default);
    }

    [Test]
    public async Task BrokerFailure_DoesNotReturnPartialInventory()
    {
        var (admin, connections) = CreateAdmin();
        await using var adminDisposal = admin;
        Respond(connections[1], [Group("valid", "Consumer", "consumer")]);
        connections[2].SendAsync<ListGroupsRequest, ListGroupsResponse>(Arg.Any<ListGroupsRequest>(),
                Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ListGroupsResponse>(new ListGroupsResponse
            {
                Groups = [], ErrorCode = ErrorCode.ClusterAuthorizationFailed
            }));
        var error = await Assert.That(async () => await admin.ListGroupsAsync()).Throws<KafkaException>();
        await Assert.That(error!.ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
    }

    [Test]
    public async Task CallerCancellation_ReachesEveryBroker()
    {
        var (admin, connections) = CreateAdmin();
        await using var adminDisposal = admin;
        using var cancellation = new CancellationTokenSource();
        foreach (var connection in connections.Values)
        {
            connection.SendAsync<ListGroupsRequest, ListGroupsResponse>(Arg.Any<ListGroupsRequest>(),
                    Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(call => new ValueTask<ListGroupsResponse>(WaitForCancellationAsync(call.ArgAt<CancellationToken>(2))));
        }
        var operation = admin.ListGroupsAsync(cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await Assert.That(async () => await operation).Throws<OperationCanceledException>();

        static async Task<ListGroupsResponse> WaitForCancellationAsync(CancellationToken token)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Cancellation did not throw.");
        }
    }

    private static void Respond(IKafkaConnection connection, IReadOnlyList<ListGroupsResponseGroup> groups) =>
        connection.SendAsync<ListGroupsRequest, ListGroupsResponse>(Arg.Any<ListGroupsRequest>(),
                Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ListGroupsResponse>(new ListGroupsResponse { Groups = groups }));

    private static (AdminClient Admin, Dictionary<int, IKafkaConnection> Connections) CreateAdmin(short version = 5, short? secondVersion = null)
    {
        var connections = new Dictionary<int, IKafkaConnection>();
        var destinations = new Dictionary<int, IKafkaConnection>();
        for (var id = 1; id <= 2; id++)
        {
            var connection = Substitute.For<IKafkaConnection>();
            connection.BrokerId.Returns(id);
            connection.Host.Returns("localhost");
            connection.Port.Returns(9091 + id);
            connection.IsConnected.Returns(true);
            Respond(connection, []);
            connections.Add(id, connection);
            destinations.Add(id, secondVersion.HasValue
                ? new CapabilityConnection(connection, id == 2 ? secondVersion.Value : version)
                : connection);
        }
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<IKafkaConnection>(destinations[call.ArgAt<int>(0)]));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(destinations[1]));
        var metadata = new MetadataManager(pool, ["localhost:9092"]);
        metadata.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 },
                new BrokerMetadata { NodeId = 2, Host = "localhost", Port = 9093 }
            ],
            ClusterId = "listing-test", ControllerId = 1, Topics = []
        });
        metadata.SetApiVersion(ApiKey.ListGroups, version, version);
        return (new AdminClient(new AdminClientOptions { BootstrapServers = ["localhost:9092"] }, pool, metadata), connections);
    }

    private sealed class CapabilityConnection(IKafkaConnection inner, short version) : IKafkaConnection, IKafkaCapabilityProvider
    {
        public int BrokerId => inner.BrokerId;
        public string Host => inner.Host;
        public int Port => inner.Port;
        public bool IsConnected => true;
        public KafkaConnectionCapabilities Capabilities { get; } = KafkaConnectionCapabilities.Create(new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None, ApiKeys = [new ApiVersion(ApiKey.ListGroups, 3, version)]
        });
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse =>
            inner.SendAsync<TRequest, TResponse>(request, apiVersion, cancellationToken);
        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(TRequest request, short apiVersion, CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse> where TResponse : IKafkaResponse => throw new NotSupportedException();
        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => default;
        public ValueTask DisposeAsync() => default;
    }
}
