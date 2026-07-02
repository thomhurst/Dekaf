using System.Buffers;
using System.Reflection;
using System.Text;
using Dekaf.Compression;
using Dekaf.Compression.Zstd;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Compression;

/// <summary>
/// Tests for Zstd compression codec.
/// </summary>
public class ZstdCompressionCodecTests
{
    #region Basic Functionality Tests

    [Test]
    public async Task ZstdCompressionCodec_Type_ReturnsZstd()
    {
        var codec = new ZstdCompressionCodec();

        await Assert.That(codec.Type).IsEqualTo(CompressionType.Zstd);
    }

    [Test]
    public async Task ZstdCompressionCodec_RoundTrip_PreservesData()
    {
        var codec = new ZstdCompressionCodec();
        var originalData = "Hello, Kafka! This is a test message for Zstd compression."u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task ZstdCompressionCodec_RoundTrip_EmptyData()
    {
        var codec = new ZstdCompressionCodec();
        var originalData = Array.Empty<byte>();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task ZstdCompressionCodec_RoundTrip_LargeData()
    {
        var codec = new ZstdCompressionCodec();
        var originalData = new byte[100_000];
        Random.Shared.NextBytes(originalData);

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(originalData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(originalData);
    }

    [Test]
    public async Task ZstdCompressionCodec_Compress_ProducesZstdMagic()
    {
        var codec = new ZstdCompressionCodec();
        var data = "Test data for Zstd magic verification"u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);

        // Zstd magic bytes: 0x28 0xB5 0x2F 0xFD
        var magic = compressedBuffer.WrittenSpan[..4].ToArray();
        await Assert.That(magic[0]).IsEqualTo((byte)0x28);
        await Assert.That(magic[1]).IsEqualTo((byte)0xB5);
        await Assert.That(magic[2]).IsEqualTo((byte)0x2F);
        await Assert.That(magic[3]).IsEqualTo((byte)0xFD);
    }

    #endregion

    #region Compression Level Tests

    [Test]
    public async Task ZstdCompressionCodec_WithHighCompression_ProducesSmallerOutput()
    {
        var fastCodec = new ZstdCompressionCodec(compressionLevel: 1);
        var highCodec = new ZstdCompressionCodec(compressionLevel: 19);

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
    public async Task ZstdCompressionCodec_RoundTrip_MultiSegmentSequence()
    {
        var codec = new ZstdCompressionCodec();

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

    #region Context Reuse Tests

    [Test]
    public async Task ZstdCompressionCodec_Compress_ReusesThreadStaticCompressorForSameLevel()
    {
        var codec = new ZstdCompressionCodec();
        var sameLevelCodec = new ZstdCompressionCodec();
        var otherLevelCodec = new ZstdCompressionCodec(compressionLevel: 5);
        var data = "zstd thread-static compressor cache"u8.ToArray();

        Compress(codec, data);
        var firstCompressor = GetRequiredStaticField("s_compressor");

        Compress(sameLevelCodec, data);
        var secondCompressor = GetRequiredStaticField("s_compressor");

        Compress(otherLevelCodec, data);
        var thirdCompressor = GetRequiredStaticField("s_compressor");

        await Assert.That(ReferenceEquals(firstCompressor, secondCompressor)).IsTrue();
        await Assert.That(ReferenceEquals(firstCompressor, thirdCompressor)).IsFalse();
    }

    [Test]
    public async Task ZstdCompressionCodec_Decompress_ReusesThreadStaticDecompressor()
    {
        var codec = new ZstdCompressionCodec();
        var data = "zstd thread-static decompressor cache"u8.ToArray();

        var compressed = Compress(codec, data);

        Decompress(codec, compressed);
        var firstDecompressor = GetRequiredStaticField("s_decompressor");

        Decompress(codec, compressed);
        var secondDecompressor = GetRequiredStaticField("s_decompressor");

        await Assert.That(ReferenceEquals(firstDecompressor, secondDecompressor)).IsTrue();
    }

    [Test]
    public async Task ZstdCompressionCodec_GrowDestinationSizeHint_DoublesUntilIntMax()
    {
        var growDestinationSizeHint = typeof(ZstdCompressionCodec).GetMethod(
            "GrowDestinationSizeHint",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(ZstdCompressionCodec), "GrowDestinationSizeHint");

        var doubled = (int)growDestinationSizeHint.Invoke(null, [64])!;
        var capped = (int)growDestinationSizeHint.Invoke(null, [int.MaxValue])!;

        await Assert.That(doubled).IsEqualTo(128);
        await Assert.That(capped).IsEqualTo(int.MaxValue);
    }

    #endregion

    #region Extension Methods Tests

    [Test]
    public async Task AddZstdExtension_RegistersCodec()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddZstd();

        await Assert.That(registry.IsSupported(CompressionType.Zstd)).IsTrue();

        var codec = registry.GetCodec(CompressionType.Zstd);
        await Assert.That(codec).IsTypeOf<ZstdCompressionCodec>();
    }

    [Test]
    public async Task AddZstdExtension_ReturnsSameRegistry_ForFluentChaining()
    {
        var registry = new CompressionCodecRegistry();
        var result = registry.AddZstd();

        await Assert.That(result).IsSameReferenceAs(registry);
    }

    [Test]
    public async Task AddZstdExtension_WithCompressionLevel_RegistersCodecWithLevel()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddZstd(compressionLevel: 10);

        await Assert.That(registry.IsSupported(CompressionType.Zstd)).IsTrue();

        // Verify it works by compressing and decompressing
        var codec = registry.GetCodec(CompressionType.Zstd);
        var data = "test"u8.ToArray();

        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(data);
    }

    #endregion

    #region Helper Classes

    private static byte[] Compress(ZstdCompressionCodec codec, byte[] data)
    {
        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);
        return compressedBuffer.WrittenSpan.ToArray();
    }

    private static byte[] Decompress(ZstdCompressionCodec codec, byte[] data)
    {
        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(data), decompressedBuffer);
        return decompressedBuffer.WrittenSpan.ToArray();
    }

    private static object GetRequiredStaticField(string name)
    {
        var field = typeof(ZstdCompressionCodec).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(ZstdCompressionCodec).FullName, name);

        return field.GetValue(null)
            ?? throw new InvalidOperationException($"{name} was not initialized.");
    }

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
