using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dekaf.SchemaRegistry.Avro;

internal sealed class AllocationFreeBinaryEncoder(Stream stream) : global::Avro.IO.Encoder
{
    private const int StackBufferSize = 256;
    private readonly Stream _stream = stream;

    public void WriteNull() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBoolean(bool value) => _stream.WriteByte(value ? (byte)1 : (byte)0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt(int value) => WriteLong(value);

    public void WriteLong(long value)
    {
        var encoded = (ulong)((value << 1) ^ (value >> 63));
        while ((encoded & ~0x7FUL) != 0)
        {
            _stream.WriteByte((byte)((encoded & 0x7F) | 0x80));
            encoded >>= 7;
        }

        _stream.WriteByte((byte)encoded);
    }

    public void WriteFloat(float value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(float)];
        BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
        _stream.Write(bytes);
    }

    public void WriteDouble(double value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(double)];
        BinaryPrimitives.WriteDoubleLittleEndian(bytes, value);
        _stream.Write(bytes);
    }

    public void WriteBytes(byte[] value)
    {
        WriteLong(value.Length);
        _stream.Write(value, 0, value.Length);
    }

    public void WriteBytes(byte[] value, int offset, int length)
    {
        WriteLong(length);
        _stream.Write(value, offset, length);
    }

    public void WriteString(string value) => WriteString(value.AsSpan());

    internal void WriteString(ReadOnlySpan<char> value)
    {
        if (value.Length <= StackBufferSize / 3)
        {
            Span<byte> bytes = stackalloc byte[StackBufferSize];
            var written = Encoding.UTF8.GetBytes(value, bytes);
            WriteLong(written);
            _stream.Write(bytes.Slice(0, written));
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(value);
        var rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var written = Encoding.UTF8.GetBytes(value, rented);
            WriteLong(written);
            _stream.Write(rented.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    internal void WriteBytes(ReadOnlySpan<byte> value)
    {
        WriteLong(value.Length);
        _stream.Write(value);
    }

    internal void WriteFixed(ReadOnlySpan<byte> value) => _stream.Write(value);

    public void WriteEnum(int value) => WriteLong(value);
    public void SetItemCount(long value)
    {
        if (value > 0)
            WriteLong(value);
    }

    public void StartItem() { }
    public void WriteArrayStart() { }
    public void WriteArrayEnd() => WriteLong(0);
    public void WriteMapStart() { }
    public void WriteMapEnd() => WriteLong(0);
    public void WriteUnionIndex(int value) => WriteLong(value);
    public void WriteFixed(byte[] data) => _stream.Write(data, 0, data.Length);
    public void WriteFixed(byte[] data, int start, int length) => _stream.Write(data, start, length);
    public void Flush() => _stream.Flush();
}
