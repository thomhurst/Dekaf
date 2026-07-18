using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Compression.Snappy;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Compression codec benchmarks.
/// Tests Snappy compression/decompression performance.
/// </summary>
[MemoryDiagnoser]
[ThroughputJob]
public class CompressionBenchmarks
{
    private readonly SnappyCompressionCodec _codec = new();

    private byte[] _smallData = null!;
    private byte[] _largeData = null!;
    private byte[] _smallCompressed = null!;
    private byte[] _largeCompressed = null!;
    private ArrayBufferWriter<byte> _outputBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallData = GenerateTestData(1024);           // 1 KB
        _largeData = GenerateTestData(1024 * 1024);    // 1 MB

        _outputBuffer = new ArrayBufferWriter<byte>(2 * 1024 * 1024);

        _codec.Compress(new ReadOnlySequence<byte>(_smallData), _outputBuffer);
        _smallCompressed = _outputBuffer.WrittenSpan.ToArray();
        _outputBuffer.Clear();

        _codec.Compress(new ReadOnlySequence<byte>(_largeData), _outputBuffer);
        _largeCompressed = _outputBuffer.WrittenSpan.ToArray();
        _outputBuffer.Clear();
    }

    // No [IterationSetup]: its presence would force single-invocation iterations
    // (cold single-shot Tier-0 samples, meaningless statistics for microsecond ops).
    // Each benchmark clears _outputBuffer itself instead.

    // ===== Compression =====

    [Benchmark(Description = "Snappy Compress 1KB")]
    public void Snappy_Compress_1KB()
    {
        _outputBuffer.Clear();
        _codec.Compress(new ReadOnlySequence<byte>(_smallData), _outputBuffer);
    }

    [Benchmark(Description = "Snappy Compress 1MB")]
    public void Snappy_Compress_1MB()
    {
        _outputBuffer.Clear();
        _codec.Compress(new ReadOnlySequence<byte>(_largeData), _outputBuffer);
    }

    // ===== Decompression =====

    [Benchmark(Description = "Snappy Decompress 1KB")]
    public void Snappy_Decompress_1KB()
    {
        _outputBuffer.Clear();
        _codec.Decompress(new ReadOnlySequence<byte>(_smallCompressed), _outputBuffer);
    }

    [Benchmark(Description = "Snappy Decompress 1MB")]
    public void Snappy_Decompress_1MB()
    {
        _outputBuffer.Clear();
        _codec.Decompress(new ReadOnlySequence<byte>(_largeCompressed), _outputBuffer);
    }

    private static byte[] GenerateTestData(int size)
    {
        var pattern = System.Text.Encoding.UTF8.GetBytes(
            """{"id":12345,"name":"test-message","timestamp":1234567890123,"data":"sample payload content here"}"""
        );

        var data = new byte[size];
        for (var i = 0; i < size; i++)
        {
            data[i] = pattern[i % pattern.Length];
        }
        return data;
    }
}
