#if NETSTANDARD2_0
namespace System.Buffers.Text;

internal static class Base64Url
{
    public static string EncodeToString(ReadOnlySpan<byte> bytes)
    {
        var text = Convert.ToBase64String(bytes.ToArray());
        return text.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
#endif
