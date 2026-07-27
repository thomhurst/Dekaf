using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class TransactionResponseArrayBoundsBenchmarks
{
    private ReadOnlyMemory<byte> _deleteRecordsPayload;

    [GlobalSetup]
    public void Setup()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(101);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteCompactString(string.Empty);
            writer.WriteUnsignedVarInt(1);
            writer.WriteUnsignedVarInt(0);
        }
        writer.WriteUnsignedVarInt(0);
        _deleteRecordsPayload = buffer.WrittenMemory;
    }

    [Benchmark]
    public int ReadDeleteRecordsResponse()
    {
        var reader = new KafkaProtocolReader(_deleteRecordsPayload);
        var response = (DeleteRecordsResponse)DeleteRecordsResponse.Read(ref reader, version: 2);
        return response.Topics.Count;
    }
}
