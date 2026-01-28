using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Serializer performance benchmarks.
/// Tests the ISerializer/IDeserializer implementations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class SerializerBenchmarks
{
    private ArrayBufferWriter<byte> _buffer = null!;
    private string _smallString = null!;
    private string _mediumString = null!;
    private string _largeString = null!;
    private byte[] _stringBytes = null!;
    private SerializationContext _context;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new ArrayBufferWriter<byte>(16384);
        _smallString = new string('a', 10);
        _mediumString = new string('a', 100);
        _largeString = new string('a', 1000);

        _context = new SerializationContext
        {
            Topic = "test-topic",
            Component = SerializationComponent.Value
        };

        // Pre-create string bytes for deserialization
        _stringBytes = System.Text.Encoding.UTF8.GetBytes(_mediumString);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _buffer.Clear();
    }

    // ===== String Serialization =====

    [Benchmark(Description = "Serialize String (10 chars)")]
    public void SerializeString_Small()
    {
        Serializers.String.Serialize(_smallString, ref _buffer, _context);
    }

    [Benchmark(Description = "Serialize String (100 chars)")]
    public void SerializeString_Medium()
    {
        Serializers.String.Serialize(_mediumString, ref _buffer, _context);
    }

    [Benchmark(Description = "Serialize String (1000 chars)")]
    public void SerializeString_Large()
    {
        Serializers.String.Serialize(_largeString, ref _buffer, _context);
    }

    [Benchmark(Description = "Deserialize String")]
    public string DeserializeString()
    {
        return Serializers.String.Deserialize(new ReadOnlySequence<byte>(_stringBytes), _context);
    }

    // ===== Int32 Serialization =====

    [Benchmark(Description = "Serialize Int32")]
    public void SerializeInt32()
    {
        Serializers.Int32.Serialize(12345678, ref _buffer, _context);
    }

    // ===== Batch Serialization (100 messages) =====

    [Benchmark(Description = "Serialize 100 Messages (key+value)")]
    public void SerializeBatch()
    {
        var keyContext = new SerializationContext { Topic = "test", Component = SerializationComponent.Key };
        var valueContext = new SerializationContext { Topic = "test", Component = SerializationComponent.Value };

        for (var i = 0; i < 100; i++)
        {
            var keyWriter = new PooledBufferWriter(initialCapacity: 64);
            Serializers.String.Serialize($"key-{i}", ref keyWriter, keyContext);
            var keyMemory = keyWriter.ToPooledMemory();

            var valueWriter = new PooledBufferWriter(initialCapacity: 256);
            Serializers.String.Serialize(_mediumString, ref valueWriter, valueContext);
            var valueMemory = valueWriter.ToPooledMemory();

            keyMemory.Return();
            valueMemory.Return();
        }
    }

    // ===== PooledBufferWriter vs ArrayBufferWriter =====

    [Benchmark(Baseline = true, Description = "ArrayBufferWriter + Copy")]
    public PooledMemory Serialize_ArrayBufferWriter()
    {
        var arrayWriter = new ArrayBufferWriter<byte>(256);
        Serializers.String.Serialize(_mediumString, ref arrayWriter, _context);

        var buffer = ArrayPool<byte>.Shared.Rent(arrayWriter.WrittenCount);
        arrayWriter.WrittenSpan.CopyTo(buffer);
        return new PooledMemory(buffer, arrayWriter.WrittenCount);
    }

    [Benchmark(Description = "PooledBufferWriter Direct")]
    public PooledMemory Serialize_PooledBufferWriter()
    {
        var pooledWriter = new PooledBufferWriter(initialCapacity: 256);
        Serializers.String.Serialize(_mediumString, ref pooledWriter, _context);
        return pooledWriter.ToPooledMemory();
    }
}
