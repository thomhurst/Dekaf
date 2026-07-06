using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class StreamsGroupDescribeMessageTests
{
    [Test]
    public async Task Request_Metadata_UsesStreamsGroupDescribeV0()
    {
        await Assert.That(StreamsGroupDescribeRequest.ApiKey).IsEqualTo(ApiKey.StreamsGroupDescribe);
        await Assert.That(StreamsGroupDescribeRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(StreamsGroupDescribeRequest.HighestSupportedVersion).IsEqualTo((short)0);
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
