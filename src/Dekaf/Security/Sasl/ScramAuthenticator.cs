using System.Security.Cryptography;
using System.Text;

namespace Dekaf.Security.Sasl;

/// <summary>
/// SASL SCRAM (Salted Challenge Response Authentication Mechanism) authenticator.
/// Implements RFC 5802 for SCRAM-SHA-256 and SCRAM-SHA-512.
/// </summary>
public sealed class ScramAuthenticator : ISaslAuthenticator
{
    private readonly string _username;
    private readonly string _password;
    private readonly HashAlgorithmName _hashAlgorithm;
    private readonly int _hashSize;
    private readonly string _mechanismName;

    private string? _clientNonce;
    private string? _clientFirstMessageBare;
    private byte[]? _saltedPassword;
    private string? _authMessage;
    private ScramState _state = ScramState.Initial;

    private enum ScramState
    {
        Initial,
        ClientFirstSent,
        ClientFinalSent,
        Complete
    }

    /// <summary>
    /// Creates a new SCRAM authenticator.
    /// </summary>
    /// <param name="mechanism">The SCRAM mechanism (ScramSha256 or ScramSha512).</param>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    public ScramAuthenticator(SaslMechanism mechanism, string username, string password)
    {
        if (mechanism != SaslMechanism.ScramSha256 && mechanism != SaslMechanism.ScramSha512)
        {
            throw new ArgumentException("Mechanism must be ScramSha256 or ScramSha512", nameof(mechanism));
        }

        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));

        if (mechanism == SaslMechanism.ScramSha256)
        {
            _hashAlgorithm = HashAlgorithmName.SHA256;
            _hashSize = 32;
            _mechanismName = "SCRAM-SHA-256";
        }
        else
        {
            _hashAlgorithm = HashAlgorithmName.SHA512;
            _hashSize = 64;
            _mechanismName = "SCRAM-SHA-512";
        }
    }

    /// <inheritdoc />
    public string MechanismName => _mechanismName;

    /// <inheritdoc />
    public bool IsComplete => _state == ScramState.Complete;

    /// <inheritdoc />
    public byte[] GetInitialResponse()
    {
        if (_state != ScramState.Initial)
        {
            throw new InvalidOperationException("GetInitialResponse can only be called once");
        }

        // Generate client nonce
        _clientNonce = GenerateNonce();

        // Build client-first-message-bare: n=<username>,r=<nonce>
        var saslName = SaslPrepUsername(_username);
        _clientFirstMessageBare = $"n={saslName},r={_clientNonce}";

        // client-first-message: gs2-header client-first-message-bare
        // gs2-header for no channel binding: n,,
        var clientFirstMessage = $"n,,{_clientFirstMessageBare}";

        _state = ScramState.ClientFirstSent;
        return Encoding.UTF8.GetBytes(clientFirstMessage);
    }

    /// <inheritdoc />
    public byte[]? EvaluateChallenge(byte[] challenge)
    {
        var challengeStr = Encoding.UTF8.GetString(challenge);

        return _state switch
        {
            ScramState.ClientFirstSent => HandleServerFirst(challengeStr),
            ScramState.ClientFinalSent => HandleServerFinal(challengeStr),
            _ => throw new InvalidOperationException($"Unexpected state: {_state}")
        };
    }

    private byte[] HandleServerFirst(string serverFirstMessage)
    {
        // Parse server-first-message: r=<nonce>,s=<salt>,i=<iteration-count>
        var parts = ParseMessage(serverFirstMessage);

        if (!parts.TryGetValue("r", out var serverNonce))
        {
            throw new AuthenticationException("Server nonce not found in server-first-message");
        }

        if (!serverNonce.StartsWith(_clientNonce!, StringComparison.Ordinal))
        {
            throw new AuthenticationException("Server nonce does not start with client nonce");
        }

        if (!parts.TryGetValue("s", out var saltBase64))
        {
            throw new AuthenticationException("Salt not found in server-first-message");
        }

        if (!parts.TryGetValue("i", out var iterationsStr) || !int.TryParse(iterationsStr, out var iterations))
        {
            throw new AuthenticationException("Iteration count not found or invalid in server-first-message");
        }

        var salt = Convert.FromBase64String(saltBase64);

        // Compute salted password using PBKDF2
        _saltedPassword = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(_password),
            salt,
            iterations,
            _hashAlgorithm,
            _hashSize);

        // Build client-final-message-without-proof
        // channel-binding: c=biws (base64 of "n,,")
        var channelBinding = "biws"; // base64("n,,")
        var clientFinalMessageWithoutProof = $"c={channelBinding},r={serverNonce}";

        // Build auth message
        _authMessage = $"{_clientFirstMessageBare},{serverFirstMessage},{clientFinalMessageWithoutProof}";

        // Compute proof
        var clientKey = Hmac(_saltedPassword, "Client Key");
        var storedKey = Hash(clientKey);
        var clientSignature = Hmac(storedKey, _authMessage);
        var clientProof = Xor(clientKey, clientSignature);

        // Build client-final-message
        var proof = Convert.ToBase64String(clientProof);
        var clientFinalMessage = $"{clientFinalMessageWithoutProof},p={proof}";

        _state = ScramState.ClientFinalSent;
        return Encoding.UTF8.GetBytes(clientFinalMessage);
    }

    private byte[]? HandleServerFinal(string serverFinalMessage)
    {
        // Parse server-final-message: v=<verifier> or e=<error>
        var parts = ParseMessage(serverFinalMessage);

        if (parts.TryGetValue("e", out var error))
        {
            throw new AuthenticationException($"SCRAM authentication failed: {error}");
        }

        if (!parts.TryGetValue("v", out var serverSignatureBase64))
        {
            throw new AuthenticationException("Server signature not found in server-final-message");
        }

        // Verify server signature
        var serverKey = Hmac(_saltedPassword!, "Server Key");
        var expectedServerSignature = Hmac(serverKey, _authMessage!);
        var actualServerSignature = Convert.FromBase64String(serverSignatureBase64);

        if (!CryptographicOperations.FixedTimeEquals(expectedServerSignature, actualServerSignature))
        {
            throw new AuthenticationException("Server signature verification failed");
        }

        _state = ScramState.Complete;
        return null;
    }

    private static Dictionary<string, string> ParseMessage(string message)
    {
        var result = new Dictionary<string, string>();
        foreach (var part in message.Split(','))
        {
            var idx = part.IndexOf('=');
            if (idx > 0)
            {
                var key = part[..idx];
                var value = part[(idx + 1)..];
                result[key] = value;
            }
        }
        return result;
    }

    private static string GenerateNonce()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes);
    }

    private static string SaslPrepUsername(string username)
    {
        // SASLprep: RFC 4013
        // For simplicity, we just escape '=' and ',' characters
        return username.Replace("=", "=3D").Replace(",", "=2C");
    }

    private byte[] Hmac(byte[] key, string message)
    {
        return Hmac(key, Encoding.UTF8.GetBytes(message));
    }

    private byte[] Hmac(byte[] key, byte[] message)
    {
        using var hmac = _hashAlgorithm == HashAlgorithmName.SHA256
            ? (HMAC)new HMACSHA256(key)
            : new HMACSHA512(key);
        return hmac.ComputeHash(message);
    }

    private byte[] Hash(byte[] data)
    {
        return _hashAlgorithm == HashAlgorithmName.SHA256
            ? SHA256.HashData(data)
            : SHA512.HashData(data);
    }

    private static byte[] Xor(byte[] a, byte[] b)
    {
        var result = new byte[a.Length];
        for (var i = 0; i < a.Length; i++)
        {
            result[i] = (byte)(a[i] ^ b[i]);
        }
        return result;
    }
}

/// <summary>
/// Exception thrown when SASL authentication fails.
/// </summary>
public class AuthenticationException : Exception
{
    public AuthenticationException(string message) : base(message) { }
    public AuthenticationException(string message, Exception innerException) : base(message, innerException) { }
}
