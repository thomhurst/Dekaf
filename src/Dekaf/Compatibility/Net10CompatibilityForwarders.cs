#if !NETSTANDARD2_0
using System.Security.Cryptography;

namespace Dekaf.Compatibility;

internal static class EnvironmentCompat
{
    public static long TickCount64 => Environment.TickCount64;
}

internal static class ObjectDisposedExceptionCompat
{
    public static void ThrowIf(bool condition, object? instance)
        => ObjectDisposedException.ThrowIf(condition, instance!);
}

internal static class RandomNumberGeneratorCompat
{
    public static byte[] GetBytes(int count)
        => RandomNumberGenerator.GetBytes(count);
}

internal static class GuidCompatibility
{
    public static Guid ReadBigEndian(ReadOnlySpan<byte> source)
        => new(source, bigEndian: true);

    public static void WriteBigEndian(Guid value, Span<byte> destination)
        => value.TryWriteBytes(destination, bigEndian: true, out _);
}
#endif
