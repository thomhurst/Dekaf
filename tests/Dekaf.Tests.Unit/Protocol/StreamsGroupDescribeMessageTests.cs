using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class StreamsGroupDescribeMessageTests
{
    [Test]
    public async Task Request_Metadata_SupportsStreamsGroupDescribeV0ToV1()
    {
        await Assert.That(StreamsGroupDescribeRequest.ApiKey).IsEqualTo(ApiKey.StreamsGroupDescribe);
        await Assert.That(StreamsGroupDescribeRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(StreamsGroupDescribeRequest.HighestSupportedVersion).IsEqualTo((short)1);
        await Assert.That(StreamsGroupDescribeResponse.HighestSupportedVersion).IsEqualTo((short)1);
    }

    [Test]
    public async Task Request_Write_EncodesGroupsAndAuthorizedOperations()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var request = new StreamsGroupDescribeRequest
        {
            GroupIds = ["streams-a", "streams-b"],
            IncludeAuthorizedOperations = true
        };

        request.Write(ref writer, version: 0);

        await Assert.That(buffer.WrittenCount).IsGreaterThan(0);
        await Assert.That(buffer.WrittenSpan[^2]).IsEqualTo((byte)1);
        await Assert.That(buffer.WrittenSpan[^1]).IsEqualTo((byte)0);
    }

    [Test]
    public async Task Response_WriteAndRead_WithTopologyAndMember_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new StreamsGroupDescribeResponse
        {
            ThrottleTimeMs = 10,
            Groups =
            [
                new StreamsGroupDescribeGroup
                {
                    ErrorCode = ErrorCode.None,
                    GroupId = "streams-a",
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
                                RepartitionSinkTopics = ["streams-a-repartition"],
                                StateChangelogTopics =
                                [
                                    TopicInfo("streams-a-store-changelog")
                                ],
                                RepartitionSourceTopics =
                                [
                                    TopicInfo("streams-a-repartition")
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
                }
            ]
        };

        original.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (StreamsGroupDescribeResponse)StreamsGroupDescribeResponse.Read(ref reader, version: 0);
        var group = response.Groups[0];
        var topology = group.Topology!;
        var member = group.Members[0];

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(10);
        await Assert.That(group.GroupId).IsEqualTo("streams-a");
        await Assert.That(group.GroupEpoch).IsEqualTo(7);
        await Assert.That(group.AuthorizedOperations).IsEqualTo(123);
        await Assert.That(topology.Epoch).IsEqualTo(5);
        await Assert.That(topology.Subtopologies![0].StateChangelogTopics[0].TopicConfigs[0].Key).IsEqualTo("cleanup.policy");
        await Assert.That(member.MemberId).IsEqualTo("member-a");
        await Assert.That(member.UserEndpoint!.Host).IsEqualTo("host-a");
        await Assert.That(member.TaskOffsets[0].Offset).IsEqualTo(42);
        await Assert.That(member.TargetAssignment.ActiveTasks[0].Partitions).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Response_WriteAndRead_NullTopologyAndEndpoint_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        var original = new StreamsGroupDescribeGroup
        {
            ErrorCode = ErrorCode.GroupIdNotFound,
            ErrorMessage = "missing",
            GroupId = "streams-missing",
            GroupState = "",
            GroupEpoch = -1,
            AssignmentEpoch = -1,
            Topology = null,
            Members =
            [
                new StreamsGroupDescribeMember
                {
                    MemberId = "member-a",
                    MemberEpoch = 1,
                    ClientId = "client-a",
                    ClientHost = "/10.0.0.1",
                    TopologyEpoch = 1,
                    ProcessId = "process-a",
                    UserEndpoint = null,
                    ClientTags = [],
                    TaskOffsets = [],
                    TaskEndOffsets = [],
                    Assignment = EmptyAssignment(),
                    TargetAssignment = EmptyAssignment(),
                    IsClassic = true
                }
            ]
        };

        original.Write(ref writer);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var deserialized = StreamsGroupDescribeGroup.Read(ref reader);

        await Assert.That(deserialized.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(deserialized.ErrorMessage).IsEqualTo("missing");
        await Assert.That(deserialized.Topology).IsNull();
        await Assert.That(deserialized.Members[0].UserEndpoint).IsNull();
        await Assert.That(deserialized.Members[0].IsClassic).IsTrue();
    }

    [Test]
    public async Task Topology_WriteAndRead_NullSubtopologies_RoundTrips()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        StreamsGroupDescribeTopology.WriteNullable(ref writer, new StreamsGroupDescribeTopology
        {
            Epoch = 3,
            Subtopologies = null
        });

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var topology = StreamsGroupDescribeTopology.ReadNullable(ref reader);

        await Assert.That(topology).IsNotNull();
        await Assert.That(topology!.Epoch).IsEqualTo(3);
        await Assert.That(topology.Subtopologies).IsNull();
    }

    [Test]
    public async Task Request_WriteV1_AppendsIncludeTopologyDescription()
    {
        var request = new StreamsGroupDescribeRequest
        {
            GroupIds = ["g"],
            IncludeAuthorizedOperations = false,
            IncludeTopologyDescription = true
        };

        var v0 = WriteRequest(request, version: 0);
        var v1 = WriteRequest(request, version: 1);

        // GroupIds, IncludeAuthorizedOperations, [IncludeTopologyDescription (v1+)], tagged fields.
        await Assert.That(v0).IsEquivalentTo(new byte[] { 2, 2, (byte)'g', 0, 0 });
        await Assert.That(v1).IsEquivalentTo(new byte[] { 2, 2, (byte)'g', 0, 1, 0 });
    }

    [Test]
    public async Task Response_ReadV1_DecodesTopologyDescriptionStatusAndAssignor()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        CreateV1Response().Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (StreamsGroupDescribeResponse)StreamsGroupDescribeResponse.Read(ref reader, version: 1);
        var available = response.Groups[0];
        var description = available.TopologyDescription!;
        var nodes = description.Subtopologies[0].Nodes;
        var remaining = reader.Remaining;

        await Assert.That(remaining).IsEqualTo(0);
        await Assert.That(available.AuthorizedOperations).IsEqualTo(8);
        await Assert.That(available.TopologyDescriptionStatus).IsEqualTo((sbyte)3);
        await Assert.That(available.AssignorName).IsEqualTo("sticky");
        await Assert.That(nodes.Select(static n => n.NodeType)).IsEquivalentTo(new sbyte[] { 1, 2, 3 });
        await Assert.That(nodes[0].SourceTopics).IsEquivalentTo(["input"]);
        await Assert.That(nodes[0].SinkTopic).IsNull();
        await Assert.That(nodes[1].Stores).IsEquivalentTo(["counts"]);
        await Assert.That(nodes[1].Successors).IsEquivalentTo(["sink"]);
        await Assert.That(nodes[2].SinkTopic).IsEqualTo("output");
        await Assert.That(description.GlobalStores[0].Source.Name).IsEqualTo("global-source");
        await Assert.That(description.GlobalStores[0].Processor.Stores).IsEquivalentTo(["global-store"]);

        var failed = response.Groups[1];
        await Assert.That(failed.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        await Assert.That(failed.TopologyDescription).IsNull();
        await Assert.That(failed.TopologyDescriptionStatus).IsEqualTo((sbyte)0);
        await Assert.That(failed.AssignorName).IsNull();
    }

    [Test]
    [Arguments((sbyte)0)]
    [Arguments((sbyte)1)]
    [Arguments((sbyte)2)]
    public async Task Response_ReadV1_StatusWithoutDescription_DecodesNullDescription(sbyte status)
    {
        var group = MinimalGroup("streams-a", status, description: null, assignorName: "sticky");
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        group.Write(ref writer, version: 1);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var decoded = StreamsGroupDescribeGroup.Read(ref reader, version: 1);

        await Assert.That(decoded.TopologyDescriptionStatus).IsEqualTo(status);
        await Assert.That(decoded.TopologyDescription).IsNull();
        await Assert.That(decoded.AssignorName).IsEqualTo("sticky");
    }

    [Test]
    public async Task Response_WriteV0_OmitsV1Fields()
    {
        var group = MinimalGroup("streams-a", 3, CreateDescription(), "sticky");

        var v0 = WriteGroup(group, version: 0);
        var v1 = WriteGroup(group, version: 1);

        var reader = new KafkaProtocolReader(v0);
        var decoded = StreamsGroupDescribeGroup.Read(ref reader, version: 0);
        var remaining = reader.Remaining;

        await Assert.That(v1.Length).IsGreaterThan(v0.Length);
        await Assert.That(remaining).IsEqualTo(0);
        await Assert.That(decoded.TopologyDescription).IsNull();
        await Assert.That(decoded.TopologyDescriptionStatus).IsEqualTo((sbyte)0);
        await Assert.That(decoded.AssignorName).IsNull();
    }

    private static byte[] WriteRequest(StreamsGroupDescribeRequest request, short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        request.Write(ref writer, version);
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] WriteGroup(StreamsGroupDescribeGroup group, short version)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        group.Write(ref writer, version);
        return buffer.WrittenSpan.ToArray();
    }

    private static StreamsGroupDescribeResponse CreateV1Response() => new()
    {
        Groups =
        [
            MinimalGroup("streams-a", 3, CreateDescription(), "sticky"),
            new StreamsGroupDescribeGroup
            {
                ErrorCode = ErrorCode.GroupIdNotFound,
                ErrorMessage = "missing",
                GroupId = "streams-missing",
                GroupState = "",
                Members = []
            }
        ]
    };

    private static StreamsGroupDescribeGroup MinimalGroup(
        string groupId,
        sbyte status,
        StreamsTopologyDescriptionData? description,
        string? assignorName) => new()
    {
        GroupId = groupId,
        GroupState = "Stable",
        Members = [],
        AuthorizedOperations = 8,
        TopologyDescription = description,
        TopologyDescriptionStatus = status,
        AssignorName = assignorName
    };

    private static StreamsTopologyDescriptionData CreateDescription() => new()
    {
        Subtopologies =
        [
            new StreamsTopologyDescriptionSubtopologyData
            {
                SubtopologyId = "0",
                Nodes =
                [
                    Node("source", 1, sourceTopics: ["input"], successors: ["process"]),
                    Node("process", 2, stores: ["counts"], successors: ["sink"]),
                    Node("sink", 3, sinkTopic: "output")
                ]
            }
        ],
        GlobalStores =
        [
            new StreamsTopologyDescriptionGlobalStoreData
            {
                Source = Node("global-source", 1, sourceTopics: ["global-input"], successors: ["global-processor"]),
                Processor = Node("global-processor", 2, stores: ["global-store"])
            }
        ]
    };

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

    private static StreamsGroupDescribeAssignment EmptyAssignment() => new()
    {
        ActiveTasks = [],
        StandbyTasks = [],
        WarmupTasks = []
    };
}
