using System.Security.Cryptography;
using System.Text;
using Dekaf.Errors;

namespace Dekaf.Security.Sasl;

/// <summary>
/// SASL SCRAM (Salted Challenge Response Authentication Mechanism) authenticator.
/// Implements RFC 5802 for SCRAM-SHA-256 and SCRAM-SHA-512.
/// </summary>
public sealed partial class ScramAuthenticator : ISaslAuthenticator
{
    /// <summary>Default upper bound on server-requested PBKDF2 iterations.</summary>
    public const int DefaultMaxIterations = 1_000_000;

    private static readonly Encoding ChallengeEncoding = new UTF8Encoding(false, true);
    private readonly string _username;
    private readonly string _password;
    private readonly HashAlgorithmName _hashAlgorithm;
    private readonly int _hashSize;
    private readonly string _mechanismName;
    private readonly bool _tokenAuth;
    private readonly int _maxIterations;

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
        Complete,
        Failed
    }

    /// <summary>
    /// Creates a new SCRAM authenticator.
    /// </summary>
    /// <param name="mechanism">The SCRAM mechanism (ScramSha256 or ScramSha512).</param>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <param name="tokenAuth">Whether to authenticate with a Kafka delegation token.</param>
    public ScramAuthenticator(SaslMechanism mechanism, string username, string password, bool tokenAuth = false)
        : this(mechanism, username, password, tokenAuth, DefaultMaxIterations)
    {
    }

    /// <summary>Creates a SCRAM authenticator with a bound on server-requested hashing work.</summary>
    /// <param name="mechanism">The SCRAM mechanism.</param>
    /// <param name="username">The username or delegation token ID.</param>
    /// <param name="password">The password or delegation token HMAC.</param>
    /// <param name="maxIterations">Maximum accepted positive PBKDF2 iteration count.</param>
    /// <param name="tokenAuth">Whether to authenticate with a Kafka delegation token.</param>
    public ScramAuthenticator(SaslMechanism mechanism, string username, string password, bool tokenAuth, int maxIterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxIterations, 1);
        _maxIterations = maxIterations;
        if (mechanism != SaslMechanism.ScramSha256 && mechanism != SaslMechanism.ScramSha512)
        {
            throw new ArgumentException("Mechanism must be ScramSha256 or ScramSha512", nameof(mechanism));
        }

        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _tokenAuth = tokenAuth;

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

        // Build client-first-message-bare: n=<username>,r=<nonce>[,tokenauth=true]
        var saslName = SaslPrepUsername(_username);
        _clientFirstMessageBare = _tokenAuth
            ? $"n={saslName},r={_clientNonce},tokenauth=true"
            : $"n={saslName},r={_clientNonce}";

        // client-first-message: gs2-header client-first-message-bare
        // gs2-header for no channel binding: n,,
        var clientFirstMessage = $"n,,{_clientFirstMessageBare}";

        _state = ScramState.ClientFirstSent;
        return Encoding.UTF8.GetBytes(clientFirstMessage);
    }

    /// <inheritdoc />
    public byte[]? EvaluateChallenge(byte[] challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        try
        {
            return _state switch
            {
                ScramState.ClientFirstSent => HandleServerFirst(ChallengeEncoding.GetString(challenge)),
                ScramState.ClientFinalSent => HandleServerFinal(ChallengeEncoding.GetString(challenge)),
                _ => throw new InvalidOperationException($"Unexpected state: {_state}")
            };
        }
        catch (DecoderFallbackException)
        {
            _state = ScramState.Failed;
            throw new AuthenticationException("Invalid UTF-8 in SCRAM server message");
        }
        catch (AuthenticationException)
        {
            _state = ScramState.Failed;
            throw;
        }
    }

    private byte[] HandleServerFirst(string serverFirstMessage)
    {
        // Parse server-first-message: r=<nonce>,s=<salt>,i=<iteration-count>
        var parts = ParseMessage(serverFirstMessage.AsSpan(), serverFirst: true);
        var serverNonce = parts.Nonce;
        if (serverNonce.Length <= _clientNonce!.Length || !serverNonce.StartsWith(_clientNonce.AsSpan(), StringComparison.Ordinal))
        {
            throw new AuthenticationException("Server nonce must extend the client nonce");
        }

        for (var index = 0; index < serverNonce.Length; index++)
        {
            if (serverNonce[index] is < '!' or > '~')
                throw new AuthenticationException("Invalid characters in SCRAM server nonce");
        }

        var iterations = ParseIterationCount(parts.Iterations);
        var salt = DecodeBase64(parts.Salt, "Invalid SCRAM salt encoding");

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
        var clientFinalMessageWithoutProof = $"c={channelBinding},r={serverNonce.ToString()}";

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
        var parts = ParseMessage(serverFinalMessage.AsSpan(), serverFirst: false);

        if (!parts.Error.IsEmpty)
        {
            // Unknown server errors are other-error, and arbitrary server text must not
            // become a credential-bearing or multiline diagnostic.
            throw new AuthenticationException($"SCRAM authentication failed: {GetServerError(parts.Error)}");
        }

        var actualServerSignature = DecodeBase64(parts.Verifier, "Invalid SCRAM server signature encoding");
        if (actualServerSignature.Length != _hashSize)
            throw new AuthenticationException("Invalid SCRAM server signature length");

        // Verify server signature
        var serverKey = Hmac(_saltedPassword!, "Server Key");
        var expectedServerSignature = Hmac(serverKey, _authMessage!);

        if (!CryptographicOperations.FixedTimeEquals(expectedServerSignature, actualServerSignature))
        {
            throw new AuthenticationException("Server signature verification failed");
        }

        _state = ScramState.Complete;
        return null;
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
        return _hashAlgorithm == HashAlgorithmName.SHA256
            ? HMACSHA256.HashData(key, message)
            : HMACSHA512.HashData(key, message);
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
