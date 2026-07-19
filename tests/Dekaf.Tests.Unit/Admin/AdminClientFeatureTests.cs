using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientFeatureTests
{
    [Test]
    public async Task DescribeFeaturesAsync_MapsConnectionCapabilitySnapshot()
    {
        var (admin, connection, _) = CreateAdmin(updateFeaturesVersion: 1);

        var result = await admin.DescribeFeaturesAsync();

        await Assert.That(result.FinalizedFeaturesEpoch).IsEqualTo(42L);
        await Assert.That(result.SupportedFeatures["metadata.version"])
            .IsEqualTo(new FeatureVersionRange(7, 19));
        await Assert.That(result.FinalizedFeatures["metadata.version"])
            .IsEqualTo(new FeatureVersionRange(7, 17));
        await connection.DidNotReceiveWithAnyArgs()
            .SendAsync<UpdateFeaturesRequest, UpdateFeaturesResponse>(default!, default, default);
    }

    [Test]
    public async Task UpdateFeaturesAsync_V1_PreservesRequestAndPerFeatureErrors()
    {
        var (admin, connection, _) = CreateAdmin(updateFeaturesVersion: 1);
        UpdateFeaturesRequest? capturedRequest = null;
        short capturedVersion = -1;
        connection.SendAsync<UpdateFeaturesRequest, UpdateFeaturesResponse>(
                Arg.Any<UpdateFeaturesRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.Arg<UpdateFeaturesRequest>();
                capturedVersion = callInfo.Arg<short>();
                return ValueTask.FromResult(new UpdateFeaturesResponse
                {
                    ErrorCode = ErrorCode.None,
                    Results =
                    [
                        new UpdateFeatureResult
                        {
                            Feature = "metadata.version",
                            ErrorCode = ErrorCode.FeatureUpdateFailed,
                            ErrorMessage = "unsupported level"
                        },
                        new UpdateFeatureResult
                        {
                            Feature = "unknown.feature",
                            ErrorCode = ErrorCode.InvalidRequest,
                            ErrorMessage = "unknown feature"
                        }
                    ]
                });
            });

        var result = await admin.UpdateFeaturesAsync(
            new Dictionary<string, FeatureUpdate>
            {
                ["metadata.version"] = new()
                {
                    MaxVersionLevel = 18,
                    UpgradeType = FeatureUpgradeType.SafeDowngrade
                },
                ["unknown.feature"] = new() { MaxVersionLevel = 1 }
            },
            new UpdateFeaturesOptions { TimeoutMs = 1234, ValidateOnly = true });

        await Assert.That(capturedVersion).IsEqualTo((short)1);
        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.TimeoutMs).IsEqualTo(1234);
        await Assert.That(capturedRequest.ValidateOnly).IsTrue();
        await Assert.That(capturedRequest.FeatureUpdates[0].UpgradeType)
            .IsEqualTo(FeatureUpdateType.SafeDowngrade);
        await Assert.That(result["metadata.version"].ErrorCode)
            .IsEqualTo(ErrorCode.FeatureUpdateFailed);
        await Assert.That(result["metadata.version"].ErrorMessage)
            .IsEqualTo("unsupported level");
        await Assert.That(result["unknown.feature"].ErrorCode)
            .IsEqualTo(ErrorCode.InvalidRequest);
    }

    [Test]
    public async Task UpdateFeaturesAsync_V2_MapsTopLevelSuccessToEachFeature()
    {
        var (admin, connection, _) = CreateAdmin(updateFeaturesVersion: 2);
        connection.SendAsync<UpdateFeaturesRequest, UpdateFeaturesResponse>(
                Arg.Any<UpdateFeaturesRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new UpdateFeaturesResponse
            {
                ErrorCode = ErrorCode.None,
                Results = []
            }));

        var result = await admin.UpdateFeaturesAsync(new Dictionary<string, FeatureUpdate>
        {
            ["metadata.version"] = new() { MaxVersionLevel = 18 },
            ["transaction.version"] = new() { MaxVersionLevel = 2 }
        });

        await Assert.That(result.Keys).IsEquivalentTo(["metadata.version", "transaction.version"]);
        await Assert.That(result.Values.All(static item => item.ErrorCode == ErrorCode.None)).IsTrue();
    }

    [Test]
    public async Task UpdateFeaturesAsync_TopLevelFailure_Throws()
    {
        var (admin, connection, _) = CreateAdmin(updateFeaturesVersion: 1);
        connection.SendAsync<UpdateFeaturesRequest, UpdateFeaturesResponse>(
                Arg.Any<UpdateFeaturesRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new UpdateFeaturesResponse
            {
                ErrorCode = ErrorCode.ClusterAuthorizationFailed,
                ErrorMessage = "denied",
                Results = []
            }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.UpdateFeaturesAsync(new Dictionary<string, FeatureUpdate>()));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.ClusterAuthorizationFailed);
    }

    [Test]
    public async Task UpdateFeaturesAsync_NotController_RediscoversController()
    {
        var (admin, firstController, secondController) = CreateAdmin(
            updateFeaturesVersion: 1,
            changeControllerOnRefresh: true);
        firstController.SendAsync<UpdateFeaturesRequest, UpdateFeaturesResponse>(
                Arg.Any<UpdateFeaturesRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new UpdateFeaturesResponse
            {
                ErrorCode = ErrorCode.NotController,
                Results = []
            }));
        secondController.SendAsync<UpdateFeaturesRequest, UpdateFeaturesResponse>(
                Arg.Any<UpdateFeaturesRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new UpdateFeaturesResponse
            {
                ErrorCode = ErrorCode.None,
                Results =
                [
                    new UpdateFeatureResult
                    {
                        Feature = "metadata.version",
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));

        var result = await admin.UpdateFeaturesAsync(new Dictionary<string, FeatureUpdate>
        {
            ["metadata.version"] = new() { MaxVersionLevel = 18 }
        });

        await Assert.That(result["metadata.version"].ErrorCode).IsEqualTo(ErrorCode.None);
        await firstController.Received(1).SendAsync<UpdateFeaturesRequest, UpdateFeaturesResponse>(
            Arg.Any<UpdateFeaturesRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
        await secondController.Received(1).SendAsync<UpdateFeaturesRequest, UpdateFeaturesResponse>(
            Arg.Any<UpdateFeaturesRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateFeaturesAsync_ValidateOnlyOnV0_ThrowsBrokerVersionException()
    {
        var (admin, _, _) = CreateAdmin(updateFeaturesVersion: 0);

        await Assert.ThrowsAsync<Dekaf.Errors.BrokerVersionException>(async () =>
            await admin.UpdateFeaturesAsync(
                new Dictionary<string, FeatureUpdate>(),
                new UpdateFeaturesOptions { ValidateOnly = true }));
    }

    private static (AdminClient Admin, IKafkaConnection First, IKafkaConnection Second) CreateAdmin(
        short updateFeaturesVersion,
        bool changeControllerOnRefresh = false)
    {
        var apiVersions = new ApiVersionsResponse
        {
            ErrorCode = ErrorCode.None,
            ApiKeys =
            [
                new ApiVersion(ApiKey.ApiVersions, 0, 4),
                new ApiVersion(ApiKey.Metadata, 9, 13),
                new ApiVersion(ApiKey.UpdateFeatures, 0, updateFeaturesVersion)
            ],
            SupportedFeatures = [new SupportedFeature("metadata.version", 7, 19)],
            FinalizedFeaturesEpoch = 42,
            FinalizedFeatures = [new FinalizedFeature("metadata.version", 17, 7)]
        };
        var first = CreateConnection(1, apiVersions);
        var second = CreateConnection(2, apiVersions);
        var metadataCalls = 0;
        first.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(CreateMetadataResponse(
                changeControllerOnRefresh && Interlocked.Increment(ref metadataCalls) > 1 ? 2 : 1)));
        second.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateMetadataResponse(2)));

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(
                callInfo.Arg<int>() == 2 ? second : first));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(first));

        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        var admin = new AdminClient(
            new AdminClientOptions
            {
                BootstrapServers = ["localhost:9092"],
                RetryBackoffMs = 1,
                RetryBackoffMaxMs = 1
            },
            pool,
            metadataManager);
        return (admin, first, second);
    }

    private static IKafkaConnection CreateConnection(
        int brokerId,
        ApiVersionsResponse apiVersions)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.BrokerId.Returns(brokerId);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9091 + brokerId);
        connection.IsConnected.Returns(true);
        connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(apiVersions));
        return connection;
    }

    private static MetadataResponse CreateMetadataResponse(int controllerId) => new()
    {
        Brokers =
        [
            new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 },
            new BrokerMetadata { NodeId = 2, Host = "localhost", Port = 9093 }
        ],
        ClusterId = "test-cluster",
        ControllerId = controllerId,
        Topics = []
    };
}
