using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Parses StreamsGroupDescribe responses: v0, v1 without a requested topology description, and v1
/// carrying a KIP-1331 topology description.
/// </summary>
[MemoryDiagnoser]
[ThroughputJob(warmupCount: 3, iterationCount: 5)]
public class StreamsGroupDescribeProtocolBenchmarks
{
    private readonly byte[] _responseV0 = Encode(version: 0, includeDescription: false);
    private readonly byte[] _responseV1 = Encode(version: 1, includeDescription: false);
    private readonly byte[] _responseV1WithDescription = Encode(version: 1, includeDescription: true);

    [Benchmark(Baseline = true)]
    public int ReadDescribeResponseV0() => Read(_responseV0, version: 0);

    [Benchmark]
    public int ReadDescribeResponseV1() => Read(_responseV1, version: 1);

    [Benchmark]
    public int ReadDescribeResponseV1WithTopologyDescription() => Read(_responseV1WithDescription, version: 1);

    private static int Read(byte[] payload, short version)
    {
        var reader = new KafkaProtocolReader(payload);
        var response = (StreamsGroupDescribeResponse)StreamsGroupDescribeResponse.Read(ref reader, version);
        return response.Groups[0].Members.Count + response.Groups[0].TopologyDescriptionStatus;
    }

    private static byte[] Encode(short version, bool includeDescription)
    {
        var buffer = new ArrayBufferWriter<byte>(4096);
        var writer = new KafkaProtocolWriter(buffer);
        new StreamsGroupDescribeResponse
        {
            Groups =
            [
                new StreamsGroupDescribeGroup
                {
                    GroupId = "streams-benchmark",
                    GroupState = "Stable",
                    GroupEpoch = 7,
                    AssignmentEpoch = 7,
                    AuthorizedOperations = 8,
                    Topology = new StreamsGroupDescribeTopology
                    {
                        Epoch = 3,
                        Subtopologies =
                        [
                            new StreamsGroupDescribeSubtopology
                            {
                                SubtopologyId = "0",
                                SourceTopics = ["input"],
                                RepartitionSinkTopics = [],
                                StateChangelogTopics = [],
                                RepartitionSourceTopics = []
                            }
                        ]
                    },
                    Members = [Member("member-a", [0, 1]), Member("member-b", [2, 3])],
                    TopologyDescription = includeDescription ? Description() : null,
                    TopologyDescriptionStatus = includeDescription ? (sbyte)3 : (sbyte)0,
                    AssignorName = "sticky"
                }
            ]
        }.Write(ref writer, version);
        return buffer.WrittenSpan.ToArray();
    }

    private static StreamsGroupDescribeMember Member(string memberId, IReadOnlyList<int> partitions)
    {
        var assignment = new StreamsGroupDescribeAssignment
        {
            ActiveTasks = [new StreamsGroupDescribeTaskIds { SubtopologyId = "0", Partitions = partitions }],
            StandbyTasks = [],
            WarmupTasks = []
        };

        return new StreamsGroupDescribeMember
        {
            MemberId = memberId,
            MemberEpoch = 7,
            ClientId = "client",
            ClientHost = "/10.0.0.1",
            TopologyEpoch = 3,
            ProcessId = "process",
            ClientTags = [],
            TaskOffsets = [],
            TaskEndOffsets = [],
            Assignment = assignment,
            TargetAssignment = assignment
        };
    }

    private static StreamsTopologyDescriptionData Description() => new()
    {
        Subtopologies =
        [
            new StreamsTopologyDescriptionSubtopologyData
            {
                SubtopologyId = "0",
                Nodes =
                [
                    Node("KSTREAM-SOURCE-0000000000", 1, ["input"], null, [], ["KSTREAM-AGGREGATE-0000000001"]),
                    Node("KSTREAM-AGGREGATE-0000000001", 2, [], null, ["counts"], ["KSTREAM-SINK-0000000002"]),
                    Node("KSTREAM-SINK-0000000002", 3, [], "output", [], [])
                ]
            }
        ],
        GlobalStores = []
    };

    private static StreamsTopologyDescriptionNodeData Node(
        string name,
        sbyte nodeType,
        IReadOnlyList<string> sourceTopics,
        string? sinkTopic,
        IReadOnlyList<string> stores,
        IReadOnlyList<string> successors) => new()
    {
        Name = name,
        NodeType = nodeType,
        SourceTopics = sourceTopics,
        SinkTopic = sinkTopic,
        Stores = stores,
        Successors = successors
    };
}
