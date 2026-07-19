using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientAlterConsumerGroupOffsetsTests
{
    private static readonly Guid TopicId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Test]
    public async Task AlterConsumerGroupOffsetsAsync_NegotiatesHighestSupportedTopicNameVersion()
    {
        const string groupId = "test-group";
        const string topic = "test-topic";
        var connection = CreateConnection(groupId);

        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new OffsetCommitResponse
            {
                Topics =
                [
                    new OffsetCommitResponseTopic
                    {
                        Name = topic,
                        Partitions =
                        [
                            new OffsetCommitResponsePartition
                            {
                                PartitionIndex = 0,
                                ErrorCode = ErrorCode.None
                            }
                        ]
                    }
                ]
            }));

        var pool = CreatePool(connection);
        var metadataManager = CreateMetadataManager(pool, topic, TopicId);
        metadataManager.SetApiVersion(ApiKey.OffsetCommit, 2, 9);

        await using var admin = new AdminClient(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            pool,
            metadataManager);

        await admin.AlterConsumerGroupOffsetsAsync(groupId, [new TopicPartitionOffset(topic, 0, 42)]);

        await connection.Received(1).SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
            Arg.Any<OffsetCommitRequest>(),
            9,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AlterConsumerGroupOffsetsAsync_V10_UsesTopicId()
    {
        const string groupId = "test-group";
        const string topic = "test-topic";
        var connection = CreateConnection(groupId);
        OffsetCommitRequest? capturedRequest = null;

        connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
                Arg.Any<OffsetCommitRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.ArgAt<OffsetCommitRequest>(0);
                return ValueTask.FromResult(new OffsetCommitResponse
                {
                    Topics =
                    [
                        new OffsetCommitResponseTopic
                        {
                            TopicId = TopicId,
                            Partitions =
                            [
                                new OffsetCommitResponsePartition
                                {
                                    PartitionIndex = 0,
                                    ErrorCode = ErrorCode.None
                                }
                            ]
                        }
                    ]
                });
            });

        var pool = CreatePool(connection);
        var metadataManager = CreateMetadataManager(pool, topic, TopicId);
        metadataManager.SetApiVersion(ApiKey.OffsetCommit, 10, 10);

        await using var admin = new AdminClient(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            pool,
            metadataManager);

        await admin.AlterConsumerGroupOffsetsAsync(
            groupId,
            [new TopicPartitionOffset(topic, 0, 42)]);

        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.Topics[0].TopicId).IsEqualTo(TopicId);
        await connection.Received(1).SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
            Arg.Any<OffsetCommitRequest>(),
            10,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListConsumerGroupOffsetsAsync_FetchAll_FallsBackToV9()
    {
        const string groupId = "test-group";
        const string topic = "test-topic";
        var connection = CreateConnection(groupId);
        OffsetFetchRequest? capturedRequest = null;
        connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
                Arg.Any<OffsetFetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequest = callInfo.ArgAt<OffsetFetchRequest>(0);
                return ValueTask.FromResult(new OffsetFetchResponse
                {
                    Groups =
                    [
                        new OffsetFetchResponseGroup
                        {
                            GroupId = groupId,
                            ErrorCode = ErrorCode.None,
                            Topics =
                            [
                                new OffsetFetchResponseTopic
                                {
                                    Name = topic,
                                    Partitions =
                                    [
                                        new OffsetFetchResponsePartition
                                        {
                                            PartitionIndex = 0,
                                            CommittedOffset = 42,
                                            ErrorCode = ErrorCode.None
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                });
            });

        var pool = CreatePool(connection);
        var metadataManager = CreateMetadataManager(pool, topic, TopicId);
        metadataManager.SetApiVersion(ApiKey.OffsetFetch, 6, 10);

        await using var admin = new AdminClient(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            pool,
            metadataManager);

        var offsets = await admin.ListConsumerGroupOffsetsAsync(groupId);

        await Assert.That(offsets[new TopicPartition(topic, 0)]).IsEqualTo(42);
        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.Topics).IsNull();
        await connection.Received(1).SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            Arg.Any<OffsetFetchRequest>(),
            9,
            Arg.Any<CancellationToken>());
    }

    private static IKafkaConnection CreateConnection(string groupId)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.BrokerId.Returns(1);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9092);
        connection.IsConnected.Returns(true);
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
                        Key = groupId,
                        NodeId = 1,
                        Host = "localhost",
                        Port = 9092,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));
        return connection;
    }

    private static IConnectionPool CreatePool(IKafkaConnection connection)
    {
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));
        return pool;
    }

    private static MetadataManager CreateMetadataManager(
        IConnectionPool pool,
        string topic,
        Guid topicId)
    {
        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }
            ],
            ClusterId = "test-cluster",
            ControllerId = 1,
            Topics =
            [
                new TopicMetadata
                {
                    Name = topic,
                    TopicId = topicId,
                    ErrorCode = ErrorCode.None,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            PartitionIndex = 0,
                            LeaderId = 1,
                            ErrorCode = ErrorCode.None,
                            ReplicaNodes = [1],
                            IsrNodes = [1]
                        }
                    ]
                }
            ]
        });
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        return metadataManager;
    }
}
