using System.Buffers;

namespace Dekaf.Serialization.Json;

/// <summary>
/// Reusable JSON scratch storage. Oversized arrays can be returned without discarding the writer.
/// </summary>
internal sealed class JsonBufferWriter : IBufferWriter<byte>
{
    private byte[] _buffer = [];
    private int _written;

    public int Capacity => _buffer.Length;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    public void ResetWrittenCount() => _written = 0;

    public void ReleaseBuffer()
    {
        var buffer = _buffer;
        _buffer = [];
        _written = 0;
        ArrayPool<byte>.Shared.Return(buffer);
    }

    public void Advance(int count)
    {
        if ((uint)count > (uint)(_buffer.Length - _written))
            throw new ArgumentOutOfRangeException(nameof(count));

        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    private void EnsureCapacity(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        sizeHint = Math.Max(sizeHint, 1);
        if (sizeHint <= _buffer.Length - _written)
            return;

        var required = checked(_written + sizeHint);
        var doubled = Math.Min((long)_buffer.Length * 2, Array.MaxLength);
        var newSize = (int)Math.Max(Math.Max(doubled, required), 256);
        var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);
        if (_buffer.Length != 0)
            ArrayPool<byte>.Shared.Return(_buffer);

        _buffer = newBuffer;
    }
}
