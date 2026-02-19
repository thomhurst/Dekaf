using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Allocation benchmarks for KafkaProtocolWriter and KafkaProtocolReader.
/// These ref structs are the foundation of Dekaf's zero-allocation protocol layer.
/// All write benchmarks should show 0 bytes allocated. Read benchmarks may allocate
/// for string/byte-array results but not for numeric types.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ProtocolAllocBenchmarks
{
    private ArrayBufferWriter<byte> _buffer = null!;

    // Pre-built read data
    private byte[] _int8Data = null!;
    private byte[] _int16Data = null!;
    private byte[] _int32Data = null!;
    private byte[] _int64Data = null!;
    private byte[] _varIntSmallData = null!;
    private byte[] _varIntLargeData = null!;
    private byte[] _varLongData = null!;
    private byte[] _uuidData = null!;
    private byte[] _float64Data = null!;
    private byte[] _stringData = null!;
    private byte[] _compactStringData = null!;
    private byte[] _bytesData = null!;
    private byte[] _mixedProtocolData = null!;

    private byte[] _rawBytesData = null!;
    private string _shortString = null!;
    private string _topicName = null!;
    private string _longString = null!;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new ArrayBufferWriter<byte>(65536);
        _rawBytesData = new byte[256];
        _shortString = "key";
        _topicName = "benchmark-topic-name";
        _longString = new string('x', 500);

        // Build read data using the writer itself to ensure correctness
        _int8Data = BuildData(w => { for (var i = 0; i < 1000; i++) w.WriteInt8((sbyte)(i % 128)); });
        _int16Data = BuildData(w => { for (var i = 0; i < 1000; i++) w.WriteInt16((short)i); });
        _int32Data = BuildData(w => { for (var i = 0; i < 1000; i++) w.WriteInt32(i); });
        _int64Data = BuildData(w => { for (var i = 0; i < 1000; i++) w.WriteInt64(i); });
        _varIntSmallData = BuildData(w => { for (var i = 0; i < 1000; i++) w.WriteVarInt(i % 64); });
        _varIntLargeData = BuildData(w => { for (var i = 0; i < 1000; i++) w.WriteVarInt(i * 1000); });
        _varLongData = BuildData(w => { for (var i = 0; i < 1000; i++) w.WriteVarLong(i * 100000L); });
        _uuidData = BuildData(w => { for (var i = 0; i < 100; i++) w.WriteUuid(Guid.NewGuid()); });
        _float64Data = BuildData(w => { for (var i = 0; i < 1000; i++) w.WriteFloat64(i * 1.1); });
        _stringData = BuildData(w => { for (var i = 0; i < 100; i++) w.WriteString(_topicName); });
        _compactStringData = BuildData(w => { for (var i = 0; i < 100; i++) w.WriteCompactString(_topicName); });
        _bytesData = BuildData(w =>
        {
            var data = new byte[256];
            for (var i = 0; i < 100; i++) w.WriteBytes(data);
        });

        // Mixed protocol data simulating a real protocol message
        _mixedProtocolData = BuildData(w =>
        {
            for (var i = 0; i < 100; i++)
            {
                w.WriteInt16(3);                // API key
                w.WriteInt16(12);               // API version
                w.WriteInt32(i);                // Correlation ID
                w.WriteString("client-id");     // Client ID
                w.WriteString(_topicName);      // Topic
                w.WriteInt32(0);                // Partition
                w.WriteInt64(0);                // Offset
                w.WriteVarInt(100);             // Message size hint
            }
        });
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _buffer.Clear();
    }

    // ===== Writer: Fixed-size numeric types (must be zero-alloc) =====

    [Benchmark(Baseline = true, Description = "Write 1000 Int32s")]
    public int WriteInt32_Thousand()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteInt32(i);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 1000 Int8s")]
    public int WriteInt8_Thousand()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteInt8((sbyte)(i % 128));
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 1000 Int16s")]
    public int WriteInt16_Thousand()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteInt16((short)i);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 1000 Int64s")]
    public int WriteInt64_Thousand()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteInt64(i);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 100 UUIDs")]
    public int WriteUuid_Hundred()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        var guid = Guid.NewGuid();
        for (var i = 0; i < 100; i++)
        {
            writer.WriteUuid(guid);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 1000 Float64s")]
    public int WriteFloat64_Thousand()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteFloat64(i * 1.1);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 1000 Booleans")]
    public int WriteBoolean_Thousand()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteBoolean(i % 2 == 0);
        }
        return writer.BytesWritten;
    }

    // ===== Writer: Variable-length integers (must be zero-alloc) =====

    [Benchmark(Description = "Write 1000 VarInts (small, 0-63)")]
    public int WriteVarInt_Small()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteVarInt(i % 64);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 1000 VarInts (large)")]
    public int WriteVarInt_Large()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteVarInt(i * 1000);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 1000 VarInts (negative, ZigZag)")]
    public int WriteVarInt_Negative()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = -500; i < 500; i++)
        {
            writer.WriteVarInt(i);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 1000 VarLongs")]
    public int WriteVarLong_Thousand()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteVarLong(i * 100000L);
        }
        return writer.BytesWritten;
    }

    // ===== Writer: Strings (must be zero-alloc) =====

    [Benchmark(Description = "Write 100 Strings (short, 3 chars)")]
    public int WriteString_Short()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteString(_shortString);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 100 Strings (topic name, 20 chars)")]
    public int WriteString_TopicName()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteString(_topicName);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 100 Strings (long, 500 chars)")]
    public int WriteString_Long()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteString(_longString);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 100 CompactStrings (topic name)")]
    public int WriteCompactString_TopicName()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteCompactString(_topicName);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 100 CompactStrings (null)")]
    public int WriteCompactString_Null()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteCompactString(null);
        }
        return writer.BytesWritten;
    }

    // ===== Writer: Bytes (must be zero-alloc) =====

    [Benchmark(Description = "Write 100x RawBytes (256B)")]
    public int WriteRawBytes()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        ReadOnlySpan<byte> data = _rawBytesData;
        for (var i = 0; i < 100; i++)
        {
            writer.WriteRawBytes(data);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 100x Bytes with length prefix (256B)")]
    public int WriteBytes_WithPrefix()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        ReadOnlySpan<byte> data = _rawBytesData;
        for (var i = 0; i < 100; i++)
        {
            writer.WriteBytes(data);
        }
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write EmptyTaggedFields 1000x")]
    public int WriteEmptyTaggedFields()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteEmptyTaggedFields();
        }
        return writer.BytesWritten;
    }

    // ===== Reader: Fixed-size numeric types (must be zero-alloc) =====

    [Benchmark(Description = "Read 1000 Int8s")]
    public int ReadInt8_Thousand()
    {
        var reader = new KafkaProtocolReader(_int8Data);
        var sum = 0;
        for (var i = 0; i < 1000; i++)
        {
            sum += reader.ReadInt8();
        }
        return sum;
    }

    [Benchmark(Description = "Read 1000 Int16s")]
    public int ReadInt16_Thousand()
    {
        var reader = new KafkaProtocolReader(_int16Data);
        var sum = 0;
        for (var i = 0; i < 1000; i++)
        {
            sum += reader.ReadInt16();
        }
        return sum;
    }

    [Benchmark(Description = "Read 1000 Int32s")]
    public int ReadInt32_Thousand()
    {
        var reader = new KafkaProtocolReader(_int32Data);
        var sum = 0;
        for (var i = 0; i < 1000; i++)
        {
            sum += reader.ReadInt32();
        }
        return sum;
    }

    [Benchmark(Description = "Read 1000 Int64s")]
    public long ReadInt64_Thousand()
    {
        var reader = new KafkaProtocolReader(_int64Data);
        var sum = 0L;
        for (var i = 0; i < 1000; i++)
        {
            sum += reader.ReadInt64();
        }
        return sum;
    }

    [Benchmark(Description = "Read 100 UUIDs")]
    public Guid ReadUuid_Hundred()
    {
        var reader = new KafkaProtocolReader(_uuidData);
        var last = Guid.Empty;
        for (var i = 0; i < 100; i++)
        {
            last = reader.ReadUuid();
        }
        return last;
    }

    [Benchmark(Description = "Read 1000 Float64s")]
    public double ReadFloat64_Thousand()
    {
        var reader = new KafkaProtocolReader(_float64Data);
        var sum = 0.0;
        for (var i = 0; i < 1000; i++)
        {
            sum += reader.ReadFloat64();
        }
        return sum;
    }

    // ===== Reader: Variable-length integers (must be zero-alloc) =====

    [Benchmark(Description = "Read 1000 VarInts (small)")]
    public int ReadVarInt_Small()
    {
        var reader = new KafkaProtocolReader(_varIntSmallData);
        var sum = 0;
        for (var i = 0; i < 1000; i++)
        {
            sum += reader.ReadVarInt();
        }
        return sum;
    }

    [Benchmark(Description = "Read 1000 VarInts (large)")]
    public int ReadVarInt_Large()
    {
        var reader = new KafkaProtocolReader(_varIntLargeData);
        var sum = 0;
        for (var i = 0; i < 1000; i++)
        {
            sum += reader.ReadVarInt();
        }
        return sum;
    }

    [Benchmark(Description = "Read 1000 VarLongs")]
    public long ReadVarLong_Thousand()
    {
        var reader = new KafkaProtocolReader(_varLongData);
        var sum = 0L;
        for (var i = 0; i < 1000; i++)
        {
            sum += reader.ReadVarLong();
        }
        return sum;
    }

    // ===== Reader: Strings (allocates the string object) =====

    [Benchmark(Description = "Read 100 Strings (20 chars)")]
    public int ReadString_Hundred()
    {
        var reader = new KafkaProtocolReader(_stringData);
        var totalLength = 0;
        for (var i = 0; i < 100; i++)
        {
            var s = reader.ReadString();
            totalLength += s?.Length ?? 0;
        }
        return totalLength;
    }

    [Benchmark(Description = "Read 100 CompactStrings (20 chars)")]
    public int ReadCompactString_Hundred()
    {
        var reader = new KafkaProtocolReader(_compactStringData);
        var totalLength = 0;
        for (var i = 0; i < 100; i++)
        {
            var s = reader.ReadCompactString();
            totalLength += s?.Length ?? 0;
        }
        return totalLength;
    }

    // ===== Reader: Skip operations (must be zero-alloc) =====

    [Benchmark(Description = "Skip 1000x 4 bytes")]
    public long Skip_Thousand()
    {
        var reader = new KafkaProtocolReader(_int32Data);
        for (var i = 0; i < 1000; i++)
        {
            reader.Skip(4);
        }
        return reader.Consumed;
    }

    // ===== Mixed protocol message pattern (simulated real workload) =====

    [Benchmark(Description = "Read mixed protocol fields (100 messages)")]
    public int ReadMixedProtocol()
    {
        var reader = new KafkaProtocolReader(_mixedProtocolData);
        var sum = 0;

        for (var i = 0; i < 100; i++)
        {
            sum += reader.ReadInt16();        // API key
            sum += reader.ReadInt16();        // API version
            sum += reader.ReadInt32();        // Correlation ID
            var clientId = reader.ReadString();    // Client ID
            var topic = reader.ReadString();       // Topic
            sum += reader.ReadInt32();        // Partition
            sum += (int)reader.ReadInt64();   // Offset
            sum += reader.ReadVarInt();       // Message size hint
        }

        return sum;
    }

    // ===== Reader from different sources (allocation comparison) =====

    [Benchmark(Description = "Reader from byte[] (fast path)")]
    public int ReadFromByteArray()
    {
        var reader = new KafkaProtocolReader(_int32Data);
        var sum = 0;
        for (var i = 0; i < 100; i++)
        {
            sum += reader.ReadInt32();
        }
        return sum;
    }

    [Benchmark(Description = "Reader from ReadOnlyMemory (fast path)")]
    public int ReadFromMemory()
    {
        var reader = new KafkaProtocolReader((ReadOnlyMemory<byte>)_int32Data);
        var sum = 0;
        for (var i = 0; i < 100; i++)
        {
            sum += reader.ReadInt32();
        }
        return sum;
    }

    [Benchmark(Description = "Reader from ReadOnlySequence single-segment (fast path)")]
    public int ReadFromSequence_SingleSegment()
    {
        var reader = new KafkaProtocolReader(new ReadOnlySequence<byte>(_int32Data));
        var sum = 0;
        for (var i = 0; i < 100; i++)
        {
            sum += reader.ReadInt32();
        }
        return sum;
    }

    // ===== Helpers =====

    private static byte[] BuildData(Action<KafkaProtocolWriter> writeAction)
    {
        var buffer = new ArrayBufferWriter<byte>(65536);
        var writer = new KafkaProtocolWriter(buffer);
        writeAction(writer);
        return buffer.WrittenSpan.ToArray();
    }
}
