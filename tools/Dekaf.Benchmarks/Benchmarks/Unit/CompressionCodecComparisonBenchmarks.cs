using System.Buffers;
using System.IO.Compression;
using BenchmarkDotNet.Attributes;
using Dekaf.Compression;
using Dekaf.Compression.Brotli;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Compression codec comparison benchmarks.
/// Compares Brotli, Zstd, LZ4, Snappy, and Gzip across representative payload sizes.
/// </summary>
[MemoryDiagnoser]
public class CompressionCodecComparisonBenchmarks
{
    private readonly GzipCompressionCodec _gzip = new();
    private readonly SnappyCompressionCodec _snappy = new(blockSize: 65536);
    private readonly Lz4CompressionCodec _lz4 = new();
    private readonly ZstdCompressionCodec _zstd = new();
    private readonly BrotliCompressionCodec _brotli = new();

    private byte[] _smallData = null!;   // 1 KB - single small message
    private byte[] _mediumData = null!;  // 64 KB - typical batch
    private byte[] _largeData = null!;   // 1 MB - large batch

    private byte[] _gzipCompressedSmall = null!;
    private byte[] _gzipCompressedMedium = null!;
    private byte[] _gzipCompressedLarge = null!;
    private byte[] _snappyCompressedSmall = null!;
    private byte[] _snappyCompressedMedium = null!;
    private byte[] _snappyCompressedLarge = null!;
    private byte[] _lz4CompressedSmall = null!;
    private byte[] _lz4CompressedMedium = null!;
    private byte[] _lz4CompressedLarge = null!;
    private byte[] _zstdCompressedSmall = null!;
    private byte[] _zstdCompressedMedium = null!;
    private byte[] _zstdCompressedLarge = null!;
    private byte[] _brotliCompressedSmall = null!;
    private byte[] _brotliCompressedMedium = null!;
    private byte[] _brotliCompressedLarge = null!;

    private ArrayBufferWriter<byte> _outputBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallData = GenerateTestData(1024);            // 1 KB
        _mediumData = GenerateTestData(64 * 1024);      // 64 KB
        _largeData = GenerateTestData(1024 * 1024);     // 1 MB

        _outputBuffer = new ArrayBufferWriter<byte>(2 * 1024 * 1024);

        // Pre-compress data for decompression benchmarks
        _gzipCompressedSmall = CompressData(_gzip, _smallData);
        _gzipCompressedMedium = CompressData(_gzip, _mediumData);
        _gzipCompressedLarge = CompressData(_gzip, _largeData);

        _snappyCompressedSmall = CompressData(_snappy, _smallData);
        _snappyCompressedMedium = CompressData(_snappy, _mediumData);
        _snappyCompressedLarge = CompressData(_snappy, _largeData);

        _lz4CompressedSmall = CompressData(_lz4, _smallData);
        _lz4CompressedMedium = CompressData(_lz4, _mediumData);
        _lz4CompressedLarge = CompressData(_lz4, _largeData);

        _zstdCompressedSmall = CompressData(_zstd, _smallData);
        _zstdCompressedMedium = CompressData(_zstd, _mediumData);
        _zstdCompressedLarge = CompressData(_zstd, _largeData);

