using System.Buffers;
using Dekaf.Compression;
using Dekaf.Protocol.Records;
using ZstdSharp;

namespace Dekaf.Compression.Zstd;

/// <summary>
/// Zstd compression codec using zero-allocation streaming APIs.
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

        var position = source.Start;
        var remaining = source.Length;

        while (source.TryGet(ref position, out var segment))
        {
            remaining -= segment.Length;
            var isFinalBlock = remaining == 0;
            var sourceSpan = segment.Span;

            // Process this segment
            while (sourceSpan.Length > 0)
            {
                var maxOutputSize = Compressor.GetCompressBound(sourceSpan.Length);
                var destSpan = destination.GetSpan(maxOutputSize);

                var status = compressor.WrapStream(sourceSpan, destSpan, out var bytesConsumed, out var bytesWritten, isFinalBlock: false);
                destination.Advance(bytesWritten);
                sourceSpan = sourceSpan[bytesConsumed..];

                if (status == OperationStatus.InvalidData)
                    throw new InvalidOperationException("Zstd compression failed: invalid data");

                if (status == OperationStatus.DestinationTooSmall && bytesConsumed == 0)
                {
                    // Need larger buffer
                    continue;
                }
            }

            // On final segment, finalize the frame
            if (isFinalBlock)
            {
                while (true)
                {
                    var destSpan = destination.GetSpan(64);
                    var status = compressor.WrapStream(ReadOnlySpan<byte>.Empty, destSpan, out _, out var bytesWritten, isFinalBlock: true);
                    destination.Advance(bytesWritten);

                    if (status == OperationStatus.Done)
                        return;

                    if (status == OperationStatus.InvalidData)
                        throw new InvalidOperationException("Zstd compression failed: invalid data");
                }
            }
        }
    }

    public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        using var decompressor = new Decompressor();

        var position = source.Start;

        while (source.TryGet(ref position, out var segment))
        {
            var sourceSpan = segment.Span;

            while (sourceSpan.Length > 0)
            {
                var destSpan = destination.GetSpan(4096);

                var status = decompressor.UnwrapStream(sourceSpan, destSpan, out var bytesConsumed, out var bytesWritten);
                destination.Advance(bytesWritten);
                sourceSpan = sourceSpan[bytesConsumed..];

                if (status == OperationStatus.Done)
                    return;

                if (status == OperationStatus.InvalidData)
                    throw new InvalidOperationException("Zstd decompression failed: invalid data");

                // NeedMoreData or DestinationTooSmall: continue with next iteration
            }
        }
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
