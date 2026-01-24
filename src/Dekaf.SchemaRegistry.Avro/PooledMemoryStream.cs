using System.Buffers;

namespace Dekaf.SchemaRegistry.Avro;

/// <summary>
/// A memory stream that uses an ArrayPool-rented buffer and can grow as needed.
/// This avoids allocations on the hot path by reusing pooled buffers.
/// </summary>
internal sealed class PooledMemoryStream : Stream
{
    private byte[] _buffer;
    private int _position;
    private int _length;
    private bool _disposed;
    private bool _ownsBuffer;

    /// <summary>
    /// Creates a new PooledMemoryStream with an initial rented buffer for writing.
    /// </summary>
    /// <param name="initialBuffer">The initial buffer (should be rented from ArrayPool).</param>
    public PooledMemoryStream(byte[] initialBuffer)
    {
        _buffer = initialBuffer;
        _ownsBuffer = false; // Caller owns the initial buffer
    }

    /// <summary>
    /// Creates a new PooledMemoryStream with a buffer containing data for reading.
    /// </summary>
    /// <param name="buffer">The buffer containing data (should be rented from ArrayPool).</param>
    /// <param name="length">The number of valid bytes in the buffer.</param>
    public PooledMemoryStream(byte[] buffer, int length)
    {
        _buffer = buffer;
        _length = length;
        _ownsBuffer = false; // Caller owns the buffer
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => !_disposed;
    public override bool CanWrite => !_disposed;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => _position = (int)value;
    }

    /// <summary>
    /// Gets the underlying buffer. Only valid bytes are from 0 to Position.
    /// </summary>
    public byte[] GetBuffer() => _buffer;

    public override void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnsureCapacity(_position + count);
        Buffer.BlockCopy(buffer, offset, _buffer, _position, count);
        _position += count;
        if (_position > _length)
            _length = _position;
    }

    public override void WriteByte(byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnsureCapacity(_position + 1);
        _buffer[_position++] = value;
        if (_position > _length)
            _length = _position;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var available = _length - _position;
        if (available <= 0)
            return 0;

        if (count > available)
            count = available;

        Buffer.BlockCopy(_buffer, _position, buffer, offset, count);
        _position += count;
        return count;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        _position = (int)newPosition;
        return _position;
    }

    public override void SetLength(long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnsureCapacity((int)value);
        _length = (int)value;
        if (_position > _length)
            _position = _length;
    }

    public override void Flush() { }

    private void EnsureCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= _buffer.Length)
            return;

        // Grow the buffer by at least doubling, but at least to the required capacity
        var newCapacity = Math.Max(_buffer.Length * 2, requiredCapacity);
        var newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);

        Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _length);

        // Return the old buffer if we own it (we grew it)
        if (_ownsBuffer)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        _buffer = newBuffer;
        _ownsBuffer = true; // We now own this buffer
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing && _ownsBuffer)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}
