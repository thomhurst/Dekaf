using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Dekaf.Protocol.Records;

namespace Dekaf.Compression.Brotli;

/// <summary>
/// Brotli compression codec using the built-in <see cref="BrotliStream"/> from <c>System.IO.Compression</c>.
/// <para>
/// <strong>Important:</strong> Brotli is NOT a standard Kafka compression type.
/// Standard Kafka clients (Java, librdkafka, Confluent.Kafka) do not support Brotli.
/// Both the producer and consumer must have the <c>Dekaf.Compression.Brotli</c> package installed
/// for messages to be compressed and decompressed correctly.
/// </para>
/// <para>
/// Brotli provides excellent compression ratios, especially for text-heavy payloads,
/// but has higher CPU cost than LZ4 or Snappy. Consider using Zstd for a better
/// balance of compression ratio and speed in most Kafka workloads.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // Register the Brotli codec with default settings
/// CompressionCodecRegistry.Default.AddBrotli();
///
/// // Register with a specific compression level (0-11)
/// CompressionCodecRegistry.Default.AddBrotli(compressionLevel: 4);
///
/// // Use with a producer builder
/// var producer = Kafka.CreateProducer&lt;string, string&gt;()
///     .WithBootstrapServers("localhost:9092")
///     .UseBrotliCompression()
///     .Build();
/// </code>
/// </example>
public sealed class BrotliCompressionCodec : ICompressionCodec
{
    /// <summary>
    /// The minimum Brotli compression level (no compression).
    /// </summary>
    public const int MinCompressionLevel = 0;

    /// <summary>
    /// The maximum Brotli compression level (best compression).
    /// </summary>
    public const int MaxCompressionLevel = 11;

    /// <summary>
    /// The default Brotli compression level, balancing speed and compression ratio.
    /// </summary>
    public const int DefaultCompressionLevel = 4;

    private readonly CompressionLevel _compressionLevel;

    /// <summary>
    /// Creates a new Brotli compression codec with the specified .NET compression level.
    /// </summary>
    /// <param name="compressionLevel">The .NET <see cref="CompressionLevel"/> to use. Default is <see cref="CompressionLevel.Fastest"/>.</param>
    public BrotliCompressionCodec(CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        _compressionLevel = compressionLevel;
    }

    /// <summary>
    /// Creates a new Brotli compression codec with an integer compression level (0-11).
    /// </summary>
    /// <param name="compressionLevel">
    /// The compression level (0-11). 0 = no compression, 1 = fastest, 11 = best compression.
    /// Default is 4 (a good balance between speed and ratio).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="compressionLevel"/> is less than 0 or greater than 11.
    /// </exception>
    public BrotliCompressionCodec(int compressionLevel)
    {
        if (compressionLevel < MinCompressionLevel || compressionLevel > MaxCompressionLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compressionLevel),
                $"Brotli compression level must be between {MinCompressionLevel} and {MaxCompressionLevel}, but was {compressionLevel}.");
        }

        _compressionLevel = compressionLevel switch
        {
            0 => CompressionLevel.NoCompression,
            >= 1 and <= 3 => CompressionLevel.Fastest,
            >= 4 and <= 8 => CompressionLevel.Optimal,
            _ => CompressionLevel.SmallestSize
        };
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
    }

    /// <inheritdoc />
    public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        using var inputStream = new ReadOnlySequenceStream(source);
        using var brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress);

        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            int bytesRead;
            while ((bytesRead = brotliStream.Read(buffer)) > 0)
            {
                var span = destination.GetSpan(bytesRead);
                buffer.AsSpan(0, bytesRead).CopyTo(span);
                destination.Advance(bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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
    /// <strong>Important:</strong> Brotli is NOT a standard Kafka compression type.
    /// Both the producer and consumer must have the <c>Dekaf.Compression.Brotli</c> package installed.
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
    /// Registers the Brotli compression codec with the specified compression level.
    /// <para>
    /// <strong>Important:</strong> Brotli is NOT a standard Kafka compression type.
    /// Both the producer and consumer must have the <c>Dekaf.Compression.Brotli</c> package installed.
    /// Standard Kafka clients (Java, librdkafka, Confluent.Kafka) cannot decompress Brotli-compressed messages.
    /// </para>
    /// </summary>
    /// <param name="registry">The compression codec registry.</param>
    /// <param name="compressionLevel">
    /// The compression level to use (0-11). Default is 4.
    /// 0 = no compression, 1 = fastest, 11 = best compression.
    /// </param>
    /// <returns>The registry for fluent chaining.</returns>
    public static CompressionCodecRegistry AddBrotli(this CompressionCodecRegistry registry, int compressionLevel = BrotliCompressionCodec.DefaultCompressionLevel)
    {
        registry.Register(new BrotliCompressionCodec(compressionLevel));
        return registry;
    }

    /// <summary>
    /// Registers the Brotli compression codec, using the registry's default compression level if available.
    /// If no explicit level is provided, falls back to <see cref="CompressionCodecRegistry.DefaultCompressionLevel"/>,
    /// then to Brotli's default (4).
    /// <para>
    /// <strong>Important:</strong> Brotli is NOT a standard Kafka compression type.
    /// Both the producer and consumer must have the <c>Dekaf.Compression.Brotli</c> package installed.
    /// </para>
    /// </summary>
    /// <param name="registry">The compression codec registry.</param>
    /// <param name="compressionLevel">The compression level (0-11). Null uses the registry default or codec default (4).</param>
    /// <returns>The registry for fluent chaining.</returns>
    public static CompressionCodecRegistry AddBrotliWithLevel(this CompressionCodecRegistry registry, int? compressionLevel = null)
    {
        var level = compressionLevel ?? registry.DefaultCompressionLevel;

        if (level.HasValue)
        {
            if (level.Value < BrotliCompressionCodec.MinCompressionLevel || level.Value > BrotliCompressionCodec.MaxCompressionLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(compressionLevel),
                    $"Brotli compression level must be between {BrotliCompressionCodec.MinCompressionLevel} and {BrotliCompressionCodec.MaxCompressionLevel}, but was {level.Value}.");
            }

            registry.Register(new BrotliCompressionCodec(level.Value));
        }
        else
        {
            registry.Register(new BrotliCompressionCodec());
        }

        return registry;
    }
}
