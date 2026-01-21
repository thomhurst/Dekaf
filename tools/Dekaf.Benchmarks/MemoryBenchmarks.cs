using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks;

/// <summary>
/// Benchmarks focused on memory allocation patterns.
/// Demonstrates Dekaf's zero-allocation design.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class MemoryBenchmarks
{
    private ArrayBufferWriter<byte> _buffer = null!;
    private byte[] _recordBatchBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new ArrayBufferWriter<byte>(65536);

        // Create a sample record batch
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

        batch.Write(_buffer);
        _recordBatchBytes = _buffer.WrittenSpan.ToArray();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _buffer.Clear();
    }

    [Benchmark(Description = "Protocol Write: 1000 Int32s")]
    public void WriteThousandInt32s()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteInt32(i);
        }
    }

    [Benchmark(Description = "Protocol Write: 100 Strings (100 chars each)")]
    public void WriteHundredStrings()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        var testString = new string('a', 100);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteString(testString);
        }
    }

    [Benchmark(Description = "Protocol Write: 100 CompactStrings")]
    public void WriteHundredCompactStrings()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        var testString = new string('a', 100);
        for (var i = 0; i < 100; i++)
        {
            writer.WriteCompactString(testString);
        }
    }

    [Benchmark(Description = "Protocol Read: 1000 Int32s")]
    public int ReadThousandInt32s()
    {
        // First create the data
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = 0; i < 1000; i++)
        {
            writer.WriteInt32(i);
        }

        // Now read it
        var reader = new KafkaProtocolReader(_buffer.WrittenSpan.ToArray());
        var sum = 0;
        for (var i = 0; i < 1000; i++)
        {
            sum += reader.ReadInt32();
        }
        return sum;
    }

    [Benchmark(Description = "RecordBatch: Write 10 Records")]
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

    [Benchmark(Description = "RecordBatch: Read 10 Records")]
    public RecordBatch ReadRecordBatch()
    {
        var reader = new KafkaProtocolReader(_recordBatchBytes);
        return RecordBatch.Read(ref reader);
    }

    [Benchmark(Description = "VarInt: Write 1000 values")]
    public void WriteThousandVarInts()
    {
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = -500; i < 500; i++)
        {
            writer.WriteVarInt(i);
        }
    }

    [Benchmark(Description = "VarInt: Read 1000 values")]
    public int ReadThousandVarInts()
    {
        // Create data
        _buffer.Clear();
        var writer = new KafkaProtocolWriter(_buffer);
        for (var i = -500; i < 500; i++)
        {
            writer.WriteVarInt(i);
        }

        // Read
        var reader = new KafkaProtocolReader(_buffer.WrittenSpan.ToArray());
        var sum = 0;
        for (var i = 0; i < 1000; i++)
        {
            sum += reader.ReadVarInt();
        }
        return sum;
    }
}
