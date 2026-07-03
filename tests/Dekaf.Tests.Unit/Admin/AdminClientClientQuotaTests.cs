using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientClientQuotaTests
{
    [Test]
    public async Task DescribeClientQuotasAsync_MapsResponseToResult()
    {
        var (admin, connection) = CreateAdminWithMockConnection();

        connection.SendAsync<DescribeClientQuotasRequest, DescribeClientQuotasResponse>(
                Arg.Any<DescribeClientQuotasRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DescribeClientQuotasResponse
            {
                ErrorCode = ErrorCode.None,
                Entries =
                [
                    new DescribeClientQuotasResponseEntry
                    {
                        Entity =
                        [
                            new DescribeClientQuotasEntityData { EntityType = "user", EntityName = "alice" },
                            new DescribeClientQuotasEntityData { EntityType = "client-id", EntityName = null }
                        ],
                        Values =
                        [
                            new DescribeClientQuotasValueData { Key = "consumer_byte_rate", Value = 1024.5 }
                        ]
                    }
                ]
            }));

        var result = await admin.DescribeClientQuotasAsync(new ClientQuotaFilter
        {
            Components = [ClientQuotaFilterComponent.Exact(ClientQuotaEntityType.User, "alice")],
            Strict = true
        });

        var entity = ClientQuotaEntity.For(
            ClientQuotaEntityComponent.ClientId(null),
            ClientQuotaEntityComponent.User("alice"));
        await Assert.That(result.TryGetValue(entity, out var quotas)).IsTrue();
        await Assert.That(quotas!["consumer_byte_rate"]).IsEqualTo(1024.5);

        await connection.Received(1).SendAsync<DescribeClientQuotasRequest, DescribeClientQuotasResponse>(
            Arg.Is<DescribeClientQuotasRequest>(r =>
                r.Strict &&
                r.Components.Count == 1 &&
                r.Components[0].EntityType == "user" &&
                r.Components[0].MatchType == 0 &&
                r.Components[0].Match == "alice"),
            Arg.Is<short>(v => v == 1),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DescribeClientQuotasAsync_DuplicateValueKeys_LastValueWins()
    {
        var (admin, connection) = CreateAdminWithMockConnection();

        connection.SendAsync<DescribeClientQuotasRequest, DescribeClientQuotasResponse>(
                Arg.Any<DescribeClientQuotasRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DescribeClientQuotasResponse
            {
                ErrorCode = ErrorCode.None,
                Entries =
                [
                    new DescribeClientQuotasResponseEntry
                    {
                        Entity = [new DescribeClientQuotasEntityData { EntityType = "user", EntityName = "alice" }],
                        Values =
                        [
                            new DescribeClientQuotasValueData { Key = "consumer_byte_rate", Value = 1024 },
                            new DescribeClientQuotasValueData { Key = "consumer_byte_rate", Value = 2048 }
                        ]
                    }
                ]
            }));

        var result = await admin.DescribeClientQuotasAsync(ClientQuotaFilter.All());

        await Assert.That(result[ClientQuotaEntity.ForUser("alice")]["consumer_byte_rate"]).IsEqualTo(2048);
    }

    [Test]
    public async Task DescribeClientQuotasAsync_TopLevelError_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection();

        connection.SendAsync<DescribeClientQuotasRequest, DescribeClientQuotasResponse>(
                Arg.Any<DescribeClientQuotasRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new DescribeClientQuotasResponse
            {
                ErrorCode = ErrorCode.InvalidRequest,
                ErrorMessage = "bad filter",
                Entries = null
            }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.DescribeClientQuotasAsync(ClientQuotaFilter.All()));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidRequest);
    }

    [Test]
    public async Task DescribeClientQuotasAsync_NullComponents_ThrowsArgumentException()
    {
        var (admin, _) = CreateAdminWithMockConnection();

        await Assert.That(async () =>
        {
            await admin.DescribeClientQuotasAsync(new ClientQuotaFilter { Components = null! });
        }).Throws<ArgumentException>();
    }

    [Test]
    public async Task DescribeClientQuotasAsync_ExactComponentWithoutMatch_ThrowsArgumentException()
    {
        var (admin, _) = CreateAdminWithMockConnection();

        await Assert.That(async () =>
        {
            await admin.DescribeClientQuotasAsync(new ClientQuotaFilter
            {
                Components =
                [
                    new ClientQuotaFilterComponent
                    {
                        EntityType = ClientQuotaEntityType.User,
                        MatchType = ClientQuotaMatchType.Exact,
                        Match = null
                    }
                ]
            });
        }).Throws<ArgumentException>();
    }

    [Test]
    public async Task DescribeClientQuotasAsync_DefaultComponentWithMatch_ThrowsArgumentException()
    {
        var (admin, _) = CreateAdminWithMockConnection();

        await Assert.That(async () =>
        {
            await admin.DescribeClientQuotasAsync(new ClientQuotaFilter
            {
                Components =
                [
                    new ClientQuotaFilterComponent
                    {
                        EntityType = ClientQuotaEntityType.User,
                        MatchType = ClientQuotaMatchType.Default,
                        Match = "alice"
                    }
                ]
            });
        }).Throws<ArgumentException>();
    }

    [Test]
    public async Task DescribeClientQuotasAsync_UnsupportedMatchType_ThrowsArgumentOutOfRangeException()
    {
        var (admin, _) = CreateAdminWithMockConnection();

        await Assert.That(async () =>
        {
            await admin.DescribeClientQuotasAsync(new ClientQuotaFilter
            {
                Components =
                [
                    new ClientQuotaFilterComponent
                    {
                        EntityType = ClientQuotaEntityType.User,
                        MatchType = (ClientQuotaMatchType)99
                    }
                ]
            });
        }).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AlterClientQuotasAsync_SendsValidateOnlyAndEntries()
    {
        var (admin, connection) = CreateAdminWithMockConnection();
        AlterClientQuotasRequest? capturedRequest = null;

        connection.SendAsync<AlterClientQuotasRequest, AlterClientQuotasResponse>(
                Arg.Any<AlterClientQuotasRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.Arg<AlterClientQuotasRequest>();
                return ValueTask.FromResult(CreateAlterSuccessResponse(capturedRequest.Entries[0].Entity));
            });

        await admin.AlterClientQuotasAsync(
            [
                ClientQuotaAlteration.Set(
                    ClientQuotaEntity.For(
                        ClientQuotaEntityComponent.User("alice"),
                        ClientQuotaEntityComponent.Ip(null)),
                    "consumer_byte_rate",
                    2048.25)
            ],
            new AlterClientQuotasOptions { ValidateOnly = true });

        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.ValidateOnly).IsTrue();
        await Assert.That(capturedRequest.Entries.Count).IsEqualTo(1);
        await Assert.That(capturedRequest.Entries[0].Entity[0].EntityType).IsEqualTo("user");
        await Assert.That(capturedRequest.Entries[0].Entity[0].EntityName).IsEqualTo("alice");
        await Assert.That(capturedRequest.Entries[0].Entity[1].EntityType).IsEqualTo("ip");
        await Assert.That(capturedRequest.Entries[0].Entity[1].EntityName).IsNull();
        await Assert.That(capturedRequest.Entries[0].Ops[0].Key).IsEqualTo("consumer_byte_rate");
        await Assert.That(capturedRequest.Entries[0].Ops[0].Value).IsEqualTo(2048.25);
        await Assert.That(capturedRequest.Entries[0].Ops[0].Remove).IsFalse();
    }

    [Test]
    public async Task AlterClientQuotasAsync_EmptyEntityComponents_ThrowsArgumentException()
    {
        var (admin, _) = CreateAdminWithMockConnection();

        await Assert.That(async () =>
        {
            await admin.AlterClientQuotasAsync(
                [
                    new ClientQuotaAlteration
                    {
                        Entity = new ClientQuotaEntity { Components = [] },
                        Operations = [ClientQuotaOperation.Set("consumer_byte_rate", 1024)]
                    }
                ]);
        }).Throws<ArgumentException>();
    }

    [Test]
    public async Task AlterClientQuotasAsync_EmptyOperations_ThrowsArgumentException()
    {
        var (admin, _) = CreateAdminWithMockConnection();

        await Assert.That(async () =>
        {
            await admin.AlterClientQuotasAsync(
                [
                    new ClientQuotaAlteration
                    {
                        Entity = ClientQuotaEntity.ForUser("alice"),
                        Operations = []
                    }
                ]);
        }).Throws<ArgumentException>();
    }

    [Test]
    public async Task AlterClientQuotasAsync_UnknownEntityType_ThrowsKafkaException()
    {
        var (admin, _) = CreateAdminWithMockConnection();

        await Assert.That(async () =>
        {
            await admin.AlterClientQuotasAsync(
                [
                    ClientQuotaAlteration.Set(
                        new ClientQuotaEntity
                        {
                            Components =
                            [
                                new ClientQuotaEntityComponent
                                {
                                    EntityType = ClientQuotaEntityType.Unknown,
                                    Name = "raw-future-entity"
                                }
                            ]
                        },
                        "consumer_byte_rate",
                        1024)
                ]);
        }).Throws<KafkaException>();
    }

    [Test]
    public async Task AlterClientQuotasAsync_Retry_ReplaysMaterializedEntries()
    {
        var (admin, connection) = CreateAdminWithMockConnection();
        var enumerationCount = 0;
        var sendCalls = 0;

        connection.SendAsync<AlterClientQuotasRequest, AlterClientQuotasResponse>(
                Arg.Any<AlterClientQuotasRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Interlocked.Increment(ref sendCalls);
                if (sendCalls == 1)
                {
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");
                }

                var request = callInfo.Arg<AlterClientQuotasRequest>();
                return ValueTask.FromResult(CreateAlterSuccessResponse(request.Entries[0].Entity));
            });

        await admin.AlterClientQuotasAsync(CreateLazyAlterations());

        await Assert.That(enumerationCount).IsEqualTo(1);
        await Assert.That(sendCalls).IsEqualTo(2);

        IEnumerable<ClientQuotaAlteration> CreateLazyAlterations()
        {
            enumerationCount++;
            yield return ClientQuotaAlteration.Remove(
                ClientQuotaEntity.ForUser("alice"),
                "consumer_byte_rate");
        }
    }

    [Test]
    public async Task AlterClientQuotasAsync_EntryError_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection();

        connection.SendAsync<AlterClientQuotasRequest, AlterClientQuotasResponse>(
                Arg.Any<AlterClientQuotasRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<AlterClientQuotasRequest>();
                return ValueTask.FromResult(new AlterClientQuotasResponse
                {
                    Entries =
                    [
                        new AlterClientQuotasResponseEntry
                        {
                            ErrorCode = ErrorCode.InvalidRequest,
                            ErrorMessage = "bad quota",
                            Entity = request.Entries[0].Entity
                        }
                    ]
                });
            });

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.AlterClientQuotasAsync(
                [ClientQuotaAlteration.Set(ClientQuotaEntity.ForUser("alice"), "consumer_byte_rate", 1024)]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidRequest);
    }

    private static AlterClientQuotasResponse CreateAlterSuccessResponse(IReadOnlyList<AlterClientQuotasEntityData> entity) => new()
    {
        Entries =
        [
            new AlterClientQuotasResponseEntry
            {
                ErrorCode = ErrorCode.None,
                Entity = entity
            }
        ]
    };

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
        metadataManager.SetApiVersion(ApiKey.DescribeClientQuotas, 0, 1);
        metadataManager.SetApiVersion(ApiKey.AlterClientQuotas, 0, 1);
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
                    new ApiVersion(ApiKey.DescribeClientQuotas, 0, 1),
                    new ApiVersion(ApiKey.AlterClientQuotas, 0, 1)
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
