using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Dekaf.SchemaRegistry;

/// <summary>
/// TLS configuration for the default Schema Registry HTTP pipeline.
/// </summary>
public sealed class SchemaRegistryTlsConfig
{
    /// <summary>
    /// Path to a CA certificate file or a directory containing CA certificate files.
    /// Supported extensions are .pem, .crt, .cer, .pfx, and .p12.
    /// </summary>
    public string? CaCertificatePath { get; init; }

    /// <summary>
    /// Password for a CA PKCS#12 file. The same password is used for every PKCS#12 file in a directory.
    /// </summary>
    public string? CaCertificatePassword { get; init; }

    /// <summary>
    /// PEM text containing one or more CA certificates.
    /// </summary>
    public string? CaCertificatePem { get; init; }

    /// <summary>
    /// Caller-owned CA certificate used to validate the server certificate.
    /// </summary>
    public X509Certificate2? CaCertificate { get; init; }

    /// <summary>
    /// Caller-owned CA certificates used to validate the server certificate.
    /// </summary>
    public X509Certificate2Collection? CaCertificates { get; init; }

    /// <summary>
    /// Caller-owned client certificate with a private key for mutual TLS.
    /// </summary>
    public X509Certificate2? ClientCertificate { get; init; }

    /// <summary>
    /// Path to a client PEM certificate or PKCS#12 file.
    /// </summary>
    public string? ClientCertificatePath { get; init; }

    /// <summary>
    /// Path to the private key for a PEM client certificate.
    /// </summary>
    public string? ClientPrivateKeyPath { get; init; }

    /// <summary>
    /// PEM text containing the client certificate.
    /// </summary>
    public string? ClientCertificatePem { get; init; }

    /// <summary>
    /// PEM text containing the client private key.
    /// </summary>
    public string? ClientPrivateKeyPem { get; init; }

    /// <summary>
    /// Password for a client PKCS#12 file or encrypted PEM private key.
    /// </summary>
    public string? ClientCertificatePassword { get; init; }

    /// <summary>
    /// Whether to validate the server certificate. Default is true.
    /// </summary>
    public bool ValidateServerCertificate { get; init; } = true;

    /// <summary>
    /// Whether to validate the server certificate host name. Default is true.
    /// </summary>
    public bool ValidateServerCertificateHostName { get; init; } = true;

    /// <summary>
    /// Whether to check certificate revocation. Default is false.
    /// </summary>
    public bool CheckCertificateRevocation { get; init; }

    /// <summary>
    /// Enabled TLS protocols. When null, operating-system defaults are used.
    /// </summary>
    public SslProtocols? EnabledSslProtocols { get; init; }

    /// <summary>
    /// Caller callback that owns server-certificate validation when supplied.
    /// </summary>
    public RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; init; }
}
