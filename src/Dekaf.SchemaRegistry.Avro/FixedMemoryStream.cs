namespace Dekaf.SchemaRegistry.Avro;

internal sealed class FixedMemoryStream : Stream
{
    private Memory<byte> _memory;
    private int _position;

    public int WrittenCount => _position;

    public void Reset(Memory<byte> memory)
    {
        _memory = memory;
        _position = 0;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _position;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var newPosition = _position + count;
        if (newPosition > _memory.Length)
            throw new FixedMemoryStreamOverflowException(newPosition);

        buffer.AsSpan(offset, count).CopyTo(_memory.Span.Slice(_position));
        _position = newPosition;
    }

    public override void WriteByte(byte value)
    {
        var newPosition = _position + 1;
        if (newPosition > _memory.Length)
            throw new FixedMemoryStreamOverflowException(newPosition);

        _memory.Span[_position] = value;
        _position = newPosition;
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();
}

internal sealed class FixedMemoryStreamOverflowException(int requiredCapacity) : Exception
{
    public int RequiredCapacity { get; } = requiredCapacity;
}
