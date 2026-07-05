using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
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
            _ =>
#if NETSTANDARD2_0
                CompressionLevel.Optimal
#else
                CompressionLevel.SmallestSize
#endif
        };
    }

    public CompressionType Type => CompressionType.Gzip;

    public void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        using var outputStream = new BufferWriterStream(destination);
        using var gzipStream = new GZipStream(outputStream, _compressionLevel, leaveOpen: true);

        foreach (var segment in source)
        {
#if NETSTANDARD2_0
            WriteSegment(gzipStream, segment);
#else
            gzipStream.Write(segment.Span);
#endif
        }
    }

    public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        using var inputStream = new ReadOnlySequenceStream(source);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);

        CompressionStreamCopy.CopyToBufferWriter(gzipStream, destination);
    }

#if NETSTANDARD2_0
    private static void WriteSegment(Stream stream, ReadOnlyMemory<byte> segment)
    {
        if (MemoryMarshal.TryGetArray(segment, out var arraySegment) &&
            arraySegment.Array is not null)
        {
            stream.Write(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
            return;
        }

        var rented = ArrayPool<byte>.Shared.Rent(segment.Length);
        try
        {
            segment.Span.CopyTo(rented);
            stream.Write(rented, 0, segment.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
#endif
}

internal static class CompressionStreamCopy
{
    private const int BufferSize = 8192;

    internal static void CopyToBufferWriter(Stream source, IBufferWriter<byte> destination)
    {
#if NETSTANDARD2_0
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            while (true)
            {
                var bytesRead = source.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    break;

                buffer.AsSpan(0, bytesRead).CopyTo(destination.GetSpan(bytesRead));
                destination.Advance(bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
#else
        while (true)
        {
            var span = destination.GetSpan(BufferSize);
            var bytesRead = source.Read(span);
            if (bytesRead == 0)
                break;

            destination.Advance(bytesRead);
        }
#endif
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

#if !NETSTANDARD2_0
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var span = _writer.GetSpan(buffer.Length);
        buffer.CopyTo(span);
        _writer.Advance(buffer.Length);
    }
#endif
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
