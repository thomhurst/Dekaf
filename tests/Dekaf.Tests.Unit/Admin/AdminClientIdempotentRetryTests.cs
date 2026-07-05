using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientIdempotentRetryTests
{
    private const string TopicName = "retry-topic";
    private const string GroupId = "retry-group";

    [Test]
    public async Task DeleteTopicsAsync_UnknownTopicOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteTopics);
        var calls = 0;

        connection.SendAsync<DeleteTopicsRequest, DeleteTopicsResponse>(
                Arg.Any<DeleteTopicsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(CreateUnknownTopicResponse());
            });

        await admin.DeleteTopicsAsync([TopicName]);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteTopicsAsync_UnknownTopicWithoutPriorSendFailure_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteTopics);
        var calls = 0;

        connection.SendAsync<DeleteTopicsRequest, DeleteTopicsResponse>(
                Arg.Any<DeleteTopicsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref calls);
                return ValueTask.FromResult(CreateUnknownTopicResponse());
            });

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.DeleteTopicsAsync([TopicName]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(calls).IsEqualTo(4);
    }

    [Test]
    public async Task DeleteConsumerGroupsAsync_GroupIdNotFoundOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteGroups);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                Arg.Any<DeleteGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(new DeleteGroupsResponse
                {
                    Results =
                    [
                        new DeleteGroupsResponseResult
                        {
                            GroupId = GroupId,
                            ErrorCode = ErrorCode.GroupIdNotFound
                        }
                    ]
                });
            });

        await admin.DeleteConsumerGroupsAsync([GroupId]);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteConsumerGroupsAsync_GroupIdNotFoundAfterNonAmbiguousRetry_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteGroups);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                Arg.Any<DeleteGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new DeleteGroupsResponse
            {
                Results =
                [
                    new DeleteGroupsResponseResult
                    {
                        GroupId = GroupId,
                        ErrorCode = Interlocked.Increment(ref calls) == 1
                            ? ErrorCode.NotCoordinator
                            : ErrorCode.GroupIdNotFound
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<GroupException>(async () =>
            await admin.DeleteConsumerGroupsAsync([GroupId]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task CreatePartitionsAsync_InvalidPartitionsOnRetry_WhenMetadataShowsTarget_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        var calls = 0;

        connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(
                Arg.Any<CreatePartitionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(new CreatePartitionsResponse
                {
                    Results =
                    [
                        new CreatePartitionsResponseResult
                        {
                            Name = TopicName,
                            ErrorCode = ErrorCode.InvalidPartitions
                        }
                    ]
                });
            });

        await admin.CreatePartitionsAsync(new Dictionary<string, int>
        {
            [TopicName] = 3
        });

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task CreatePartitionsAsync_InvalidPartitionsAfterNonAmbiguousRetry_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.CreatePartitions);
        var calls = 0;

        connection.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(
                Arg.Any<CreatePartitionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new CreatePartitionsResponse
            {
                Results =
                [
                    new CreatePartitionsResponseResult
                    {
                        Name = TopicName,
                        ErrorCode = Interlocked.Increment(ref calls) == 1
                            ? ErrorCode.NotController
                            : ErrorCode.InvalidPartitions
                    }
                ]
            }));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await admin.CreatePartitionsAsync(new Dictionary<string, int>
            {
                [TopicName] = 3
            }));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidPartitions);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteConsumerGroupOffsetsAsync_GroupIdNotFoundOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.OffsetDelete);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(new OffsetDeleteResponse
                {
                    ErrorCode = ErrorCode.GroupIdNotFound,
                    Topics = []
                });
            });

        await admin.DeleteConsumerGroupOffsetsAsync(GroupId, [new TopicPartition(TopicName, 0)]);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteConsumerGroupOffsetsAsync_GroupIdNotFoundAfterNonAmbiguousRetry_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.OffsetDelete);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
                Arg.Any<OffsetDeleteRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new OffsetDeleteResponse
            {
                ErrorCode = Interlocked.Increment(ref calls) == 1
                    ? ErrorCode.NotCoordinator
                    : ErrorCode.GroupIdNotFound,
                Topics = []
            }));

        var exception = await Assert.ThrowsAsync<GroupException>(async () =>
            await admin.DeleteConsumerGroupOffsetsAsync(GroupId, [new TopicPartition(TopicName, 0)]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteShareGroupOffsetsAsync_GroupIdNotFoundOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteShareGroupOffsets);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<DeleteShareGroupOffsetsRequest, DeleteShareGroupOffsetsResponse>(
                Arg.Any<DeleteShareGroupOffsetsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(new DeleteShareGroupOffsetsResponse
                {
                    ErrorCode = ErrorCode.GroupIdNotFound,
                    Responses = []
                });
            });

        await admin.DeleteShareGroupOffsetsAsync(GroupId, [TopicName]);

        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task DeleteShareGroupOffsetsAsync_GroupIdNotFoundAfterNonAmbiguousRetry_Throws()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.DeleteShareGroupOffsets);
        SetupFindCoordinator(connection);
        var calls = 0;

        connection.SendAsync<DeleteShareGroupOffsetsRequest, DeleteShareGroupOffsetsResponse>(
                Arg.Any<DeleteShareGroupOffsetsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromResult(new DeleteShareGroupOffsetsResponse
            {
                ErrorCode = Interlocked.Increment(ref calls) == 1
                    ? ErrorCode.NotCoordinator
                    : ErrorCode.GroupIdNotFound,
                Responses = []
            }));

        var exception = await Assert.ThrowsAsync<GroupException>(async () =>
            await admin.DeleteShareGroupOffsetsAsync(GroupId, [TopicName]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test]
    public async Task ElectLeadersAsync_ElectionNotNeededOnRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.ElectLeaders);
        var calls = 0;

        connection.SendAsync<ElectLeadersRequest, ElectLeadersResponse>(
                Arg.Any<ElectLeadersRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new KafkaException(ErrorCode.RequestTimedOut, "simulated timeout");

                return ValueTask.FromResult(new ElectLeadersResponse
                {
                    ErrorCode = ErrorCode.None,
                    ReplicaElectionResults =
                    [
                        new ElectLeadersResponseTopic
                        {
                            Topic = TopicName,
                            PartitionResult =
                            [
                                new ElectLeadersResponsePartition
                                {
                                    PartitionId = 0,
                                    ErrorCode = ErrorCode.ElectionNotNeeded,
                                    ErrorMessage = "already leader"
                                }
                            ]
                        }
                    ]
                });
            });

        var results = await admin.ElectLeadersAsync(
            ElectionType.Preferred,
            [new TopicPartition(TopicName, 0)]);

        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(results[new TopicPartition(TopicName, 0)].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[new TopicPartition(TopicName, 0)].ErrorMessage).IsNull();
    }

    [Test]
    public async Task ElectLeadersAsync_ElectionNotNeededWithoutPriorRetry_TreatedAsSuccess()
    {
        var (admin, connection) = CreateAdminWithMockConnection(ApiKey.ElectLeaders);

        connection.SendAsync<ElectLeadersRequest, ElectLeadersResponse>(
                Arg.Any<ElectLeadersRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ElectLeadersResponse
            {
                ErrorCode = ErrorCode.None,
                ReplicaElectionResults =
                [
                    new ElectLeadersResponseTopic
                    {
                        Topic = TopicName,
                        PartitionResult =
                        [
                            new ElectLeadersResponsePartition
                            {
                                PartitionId = 0,
                                ErrorCode = ErrorCode.ElectionNotNeeded,
                                ErrorMessage = "already leader"
                            }
                        ]
                    }
                ]
            }));

        var results = await admin.ElectLeadersAsync(
            ElectionType.Preferred,
            [new TopicPartition(TopicName, 0)]);

        await Assert.That(results[new TopicPartition(TopicName, 0)].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[new TopicPartition(TopicName, 0)].ErrorMessage).IsNull();
    }

    private static void SetupFindCoordinator(IKafkaConnection connection)
    {
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator
                    {
                        Key = GroupId,
                        NodeId = 1,
                        Host = "localhost",
                        Port = 9092,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));
    }

    private static DeleteTopicsResponse CreateUnknownTopicResponse() => new()
    {
        Responses =
        [
            new DeleteTopicsResponseTopic
            {
                Name = TopicName,
                ErrorCode = ErrorCode.UnknownTopicOrPartition
            }
        ]
    };

    private static (AdminClient Admin, IKafkaConnection Connection) CreateAdminWithMockConnection(
        params ApiKey[] extraApiKeys)
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
        metadataManager.SetApiVersion(ApiKey.Metadata, 9, 13);
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);

        foreach (var apiKey in extraApiKeys)
            metadataManager.SetApiVersion(apiKey, 0, 99);

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
                ApiKeys = extraApiKeys
                    .Append(ApiKey.Metadata)
                    .Append(ApiKey.FindCoordinator)
                    .Distinct()
                    .Select(apiKey => new ApiVersion(apiKey, 0, 99))
                    .ToList()
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
        Topics =
        [
            new TopicMetadata
            {
                Name = TopicName,
                ErrorCode = ErrorCode.None,
                Partitions =
                [
                    new PartitionMetadata
                    {
                        PartitionIndex = 0,
                        LeaderId = 1,
                        LeaderEpoch = 1,
                        ReplicaNodes = [1],
                        IsrNodes = [1],
                        OfflineReplicas = [],
                        ErrorCode = ErrorCode.None
                    },
                    new PartitionMetadata
                    {
                        PartitionIndex = 1,
                        LeaderId = 1,
                        LeaderEpoch = 1,
                        ReplicaNodes = [1],
                        IsrNodes = [1],
                        OfflineReplicas = [],
                        ErrorCode = ErrorCode.None
                    },
                    new PartitionMetadata
                    {
                        PartitionIndex = 2,
                        LeaderId = 1,
                        LeaderEpoch = 1,
                        ReplicaNodes = [1],
                        IsrNodes = [1],
                        OfflineReplicas = [],
                        ErrorCode = ErrorCode.None
                    }
                ]
            }
        ]
    };
}
