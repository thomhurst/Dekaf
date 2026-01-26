using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks;

/// <summary>
/// Benchmarks for Dekaf serialization and protocol encoding.
/// These are micro-benchmarks that don't require Kafka.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class SerializationBenchmarks
{
    private ArrayBufferWriter<byte> _buffer = null!;
    private byte[] _readBuffer = null!;
    private string _testString = null!;
    private byte[] _testBytes = null!;

    [Params(10, 100, 1000)]
    public int StringLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new ArrayBufferWriter<byte>(4096);
        _testString = new string('a', StringLength);
        _testBytes = Encoding.UTF8.GetBytes(_testString);

        // Pre-create a read buffer for protocol reader tests
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        writer.WriteInt32(12345);
        writer.WriteInt64(9876543210L);
        writer.WriteString(_testString);
        writer.WriteCompactString(_testString);
        writer.WriteVarInt(300);
        writer.WriteVarInt(-150);
        _readBuffer = _buffer.WrittenSpan.ToArray();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _buffer.Clear();
    }

    // ===== Protocol Writer Benchmarks =====

    [Benchmark(Description = "Dekaf Writer: Int32")]
    public void WriteInt32_Dekaf()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        writer.WriteInt32(12345678);
    }

    [Benchmark(Description = "Baseline Writer: Int32")]
    public void WriteInt32_Baseline()
    {
        var span = _buffer.GetSpan(4);
        BinaryPrimitives.WriteInt32BigEndian(span, 12345678);
        _buffer.Advance(4);
    }

    [Benchmark(Description = "Dekaf Writer: Int64")]
    public void WriteInt64_Dekaf()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        writer.WriteInt64(9876543210L);
    }

    [Benchmark(Description = "Baseline Writer: Int64")]
    public void WriteInt64_Baseline()
    {
        var span = _buffer.GetSpan(8);
        BinaryPrimitives.WriteInt64BigEndian(span, 9876543210L);
        _buffer.Advance(8);
    }

    [Benchmark(Description = "Dekaf Writer: String")]
    public void WriteString_Dekaf()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        writer.WriteString(_testString);
    }

    [Benchmark(Description = "Baseline Writer: String")]
    public void WriteString_Baseline()
    {
        var byteCount = Encoding.UTF8.GetByteCount(_testString);
        var span = _buffer.GetSpan(2 + byteCount);
        BinaryPrimitives.WriteInt16BigEndian(span, (short)byteCount);
        Encoding.UTF8.GetBytes(_testString, span[2..]);
        _buffer.Advance(2 + byteCount);
    }

    [Benchmark(Description = "Dekaf Writer: CompactString")]
    public void WriteCompactString_Dekaf()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        writer.WriteCompactString(_testString);
    }

    [Benchmark(Description = "Dekaf Writer: VarInt (small)")]
    public void WriteVarIntSmall_Dekaf()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        writer.WriteVarInt(42);
    }

    [Benchmark(Description = "Dekaf Writer: VarInt (large)")]
    public void WriteVarIntLarge_Dekaf()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        writer.WriteVarInt(300);
    }

    [Benchmark(Description = "Dekaf Writer: VarInt (negative)")]
    public void WriteVarIntNegative_Dekaf()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        writer.WriteVarInt(-150);
    }

    // ===== Protocol Reader Benchmarks =====

    [Benchmark(Description = "Dekaf Reader: Int32")]
    public int ReadInt32_Dekaf()
    {
        var reader = new KafkaProtocolReader(_readBuffer);
        return reader.ReadInt32();
    }

    [Benchmark(Description = "Baseline Reader: Int32")]
    public int ReadInt32_Baseline()
    {
        return BinaryPrimitives.ReadInt32BigEndian(_readBuffer);
    }

    [Benchmark(Description = "Dekaf Reader: Int64")]
    public long ReadInt64_Dekaf()
    {
        var reader = new KafkaProtocolReader(_readBuffer);
        _ = reader.ReadInt32(); // skip int32
        return reader.ReadInt64();
    }

    [Benchmark(Description = "Dekaf Reader: String")]
    public string? ReadString_Dekaf()
    {
        var reader = new KafkaProtocolReader(_readBuffer);
        _ = reader.ReadInt32();
        _ = reader.ReadInt64();
        return reader.ReadString();
    }

    [Benchmark(Description = "Dekaf Reader: Full Sequence")]
    public (int, long, string?, string?, int, int) ReadFullSequence_Dekaf()
    {
        var reader = new KafkaProtocolReader(_readBuffer);
        var i32 = reader.ReadInt32();
        var i64 = reader.ReadInt64();
        var str = reader.ReadString();
        var compactStr = reader.ReadCompactString();
        var varInt1 = reader.ReadVarInt();
        var varInt2 = reader.ReadVarInt();
        return (i32, i64, str, compactStr, varInt1, varInt2);
    }

    // ===== Serializer Benchmarks =====

    [Benchmark(Description = "Dekaf Serializer: String")]
    public void SerializeString_Dekaf()
    {
        var serializer = Serializers.String;
        var context = new SerializationContext { Topic = "test", Component = SerializationComponent.Value };
        serializer.Serialize(_testString, ref _buffer, context);
    }

    [Benchmark(Description = "Dekaf Serializer: Int32")]
    public void SerializeInt32_Dekaf()
    {
        var serializer = Serializers.Int32;
        var context = new SerializationContext { Topic = "test", Component = SerializationComponent.Value };
        serializer.Serialize(12345678, ref _buffer, context);
    }

    [Benchmark(Description = "Dekaf Deserializer: String")]
    public string DeserializeString_Dekaf()
    {
        var serializer = Serializers.String;
        var context = new SerializationContext { Topic = "test", Component = SerializationComponent.Value };
        return serializer.Deserialize(new ReadOnlySequence<byte>(_testBytes), context);
    }

    [Benchmark(Description = "Dekaf Deserializer: Int32")]
    public int DeserializeInt32_Dekaf()
    {
        var data = new byte[] { 0x00, 0xBC, 0x61, 0x4E }; // 12345678 big-endian
        var serializer = Serializers.Int32;
        var context = new SerializationContext { Topic = "test", Component = SerializationComponent.Value };
        return serializer.Deserialize(new ReadOnlySequence<byte>(data), context);
    }

    // ===== SerializationContext Allocation Benchmarks =====

    [ThreadStatic]
    private static SerializationContext t_reuseContext;

    [Benchmark(Description = "SerializationContext: New Instance (allocates)")]
    public void SerializationContext_NewInstance()
    {
        for (int i = 0; i < 1000; i++)
        {
            // Anti-pattern: Creates new struct on each serialization
            var context = new SerializationContext
            {
                Topic = "test-topic",
                Component = SerializationComponent.Value,
                Headers = null
            };

            // Simulate passing to serializer (prevents dead code elimination)
            Serializers.String.Serialize("test", ref _buffer, context);
            _buffer.Clear();
        }
    }

    [Benchmark(Description = "SerializationContext: ThreadStatic Reuse (zero-allocation)")]
    public void SerializationContext_ThreadStaticReuse()
    {
        for (int i = 0; i < 1000; i++)
        {
            // Optimized pattern: Reuse thread-local struct by updating properties
            t_reuseContext.Topic = "test-topic";
            t_reuseContext.Component = SerializationComponent.Value;
            t_reuseContext.Headers = null;

            // Pass to serializer (struct is copied by value, so safe)
            Serializers.String.Serialize("test", ref _buffer, t_reuseContext);
            _buffer.Clear();
        }
    }
}
