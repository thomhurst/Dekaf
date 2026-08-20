using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
[ShortRunJob]
public class ProduceResponseParsingBenchmarks
{
    private readonly byte[] _legacyResponse = CreateResponse(version: 8);
    private readonly byte[] _flexibleResponse = CreateResponse(version: 9);

    [GlobalSetup]
    public void WarmPoolsAndTopicCache()
    {
        ParseAndReturn(_legacyResponse, version: 8);
        ParseAndReturn(_flexibleResponse, version: 9);
    }

    [Benchmark]
    public long ParseLegacyV8() => ParseAndReturn(_legacyResponse, version: 8);

    [Benchmark(Baseline = true)]
    public long ParseFlexibleV9() => ParseAndReturn(_flexibleResponse, version: 9);

    private static long ParseAndReturn(byte[] payload, short version)
    {
        var reader = new KafkaProtocolReader(payload.AsSpan());
        var response = (ProduceResponse)ProduceResponse.Read(ref reader, version);
        var offset = response.Responses[0].PartitionResponses[0].BaseOffset;
        response.Return();
        return offset;
    }

    private static byte[] CreateResponse(short version)
    {
        var flexible = ProduceRequest.IsFlexibleVersion(version);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);

        if (flexible)
            writer.WriteUnsignedVarInt(2);
        else
            writer.WriteInt32(1);

        if (flexible)
            writer.WriteCompactString("produce-benchmark-topic");
        else
            writer.WriteString("produce-benchmark-topic");

        if (flexible)
            writer.WriteUnsignedVarInt(2);
        else
            writer.WriteInt32(1);

        writer.WriteInt32(0);
        writer.WriteInt16((short)ErrorCode.None);
        writer.WriteInt64(42);
        writer.WriteInt64(-1);
        writer.WriteInt64(0);
        if (flexible)
        {
            writer.WriteUnsignedVarInt(0);
            writer.WriteUnsignedVarInt(0);
            writer.WriteEmptyTaggedFields();
            writer.WriteEmptyTaggedFields();
        }
        else
        {
            writer.WriteInt32(-1);
            writer.WriteInt16(-1);
        }

        writer.WriteInt32(0);
        if (flexible)
            writer.WriteEmptyTaggedFields();

        return buffer.WrittenSpan.ToArray();
    }
}
