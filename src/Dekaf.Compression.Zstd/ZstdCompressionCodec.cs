using System.Buffers;
using Dekaf;
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

    /// <summary>
    /// Creates a new Zstd compression codec with the specified compression level.
    /// </summary>
    /// <param name="compressionLevel">The compression level (1-22). Default is 3.</param>
    public ZstdCompressionCodec(int compressionLevel = 3)
    {
        if (compressionLevel < 1 || compressionLevel > 22)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compressionLevel),
                $"Zstd compression level must be between 1 and 22, but was {compressionLevel}.");
        }

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
/// Extension methods for configuring Zstd compression on the producer builder.
/// </summary>
public static class ZstdProducerBuilderExtensions
{
    /// <summary>
    /// Configures the producer to use Zstd compression.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseZstdCompression<TKey, TValue>(this ProducerBuilder<TKey, TValue> builder)
    {
        return builder.UseCompression(CompressionType.Zstd);
    }
}

/// <summary>
/// Extension methods for registering Zstd compression.
/// </summary>
public static class ZstdCompressionExtensions
{
    /// <summary>
    /// Registers the Zstd compression codec with the specified compression level.
    /// </summary>
    /// <param name="registry">The compression codec registry.</param>
    /// <param name="compressionLevel">The compression level to use (1-22). Default is 3.</param>
    /// <returns>The registry for fluent chaining.</returns>
    public static CompressionCodecRegistry AddZstd(this CompressionCodecRegistry registry, int compressionLevel = 3)
    {
        registry.Register(new ZstdCompressionCodec(compressionLevel));
        return registry;
    }

    /// <summary>
    /// Registers the Zstd compression codec, using the registry's default compression level if available.
    /// If no explicit level is provided, falls back to <see cref="CompressionCodecRegistry.DefaultCompressionLevel"/>,
    /// then to Zstd's default (3).
    /// </summary>
    /// <param name="registry">The compression codec registry.</param>
    /// <param name="compressionLevel">The compression level (1-22). Null uses the registry default or codec default (3).</param>
    /// <returns>The registry for fluent chaining.</returns>
    public static CompressionCodecRegistry AddZstdWithLevel(this CompressionCodecRegistry registry, int? compressionLevel = null)
    {
        var level = compressionLevel ?? registry.DefaultCompressionLevel;

        if (level.HasValue)
        {
            if (level.Value < 1 || level.Value > 22)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(compressionLevel),
                    $"Zstd compression level must be between 1 and 22, but was {level.Value}.");
            }

            registry.Register(new ZstdCompressionCodec(level.Value));
        }
        else
        {
            registry.Register(new ZstdCompressionCodec());
        }

        return registry;
    }
}
