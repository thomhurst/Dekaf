using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Serializer performance benchmarks.
/// Tests the ISerializer/IDeserializer implementations.
/// </summary>
/// <remarks>
/// Benchmarks are grouped by category so the Ratio column only compares like with like;
/// the single baseline lives in the Writer category. There is deliberately no
/// <c>[IterationSetup]</c>: its presence would force single-invocation iterations, which
/// cannot produce meaningful statistics for nanosecond-scale operations — buffers are
/// reset inside each benchmark instead.
/// </remarks>
[MemoryDiagnoser]
[ShortRunJob]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SerializerBenchmarks
{
    private ArrayBufferWriter<byte> _buffer = null!;
    private string _smallString = null!;
    private string _mediumString = null!;
    private string _largeString = null!;
    private string[] _batchKeys = null!;
    private byte[] _stringBytes = null!;
    private SerializationContext _context;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new ArrayBufferWriter<byte>(16384);
        _smallString = new string('a', 10);
        _mediumString = new string('a', 100);
        _largeString = new string('a', 1000);

        // Pre-created so the batch benchmark's Allocated column reflects the library,
        // not the benchmark's own key-string interpolation.
        _batchKeys = [.. Enumerable.Range(0, 100).Select(i => $"key-{i}")];

        _context = new SerializationContext
        {
            Topic = "test-topic",
            Component = SerializationComponent.Value
        };

        // Pre-create string bytes for deserialization
        _stringBytes = System.Text.Encoding.UTF8.GetBytes(_mediumString);
    }

    // ===== String Serialization =====

    [BenchmarkCategory("Scalar")]
    [Benchmark(Description = "Serialize String (10 chars)")]
    public void SerializeString_Small()
    {
        _buffer.ResetWrittenCount();
        Serializers.String.Serialize(_smallString, ref _buffer, _context);
    }

    [BenchmarkCategory("Scalar")]
    [Benchmark(Description = "Serialize String (100 chars)")]
    public void SerializeString_Medium()
    {
        _buffer.ResetWrittenCount();
        Serializers.String.Serialize(_mediumString, ref _buffer, _context);
    }

    [BenchmarkCategory("Scalar")]
    [Benchmark(Description = "Serialize String (1000 chars)")]
    public void SerializeString_Large()
    {
        _buffer.ResetWrittenCount();
        Serializers.String.Serialize(_largeString, ref _buffer, _context);
    }

    [BenchmarkCategory("Scalar")]
    [Benchmark(Description = "Deserialize String")]
    public string DeserializeString()
    {
        return Serializers.String.Deserialize((ReadOnlyMemory<byte>)_stringBytes, _context);
    }

    // ===== Int32 Serialization =====

    [BenchmarkCategory("Scalar")]
    [Benchmark(Description = "Serialize Int32")]
    public void SerializeInt32()
    {
        _buffer.ResetWrittenCount();
        Serializers.Int32.Serialize(12345678, ref _buffer, _context);
    }

    // ===== Batch Serialization (100 messages) =====

    [BenchmarkCategory("Batch")]
    [Benchmark(Description = "Serialize 100 Messages (key+value)")]
    public void SerializeBatch()
    {
        var keyContext = new SerializationContext { Topic = "test", Component = SerializationComponent.Key };
        var valueContext = new SerializationContext { Topic = "test", Component = SerializationComponent.Value };

        for (var i = 0; i < 100; i++)
        {
            var keyWriter = new PooledBufferWriter(initialCapacity: 64);
            Serializers.String.Serialize(_batchKeys[i], ref keyWriter, keyContext);
            var keyMemory = keyWriter.ToPooledMemory();

            var valueWriter = new PooledBufferWriter(initialCapacity: 256);
            Serializers.String.Serialize(_mediumString, ref valueWriter, valueContext);
            var valueMemory = valueWriter.ToPooledMemory();

            keyMemory.Return();
            valueMemory.Return();
        }
    }

    // ===== PooledBufferWriter vs ArrayBufferWriter =====

    [BenchmarkCategory("Writer")]
    [Benchmark(Baseline = true, Description = "ArrayBufferWriter + Copy")]
    public void Serialize_ArrayBufferWriter()
    {
        var arrayWriter = new ArrayBufferWriter<byte>(256);
        Serializers.String.Serialize(_mediumString, ref arrayWriter, _context);

        var buffer = ProducerDataPool.BytePool.Rent(arrayWriter.WrittenCount);
        arrayWriter.WrittenSpan.CopyTo(buffer);
        new PooledMemory(buffer, arrayWriter.WrittenCount).Return();
    }

    [BenchmarkCategory("Writer")]
    [Benchmark(Description = "PooledBufferWriter Direct")]
    public void Serialize_PooledBufferWriter()
    {
        var pooledWriter = new PooledBufferWriter(initialCapacity: 256);
        Serializers.String.Serialize(_mediumString, ref pooledWriter, _context);
        pooledWriter.ToPooledMemory().Return();
    }
}
