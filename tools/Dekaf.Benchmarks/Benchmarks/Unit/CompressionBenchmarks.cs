using System.Buffers;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Producer;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Compression codec benchmarks.
/// Tests Snappy codecs and producer precompression buffer ownership across codecs.
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

    private RecordBatch _batch1MB = null!;
    private RecordBatch _batch5MB = null!;
    private Action _zstd1MB = null!;
    private Action _zstd5MB = null!;
    private Action _lz41MB = null!;
    private Action _lz45MB = null!;
    private Action _snappy1MB = null!;
    private Action _snappy5MB = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallData = GenerateTestData(1024);           // 1 KB
        _largeData = GenerateTestData(1024 * 1024);    // 1 MB

        var registry = new CompressionCodecRegistry();
        registry.AddZstd().AddLz4().AddSnappy();
        var data1MB = GenerateMixedData(1024 * 1024);
        var data5MB = GenerateMixedData(5 * 1024 * 1024);
        _batch1MB = new RecordBatch();
        _batch1MB.SetPreEncodedRecords(data1MB);
        _batch5MB = new RecordBatch();
        _batch5MB.SetPreEncodedRecords(data5MB);
        _zstd1MB = BindPreCompression(_batch1MB, registry, CompressionType.Zstd, data1MB);
        _zstd5MB = BindPreCompression(_batch5MB, registry, CompressionType.Zstd, data5MB);
        _lz41MB = BindPreCompression(_batch1MB, registry, CompressionType.Lz4, data1MB);
        _lz45MB = BindPreCompression(_batch5MB, registry, CompressionType.Lz4, data5MB);
        _snappy1MB = BindPreCompression(_batch1MB, registry, CompressionType.Snappy, data1MB);
        _snappy5MB = BindPreCompression(_batch5MB, registry, CompressionType.Snappy, data5MB);

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

    // Includes the producer's output rent, growth, estimate snapshot, CRC, and pool return.
    // The 5 MiB case exceeds both default BatchSize and the producer pool's 4 MiB cap.
    [Benchmark]
    public int Zstd_PreCompress_1MB() => PreCompressAndReturn(_batch1MB, _zstd1MB);

    [Benchmark]
    public int Zstd_PreCompress_5MB() => PreCompressAndReturn(_batch5MB, _zstd5MB);

    [Benchmark]
    public int Lz4_PreCompress_1MB() => PreCompressAndReturn(_batch1MB, _lz41MB);

    [Benchmark]
    public int Lz4_PreCompress_5MB() => PreCompressAndReturn(_batch5MB, _lz45MB);

    [Benchmark]
    public int Snappy_PreCompress_1MB() => PreCompressAndReturn(_batch1MB, _snappy1MB);

    [Benchmark]
    public int Snappy_PreCompress_5MB() => PreCompressAndReturn(_batch5MB, _snappy5MB);

    private static int PreCompressAndReturn(RecordBatch batch, Action preCompress)
    {
        preCompress();
        var length = batch.PreCompressedLength;
        batch.ReturnPreCompressedBuffer();
        return length;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _batch1MB.Dispose();
        _batch5MB.Dispose();
    }

    private static Action BindPreCompression(
        RecordBatch batch, CompressionCodecRegistry registry, CompressionType compression, byte[] data)
    {
        var scratch = new ArrayBufferWriter<byte>();
        registry.GetCodec(compression).Compress(new ReadOnlySequence<byte>(data), scratch);
        var ratio = (double)scratch.WrittenCount / data.Length;

        // Bind once so the gate can compile this fixture against the pre-estimate baseline.
        // Both revisions measure one cached delegate call; no reflection runs per batch.
        var estimatedMethod = typeof(RecordBatch).GetMethod("PreCompress", BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(CompressionType), typeof(CompressionCodecRegistry), typeof(double)], null);
        if (estimatedMethod is not null)
        {
            var estimator = new CompressionRatioEstimator();
            for (var i = 0; i < 200; i++)
                estimator.Update("benchmark", compression, ratio);
            var compress = estimatedMethod.CreateDelegate<Action<CompressionType, CompressionCodecRegistry, double>>(batch);
            return () => compress(compression, registry, estimator.Get("benchmark", compression));
        }

        var method = typeof(RecordBatch).GetMethod("PreCompress", BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(CompressionType), typeof(CompressionCodecRegistry)], null)!;
        var baselineCompress = method.CreateDelegate<Action<CompressionType, CompressionCodecRegistry>>(batch);
        return () => baselineCompress(compression, registry);
    }

    private static byte[] GenerateMixedData(int size)
    {
        var data = new byte[size];
        var random = new Random(3827);
        // Independent random quarters mixed with repeated bytes model a roughly 25% ratio.
        for (var offset = 0; offset < data.Length; offset += 4096)
            random.NextBytes(data.AsSpan(offset, Math.Min(1024, data.Length - offset)));
        return data;
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
