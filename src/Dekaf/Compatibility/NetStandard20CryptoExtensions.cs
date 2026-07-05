#if NETSTANDARD2_0
namespace System.Security.Cryptography;

internal static class CryptographicOperations
{
    public static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
            return false;

        var diff = 0;
        for (var i = 0; i < left.Length; i++)
            diff |= left[i] ^ right[i];

        return diff == 0;
    }

    public static void ZeroMemory(Span<byte> buffer) => buffer.Clear();
}
#endif
