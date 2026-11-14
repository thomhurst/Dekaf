using System.Buffers;

namespace Dekaf.Streams.Internal;

/// <summary>A growable buffer that one node or store reuses for every record it serializes.</summary>
internal sealed class ReusableBufferWriter : IBufferWriter<byte>
{
    private byte[] _buffer = new byte[64];
    private int _written;

    public int WrittenCount => _written;

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    public void Reset() => _written = 0;

    /// <summary>Drops everything written after the first <paramref name="length"/> bytes.</summary>
    public void Truncate(int length) => _written = length;

    public void Advance(int count) => _written += count;

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
        var needed = _written + Math.Max(sizeHint, 1);
        if (needed > _buffer.Length)
        {
            Array.Resize(ref _buffer, Math.Max(needed, _buffer.Length * 2));
        }
    }
}
