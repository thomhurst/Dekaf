using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class TransactionResponseArrayBoundsBenchmarks
{
    private const int NodeEndpointCount = 100;
    private const int NodeEndpointMinSize = 11;

    private ReadOnlyMemory<byte> _deleteRecordsPayload;
    private ReadOnlyMemory<byte> _fetchPayload;

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

        buffer = new ArrayBufferWriter<byte>();
        writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt32(0);
        writer.WriteUnsignedVarInt(1);
        writer.WriteUnsignedVarInt(1);
        writer.WriteUnsignedVarInt(0);
        writer.WriteUnsignedVarInt(1 + (NodeEndpointCount * NodeEndpointMinSize));
        writer.WriteUnsignedVarInt(NodeEndpointCount + 1);
        for (var i = 0; i < NodeEndpointCount; i++)
        {
            writer.WriteInt32(i);
            writer.WriteCompactString(string.Empty);
            writer.WriteInt32(9092);
            writer.WriteCompactString(null);
            writer.WriteUnsignedVarInt(0);
        }

        _fetchPayload = buffer.WrittenMemory;
    }

    [Benchmark]
    public int ReadDeleteRecordsResponse()
    {
        var reader = new KafkaProtocolReader(_deleteRecordsPayload);
        var response = (DeleteRecordsResponse)DeleteRecordsResponse.Read(ref reader, version: 2);
        return response.Topics.Count;
    }

    [Benchmark]
    public int ReadFetchResponseNodeEndpoints()
    {
        var reader = new KafkaProtocolReader(_fetchPayload);
        var response = (FetchResponse)FetchResponse.Read(ref reader, version: 16);
        var count = response.NodeEndpoints.Length;
        response.ReturnToPool();
        return count;
    }
}
