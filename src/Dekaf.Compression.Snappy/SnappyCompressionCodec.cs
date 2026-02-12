using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Dekaf.Protocol.Records;

namespace Dekaf.Compression.Snappy;

/// <summary>
/// Snappy compression codec with xerial-snappy framing.
/// Kafka uses xerial-snappy format which wraps raw snappy blocks with:
/// - Magic header (8 bytes): 0x82 SNAPPY 0x00
/// - Version (4 bytes BE): 1
/// - Min compatible version (4 bytes BE): 1
/// - Multiple blocks, each with: [compressed size (4 bytes BE)][snappy data]
/// </summary>
public sealed class SnappyCompressionCodec : ICompressionCodec
{
    // Xerial-snappy magic header
    private static ReadOnlySpan<byte> XerialMagic => [0x82, 0x53, 0x4e, 0x41, 0x50, 0x50, 0x59, 0x00];

    // Total header size: magic (8) + version (4) + compat version (4) = 16 bytes
    private const int HeaderSize = 16;

    // Default block size for compression (64KB)
    private const int DefaultBlockSize = 65536;

    private readonly int _blockSize;

    // Thread-local reusable buffer for compression output (avoids ~77KB allocation per Compress call)
    [ThreadStatic]
    private static ArrayBufferWriter<byte>? t_compressedBuffer;

    /// <summary>
    /// Creates a new Snappy compression codec.
    /// </summary>
    /// <param name="blockSize">The block size for compression. Defaults to 64KB.</param>
    public SnappyCompressionCodec(int blockSize = DefaultBlockSize)
    {
        if (blockSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");

        _blockSize = blockSize;
    }

    /// <inheritdoc />
    public CompressionType Type => CompressionType.Snappy;

    /// <inheritdoc />
    public void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        // Write xerial header: magic (8 bytes) + version (4 bytes) + compat version (4 bytes)
        var headerSpan = destination.GetSpan(HeaderSize);
        XerialMagic.CopyTo(headerSpan);
        BinaryPrimitives.WriteInt32BigEndian(headerSpan.Slice(XerialMagic.Length), 1); // version
        BinaryPrimitives.WriteInt32BigEndian(headerSpan.Slice(XerialMagic.Length + 4), 1); // min compat version
        destination.Advance(HeaderSize);

        // Process in blocks
        var position = source.Start;
        var remaining = source.Length;

        // Reuse thread-local buffer to capture compressed output (needed to get size for header)
        var compressedBuffer = t_compressedBuffer ??= new ArrayBufferWriter<byte>();

        while (remaining > 0)
        {
            var blockLength = (int)Math.Min(remaining, _blockSize);
            var blockSequence = source.Slice(position, blockLength);

            // Compress the block using ReadOnlySequence/IBufferWriter overload
            compressedBuffer.ResetWrittenCount();
            Snappier.Snappy.Compress(blockSequence, compressedBuffer);
            var compressedLength = compressedBuffer.WrittenCount;

            // Write block header: [compressed size (4 bytes BE)]
            var blockHeaderSpan = destination.GetSpan(4);
            BinaryPrimitives.WriteInt32BigEndian(blockHeaderSpan, compressedLength);
            destination.Advance(4);

            // Write compressed data
            var compressedSpan = destination.GetSpan(compressedLength);
            compressedBuffer.WrittenSpan.CopyTo(compressedSpan);
            destination.Advance(compressedLength);

            position = source.GetPosition(blockLength, position);
            remaining -= blockLength;
        }
    }

    /// <inheritdoc />
    public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        if (source.Length < HeaderSize)
            throw new InvalidDataException("Snappy data too short for xerial header.");

        // Verify and skip xerial header (magic + version + compat version)
        Span<byte> headerBuffer = stackalloc byte[HeaderSize];
        source.Slice(0, HeaderSize).CopyTo(headerBuffer);

        if (!headerBuffer.Slice(0, XerialMagic.Length).SequenceEqual(XerialMagic))
            throw new InvalidDataException("Invalid xerial-snappy magic header.");

        var position = source.GetPosition(HeaderSize);
        var remaining = source.Length - HeaderSize;

        // Process blocks
        Span<byte> blockHeader = stackalloc byte[4];
        byte[]? compressedBuffer = null;

        try
        {
            while (remaining >= 4)
            {
                // Read block header: [compressed size (4 bytes BE)]
                source.Slice(position, 4).CopyTo(blockHeader);
                var compressedSize = BinaryPrimitives.ReadInt32BigEndian(blockHeader);

                position = source.GetPosition(4, position);
                remaining -= 4;

                if (compressedSize < 0)
                    throw new InvalidDataException("Invalid block size in xerial-snappy data.");

                if (remaining < compressedSize)
                    throw new InvalidDataException("Truncated xerial-snappy block.");

                var compressedSequence = source.Slice(position, compressedSize);

                // Get contiguous span for compressed data
                ReadOnlySpan<byte> compressedSpan;
                if (compressedSequence.IsSingleSegment)
                {
                    compressedSpan = compressedSequence.FirstSpan;
                }
                else
                {
                    if (compressedBuffer == null || compressedBuffer.Length < compressedSize)
                    {
                        if (compressedBuffer != null)
                            ArrayPool<byte>.Shared.Return(compressedBuffer);
                        compressedBuffer = ArrayPool<byte>.Shared.Rent(compressedSize);
                    }
                    compressedSequence.CopyTo(compressedBuffer);
                    compressedSpan = compressedBuffer.AsSpan(0, compressedSize);
                }

                // Get uncompressed length from snappy frame, then decompress
                var uncompressedSize = Snappier.Snappy.GetUncompressedLength(compressedSpan);
                var decompressedSpan = destination.GetSpan(uncompressedSize);
                var actualDecompressedLength = Snappier.Snappy.Decompress(compressedSpan, decompressedSpan);

                destination.Advance(actualDecompressedLength);

                position = source.GetPosition(compressedSize, position);
                remaining -= compressedSize;
            }

            if (remaining != 0)
                throw new InvalidDataException("Trailing data after last xerial-snappy block.");
        }
        finally
        {
            if (compressedBuffer != null)
                ArrayPool<byte>.Shared.Return(compressedBuffer);
        }
    }

}

internal static class SnappyModuleInit
{
    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries")]
    internal static void Register()
    {
        CompressionCodecRegistry.Default.AddSnappy();
    }
}

/// <summary>
/// Extension methods for configuring Snappy compression on the producer builder.
/// </summary>
public static class SnappyProducerBuilderExtensions
{
    /// <summary>
    /// Configures the producer to use Snappy compression.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseSnappyCompression<TKey, TValue>(this ProducerBuilder<TKey, TValue> builder)
    {
        return builder.UseCompression(CompressionType.Snappy);
    }
}

/// <summary>
/// Extension methods for registering Snappy compression.
/// </summary>
public static class SnappyCompressionExtensions
{
    /// <summary>
    /// Registers the Snappy compression codec.
    /// Snappy is a fixed-algorithm codec and does not support compression levels.
    /// The <see cref="CompressionCodecRegistry.DefaultCompressionLevel"/> property is ignored for Snappy.
    /// </summary>
    /// <param name="registry">The compression codec registry.</param>
    /// <param name="blockSize">The block size for compression. Defaults to 64KB.</param>
    /// <returns>The registry for fluent chaining.</returns>
    public static CompressionCodecRegistry AddSnappy(this CompressionCodecRegistry registry, int blockSize = 65536)
    {
        registry.Register(new SnappyCompressionCodec(blockSize));
        return registry;
    }
}
