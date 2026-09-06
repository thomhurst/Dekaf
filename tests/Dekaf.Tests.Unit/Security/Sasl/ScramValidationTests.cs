using System.Security.Cryptography;
using System.Text;
using Dekaf.Errors;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public class ScramValidationTests
{
    public static IEnumerable<(SaslMechanism Mechanism, string Challenge)> InvalidFirstMessages()
    {
        string[] challenges =
        [
            "m=unsupported,r={nonce}server,s=c2FsdA==,i=4096",
            "r={nonce}server,s=c2FsdA==,i=4096,m=unsupported",
            "r={nonce}server,s=c2FsdA==,i=4096,i=1",
            "r={nonce}server,r={nonce}server,s=c2FsdA==,i=1",
            "r={nonce}server,s=c2FsdA==,s=c2FsdA==,i=1",
            "r={nonce}server,s=c2FsdA==,i=1,x=one,x=two",
            "r={nonce}server,s=c2FsdA==,i=1,broken",
            "r={nonce}server,s=c2FsdA==,i=1,",
            ",r={nonce}server,s=c2FsdA==,i=1",
            "r={nonce}server,,s=c2FsdA==,i=1",
            "r={nonce}server,s=c2FsdA==,i=1,xx=value",
            "r={nonce}server,s=c2FsdA==,i=1,x=",
            "r={nonce}server,s=c2FsdA==,i=1,v=",
            "r={nonce}server,s=c2FsdA==,i=1,x=bad\0value",
            "s=c2FsdA==,i=1",
            "r={nonce}server,i=1",
            "r={nonce}server,s=c2FsdA==",
            "s=c2FsdA==,r={nonce}server,i=1",
            "r=wrong-nonce,s=c2FsdA==,i=1",
            "r={nonce},s=c2FsdA==,i=1",
            "r={nonce} space,s=c2FsdA==,i=1",
            "r={nonce}server,s=invalid!,i=1",
            "r={nonce}server,s=AB==,i=1",
            "r={nonce}server,s=c2Fs dA==,i=1",
            "r={nonce}server,s=c2FsdA=,i=1",
            "r={nonce}server,s=c2FsdA==,i=0",
            "r={nonce}server,s=c2FsdA==,i=-1",
            "r={nonce}server,s=c2FsdA==,i=+1",
            "r={nonce}server,s=c2FsdA==,i=01",
            "r={nonce}server,s=c2FsdA==,i= 1",
            "r={nonce}server,s=c2FsdA==,i=1 ",
            "r={nonce}server,s=c2FsdA==,i=2147483648"
        ];
        foreach (var mechanism in new[] { SaslMechanism.ScramSha256, SaslMechanism.ScramSha512 })
        {
            foreach (var challenge in challenges)
                yield return (mechanism, challenge);
        }
    }

    [Test]
    [MethodDataSource(nameof(InvalidFirstMessages))]
    public async Task ServerFirst_RejectsMalformedMessage(SaslMechanism mechanism, string challenge)
    {
        var authenticator = new ScramAuthenticator(mechanism, "test-user", "test-password");
        var first = Encoding.UTF8.GetString(authenticator.GetInitialResponse());
        var nonce = first[(first.IndexOf(",r=", StringComparison.Ordinal) + 3)..];
        var bytes = Encoding.UTF8.GetBytes(challenge.Replace("{nonce}", nonce));

        await Assert.That(() => authenticator.EvaluateChallenge(bytes)).Throws<AuthenticationException>();
        await Assert.That(authenticator.IsComplete).IsFalse();
        await Assert.That(typeof(ScramAuthenticator).GetField("_saltedPassword",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(authenticator)).IsNull();
    }

    [Test]
    [Arguments(SaslMechanism.ScramSha256, false)]
    [Arguments(SaslMechanism.ScramSha256, true)]
    [Arguments(SaslMechanism.ScramSha512, false)]
    [Arguments(SaslMechanism.ScramSha512, true)]
    public async Task ValidExchange_VerifiesBothProofsAndIgnoresOptionalExtensions(SaslMechanism mechanism, bool tokenAuth)
    {
        var exchange = new Exchange(mechanism, tokenAuth, firstExtensions: ",x=δ,X=other");
        await Assert.That(exchange.ClientProof).IsEquivalentTo(exchange.ExpectedClientProof);
        await Assert.That(exchange.ClientFirst.Contains(",tokenauth=true", StringComparison.Ordinal)).IsEqualTo(tokenAuth);
        var result = exchange.Authenticator.EvaluateChallenge(Encoding.UTF8.GetBytes($"v={exchange.Signature},y=ignored"));
        await Assert.That(result).IsNull();
        await Assert.That(exchange.Authenticator.IsComplete).IsTrue();
    }

    public static IEnumerable<(SaslMechanism Mechanism, string Challenge)> InvalidFinalMessages()
    {
        string[] challenges =
        [
            "v={signature},v={signature}",
            "v={signature},e=invalid-proof",
            "e=invalid-proof,v={signature}",
            "v={signature},m=unsupported",
            "v={signature},x=one,x=two",
            "v={signature},s=",
            "v={signature},broken",
            "v={signature},",
            "x=one,v={signature}",
            "v=invalid!",
            "v=",
            "v=AA==",
            "e=",
            ""
        ];
        foreach (var mechanism in new[] { SaslMechanism.ScramSha256, SaslMechanism.ScramSha512 })
        {
            foreach (var challenge in challenges)
                yield return (mechanism, challenge);
        }
    }

    [Test]
    [MethodDataSource(nameof(InvalidFinalMessages))]
    public async Task ServerFinal_RejectsMalformedMessage(SaslMechanism mechanism, string challenge)
    {
        var exchange = new Exchange(mechanism);
        var bytes = Encoding.UTF8.GetBytes(challenge.Replace("{signature}", exchange.Signature));
        await Assert.That(() => exchange.Authenticator.EvaluateChallenge(bytes)).Throws<AuthenticationException>();
        await Assert.That(exchange.Authenticator.IsComplete).IsFalse();
    }

    [Test]
    [Arguments(SaslMechanism.ScramSha256)]
    [Arguments(SaslMechanism.ScramSha512)]
    public async Task ServerFinal_RejectsNonCanonicalPaddingAndWrongSignature(SaslMechanism mechanism)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var exchange = new Exchange(mechanism);
        var signature = exchange.Signature.ToCharArray();
        var lastPayload = signature.Length - 1;
        while (signature[lastPayload] == '=')
            lastPayload--;
        signature[lastPayload] = alphabet[alphabet.IndexOf(signature[lastPayload]) + 1];
        var nonCanonical = new string(signature);
        // Convert accepts these nonzero unused bits, but SCRAM requires canonical base64.
        await Assert.That(Convert.FromBase64String(nonCanonical)).IsEquivalentTo(Convert.FromBase64String(exchange.Signature));
        await Assert.That(() => exchange.Authenticator.EvaluateChallenge(Encoding.UTF8.GetBytes($"v={nonCanonical}")))
            .Throws<AuthenticationException>();

        foreach (var corruptFirstByte in new[] { true, false })
        {
            exchange = new Exchange(mechanism);
            var incorrect = Convert.FromBase64String(exchange.Signature);
            incorrect[corruptFirstByte ? 0 : incorrect.Length - 1] ^= 1;
            await Assert.That(() => exchange.Authenticator.EvaluateChallenge(Encoding.UTF8.GetBytes($"v={Convert.ToBase64String(incorrect)}")))
                .Throws<AuthenticationException>().WithMessageContaining("verification failed");
        }
    }

    [Test]
    [Arguments(SaslMechanism.ScramSha256, false)]
    [Arguments(SaslMechanism.ScramSha256, true)]
    [Arguments(SaslMechanism.ScramSha512, false)]
    [Arguments(SaslMechanism.ScramSha512, true)]
    public async Task Challenge_RejectsInvalidUtf8(SaslMechanism mechanism, bool serverFinal)
    {
        var authenticator = serverFinal ? new Exchange(mechanism).Authenticator
            : new ScramAuthenticator(mechanism, "test-user", "test-password");
        if (!serverFinal)
            authenticator.GetInitialResponse();
        await Assert.That(() => authenticator.EvaluateChallenge([0xff]))
            .Throws<AuthenticationException>().WithMessageContaining("UTF-8");
    }

    [Test]
    [Arguments(SaslMechanism.ScramSha256)]
    [Arguments(SaslMechanism.ScramSha512)]
    public async Task ServerError_DoesNotEchoUntrustedErrorText(SaslMechanism mechanism)
    {
        var exchange = new Exchange(mechanism);
        await Assert.That(() => exchange.Authenticator.EvaluateChallenge("e=test-user test-password\nunsafe"u8.ToArray()))
            .Throws<AuthenticationException>().WithMessage("SCRAM authentication failed: other-error");
        await Assert.That(() => exchange.Authenticator.EvaluateChallenge(Encoding.UTF8.GetBytes($"v={exchange.Signature}")))
            .Throws<InvalidOperationException>();
        await Assert.That(exchange.Authenticator.IsComplete).IsFalse();
    }

    [Test]
    [Arguments(SaslMechanism.ScramSha256, 1)]
    [Arguments(SaslMechanism.ScramSha256, 16)]
    [Arguments(SaslMechanism.ScramSha512, 1)]
    [Arguments(SaslMechanism.ScramSha512, 16)]
    public async Task ConfiguredMaximum_AllowsExactBoundary(SaslMechanism mechanism, int maximum)
    {
        var exchange = new Exchange(mechanism, iterations: maximum, maxIterations: maximum);
        await Assert.That(exchange.ClientProof).IsEquivalentTo(exchange.ExpectedClientProof);
        exchange.Authenticator.EvaluateChallenge(Encoding.UTF8.GetBytes($"v={exchange.Signature}"));
        await Assert.That(exchange.Authenticator.IsComplete).IsTrue();
    }

    [Test]
    [Arguments(SaslMechanism.ScramSha256, 16)]
    [Arguments(SaslMechanism.ScramSha512, 16)]
    [Arguments(SaslMechanism.ScramSha256, ScramAuthenticator.DefaultMaxIterations)]
    [Arguments(SaslMechanism.ScramSha512, ScramAuthenticator.DefaultMaxIterations)]
    public async Task ConfiguredMaximum_RejectsBeforeDerivingPassword(SaslMechanism mechanism, int maximum)
    {
        var authenticator = new ScramAuthenticator(mechanism, "test-user", "test-password", tokenAuth: false, maxIterations: maximum);
        var first = Encoding.UTF8.GetString(authenticator.GetInitialResponse());
        var nonce = first[(first.IndexOf(",r=", StringComparison.Ordinal) + 3)..];
        var bytes = Encoding.UTF8.GetBytes($"r={nonce}server,s=c2FsdA==,i={maximum + 1}");
        await Assert.That(() => authenticator.EvaluateChallenge(bytes)).Throws<AuthenticationException>()
            .WithMessageContaining("SaslScramMaxIterations");
        var passwordField = typeof(ScramAuthenticator).GetField("_saltedPassword", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        await Assert.That(passwordField.GetValue(authenticator)).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task Constructor_RejectsInvalidMaximum(int maximum)
    {
        await Assert.That(() => new ScramAuthenticator(SaslMechanism.ScramSha256, "test-user", "test-password", false, maximum))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(SaslMechanism.ScramSha256)]
    [Arguments(SaslMechanism.ScramSha512)]
    public async Task EmptySalt_AcceptsCanonicalBase64Grammar(SaslMechanism mechanism)
    {
        var exchange = new Exchange(mechanism, emptySalt: true);
        await Assert.That(exchange.ClientProof).IsEquivalentTo(exchange.ExpectedClientProof);
        exchange.Authenticator.EvaluateChallenge(Encoding.UTF8.GetBytes($"v={exchange.Signature}"));
        await Assert.That(exchange.Authenticator.IsComplete).IsTrue();
    }

    private sealed class Exchange
    {
        public ScramAuthenticator Authenticator { get; }
        public string ClientFirst { get; }
        public byte[] ClientProof { get; }
        public byte[] ExpectedClientProof { get; }
        public string Signature { get; }

        public Exchange(SaslMechanism mechanism, bool tokenAuth = false, int iterations = 16,
            int maxIterations = ScramAuthenticator.DefaultMaxIterations, string firstExtensions = "", bool emptySalt = false)
        {
            Authenticator = new ScramAuthenticator(mechanism, "test-user", "test-password", tokenAuth, maxIterations);
            ClientFirst = Encoding.UTF8.GetString(Authenticator.GetInitialResponse());
            var nonce = ClientFirst.Split(',').Single(static part => part.StartsWith("r=", StringComparison.Ordinal))[2..];
            var salt = emptySalt ? Array.Empty<byte>() : "salt"u8.ToArray();
            var serverFirst = $"r={nonce}server,s={Convert.ToBase64String(salt)},i={iterations}{firstExtensions}";
            var final = Encoding.UTF8.GetString(Authenticator.EvaluateChallenge(Encoding.UTF8.GetBytes(serverFirst))!);
            var proofStart = final.LastIndexOf(",p=", StringComparison.Ordinal);
            ClientProof = Convert.FromBase64String(final[(proofStart + 3)..]);
            var transcript = Encoding.UTF8.GetBytes($"{ClientFirst[3..]},{serverFirst},{final[..proofStart]}");
            var algorithm = mechanism == SaslMechanism.ScramSha256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA512;
            var size = mechanism == SaslMechanism.ScramSha256 ? 32 : 64;
            var salted = Rfc2898DeriveBytes.Pbkdf2("test-password"u8.ToArray(), salt, iterations, algorithm, size);
            byte[] Hmac(byte[] key, byte[] data) => mechanism == SaslMechanism.ScramSha256
                ? HMACSHA256.HashData(key, data) : HMACSHA512.HashData(key, data);
            var clientKey = Hmac(salted, "Client Key"u8.ToArray());
            var storedKey = mechanism == SaslMechanism.ScramSha256 ? SHA256.HashData(clientKey) : SHA512.HashData(clientKey);
            var clientSignature = Hmac(storedKey, transcript);
            ExpectedClientProof = new byte[size];
            for (var index = 0; index < size; index++)
                ExpectedClientProof[index] = (byte)(clientKey[index] ^ clientSignature[index]);
            Signature = Convert.ToBase64String(Hmac(Hmac(salted, "Server Key"u8.ToArray()), transcript));
        }
    }
}
