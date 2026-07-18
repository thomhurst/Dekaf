using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Zero-allocation protocol benchmarks for Kafka wire protocol operations.
/// These benchmarks verify Dekaf's allocation-free design.
/// </summary>
/// <remarks>
/// The RecordBatch write benchmarks reuse a batch built in <c>[GlobalSetup]</c>
/// (<see cref="RecordBatch.Write"/> does not mutate the batch), and the read benchmarks
/// return the batch to its pool the way the consumer path does — so the Allocated column
/// measures the library, not the benchmark's own input construction.
/// </remarks>
[MemoryDiagnoser]
[ThroughputJob]
public class ProtocolBenchmarks
{
    private ArrayBufferWriter<byte> _buffer = null!;
    private byte[] _int32Data = null!;
    private byte[] _varIntData = null!;
    private byte[] _recordBatchBytes = null!;
    private RecordBatch _writeBatch = null!;
    private string _testString = null!;
    private string _longString = null!;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new ArrayBufferWriter<byte>(65536);
        _testString = new string('a', 100);
        _longString = new string('a', 300);

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

        _writeBatch = CreateTenRecordBatch();
    }

    // No [IterationSetup]: its presence would force single-invocation iterations
    // (cold single-shot Tier-0 samples, meaningless statistics for microsecond ops).
    // Each write benchmark clears _buffer itself; read benchmarks don't use it.

    // ===== Write Operations =====

    [Benchmark(Description = "Write 1000 Int32s")]
    public void WriteInt32_Thousand()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteInt32(i);
        }
    }

    [Benchmark(Description = "Write 100 Strings (100 chars)")]
    public void WriteString_Hundred()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteString(_testString);
        }
    }

    [Benchmark(Description = "Write 100 Strings (300 chars)")]
    public void WriteString_Long()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteString(_longString);
        }
    }

    [Benchmark(Description = "Write 100 String spans (300 chars)")]
    public void WriteStringSpan_Long()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        var value = _longString.AsSpan();
        for (var i = 0; i < 100; i++)
        {
            writer.WriteString(value);
        }
    }

    [Benchmark(Description = "Write 100 CompactStrings")]
    public void WriteCompactString_Hundred()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteCompactString(_testString);
        }
    }

    [Benchmark(Description = "Write 100 CompactStrings (300 chars)")]
    public void WriteCompactString_Long()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteCompactString(_longString);
        }
    }

    [Benchmark(Description = "Write 100 CompactString spans (300 chars)")]
    public void WriteCompactStringSpan_Long()
    {
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        var value = _longString.AsSpan();
        for (var i = 0; i < 100; i++)
        {
            writer.WriteCompactString(value);
        }
    }

    [Benchmark(Description = "Write 1000 VarInts")]
    public void WriteVarInt_Thousand()
    {
        _buffer.Clear();
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
        _buffer.Clear();
        _writeBatch.Write(_buffer);
    }

    [Benchmark(Description = "Write RecordBatch pre-serialized (10 records)")]
    public void WriteRecordBatchPreSerialized()
    {
        _buffer.Clear();
        _writeBatch.PreCompress(CompressionType.None, null);
        _writeBatch.Write(_buffer);
        _writeBatch.ReturnPreCompressedBuffer();
    }

    private static RecordBatch CreateTenRecordBatch() => new()
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

    [Benchmark(Description = "Read RecordBatch (10 records)")]
    public long ReadRecordBatch()
    {
        var reader = new KafkaProtocolReader(_recordBatchBytes);
        var batch = RecordBatch.Read(ref reader);
        var baseOffset = batch.BaseOffset;
        batch.DisposeAndReturnUnownedConsumerBatch();
        return baseOffset;
    }

    [Benchmark(Description = "Read + Iterate RecordBatch (10 records)")]
    public int ReadAndIterateRecordBatch()
    {
        var reader = new KafkaProtocolReader(_recordBatchBytes);
        var batch = RecordBatch.Read(ref reader);
        var sum = 0;
        for (var i = 0; i < batch.Records.Count; i++)
        {
            sum += batch.Records[i].OffsetDelta;
        }
        batch.DisposeAndReturnUnownedConsumerBatch();
        return sum;
    }
}
