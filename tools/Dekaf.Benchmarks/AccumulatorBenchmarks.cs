using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks;

/// <summary>
/// Benchmarks testing the fire-and-forget code path without Kafka networking.
/// This isolates serialization + accumulator performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class AccumulatorBenchmarks
{
    private RecordAccumulator _accumulator = null!;
    private string _topic = "benchmark-topic";
    private string _messageValue = null!;
    private ISerializer<string> _serializer = null!;
    private SerializationContext _keyContext;
    private SerializationContext _valueContext;

    [Params(100, 1000)]
    public int MessageSize { get; set; }

    [Params(100, 1000)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _messageValue = new string('x', MessageSize);
        _serializer = Serializers.String;

        _keyContext = new SerializationContext
        {
            Topic = _topic,
            Component = SerializationComponent.Key
        };
        _valueContext = new SerializationContext
        {
            Topic = _topic,
            Component = SerializationComponent.Value
        };

        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 16384,
            LingerMs = 5
        };

        _accumulator = new RecordAccumulator(options);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _accumulator.DisposeAsync();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Drain the accumulator between iterations
        _accumulator.DisposeAsync().AsTask().GetAwaiter().GetResult();

        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            BatchSize = 16384,
            LingerMs = 5
        };
        _accumulator = new RecordAccumulator(options);
    }

    [Benchmark(Description = "Serialize + Accumulate (Fire-and-Forget Path)")]
    public void SerializeAndAccumulate()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            var key = $"key-{i}";

            // Serialize key
            var keyWriter = new PooledBufferWriter(initialCapacity: 64);
            _serializer.Serialize(key, ref keyWriter, _keyContext);
            var keyMemory = keyWriter.ToPooledMemory();

            // Serialize value
            var valueWriter = new PooledBufferWriter(initialCapacity: MessageSize + 16);
            _serializer.Serialize(_messageValue, ref valueWriter, _valueContext);
            var valueMemory = valueWriter.ToPooledMemory();

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var partition = i % 3; // Simulate 3 partitions

            // Append to accumulator (fire-and-forget path)
            _accumulator.TryAppendFireAndForget(
                _topic,
                partition,
                timestamp,
                keyMemory,
                valueMemory,
                headers: null,
                pooledHeaderArray: null);
        }
    }

    [Benchmark(Description = "Serialize Only (Key + Value)")]
    public void SerializeOnly()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            var key = $"key-{i}";

            // Serialize key
            var keyWriter = new PooledBufferWriter(initialCapacity: 64);
            _serializer.Serialize(key, ref keyWriter, _keyContext);
            var keyMemory = keyWriter.ToPooledMemory();

            // Serialize value
            var valueWriter = new PooledBufferWriter(initialCapacity: MessageSize + 16);
            _serializer.Serialize(_messageValue, ref valueWriter, _valueContext);
            var valueMemory = valueWriter.ToPooledMemory();

            // Return to pool (simulate what accumulator does on cleanup)
            keyMemory.Return();
            valueMemory.Return();
        }
    }

    [Benchmark(Description = "String Interpolation Only")]
    public void StringInterpolationOnly()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            var key = $"key-{i}";
            // Use the key to prevent optimization
            if (key.Length < 0) throw new InvalidOperationException();
        }
    }

    [Benchmark(Description = "DateTimeOffset.UtcNow Only")]
    public void TimestampOnly()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // Use the timestamp to prevent optimization
            if (timestamp < 0) throw new InvalidOperationException();
        }
    }

    [Benchmark(Description = "Accumulate Same Partition (Tests Cache)")]
    public void AccumulateSamePartition()
    {
        // This tests the thread-local batch caching optimization.
        // All messages go to partition 0, so after the first message,
        // subsequent messages should hit the fast path.
        for (var i = 0; i < BatchSize; i++)
        {
            var key = $"key-{i}";

            // Serialize key
            var keyWriter = new PooledBufferWriter(initialCapacity: 64);
            _serializer.Serialize(key, ref keyWriter, _keyContext);
            var keyMemory = keyWriter.ToPooledMemory();

            // Serialize value
            var valueWriter = new PooledBufferWriter(initialCapacity: MessageSize + 16);
            _serializer.Serialize(_messageValue, ref valueWriter, _valueContext);
            var valueMemory = valueWriter.ToPooledMemory();

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            const int partition = 0; // Same partition - should hit cache after first message

            _accumulator.TryAppendFireAndForget(
                _topic,
                partition,
                timestamp,
                keyMemory,
                valueMemory,
                headers: null,
                pooledHeaderArray: null);
        }
    }

    [Benchmark(Description = "Accumulate Rotating Partitions (No Cache)")]
    public void AccumulateRotatingPartitions()
    {
        // This tests the slow path where messages rotate through partitions,
        // so the cache misses on every message.
        for (var i = 0; i < BatchSize; i++)
        {
            var key = $"key-{i}";

            // Serialize key
            var keyWriter = new PooledBufferWriter(initialCapacity: 64);
            _serializer.Serialize(key, ref keyWriter, _keyContext);
            var keyMemory = keyWriter.ToPooledMemory();

            // Serialize value
            var valueWriter = new PooledBufferWriter(initialCapacity: MessageSize + 16);
            _serializer.Serialize(_messageValue, ref valueWriter, _valueContext);
            var valueMemory = valueWriter.ToPooledMemory();

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var partition = i % 3; // Rotating partitions - cache misses

            _accumulator.TryAppendFireAndForget(
                _topic,
                partition,
                timestamp,
                keyMemory,
                valueMemory,
                headers: null,
                pooledHeaderArray: null);
        }
    }

    [Benchmark(Description = "Batch Append Same Partition (Single Lock)")]
    public void BatchAppendSamePartition()
    {
        // This tests the batch append API which acquires the lock once for N records.
        // Pre-serialize all records into an array, then append as a batch.
        var records = new ProducerRecordData[BatchSize];
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (var i = 0; i < BatchSize; i++)
        {
            var key = $"key-{i}";

            // Serialize key
            var keyWriter = new PooledBufferWriter(initialCapacity: 64);
            _serializer.Serialize(key, ref keyWriter, _keyContext);
            var keyMemory = keyWriter.ToPooledMemory();

            // Serialize value
            var valueWriter = new PooledBufferWriter(initialCapacity: MessageSize + 16);
            _serializer.Serialize(_messageValue, ref valueWriter, _valueContext);
            var valueMemory = valueWriter.ToPooledMemory();

            records[i] = new ProducerRecordData
            {
                Timestamp = timestamp,
                Key = keyMemory,
                Value = valueMemory,
                Headers = null,
                PooledHeaderArray = null
            };
        }

        // Single batch append with one lock acquisition
        const int partition = 0;
        var success = _accumulator.TryAppendFireAndForgetBatch(_topic, partition, records);

        // If batch append failed (accumulator disposed), clean up pooled resources
        // Ownership of appended records transferred to accumulator; we only clean up on failure
        if (!success)
        {
            foreach (var record in records)
            {
                record.Key.Return();
                record.Value.Return();
            }
        }
    }
}
