using System.Buffers;
using Dekaf.Compression;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Compression;

public class CompressionCodecRegistryTests
{
    [Test]
    public async Task Constructor_RegistersNoneByDefault()
    {
        var registry = new CompressionCodecRegistry();
        await Assert.That(registry.IsSupported(CompressionType.None)).IsTrue();
    }

    [Test]
    public async Task Constructor_RegistersGzipByDefault()
    {
        var registry = new CompressionCodecRegistry();
        await Assert.That(registry.IsSupported(CompressionType.Gzip)).IsTrue();
    }

    [Test]
    public async Task IsSupported_RegisteredType_ReturnsTrue()
    {
        var registry = new CompressionCodecRegistry();
        await Assert.That(registry.IsSupported(CompressionType.None)).IsTrue();
    }

    [Test]
    public async Task IsSupported_UnregisteredType_ReturnsFalse()
    {
        var registry = new CompressionCodecRegistry();
        await Assert.That(registry.IsSupported(CompressionType.Lz4)).IsFalse();
    }

    [Test]
    public async Task GetCodec_RegisteredType_ReturnsCodec()
    {
        var registry = new CompressionCodecRegistry();
        var codec = registry.GetCodec(CompressionType.None);
        await Assert.That(codec).IsNotNull();
        await Assert.That(codec.Type).IsEqualTo(CompressionType.None);
    }

    [Test]
    public async Task GetCodec_RegisteredGzip_ReturnsGzipCodec()
    {
        var registry = new CompressionCodecRegistry();
        var codec = registry.GetCodec(CompressionType.Gzip);
        await Assert.That(codec).IsNotNull();
        await Assert.That(codec).IsTypeOf<GzipCompressionCodec>();
    }

    [Test]
    public async Task GetCodec_UnregisteredType_ThrowsNotSupportedException()
    {
        var registry = new CompressionCodecRegistry();

        var act = () => registry.GetCodec(CompressionType.Snappy);

        await Assert.That(act).Throws<NotSupportedException>();
    }

    [Test]
    public async Task Register_CustomCodec_ThenGetCodecReturnsIt()
    {
        var registry = new CompressionCodecRegistry();
        var customCodec = new NoneCompressionCodec(); // reuse as a stand-in
        // Create a wrapper to simulate a custom type
        registry.Register(customCodec);

        var codec = registry.GetCodec(CompressionType.None);
        await Assert.That(codec).IsSameReferenceAs(customCodec);
    }

    [Test]
    public async Task Register_OverwritesExistingCodec()
    {
        var registry = new CompressionCodecRegistry();
        var originalCodec = registry.GetCodec(CompressionType.None);

        var newCodec = new NoneCompressionCodec();
        registry.Register(newCodec);

        var codec = registry.GetCodec(CompressionType.None);
        await Assert.That(codec).IsSameReferenceAs(newCodec);
        await Assert.That(codec).IsNotSameReferenceAs(originalCodec);
    }

    [Test]
    public async Task Default_IsNotNull()
    {
        await Assert.That(CompressionCodecRegistry.Default).IsNotNull();
    }

    [Test]
    public async Task Default_SupportsNone()
    {
        await Assert.That(CompressionCodecRegistry.Default.IsSupported(CompressionType.None)).IsTrue();
    }

    [Test]
    public async Task Default_SupportsGzip()
    {
        await Assert.That(CompressionCodecRegistry.Default.IsSupported(CompressionType.Gzip)).IsTrue();
    }

    [Test]
    public async Task WithCompressionLevel_CustomCodec_CreatesLocalCodecAndPreservesOtherRegistrations()
    {
        var registry = new CompressionCodecRegistry { DefaultCompressionLevel = 3 };
        var original = new ConfigurableCodec(3);
        registry.Register(original);
        var gzip = registry.GetCodec(CompressionType.Gzip);

        var configured = registry.WithCompressionLevel(CompressionType.Lz4, 9);

        await Assert.That(((ConfigurableCodec)configured.GetCodec(CompressionType.Lz4)).Level).IsEqualTo(9);
        await Assert.That(registry.GetCodec(CompressionType.Lz4)).IsSameReferenceAs(original);
        await Assert.That(original.Level).IsEqualTo(3);
        await Assert.That(configured.GetCodec(CompressionType.Gzip)).IsSameReferenceAs(gzip);
        await Assert.That(registry.DefaultCompressionLevel).IsEqualTo(3);
        await Assert.That(configured.DefaultCompressionLevel).IsEqualTo(3);
    }

    [Test]
    public async Task WithCompressionLevel_CodecWithoutLevels_DoesNotChangeRegistry()
    {
        var registry = new CompressionCodecRegistry();
        var codec = registry.GetCodec(CompressionType.None);

        var configured = registry.WithCompressionLevel(CompressionType.None, 9);

        await Assert.That(configured).IsSameReferenceAs(registry);
        await Assert.That(configured.GetCodec(CompressionType.None)).IsSameReferenceAs(codec);
        await Assert.That(configured.DefaultCompressionLevel).IsNull();
    }

    private sealed class ConfigurableCodec(int level) : ICompressionCodecWithLevel
    {
        public int Level { get; } = level;
        public CompressionType Type => CompressionType.Lz4;
        public ICompressionCodec WithCompressionLevel(int compressionLevel) => new ConfigurableCodec(compressionLevel);
        public void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination) => throw new NotSupportedException();
        public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination) => throw new NotSupportedException();
    }
}