        _brotliCompressedSmall = CompressData(_brotli, _smallData);
        _brotliCompressedMedium = CompressData(_brotli, _mediumData);
        _brotliCompressedLarge = CompressData(_brotli, _largeData);
    }

    [IterationCleanup]
    public void IterationCleanup() => _outputBuffer.Clear();

    // ===== Compression 1KB =====

    [Benchmark(Description = "Gzip Compress 1KB")]
    [BenchmarkCategory("Compress", "1KB")]
    public void Gzip_Compress_1KB()
    {
        _gzip.Compress(new ReadOnlySequence<byte>(_smallData), _outputBuffer);
    }

    [Benchmark(Description = "Snappy Compress 1KB")]
    [BenchmarkCategory("Compress", "1KB")]
    public void Snappy_Compress_1KB()
    {
        _snappy.Compress(new ReadOnlySequence<byte>(_smallData), _outputBuffer);
    }

    [Benchmark(Description = "LZ4 Compress 1KB")]
    [BenchmarkCategory("Compress", "1KB")]
    public void Lz4_Compress_1KB()
    {
        _lz4.Compress(new ReadOnlySequence<byte>(_smallData), _outputBuffer);
    }

    [Benchmark(Description = "Zstd Compress 1KB")]
    [BenchmarkCategory("Compress", "1KB")]
    public void Zstd_Compress_1KB()
    {
        _zstd.Compress(new ReadOnlySequence<byte>(_smallData), _outputBuffer);
    }

    [Benchmark(Description = "Brotli Compress 1KB")]
    [BenchmarkCategory("Compress", "1KB")]
    public void Brotli_Compress_1KB()
    {
        _brotli.Compress(new ReadOnlySequence<byte>(_smallData), _outputBuffer);
    }

    // ===== Compression 64KB =====

    [Benchmark(Description = "Gzip Compress 64KB")]
    [BenchmarkCategory("Compress", "64KB")]
    public void Gzip_Compress_64KB()
    {
        _gzip.Compress(new ReadOnlySequence<byte>(_mediumData), _outputBuffer);
    }

    [Benchmark(Description = "Snappy Compress 64KB")]
    [BenchmarkCategory("Compress", "64KB")]
    public void Snappy_Compress_64KB()
    {
        _snappy.Compress(new ReadOnlySequence<byte>(_mediumData), _outputBuffer);
    }

    [Benchmark(Description = "LZ4 Compress 64KB")]
    [BenchmarkCategory("Compress", "64KB")]
    public void Lz4_Compress_64KB()
    {
        _lz4.Compress(new ReadOnlySequence<byte>(_mediumData), _outputBuffer);
    }

    [Benchmark(Description = "Zstd Compress 64KB")]
    [BenchmarkCategory("Compress", "64KB")]
    public void Zstd_Compress_64KB()
    {
        _zstd.Compress(new ReadOnlySequence<byte>(_mediumData), _outputBuffer);
    }

    [Benchmark(Description = "Brotli Compress 64KB")]
    [BenchmarkCategory("Compress", "64KB")]
    public void Brotli_Compress_64KB()
    {
        _brotli.Compress(new ReadOnlySequence<byte>(_mediumData), _outputBuffer);
    }

    // ===== Compression 1MB =====

    [Benchmark(Description = "Gzip Compress 1MB")]
    [BenchmarkCategory("Compress", "1MB")]
    public void Gzip_Compress_1MB()
    {
        _gzip.Compress(new ReadOnlySequence<byte>(_largeData), _outputBuffer);
    }

    [Benchmark(Description = "Snappy Compress 1MB")]
    [BenchmarkCategory("Compress", "1MB")]
    public void Snappy_Compress_1MB()
    {
        _snappy.Compress(new ReadOnlySequence<byte>(_largeData), _outputBuffer);
    }

    [Benchmark(Description = "LZ4 Compress 1MB")]
    [BenchmarkCategory("Compress", "1MB")]
    public void Lz4_Compress_1MB()
    {
        _lz4.Compress(new ReadOnlySequence<byte>(_largeData), _outputBuffer);
    }

    [Benchmark(Description = "Zstd Compress 1MB")]
    [BenchmarkCategory("Compress", "1MB")]
    public void Zstd_Compress_1MB()
    {
        _zstd.Compress(new ReadOnlySequence<byte>(_largeData), _outputBuffer);
    }

    [Benchmark(Description = "Brotli Compress 1MB")]
    [BenchmarkCategory("Compress", "1MB")]
    public void Brotli_Compress_1MB()
    {
        _brotli.Compress(new ReadOnlySequence<byte>(_largeData), _outputBuffer);
    }

    // ===== Decompression 1KB =====

    [Benchmark(Description = "Gzip Decompress 1KB")]
    [BenchmarkCategory("Decompress", "1KB")]
    public void Gzip_Decompress_1KB()
    {
        _gzip.Decompress(new ReadOnlySequence<byte>(_gzipCompressedSmall), _outputBuffer);
    }

    [Benchmark(Description = "Snappy Decompress 1KB")]
    [BenchmarkCategory("Decompress", "1KB")]
    public void Snappy_Decompress_1KB()
    {
        _snappy.Decompress(new ReadOnlySequence<byte>(_snappyCompressedSmall), _outputBuffer);
    }

    [Benchmark(Description = "LZ4 Decompress 1KB")]
    [BenchmarkCategory("Decompress", "1KB")]
    public void Lz4_Decompress_1KB()
    {
        _lz4.Decompress(new ReadOnlySequence<byte>(_lz4CompressedSmall), _outputBuffer);
    }

    [Benchmark(Description = "Zstd Decompress 1KB")]
    [BenchmarkCategory("Decompress", "1KB")]
    public void Zstd_Decompress_1KB()
    {
        _zstd.Decompress(new ReadOnlySequence<byte>(_zstdCompressedSmall), _outputBuffer);
    }

    [Benchmark(Description = "Brotli Decompress 1KB")]
    [BenchmarkCategory("Decompress", "1KB")]
    public void Brotli_Decompress_1KB()
    {
        _brotli.Decompress(new ReadOnlySequence<byte>(_brotliCompressedSmall), _outputBuffer);
    }

    // ===== Decompression 64KB =====

    [Benchmark(Description = "Gzip Decompress 64KB")]
    [BenchmarkCategory("Decompress", "64KB")]
    public void Gzip_Decompress_64KB()
    {
        _gzip.Decompress(new ReadOnlySequence<byte>(_gzipCompressedMedium), _outputBuffer);
    }

    [Benchmark(Description = "Snappy Decompress 64KB")]
    [BenchmarkCategory("Decompress", "64KB")]
    public void Snappy_Decompress_64KB()
    {
        _snappy.Decompress(new ReadOnlySequence<byte>(_snappyCompressedMedium), _outputBuffer);
    }

    [Benchmark(Description = "LZ4 Decompress 64KB")]
    [BenchmarkCategory("Decompress", "64KB")]
    public void Lz4_Decompress_64KB()
    {
        _lz4.Decompress(new ReadOnlySequence<byte>(_lz4CompressedMedium), _outputBuffer);
    }

    [Benchmark(Description = "Zstd Decompress 64KB")]
    [BenchmarkCategory("Decompress", "64KB")]
    public void Zstd_Decompress_64KB()
    {
        _zstd.Decompress(new ReadOnlySequence<byte>(_zstdCompressedMedium), _outputBuffer);
    }

    [Benchmark(Description = "Brotli Decompress 64KB")]
    [BenchmarkCategory("Decompress", "64KB")]
    public void Brotli_Decompress_64KB()
    {
        _brotli.Decompress(new ReadOnlySequence<byte>(_brotliCompressedMedium), _outputBuffer);
    }

    // ===== Decompression 1MB =====

    [Benchmark(Description = "Gzip Decompress 1MB")]
    [BenchmarkCategory("Decompress", "1MB")]
    public void Gzip_Decompress_1MB()
    {
        _gzip.Decompress(new ReadOnlySequence<byte>(_gzipCompressedLarge), _outputBuffer);
    }

    [Benchmark(Description = "Snappy Decompress 1MB")]
    [BenchmarkCategory("Decompress", "1MB")]
    public void Snappy_Decompress_1MB()
    {
        _snappy.Decompress(new ReadOnlySequence<byte>(_snappyCompressedLarge), _outputBuffer);
    }

    [Benchmark(Description = "LZ4 Decompress 1MB")]
    [BenchmarkCategory("Decompress", "1MB")]
    public void Lz4_Decompress_1MB()
    {
        _lz4.Decompress(new ReadOnlySequence<byte>(_lz4CompressedLarge), _outputBuffer);
    }

    [Benchmark(Description = "Zstd Decompress 1MB")]
    [BenchmarkCategory("Decompress", "1MB")]
    public void Zstd_Decompress_1MB()
    {
        _zstd.Decompress(new ReadOnlySequence<byte>(_zstdCompressedLarge), _outputBuffer);
    }

    [Benchmark(Description = "Brotli Decompress 1MB")]
    [BenchmarkCategory("Decompress", "1MB")]
    public void Brotli_Decompress_1MB()
    {
        _brotli.Decompress(new ReadOnlySequence<byte>(_brotliCompressedLarge), _outputBuffer);
    }

    // ===== Helpers =====

    private byte[] CompressData(ICompressionCodec codec, byte[] data)
    {
        var buffer = new ArrayBufferWriter<byte>(data.Length);
        codec.Compress(new ReadOnlySequence<byte>(data), buffer);
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Generates representative JSON-like Kafka message payloads.
    /// Uses a repeating JSON pattern to simulate realistic compressibility.
    /// </summary>
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
