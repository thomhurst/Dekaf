using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Verifies OffsetCommit and OffsetFetch request serialization remains allocation-free.
/// Version 9 protects the existing name path; version 10 measures the topic-ID path.
/// </summary>
[MemoryDiagnoser]
[ThroughputJob(warmupCount: 3, iterationCount: 5)]
public class OffsetTopicIdProtocolBenchmarks
{
    private readonly ArrayBufferWriter<byte> _buffer = new(1024);
    private readonly OffsetCommitRequest _commitRequest = new()
    {
        GroupId = "benchmark-group",
        MemberId = "benchmark-member",
        Topics =
        [
            new OffsetCommitRequestTopic
            {
                Name = "benchmark-topic",
                Partitions =
                [
                    new OffsetCommitRequestPartition
                    {
                        PartitionIndex = 0,
                        CommittedOffset = 42,
                        CommittedLeaderEpoch = 3
                    }
                ]
            }
        ]
    };
    private readonly OffsetFetchRequest _fetchRequest = new()
    {
        GroupId = "benchmark-group",
        Groups =
        [
            new OffsetFetchRequestGroup
            {
                GroupId = "benchmark-group",
                MemberId = "benchmark-member",
                MemberEpoch = 3,
                Topics =
                [
                    new OffsetFetchRequestTopic
                    {
                        Name = "benchmark-topic",
                        PartitionIndexes = [0]
                    }
                ]
            }
        ]
    };

    [Params((short)9, (short)10)]
    public short Version { get; set; }

    [Benchmark]
    public void WriteOffsetCommitRequest()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        _commitRequest.Write(ref writer, Version);
    }

    [Benchmark]
    public void WriteOffsetFetchRequest()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        _fetchRequest.Write(ref writer, Version);
    }
}
