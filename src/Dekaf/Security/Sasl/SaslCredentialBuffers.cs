using System.Security.Cryptography;

namespace Dekaf.Security.Sasl;

internal static class SaslCredentialBuffers
{
    public static void Clear(byte[]? buffer)
    {
        if (buffer is { Length: > 0 })
            CompatibilityBcl.ZeroMemory(buffer);
    }
}
