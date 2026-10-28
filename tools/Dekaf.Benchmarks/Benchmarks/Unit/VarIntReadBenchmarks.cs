using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Protocol;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Reads maximum-width varints and varlongs, which decode through the slow paths and reach the
/// terminal-byte overflow check. Contiguous input enters the slow path after the two-byte fast
/// path; segmented input decodes every byte through the sequence reader.
/// </summary>
[MemoryDiagnoser]
[ThroughputJob]
public class VarIntReadBenchmarks
{
    private const int Count = 1000;

    private byte[] _varIntData = null!;
    private byte[] _varLongData = null!;
    private ReadOnlySequence<byte> _segmentedVarIntData;
    private ReadOnlySequence<byte> _segmentedVarLongData;

    [GlobalSetup]
    public void Setup()
    {
        var buffer = new ArrayBufferWriter<byte>(Count * 10);
        var writer = new KafkaProtocolWriter(buffer);
        for (var i = 0; i < Count; i++)
        {
            // ZigZag maps values near MinValue to the widest encodings (5 and 10 bytes).
            writer.WriteVarInt(int.MinValue + i);
        }
        _varIntData = buffer.WrittenSpan.ToArray();

        buffer.Clear();
        writer = new KafkaProtocolWriter(buffer);
        for (var i = 0; i < Count; i++)
        {
            writer.WriteVarLong(long.MinValue + i);
        }
        _varLongData = buffer.WrittenSpan.ToArray();

        _segmentedVarIntData = CreateTwoSegmentSequence(_varIntData);
        _segmentedVarLongData = CreateTwoSegmentSequence(_varLongData);
    }

    [Benchmark(Description = "Read 1000 5-byte VarInts")]
    public int ReadMaxWidthVarInt_Contiguous()
    {
        var reader = new KafkaProtocolReader(_varIntData);
        return SumVarInts(ref reader);
    }

    [Benchmark(Description = "Read 1000 10-byte VarLongs")]
    public long ReadMaxWidthVarLong_Contiguous()
    {
        var reader = new KafkaProtocolReader(_varLongData);
        return SumVarLongs(ref reader);
    }

    [Benchmark(Description = "Read 1000 5-byte VarInts, segmented")]
    public int ReadMaxWidthVarInt_Segmented()
    {
        var reader = new KafkaProtocolReader(_segmentedVarIntData);
        return SumVarInts(ref reader);
    }

    [Benchmark(Description = "Read 1000 10-byte VarLongs, segmented")]
    public long ReadMaxWidthVarLong_Segmented()
    {
        var reader = new KafkaProtocolReader(_segmentedVarLongData);
        return SumVarLongs(ref reader);
    }

    private static int SumVarInts(ref KafkaProtocolReader reader)
    {
        var sum = 0;
        for (var i = 0; i < Count; i++)
        {
            sum += reader.ReadVarInt();
        }
        return sum;
    }

    private static long SumVarLongs(ref KafkaProtocolReader reader)
    {
        var sum = 0L;
        for (var i = 0; i < Count; i++)
        {
            sum += reader.ReadVarLong();
        }
        return sum;
    }

    private static ReadOnlySequence<byte> CreateTwoSegmentSequence(byte[] data)
    {
        var splitAt = data.Length / 2;
        var first = new BufferSegment(data.AsMemory(0, splitAt), runningIndex: 0);
        var second = new BufferSegment(data.AsMemory(splitAt), runningIndex: splitAt);
        first.SetNext(second);
        return new ReadOnlySequence<byte>(first, 0, second, second.Memory.Length);
    }

    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(ReadOnlyMemory<byte> memory, long runningIndex)
        {
            Memory = memory;
            RunningIndex = runningIndex;
        }

        public void SetNext(BufferSegment next) => Next = next;
    }
}
