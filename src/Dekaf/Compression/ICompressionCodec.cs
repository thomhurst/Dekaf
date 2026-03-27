using System.Buffers;
using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<CompressionType, ICompressionCodec> _codecs = [];

    /// <summary>
    /// Sentinel value indicating no compression level is set (<see cref="DefaultCompressionLevel"/> returns null).
    /// </summary>
    private const int NotSet = int.MinValue;

    private int _defaultCompressionLevel = NotSet;

    /// <summary>
    /// Default global registry with built-in codecs.
    /// Used by RecordBatch for compression/decompression when no specific registry is provided.
    /// <para/>
    /// <b>Warning:</b> This is a process-global singleton. Codec registrations and configuration
    /// changes affect all clients in the same process. If multiple clients need different codec
    /// configurations, create separate <see cref="CompressionCodecRegistry"/> instances instead.
    /// </summary>
    public static CompressionCodecRegistry Default { get; } = new();

    /// <summary>
    /// Default compression level hint for codec registration.
    /// When set, codec extension methods (AddLz4, AddZstd, etc.) can use this level
    /// as a fallback when no explicit level is provided.
    /// Null means use each codec's built-in default.
    /// This property is thread-safe via <see cref="Volatile"/> reads/writes.
    /// </summary>
    public int? DefaultCompressionLevel
    {
        get
        {
            var value = Volatile.Read(ref _defaultCompressionLevel);
            return value == NotSet ? null : value;
        }
        set => Volatile.Write(ref _defaultCompressionLevel, value ?? NotSet);
    }

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
