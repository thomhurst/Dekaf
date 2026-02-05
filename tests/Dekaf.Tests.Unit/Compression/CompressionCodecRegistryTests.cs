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
}
