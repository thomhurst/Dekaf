namespace Dekaf;

internal static class KafkaGuid
{
    public static Guid ReadBigEndian(ReadOnlySpan<byte> source)
    {
#if NETSTANDARD2_0
        Span<byte> bytes = stackalloc byte[16];
        bytes[0] = source[3];
        bytes[1] = source[2];
        bytes[2] = source[1];
        bytes[3] = source[0];
        bytes[4] = source[5];
        bytes[5] = source[4];
        bytes[6] = source[7];
        bytes[7] = source[6];
        source.Slice(8, 8).CopyTo(bytes.Slice(8));
        return new Guid(bytes.ToArray());
#else
        return new Guid(source, bigEndian: true);
#endif
    }

    public static void WriteBigEndian(Guid value, Span<byte> destination)
    {
#if NETSTANDARD2_0
        var bytes = value.ToByteArray();
        destination[0] = bytes[3];
        destination[1] = bytes[2];
        destination[2] = bytes[1];
        destination[3] = bytes[0];
        destination[4] = bytes[5];
        destination[5] = bytes[4];
        destination[6] = bytes[7];
        destination[7] = bytes[6];
        bytes.AsSpan(8, 8).CopyTo(destination.Slice(8));
#else
        value.TryWriteBytes(destination, bigEndian: true, out _);
#endif
    }
}
