using Dekaf.Admin;
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

    private static DescribeTransactionsResponse CreateDescribeTransactionsResponse(
        DescribeTransactionsRequest request,
        long producerId)
    {
        return new DescribeTransactionsResponse
        {
            TransactionStates = request.TransactionalIds.Select(transactionalId => new DescribeTransactionsResponseState
            {
                ErrorCode = ErrorCode.None,
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
        long producerId)
    {
        return new DescribeProducersResponse
        {
            Topics = request.Topics.Select(topic => new DescribeProducersResponseTopic
            {
                Name = topic.Name,
                Partitions = topic.PartitionIndexes.Select(partition => new DescribeProducersResponsePartition
                {
                    PartitionIndex = partition,
                    ErrorCode = ErrorCode.None,
                    ActiveProducers =
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
                    ]
                }).ToList()
            }).ToList()
        };
    }

    private static (AdminClient Admin, IReadOnlyDictionary<int, IKafkaConnection> Connections) CreateAdminWithMockConnections()
    {
        var connections = new Dictionary<int, IKafkaConnection>
        {
            [1] = CreateConnection(1),
            [2] = CreateConnection(2)
        };

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(connections[callInfo.ArgAt<int>(0)]));

        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
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
        });

        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadataManager.SetApiVersion(ApiKey.ListTransactions, 0, 2);
        metadataManager.SetApiVersion(ApiKey.DescribeTransactions, 0, 0);
        metadataManager.SetApiVersion(ApiKey.DescribeProducers, 0, 0);

        var admin = new AdminClient(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            pool,
            metadataManager);

        return (admin, connections);
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
}
