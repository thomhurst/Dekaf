using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards consumer Fetch v18 against wire-size or allocation regressions relative to v16.
/// Replica-only v17/v18 tagged fields remain at their protocol defaults.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class FetchRequestSerializationBenchmarks
{
    private readonly ArrayBufferWriter<byte> _buffer = new(256);
    private readonly FetchRequest _request = new()
    {
        MaxWaitMs = 500,
        MinBytes = 1,
        MaxBytes = 1_048_576,
        SessionId = 42,
        SessionEpoch = 1,
        Topics =
        [
            new FetchRequestTopic
            {
                TopicId = new Guid("00112233-4455-6677-8899-aabbccddeeff"),
                Partitions =
                [
                    new FetchRequestPartition
                    {
                        Partition = 0,
                        CurrentLeaderEpoch = 7,
                        FetchOffset = 42,
                        LastFetchedEpoch = 7,
                        PartitionMaxBytes = 1_048_576
                    }
                ]
            }
        ],
        RackId = string.Empty
    };

    [Params((short)16, (short)18)]
    public short Version { get; set; }

    [Benchmark]
    public int WriteConsumerRequest()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        _request.Write(ref writer, Version);
        return writer.BytesWritten;
    }
}
