using System.Buffers;
using Dekaf.Protocol.Records;

namespace Dekaf.Compression;

/// <summary>
/// Interface for compression codecs.
/// </summary>
public interface ICompressionCodec
{
    /// <summary>
    /// The compression type.
    /// </summary>
    CompressionType Type { get; }

    /// <summary>
    /// Compresses data.
    /// </summary>
    void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination);

    /// <summary>
    /// Decompresses data.
    /// </summary>
    void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination);
}

/// <summary>
/// Registry for compression codecs.
/// </summary>
public sealed class CompressionCodecRegistry
{
    private readonly Dictionary<CompressionType, ICompressionCodec> _codecs = [];

    /// <summary>
    /// Default global registry with built-in codecs.
    /// Used by RecordBatch for compression/decompression when no specific registry is provided.
    /// </summary>
    public static CompressionCodecRegistry Default { get; } = new();

    /// <summary>
    /// Creates a registry with built-in codecs.
    /// </summary>
    public CompressionCodecRegistry()
    {
        Register(new NoneCompressionCodec());
        Register(new GzipCompressionCodec());
    }

    /// <summary>
    /// Registers a codec.
    /// </summary>
    public void Register(ICompressionCodec codec)
    {
        _codecs[codec.Type] = codec;
    }

    /// <summary>
    /// Gets a codec for the specified compression type.
    /// </summary>
    public ICompressionCodec GetCodec(CompressionType type)
    {
        if (_codecs.TryGetValue(type, out var codec))
        {
            return codec;
        }

        throw new NotSupportedException($"Compression type {type} is not supported. Please register the appropriate codec.");
    }

    /// <summary>
    /// Checks if a codec is registered.
    /// </summary>
    public bool IsSupported(CompressionType type) => _codecs.ContainsKey(type);
}
