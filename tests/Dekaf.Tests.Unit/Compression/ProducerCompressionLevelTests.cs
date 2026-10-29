using System.Buffers;
using System.Reflection;
using System.Text;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Zstd;
using Dekaf.Producer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using K4os.Compression.LZ4.Streams;

namespace Dekaf.Tests.Unit.Compression;

public sealed class ProducerCompressionLevelTests
{
    private static readonly byte[] Data = Encoding.UTF8.GetBytes(
        string.Concat(Enumerable.Repeat("Producer compression level isolation. ", 500)));

    [Before(Class)]
    public static void LoadCompressionPackages()
    {
        // Module initializers run when their assemblies are first used.
        _ = new Lz4CompressionCodec();
        _ = new ZstdCompressionCodec();
    }

    [Test]
    [Arguments(CompressionType.Gzip, 0, 9)]
    [Arguments(CompressionType.Gzip, 9, 0)]
    [Arguments(CompressionType.Lz4, 0, 12)]
    [Arguments(CompressionType.Lz4, 12, 0)]
    [Arguments(CompressionType.Zstd, 1, 19)]
    [Arguments(CompressionType.Zstd, 19, 1)]
    public async Task Build_CompressionLevels_AreLocalToEachProducer(
        CompressionType type, int firstLevel, int secondLevel)
    {
        var globalCodec = CompressionCodecRegistry.Default.GetCodec(type);
        var globalLevel = CompressionCodecRegistry.Default.DefaultCompressionLevel;
        var defaultOutput = Compress(globalCodec);

        await using var defaultProducer = Builder(type).Build();
        await using var first = Builder(type).WithCompressionLevel(firstLevel).Build();
        var firstCodec = GetRegistry(first).GetCodec(type);
        var firstOutput = Compress(firstCodec);
        await using var second = Builder(type).WithCompressionLevel(secondLevel).Build();
        await using var laterDefault = Builder(type).Build();

        await Assert.That(Compress(firstCodec)).IsEquivalentTo(firstOutput);
        await Assert.That(firstOutput).IsEquivalentTo(Compress(CreateCodec(type, firstLevel)));
        await Assert.That(GetLevel(firstCodec)).IsEqualTo(GetLevel(CreateCodec(type, firstLevel)));
        await Assert.That(GetLevel(GetRegistry(second).GetCodec(type)))
            .IsEqualTo(GetLevel(CreateCodec(type, secondLevel)));
        await Assert.That(Compress(GetRegistry(second).GetCodec(type)))
            .IsEquivalentTo(Compress(CreateCodec(type, secondLevel)));
        await Assert.That(GetRegistry(first)).IsNotSameReferenceAs(GetRegistry(second));
        await Assert.That(GetRegistry(defaultProducer).GetCodec(type)).IsSameReferenceAs(globalCodec);
        await Assert.That(Compress(GetRegistry(laterDefault).GetCodec(type))).IsEquivalentTo(defaultOutput);
        await Assert.That(CompressionCodecRegistry.Default.GetCodec(type)).IsSameReferenceAs(globalCodec);
        await Assert.That(CompressionCodecRegistry.Default.DefaultCompressionLevel).IsEqualTo(globalLevel);

        // The producer registry must retain codecs supplied by optional packages.
        await Assert.That(GetRegistry(first).IsSupported(CompressionType.Lz4)).IsTrue();
        await Assert.That(GetRegistry(first).IsSupported(CompressionType.Zstd)).IsTrue();
    }

    [Test]
    [Arguments(CompressionType.Gzip, -1)]
    [Arguments(CompressionType.Gzip, 10)]
    [Arguments(CompressionType.Lz4, -1)]
    [Arguments(CompressionType.Lz4, 13)]
    [Arguments(CompressionType.Zstd, 0)]
    [Arguments(CompressionType.Zstd, 23)]
    public async Task Build_InvalidLevel_ThrowsWithoutChangingGlobalRegistry(CompressionType type, int level)
    {
        var globalCodec = CompressionCodecRegistry.Default.GetCodec(type);
        var globalLevel = CompressionCodecRegistry.Default.DefaultCompressionLevel;

        await Assert.That(() => Builder(type).WithCompressionLevel(level).Build())
            .Throws<ArgumentOutOfRangeException>();

        await Assert.That(CompressionCodecRegistry.Default.GetCodec(type)).IsSameReferenceAs(globalCodec);
        await Assert.That(CompressionCodecRegistry.Default.DefaultCompressionLevel).IsEqualTo(globalLevel);
    }

    [Test]
    public async Task Build_LevelBeforeCompressionType_UsesFinalCompressionType()
    {
        await using var producer = Builder(CompressionType.Gzip)
            .WithCompressionLevel(12)
            .UseLz4Compression()
            .Build();

        await Assert.That(Compress(GetRegistry(producer).GetCodec(CompressionType.Lz4)))
            .IsEquivalentTo(Compress(CreateCodec(CompressionType.Lz4, 12)));
    }

    [Test]
    [Arguments(CompressionType.Gzip, 10)]
    [Arguments(CompressionType.Lz4, 13)]
    [Arguments(CompressionType.Zstd, 23)]
    public async Task Constructor_InvalidLevel_Throws(CompressionType type, int level)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            CompressionType = type,
            CompressionLevel = level
        };

        await Assert.That(() => new KafkaProducer<string, string>(options, Serializers.String, Serializers.String))
            .Throws<ArgumentOutOfRangeException>();
    }

    private static ProducerBuilder<string, string> Builder(CompressionType type) =>
        Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithBufferMemory(1024 * 1024)
            .WithValueTaskSourcePoolSize(8)
            .UseCompression(type);

    private static CompressionCodecRegistry GetRegistry(IKafkaProducer<string, string> producer) =>
        (CompressionCodecRegistry)typeof(KafkaProducer<string, string>)
            .GetField("_compressionCodecs", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(producer)!;

    private static ICompressionCodec CreateCodec(CompressionType type, int level)
    {
        switch (type)
        {
            case CompressionType.Gzip:
                return new GzipCompressionCodec(level);
            case CompressionType.Lz4:
                var registry = new CompressionCodecRegistry();
                registry.AddLz4((int?)level);
                return registry.GetCodec(type);
            case CompressionType.Zstd:
                return new ZstdCompressionCodec(level);
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private static int GetLevel(ICompressionCodec codec)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        if (codec is Lz4CompressionCodec)
        {
            var settings = (LZ4EncoderSettings)codec.GetType().GetField("_encoderSettings", flags)!.GetValue(codec)!;
            return (int)settings.CompressionLevel;
        }

        return Convert.ToInt32(codec.GetType().GetField("_compressionLevel", flags)!.GetValue(codec));
    }

    private static byte[] Compress(ICompressionCodec codec)
    {
        var writer = new ArrayBufferWriter<byte>();
        codec.Compress(new ReadOnlySequence<byte>(Data), writer);
        return writer.WrittenSpan.ToArray();
    }
}
