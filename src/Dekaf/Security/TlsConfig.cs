using System.Security.Cryptography.X509Certificates;

namespace Dekaf.Security;

/// <summary>
/// Configuration for TLS/SSL connections including mutual TLS (mTLS) authentication.
/// </summary>
public sealed class TlsConfig
{
    /// <summary>
    /// Path to the CA certificate file (PEM or PFX/PKCS12 format) used to validate the server's certificate.
    /// If null, the system's default certificate store is used.
    /// </summary>
    public string? CaCertificatePath { get; init; }

    /// <summary>
    /// The CA certificate used to validate the server's certificate.
    /// If null, the system's default certificate store is used.
    /// </summary>
    public X509Certificate2? CaCertificateObject { get; init; }

    /// <summary>
    /// A collection of CA certificates used to validate the server's certificate.
    /// If null, the system's default certificate store is used.
    /// </summary>
    public X509Certificate2Collection? CaCertificateCollection { get; init; }

    /// <summary>
    /// The client certificate used for mutual TLS authentication.
    /// When set, the client will present this certificate to the server during TLS handshake.
    /// </summary>
    public X509Certificate2? ClientCertificate { get; init; }

    /// <summary>
    /// Path to the client certificate file (PEM or PFX/PKCS12 format).
    /// Used when <see cref="ClientCertificate"/> is not set.
    /// </summary>
    public string? ClientCertificatePath { get; init; }

    /// <summary>
    /// Path to the client private key file (PEM format).
    /// Required when <see cref="ClientCertificatePath"/> points to a PEM certificate.
    /// </summary>
    public string? ClientKeyPath { get; init; }

    /// <summary>
    /// Password for the client private key or PFX file.
    /// </summary>
    public string? ClientKeyPassword { get; init; }

    /// <summary>
    /// Whether to validate the server's certificate.
    /// Set to false to allow self-signed certificates (not recommended for production).
    /// Default is true.
    /// </summary>
    public bool ValidateServerCertificate { get; init; } = true;

    /// <summary>
    /// The expected host name in the server's certificate.
    /// If null, the connection host name is used.
    /// </summary>
    public string? TargetHost { get; init; }

    /// <summary>
    /// List of enabled SSL/TLS protocols.
    /// If null, the system defaults are used (typically TLS 1.2 and TLS 1.3).
    /// </summary>
    public System.Security.Authentication.SslProtocols? EnabledSslProtocols { get; init; }

    /// <summary>
    /// Whether to check certificate revocation.
    /// Default is false for performance reasons.
    /// </summary>
    public bool CheckCertificateRevocation { get; init; }

    /// <summary>
    /// Creates a new TlsConfig with default settings (TLS enabled, server validation enabled).
    /// </summary>
    public TlsConfig()
    {
    }

    /// <summary>
    /// Creates a TlsConfig for mutual TLS with certificate files.
    /// </summary>
    /// <param name="caCertPath">Path to the CA certificate file.</param>
    /// <param name="clientCertPath">Path to the client certificate file.</param>
    /// <param name="clientKeyPath">Path to the client private key file.</param>
    /// <param name="keyPassword">Optional password for the private key.</param>
    /// <returns>A configured TlsConfig for mTLS.</returns>
    public static TlsConfig CreateMutualTls(
        string caCertPath,
        string clientCertPath,
        string clientKeyPath,
        string? keyPassword = null)
    {
        return new TlsConfig
        {
            CaCertificatePath = caCertPath,
            ClientCertificatePath = clientCertPath,
            ClientKeyPath = clientKeyPath,
            ClientKeyPassword = keyPassword,
            ValidateServerCertificate = true
        };
    }

    /// <summary>
    /// Creates a TlsConfig for mutual TLS with in-memory certificates.
    /// </summary>
    /// <param name="clientCertificate">The client certificate with private key.</param>
    /// <param name="caCertificate">Optional CA certificate for server validation.</param>
    /// <returns>A configured TlsConfig for mTLS.</returns>
    public static TlsConfig CreateMutualTls(
        X509Certificate2 clientCertificate,
        X509Certificate2? caCertificate = null)
    {
        return new TlsConfig
        {
            ClientCertificate = clientCertificate,
            CaCertificateObject = caCertificate,
            ValidateServerCertificate = true
        };
    }
}
