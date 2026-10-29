using System.Buffers;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;
using Dekaf.Protocol;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Protocol;

public sealed class PreCompressionBufferTests
{
    [Test]
    [Arguments(CompressionType.Zstd, false)]
    [Arguments(CompressionType.Zstd, true)]
    [Arguments(CompressionType.Lz4, false)]
    [Arguments(CompressionType.Lz4, true)]
    [Arguments(CompressionType.Snappy, false)]
    [Arguments(CompressionType.Snappy, true)]
    public async Task PreCompress_LargeCompressibleBatch_RetainsPoolSizedOutput(CompressionType compression, bool preEncoded)
    {
        var registry = CreateRegistry();
        var value = new byte[5 * 1024 * 1024];
        using var batch = new RecordBatch { Records = [new Record { Value = value }] };
        if (preEncoded)
        {
            var encoded = new ArrayBufferWriter<byte>();
            var writer = new KafkaProtocolWriter(encoded);
            batch.Records[0].Write(ref writer);
            batch.SetPreEncodedRecords(encoded.WrittenMemory);
        }

        batch.PreCompress(compression, registry, estimatedCompressionRatio: 0.05);

        await Assert.That(batch.PreCompressedRecords!.Length).IsLessThanOrEqualTo(2 * 1024 * 1024);
        await Assert.That(batch.PreCompressedLength).IsGreaterThan(0);
        var wire = new ArrayBufferWriter<byte>();
        batch.Write(wire, compression, registry);
        var reader = new KafkaProtocolReader(wire.WrittenMemory);
        using var decoded = RecordBatch.Read(ref reader, registry, wire.WrittenCount, checkCrcs: true);
        await Assert.That(decoded.Records[0].Value.Span.SequenceEqual(value)).IsTrue();
    }

    [Test]
    [Arguments(CompressionType.Zstd)]
    [Arguments(CompressionType.Lz4)]
    [Arguments(CompressionType.Snappy)]
    public async Task PreCompress_StaleEstimate_GrowsWithoutLosingData(CompressionType compression)
    {
        var registry = CreateRegistry();
        var value = new byte[1024 * 1024];
        new Random(3827).NextBytes(value);
        using var batch = new RecordBatch();
        batch.SetPreEncodedRecords(value);

        batch.PreCompress(compression, registry, estimatedCompressionRatio: 0.01);

        var decoded = new ArrayBufferWriter<byte>();
        registry.GetCodec(compression).Decompress(
            new ReadOnlySequence<byte>(batch.PreCompressedRecords!.AsMemory(0, batch.PreCompressedLength)), decoded);
        await Assert.That(decoded.WrittenSpan.SequenceEqual(value)).IsTrue();
    }

    private static CompressionCodecRegistry CreateRegistry()
    {
        var registry = new CompressionCodecRegistry();
        registry.AddZstd().AddLz4().AddSnappy();
        return registry;
    }
}
