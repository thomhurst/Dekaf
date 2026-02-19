using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Allocation benchmarks for consumer hot paths.
/// Verifies zero-allocation behavior for record deserialization, batch parsing,
/// and fetch response processing that run per-message at high throughput.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ConsumerHotPathBenchmarks
{
    private byte[] _singleRecordBytes = null!;
    private byte[] _smallBatchBytes = null!;
    private byte[] _largeBatchBytes = null!;
    private byte[] _batchWithHeadersBytes = null!;
    private byte[] _stringValueBytes = null!;
    private byte[] _int32ValueBytes = null!;
    private byte[] _int64ValueBytes = null!;
    private byte[] _guidValueBytes = null!;
    private SerializationContext _valueContext;
    private SerializationContext _nullContext;

    [GlobalSetup]
    public void Setup()
    {
        _valueContext = new SerializationContext
        {
            Topic = "benchmark-topic",
            Component = SerializationComponent.Value
        };
        _nullContext = new SerializationContext
        {
            Topic = "benchmark-topic",
            Component = SerializationComponent.Value,
            IsNull = true
        };

        // Create a single record for parsing
        _singleRecordBytes = CreateRecordBytes(0, "key-0", new string('x', 1000));

        // Create a small batch (10 records, ~1KB values)
        _smallBatchBytes = CreateBatchBytes(10, 1000);

        // Create a large batch (100 records, ~1KB values)
        _largeBatchBytes = CreateBatchBytes(100, 1000);

        // Create a batch with headers
        _batchWithHeadersBytes = CreateBatchWithHeadersBytes(10, 1000);

        // Pre-create serialized value bytes for deserialization benchmarks
        _stringValueBytes = Encoding.UTF8.GetBytes("Hello, Kafka benchmark value!");
        _int32ValueBytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(_int32ValueBytes, 42);
        _int64ValueBytes = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(_int64ValueBytes, 123456789L);
        _guidValueBytes = new byte[16];
        Guid.NewGuid().TryWriteBytes(_guidValueBytes, bigEndian: true, out _);
    }

    // ===== Single Record Parsing =====

    [Benchmark(Baseline = true, Description = "Parse single Record (1KB value)")]
    public Record ParseSingleRecord()
    {
        var reader = new KafkaProtocolReader(_singleRecordBytes);
        return Record.Read(ref reader);
    }

    // ===== RecordBatch Parsing (consumer fetch response hot path) =====

    [Benchmark(Description = "Parse RecordBatch (10 records, 1KB values)")]
    public RecordBatch ParseSmallBatch()
    {
        var reader = new KafkaProtocolReader(_smallBatchBytes);
        return RecordBatch.Read(ref reader);
    }

    [Benchmark(Description = "Parse RecordBatch (100 records, 1KB values)")]
    public RecordBatch ParseLargeBatch()
    {
        var reader = new KafkaProtocolReader(_largeBatchBytes);
        return RecordBatch.Read(ref reader);
    }

    [Benchmark(Description = "Parse RecordBatch with headers (10 records)")]
    public RecordBatch ParseBatchWithHeaders()
    {
        var reader = new KafkaProtocolReader(_batchWithHeadersBytes);
        return RecordBatch.Read(ref reader);
    }

    // ===== Record Iteration (accessing parsed records) =====

    [Benchmark(Description = "Parse + iterate 10 records (access Key+Value)")]
    public int ParseAndIterateSmallBatch()
    {
        var reader = new KafkaProtocolReader(_smallBatchBytes);
        var batch = RecordBatch.Read(ref reader);

        var totalBytes = 0;
        for (var i = 0; i < batch.Records.Count; i++)
        {
            var record = batch.Records[i];
            totalBytes += record.Key.Length + record.Value.Length;
        }

        return totalBytes;
    }

    [Benchmark(Description = "Parse + iterate 100 records (access Key+Value)")]
    public int ParseAndIterateLargeBatch()
    {
        var reader = new KafkaProtocolReader(_largeBatchBytes);
        var batch = RecordBatch.Read(ref reader);

        var totalBytes = 0;
        for (var i = 0; i < batch.Records.Count; i++)
        {
            var record = batch.Records[i];
            totalBytes += record.Key.Length + record.Value.Length;
        }

        return totalBytes;
    }

    // ===== Value Deserialization (per-message) =====

    [Benchmark(Description = "Deserialize String value")]
    public string DeserializeString()
    {
        return Serializers.String.Deserialize(
            new ReadOnlySequence<byte>(_stringValueBytes), _valueContext);
    }

    [Benchmark(Description = "Deserialize Int32 value")]
    public int DeserializeInt32()
    {
        return Serializers.Int32.Deserialize(
            new ReadOnlySequence<byte>(_int32ValueBytes), _valueContext);
    }

    [Benchmark(Description = "Deserialize Int64 value")]
    public long DeserializeInt64()
    {
        return Serializers.Int64.Deserialize(
            new ReadOnlySequence<byte>(_int64ValueBytes), _valueContext);
    }

    [Benchmark(Description = "Deserialize Guid value")]
    public Guid DeserializeGuid()
    {
        return Serializers.Guid.Deserialize(
            new ReadOnlySequence<byte>(_guidValueBytes), _valueContext);
    }

    [Benchmark(Description = "Deserialize RawBytes (zero-copy)")]
    public ReadOnlyMemory<byte> DeserializeRawBytes()
    {
        return Serializers.RawBytes.Deserialize(
            new ReadOnlySequence<byte>(_stringValueBytes), _valueContext);
    }

    [Benchmark(Description = "Deserialize NullableString (null)")]
    public string? DeserializeNullableString_Null()
    {
        return Serializers.NullableString.Deserialize(
            new ReadOnlySequence<byte>(ReadOnlyMemory<byte>.Empty), _nullContext);
    }

    // ===== Full consumer deserialization pipeline =====

    [Benchmark(Description = "Full pipeline: Parse batch + deserialize 10 string values")]
    public int FullDeserializationPipeline()
    {
        var reader = new KafkaProtocolReader(_smallBatchBytes);
        var batch = RecordBatch.Read(ref reader);

        var totalLength = 0;
        for (var i = 0; i < batch.Records.Count; i++)
        {
            var record = batch.Records[i];

            // Deserialize key
            if (!record.IsKeyNull)
            {
                var key = Serializers.String.Deserialize(
                    new ReadOnlySequence<byte>(record.Key), _valueContext);
                totalLength += key.Length;
            }

            // Deserialize value
            if (!record.IsValueNull)
            {
                var value = Serializers.String.Deserialize(
                    new ReadOnlySequence<byte>(record.Value), _valueContext);
                totalLength += value.Length;
            }
        }

        return totalLength;
    }

    // ===== KafkaProtocolReader hot path operations =====

    [Benchmark(Description = "KafkaProtocolReader: Read record header fields")]
    public (int, byte, long, int) ReadRecordHeaderFields()
    {
        // Simulates the per-record header parsing in Record.Read()
        var reader = new KafkaProtocolReader(_singleRecordBytes);
        var length = reader.ReadVarInt();
        var attributes = (byte)reader.ReadInt8();
        var timestampDelta = reader.ReadVarLong();
        var offsetDelta = reader.ReadVarInt();
        return (length, attributes, timestampDelta, offsetDelta);
    }

    [Benchmark(Description = "KafkaProtocolReader: ReadMemorySlice (1KB, zero-copy)")]
    public ReadOnlyMemory<byte> ReadMemorySlice()
    {
        // This is the zero-copy path used for reading record key/value data
        var reader = new KafkaProtocolReader((ReadOnlyMemory<byte>)_stringValueBytes);
        return reader.ReadMemorySlice(_stringValueBytes.Length);
    }

    // ===== Helpers =====

    private static byte[] CreateRecordBytes(int index, string key, string value)
    {
        var record = new Record
        {
            TimestampDelta = index,
            OffsetDelta = index,
            Key = Encoding.UTF8.GetBytes(key),
            IsKeyNull = false,
            Value = Encoding.UTF8.GetBytes(value),
            IsValueNull = false,
            Headers = null
        };

        var buffer = new ArrayBufferWriter<byte>(4096);
        var writer = new KafkaProtocolWriter(buffer);
        record.Write(ref writer);
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] CreateBatchBytes(int recordCount, int valueSize)
    {
        var records = new List<Record>(recordCount);
        for (var i = 0; i < recordCount; i++)
        {
            records.Add(new Record
            {
                TimestampDelta = i,
                OffsetDelta = i,
                Key = Encoding.UTF8.GetBytes($"key-{i}"),
                IsKeyNull = false,
                Value = Encoding.UTF8.GetBytes($"v{i}-{new string('x', valueSize - 4)}"),
                IsValueNull = false,
                Headers = null
            });
        }

        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            MaxTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastOffsetDelta = recordCount - 1,
            Records = records
        };

        var buffer = new ArrayBufferWriter<byte>(recordCount * (valueSize + 64));
        batch.Write(buffer);
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] CreateBatchWithHeadersBytes(int recordCount, int valueSize)
    {
        var records = new List<Record>(recordCount);
        for (var i = 0; i < recordCount; i++)
        {
            records.Add(new Record
            {
                TimestampDelta = i,
                OffsetDelta = i,
                Key = Encoding.UTF8.GetBytes($"key-{i}"),
                IsKeyNull = false,
                Value = Encoding.UTF8.GetBytes($"v{i}-{new string('x', valueSize - 4)}"),
                IsValueNull = false,
                Headers =
                [
                    new Header("trace-id", Encoding.UTF8.GetBytes($"trace-{i}")),
                    new Header("source", Encoding.UTF8.GetBytes("benchmark"))
                ]
            });
        }

        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            MaxTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastOffsetDelta = recordCount - 1,
            Records = records
        };

        var buffer = new ArrayBufferWriter<byte>(recordCount * (valueSize + 128));
        batch.Write(buffer);
        return buffer.WrittenSpan.ToArray();
    }
}
