namespace Dekaf.Security.Sasl;

/// <summary>
/// Username and password used for SASL/PLAIN or SASL/SCRAM authentication.
/// </summary>
public readonly struct SaslCredentials
{
    public SaslCredentials(string username, string password)
    {
        Username = username ?? throw new ArgumentNullException(nameof(username));
        Password = password ?? throw new ArgumentNullException(nameof(password));
    }

    /// <summary>The SASL username.</summary>
    public string Username { get; }

    /// <summary>The SASL password.</summary>
    public string Password { get; }
}
