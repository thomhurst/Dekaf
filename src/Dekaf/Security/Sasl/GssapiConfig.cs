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
    /// If not specified, uses the default credentials from the credential cache.
    /// </summary>
    public string? Principal { get; init; }

    /// <summary>
    /// Path to the keytab file for service authentication.
    /// If not specified, uses the default keytab or credential cache.
    /// </summary>
    public string? KeytabPath { get; init; }

    /// <summary>
    /// The Kerberos realm. If not specified, uses the default realm from the system configuration.
    /// </summary>
    public string? Realm { get; init; }
}
