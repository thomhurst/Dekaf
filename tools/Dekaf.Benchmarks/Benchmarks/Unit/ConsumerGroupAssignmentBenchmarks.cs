using System.Buffers;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Admin;
using Dekaf.Protocol;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Consumer assignment decoding, including frames with empty topic entries.</summary>
[MemoryDiagnoser]
public class ConsumerGroupAssignmentBenchmarks
{
    [Params(0, 1024)]
    public int EmptyTopics { get; set; }

    [Params(false, true)]
    public bool IncludePartition { get; set; }

    private byte[] _assignment = null!;
    private Func<byte[]?, IReadOnlyList<TopicPartition>?> _parse = null!;

    [GlobalSetup]
    public void Setup()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new KafkaProtocolWriter(buffer);
        writer.WriteInt16(0);
        writer.WriteInt32(EmptyTopics + (IncludePartition ? 1 : 0));
        for (var i = 0; i < EmptyTopics; i++)
        {
            writer.WriteString("t");
            writer.WriteInt32(0);
        }
        if (IncludePartition)
        {
            writer.WriteString("topic");
            writer.WriteInt32(1);
            writer.WriteInt32(2);
        }
        writer.WriteBytes([]);
        _assignment = buffer.WrittenSpan.ToArray();
        _parse = typeof(AdminClient).GetMethod("ParseMemberAssignment",
            BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<byte[]?, IReadOnlyList<TopicPartition>?>>();

        var result = Decode();
        if (result is null || result.Count != (IncludePartition ? 1 : 0) ||
            (IncludePartition && result[0] != new TopicPartition("topic", 2)))
            throw new InvalidOperationException("Assignment decoding must preserve every assigned partition.");
    }

    [Benchmark]
    public IReadOnlyList<TopicPartition>? Decode() => _parse(_assignment);
}
