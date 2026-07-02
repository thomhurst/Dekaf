using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientConsumerGroupDescribeTests
{
    [Test]
    public async Task DescribeConsumerGroupsAsync_FallsBackPerGroupWhenApi69ReportsClassicGroup()
    {
        var (admin, connection) = CreateAdminWithMockConnection();

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
                            NodeId = 1,
                            Host = "localhost",
                            Port = 9092,
                            ErrorCode = ErrorCode.None
                        }
                    ]
                });
            });

        connection.SendAsync<ConsumerGroupDescribeRequest, ConsumerGroupDescribeResponse>(
                Arg.Any<ConsumerGroupDescribeRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ConsumerGroupDescribeRequest>();
                return ValueTask.FromResult(new ConsumerGroupDescribeResponse
                {
                    Groups = request.GroupIds.Select(groupId => groupId == "classic-group"
                        ? new ConsumerGroupDescribeGroup
                        {
                            ErrorCode = ErrorCode.GroupIdNotFound,
                            ErrorMessage = "Group is not a consumer protocol group.",
                            GroupId = groupId,
                            GroupState = "",
                            GroupEpoch = -1,
                            AssignmentEpoch = -1,
                            AssignorName = "",
                            Members = []
                        }
                        : new ConsumerGroupDescribeGroup
                        {
                            ErrorCode = ErrorCode.None,
                            GroupId = groupId,
                            GroupState = "Stable",
                            GroupEpoch = 3,
                            AssignmentEpoch = 4,
                            AssignorName = "range",
                            AuthorizedOperations = 123,
                            Members = []
                        }).ToList()
                });
            });

        connection.SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
                Arg.Any<DescribeGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<DescribeGroupsRequest>();
                return ValueTask.FromResult(new DescribeGroupsResponse
                {
                    Groups = request.Groups.Select(groupId => new DescribeGroupsResponseGroup
                    {
                        ErrorCode = ErrorCode.None,
                        GroupId = groupId,
                        GroupState = "Stable",
                        ProtocolType = "consumer",
                        ProtocolData = "cooperative-sticky",
                        AuthorizedOperations = 456,
                        Members = []
                    }).ToList()
                });
            });

        var descriptions = await admin.DescribeConsumerGroupsAsync(["consumer-group", "classic-group"]);

        await Assert.That(descriptions).ContainsKey("consumer-group");
        await Assert.That(descriptions).ContainsKey("classic-group");
        await Assert.That(descriptions["consumer-group"].GroupEpoch).IsEqualTo(3);
        await Assert.That(descriptions["consumer-group"].AssignorName).IsEqualTo("range");
        await Assert.That(descriptions["classic-group"].GroupEpoch).IsNull();
        await Assert.That(descriptions["classic-group"].ProtocolData).IsEqualTo("cooperative-sticky");

        await connection.Received(1).SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
            Arg.Is<DescribeGroupsRequest>(r => r.Groups.Count == 1 && r.Groups[0] == "classic-group"),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
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
        metadataManager.SetApiVersion(ApiKey.ConsumerGroupDescribe, 0, 1);
        metadataManager.SetApiVersion(ApiKey.DescribeGroups, 5, 5);

        var admin = new AdminClient(
            new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
            pool,
            metadataManager);

        return (admin, connection);
    }
}
