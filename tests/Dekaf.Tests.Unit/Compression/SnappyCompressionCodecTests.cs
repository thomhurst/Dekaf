using System.Buffers;
using System.Text;
using Dekaf.Compression;
using Dekaf.Compression.Snappy;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Compression;

public class SnappyCompressionCodecTests
{
    [Test]
    public async Task Type_ReturnsSnappy()
    {
        var codec = new SnappyCompressionCodec();

        await Assert.That(codec.Type).IsEqualTo(CompressionType.Snappy);
    }

    [Test]
    public async Task CompressDecompress_RoundTrip_SmallData()
    {
        var codec = new SnappyCompressionCodec();
        var original = "Hello, Kafka with Snappy compression!"u8.ToArray();
        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(original);
    }

    [Test]
    public async Task CompressDecompress_RoundTrip_LargeData()
    {
        var codec = new SnappyCompressionCodec(blockSize: 1024);
        var original = new byte[10000];
        Random.Shared.NextBytes(original);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(original);
    }

    [Test]
    public async Task CompressDecompress_RoundTrip_RepetitiveData()
    {
        // Repetitive data compresses well
        var codec = new SnappyCompressionCodec();
        var original = Encoding.UTF8.GetBytes(new string('A', 10000));

        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(original);
        // Compressed should be smaller for repetitive data
        await Assert.That(compressedBuffer.WrittenCount).IsLessThan(original.Length);
    }

    [Test]
    public async Task Compress_ProducesXerialMagicHeader()
    {
        var codec = new SnappyCompressionCodec();
        var data = "test"u8.ToArray();
        var compressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);

        // Xerial magic header: 0x82 0x53 0x4e 0x41 0x50 0x50 0x59 0x00
        var expectedMagic = new byte[] { 0x82, 0x53, 0x4e, 0x41, 0x50, 0x50, 0x59, 0x00 };
        await Assert.That(compressedBuffer.WrittenSpan.Slice(0, 8).ToArray()).IsEquivalentTo(expectedMagic);
    }

    [Test]
    public async Task Decompress_InvalidMagicHeader_Throws()
    {
        var codec = new SnappyCompressionCodec();
        var invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        await Assert.That(() => codec.Decompress(new ReadOnlySequence<byte>(invalidData), decompressedBuffer))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Decompress_TruncatedHeader_Throws()
    {
        var codec = new SnappyCompressionCodec();
        var truncatedData = new byte[] { 0x82, 0x53, 0x4e, 0x41 }; // Only 4 bytes
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        await Assert.That(() => codec.Decompress(new ReadOnlySequence<byte>(truncatedData), decompressedBuffer))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task CompressDecompress_EmptyData()
    {
        var codec = new SnappyCompressionCodec();
        var original = Array.Empty<byte>();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenCount).IsEqualTo(0);
    }

    [Test]
    public async Task CompressDecompress_MultipleBlocks()
    {
        // Use small block size to force multiple blocks
        var codec = new SnappyCompressionCodec(blockSize: 100);
        var original = new byte[500];
        Random.Shared.NextBytes(original);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(new ReadOnlySequence<byte>(original), compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(original);
    }

    [Test]
    public async Task Constructor_InvalidBlockSize_Throws()
    {
        await Assert.That(() => new SnappyCompressionCodec(blockSize: 0))
            .Throws<ArgumentOutOfRangeException>();

        await Assert.That(() => new SnappyCompressionCodec(blockSize: -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AddSnappy_RegistersCodec()
    {
        var registry = new CompressionCodecRegistry();

        registry.AddSnappy();

        await Assert.That(registry.IsSupported(CompressionType.Snappy)).IsTrue();
        await Assert.That(registry.GetCodec(CompressionType.Snappy)).IsTypeOf<SnappyCompressionCodec>();
    }

    [Test]
    public async Task AddSnappy_WithCustomBlockSize_RegistersCodec()
    {
        var registry = new CompressionCodecRegistry();

        registry.AddSnappy(blockSize: 32768);

        await Assert.That(registry.IsSupported(CompressionType.Snappy)).IsTrue();
    }

    [Test]
    public async Task CompressDecompress_MultiSegmentSequence()
    {
        var codec = new SnappyCompressionCodec();

        // Create a multi-segment ReadOnlySequence
        var segment1 = new byte[] { 1, 2, 3, 4, 5 };
        var segment2 = new byte[] { 6, 7, 8, 9, 10 };
        var firstSegment = new TestMemorySegment<byte>(segment1);
        var lastSegment = firstSegment.Append(segment2);
        var multiSegmentSequence = new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, segment2.Length);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        var decompressedBuffer = new ArrayBufferWriter<byte>();

        codec.Compress(multiSegmentSequence, compressedBuffer);
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        var expected = segment1.Concat(segment2).ToArray();
        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(expected);
    }
}

/// <summary>
/// Helper class for creating multi-segment ReadOnlySequence for testing.
/// </summary>
internal sealed class TestMemorySegment<T> : ReadOnlySequenceSegment<T>
{
    public TestMemorySegment(ReadOnlyMemory<T> memory)
    {
        Memory = memory;
    }

    public TestMemorySegment<T> Append(ReadOnlyMemory<T> memory)
    {
        var segment = new TestMemorySegment<T>(memory)
        {
            RunningIndex = RunningIndex + Memory.Length
        };
        Next = segment;
        return segment;
    }
}
