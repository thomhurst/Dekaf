using System.Buffers;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class StreamsGroupHeartbeatMessageTests
{
    [Test]
    public async Task Request_Metadata_UsesStreamsGroupHeartbeatV0()
    {
        await Assert.That(StreamsGroupHeartbeatRequest.ApiKey).IsEqualTo(ApiKey.StreamsGroupHeartbeat);
        await Assert.That(StreamsGroupHeartbeatRequest.LowestSupportedVersion).IsEqualTo((short)0);
        await Assert.That(StreamsGroupHeartbeatRequest.HighestSupportedVersion).IsEqualTo((short)0);
    }

    [Test]
    public async Task Request_Write_MinimalChangeOnlyHeartbeat_MatchesKafkaV0Fixture()
    {
        var request = new StreamsGroupHeartbeatRequest
        {
            GroupId = "g",
            MemberId = "m",
            MemberEpoch = 1
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        request.Write(ref writer, version: 0);

        await Assert.That(Convert.ToHexString(buffer.WrittenSpan)).IsEqualTo(
            "0267026D00000001000000000000FFFFFFFFFF00000000FF0000000000");
    }

    [Test]
    public async Task Request_Write_AllNestedFields_MatchesKafkaV0Fixture()
    {
        var request = CreateFullRequest();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        request.Write(ref writer, version: 0);

        await Assert.That(Convert.ToHexString(buffer.WrittenSpan)).IsEqualTo(FullRequestFixture);
    }

    [Test]
    public async Task Response_WriteAndRead_AllFields_RoundTrips()
    {
        var original = new StreamsGroupHeartbeatResponse
        {
            ThrottleTimeMs = 12,
            ErrorCode = ErrorCode.UnknownServerError,
            ErrorMessage = "retry later",
            MemberId = "member-a",
            MemberEpoch = 4,
            HeartbeatIntervalMs = 5_000,
            AcceptableRecoveryLag = 10,
            TaskOffsetIntervalMs = 15_000,
            Status = [new StreamsGroupHeartbeatStatus { StatusCode = 1, StatusDetail = "input missing" }],
            ActiveTasks = [TaskIds("sub-0", [0, 1])],
            StandbyTasks = [TaskIds("sub-0", [2])],
            WarmupTasks = [TaskIds("sub-0", [3])],
            EndpointInformationEpoch = 9,
            PartitionsByUserEndpoint =
            [
                new StreamsGroupHeartbeatEndpointPartitions
                {
                    UserEndpoint = new StreamsGroupHeartbeatEndpoint { Host = "host-a", Port = 7070 },
                    ActivePartitions =
                    [
                        new StreamsGroupHeartbeatTopicPartitions { Topic = "input", Partitions = [0, 1] }
                    ],
                    StandbyPartitions =
                    [
                        new StreamsGroupHeartbeatTopicPartitions { Topic = "input", Partitions = [2] }
                    ]
                }
            ]
        };
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        original.Write(ref writer, version: 0);

        var reader = new KafkaProtocolReader(buffer.WrittenMemory);
        var response = (StreamsGroupHeartbeatResponse)StreamsGroupHeartbeatResponse.Read(ref reader, version: 0);
        var remaining = reader.Remaining;

        await Assert.That(response.ThrottleTimeMs).IsEqualTo(12);
        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.UnknownServerError);
        await Assert.That(response.ErrorMessage).IsEqualTo("retry later");
        await Assert.That(response.MemberId).IsEqualTo("member-a");
        await Assert.That(response.MemberEpoch).IsEqualTo(4);
        await Assert.That(response.AcceptableRecoveryLag).IsEqualTo(10);
        await Assert.That(response.Status![0].StatusDetail).IsEqualTo("input missing");
        await Assert.That(response.ActiveTasks![0].Partitions).IsEquivalentTo([0, 1]);
        await Assert.That(response.StandbyTasks![0].Partitions).IsEquivalentTo([2]);
        await Assert.That(response.WarmupTasks![0].Partitions).IsEquivalentTo([3]);
        await Assert.That(response.PartitionsByUserEndpoint![0].UserEndpoint.Port).IsEqualTo((ushort)7070);
        await Assert.That(response.PartitionsByUserEndpoint[0].ActivePartitions[0].Topic).IsEqualTo("input");
        await Assert.That(remaining).IsEqualTo(0L);
    }

    [Test]
    public async Task Response_Read_NullChangeOnlyFields_MatchesKafkaV0Fixture()
    {
        var fixture = Convert.FromHexString(
            "00000000000000026D00000001000003E80000000A0000138800000000000000020000");
        var reader = new KafkaProtocolReader(fixture);

        var response = (StreamsGroupHeartbeatResponse)StreamsGroupHeartbeatResponse.Read(ref reader, version: 0);
        var remaining = reader.Remaining;

        await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(response.ErrorMessage).IsNull();
        await Assert.That(response.MemberId).IsEqualTo("m");
        await Assert.That(response.Status).IsNull();
        await Assert.That(response.ActiveTasks).IsNull();
        await Assert.That(response.StandbyTasks).IsNull();
        await Assert.That(response.WarmupTasks).IsNull();
        await Assert.That(response.EndpointInformationEpoch).IsEqualTo(2);
        await Assert.That(response.PartitionsByUserEndpoint).IsNull();
        await Assert.That(remaining).IsEqualTo(0L);
    }

    [Test]
    public async Task Response_Read_HostileTaskCount_RejectsBeforeAllocation()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16(0);
        writer.WriteCompactNullableString(null);
        writer.WriteCompactString("member-a");
        writer.WriteInt32(1);
        writer.WriteInt32(5_000);
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(StreamsGroupHeartbeatResponse.MaxTaskCount + 2);

        var exception = Assert.Throws<MalformedProtocolDataException>(() =>
        {
            var reader = new KafkaProtocolReader(buffer.WrittenMemory);
            _ = StreamsGroupHeartbeatResponse.Read(ref reader, version: 0);
        });

        await Assert.That(exception.Message).Contains("Invalid protocol data");
    }

    private static StreamsGroupHeartbeatRequest CreateFullRequest() => new()
    {
        GroupId = "streams-a",
        MemberId = "member-a",
        MemberEpoch = 3,
        EndpointInformationEpoch = 8,
        InstanceId = "instance-a",
        RackId = "rack-a",
        RebalanceTimeoutMs = 30_000,
        Topology = new StreamsGroupHeartbeatTopology
        {
            Epoch = 7,
            Subtopologies =
            [
                new StreamsGroupHeartbeatSubtopology
                {
                    SubtopologyId = "sub-0",
                    SourceTopics = ["input"],
                    SourceTopicRegex = ["input-.*"],
                    StateChangelogTopics = [TopicInfo("store-changelog", 0)],
                    RepartitionSinkTopics = ["repartition"],
                    RepartitionSourceTopics = [TopicInfo("repartition", 6)],
                    CopartitionGroups =
                    [
                        new StreamsGroupHeartbeatCopartitionGroup
                        {
                            SourceTopics = [0],
                            SourceTopicRegex = [0],
                            RepartitionSourceTopics = [0]
                        }
                    ]
                }
            ]
        },
        ActiveTasks = [TaskIds("sub-0", [0, 1])],
        StandbyTasks = [TaskIds("sub-0", [2])],
        WarmupTasks = [TaskIds("sub-0", [3])],
        ProcessId = "process-a",
        UserEndpoint = new StreamsGroupHeartbeatEndpoint { Host = "localhost", Port = 7070 },
        ClientTags = [new StreamsGroupHeartbeatKeyValue { Key = "zone", Value = "a" }],
        TaskOffsets =
        [
            new StreamsGroupHeartbeatTaskOffset { SubtopologyId = "sub-0", Partition = 0, Offset = 42 }
        ],
        TaskEndOffsets =
        [
            new StreamsGroupHeartbeatTaskOffset { SubtopologyId = "sub-0", Partition = 0, Offset = 50 }
        ],
        ShutdownApplication = true
    };

    private static StreamsGroupHeartbeatTopicInfo TopicInfo(string name, int partitions) => new()
    {
        Name = name,
        Partitions = partitions,
        ReplicationFactor = 3,
        TopicConfigs = [new StreamsGroupHeartbeatKeyValue { Key = "cleanup.policy", Value = "compact" }]
    };

    private static StreamsGroupHeartbeatTaskIds TaskIds(string subtopologyId, IReadOnlyList<int> partitions) => new()
    {
        SubtopologyId = subtopologyId,
        Partitions = partitions
    };

    private const string FullRequestFixture =
        "0A73747265616D732D61096D656D6265722D6100000003000000080B696E7374616E63652D61077261636B2D6100007530010000000702067375622D300206696E7075740209696E7075742D2E2A021073746F72652D6368616E67656C6F67000000000003020F636C65616E75702E706F6C69637908636F6D706163740000020C7265706172746974696F6E020C7265706172746974696F6E000000060003020F636C65616E75702E706F6C69637908636F6D7061637400000202000002000002000000000002067375622D300300000000000000010002067375622D3002000000020002067375622D300200000003000A70726F636573732D61010A6C6F63616C686F73741B9E0002057A6F6E6502610002067375622D3000000000000000000000002A0002067375622D30000000000000000000000032000100";
}
