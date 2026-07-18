using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientAlterConsumerGroupOffsetsTests
{
    [Test]
    public async Task AlterConsumerGroupOffsetsAsync_NegotiatesHighestSupportedTopicNameVersion()
    {
        const string groupId = "test-group";
        const string topic = "test-topic";
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

        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
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
        });
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
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
}
