using System.Buffers;
using System.IO.Compression;
using System.Text;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Zstd;
using Dekaf.Protocol.Records;
using K4os.Compression.LZ4;

namespace Dekaf.Tests.Unit.Compression;

/// <summary>
/// Tests for per-codec compression level configuration.
/// </summary>
public sealed class CompressionLevelTests
{
    private static readonly byte[] TestData =
        Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Kafka compression level test message. ", 500)));

    #region Gzip Compression Level Tests

    [Test]
    public async Task GzipCompressionCodec_IntLevel0_RoundTripPreservesData()
    {
        var codec = new GzipCompressionCodec(compressionLevel: 0);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task GzipCompressionCodec_IntLevel5_RoundTripPreservesData()
    {
        var codec = new GzipCompressionCodec(compressionLevel: 5);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task GzipCompressionCodec_IntLevel9_RoundTripPreservesData()
    {
        var codec = new GzipCompressionCodec(compressionLevel: 9);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task GzipCompressionCodec_IntLevel9_ProducesSmallerOutputThanLevel0()
    {
        var codec0 = new GzipCompressionCodec(compressionLevel: 0);
        var codec9 = new GzipCompressionCodec(compressionLevel: 9);

        var compressed0 = Compress(codec0, TestData);
        var compressed9 = Compress(codec9, TestData);

        await Assert.That(compressed9.Length).IsLessThan(compressed0.Length);
    }

    [Test]
    public async Task GzipCompressionCodec_IntLevelNegative_ThrowsArgumentOutOfRange()
    {
        var act = () => new GzipCompressionCodec(compressionLevel: -1);
        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task GzipCompressionCodec_IntLevel10_ThrowsArgumentOutOfRange()
    {
        var act = () => new GzipCompressionCodec(compressionLevel: 10);
        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task GzipCompressionCodec_DefaultConstructor_UsesDefaultLevel()
    {
        var codec = new GzipCompressionCodec();
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task GzipCompressionCodec_EnumCompressionLevel_StillWorks()
    {
        var codec = new GzipCompressionCodec(CompressionLevel.SmallestSize);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    #endregion

    #region Zstd Compression Level Tests

    [Test]
    public async Task ZstdCompressionCodec_Level1_RoundTripPreservesData()
    {
        var codec = new ZstdCompressionCodec(compressionLevel: 1);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task ZstdCompressionCodec_Level10_RoundTripPreservesData()
    {
        var codec = new ZstdCompressionCodec(compressionLevel: 10);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task ZstdCompressionCodec_Level22_RoundTripPreservesData()
    {
        var codec = new ZstdCompressionCodec(compressionLevel: 22);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task ZstdCompressionCodec_HighLevel_ProducesSmallerOutput()
    {
        var codecLow = new ZstdCompressionCodec(compressionLevel: 1);
        var codecHigh = new ZstdCompressionCodec(compressionLevel: 19);

        var compressedLow = Compress(codecLow, TestData);
        var compressedHigh = Compress(codecHigh, TestData);

        await Assert.That(compressedHigh.Length).IsLessThanOrEqualTo(compressedLow.Length);
    }

    [Test]
    public async Task ZstdCompressionCodec_Level0_ThrowsArgumentOutOfRange()
    {
        var act = () => new ZstdCompressionCodec(compressionLevel: 0);
        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ZstdCompressionCodec_Level23_ThrowsArgumentOutOfRange()
    {
        var act = () => new ZstdCompressionCodec(compressionLevel: 23);
        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ZstdCompressionCodec_DefaultConstructor_UsesLevel3()
    {
        var codec = new ZstdCompressionCodec();
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    #endregion

    #region LZ4 Compression Level Tests

    [Test]
    public async Task Lz4CompressionCodec_LevelFast_RoundTripPreservesData()
    {
        var codec = new Lz4CompressionCodec(LZ4Level.L00_FAST);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task Lz4CompressionCodec_LevelMax_RoundTripPreservesData()
    {
        var codec = new Lz4CompressionCodec(LZ4Level.L12_MAX);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task Lz4CompressionCodec_HighLevel_ProducesSmallerOutput()
    {
        var codecFast = new Lz4CompressionCodec(LZ4Level.L00_FAST);
        var codecMax = new Lz4CompressionCodec(LZ4Level.L12_MAX);

        var compressedFast = Compress(codecFast, TestData);
        var compressedMax = Compress(codecMax, TestData);

        await Assert.That(compressedMax.Length).IsLessThanOrEqualTo(compressedFast.Length);
    }

    [Test]
    public async Task Lz4CompressionCodec_DefaultConstructor_UsesDefaultLevel()
    {
        var codec = new Lz4CompressionCodec();
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    #endregion

    #region Registry Extension Method Tests

    [Test]
    public async Task AddLz4_WithIntLevel0_RegistersWorkingCodec()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddLz4(compressionLevel: (int?)0);

        var codec = registry.GetCodec(CompressionType.Lz4);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task AddLz4_WithIntLevel12_RegistersWorkingCodec()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddLz4(compressionLevel: (int?)12);

        var codec = registry.GetCodec(CompressionType.Lz4);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task AddLz4_WithIntLevel13_ThrowsArgumentOutOfRange()
    {
        var registry = new CompressionCodecRegistry();
        var act = () => registry.AddLz4(compressionLevel: (int?)13);
        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AddLz4_WithIntLevelNegative_ThrowsArgumentOutOfRange()
    {
        var registry = new CompressionCodecRegistry();
        var act = () => registry.AddLz4(compressionLevel: (int?)-1);
        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AddLz4_WithNullLevel_UsesRegistryDefault()
    {
        var registry = new CompressionCodecRegistry();
        registry.DefaultCompressionLevel = 5;
        registry.AddLz4(compressionLevel: null);

        var codec = registry.GetCodec(CompressionType.Lz4);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task AddLz4_WithNullLevelAndNoDefault_UsesCodecDefault()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddLz4(compressionLevel: null);

        var codec = registry.GetCodec(CompressionType.Lz4);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task AddZstdWithLevel_WithLevel10_RegistersWorkingCodec()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddZstdWithLevel(compressionLevel: 10);

        var codec = registry.GetCodec(CompressionType.Zstd);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task AddZstdWithLevel_WithLevel0_ThrowsArgumentOutOfRange()
    {
        var registry = new CompressionCodecRegistry();
        var act = () => registry.AddZstdWithLevel(compressionLevel: 0);
        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AddZstdWithLevel_WithLevel23_ThrowsArgumentOutOfRange()
    {
        var registry = new CompressionCodecRegistry();
        var act = () => registry.AddZstdWithLevel(compressionLevel: 23);
        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AddZstdWithLevel_WithNullLevel_UsesRegistryDefault()
    {
        var registry = new CompressionCodecRegistry();
        registry.DefaultCompressionLevel = 5;
        registry.AddZstdWithLevel(compressionLevel: null);

        var codec = registry.GetCodec(CompressionType.Zstd);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task AddZstdWithLevel_WithNullLevelAndNoDefault_UsesCodecDefault()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddZstdWithLevel();

        var codec = registry.GetCodec(CompressionType.Zstd);
        var result = CompressAndDecompress(codec);
        await Assert.That(result).IsEquivalentTo(TestData);
    }

    #endregion

    #region DefaultCompressionLevel Property Tests

    [Test]
    public async Task Registry_DefaultCompressionLevel_IsNullByDefault()
    {
        var registry = new CompressionCodecRegistry();
        await Assert.That(registry.DefaultCompressionLevel).IsNull();
    }

    [Test]
    public async Task Registry_DefaultCompressionLevel_CanBeSet()
    {
        var registry = new CompressionCodecRegistry();
        registry.DefaultCompressionLevel = 5;
        await Assert.That(registry.DefaultCompressionLevel).IsEqualTo(5);
    }

    [Test]
    public async Task Registry_DefaultCompressionLevel_CanBeCleared()
    {
        var registry = new CompressionCodecRegistry();
        registry.DefaultCompressionLevel = 5;
        registry.DefaultCompressionLevel = null;
        await Assert.That(registry.DefaultCompressionLevel).IsNull();
    }

    #endregion

    #region ProducerOptions Compression Level Tests

    [Test]
    public async Task ProducerOptions_CompressionLevel_IsNullByDefault()
    {
        var options = new Dekaf.Producer.ProducerOptions
        {
            BootstrapServers = ["localhost:9092"]
        };
        await Assert.That(options.CompressionLevel).IsNull();
    }

    [Test]
    public async Task ProducerOptions_CompressionLevel_CanBeSet()
    {
        var options = new Dekaf.Producer.ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            CompressionLevel = 5
        };
        await Assert.That(options.CompressionLevel).IsEqualTo(5);
    }

    #endregion

    #region Cross-Codec Decompression Tests

    [Test]
    public async Task GzipCompressionCodec_CompressWithLevel3_DecompressWithDefaultCodec()
    {
        var compressor = new GzipCompressionCodec(compressionLevel: 3);
        var decompressor = new GzipCompressionCodec(); // default level

        var compressedBuffer = new ArrayBufferWriter<byte>();
        compressor.Compress(new ReadOnlySequence<byte>(TestData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        decompressor.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task ZstdCompressionCodec_CompressWithLevel15_DecompressWithDefaultCodec()
    {
        var compressor = new ZstdCompressionCodec(compressionLevel: 15);
        var decompressor = new ZstdCompressionCodec(); // default level (3)

        var compressedBuffer = new ArrayBufferWriter<byte>();
        compressor.Compress(new ReadOnlySequence<byte>(TestData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        decompressor.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(TestData);
    }

    [Test]
    public async Task Lz4CompressionCodec_CompressWithLevelMax_DecompressWithDefaultCodec()
    {
        var compressor = new Lz4CompressionCodec(LZ4Level.L12_MAX);
        var decompressor = new Lz4CompressionCodec(); // default level (L00_FAST)

        var compressedBuffer = new ArrayBufferWriter<byte>();
        compressor.Compress(new ReadOnlySequence<byte>(TestData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        decompressor.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        await Assert.That(decompressedBuffer.WrittenSpan.ToArray()).IsEquivalentTo(TestData);
    }

    #endregion

    #region Helper Methods

    private static byte[] CompressAndDecompress(ICompressionCodec codec)
    {
        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(TestData), compressedBuffer);

        var decompressedBuffer = new ArrayBufferWriter<byte>();
        codec.Decompress(new ReadOnlySequence<byte>(compressedBuffer.WrittenMemory), decompressedBuffer);

        return decompressedBuffer.WrittenSpan.ToArray();
    }

    private static byte[] Compress(ICompressionCodec codec, byte[] data)
    {
        var compressedBuffer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(data), compressedBuffer);
        return compressedBuffer.WrittenSpan.ToArray();
    }

    #endregion
}
