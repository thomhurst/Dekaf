namespace Dekaf.Security.Sasl;

/// <summary>
/// Configuration for GSSAPI (Kerberos) authentication.
/// </summary>
public sealed class GssapiConfig
{
    /// <summary>
    /// The Kerberos service principal name. Defaults to "kafka".
    /// The full SPN will be: {ServiceName}/{hostname}@{Realm}
    /// </summary>
    public string ServiceName { get; init; } = "kafka";

    /// <summary>
    /// The client principal name (e.g., "user@REALM.COM").
    /// </summary>
    /// <remarks>
    /// Note: This property is provided for configuration compatibility with librdkafka,
    /// but it does not directly affect credential selection in .NET. The NegotiateAuthentication
    /// API always uses the system's default credentials from the Kerberos credential cache.
    /// To authenticate as a specific principal:
    /// - On Linux/macOS: Run <c>kinit principal@REALM</c> before starting the application
    /// - On Windows: Log in as the desired user or use <c>runas /user:principal</c>
    /// </remarks>
    public string? Principal { get; init; }

    /// <summary>
    /// Path to the keytab file for service authentication.
    /// </summary>
    /// <remarks>
    /// Note: This property is provided for configuration compatibility with librdkafka,
    /// but it does not directly affect credential selection in .NET. The NegotiateAuthentication
    /// API always uses the system's Kerberos configuration for keytab-based authentication.
    /// To use a specific keytab:
    /// - On Linux: Set the <c>KRB5_CLIENT_KTNAME</c> environment variable to the keytab path
    /// - On macOS: Set the <c>KRB5_CLIENT_KTNAME</c> environment variable to the keytab path
    /// - On Windows: Configure the keytab through the system Kerberos configuration
    /// </remarks>
    public string? KeytabPath { get; init; }

    /// <summary>
    /// The Kerberos realm. If not specified, uses the default realm from the system configuration.
    /// </summary>
    public string? Realm { get; init; }
}
