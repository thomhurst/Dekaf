using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Protects ListOffsets request serialization from per-partition allocations.
/// </summary>
[MemoryDiagnoser]
[ThroughputJob(warmupCount: 3, iterationCount: 5)]
public class ListOffsetsProtocolBenchmarks
{
    private readonly ArrayBufferWriter<byte> _buffer = new(8 * 1024);
    private ListOffsetsRequest _request = null!;

    [Params((short)8, (short)9, (short)10, (short)11)]
    public short Version { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var partitions = new ListOffsetsRequestPartition[128];
        for (var partition = 0; partition < partitions.Length; partition++)
        {
            partitions[partition] = new ListOffsetsRequestPartition
            {
                PartitionIndex = partition,
                CurrentLeaderEpoch = 3,
                Timestamp = ListOffsetsTimestamp.LatestTiered
            };
        }

        _request = new ListOffsetsRequest
        {
            Topics =
            [
                new ListOffsetsRequestTopic
                {
                    Name = "benchmark-topic",
                    Partitions = partitions
                }
            ],
            TimeoutMs = 30_000
        };
    }

    [Benchmark]
    public void WriteRequest()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        _request.Write(ref writer, Version);
    }
}
