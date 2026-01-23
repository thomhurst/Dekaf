using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Compression.Snappy;

namespace Dekaf.Benchmarks;

/// <summary>
/// Benchmarks for Snappy compression codec.
/// Verifies zero-allocation behavior and measures throughput.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class SnappyCompressionBenchmarks
{
    private readonly SnappyCompressionCodec _codec = new(blockSize: 65536);

    private byte[] _smallData = null!;
    private byte[] _largeData = null!;
    private byte[] _smallCompressed = null!;
    private byte[] _largeCompressed = null!;
    private ArrayBufferWriter<byte> _outputBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Generate test data with realistic patterns (not just random bytes)
        _smallData = GenerateTestData(1024);           // 1 KB
        _largeData = GenerateTestData(1024 * 1024);    // 1 MB

        // Pre-compress data for decompression benchmarks
        _outputBuffer = new ArrayBufferWriter<byte>(2 * 1024 * 1024);

        _codec.Compress(new ReadOnlySequence<byte>(_smallData), _outputBuffer);
        _smallCompressed = _outputBuffer.WrittenSpan.ToArray();
        _outputBuffer.Clear();

        _codec.Compress(new ReadOnlySequence<byte>(_largeData), _outputBuffer);
        _largeCompressed = _outputBuffer.WrittenSpan.ToArray();
        _outputBuffer.Clear();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _outputBuffer.Clear();
    }

    // ===== Compression Benchmarks =====

    [Benchmark(Description = "Snappy Compress: 1KB")]
    public void Compress_SmallMessage()
    {
        _codec.Compress(new ReadOnlySequence<byte>(_smallData), _outputBuffer);
    }

    [Benchmark(Description = "Snappy Compress: 1MB")]
    public void Compress_LargeMessage()
    {
        _codec.Compress(new ReadOnlySequence<byte>(_largeData), _outputBuffer);
    }

    // ===== Decompression Benchmarks =====

    [Benchmark(Description = "Snappy Decompress: 1KB")]
    public void Decompress_SmallMessage()
    {
        _codec.Decompress(new ReadOnlySequence<byte>(_smallCompressed), _outputBuffer);
    }

    [Benchmark(Description = "Snappy Decompress: 1MB")]
    public void Decompress_LargeMessage()
    {
        _codec.Decompress(new ReadOnlySequence<byte>(_largeCompressed), _outputBuffer);
    }

    // ===== Multi-Segment Sequence Benchmarks =====

    [Benchmark(Description = "Snappy Compress: 1KB (multi-segment)")]
    public void Compress_SmallMessage_MultiSegment()
    {
        var sequence = CreateMultiSegmentSequence(_smallData, chunkSize: 256);
        _codec.Compress(sequence, _outputBuffer);
    }

    [Benchmark(Description = "Snappy Compress: 1MB (multi-segment)")]
    public void Compress_LargeMessage_MultiSegment()
    {
        var sequence = CreateMultiSegmentSequence(_largeData, chunkSize: 16384);
        _codec.Compress(sequence, _outputBuffer);
    }

    /// <summary>
    /// Generates test data with realistic patterns that compress well.
    /// Uses repeating JSON-like structures to simulate Kafka message payloads.
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

    /// <summary>
    /// Creates a multi-segment ReadOnlySequence from a byte array.
    /// This tests the codec's handling of non-contiguous memory.
    /// </summary>
    private static ReadOnlySequence<byte> CreateMultiSegmentSequence(byte[] data, int chunkSize)
    {
        if (data.Length <= chunkSize)
            return new ReadOnlySequence<byte>(data);

        var segments = new List<ReadOnlyMemory<byte>>();
        for (var i = 0; i < data.Length; i += chunkSize)
        {
            var length = Math.Min(chunkSize, data.Length - i);
            segments.Add(data.AsMemory(i, length));
        }

        // Build the linked list of segments
        var first = new MemorySegment<byte>(segments[0]);
        var last = first;
        for (var i = 1; i < segments.Count; i++)
        {
            last = last.Append(segments[i]);
        }

        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    /// <summary>
    /// Memory segment for building multi-segment sequences.
    /// </summary>
    private sealed class MemorySegment<T> : ReadOnlySequenceSegment<T>
    {
        public MemorySegment(ReadOnlyMemory<T> memory)
        {
            Memory = memory;
        }

        public MemorySegment<T> Append(ReadOnlyMemory<T> memory)
        {
            var segment = new MemorySegment<T>(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }
}
