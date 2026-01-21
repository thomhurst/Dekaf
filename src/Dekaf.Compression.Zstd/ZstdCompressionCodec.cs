using System.Buffers;
using Dekaf.Compression;
using Dekaf.Protocol.Records;
using ZstdSharp;

namespace Dekaf.Compression.Zstd;

/// <summary>
/// Zstd compression codec.
/// </summary>
public sealed class ZstdCompressionCodec : ICompressionCodec
{
    private readonly int _compressionLevel;

    public ZstdCompressionCodec(int compressionLevel = 3)
    {
        _compressionLevel = compressionLevel;
    }

    public CompressionType Type => CompressionType.Zstd;

    public void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        using var compressor = new Compressor(_compressionLevel);

        var sourceArray = source.ToArray();
        var maxCompressedSize = Compressor.GetCompressBound(sourceArray.Length);
        var compressed = new byte[maxCompressedSize];

        var compressedSize = compressor.Wrap(sourceArray, compressed);

        var span = destination.GetSpan(compressedSize);
        compressed.AsSpan(0, compressedSize).CopyTo(span);
        destination.Advance(compressedSize);
    }

    public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        using var decompressor = new Decompressor();

        var sourceArray = source.ToArray();
        var decompressedArray = decompressor.Unwrap(sourceArray).ToArray();

        var span = destination.GetSpan(decompressedArray.Length);
        decompressedArray.CopyTo(span);
        destination.Advance(decompressedArray.Length);
    }
}

/// <summary>
/// Extension methods for registering Zstd compression.
/// </summary>
public static class ZstdCompressionExtensions
{
    /// <summary>
    /// Registers the Zstd compression codec.
    /// </summary>
    public static CompressionCodecRegistry AddZstd(this CompressionCodecRegistry registry, int compressionLevel = 3)
    {
        registry.Register(new ZstdCompressionCodec(compressionLevel));
        return registry;
    }
}
