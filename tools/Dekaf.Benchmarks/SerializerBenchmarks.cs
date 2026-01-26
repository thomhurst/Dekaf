using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks;

/// <summary>
/// Benchmarks comparing serialization patterns:
/// - Old: ArrayBufferWriter + copy to pooled array
/// - New: PooledBufferWriter direct (zero-copy)
///
/// These benchmarks demonstrate the allocation savings from the
/// ISerializer&lt;T&gt; generic interface supporting ref struct buffer writers.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class SerializerBenchmarks
{
    private const string TestKey = "test-key-with-some-realistic-content-length";
    private const string TestValue = "This is a test message value that simulates realistic Kafka message content with enough length to be meaningful for benchmarking purposes.";

    private static readonly SerializationContext KeyContext = new()
    {
        Topic = "benchmark-topic",
        Component = SerializationComponent.Key
    };

    private static readonly SerializationContext ValueContext = new()
    {
        Topic = "benchmark-topic",
        Component = SerializationComponent.Value
    };

    // ===== String Serialization: Single Operation =====

    [Benchmark(Description = "String Serialize: ArrayBufferWriter + Copy")]
    public PooledMemory StringSerialize_ArrayBufferWriter()
    {
        // Old pattern: serialize to ArrayBufferWriter, then copy to pooled array
        var arrayWriter = new ArrayBufferWriter<byte>(256);
        Serializers.String.Serialize(TestKey, ref arrayWriter, KeyContext);

        // Copy to pooled memory (the old overhead)
        var buffer = ArrayPool<byte>.Shared.Rent(arrayWriter.WrittenCount);
        arrayWriter.WrittenSpan.CopyTo(buffer);
        return new PooledMemory(buffer, arrayWriter.WrittenCount);
    }

    [Benchmark(Description = "String Serialize: PooledBufferWriter Direct")]
    public PooledMemory StringSerialize_PooledBufferWriter()
    {
        // New pattern: serialize directly to pooled buffer
        var pooledWriter = new PooledBufferWriter(initialCapacity: 256);
        Serializers.String.Serialize(TestKey, ref pooledWriter, KeyContext);

        // Direct transfer - no copy!
        return pooledWriter.ToPooledMemory();
    }

    // ===== String Serialization: Batch (100 messages) =====

    [Benchmark(Description = "String Serialize x100: ArrayBufferWriter + Copy")]
    public void StringSerializeBatch_ArrayBufferWriter()
    {
        for (int i = 0; i < 100; i++)
        {
            var arrayWriter = new ArrayBufferWriter<byte>(256);
            Serializers.String.Serialize(TestKey, ref arrayWriter, KeyContext);

            var buffer = ArrayPool<byte>.Shared.Rent(arrayWriter.WrittenCount);
            arrayWriter.WrittenSpan.CopyTo(buffer);
            var result = new PooledMemory(buffer, arrayWriter.WrittenCount);
            result.Return();
        }
    }

    [Benchmark(Description = "String Serialize x100: PooledBufferWriter Direct")]
    public void StringSerializeBatch_PooledBufferWriter()
    {
        for (int i = 0; i < 100; i++)
        {
            var pooledWriter = new PooledBufferWriter(initialCapacity: 256);
            Serializers.String.Serialize(TestKey, ref pooledWriter, KeyContext);

            var result = pooledWriter.ToPooledMemory();
            result.Return();
        }
    }

    // ===== Key + Value Serialization (realistic producer pattern) =====

    [Benchmark(Description = "Key+Value Serialize: ArrayBufferWriter + Copy")]
    public (PooledMemory Key, PooledMemory Value) KeyValueSerialize_ArrayBufferWriter()
    {
        // Serialize key
        var keyWriter = new ArrayBufferWriter<byte>(256);
        Serializers.String.Serialize(TestKey, ref keyWriter, KeyContext);
        var keyBuffer = ArrayPool<byte>.Shared.Rent(keyWriter.WrittenCount);
        keyWriter.WrittenSpan.CopyTo(keyBuffer);
        var key = new PooledMemory(keyBuffer, keyWriter.WrittenCount);

        // Serialize value
        var valueWriter = new ArrayBufferWriter<byte>(256);
        Serializers.String.Serialize(TestValue, ref valueWriter, ValueContext);
        var valueBuffer = ArrayPool<byte>.Shared.Rent(valueWriter.WrittenCount);
        valueWriter.WrittenSpan.CopyTo(valueBuffer);
        var value = new PooledMemory(valueBuffer, valueWriter.WrittenCount);

        return (key, value);
    }

    [Benchmark(Description = "Key+Value Serialize: PooledBufferWriter Direct")]
    public (PooledMemory Key, PooledMemory Value) KeyValueSerialize_PooledBufferWriter()
    {
        // Serialize key
        var keyWriter = new PooledBufferWriter(initialCapacity: 256);
        Serializers.String.Serialize(TestKey, ref keyWriter, KeyContext);
        var key = keyWriter.ToPooledMemory();

        // Serialize value
        var valueWriter = new PooledBufferWriter(initialCapacity: 256);
        Serializers.String.Serialize(TestValue, ref valueWriter, ValueContext);
        var value = valueWriter.ToPooledMemory();

        return (key, value);
    }

    // ===== Key + Value Batch (simulating 100 message production) =====

    [Benchmark(Description = "Key+Value x100: ArrayBufferWriter + Copy")]
    public void KeyValueBatch_ArrayBufferWriter()
    {
        for (int i = 0; i < 100; i++)
        {
            var (key, value) = KeyValueSerialize_ArrayBufferWriter();
            key.Return();
            value.Return();
        }
    }

    [Benchmark(Description = "Key+Value x100: PooledBufferWriter Direct")]
    public void KeyValueBatch_PooledBufferWriter()
    {
        for (int i = 0; i < 100; i++)
        {
            var (key, value) = KeyValueSerialize_PooledBufferWriter();
            key.Return();
            value.Return();
        }
    }

    // ===== Int32 Serialization (compact binary format) =====

    [Benchmark(Description = "Int32 Serialize x1000: ArrayBufferWriter + Copy")]
    public void Int32SerializeBatch_ArrayBufferWriter()
    {
        for (int i = 0; i < 1000; i++)
        {
            var arrayWriter = new ArrayBufferWriter<byte>(8);
            Serializers.Int32.Serialize(i, ref arrayWriter, KeyContext);

            var buffer = ArrayPool<byte>.Shared.Rent(arrayWriter.WrittenCount);
            arrayWriter.WrittenSpan.CopyTo(buffer);
            var result = new PooledMemory(buffer, arrayWriter.WrittenCount);
            result.Return();
        }
    }

    [Benchmark(Description = "Int32 Serialize x1000: PooledBufferWriter Direct")]
    public void Int32SerializeBatch_PooledBufferWriter()
    {
        for (int i = 0; i < 1000; i++)
        {
            var pooledWriter = new PooledBufferWriter(initialCapacity: 8);
            Serializers.Int32.Serialize(i, ref pooledWriter, KeyContext);

            var result = pooledWriter.ToPooledMemory();
            result.Return();
        }
    }

    // ===== Large Value Serialization =====

    private static readonly string LargeValue = new('x', 10_000); // 10KB message

    [Benchmark(Description = "Large 10KB Serialize: ArrayBufferWriter + Copy")]
    public PooledMemory LargeSerialize_ArrayBufferWriter()
    {
        var arrayWriter = new ArrayBufferWriter<byte>(16384);
        Serializers.String.Serialize(LargeValue, ref arrayWriter, ValueContext);

        var buffer = ArrayPool<byte>.Shared.Rent(arrayWriter.WrittenCount);
        arrayWriter.WrittenSpan.CopyTo(buffer);
        return new PooledMemory(buffer, arrayWriter.WrittenCount);
    }

    [Benchmark(Description = "Large 10KB Serialize: PooledBufferWriter Direct")]
    public PooledMemory LargeSerialize_PooledBufferWriter()
    {
        var pooledWriter = new PooledBufferWriter(initialCapacity: 16384);
        Serializers.String.Serialize(LargeValue, ref pooledWriter, ValueContext);

        return pooledWriter.ToPooledMemory();
    }
}
