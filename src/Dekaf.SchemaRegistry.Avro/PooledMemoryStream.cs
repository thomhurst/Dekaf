using System.Buffers;

namespace Dekaf.SchemaRegistry.Avro;

/// <summary>
/// A memory stream that uses an ArrayPool-rented buffer and can grow as needed.
/// This avoids allocations on the hot path by reusing pooled buffers.
/// </summary>
internal sealed class PooledMemoryStream : Stream
{
    private static readonly byte[] EmptyBuffer = [];

    private byte[] _buffer;
    private int _origin;
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
        _buffer = EmptyBuffer;
        Reset(initialBuffer);
    }

    /// <summary>
    /// Creates a new PooledMemoryStream with a buffer containing data for reading.
    /// </summary>
    /// <param name="buffer">The buffer containing data (should be rented from ArrayPool).</param>
    /// <param name="length">The number of valid bytes in the buffer.</param>
    public PooledMemoryStream(byte[] buffer, int length)
    {
        _buffer = EmptyBuffer;
        Reset(buffer, length);
    }

    public void Reset(byte[] buffer, int length = 0)
    {
        Reset(buffer, offset: 0, length);
    }

    public void Reset(byte[] buffer, int offset, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (offset > buffer.Length || length > buffer.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(length), "Offset and length must describe a valid range in the buffer.");

        ReleaseOwnedBuffer();

        _buffer = buffer;
        _origin = offset;
        _position = 0;
        _length = length;
        _ownsBuffer = false; // Caller owns the supplied buffer
        _disposed = false;
    }

    public void ResetForWriting(int minimumCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumCapacity);

        if (!_ownsBuffer || _buffer.Length < minimumCapacity)
        {
            ReleaseOwnedBuffer();
            _buffer = ArrayPool<byte>.Shared.Rent(minimumCapacity);
            _ownsBuffer = true;
        }

        _origin = 0;
        _position = 0;
        _length = 0;
        _disposed = false;
    }

    public void DetachBuffer()
    {
        ReleaseOwnedBuffer();

        _buffer = EmptyBuffer;
        _origin = 0;
        _position = 0;
        _length = 0;
        _ownsBuffer = false;
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => !_disposed;
    public override bool CanWrite => !_disposed;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > Array.MaxLength)
                throw new ArgumentOutOfRangeException(nameof(value), $"Position must be between 0 and {Array.MaxLength}.");
            _position = (int)value;
        }
    }

    /// <summary>
    /// Gets the underlying buffer. For write-mode streams, valid bytes are from 0 to Position.
    /// </summary>
    public byte[] GetBuffer() => _buffer;

    public int Capacity => _buffer.Length - _origin;

    public override void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var newPosition = (long)_position + count;
        if (newPosition > Array.MaxLength)
            throw new NotSupportedException($"PooledMemoryStream does not support streams larger than {Array.MaxLength} bytes.");

        EnsureCapacity((int)newPosition);
        Buffer.BlockCopy(buffer, offset, _buffer, _origin + _position, count);
        _position += count;
        if (_position > _length)
            _length = _position;
    }

    public override void WriteByte(byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_position == Array.MaxLength)
            throw new NotSupportedException($"PooledMemoryStream does not support streams larger than {Array.MaxLength} bytes.");

        EnsureCapacity(_position + 1);
        _buffer[_origin + _position++] = value;
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

        Buffer.BlockCopy(_buffer, _origin + _position, buffer, offset, count);
        _position += count;
        return count;
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var available = _length - _position;
        if (available <= 0)
            return 0;

        var count = Math.Min(buffer.Length, available);
        _buffer.AsSpan(_origin + _position, count).CopyTo(buffer);
        _position += count;
        return count;
    }

    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_position >= _length)
            return -1;

        return _buffer[_origin + _position++];
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

        if (newPosition < 0 || newPosition > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(offset), $"Seek position must be between 0 and {Array.MaxLength}.");

        _position = (int)newPosition;
        return _position;
    }

    public override void SetLength(long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (value < 0 || value > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(value), $"Length must be between 0 and {Array.MaxLength}.");

        EnsureCapacity((int)value);
        _length = (int)value;
        if (_position > _length)
            _position = _length;
    }

    public override void Flush() { }

    private void EnsureCapacity(int requiredCapacity)
    {
        if (_origin + requiredCapacity <= _buffer.Length)
            return;

        // Grow the buffer by at least doubling, but at least to the required capacity.
        // Widen to long to detect overflow and cap at Array.MaxLength.
        var doubled = Math.Min((long)Capacity * 2, Array.MaxLength);
        var newCapacity = (int)Math.Max(doubled, requiredCapacity);

        if (newCapacity <= Capacity)
            throw new InvalidOperationException("Cannot grow buffer: maximum size reached.");

        var newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);

        Buffer.BlockCopy(_buffer, _origin, newBuffer, 0, _length);

        // Return the old buffer if we own it (we grew it)
        if (_ownsBuffer)
        {
            // No need to clear - contains serialized Avro payloads, not sensitive data
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: false);
        }

        _buffer = newBuffer;
        _origin = 0;
        _ownsBuffer = true; // We now own this buffer
    }

    private void ReleaseOwnedBuffer()
    {
        if (!_ownsBuffer)
            return;

        // No need to clear - contains serialized Avro payloads, not sensitive data
        ArrayPool<byte>.Shared.Return(_buffer, clearArray: false);
        _ownsBuffer = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing && _ownsBuffer)
        {
            ReleaseOwnedBuffer();
        }

        _buffer = EmptyBuffer;
        _origin = 0;
        _disposed = true;
        base.Dispose(disposing);
    }
}
