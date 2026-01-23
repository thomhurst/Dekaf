using System.Buffers;
using System.Text;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;
using Dekaf.Protocol.Records;
using K4os.Compression.LZ4;

namespace Dekaf.Tests.Unit.Compression;

/// <summary>
/// Tests for LZ4 compression codec.
/// </summary>
public class Lz4CompressionCodecTests
{
    #region Basic Functionality Tests

    [Test]
    public async Task Lz4CompressionCodec_Type_ReturnsLz4()
    {
        var codec = new Lz4CompressionCodec();

        await Assert.That(codec.Type).IsEqualTo(CompressionType.Lz4);
    }

    [Test]
    public async Task Lz4CompressionCodec_RoundTrip_PreservesData()
    {
        var codec = new Lz4CompressionCodec();
        var originalData = "Hello, Kafka! This is a test message for LZ4 compression."u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task Lz4CompressionCodec_RoundTrip_EmptyData()
    {
        var codec = new Lz4CompressionCodec();
        var originalData = Array.Empty<byte>();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task Lz4CompressionCodec_RoundTrip_LargeData()
    {
        var codec = new Lz4CompressionCodec();
        var originalData = new byte[100_000];
        Random.Shared.NextBytes(originalData);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task Lz4CompressionCodec_Compress_ProducesFrameFormat()
    {
        var codec = new Lz4CompressionCodec();
        var data = "Test data for LZ4 frame format verification"u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);

        // LZ4 frame magic bytes: 0x04 0x22 0x4D 0x18
        var magic = compressedBuffer.WrittenSpan[..4].ToArray();
        await Assert.That(magic[0]).IsEqualTo((byte)0x04);
        await Assert.That(magic[1]).IsEqualTo((byte)0x22);
        await Assert.That(magic[2]).IsEqualTo((byte)0x4D);
        await Assert.That(magic[3]).IsEqualTo((byte)0x18);
    }

    #endregion

    #region Compression Level Tests

    [Test]
    public async Task Lz4CompressionCodec_WithHighCompression_ProducesSmallerOutput()
    {
        var fastCodec = new Lz4CompressionCodec(LZ4Level.L00_FAST);
        var highCodec = new Lz4CompressionCodec(LZ4Level.L12_MAX);

        // Create compressible data (repeated patterns compress well)
        var originalData = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Kafka message ", 1000)));

        var fastBuffer = new ArrayBufferWriter<byte>();
        fastCodec.Compress(new ReadOnlySequence<byte>(originalData), fastBuffer);

        var highBuffer = new ArrayBufferWriter<byte>();
        highCodec.Compress(new ReadOnlySequence<byte>(originalData), highBuffer);

        // High compression should produce smaller or equal output
        await Assert.That(highBuffer.WrittenCount).IsLessThanOrEqualTo(fastBuffer.WrittenCount);

        // Verify both decompress correctly
        var fastDecompressed = new ArrayBufferWriter<byte>();
        fastCodec.Decompress(new ReadOnlySequence<byte>(fastBuffer.WrittenMemory), fastDecompressed);
        await Assert.That(fastDecompressed.WrittenSpan.ToArray()).IsEquivalentTo(originalData);

        var highDecompressed = new ArrayBufferWriter<byte>();
        highCodec.Decompress(new ReadOnlySequence<byte>(highBuffer.WrittenMemory), highDecompressed);
        await Assert.That(highDecompressed.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    #endregion

    #region Multi-Segment Tests

    [Test]
    public async Task Lz4CompressionCodec_RoundTrip_MultiSegmentSequence()
    {
        var codec = new Lz4CompressionCodec();

        // Create a multi-segment sequence
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
    public async Task AddLz4Extension_RegistersCodec()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddLz4();

        await Assert.That(registry.IsSupported(CompressionType.Lz4)).IsTrue();

        var codec = registry.GetCodec(CompressionType.Lz4);
        await Assert.That(codec).IsTypeOf<Lz4CompressionCodec>();
    }

    [Test]
    public async Task AddLz4Extension_ReturnsSameRegistry_ForFluentChaining()
    {
        var registry = new CompressionCodecRegistry();
        var result = registry.AddLz4();

        await Assert.That(result).IsSameReferenceAs(registry);
    }

    [Test]
    public async Task AddLz4Extension_WithCompressionLevel_RegistersCodecWithLevel()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddLz4(LZ4Level.L09_HC);

        await Assert.That(registry.IsSupported(CompressionType.Lz4)).IsTrue();

        // Verify it works by compressing and decompressing
        var codec = registry.GetCodec(CompressionType.Lz4);
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
