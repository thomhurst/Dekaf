using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientControllerBootstrapTests
{
    [Test]
    public async Task DescribeClusterAsync_DiscoversControllersWithoutRegisteringBrokers()
    {
        await using var context = new ControllerAdminContext();

        var result = await context.Client.DescribeClusterAsync();

        await Assert.That(result.ClusterId).IsEqualTo("cluster-a");
        await Assert.That(result.ControllerId).IsEqualTo(2);
        await Assert.That(result.Nodes.Select(static node => node.NodeId)).IsEquivalentTo([1, 2]);
        context.Pool.DidNotReceiveWithAnyArgs().RegisterBroker(default, default!, default);
        await Assert.That(context.DiscoveryRequests).IsEqualTo(1);
        await Assert.That(context.LastDiscoveryRequest!.EndpointType)
            .IsEqualTo(DescribeClusterEndpointType.Controller);
        await Assert.That(context.LastDiscoveryVersion).IsGreaterThanOrEqualTo((short)1);
    }

    [Test]
    public async Task DescribeMetadataQuorumAsync_RoutesToAdvertisedActiveController()
    {
        await using var context = new ControllerAdminContext();
        context.ActiveController.SendAsync<DescribeQuorumRequest, DescribeQuorumResponse>(
                Arg.Any<DescribeQuorumRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateQuorumResponse(leaderId: 2)));

        var result = await context.Client.DescribeMetadataQuorumAsync();

        await Assert.That(result.LeaderId).IsEqualTo(2);
        await context.ActiveController.Received(1).SendAsync<DescribeQuorumRequest, DescribeQuorumResponse>(
            Arg.Any<DescribeQuorumRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
        await context.BootstrapController.DidNotReceiveWithAnyArgs()
            .SendAsync<DescribeQuorumRequest, DescribeQuorumResponse>(default!, default, default);
    }

    [Test]
    public async Task CreateTopicsAsync_ControllerBootstrap_RejectsBeforeWrite()
    {
        await using var context = new ControllerAdminContext();

        var exception = await Assert.That(async () => await context.Client.CreateTopicsAsync(
                [new NewTopic { Name = "unsupported" }]))
            .Throws<KafkaException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnsupportedEndpointType);
        await context.ActiveController.DidNotReceiveWithAnyArgs()
            .SendAsync<CreateTopicsRequest, CreateTopicsResponse>(default!, default, default);
    }

    [Test]
    public async Task MetadataOnlyBrokerOperation_ControllerBootstrap_RejectsInsteadOfReturningEmptyData()
    {
        await using var context = new ControllerAdminContext();

        var exception = await Assert.That(async () => await context.Client.ListTopicsAsync())
            .Throws<KafkaException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnsupportedEndpointType);
        await Assert.That(context.DiscoveryRequests).IsEqualTo(0);
    }

    [Test]
    public async Task BrokerBootstrapEndpoint_ReturnsClearEndpointTypeError()
    {
        await using var context = new ControllerAdminContext(new DescribeClusterResponse
        {
            ErrorCode = ErrorCode.MismatchedEndpointType,
            ErrorMessage = "Expected broker endpoint",
            EndpointType = DescribeClusterEndpointType.Broker,
            ClusterId = "cluster-a",
            ControllerId = -1,
            Nodes = []
        });

        var exception = await Assert.That(async () => await context.Client.DescribeClusterAsync())
            .Throws<KafkaException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.MismatchedEndpointType);
        await Assert.That(exception.Message).Contains("Expected broker endpoint");
    }

    [Test]
    public async Task RetriableControllerFailure_RefreshesLeaderAndFailsOverWithoutBrokerFallback()
    {
        await using var context = new ControllerAdminContext();
        context.EnqueueDiscovery(CreateDiscoveryResponse(activeControllerId: 1));
        context.ActiveController.SendAsync<DescribeQuorumRequest, DescribeQuorumResponse>(
                Arg.Any<DescribeQuorumRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<DescribeQuorumResponse>(
                Task.FromException<DescribeQuorumResponse>(
                    new KafkaException(ErrorCode.NotController, "Leader changed"))));
        context.OtherController.SendAsync<DescribeQuorumRequest, DescribeQuorumResponse>(
                Arg.Any<DescribeQuorumRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateQuorumResponse(leaderId: 1)));

        var result = await context.Client.DescribeMetadataQuorumAsync();

        await Assert.That(result.LeaderId).IsEqualTo(1);
        await Assert.That(context.DiscoveryRequests).IsGreaterThanOrEqualTo(2);
        await context.OtherController.Received(1).SendAsync<DescribeQuorumRequest, DescribeQuorumResponse>(
            Arg.Any<DescribeQuorumRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
        await context.Pool.DidNotReceiveWithAnyArgs()
            .GetConnectionAsync(default(int), default);
    }

    [Test]
    public async Task DescribeBrokerLoggerConfig_TargetsRequestedPhysicalController()
    {
        await using var context = new ControllerAdminContext();
        context.OtherController.SendAsync<DescribeConfigsRequest, DescribeConfigsResponse>(
                Arg.Any<DescribeConfigsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DescribeConfigsResponse
            {
                Results =
                [
                    new DescribeConfigsResult
                    {
                        ErrorCode = ErrorCode.None,
                        ResourceType = (sbyte)ConfigResourceType.BrokerLogger,
                        ResourceName = "1",
                        Configs = []
                    }
                ]
            }));

        var result = await context.Client.DescribeConfigsAsync([ConfigResource.BrokerLogger(1)]);

        await Assert.That(result.ContainsKey(ConfigResource.BrokerLogger(1))).IsTrue();
        await context.OtherController.Received(1).SendAsync<DescribeConfigsRequest, DescribeConfigsResponse>(
            Arg.Any<DescribeConfigsRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
        await context.ActiveController.DidNotReceiveWithAnyArgs()
            .SendAsync<DescribeConfigsRequest, DescribeConfigsResponse>(default!, default, default);
    }

    private static DescribeQuorumResponse CreateQuorumResponse(int leaderId) => new()
    {
        ErrorCode = ErrorCode.None,
        Topics =
        [
            new DescribeQuorumResponseTopic
            {
                TopicName = "__cluster_metadata",
                Partitions =
                [
                    new DescribeQuorumResponsePartition
                    {
                        PartitionIndex = 0,
                        ErrorCode = ErrorCode.None,
                        LeaderId = leaderId,
                        LeaderEpoch = 1,
                        HighWatermark = 10,
                        CurrentVoters = [],
                        Observers = []
                    }
                ]
            }
        ],
        Nodes = []
    };

    private static DescribeClusterResponse CreateDiscoveryResponse(int activeControllerId) => new()
    {
        ErrorCode = ErrorCode.None,
        EndpointType = DescribeClusterEndpointType.Controller,
        ClusterId = "cluster-a",
        ControllerId = activeControllerId,
        Nodes =
        [
            new DescribeClusterNode { NodeId = 1, Host = "controller-1", Port = 19093 },
            new DescribeClusterNode { NodeId = 2, Host = "controller-2", Port = 19094 }
        ]
    };

    private sealed class ControllerAdminContext : IAsyncDisposable
    {
        private readonly MetadataManager _metadataManager;
        private readonly Queue<DescribeClusterResponse> _discoveryResponses = new();

        internal ControllerAdminContext(DescribeClusterResponse? discoveryResponse = null)
        {
            BootstrapController = CreateConnection("seed", 9093);
            ActiveController = CreateConnection("controller-2", 19094);
            OtherController = CreateConnection("controller-1", 19093);
            Pool = Substitute.For<IConnectionPool>();
            Pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var host = callInfo.ArgAt<string>(0);
                    return ValueTask.FromResult(host switch
                    {
                        "controller-1" => OtherController,
                        "controller-2" => ActiveController,
                        _ => BootstrapController
                    });
                });

            _discoveryResponses.Enqueue(discoveryResponse ?? CreateDiscoveryResponse(activeControllerId: 2));
            ConfigureDiscovery(BootstrapController);
            ConfigureDiscovery(ActiveController);
            ConfigureDiscovery(OtherController);

            _metadataManager = new MetadataManager(Pool, []);
            _metadataManager.SetApiVersion(ApiKey.DescribeCluster, 1, 2);
            _metadataManager.SetApiVersion(ApiKey.DescribeQuorum, 0, 2);
            _metadataManager.SetApiVersion(ApiKey.CreateTopics, 0, 7);
            _metadataManager.SetApiVersion(ApiKey.DescribeConfigs, 4, 4);
            Client = new AdminClient(
                new AdminClientOptions
                {
                    BootstrapControllers = ["seed:9093"],
                    RetryBackoffMs = 1,
                    RetryBackoffMaxMs = 1
                },
                Pool,
                _metadataManager,
                ownsResources: true);
        }

        internal AdminClient Client { get; }
        internal IConnectionPool Pool { get; }
        internal IKafkaConnection BootstrapController { get; }
        internal IKafkaConnection ActiveController { get; }
        internal IKafkaConnection OtherController { get; }
        internal int DiscoveryRequests { get; private set; }
        internal DescribeClusterRequest? LastDiscoveryRequest { get; private set; }
        internal short LastDiscoveryVersion { get; private set; }

        internal void EnqueueDiscovery(DescribeClusterResponse response) => _discoveryResponses.Enqueue(response);

        public async ValueTask DisposeAsync() => await Client.DisposeAsync().ConfigureAwait(false);

        private void ConfigureDiscovery(IKafkaConnection connection)
        {
            connection.SendAsync<DescribeClusterRequest, DescribeClusterResponse>(
                    Arg.Any<DescribeClusterRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    DiscoveryRequests++;
                    LastDiscoveryRequest = callInfo.ArgAt<DescribeClusterRequest>(0);
                    LastDiscoveryVersion = callInfo.ArgAt<short>(1);
                    var response = _discoveryResponses.Count > 1
                        ? _discoveryResponses.Dequeue()
                        : _discoveryResponses.Peek();
                    return ValueTask.FromResult(response);
                });
        }

        private static IKafkaConnection CreateConnection(string host, int port)
        {
            var connection = Substitute.For<IKafkaConnection>();
            connection.Host.Returns(host);
            connection.Port.Returns(port);
            connection.IsConnected.Returns(true);
            return connection;
        }
    }
}
