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
        var authzid = _authorizationId ?? string.Empty;
        var message = $"{authzid}\0{_username}\0{_password}";
        _complete = true;
        return Encoding.UTF8.GetBytes(message);
    }

    /// <inheritdoc />
    public byte[]? EvaluateChallenge(byte[] challenge)
    {
        // PLAIN is a single-round mechanism, no challenge expected
        _complete = true;
        return null;
    }
}
