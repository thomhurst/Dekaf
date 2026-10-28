using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Dekaf.Protocol.Records;

namespace Dekaf.Compression.Brotli;

/// <summary>
/// Brotli compression codec using the built-in <see cref="BrotliStream"/> from <c>System.IO.Compression</c>.
/// <para>
/// <strong>Important:</strong> Brotli is NOT a standard Kafka compression type.
/// Standard Kafka brokers reject produce requests that use it, so the producer builder rejects
/// <see cref="CompressionType.Brotli"/> with a <see cref="NotSupportedException"/> from <c>Build()</c>.
/// Installing <c>Dekaf.Compression.Brotli</c> on both the producer and the consumer does not enable
/// Brotli on standard Kafka brokers. Standard Kafka clients (Java, librdkafka, Confluent.Kafka) do not support Brotli either.
/// </para>
/// <para>
/// Use the codec directly for client-side scenarios such as local or custom storage, where Dekaf
/// performs both compression and decompression. For Kafka topics, use Gzip, Snappy, Lz4, or Zstd.
/// </para>
/// <para>
/// Brotli provides excellent compression ratios, especially for text-heavy payloads,
/// but has higher CPU cost than LZ4 or Snappy.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var codec = new BrotliCompressionCodec(CompressionLevel.SmallestSize);
///
/// // Compress a payload for local or custom storage
/// var compressed = new ArrayBufferWriter&lt;byte&gt;();
/// codec.Compress(new ReadOnlySequence&lt;byte&gt;(payload), compressed);
///
/// // Decompress it later with the same codec
/// var decompressed = new ArrayBufferWriter&lt;byte&gt;();
/// codec.Decompress(new ReadOnlySequence&lt;byte&gt;(compressed.WrittenMemory), decompressed);
/// </code>
/// </example>
public sealed class BrotliCompressionCodec : ICompressionCodec
{
    private readonly CompressionLevel _compressionLevel;

    /// <summary>
    /// Creates a new Brotli compression codec with the specified .NET compression level.
    /// </summary>
    /// <param name="compressionLevel">The .NET <see cref="CompressionLevel"/> to use. Default is <see cref="CompressionLevel.Fastest"/>.</param>
    public BrotliCompressionCodec(CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        _compressionLevel = compressionLevel;
    }

    /// <inheritdoc />
    public CompressionType Type => CompressionType.Brotli;

    /// <inheritdoc />
    public void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        using var outputStream = new BufferWriterStream(destination);
        using var brotliStream = new BrotliStream(outputStream, _compressionLevel, leaveOpen: true);

        foreach (var segment in source)
        {
            brotliStream.Write(segment.Span);
        }

        brotliStream.Flush();
    }

    /// <inheritdoc />
    public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        using var inputStream = new ReadOnlySequenceStream(source);
        using var brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress);

        CompressionStreamCopy.CopyToBufferWriter(brotliStream, destination, "Invalid Brotli payload.");
    }
}

internal static class BrotliModuleInit
{
    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries")]
    internal static void Register()
    {
        CompressionCodecRegistry.Default.AddBrotli();
    }
}

/// <summary>
/// Extension methods for configuring Brotli compression on the producer builder.
/// </summary>
public static class BrotliProducerBuilderExtensions
{
    /// <summary>
    /// Configures the producer to use Brotli compression.
    /// <para>
    /// <strong>Important:</strong> Brotli is NOT a standard Kafka compression type, and standard Kafka brokers
    /// reject produce requests that use it. <c>Build()</c> therefore throws a <see cref="NotSupportedException"/>
    /// for a producer configured with this method, even when both clients have the
    /// <c>Dekaf.Compression.Brotli</c> package installed. Use <see cref="BrotliCompressionCodec"/> directly for
    /// local or custom storage, and Gzip, Snappy, Lz4, or Zstd for Kafka topics.
    /// </para>
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseBrotliCompression<TKey, TValue>(this ProducerBuilder<TKey, TValue> builder)
    {
        return builder.UseCompression(CompressionType.Brotli);
    }
}

/// <summary>
/// Extension methods for registering Brotli compression.
/// </summary>
public static class BrotliCompressionExtensions
{
    /// <summary>
    /// Registers the Brotli compression codec with the specified .NET compression level.
    /// <para>
    /// <strong>Important:</strong> Brotli is NOT a standard Kafka compression type.
    /// Registering the codec does not enable Brotli on standard Kafka brokers: the producer builder still rejects
    /// <see cref="CompressionType.Brotli"/>. Standard Kafka clients (Java, librdkafka, Confluent.Kafka)
    /// cannot decompress Brotli-compressed messages.
    /// </para>
    /// </summary>
    /// <param name="registry">The compression codec registry.</param>
    /// <param name="compressionLevel">
    /// The .NET <see cref="CompressionLevel"/> to use. Default is <see cref="CompressionLevel.Fastest"/>.
    /// </param>
    /// <returns>The registry for fluent chaining.</returns>
    public static CompressionCodecRegistry AddBrotli(this CompressionCodecRegistry registry, CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        registry.Register(new BrotliCompressionCodec(compressionLevel));
        return registry;
    }

}
