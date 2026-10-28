using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Encodes a StreamsGroupTopologyDescriptionUpdate request for a three-node subtopology with a global
/// store, and decodes its response. A member sends one per topology epoch when the coordinator asks.
/// </summary>
[MemoryDiagnoser]
[ThroughputJob(warmupCount: 3, iterationCount: 5)]
public class StreamsGroupTopologyDescriptionUpdateProtocolBenchmarks
{
    private readonly ArrayBufferWriter<byte> _buffer = new(1024);
    private readonly StreamsGroupTopologyDescriptionUpdateRequest _request = new()
    {
        GroupId = "streams-benchmark",
        MemberId = "member-benchmark",
        TopologyEpoch = 3,
        TopologyDescription = new StreamsTopologyDescriptionData
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
            GlobalStores =
            [
                new StreamsTopologyDescriptionGlobalStoreData
                {
                    Source = Node("KSTREAM-SOURCE-0000000003", 1, ["global-input"], null, [], ["KTABLE-SOURCE-0000000004"]),
                    Processor = Node("KTABLE-SOURCE-0000000004", 2, [], null, ["global-store"], [])
                }
            ]
        }
    };
    private readonly byte[] _response = EncodeResponse();

    [Benchmark]
    public int WriteTopologyDescriptionUpdateV0()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        _request.Write(ref writer, version: 0);
        return _buffer.WrittenCount;
    }

    [Benchmark]
    public int ReadTopologyDescriptionUpdateResponseV0()
    {
        var reader = new KafkaProtocolReader(_response);
        var response = (StreamsGroupTopologyDescriptionUpdateResponse)
            StreamsGroupTopologyDescriptionUpdateResponse.Read(ref reader, version: 0);
        return response.ThrottleTimeMs;
    }

    private static StreamsTopologyDescriptionNodeData Node(
        string name,
        sbyte nodeType,
        string[] sourceTopics,
        string? sinkTopic,
        string[] stores,
        string[] successors) => new()
        {
            Name = name,
            NodeType = nodeType,
            SourceTopics = sourceTopics,
            SinkTopic = sinkTopic,
            Stores = stores,
            Successors = successors
        };

    private static byte[] EncodeResponse()
    {
        var buffer = new ArrayBufferWriter<byte>(16);
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteCompactNullableString(null);
        writer.WriteUnsignedVarInt(0);
        return buffer.WrittenSpan.ToArray();
    }
}
