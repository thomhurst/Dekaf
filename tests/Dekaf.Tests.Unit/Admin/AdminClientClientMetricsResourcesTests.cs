using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Tests.Unit.Producer;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientClientMetricsResourcesTests
{
    [Test]
    public async Task ListClientMetricsResourcesAsync_MapsResponseToResult()
    {
        var (admin, connection) = CreateAdminWithMockConnection(maxApiVersion: 1);

        connection.SendAsync<ListClientMetricsResourcesRequest, ListClientMetricsResourcesResponse>(
                Arg.Any<ListClientMetricsResourcesRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ListClientMetricsResourcesResponse
            {
                ErrorCode = ErrorCode.None,
                ClientMetricsResources =
                [
                    new ListClientMetricsResource { Name = "prod" },
                    new ListClientMetricsResource { Name = "analytics" }
                ]
            }));

        var result = await admin.ListClientMetricsResourcesAsync();

        await Assert.That(result.Select(static resource => resource.Name))
            .IsEquivalentTo(["prod", "analytics"]);

        await connection.Received(1).SendAsync<ListClientMetricsResourcesRequest, ListClientMetricsResourcesResponse>(
            Arg.Any<ListClientMetricsResourcesRequest>(),
            Arg.Is<short>(version => version == 0),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListConfigResourcesAsync_MapsTypedResourcesAndFilters()
    {
        var (admin, connection) = CreateAdminWithMockConnection(maxApiVersion: 1);

        connection.SendAsync<ListConfigResourcesRequest, ListConfigResourcesResponse>(
                Arg.Any<ListConfigResourcesRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ListConfigResourcesResponse
            {
                ErrorCode = ErrorCode.None,
                ConfigResources =
                [
                    new ListConfigResource { Name = "orders", ResourceType = (sbyte)ConfigResourceType.Topic },
                    new ListConfigResource { Name = "analytics", ResourceType = (sbyte)ConfigResourceType.ClientMetrics }
                ]
            }));

        var result = await admin.ListConfigResourcesAsync(new ListConfigResourcesOptions
        {
            ResourceTypes = [ConfigResourceType.Topic, ConfigResourceType.ClientMetrics]
        });

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Name).IsEqualTo("orders");
        await Assert.That(result[0].Type).IsEqualTo(ConfigResourceType.Topic);
        await Assert.That(result[1].Name).IsEqualTo("analytics");
        await Assert.That(result[1].Type).IsEqualTo(ConfigResourceType.ClientMetrics);

        await connection.Received(1).SendAsync<ListConfigResourcesRequest, ListConfigResourcesResponse>(
            Arg.Is<ListConfigResourcesRequest>(request => request != null &&
                request.ResourceTypes.SequenceEqual(new[]
                {
                    (sbyte)ConfigResourceType.Topic,
                    (sbyte)ConfigResourceType.ClientMetrics
                })),
            Arg.Is<short>(version => version == 1),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListConfigResourcesAsync_V0Broker_ThrowsBrokerVersionException()
    {
        var (admin, _) = CreateAdminWithMockConnection(maxApiVersion: 0);

        var exception = await Assert.ThrowsAsync<BrokerVersionException>(async () =>
            await admin.ListConfigResourcesAsync());

        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task ListClientMetricsResourcesAsync_TopLevelError_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection();

        connection.SendAsync<ListClientMetricsResourcesRequest, ListClientMetricsResourcesResponse>(
                Arg.Any<ListClientMetricsResourcesRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ListClientMetricsResourcesResponse
            {
                ErrorCode = ErrorCode.ClusterAuthorizationFailed
            }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.ListClientMetricsResourcesAsync());

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
    }

    [Test]
    public async Task ListClientMetricsResourcesAsync_HoldsConnectionLeaseThroughResponse()
    {
        var connection = new TestKafkaConnection
        {
            SendResponse = static requestType => requestType == typeof(ListClientMetricsResourcesRequest)
                ? new ListClientMetricsResourcesResponse { ErrorCode = ErrorCode.None }
                : throw new NotSupportedException()
        };
        var (admin, _) = CreateAdminWithMockConnection(connection);
        var retirableConnection = (IRetirableKafkaConnection)connection;

        await admin.ListClientMetricsResourcesAsync();

        await Assert.That(connection.LeaseCountDuringRequest).IsEqualTo(1);
        await Assert.That(retirableConnection.LeaseCount).IsEqualTo(0);
    }

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdminWithMockConnection(
        IKafkaConnection? suppliedConnection = null,
        short maxApiVersion = 0)
    {
        var connection = suppliedConnection ?? Substitute.For<IKafkaConnection>();
        if (suppliedConnection is null)
        {
            connection.BrokerId.Returns(1);
            connection.Host.Returns("localhost");
            connection.Port.Returns(9092);
            connection.IsConnected.Returns(true);
        }

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.Metadata.Update(CreateMetadataResponse());
        metadataManager.SetApiVersion(ApiKey.ListClientMetricsResources, 0, maxApiVersion);
        metadataManager.SetApiVersion(ApiKey.Metadata, 9, 13);

        if (suppliedConnection is null)
        {
            connection.SendAsync<MetadataRequest, MetadataResponse>(
                    Arg.Any<MetadataRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(CreateMetadataResponse()));

            connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                    Arg.Any<ApiVersionsRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(new ApiVersionsResponse
                {
                    ErrorCode = ErrorCode.None,
                    ApiKeys =
                    [
                        new ApiVersion(ApiKey.Metadata, 9, 13),
                        new ApiVersion(ApiKey.ListClientMetricsResources, 0, maxApiVersion)
                    ]
                }));
        }

        var admin = new AdminClient(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            pool,
            metadataManager);

        return (admin, connection);
    }

    private static MetadataResponse CreateMetadataResponse() => new()
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
    };
}
