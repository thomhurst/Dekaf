using System.Buffers;
using System.IO.Compression;
using System.Text;
using Dekaf.Compression;
using Dekaf.Compression.Brotli;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Compression;

/// <summary>
/// Tests for Brotli compression codec.
/// Note: Brotli is NOT a standard Kafka compression type. Both producer and consumer
/// must have the Dekaf.Compression.Brotli codec installed.
/// </summary>
public class BrotliCompressionCodecTests
{
    #region Basic Functionality Tests

    [Test]
    public async Task BrotliCompressionCodec_Type_ReturnsBrotli()
    {
        var codec = new BrotliCompressionCodec();
        await Assert.That(codec.Type).IsEqualTo(CompressionType.Brotli);
    }

    [Test]
    public async Task BrotliCompressionCodec_RoundTrip_PreservesData()
    {
        var codec = new BrotliCompressionCodec();
        var originalData = "Hello, Kafka! This is a test message for Brotli compression."u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task BrotliCompressionCodec_RoundTrip_EmptyData()
    {
        var codec = new BrotliCompressionCodec();
        var originalData = Array.Empty<byte>();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task BrotliCompressionCodec_RoundTrip_LargeData()
    {
        var codec = new BrotliCompressionCodec();
        var originalData = new byte[100_000];
        Random.Shared.NextBytes(originalData);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task BrotliCompressionCodec_Compress_ActuallyCompressesData()
    {
        var codec = new BrotliCompressionCodec();
        // Highly compressible repeated data
        var originalData = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Kafka message ", 1000)));

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        // Compressed output should be smaller than the original
        await Assert.That(compressedBuffer.WrittenCount).IsLessThan(originalData.Length);
    }

    #endregion

    #region Compression Level Tests

    [Test]
    public async Task BrotliCompressionCodec_WithSmallestSize_ProducesSmallerOrEqualOutput()
    {
        var fastCodec = new BrotliCompressionCodec(CompressionLevel.Fastest);
        var smallCodec = new BrotliCompressionCodec(CompressionLevel.SmallestSize);

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

    [Test]
    public async Task BrotliCompressionCodec_NoCompression_RoundTripPreservesData()
    {
        var codec = new BrotliCompressionCodec(CompressionLevel.NoCompression);
        var originalData = "Test data for Brotli NoCompression"u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task BrotliCompressionCodec_SmallestSize_RoundTripPreservesData()
    {
        var codec = new BrotliCompressionCodec(CompressionLevel.SmallestSize);
        var originalData = "Test data for Brotli SmallestSize"u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task BrotliCompressionCodec_SmallestSize_ProducesSmallerOutputThanNoCompression()
    {
        var noCompressionCodec = new BrotliCompressionCodec(CompressionLevel.NoCompression);
        var smallestSizeCodec = new BrotliCompressionCodec(CompressionLevel.SmallestSize);

        var originalData = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Kafka message ", 1000)));

        var noCompressionBuffer = new ArrayBufferWriter<byte>();
        noCompressionCodec.Compress(new ReadOnlySequence<byte>(originalData), noCompressionBuffer);

        var smallestSizeBuffer = new ArrayBufferWriter<byte>();
        smallestSizeCodec.Compress(new ReadOnlySequence<byte>(originalData), smallestSizeBuffer);

        await Assert.That(smallestSizeBuffer.WrittenCount).IsLessThan(noCompressionBuffer.WrittenCount);
    }

    [Test]
    public async Task BrotliCompressionCodec_CrossLevel_CompressSmallestSizeDecompressDefault()
    {
        var compressor = new BrotliCompressionCodec(CompressionLevel.SmallestSize);
        var decompressor = new BrotliCompressionCodec(); // default level

        var originalData = "Cross-level Brotli decompression test"u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        compressor.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        decompressor.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    #endregion

    #region Multi-Segment Tests

    [Test]
    public async Task BrotliCompressionCodec_RoundTrip_MultiSegmentSequence()
    {
        var codec = new BrotliCompressionCodec();

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

    #region Extension Methods Tests

    [Test]
    public async Task AddBrotliExtension_RegistersCodec()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddBrotli();

        await Assert.That(registry.IsSupported(CompressionType.Brotli)).IsTrue();

        var codec = registry.GetCodec(CompressionType.Brotli);
        await Assert.That(codec).IsTypeOf<BrotliCompressionCodec>();
    }

    [Test]
    public async Task AddBrotliExtension_ReturnsSameRegistry_ForFluentChaining()
    {
        var registry = new CompressionCodecRegistry();
        var result = registry.AddBrotli();

        await Assert.That(result).IsSameReferenceAs(registry);
    }

    [Test]
    public async Task AddBrotliExtension_WithCompressionLevel_RegistersCodecWithLevel()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddBrotli(compressionLevel: CompressionLevel.SmallestSize);

        await Assert.That(registry.IsSupported(CompressionType.Brotli)).IsTrue();

        // Verify it works by compressing and decompressing
        var codec = registry.GetCodec(CompressionType.Brotli);
        var data = "test"u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(data);
    }


    #endregion

    #region Helper Classes

    /// <summary>
    /// Helper class for creating multi-segment ReadOnlySequence for testing.
    /// </summary>
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
