using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dekaf.Benchmarks.Infrastructure;
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
/// reset inside each benchmark instead. The Writer and Batch benchmarks exercise
/// <see cref="ReusableBufferWriter"/> — the writer the production serialization path
/// actually uses (thread-local buffers in <c>ProducerThreadCache</c>).
/// </remarks>
[MemoryDiagnoser]
[ThroughputJob]
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

    // Persistent buffers for ReusableBufferWriter, mirroring the producer's
    // thread-local KeySerializationBuffer/ValueSerializationBuffer.
    private byte[]? _reusableKeyBuffer;
    private byte[]? _reusableValueBuffer;

    [GlobalSetup]
    public void Setup()
    {
        _buffer = new ArrayBufferWriter<byte>(16384);
        _smallString = new string('a', 10);
        _mediumString = new string('a', 100);
        _largeString = new string('a', 1000);

        _batchKeys = BenchmarkData.CreateKeys(100);

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
            var keyWriter = new ReusableBufferWriter(ref _reusableKeyBuffer, 64);
            Serializers.String.Serialize(_batchKeys[i], ref keyWriter, keyContext);
            var keyMemory = keyWriter.ToPooledMemory();
            keyWriter.UpdateBufferRef(ref _reusableKeyBuffer);

            var valueWriter = new ReusableBufferWriter(ref _reusableValueBuffer, 256);
            Serializers.String.Serialize(_mediumString, ref valueWriter, valueContext);
            var valueMemory = valueWriter.ToPooledMemory();
            valueWriter.UpdateBufferRef(ref _reusableValueBuffer);

            keyMemory.Return();
            valueMemory.Return();
        }
    }

    // ===== ReusableBufferWriter (production path) vs ArrayBufferWriter =====

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
    [Benchmark(Description = "ReusableBufferWriter Direct")]
    public void Serialize_ReusableBufferWriter()
    {
        var writer = new ReusableBufferWriter(ref _reusableValueBuffer, 256);
        Serializers.String.Serialize(_mediumString, ref writer, _context);
        writer.ToPooledMemory().Return();
        writer.UpdateBufferRef(ref _reusableValueBuffer);
    }
}
