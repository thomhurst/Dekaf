using System.Buffers;
using System.IO.Compression;
using System.Text;
using Dekaf.Compression;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Compression;

public class GzipCompressionCodecTests
{
    #region Basic Functionality Tests

    [Test]
    public async Task GzipCompressionCodec_Type_ReturnsGzip()
    {
        var codec = new GzipCompressionCodec();
        await Assert.That(codec.Type).IsEqualTo(CompressionType.Gzip);
    }

    [Test]
    public async Task GzipCompressionCodec_RoundTrip_PreservesData()
    {
        var codec = new GzipCompressionCodec();
        var originalData = "Hello, Kafka! This is a test message for Gzip compression."u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task GzipCompressionCodec_RoundTrip_EmptyData()
    {
        var codec = new GzipCompressionCodec();
        var originalData = Array.Empty<byte>();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task GzipCompressionCodec_RoundTrip_LargeData()
    {
        var codec = new GzipCompressionCodec();
        var originalData = new byte[100_000];
        Random.Shared.NextBytes(originalData);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task GzipCompressionCodec_Compress_ProducesGzipMagicBytes()
    {
        var codec = new GzipCompressionCodec();
        var data = "Test data for Gzip magic bytes verification"u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);

        // Gzip magic bytes: 0x1F 0x8B
        await Assert.That(compressedBuffer.WrittenSpan[0]).IsEqualTo((byte)0x1F);
        await Assert.That(compressedBuffer.WrittenSpan[1]).IsEqualTo((byte)0x8B);
    }

    #endregion

    #region Compression Level Tests

    [Test]
    public async Task GzipCompressionCodec_WithSmallestSize_ProducesSmallerOrEqualOutput()
    {
        var fastCodec = new GzipCompressionCodec(CompressionLevel.Fastest);
        var smallCodec = new GzipCompressionCodec(CompressionLevel.SmallestSize);

        var originalData = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Kafka message ", 1000)));

        var fastBuffer = new ArrayBufferWriter<byte>();
        fastCodec.Compress(new ReadOnlySequence<byte>(originalData), fastBuffer);

        var smallBuffer = new ArrayBufferWriter<byte>();
        smallCodec.Compress(new ReadOnlySequence<byte>(originalData), smallBuffer);

        await Assert.That(smallBuffer.WrittenCount).IsLessThanOrEqualTo(fastBuffer.WrittenCount);

        // Verify both decompress correctly
        var fastDecompressed = new ArrayBufferWriter<byte>();
        fastCodec.Decompress(new ReadOnlySequence<byte>(fastBuffer.WrittenMemory), fastDecompressed);
        await Assert.That(fastDecompressed.WrittenSpan.ToArray()).IsEquivalentTo(originalData);

        var smallDecompressed = new ArrayBufferWriter<byte>();
        smallCodec.Decompress(new ReadOnlySequence<byte>(smallBuffer.WrittenMemory), smallDecompressed);
        await Assert.That(smallDecompressed.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    #endregion

    #region Multi-Segment Tests

    [Test]
    public async Task GzipCompressionCodec_RoundTrip_MultiSegmentSequence()
    {
        var codec = new GzipCompressionCodec();

        var segment1 = new byte[] { 1, 2, 3, 4, 5 };
        var segment2 = new byte[] { 6, 7, 8, 9, 10 };
        var segment3 = new byte[] { 11, 12, 13, 14, 15 };

        var firstSegment = new TestMemorySegment(segment1);
        var secondSegment = firstSegment.Append(segment2);
        var thirdSegment = secondSegment.Append(segment3);

        var sequence = new ReadOnlySequence<byte>(firstSegment, 0, thirdSegment, segment3.Length);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(sequence, compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        var expectedData = segment1.Concat(segment2).Concat(segment3).ToArray();
        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(expectedData);
    }

    #endregion

    #region Helper Classes

    private sealed class TestMemorySegment : ReadOnlySequenceSegment<byte>
    {
        public TestMemorySegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public TestMemorySegment Append(ReadOnlyMemory<byte> memory)
        {
            var segment = new TestMemorySegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }

    #endregion
}
