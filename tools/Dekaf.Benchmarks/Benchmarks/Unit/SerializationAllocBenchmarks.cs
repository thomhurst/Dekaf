using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Comprehensive allocation benchmarks for all built-in serializers.
/// Each benchmark uses [MemoryDiagnoser] to verify zero-allocation behavior
/// on the serialization path and acceptable allocation on the deserialization path.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class SerializationAllocBenchmarks
{
    private ArrayBufferWriter<byte> _arrayBuffer = null!;
    private SerializationContext _context;

    // Pre-created test values
    private string _shortString = null!;
    private string _mediumString = null!;
    private string _longString = null!;
    private byte[] _byteArray = null!;

    // Pre-serialized bytes for deserialization
    private byte[] _stringBytes = null!;
    private byte[] _int32Bytes = null!;
    private byte[] _int64Bytes = null!;
    private byte[] _guidBytes = null!;
    private byte[] _doubleBytes = null!;
    private byte[] _rawBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _arrayBuffer = new ArrayBufferWriter<byte>(16384);

        _context = new SerializationContext
        {
            Topic = "bench-topic",
            Component = SerializationComponent.Value
        };

        _shortString = "key";
        _mediumString = new string('a', 100);
        _longString = new string('a', 1000);
        _byteArray = new byte[256];
        Random.Shared.NextBytes(_byteArray);

        // Pre-serialize all types for deserialization benchmarks
        _stringBytes = System.Text.Encoding.UTF8.GetBytes(_mediumString);

        _int32Bytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(_int32Bytes, 42);

        _int64Bytes = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(_int64Bytes, 123456789L);

        _guidBytes = new byte[16];
        Guid.NewGuid().TryWriteBytes(_guidBytes, bigEndian: true, out _);

        _doubleBytes = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteDoubleBigEndian(_doubleBytes, 3.14159);

        _rawBytes = new byte[256];
        Random.Shared.NextBytes(_rawBytes);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _arrayBuffer.Clear();
    }

    // ===== String Serializer =====

    [Benchmark(Baseline = true, Description = "String.Serialize (short, 3 chars)")]
    public void StringSerialize_Short()
    {
        Serializers.String.Serialize(_shortString, ref _arrayBuffer, _context);
    }

    [Benchmark(Description = "String.Serialize (medium, 100 chars)")]
    public void StringSerialize_Medium()
    {
        Serializers.String.Serialize(_mediumString, ref _arrayBuffer, _context);
    }

    [Benchmark(Description = "String.Serialize (long, 1000 chars)")]
    public void StringSerialize_Long()
    {
        Serializers.String.Serialize(_longString, ref _arrayBuffer, _context);
    }

    [Benchmark(Description = "String.Deserialize (100 chars)")]
    public string StringDeserialize()
    {
        return Serializers.String.Deserialize(
            new ReadOnlySequence<byte>(_stringBytes), _context);
    }

    // ===== NullableString Serializer =====

    [Benchmark(Description = "NullableString.Serialize (non-null)")]
    public void NullableStringSerialize_NonNull()
    {
        Serializers.NullableString.Serialize(_mediumString, ref _arrayBuffer, _context);
    }

    [Benchmark(Description = "NullableString.Serialize (null)")]
    public void NullableStringSerialize_Null()
    {
        Serializers.NullableString.Serialize(null, ref _arrayBuffer, _context);
    }

    [Benchmark(Description = "NullableString.Deserialize (null)")]
    public string? NullableStringDeserialize_Null()
    {
        var nullContext = new SerializationContext
        {
            Topic = "bench-topic",
            Component = SerializationComponent.Value,
            IsNull = true
        };
        return Serializers.NullableString.Deserialize(
            new ReadOnlySequence<byte>(ReadOnlyMemory<byte>.Empty), nullContext);
    }

    // ===== Int32 Serializer =====

    [Benchmark(Description = "Int32.Serialize")]
    public void Int32Serialize()
    {
        Serializers.Int32.Serialize(42, ref _arrayBuffer, _context);
    }

    [Benchmark(Description = "Int32.Deserialize")]
    public int Int32Deserialize()
    {
        return Serializers.Int32.Deserialize(
            new ReadOnlySequence<byte>(_int32Bytes), _context);
    }

    // ===== Int64 Serializer =====

    [Benchmark(Description = "Int64.Serialize")]
    public void Int64Serialize()
    {
        Serializers.Int64.Serialize(123456789L, ref _arrayBuffer, _context);
    }

    [Benchmark(Description = "Int64.Deserialize")]
    public long Int64Deserialize()
    {
        return Serializers.Int64.Deserialize(
            new ReadOnlySequence<byte>(_int64Bytes), _context);
    }

    // ===== Guid Serializer =====

    [Benchmark(Description = "Guid.Serialize")]
    public void GuidSerialize()
    {
        Serializers.Guid.Serialize(Guid.NewGuid(), ref _arrayBuffer, _context);
    }

    [Benchmark(Description = "Guid.Deserialize")]
    public Guid GuidDeserialize()
    {
        return Serializers.Guid.Deserialize(
            new ReadOnlySequence<byte>(_guidBytes), _context);
    }

    // ===== Double Serializer =====

    [Benchmark(Description = "Double.Serialize")]
    public void DoubleSerialize()
    {
        Serializers.Double.Serialize(3.14159, ref _arrayBuffer, _context);
    }

    [Benchmark(Description = "Double.Deserialize")]
    public double DoubleDeserialize()
    {
        return Serializers.Double.Deserialize(
            new ReadOnlySequence<byte>(_doubleBytes), _context);
    }

    // ===== ByteArray Serializer =====

    [Benchmark(Description = "ByteArray.Serialize (256B)")]
    public void ByteArraySerialize()
    {
        Serializers.ByteArray.Serialize(_byteArray, ref _arrayBuffer, _context);
    }

    [Benchmark(Description = "ByteArray.Deserialize (256B)")]
    public byte[] ByteArrayDeserialize()
    {
        return Serializers.ByteArray.Deserialize(
            new ReadOnlySequence<byte>(_byteArray), _context);
    }

    // ===== RawBytes Serializer (zero-copy) =====

    [Benchmark(Description = "RawBytes.Serialize (256B)")]
    public void RawBytesSerialize()
    {
        ReadOnlyMemory<byte> data = _rawBytes;
        Serializers.RawBytes.Serialize(data, ref _arrayBuffer, _context);
    }

    [Benchmark(Description = "RawBytes.Deserialize (zero-copy, 256B)")]
    public ReadOnlyMemory<byte> RawBytesDeserialize()
    {
        return Serializers.RawBytes.Deserialize(
            new ReadOnlySequence<byte>(_rawBytes), _context);
    }

    // ===== Batch serialization throughput (amortized cost) =====

    [Benchmark(Description = "1000x Int32.Serialize (throughput)")]
    public void Int32Serialize_Thousand()
    {
        for (var i = 0; i < 1000; i++)
        {
            Serializers.Int32.Serialize(i, ref _arrayBuffer, _context);
        }
    }

    [Benchmark(Description = "1000x String.Serialize short keys (throughput)")]
    public void StringSerialize_ThousandKeys()
    {
        for (var i = 0; i < 1000; i++)
        {
            Serializers.String.Serialize(_shortString, ref _arrayBuffer, _context);
        }
    }

    // ===== PooledBufferWriter serialization (actual producer path) =====

    [Benchmark(Description = "PooledBufferWriter: String.Serialize (100 chars)")]
    public PooledMemory PooledWriter_StringSerialize()
    {
        var writer = new PooledBufferWriter(initialCapacity: 128);
        Serializers.String.Serialize(_mediumString, ref writer, _context);
        return writer.ToPooledMemory();
    }

    [Benchmark(Description = "PooledBufferWriter: Int32.Serialize")]
    public PooledMemory PooledWriter_Int32Serialize()
    {
        var writer = new PooledBufferWriter(initialCapacity: 8);
        Serializers.Int32.Serialize(42, ref writer, _context);
        return writer.ToPooledMemory();
    }

    [Benchmark(Description = "PooledBufferWriter: Guid.Serialize")]
    public PooledMemory PooledWriter_GuidSerialize()
    {
        var writer = new PooledBufferWriter(initialCapacity: 16);
        Serializers.Guid.Serialize(Guid.NewGuid(), ref writer, _context);
        return writer.ToPooledMemory();
    }
}
