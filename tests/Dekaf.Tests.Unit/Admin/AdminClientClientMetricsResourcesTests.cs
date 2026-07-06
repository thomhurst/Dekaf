using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientClientMetricsResourcesTests
{
    [Test]
    public async Task ListClientMetricsResourcesAsync_MapsResponseToResult()
    {
        var (admin, connection) = CreateAdminWithMockConnection();

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

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdminWithMockConnection()
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.BrokerId.Returns(1);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9092);
        connection.IsConnected.Returns(true);

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.Metadata.Update(CreateMetadataResponse());
        metadataManager.SetApiVersion(ApiKey.ListClientMetricsResources, 0, 0);
        metadataManager.SetApiVersion(ApiKey.Metadata, 9, 13);

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
                    new ApiVersion(ApiKey.ListClientMetricsResources, 0, 0)
                ]
            }));

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
