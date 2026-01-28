using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Zero-allocation protocol benchmarks for Kafka wire protocol operations.
/// These benchmarks verify Dekaf's allocation-free design.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ProtocolBenchmarks
{
    private ArrayBufferWriter<byte> _buffer = null!;
    private byte[] _int32Data = null!;
    private byte[] _varIntData = null!;
    private byte[] _recordBatchBytes = null!;
    private string _testString = null!;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new ArrayBufferWriter<byte>(65536);
        _testString = new string('a', 100);

        // Pre-create int32 data for reading
        var tempBuffer = new ArrayBufferWriter<byte>(4096);
        var writer = new KafkaProtocolWriter(tempBuffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteInt32(i);
        }
        _int32Data = tempBuffer.WrittenSpan.ToArray();

        // Pre-create varint data for reading
        tempBuffer.Clear();
        writer = new KafkaProtocolWriter(tempBuffer);
        for (var i = -500; i < 500; i++)
        {
            writer.WriteVarInt(i);
        }
        _varIntData = tempBuffer.WrittenSpan.ToArray();

        // Create a sample record batch
        tempBuffer.Clear();
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            MaxTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastOffsetDelta = 9,
            Records = Enumerable.Range(0, 10).Select(i => new Record
            {
                TimestampDelta = i,
                OffsetDelta = i,
                Key = System.Text.Encoding.UTF8.GetBytes($"key-{i}"),
                Value = System.Text.Encoding.UTF8.GetBytes($"value-{i}-{new string('x', 100)}")
            }).ToList()
        };
        batch.Write(tempBuffer);
        _recordBatchBytes = tempBuffer.WrittenSpan.ToArray();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _buffer.Clear();
    }

    // ===== Write Operations =====

    [Benchmark(Description = "Write 1000 Int32s")]
    public void WriteInt32_Thousand()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteInt32(i);
        }
    }

    [Benchmark(Description = "Write 100 Strings (100 chars)")]
    public void WriteString_Hundred()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteString(_testString);
        }
    }

    [Benchmark(Description = "Write 100 CompactStrings")]
    public void WriteCompactString_Hundred()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteCompactString(_testString);
        }
    }

    [Benchmark(Description = "Write 1000 VarInts")]
    public void WriteVarInt_Thousand()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = -500; i < 500; i++)
        {
            writer.WriteVarInt(i);
        }
    }

    // ===== Read Operations =====

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

    [Benchmark(Description = "Read 1000 VarInts")]
    public int ReadVarInt_Thousand()
    {
        var reader = new KafkaProtocolReader(_varIntData);
        var sum = 0;
        for (var i = 0; i < 1000; i++)
        {
            sum += reader.ReadVarInt();
        }
        return sum;
    }

    // ===== RecordBatch Operations =====

    [Benchmark(Description = "Write RecordBatch (10 records)")]
    public void WriteRecordBatch()
    {
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            MaxTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastOffsetDelta = 9,
            Records = Enumerable.Range(0, 10).Select(i => new Record
            {
                TimestampDelta = i,
                OffsetDelta = i,
                Key = System.Text.Encoding.UTF8.GetBytes($"key-{i}"),
                Value = System.Text.Encoding.UTF8.GetBytes($"value-{i}")
            }).ToList()
        };

        batch.Write(_buffer);
    }

    [Benchmark(Description = "Read RecordBatch (10 records)")]
    public RecordBatch ReadRecordBatch()
    {
        var reader = new KafkaProtocolReader(_recordBatchBytes);
        return RecordBatch.Read(ref reader);
    }
}
