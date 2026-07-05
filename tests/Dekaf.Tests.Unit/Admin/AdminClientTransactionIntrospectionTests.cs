using System.Reflection;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientTransactionIntrospectionTests
{
    [Test]
    public async Task ListTransactionsAsync_FansOutToAllBrokersAndMergesResults()
    {
        var (admin, connections) = CreateAdminWithMockConnections();

        connections[1].SendAsync<ListTransactionsRequest, ListTransactionsResponse>(
                Arg.Any<ListTransactionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ListTransactionsResponse
            {
                ErrorCode = ErrorCode.None,
                UnknownStateFilters = ["UnknownState"],
                TransactionStates =
                [
                    new ListTransactionsResponseState
                    {
                        TransactionalId = "tx-a",
                        ProducerId = 101,
                        TransactionState = "Ongoing"
                    }
                ]
            }));

        connections[2].SendAsync<ListTransactionsRequest, ListTransactionsResponse>(
                Arg.Any<ListTransactionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ListTransactionsResponse
            {
                ErrorCode = ErrorCode.None,
                UnknownStateFilters = [],
                TransactionStates =
                [
                    new ListTransactionsResponseState
                    {
                        TransactionalId = "tx-b",
                        ProducerId = 202,
                        TransactionState = "PrepareCommit"
                    },
                    new ListTransactionsResponseState
                    {
                        TransactionalId = "tx-a",
                        ProducerId = 999,
                        TransactionState = "Ongoing"
                    }
                ]
            }));

        var result = await admin.ListTransactionsAsync(new ListTransactionsOptions
        {
            StateFilters = ["Ongoing"],
            ProducerIdFilters = [101],
            DurationFilterMs = 1000,
            TransactionalIdPattern = "tx-.*"
        });

        await Assert.That(result.UnknownStateFilters).IsEquivalentTo(["UnknownState"]);
        await Assert.That(result.Transactions.Select(t => t.TransactionalId)).IsEquivalentTo(["tx-a", "tx-b"]);
        await Assert.That(result.Transactions.Count(t => t.TransactionalId == "tx-a")).IsEqualTo(1);
        await Assert.That(result.Transactions.Single(t => t.TransactionalId == "tx-a").CoordinatorId).IsEqualTo(1);
        await Assert.That(result.Transactions.Single(t => t.TransactionalId == "tx-b").CoordinatorId).IsEqualTo(2);

        await connections[1].Received(1).SendAsync<ListTransactionsRequest, ListTransactionsResponse>(
            Arg.Is<ListTransactionsRequest>(r =>
                r.StateFilters!.Count == 1 &&
                r.StateFilters[0] == "Ongoing" &&
                r.ProducerIdFilters!.Count == 1 &&
                r.ProducerIdFilters[0] == 101 &&
                r.DurationFilterMs == 1000 &&
                r.TransactionalIdPattern == "tx-.*"),
            2,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListTransactionsAsync_DurationFilterOnV0Broker_ThrowsBrokerVersionException()
    {
        var (admin, _) = CreateAdminWithMockConnections(listTransactionsMaxVersion: 0);

        await Assert.That(async () =>
        {
            await admin.ListTransactionsAsync(new ListTransactionsOptions { DurationFilterMs = 1000 });
        }).Throws<BrokerVersionException>();
    }

    [Test]
    public async Task ListTransactionsAsync_TransactionalIdPatternOnV1Broker_ThrowsBrokerVersionException()
    {
        var (admin, _) = CreateAdminWithMockConnections(listTransactionsMaxVersion: 1);

        await Assert.That(async () =>
        {
            await admin.ListTransactionsAsync(new ListTransactionsOptions { TransactionalIdPattern = "tx-.*" });
        }).Throws<BrokerVersionException>();
    }

    [Test]
    public async Task ListTransactionsAsync_WhenApiKeyMissing_ThrowsBrokerVersionException()
    {
        var (admin, _) = CreateAdminWithMockConnections(includeListTransactionsApi: false);

        await Assert.That(async () =>
        {
            await admin.ListTransactionsAsync();
        }).Throws<BrokerVersionException>();
    }

    [Test]
    public async Task DescribeTransactionsAsync_UsesTransactionCoordinatorsAndMapsDescriptions()
    {
        var (admin, connections) = CreateAdminWithMockConnections();

        connections[1].SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<FindCoordinatorRequest>();
                var coordinatorId = request.Key == "tx-b" ? 2 : 1;

                return ValueTask.FromResult(new FindCoordinatorResponse
                {
                    Coordinators =
                    [
                        new Coordinator
                        {
                            Key = request.Key,
                            NodeId = coordinatorId,
                            Host = $"broker-{coordinatorId}",
                            Port = 9090 + coordinatorId,
                            ErrorCode = ErrorCode.None
                        }
                    ]
                });
            });

        connections[1].SendAsync<DescribeTransactionsRequest, DescribeTransactionsResponse>(
                Arg.Any<DescribeTransactionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(CreateDescribeTransactionsResponse(callInfo.Arg<DescribeTransactionsRequest>(), 101)));

        connections[2].SendAsync<DescribeTransactionsRequest, DescribeTransactionsResponse>(
                Arg.Any<DescribeTransactionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(CreateDescribeTransactionsResponse(callInfo.Arg<DescribeTransactionsRequest>(), 202)));

        var descriptions = await admin.DescribeTransactionsAsync(["tx-a", "tx-b"]);

        await Assert.That(descriptions.Keys).IsEquivalentTo(["tx-a", "tx-b"]);
        await Assert.That(descriptions["tx-a"].CoordinatorId).IsEqualTo(1);
        await Assert.That(descriptions["tx-b"].CoordinatorId).IsEqualTo(2);
        await Assert.That(descriptions["tx-a"].TopicPartitions).IsEquivalentTo(
            [new TopicPartition("orders", 0), new TopicPartition("orders", 1)]);

        await connections[1].Received(1).SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            Arg.Is<FindCoordinatorRequest>(r => r.Key == "tx-a" && r.KeyType == CoordinatorType.Transaction),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DescribeTransactionsAsync_WhenApiKeyMissing_ThrowsBrokerVersionException()
    {
        var (admin, _) = CreateAdminWithMockConnections(includeDescribeTransactionsApi: false);

        await Assert.That(async () =>
        {
            await admin.DescribeTransactionsAsync([]);
        }).Throws<BrokerVersionException>();
    }

    [Test]
    public async Task DescribeTransactionsAsync_EmptyInput_ReturnsEmptyResult()
    {
        var (admin, _) = CreateAdminWithMockConnections();

        var result = await admin.DescribeTransactionsAsync([]);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task DescribeTransactionsAsync_MetadataRefreshItemError_ThrowsKafkaException()
    {
        var (admin, connections) = CreateAdminWithMockConnections();
        SetupTransactionCoordinatorLookup(connections[1], coordinatorId: 1);
        connections[1].SendAsync<DescribeTransactionsRequest, DescribeTransactionsResponse>(
                Arg.Any<DescribeTransactionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateDescribeTransactionsResponse(
                new DescribeTransactionsRequest { TransactionalIds = ["tx-a"] },
                producerId: 101,
                errorCode: ErrorCode.BrokerNotAvailable)));

        await Assert.That(async () =>
        {
            await admin.DescribeTransactionsAsync(["tx-a"]);
        }).Throws<KafkaException>();
    }

    [Test]
    public async Task DescribeTransactionsAsync_NonRetriableItemError_ReturnsResultError()
    {
        var (admin, connections) = CreateAdminWithMockConnections();
        SetupTransactionCoordinatorLookup(connections[1], coordinatorId: 1);
        connections[1].SendAsync<DescribeTransactionsRequest, DescribeTransactionsResponse>(
                Arg.Any<DescribeTransactionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateDescribeTransactionsResponse(
                new DescribeTransactionsRequest { TransactionalIds = ["tx-a"] },
                producerId: 101,
                errorCode: ErrorCode.TransactionalIdAuthorizationFailed)));

        var result = await admin.DescribeTransactionsAsync(["tx-a"]);

        await Assert.That(result["tx-a"].ErrorCode).IsEqualTo(ErrorCode.TransactionalIdAuthorizationFailed);
    }

    [Test]
    public async Task DescribeProducersAsync_GroupsPartitionsByLeaderAndMapsActiveProducers()
    {
        var (admin, connections) = CreateAdminWithMockConnections();

        connections[1].SendAsync<DescribeProducersRequest, DescribeProducersResponse>(
                Arg.Any<DescribeProducersRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(CreateDescribeProducersResponse(callInfo.Arg<DescribeProducersRequest>(), 101)));

        connections[2].SendAsync<DescribeProducersRequest, DescribeProducersResponse>(
                Arg.Any<DescribeProducersRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(CreateDescribeProducersResponse(callInfo.Arg<DescribeProducersRequest>(), 202)));

        var result = await admin.DescribeProducersAsync(
            [new TopicPartition("orders", 0), new TopicPartition("orders", 1)]);

        await Assert.That(result[new TopicPartition("orders", 0)].ActiveProducers[0].ProducerId).IsEqualTo(101);
        await Assert.That(result[new TopicPartition("orders", 1)].ActiveProducers[0].ProducerId).IsEqualTo(202);

        await connections[1].Received(1).SendAsync<DescribeProducersRequest, DescribeProducersResponse>(
            Arg.Is<DescribeProducersRequest>(r =>
                r.Topics.Count == 1 &&
                r.Topics[0].Name == "orders" &&
                r.Topics[0].PartitionIndexes.Count == 1 &&
                r.Topics[0].PartitionIndexes[0] == 0),
            0,
            Arg.Any<CancellationToken>());

        await connections[2].Received(1).SendAsync<DescribeProducersRequest, DescribeProducersResponse>(
            Arg.Is<DescribeProducersRequest>(r =>
                r.Topics.Count == 1 &&
                r.Topics[0].Name == "orders" &&
                r.Topics[0].PartitionIndexes.Count == 1 &&
                r.Topics[0].PartitionIndexes[0] == 1),
            0,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DescribeProducersAsync_WhenApiKeyMissing_ThrowsBrokerVersionException()
    {
        var (admin, _) = CreateAdminWithMockConnections(includeDescribeProducersApi: false);

        await Assert.That(async () =>
        {
            await admin.DescribeProducersAsync([]);
        }).Throws<BrokerVersionException>();
    }

    [Test]
    public async Task DescribeProducersAsync_EmptyInput_ReturnsEmptyResult()
    {
        var (admin, _) = CreateAdminWithMockConnections();

        var result = await admin.DescribeProducersAsync([]);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task DescribeProducersAsync_MetadataRefreshPartitionError_ThrowsKafkaException()
    {
        var (admin, connections) = CreateAdminWithMockConnections();
        connections[1].SendAsync<DescribeProducersRequest, DescribeProducersResponse>(
                Arg.Any<DescribeProducersRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(CreateDescribeProducersResponse(
                callInfo.Arg<DescribeProducersRequest>(),
                producerId: 101,
                errorCode: ErrorCode.BrokerNotAvailable)));

        await Assert.That(async () =>
        {
            await admin.DescribeProducersAsync([new TopicPartition("orders", 0)]);
        }).Throws<KafkaException>();
    }

    [Test]
    public async Task DescribeProducersAsync_NonRetriablePartitionError_ReturnsResultError()
    {
        var (admin, connections) = CreateAdminWithMockConnections();
        connections[1].SendAsync<DescribeProducersRequest, DescribeProducersResponse>(
                Arg.Any<DescribeProducersRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(CreateDescribeProducersResponse(
                callInfo.Arg<DescribeProducersRequest>(),
                producerId: 101,
                errorCode: ErrorCode.TopicAuthorizationFailed,
                errorMessage: "denied")));

        var result = await admin.DescribeProducersAsync([new TopicPartition("orders", 0)]);

        await Assert.That(result[new TopicPartition("orders", 0)].ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(result[new TopicPartition("orders", 0)].ErrorMessage).IsEqualTo("denied");
        await Assert.That(result[new TopicPartition("orders", 0)].ActiveProducers).IsEmpty();
    }

    [Test]
    public async Task DescribeProducersAsync_LeaderNotAvailable_ThrowsKafkaException()
    {
        var (admin, _) = CreateAdminWithMockConnections();

        await Assert.That(async () =>
        {
            await admin.DescribeProducersAsync([new TopicPartition("orders", 9)]);
        }).Throws<KafkaException>();
    }

    [Test]
    public async Task FenceProducersAsync_UsesTransactionCoordinatorsAndMapsProducerIds()
    {
        var (admin, connections) = CreateAdminWithMockConnections();

        connections[1].SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<FindCoordinatorRequest>();
                var coordinatorId = request.Key == "tx-b" ? 2 : 1;

                return ValueTask.FromResult(new FindCoordinatorResponse
                {
                    Coordinators =
                    [
                        new Coordinator
                        {
                            Key = request.Key,
                            NodeId = coordinatorId,
                            Host = $"broker-{coordinatorId}",
                            Port = 9090 + coordinatorId,
                            ErrorCode = ErrorCode.None
                        }
                    ]
                });
            });

        connections[1].SendAsync<InitProducerIdRequest, InitProducerIdResponse>(
                Arg.Any<InitProducerIdRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new InitProducerIdResponse
            {
                ErrorCode = ErrorCode.None,
                ProducerId = 101,
                ProducerEpoch = 3
            }));

        connections[2].SendAsync<InitProducerIdRequest, InitProducerIdResponse>(
                Arg.Any<InitProducerIdRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new InitProducerIdResponse
            {
                ErrorCode = ErrorCode.None,
                ProducerId = 202,
                ProducerEpoch = 4
            }));

        var result = await admin.FenceProducersAsync(
            ["tx-a", "tx-b"],
            new FenceProducersOptions { TimeoutMs = 12345 });

        await Assert.That(result["tx-a"].ProducerId).IsEqualTo(101);
        await Assert.That(result["tx-a"].ProducerEpoch).IsEqualTo((short)3);
        await Assert.That(result["tx-b"].ProducerId).IsEqualTo(202);
        await Assert.That(result["tx-b"].ProducerEpoch).IsEqualTo((short)4);

        await connections[1].Received(1).SendAsync<InitProducerIdRequest, InitProducerIdResponse>(
            Arg.Is<InitProducerIdRequest>(r =>
                r.TransactionalId == "tx-a" &&
                r.TransactionTimeoutMs == 12345 &&
                r.ProducerId == -1 &&
                r.ProducerEpoch == -1),
            5,
            Arg.Any<CancellationToken>());

        await connections[2].Received(1).SendAsync<InitProducerIdRequest, InitProducerIdResponse>(
            Arg.Is<InitProducerIdRequest>(r =>
                r.TransactionalId == "tx-b" &&
                r.TransactionTimeoutMs == 12345 &&
                r.ProducerId == -1 &&
                r.ProducerEpoch == -1),
            5,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FenceProducersAsync_ConcurrentTransactions_Retries()
    {
        var (admin, connections) = CreateAdminWithMockConnections();
        SetupTransactionCoordinatorLookup(connections[1], coordinatorId: 1);
        var attempts = 0;

        connections[1].SendAsync<InitProducerIdRequest, InitProducerIdResponse>(
                Arg.Any<InitProducerIdRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                return ValueTask.FromResult(new InitProducerIdResponse
                {
                    ErrorCode = attempts == 1 ? ErrorCode.ConcurrentTransactions : ErrorCode.None,
                    ProducerId = attempts == 1 ? -1 : 101,
                    ProducerEpoch = attempts == 1 ? (short)-1 : (short)3
                });
            });

        var result = await admin.FenceProducersAsync(["tx-a"]);

        await Assert.That(result["tx-a"].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(result["tx-a"].ProducerId).IsEqualTo(101);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task FenceProducersAsync_NonRetriableItemError_ReturnsResultError()
    {
        var (admin, connections) = CreateAdminWithMockConnections();
        SetupTransactionCoordinatorLookup(connections[1], coordinatorId: 1);
        connections[1].SendAsync<InitProducerIdRequest, InitProducerIdResponse>(
                Arg.Any<InitProducerIdRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new InitProducerIdResponse
            {
                ErrorCode = ErrorCode.TransactionalIdAuthorizationFailed
            }));

        var result = await admin.FenceProducersAsync(["tx-a"]);

        await Assert.That(result["tx-a"].ErrorCode).IsEqualTo(ErrorCode.TransactionalIdAuthorizationFailed);
    }

    [Test]
    public async Task FenceProducersAsync_WhenApiKeyMissing_ThrowsBrokerVersionException()
    {
        var (admin, _) = CreateAdminWithMockConnections(includeInitProducerIdApi: false);

        await Assert.That(async () =>
        {
            await admin.FenceProducersAsync(["tx-a"]);
        }).Throws<BrokerVersionException>();
    }

    [Test]
    public async Task AbortTransactionAsync_SendsAbortMarkerToPartitionLeader()
    {
        var (admin, connections) = CreateAdminWithMockConnections();

        connections[2].SendAsync<WriteTxnMarkersRequest, WriteTxnMarkersResponse>(
                Arg.Any<WriteTxnMarkersRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(CreateWriteTxnMarkersResponse(callInfo.Arg<WriteTxnMarkersRequest>())));

        var result = await admin.AbortTransactionAsync(new AbortTransactionSpec
        {
            TopicPartition = new TopicPartition("orders", 1),
            ProducerId = 202,
            ProducerEpoch = 4,
            CoordinatorEpoch = 5
        });

        await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.None);

        await connections[2].Received(1).SendAsync<WriteTxnMarkersRequest, WriteTxnMarkersResponse>(
            Arg.Is<WriteTxnMarkersRequest>(r =>
                r.Markers.Count == 1 &&
                r.Markers[0].ProducerId == 202 &&
                r.Markers[0].ProducerEpoch == 4 &&
                !r.Markers[0].TransactionResult &&
                r.Markers[0].CoordinatorEpoch == 5 &&
                r.Markers[0].Topics.Count == 1 &&
                r.Markers[0].Topics[0].Name == "orders" &&
                r.Markers[0].Topics[0].PartitionIndexes.Count == 1 &&
                r.Markers[0].Topics[0].PartitionIndexes[0] == 1),
            2,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AbortTransactionAsync_NonRetriablePartitionError_ReturnsResultError()
    {
        var (admin, connections) = CreateAdminWithMockConnections();
        connections[1].SendAsync<WriteTxnMarkersRequest, WriteTxnMarkersResponse>(
                Arg.Any<WriteTxnMarkersRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(CreateWriteTxnMarkersResponse(
                callInfo.Arg<WriteTxnMarkersRequest>(),
                ErrorCode.InvalidProducerEpoch)));

        var result = await admin.AbortTransactionAsync(new AbortTransactionSpec
        {
            TopicPartition = new TopicPartition("orders", 0),
            ProducerId = 101,
            ProducerEpoch = 3,
            CoordinatorEpoch = 5
        });

        await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.InvalidProducerEpoch);
    }

    [Test]
    public async Task AbortTransactionAsync_WhenApiKeyMissing_ThrowsBrokerVersionException()
    {
        var (admin, _) = CreateAdminWithMockConnections(includeWriteTxnMarkersApi: false);

        await Assert.That(async () =>
        {
            await admin.AbortTransactionAsync(new AbortTransactionSpec
            {
                TopicPartition = new TopicPartition("orders", 0),
                ProducerId = 101,
                ProducerEpoch = 3,
                CoordinatorEpoch = 5
            });
        }).Throws<BrokerVersionException>();
    }

    private static DescribeTransactionsResponse CreateDescribeTransactionsResponse(
        DescribeTransactionsRequest request,
        long producerId,
        ErrorCode errorCode = ErrorCode.None)
    {
        return new DescribeTransactionsResponse
        {
            TransactionStates = request.TransactionalIds.Select(transactionalId => new DescribeTransactionsResponseState
            {
                ErrorCode = errorCode,
                TransactionalId = transactionalId,
                TransactionState = "Ongoing",
                TransactionTimeoutMs = 60000,
                TransactionStartTimeMs = 1700000000000,
                ProducerId = producerId,
                ProducerEpoch = 3,
                Topics =
                [
                    new DescribeTransactionsResponseTopic
                    {
                        Topic = "orders",
                        Partitions = [0, 1]
                    }
                ]
            }).ToList()
        };
    }

    private static DescribeProducersResponse CreateDescribeProducersResponse(
        DescribeProducersRequest request,
        long producerId,
        ErrorCode errorCode = ErrorCode.None,
        string? errorMessage = null)
    {
        return new DescribeProducersResponse
        {
            Topics = request.Topics.Select(topic => new DescribeProducersResponseTopic
            {
                Name = topic.Name,
                Partitions = topic.PartitionIndexes.Select(partition => new DescribeProducersResponsePartition
                {
                    PartitionIndex = partition,
                    ErrorCode = errorCode,
                    ErrorMessage = errorMessage,
                    ActiveProducers = CreateActiveProducers(errorCode, producerId)
                }).ToList()
            }).ToList()
        };
    }

    private static IReadOnlyList<DescribeProducersResponseProducer> CreateActiveProducers(
        ErrorCode errorCode,
        long producerId)
    {
        if (errorCode != ErrorCode.None)
            return [];

        return
        [
            new DescribeProducersResponseProducer
            {
                ProducerId = producerId,
                ProducerEpoch = 4,
                LastSequence = 10,
                LastTimestamp = 1700000000000,
                CoordinatorEpoch = 5,
                CurrentTxnStartOffset = 99
            }
        ];
    }

    private static WriteTxnMarkersResponse CreateWriteTxnMarkersResponse(
        WriteTxnMarkersRequest request,
        ErrorCode errorCode = ErrorCode.None)
    {
        return new WriteTxnMarkersResponse
        {
            Markers = request.Markers.Select(marker => new WriteTxnMarkersResponseMarker
            {
                ProducerId = marker.ProducerId,
                Topics = marker.Topics.Select(topic => new WriteTxnMarkersResponseTopic
                {
                    Name = topic.Name,
                    Partitions = topic.PartitionIndexes.Select(partition => new WriteTxnMarkersResponsePartition
                    {
                        PartitionIndex = partition,
                        ErrorCode = errorCode
                    }).ToList()
                }).ToList()
            }).ToList()
        };
    }

    private static (AdminClient Admin, IReadOnlyDictionary<int, IKafkaConnection> Connections) CreateAdminWithMockConnections(
        bool includeListTransactionsApi = true,
        bool includeDescribeTransactionsApi = true,
        bool includeDescribeProducersApi = true,
        bool includeInitProducerIdApi = true,
        bool includeWriteTxnMarkersApi = true,
        short listTransactionsMaxVersion = 2)
    {
        var connections = new Dictionary<int, IKafkaConnection>
        {
            [1] = CreateConnection(1),
            [2] = CreateConnection(2)
        };

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(connections[callInfo.ArgAt<int>(0)]));
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connections[1]));

        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.Metadata.Update(CreateMetadataResponse());
        SetInstanceField(metadataManager, "_metadataApiVersion", MetadataRequest.HighestSupportedVersion);

        connections[1].SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateMetadataResponse()));

        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadataManager.SetApiVersion(ApiKey.Metadata, MetadataRequest.LowestSupportedVersion, MetadataRequest.HighestSupportedVersion);
        if (includeListTransactionsApi)
            metadataManager.SetApiVersion(ApiKey.ListTransactions, 0, listTransactionsMaxVersion);
        if (includeDescribeTransactionsApi)
            metadataManager.SetApiVersion(ApiKey.DescribeTransactions, 0, 0);
        if (includeDescribeProducersApi)
            metadataManager.SetApiVersion(ApiKey.DescribeProducers, 0, 0);
        if (includeInitProducerIdApi)
            metadataManager.SetApiVersion(ApiKey.InitProducerId, 2, 5);
        if (includeWriteTxnMarkersApi)
            metadataManager.SetApiVersion(ApiKey.WriteTxnMarkers, 1, 2);

        var admin = new AdminClient(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            pool,
            metadataManager);

        return (admin, connections);
    }

    private static void SetupTransactionCoordinatorLookup(IKafkaConnection connection, int coordinatorId)
    {
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<FindCoordinatorRequest>();

                return ValueTask.FromResult(new FindCoordinatorResponse
                {
                    Coordinators =
                    [
                        new Coordinator
                        {
                            Key = request.Key,
                            NodeId = coordinatorId,
                            Host = $"broker-{coordinatorId}",
                            Port = 9090 + coordinatorId,
                            ErrorCode = ErrorCode.None
                        }
                    ]
                });
            });
    }

    private static MetadataResponse CreateMetadataResponse()
    {
        return new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9091 },
                new BrokerMetadata { NodeId = 2, Host = "broker-2", Port = 9092 }
            ],
            ClusterId = "test-cluster",
            ControllerId = 1,
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = "orders",
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 1,
                            ReplicaNodes = [1, 2],
                            IsrNodes = [1, 2]
                        },
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 1,
                            LeaderId = 2,
                            ReplicaNodes = [1, 2],
                            IsrNodes = [1, 2]
                        }
                    ]
                }
            ]
        };
    }

    private static IKafkaConnection CreateConnection(int brokerId)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.BrokerId.Returns(brokerId);
        connection.Host.Returns($"broker-{brokerId}");
        connection.Port.Returns(9090 + brokerId);
        connection.IsConnected.Returns(true);
        return connection;
    }

    private static void SetInstanceField<T>(object target, string name, T value)
    {
        const BindingFlags instanceFieldFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var field = target.GetType().GetField(name, instanceFieldFlags);
        field!.SetValue(target, value);
    }
}
