using System.Buffers;
using System.Buffers.Binary;
using Dekaf.Compression;
using Dekaf.Protocol.Records;
using Snappier;

namespace Dekaf.Compression.Snappy;

/// <summary>
/// Snappy compression codec with xerial-snappy framing.
/// Kafka uses xerial-snappy format which wraps raw snappy blocks with:
/// - Magic header: 0x82 0x53 0x4e 0x41 0x50 0x50 0x59 0x00
/// - Multiple blocks, each with: [compressed size (4 bytes BE)][uncompressed size (4 bytes BE)][snappy data]
/// </summary>
public sealed class SnappyCompressionCodec : ICompressionCodec
{
    // Xerial-snappy magic header
    private static ReadOnlySpan<byte> XerialMagic => [0x82, 0x53, 0x4e, 0x41, 0x50, 0x50, 0x59, 0x00];

    // Default block size for compression (64KB)
    private const int DefaultBlockSize = 65536;

    private readonly int _blockSize;

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
        // Write xerial magic header
        var headerSpan = destination.GetSpan(XerialMagic.Length);
        XerialMagic.CopyTo(headerSpan);
        destination.Advance(XerialMagic.Length);

        // Process in blocks
        var position = source.Start;
        var remaining = source.Length;

        // Temporary buffer to capture compressed output (needed to get size for header)
        var compressedBuffer = new ArrayBufferWriter<byte>(Snappier.Snappy.GetMaxCompressedLength(_blockSize));

        while (remaining > 0)
        {
            var blockLength = (int)Math.Min(remaining, _blockSize);
            var blockSequence = source.Slice(position, blockLength);

            // Compress the block using ReadOnlySequence/IBufferWriter overload
            compressedBuffer.Clear();
            Snappier.Snappy.Compress(blockSequence, compressedBuffer);
            var compressedLength = compressedBuffer.WrittenCount;

            // Write block header: [compressed size (4 bytes BE)][uncompressed size (4 bytes BE)]
            var blockHeaderSpan = destination.GetSpan(8);
            BinaryPrimitives.WriteInt32BigEndian(blockHeaderSpan, compressedLength);
            BinaryPrimitives.WriteInt32BigEndian(blockHeaderSpan.Slice(4), blockLength);
            destination.Advance(8);

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
        if (source.Length < XerialMagic.Length)
            throw new InvalidDataException("Snappy data too short for xerial header.");

        // Verify and skip xerial magic header
        Span<byte> headerBuffer = stackalloc byte[XerialMagic.Length];
        source.Slice(0, XerialMagic.Length).CopyTo(headerBuffer);

        if (!headerBuffer.SequenceEqual(XerialMagic))
            throw new InvalidDataException("Invalid xerial-snappy magic header.");

        var position = source.GetPosition(XerialMagic.Length);
        var remaining = source.Length - XerialMagic.Length;

        // Process blocks
        Span<byte> blockHeader = stackalloc byte[8];
        byte[]? compressedBuffer = null;

        try
        {
            while (remaining >= 8)
            {
                // Read block header
                source.Slice(position, 8).CopyTo(blockHeader);
                var compressedSize = BinaryPrimitives.ReadInt32BigEndian(blockHeader);
                var uncompressedSize = BinaryPrimitives.ReadInt32BigEndian(blockHeader.Slice(4));

                position = source.GetPosition(8, position);
                remaining -= 8;

                if (compressedSize < 0 || uncompressedSize < 0)
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

                // Decompress directly into destination and verify size
                var decompressedSpan = destination.GetSpan(uncompressedSize);
                var actualDecompressedLength = Snappier.Snappy.Decompress(compressedSpan, decompressedSpan);

                if (actualDecompressedLength != uncompressedSize)
                    throw new InvalidDataException(
                        $"Decompressed size mismatch. Expected {uncompressedSize}, got {actualDecompressedLength}.");

                destination.Advance(uncompressedSize);

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

/// <summary>
/// Extension methods for registering Snappy compression.
/// </summary>
public static class SnappyCompressionExtensions
{
    /// <summary>
    /// Registers the Snappy compression codec.
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
