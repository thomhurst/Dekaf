#if NETSTANDARD2_0
namespace System.Text;

internal static class EncodingCompatibilityExtensions
{
    public static int GetBytes(this Encoding encoding, string text, Span<byte> destination)
    {
        var bytes = encoding.GetBytes(text);
        bytes.AsSpan().CopyTo(destination);
        return bytes.Length;
    }

    public static int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> destination)
    {
        var text = new string(chars.ToArray());
        return GetBytes(encoding, text, destination);
    }

    public static int GetByteCount(this Encoding encoding, ReadOnlySpan<char> chars)
        => encoding.GetByteCount(chars.ToArray());

    public static string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
        => encoding.GetString(bytes.ToArray());
}

internal static class StringCompatibilityExtensions
{
    public static bool Contains(this string text, string value, StringComparison comparisonType)
        => text.IndexOf(value, comparisonType) >= 0;

    public static bool Contains(this string text, char value, StringComparison comparisonType)
        => text.IndexOf(value.ToString(), comparisonType) >= 0;

    public static bool StartsWith(this string text, char value)
        => text.Length > 0 && text[0] == value;

    public static string[] Split(this string text, char separator, StringSplitOptions options)
        => text.Split(new[] { separator }, options);
}

internal static class Ascii
{
    public static bool IsValid(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] > 0x7F)
                return false;
        }

        return true;
    }
}
#endif
