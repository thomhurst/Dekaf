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
    private readonly byte[] _steadyStateResponseV0 = EncodeSteadyStateResponse(version: 0);
    private readonly byte[] _steadyStateResponseV1 = EncodeSteadyStateResponse(version: 1);

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

    [Benchmark]
    public long ReadSteadyStateHeartbeatResponseV0() => ReadResponse(_steadyStateResponseV0, version: 0);

    [Benchmark]
    public long ReadSteadyStateHeartbeatResponseV1() => ReadResponse(_steadyStateResponseV1, version: 1);

    private static long ReadResponse(byte[] payload, short version)
    {
        var reader = new KafkaProtocolReader(payload);
        var response = (StreamsGroupHeartbeatResponse)StreamsGroupHeartbeatResponse.Read(ref reader, version);
        return response.AcceptableRecoveryLag;
    }

    // Steady-state heartbeats carry no change-only fields.
    private static byte[] EncodeSteadyStateResponse(short version)
    {
        var buffer = new ArrayBufferWriter<byte>(128);
        var writer = new KafkaProtocolWriter(buffer);
        new StreamsGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.None,
            MemberId = "member-benchmark",
            MemberEpoch = 7,
            HeartbeatIntervalMs = 5_000,
            AcceptableRecoveryLag = 10_000,
            TaskOffsetIntervalMs = 60_000,
            EndpointInformationEpoch = 4
        }.Write(ref writer, version);
        return buffer.WrittenSpan.ToArray();
    }
}
