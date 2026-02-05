using System.Buffers;
using System.IO.Compression;
using Dekaf.Protocol.Records;

namespace Dekaf.Compression;

/// <summary>
/// No compression codec (pass-through).
/// </summary>
public sealed class NoneCompressionCodec : ICompressionCodec
{
    public CompressionType Type => CompressionType.None;

    public void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        foreach (var segment in source)
        {
            var span = destination.GetSpan(segment.Length);
            segment.Span.CopyTo(span);
            destination.Advance(segment.Length);
        }
    }

    public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        foreach (var segment in source)
        {
            var span = destination.GetSpan(segment.Length);
            segment.Span.CopyTo(span);
            destination.Advance(segment.Length);
        }
    }
}

/// <summary>
/// Gzip compression codec.
/// </summary>
public sealed class GzipCompressionCodec : ICompressionCodec
{
    private readonly CompressionLevel _compressionLevel;

    /// <summary>
    /// Creates a new Gzip compression codec with the specified .NET compression level.
    /// </summary>
    /// <param name="compressionLevel">The .NET CompressionLevel to use. Default is Fastest.</param>
    public GzipCompressionCodec(CompressionLevel compressionLevel = CompressionLevel.Fastest)
    {
        _compressionLevel = compressionLevel;
    }

    /// <summary>
    /// Creates a new Gzip compression codec with an integer compression level (0-9).
    /// Maps to .NET CompressionLevel: 0 = NoCompression, 1-3 = Fastest, 4-6 = Optimal, 7-9 = SmallestSize.
    /// </summary>
    /// <param name="compressionLevel">The compression level (0-9).</param>
    public GzipCompressionCodec(int compressionLevel)
    {
        if (compressionLevel < 0 || compressionLevel > 9)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compressionLevel),
                $"Gzip compression level must be between 0 and 9, but was {compressionLevel}.");
        }

        _compressionLevel = compressionLevel switch
        {
            0 => CompressionLevel.NoCompression,
            >= 1 and <= 3 => CompressionLevel.Fastest,
            >= 4 and <= 6 => CompressionLevel.Optimal,
            _ => CompressionLevel.SmallestSize
        };
    }

    public CompressionType Type => CompressionType.Gzip;

    public void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        using var outputStream = new BufferWriterStream(destination);
        using var gzipStream = new GZipStream(outputStream, _compressionLevel, leaveOpen: true);

        foreach (var segment in source)
        {
            gzipStream.Write(segment.Span);
        }
    }

    public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        using var inputStream = new ReadOnlySequenceStream(source);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);

        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            int bytesRead;
            while ((bytesRead = gzipStream.Read(buffer)) > 0)
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
