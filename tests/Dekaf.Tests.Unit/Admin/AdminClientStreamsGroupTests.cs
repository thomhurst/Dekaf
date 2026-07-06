using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientStreamsGroupTests
{
    [Test]
    public async Task ListStreamsGroupsAsync_FiltersByStreamsTypeAndDeduplicates()
    {
        var (admin, connections) = CreateAdminWithMockConnections();

        connections[1].SendAsync<ListGroupsRequest, ListGroupsResponse>(
                Arg.Any<ListGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ListGroupsResponse
            {
                ErrorCode = ErrorCode.None,
                Groups =
                [
                    Group("streams-a", "Streams", "Stable"),
                    Group("share-a", "Share", "Stable")
                ]
            }));

        connections[2].SendAsync<ListGroupsRequest, ListGroupsResponse>(
                Arg.Any<ListGroupsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ListGroupsResponse
            {
                ErrorCode = ErrorCode.None,
                Groups =
                [
                    Group("streams-a", "Streams", "Stable"),
                    Group("streams-b", "Streams", "Empty")
                ]
            }));

        var result = await admin.ListStreamsGroupsAsync(new ListStreamsGroupsOptions
        {
            States = ["Stable"]
        });

        await Assert.That(result.Select(g => g.GroupId)).IsEquivalentTo(["streams-a", "streams-b"]);
        await connections[1].Received(1).SendAsync<ListGroupsRequest, ListGroupsResponse>(
            Arg.Is<ListGroupsRequest>(r =>
                r.StatesFilter!.Count == 1 &&
                r.StatesFilter[0] == "Stable" &&
                r.TypesFilter!.Count == 1 &&
                r.TypesFilter[0] == "streams"),
            5,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DescribeStreamsGroupsAsync_UsesStreamsDescribeAndMapsResults()
    {
        var (admin, connections) = CreateAdminWithMockConnections();
        var connection = connections[1];

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

        connection.SendAsync<StreamsGroupDescribeRequest, StreamsGroupDescribeResponse>(
                Arg.Any<StreamsGroupDescribeRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<StreamsGroupDescribeRequest>();
                return ValueTask.FromResult(new StreamsGroupDescribeResponse
                {
                    Groups = request.GroupIds.Select(static groupId => new StreamsGroupDescribeGroup
                    {
                        ErrorCode = ErrorCode.None,
                        GroupId = groupId,
                        GroupState = "Stable",
                        GroupEpoch = 7,
                        AssignmentEpoch = 6,
                        AuthorizedOperations = 123,
                        Topology = new StreamsGroupDescribeTopology
                        {
                            Epoch = 5,
                            Subtopologies =
                            [
                                new StreamsGroupDescribeSubtopology
                                {
                                    SubtopologyId = "0",
                                    SourceTopics = ["input-a"],
                                    RepartitionSinkTopics = ["app-repartition"],
                                    StateChangelogTopics =
                                    [
                                        TopicInfo("app-store-changelog")
                                    ],
                                    RepartitionSourceTopics =
                                    [
                                        TopicInfo("app-repartition")
                                    ]
                                }
                            ]
                        },
                        Members =
                        [
                            new StreamsGroupDescribeMember
                            {
                                MemberId = "member-a",
                                MemberEpoch = 4,
                                InstanceId = "instance-a",
                                RackId = "rack-a",
                                ClientId = "client-a",
                                ClientHost = "/10.0.0.1",
                                TopologyEpoch = 5,
                                ProcessId = "process-a",
                                UserEndpoint = new StreamsGroupDescribeEndpoint
                                {
                                    Host = "host-a",
                                    Port = 7070
                                },
                                ClientTags =
                                [
                                    new StreamsGroupDescribeKeyValue { Key = "zone", Value = "a" }
                                ],
                                TaskOffsets =
                                [
                                    new StreamsGroupDescribeTaskOffset
                                    {
                                        SubtopologyId = "0",
                                        Partition = 1,
                                        Offset = 42
                                    }
                                ],
                                TaskEndOffsets =
                                [
                                    new StreamsGroupDescribeTaskOffset
                                    {
                                        SubtopologyId = "0",
                                        Partition = 1,
                                        Offset = 50
                                    }
                                ],
                                Assignment = Assignment("0", [1, 2]),
                                TargetAssignment = Assignment("0", [1, 2, 3]),
                                IsClassic = false
                            }
                        ]
                    }).ToList()
                });
            });

        var descriptions = await admin.DescribeStreamsGroupsAsync(["streams-a"]);
        var description = descriptions["streams-a"];
        var member = description.Members[0];

        await Assert.That(description.GroupEpoch).IsEqualTo(7);
        await Assert.That(description.Topology!.Subtopologies![0].StateChangelogTopics[0].TopicConfigs[0].Value).IsEqualTo("compact");
        await Assert.That(member.ProcessId).IsEqualTo("process-a");
        await Assert.That(member.UserEndpoint!.Port).IsEqualTo(7070);
        await Assert.That(member.TargetAssignment.ActiveTasks[0].Partitions).IsEquivalentTo([1, 2, 3]);
        await connection.Received(1).SendAsync<StreamsGroupDescribeRequest, StreamsGroupDescribeResponse>(
            Arg.Is<StreamsGroupDescribeRequest>(r =>
                r.IncludeAuthorizedOperations &&
                r.GroupIds.Count == 1 &&
                r.GroupIds[0] == "streams-a"),
            0,
            Arg.Any<CancellationToken>());
    }

    private static ListGroupsResponseGroup Group(string groupId, string groupType, string state) => new()
    {
        GroupId = groupId,
        GroupType = groupType,
        GroupState = state,
        ProtocolType = "streams"
    };

    private static StreamsGroupDescribeTopicInfo TopicInfo(string name) => new()
    {
        Name = name,
        Partitions = 2,
        ReplicationFactor = 3,
        TopicConfigs =
        [
            new StreamsGroupDescribeKeyValue
            {
                Key = "cleanup.policy",
                Value = "compact"
            }
        ]
    };

    private static StreamsGroupDescribeAssignment Assignment(string subtopologyId, IReadOnlyList<int> partitions) => new()
    {
        ActiveTasks =
        [
            new StreamsGroupDescribeTaskIds
            {
                SubtopologyId = subtopologyId,
                Partitions = partitions
            }
        ],
        StandbyTasks = [],
        WarmupTasks = []
    };

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
        pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connections[1]));

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
                },
                new BrokerMetadata
                {
                    NodeId = 2,
                    Host = "localhost",
                    Port = 9093
                }
            ],
            ClusterId = "test-cluster",
            ControllerId = 1,
            Topics = []
        });
        metadataManager.SetApiVersion(ApiKey.FindCoordinator, 4, 5);
        metadataManager.SetApiVersion(ApiKey.ListGroups, 5, 5);
        metadataManager.SetApiVersion(ApiKey.StreamsGroupDescribe, 0, 0);

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
        connection.Host.Returns("localhost");
        connection.Port.Returns(9091 + brokerId);
        connection.IsConnected.Returns(true);
        return connection;
    }
}
