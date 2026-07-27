using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

[MemoryDiagnoser]
public class ResponseArrayBoundsBenchmarks
{
    private ReadOnlyMemory<byte> _payload;

    [GlobalSetup]
    public void Setup()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteUnsignedVarInt(101);
        for (var i = 0; i < 100; i++)
            writer.WriteInt32(i);
        _payload = buffer.WrittenMemory;
    }

    [Benchmark(Baseline = true)]
    public int[] ReadCompactArray_Unbounded()
    {
        var reader = new KafkaProtocolReader(_payload);
        return reader.ReadCompactArray(static (ref KafkaProtocolReader r) => r.ReadInt32());
    }

    [Benchmark]
    public int[] ReadCompactArray_MinimumSizeBound()
    {
        var reader = new KafkaProtocolReader(_payload);
        return reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32(),
            minElementSize: 4,
            maxCount: int.MaxValue);
    }
}
