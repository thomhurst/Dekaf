using System.Buffers;
using Dekaf.Admin;
using Dekaf.Errors;
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

        await Assert.That(result.Select(g => g.GroupId)).IsEquivalentTo(["streams-a"]);
        await connections[1].Received(1).SendAsync<ListGroupsRequest, ListGroupsResponse>(
            Arg.Is<ListGroupsRequest>(r =>
                r != null && r.StatesFilter!.Count == 1 &&
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
                var request = callInfo.Arg<FindCoordinatorRequest>()!;
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
                var request = callInfo.Arg<StreamsGroupDescribeRequest>()!;
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
                r != null && r.IncludeAuthorizedOperations &&
                r.GroupIds.Count == 1 &&
                r.GroupIds[0] == "streams-a"),
            0,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DescribeStreamsGroupsAsync_WithTopologyDescription_SendsV1AndMapsEachGroupStatus()
    {
        var (admin, connections) = CreateAdminWithMockConnections(streamsGroupDescribeMaxVersion: 1);
        var connection = connections[1];
        StubCoordinator(connection);

        connection.SendAsync<StreamsGroupDescribeRequest, StreamsGroupDescribeResponse>(
                Arg.Any<StreamsGroupDescribeRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new StreamsGroupDescribeResponse
            {
                Groups =
                [
                    DescribedGroup("available", 3, new StreamsTopologyDescriptionData
                    {
                        Subtopologies =
                        [
                            new StreamsTopologyDescriptionSubtopologyData
                            {
                                SubtopologyId = "0",
                                Nodes =
                                [
                                    Node("KSTREAM-SOURCE-0000000000", 1, sourceTopics: ["input"], successors: ["KSTREAM-AGGREGATE-0000000001"]),
                                    Node("KSTREAM-AGGREGATE-0000000001", 2, stores: ["counts"], successors: ["KSTREAM-SINK-0000000002"]),
                                    Node("KSTREAM-SINK-0000000002", 3, sinkTopic: "output")
                                ]
                            }
                        ],
                        GlobalStores =
                        [
                            new StreamsTopologyDescriptionGlobalStoreData
                            {
                                Source = Node("KSTREAM-SOURCE-0000000003", 1, sourceTopics: ["global-input"], successors: ["KTABLE-SOURCE-0000000004"]),
                                Processor = Node("KTABLE-SOURCE-0000000004", 2, stores: ["global-store"])
                            }
                        ]
                    }),
                    DescribedGroup("failed-fetch", 2, null),
                    DescribedGroup("not-stored", 1, null)
                ]
            }));

        var descriptions = await admin.DescribeStreamsGroupsAsync(
            ["available", "failed-fetch", "not-stored"],
            new DescribeStreamsGroupsOptions { IncludeTopologyDescription = true });

        var available = descriptions["available"];
        await Assert.That(available.TopologyDescriptionStatus).IsEqualTo(StreamsGroupTopologyDescriptionStatus.Available);
        var nodes = available.TopologyDescription!.Subtopologies[0].Nodes;
        await Assert.That(nodes.Select(static n => n.NodeType)).IsEquivalentTo(
            [StreamsGroupTopologyNodeType.Source, StreamsGroupTopologyNodeType.Processor, StreamsGroupTopologyNodeType.Sink]);
        await Assert.That(nodes[0].SourceTopics).IsEquivalentTo(["input"]);
        await Assert.That(nodes[0].Successors).IsEquivalentTo(["KSTREAM-AGGREGATE-0000000001"]);
        await Assert.That(nodes[1].Stores).IsEquivalentTo(["counts"]);
        await Assert.That(nodes[1].SinkTopic).IsNull();
        await Assert.That(nodes[2].SinkTopic).IsEqualTo("output");
        var globalStore = available.TopologyDescription.GlobalStores[0];
        await Assert.That(globalStore.Source.SourceTopics).IsEquivalentTo(["global-input"]);
        await Assert.That(globalStore.Processor.Stores).IsEquivalentTo(["global-store"]);

        var failedFetch = descriptions["failed-fetch"];
        await Assert.That(failedFetch.TopologyDescriptionStatus).IsEqualTo(StreamsGroupTopologyDescriptionStatus.Error);
        await Assert.That(failedFetch.TopologyDescription).IsNull();
        await Assert.That(failedFetch.GroupState).IsEqualTo("Stable");
        await Assert.That(descriptions["not-stored"].TopologyDescriptionStatus).IsEqualTo(StreamsGroupTopologyDescriptionStatus.NotStored);
        await connection.Received(1).SendAsync<StreamsGroupDescribeRequest, StreamsGroupDescribeResponse>(
            Arg.Is<StreamsGroupDescribeRequest>(r => r != null && r.IncludeTopologyDescription),
            1,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DescribeStreamsGroupsAsync_WithoutTopologyDescription_DoesNotRequestIt()
    {
        var (admin, connections) = CreateAdminWithMockConnections(streamsGroupDescribeMaxVersion: 1);
        var connection = connections[1];
        StubCoordinator(connection);

        connection.SendAsync<StreamsGroupDescribeRequest, StreamsGroupDescribeResponse>(
                Arg.Any<StreamsGroupDescribeRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new StreamsGroupDescribeResponse
            {
                Groups = [DescribedGroup("streams-a", 0, null)]
            }));

        var descriptions = await admin.DescribeStreamsGroupsAsync(["streams-a"]);

        await Assert.That(descriptions["streams-a"].TopologyDescriptionStatus).IsEqualTo(StreamsGroupTopologyDescriptionStatus.NotRequested);
        await connection.Received(1).SendAsync<StreamsGroupDescribeRequest, StreamsGroupDescribeResponse>(
            Arg.Is<StreamsGroupDescribeRequest>(r => r != null && !r.IncludeTopologyDescription),
            1,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DescribeStreamsGroupsAsync_WithTopologyDescriptionOnV0Broker_ThrowsUnsupportedVersion()
    {
        var (admin, connections) = CreateAdminWithMockConnections(streamsGroupDescribeMaxVersion: 0);
        var connection = connections[1];
        StubCoordinator(connection);

        var exception = await Assert.That(async () => await admin.DescribeStreamsGroupsAsync(
                ["streams-a"],
                new DescribeStreamsGroupsOptions { IncludeTopologyDescription = true }))
            .Throws<KafkaException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnsupportedVersion);
        await connection.DidNotReceive().SendAsync<StreamsGroupDescribeRequest, StreamsGroupDescribeResponse>(
            Arg.Any<StreamsGroupDescribeRequest>(),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DescribeStreamsGroupsAsync_OnV1Broker_MapsEachGroupsAssignorName()
    {
        var (admin, connections) = CreateAdminWithMockConnections(streamsGroupDescribeMaxVersion: 1);
        var connection = connections[1];
        StubCoordinator(connection);

        connection.SendAsync<StreamsGroupDescribeRequest, StreamsGroupDescribeResponse>(
                Arg.Any<StreamsGroupDescribeRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new StreamsGroupDescribeResponse
            {
                Groups =
                [
                    DescribedGroup("default", 0, null, assignorName: "sticky"),
                    DescribedGroup("custom", 0, null, assignorName: "rack-balanced"),
                    DescribedGroup("unreported", 0, null)
                ]
            }));

        var descriptions = await admin.DescribeStreamsGroupsAsync(["default", "custom", "unreported"]);

        await Assert.That(descriptions["default"].AssignorName).IsEqualTo("sticky");
        await Assert.That(descriptions["custom"].AssignorName).IsEqualTo("rack-balanced");
        await Assert.That(descriptions["unreported"].AssignorName).IsNull();
        await connection.Received(1).SendAsync<StreamsGroupDescribeRequest, StreamsGroupDescribeResponse>(
            Arg.Any<StreamsGroupDescribeRequest>(),
            1,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DescribeStreamsGroupsAsync_OnV0Broker_ReportsNoAssignorName()
    {
        var (admin, connections) = CreateAdminWithMockConnections(streamsGroupDescribeMaxVersion: 0);
        var connection = connections[1];
        StubCoordinator(connection);

        connection.SendAsync<StreamsGroupDescribeRequest, StreamsGroupDescribeResponse>(
                Arg.Any<StreamsGroupDescribeRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(RoundTrip(
                new StreamsGroupDescribeResponse
                {
                    Groups = [DescribedGroup("streams-a", 0, null, assignorName: "sticky")]
                },
                callInfo.ArgAt<short>(1))));

        var descriptions = await admin.DescribeStreamsGroupsAsync(["streams-a"]);

        await Assert.That(descriptions["streams-a"].AssignorName).IsNull();
        await Assert.That(descriptions["streams-a"].TopologyDescriptionStatus).IsEqualTo(StreamsGroupTopologyDescriptionStatus.NotRequested);
        await connection.Received(1).SendAsync<StreamsGroupDescribeRequest, StreamsGroupDescribeResponse>(
            Arg.Any<StreamsGroupDescribeRequest>(),
            0,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task IncrementalAlterConfigsAsync_StreamsAssignorName_TargetsTheGroupResource()
    {
        var (admin, connections) = CreateAdminWithMockConnections();
        var requests = new List<IncrementalAlterConfigsRequest>();
        connections[1].SendAsync<IncrementalAlterConfigsRequest, IncrementalAlterConfigsResponse>(
                Arg.Any<IncrementalAlterConfigsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<IncrementalAlterConfigsRequest>();
                requests.Add(request);
                return ValueTask.FromResult(new IncrementalAlterConfigsResponse
                {
                    Responses = request.Resources.Select(static resource => new IncrementalAlterConfigsResourceResponse
                    {
                        ResourceType = resource.ResourceType,
                        ResourceName = resource.ResourceName,
                        ErrorCode = ErrorCode.None
                    }).ToArray()
                });
            });

        await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [new ConfigResource { Type = ConfigResourceType.Group, Name = "streams-a" }] =
                [ConfigAlter.Set("streams.assignor.name", "rack-balanced")]
        });

        var resource = requests.Single().Resources.Single();
        await Assert.That(resource.ResourceType).IsEqualTo((sbyte)ConfigResourceType.Group);
        await Assert.That(resource.ResourceName).IsEqualTo("streams-a");
        var change = resource.Configs.Single();
        await Assert.That(change.Name).IsEqualTo("streams.assignor.name");
        await Assert.That(change.ConfigOperation).IsEqualTo((sbyte)ConfigAlterOperation.Set);
        await Assert.That(change.Value).IsEqualTo("rack-balanced");
    }

    [Test]
    public async Task IncrementalAlterConfigsAsync_UnregisteredStreamsAssignor_PreservesInvalidConfig()
    {
        var (admin, connections) = CreateAdminWithMockConnections();
        var group = new ConfigResource { Type = ConfigResourceType.Group, Name = "streams-a" };
        const string brokerMessage = "streams.assignor.name 'missing' is not a registered task assignor. Registered assignors are: [sticky].";
        AdminClientDetailedConfigTests.Setup(connections[1], incremental: true, _ => [(group, ErrorCode.InvalidConfig, brokerMessage)]);
        var configs = new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
        {
            [group] = [ConfigAlter.Set("streams.assignor.name", "missing")]
        };

        var exception = await Assert.That(async () => await admin.IncrementalAlterConfigsAsync(configs))
            .Throws<KafkaException>();
        var detailed = await admin.IncrementalAlterConfigsDetailedAsync(configs);

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
        await Assert.That(exception.IsRetriable).IsFalse();
        await Assert.That(exception.Message).Contains(brokerMessage);
        await Assert.That(detailed[group].Outcome).IsEqualTo(AdminMutationOutcome.Failed);
        await Assert.That(detailed[group].ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
        await Assert.That(detailed[group].ErrorMessage).IsEqualTo(brokerMessage);
    }

    [Test]
    public async Task DescribeStreamsGroupsAsync_OptionsWithoutCapability_ThrowsNotSupported()
    {
        var admin = Substitute.For<IAdminClient>();

        await Assert.That(async () => await admin.DescribeStreamsGroupsAsync(["streams-a"], new DescribeStreamsGroupsOptions()))
            .Throws<NotSupportedException>();
    }

    private static void StubCoordinator(IKafkaConnection connection)
    {
        connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                Arg.Any<FindCoordinatorRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => ValueTask.FromResult(new FindCoordinatorResponse
            {
                Coordinators =
                [
                    new Coordinator
                    {
                        Key = callInfo.Arg<FindCoordinatorRequest>()!.Key,
                        NodeId = 1,
                        Host = "localhost",
                        Port = 9092,
                        ErrorCode = ErrorCode.None
                    }
                ]
            }));
    }

    private static StreamsGroupDescribeGroup DescribedGroup(
        string groupId,
        sbyte topologyDescriptionStatus,
        StreamsTopologyDescriptionData? topologyDescription,
        string? assignorName = null) => new()
    {
        ErrorCode = ErrorCode.None,
        GroupId = groupId,
        GroupState = "Stable",
        Members = [],
        TopologyDescription = topologyDescription,
        TopologyDescriptionStatus = topologyDescriptionStatus,
        AssignorName = assignorName
    };

    // Encodes and decodes at the negotiated version, so a v0 exchange drops the v1-only fields.
    private static StreamsGroupDescribeResponse RoundTrip(StreamsGroupDescribeResponse response, short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        response.Write(ref writer, version);
        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        return (StreamsGroupDescribeResponse)StreamsGroupDescribeResponse.Read(ref reader, version);
    }

    private static StreamsTopologyDescriptionNodeData Node(
        string name,
        sbyte nodeType,
        IReadOnlyList<string>? sourceTopics = null,
        string? sinkTopic = null,
        IReadOnlyList<string>? stores = null,
        IReadOnlyList<string>? successors = null) => new()
    {
        Name = name,
        NodeType = nodeType,
        SourceTopics = sourceTopics ?? [],
        SinkTopic = sinkTopic,
        Stores = stores ?? [],
        Successors = successors ?? []
    };

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

    private static (AdminClient Admin, IReadOnlyDictionary<int, IKafkaConnection> Connections) CreateAdminWithMockConnections(
        short streamsGroupDescribeMaxVersion = 0)
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
        metadataManager.SetApiVersion(ApiKey.StreamsGroupDescribe, 0, streamsGroupDescribeMaxVersion);
        metadataManager.SetApiVersion(ApiKey.IncrementalAlterConfigs, 0, 1);

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
