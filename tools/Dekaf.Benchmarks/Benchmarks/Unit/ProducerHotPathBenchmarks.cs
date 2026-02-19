using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Allocation benchmarks for producer hot paths.
/// Verifies zero-allocation behavior for message serialization, record creation,
/// and batch append operations that run per-message at high throughput.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ProducerHotPathBenchmarks
{
    private ArrayBufferWriter<byte> _buffer = null!;
    private SerializationContext _keyContext;
    private SerializationContext _valueContext;
    private string _key = null!;
    private string _smallValue = null!;
    private string _mediumValue = null!;
    private string _largeValue = null!;
    private byte[] _preSerializedKey = null!;
    private byte[] _preSerializedValue = null!;
    private RecordBatch _sampleBatch = null!;
    private byte[] _recordBatchBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new ArrayBufferWriter<byte>(65536);

        _keyContext = new SerializationContext
        {
            Topic = "benchmark-topic",
            Component = SerializationComponent.Key
        };
        _valueContext = new SerializationContext
        {
            Topic = "benchmark-topic",
            Component = SerializationComponent.Value
        };

        _key = "benchmark-key-001";
        _smallValue = new string('x', 100);      // 100 bytes - small message
        _mediumValue = new string('x', 1000);     // 1 KB - typical message
        _largeValue = new string('x', 10000);     // 10 KB - large message

        // Pre-serialize key and value for record append benchmarks
        _preSerializedKey = Encoding.UTF8.GetBytes(_key);
        _preSerializedValue = Encoding.UTF8.GetBytes(_mediumValue);

        // Create a sample batch for write benchmarks
        _sampleBatch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            MaxTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastOffsetDelta = 9,
            Records = CreateSampleRecords(10)
        };

        // Pre-serialize batch for read benchmarks
        var tempBuffer = new ArrayBufferWriter<byte>(65536);
        _sampleBatch.Write(tempBuffer);
        _recordBatchBytes = tempBuffer.WrittenSpan.ToArray();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _buffer.Clear();
    }

    // ===== Serialization Hot Path (per-message) =====

    [Benchmark(Baseline = true, Description = "PooledBufferWriter: Serialize key+value (1KB)")]
    public PooledMemory SerializeKeyValue_PooledWriter()
    {
        // This is the actual producer hot path: serialize using PooledBufferWriter
        // which rents from ArrayPool and transfers ownership to PooledMemory.
        var keyWriter = new PooledBufferWriter(initialCapacity: 64);
        Serializers.String.Serialize(_key, ref keyWriter, _keyContext);
        var keyMemory = keyWriter.ToPooledMemory();

        var valueWriter = new PooledBufferWriter(initialCapacity: 1024);
        Serializers.String.Serialize(_mediumValue, ref valueWriter, _valueContext);
        var valueMemory = valueWriter.ToPooledMemory();

        // Return key memory, keep value as the benchmark result to prevent dead-code elimination
        keyMemory.Return();
        var result = valueMemory;
        // Note: caller would normally call result.Return() - omitted to return value
        return result;
    }

    [Benchmark(Description = "PooledBufferWriter: Serialize small value (100B)")]
    public PooledMemory SerializeSmallValue_PooledWriter()
    {
        var writer = new PooledBufferWriter(initialCapacity: 128);
        Serializers.String.Serialize(_smallValue, ref writer, _valueContext);
        return writer.ToPooledMemory();
    }

    [Benchmark(Description = "PooledBufferWriter: Serialize large value (10KB)")]
    public PooledMemory SerializeLargeValue_PooledWriter()
    {
        var writer = new PooledBufferWriter(initialCapacity: 10240);
        Serializers.String.Serialize(_largeValue, ref writer, _valueContext);
        return writer.ToPooledMemory();
    }

    // ===== Record Creation (per-message struct creation) =====

    [Benchmark(Description = "Create Record struct")]
    public Record CreateRecord()
    {
        // Record is a readonly record struct - should be zero-allocation
        return new Record
        {
            TimestampDelta = 42,
            OffsetDelta = 7,
            Key = _preSerializedKey,
            IsKeyNull = false,
            Value = _preSerializedValue,
            IsValueNull = false,
            Headers = null
        };
    }

    [Benchmark(Description = "Create Record struct with headers")]
    public Record CreateRecord_WithHeaders()
    {
        // Headers array is a per-message allocation that's tracked
        var headers = new Header[]
        {
            new("trace-id", Encoding.UTF8.GetBytes("abc123")),
            new("correlation-id", Encoding.UTF8.GetBytes("def456"))
        };

        return new Record
        {
            TimestampDelta = 42,
            OffsetDelta = 7,
            Key = _preSerializedKey,
            IsKeyNull = false,
            Value = _preSerializedValue,
            IsValueNull = false,
            Headers = headers
        };
    }

    // ===== Record Write (per-message serialization to wire format) =====

    [Benchmark(Description = "Write single Record to buffer")]
    public int WriteRecord()
    {
        var record = new Record
        {
            TimestampDelta = 42,
            OffsetDelta = 7,
            Key = _preSerializedKey,
            IsKeyNull = false,
            Value = _preSerializedValue,
            IsValueNull = false,
            Headers = null
        };

        var writer = new KafkaProtocolWriter(_buffer);
        record.Write(ref writer);
        return writer.BytesWritten;
    }

    [Benchmark(Description = "Write 100 Records to buffer")]
    public int WriteRecords_Hundred()
    {
        var writer = new KafkaProtocolWriter(_buffer);

        for (var i = 0; i < 100; i++)
        {
            var record = new Record
            {
                TimestampDelta = i,
                OffsetDelta = i,
                Key = _preSerializedKey,
                IsKeyNull = false,
                Value = _preSerializedValue,
                IsValueNull = false,
                Headers = null
            };

            record.Write(ref writer);
        }

        return writer.BytesWritten;
    }

    // ===== RecordBatch Write (per-batch) =====

    [Benchmark(Description = "Write RecordBatch (10 records, 1KB values)")]
    public int WriteRecordBatch()
    {
        _sampleBatch.Write(_buffer);
        return _buffer.WrittenCount;
    }

    // ===== RecordBatch Read/Parse (consumer hot path) =====

    [Benchmark(Description = "Read RecordBatch (10 records, 1KB values)")]
    public RecordBatch ReadRecordBatch()
    {
        var reader = new KafkaProtocolReader(_recordBatchBytes);
        return RecordBatch.Read(ref reader);
    }

    // ===== PooledMemory lifecycle (per-message) =====

    [Benchmark(Description = "PooledMemory: Rent + Return (256B)")]
    public void PooledMemory_RentReturn_256()
    {
        var array = ArrayPool<byte>.Shared.Rent(256);
        var memory = new PooledMemory(array, 256);
        memory.Return();
    }

    [Benchmark(Description = "PooledMemory: Rent + Return (1KB)")]
    public void PooledMemory_RentReturn_1024()
    {
        var array = ArrayPool<byte>.Shared.Rent(1024);
        var memory = new PooledMemory(array, 1024);
        memory.Return();
    }

    // ===== Full message serialization pipeline (end-to-end per-message) =====

    [Benchmark(Description = "Full pipeline: Serialize key+value + create Record")]
    public Record FullSerializationPipeline()
    {
        // Step 1: Serialize key
        var keyWriter = new PooledBufferWriter(initialCapacity: 64);
        Serializers.String.Serialize(_key, ref keyWriter, _keyContext);
        var keyMemory = keyWriter.ToPooledMemory();

        // Step 2: Serialize value
        var valueWriter = new PooledBufferWriter(initialCapacity: 1024);
        Serializers.String.Serialize(_mediumValue, ref valueWriter, _valueContext);
        var valueMemory = valueWriter.ToPooledMemory();

        // Step 3: Create record (struct, stack-only)
        var record = new Record
        {
            TimestampDelta = 0,
            OffsetDelta = 0,
            Key = keyMemory.Memory,
            IsKeyNull = false,
            Value = valueMemory.Memory,
            IsValueNull = false,
            Headers = null
        };

        // Step 4: Cleanup
        keyMemory.Return();
        valueMemory.Return();

        return record;
    }

    [Benchmark(Description = "Full pipeline: 100 messages serialized")]
    public int FullSerializationPipeline_Batch()
    {
        var totalBytes = 0;

        for (var i = 0; i < 100; i++)
        {
            var keyWriter = new PooledBufferWriter(initialCapacity: 64);
            Serializers.String.Serialize(_key, ref keyWriter, _keyContext);
            var keyMemory = keyWriter.ToPooledMemory();

            var valueWriter = new PooledBufferWriter(initialCapacity: 1024);
            Serializers.String.Serialize(_mediumValue, ref valueWriter, _valueContext);
            var valueMemory = valueWriter.ToPooledMemory();

            totalBytes += keyMemory.Length + valueMemory.Length;

            keyMemory.Return();
            valueMemory.Return();
        }

        return totalBytes;
    }

    // ===== Helpers =====

    private List<Record> CreateSampleRecords(int count)
    {
        var records = new List<Record>(count);
        for (var i = 0; i < count; i++)
        {
            records.Add(new Record
            {
                TimestampDelta = i,
                OffsetDelta = i,
                Key = Encoding.UTF8.GetBytes($"key-{i}"),
                IsKeyNull = false,
                Value = Encoding.UTF8.GetBytes($"value-{i}-{new string('x', 990)}"),
                IsValueNull = false,
                Headers = null
            });
        }
        return records;
    }
}
