using System.Buffers;
using System.Runtime.CompilerServices;
using Dekaf;
using Dekaf.Compression;
using Dekaf.Protocol.Records;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace Dekaf.Compression.Lz4;

/// <summary>
/// LZ4 compression codec using LZ4 frame format (LZ4F).
/// Kafka requires LZ4 frame format (magic: 0x04 0x22 0x4D 0x18), not raw block format.
/// </summary>
public sealed class Lz4CompressionCodec : ICompressionCodec
{
    private readonly LZ4Level _compressionLevel;

    /// <summary>
    /// Creates a new LZ4 compression codec with the specified compression level.
    /// </summary>
    /// <param name="compressionLevel">The compression level to use. Default is LZ4Level.L00_FAST.</param>
    public Lz4CompressionCodec(LZ4Level compressionLevel = LZ4Level.L00_FAST)
    {
        _compressionLevel = compressionLevel;
    }

    /// <inheritdoc />
    public CompressionType Type => CompressionType.Lz4;

    /// <inheritdoc />
    public void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        // Use LZ4 frame format as required by Kafka
        using var outputStream = new BufferWriterStream(destination);
        using var lz4Stream = LZ4Stream.Encode(outputStream, _compressionLevel, leaveOpen: true);

        foreach (var segment in source)
        {
            lz4Stream.Write(segment.Span);
        }
    }

    /// <inheritdoc />
    public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        using var inputStream = new ReadOnlySequenceStream(source);
        using var lz4Stream = LZ4Stream.Decode(inputStream, leaveOpen: true);

        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            int bytesRead;
            while ((bytesRead = lz4Stream.Read(buffer)) > 0)
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

internal static class Lz4ModuleInit
{
    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries")]
    internal static void Register()
    {
        CompressionCodecRegistry.Default.AddLz4();
    }
}

/// <summary>
/// Extension methods for configuring LZ4 compression on the producer builder.
/// </summary>
public static class Lz4ProducerBuilderExtensions
{
    /// <summary>
    /// Configures the producer to use LZ4 compression.
    /// </summary>
    public static ProducerBuilder<TKey, TValue> UseLz4Compression<TKey, TValue>(this ProducerBuilder<TKey, TValue> builder)
    {
        return builder.UseCompression(CompressionType.Lz4);
    }
}

/// <summary>
/// Extension methods for registering LZ4 compression.
/// </summary>
public static class Lz4CompressionExtensions
{
    /// <summary>
    /// Registers the LZ4 compression codec with the specified compression level.
    /// </summary>
    /// <param name="registry">The compression codec registry.</param>
    /// <param name="compressionLevel">The compression level to use. Default is LZ4Level.L00_FAST.</param>
    /// <returns>The registry for fluent chaining.</returns>
    public static CompressionCodecRegistry AddLz4(this CompressionCodecRegistry registry, LZ4Level compressionLevel = LZ4Level.L00_FAST)
    {
        registry.Register(new Lz4CompressionCodec(compressionLevel));
        return registry;
    }

    /// <summary>
    /// Registers the LZ4 compression codec with an integer compression level (0-12).
    /// If no explicit level is provided, falls back to <see cref="CompressionCodecRegistry.DefaultCompressionLevel"/>,
    /// then to LZ4's default (L00_FAST).
    /// </summary>
    /// <param name="registry">The compression codec registry.</param>
    /// <param name="compressionLevel">The compression level (0-12). Null uses the registry default or codec default.</param>
    /// <returns>The registry for fluent chaining.</returns>
    public static CompressionCodecRegistry AddLz4(this CompressionCodecRegistry registry, int? compressionLevel)
    {
        var level = compressionLevel ?? registry.DefaultCompressionLevel;

        if (level.HasValue)
        {
            if (level.Value < 0 || level.Value > 12)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(compressionLevel),
                    $"LZ4 compression level must be between 0 and 12, but was {level.Value}.");
            }

            var lz4Level = level.Value switch
            {
                0 => LZ4Level.L00_FAST,
                1 => LZ4Level.L03_HC,
                2 => LZ4Level.L04_HC,
                3 => LZ4Level.L05_HC,
                4 => LZ4Level.L06_HC,
                5 => LZ4Level.L07_HC,
                6 => LZ4Level.L08_HC,
                7 => LZ4Level.L09_HC,
                8 => LZ4Level.L10_OPT,
                9 => LZ4Level.L11_OPT,
                10 => LZ4Level.L12_MAX,
                11 => LZ4Level.L12_MAX,
                12 => LZ4Level.L12_MAX,
                _ => LZ4Level.L00_FAST
            };

            registry.Register(new Lz4CompressionCodec(lz4Level));
        }
        else
        {
            registry.Register(new Lz4CompressionCodec());
        }

        return registry;
    }
}

/// <summary>
/// Stream wrapper for IBufferWriter.
/// </summary>
internal sealed class BufferWriterStream : Stream
{
    private readonly IBufferWriter<byte> _writer;

    public BufferWriterStream(IBufferWriter<byte> writer)
    {
        _writer = writer;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        var span = _writer.GetSpan(count);
        buffer.AsSpan(offset, count).CopyTo(span);
        _writer.Advance(count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var span = _writer.GetSpan(buffer.Length);
        buffer.CopyTo(span);
        _writer.Advance(buffer.Length);
    }
}

/// <summary>
/// Stream wrapper for ReadOnlySequence.
/// </summary>
internal sealed class ReadOnlySequenceStream : Stream
{
    private ReadOnlySequence<byte> _sequence;
    private SequencePosition _position;

    public ReadOnlySequenceStream(ReadOnlySequence<byte> sequence)
    {
        _sequence = sequence;
        _position = sequence.Start;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _sequence.Length;
    public override long Position
    {
        get => _sequence.Slice(_sequence.Start, _position).Length;
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remaining = _sequence.Slice(_position);
        if (remaining.IsEmpty)
            return 0;

        var toRead = (int)Math.Min(count, remaining.Length);
        remaining.Slice(0, toRead).CopyTo(buffer.AsSpan(offset));
        _position = _sequence.GetPosition(toRead, _position);
        return toRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();
}
