using System.Reflection;
using System.Security.Cryptography;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public class ScramAuthenticatorTests
{
    [Test]
    public async Task Hmac_ScramSha256_MatchesInstanceHmac()
    {
        var key = "test-key"u8.ToArray();
        var message = "test-message"u8.ToArray();

        var result = InvokeHmac(SaslMechanism.ScramSha256, key, message);

        using var hmac = new HMACSHA256(key);
        await Assert.That(result).IsEquivalentTo(hmac.ComputeHash(message));
    }

    [Test]
    public async Task Hmac_ScramSha512_MatchesInstanceHmac()
    {
        var key = "test-key"u8.ToArray();
        var message = "test-message"u8.ToArray();

        var result = InvokeHmac(SaslMechanism.ScramSha512, key, message);

        using var hmac = new HMACSHA512(key);
        await Assert.That(result).IsEquivalentTo(hmac.ComputeHash(message));
    }

    private static byte[] InvokeHmac(SaslMechanism mechanism, byte[] key, byte[] message)
    {
        var authenticator = new ScramAuthenticator(mechanism, "user", "password");
        var method = typeof(ScramAuthenticator).GetMethod(
            "Hmac",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(byte[]), typeof(byte[])],
            modifiers: null);

        return (byte[])method!.Invoke(authenticator, [key, message])!;
    }
}
