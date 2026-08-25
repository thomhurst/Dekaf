using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Streams;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ThroughputJob(warmupCount: 3, iterationCount: 5)]
public class StreamsGroupHeartbeatProtocolBenchmarks
{
    private readonly ArrayBufferWriter<byte> _buffer = new(1024);
    private readonly StreamsGroupHeartbeatRequest _request = new()
    {
        GroupId = "streams-benchmark",
        MemberId = "member-benchmark",
        MemberEpoch = 7,
        EndpointInformationEpoch = 4,
        ActiveTasks =
        [
            new StreamsGroupHeartbeatTaskIds { SubtopologyId = "0", Partitions = [0, 1, 2, 3] }
        ],
        StandbyTasks =
        [
            new StreamsGroupHeartbeatTaskIds { SubtopologyId = "0", Partitions = [4, 5] }
        ],
        WarmupTasks = [],
        TaskOffsets =
        [
            new StreamsGroupHeartbeatTaskOffset { SubtopologyId = "0", Partition = 0, Offset = 42 }
        ]
    };
    private readonly StreamsGroupHeartbeatRequestCache _requestCache = new();

    [GlobalSetup]
    public void Setup() => GetCachedSteadyStateHeartbeat();

    [Benchmark]
    public int WriteStreamsGroupHeartbeatV0()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        _request.Write(ref writer, version: 0);
        return _buffer.WrittenCount;
    }

    [Benchmark]
    public object GetCachedSteadyStateHeartbeat() =>
        _requestCache.Get(
            _request.GroupId,
            _request.MemberId,
            _request.MemberEpoch,
            _request.EndpointInformationEpoch,
            _request.InstanceId);
}
