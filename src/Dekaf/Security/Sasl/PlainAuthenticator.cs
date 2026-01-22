using System.Text;

namespace Dekaf.Security.Sasl;

/// <summary>
/// SASL PLAIN mechanism authenticator.
/// Implements RFC 4616 - The PLAIN Simple Authentication and Security Layer (SASL) Mechanism.
/// </summary>
public sealed class PlainAuthenticator : ISaslAuthenticator
{
    private readonly string _username;
    private readonly string _password;
    private readonly string? _authorizationId;
    private bool _complete;

    /// <summary>
    /// Creates a new PLAIN authenticator.
    /// </summary>
    /// <param name="username">The authentication identity (username).</param>
    /// <param name="password">The password.</param>
    /// <param name="authorizationId">Optional authorization identity. If null, uses the username.</param>
    public PlainAuthenticator(string username, string password, string? authorizationId = null)
    {
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _authorizationId = authorizationId;
    }

    /// <inheritdoc />
    public string MechanismName => "PLAIN";

    /// <inheritdoc />
    public bool IsComplete => _complete;

    /// <inheritdoc />
    public byte[] GetInitialResponse()
    {
        // PLAIN message format: [authzid] NUL authcid NUL passwd
        // authzid = authorization identity (optional, usually empty for Kafka)
        // authcid = authentication identity (username)
        // passwd = password
        // Write directly to buffer to avoid string interpolation allocation
        var authzid = _authorizationId ?? string.Empty;
        var totalBytes = Encoding.UTF8.GetByteCount(authzid) + 1 +
                         Encoding.UTF8.GetByteCount(_username) + 1 +
                         Encoding.UTF8.GetByteCount(_password);

        var result = new byte[totalBytes];
        var offset = 0;

        offset += Encoding.UTF8.GetBytes(authzid, result.AsSpan(offset));
        result[offset++] = 0; // NUL separator
        offset += Encoding.UTF8.GetBytes(_username, result.AsSpan(offset));
        result[offset++] = 0; // NUL separator
        Encoding.UTF8.GetBytes(_password, result.AsSpan(offset));

        _complete = true;
        return result;
    }

    /// <inheritdoc />
    public byte[]? EvaluateChallenge(byte[] challenge)
    {
        // PLAIN is a single-round mechanism, no challenge expected
        _complete = true;
        return null;
    }
}
