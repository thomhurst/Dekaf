using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Dekaf.Tests.Integration.Security;

/// <summary>
/// Generates test certificates for SSL/TLS integration testing.
/// Creates a CA certificate, server certificate (signed by CA), and client certificate (signed by CA).
/// All certificates are self-signed test certificates and must not be used in production.
/// </summary>
internal sealed class TestCertificateGenerator : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// The root CA certificate used to sign both server and client certificates.
    /// </summary>
    public X509Certificate2 CaCertificate { get; }

    /// <summary>
    /// The server certificate signed by the CA, used by Kafka for SSL listeners.
    /// </summary>
    public X509Certificate2 ServerCertificate { get; }

    /// <summary>
    /// The client certificate signed by the CA, used for mutual TLS authentication.
    /// </summary>
    public X509Certificate2 ClientCertificate { get; }

    /// <summary>
    /// Directory containing the generated certificate files.
    /// </summary>
    public string CertificateDirectory { get; }

    /// <summary>
    /// Path to the CA certificate PEM file.
    /// </summary>
    public string CaCertPemPath { get; }

    /// <summary>
    /// Path to the server keystore in PKCS12 format (for Kafka).
    /// </summary>
    public string ServerKeystorePath { get; }

    /// <summary>
    /// Path to the server truststore in PKCS12 format (for Kafka).
    /// </summary>
    public string ServerTruststorePath { get; }

    /// <summary>
    /// Path to the client certificate PEM file.
    /// </summary>
    public string ClientCertPemPath { get; }

    /// <summary>
    /// Path to the client private key PEM file.
    /// </summary>
    public string ClientKeyPemPath { get; }

    /// <summary>
    /// Password used for all keystores and truststores.
    /// </summary>
    public const string StorePassword = "test-password";

    public TestCertificateGenerator()
    {
        CertificateDirectory = Path.Combine(Path.GetTempPath(), $"dekaf-tls-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(CertificateDirectory);

        // Generate CA certificate
        CaCertificate = GenerateCaCertificate();

        // Generate server certificate signed by CA
        ServerCertificate = GenerateSignedCertificate(
            CaCertificate,
            "CN=kafka-broker, O=Dekaf Test, C=US",
            isServer: true);

        // Generate client certificate signed by CA
        ClientCertificate = GenerateSignedCertificate(
            CaCertificate,
            "CN=dekaf-client, O=Dekaf Test, C=US",
            isServer: false);

        // Export certificates to files
        CaCertPemPath = Path.Combine(CertificateDirectory, "ca-cert.pem");
        ServerKeystorePath = Path.Combine(CertificateDirectory, "server.p12");
        ServerTruststorePath = Path.Combine(CertificateDirectory, "truststore.p12");
        ClientCertPemPath = Path.Combine(CertificateDirectory, "client-cert.pem");
        ClientKeyPemPath = Path.Combine(CertificateDirectory, "client-key.pem");

        ExportCertificates();
    }

    private static X509Certificate2 GenerateCaCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Dekaf Test CA, O=Dekaf Test, C=US",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: true,
                pathLengthConstraint: 1,
                critical: true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                critical: true));

        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(5));

        // Re-import to ensure private key is exportable
        return X509CertificateLoader.LoadPkcs12(
            cert.Export(X509ContentType.Pfx, StorePassword),
            StorePassword,
            X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 GenerateSignedCertificate(
        X509Certificate2 caCert,
        string subjectName,
        bool isServer)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        if (isServer)
        {
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    [new Oid("1.3.6.1.5.5.7.3.1")], // Server Authentication
                    critical: false));

            // Add Subject Alternative Names for the server certificate
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName("kafka");
            sanBuilder.AddDnsName("broker");
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            sanBuilder.AddIpAddress(System.Net.IPAddress.Parse("0.0.0.0"));
            // Add a wildcard for container networking
            sanBuilder.AddDnsName("*.docker.internal");
            request.CertificateExtensions.Add(sanBuilder.Build());
        }
        else
        {
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature,
                    critical: true));

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    [new Oid("1.3.6.1.5.5.7.3.2")], // Client Authentication
                    critical: false));
        }

        // Sign with CA
        var serialNumber = new byte[16];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] &= 0x7F; // Ensure positive serial number

        using var caPrivateKey = caCert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("CA certificate does not have an RSA private key");

        var cert = request.Create(
            caCert,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(2),
            serialNumber);

        // Combine signed certificate with private key
        var certWithKey = cert.CopyWithPrivateKey(rsa);

        // Re-import to ensure private key is exportable
        return X509CertificateLoader.LoadPkcs12(
            certWithKey.Export(X509ContentType.Pfx, StorePassword),
            StorePassword,
            X509KeyStorageFlags.Exportable);
    }

    private void ExportCertificates()
    {
        // Export CA certificate as PEM
        var caPem = ExportCertificateToPem(CaCertificate);
        File.WriteAllText(CaCertPemPath, caPem);

        // Export server keystore as PKCS12 (contains server cert + key)
        var serverP12 = ServerCertificate.Export(X509ContentType.Pfx, StorePassword);
        File.WriteAllBytes(ServerKeystorePath, serverP12);

        // Export truststore as PKCS12 (contains CA cert only, no private key)
        var caCertOnly = X509CertificateLoader.LoadCertificate(CaCertificate.RawData);
        var truststoreP12 = caCertOnly.Export(X509ContentType.Pfx, StorePassword);
        File.WriteAllBytes(ServerTruststorePath, truststoreP12);

        // Export client certificate as PEM
        var clientCertPem = ExportCertificateToPem(ClientCertificate);
        File.WriteAllText(ClientCertPemPath, clientCertPem);

        // Export client private key as PEM
        var clientKeyPem = ExportPrivateKeyToPem(ClientCertificate);
        File.WriteAllText(ClientKeyPemPath, clientKeyPem);
    }

    private static string ExportCertificateToPem(X509Certificate2 cert)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN CERTIFICATE-----");
        sb.AppendLine(Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END CERTIFICATE-----");
        return sb.ToString();
    }

    private static string ExportPrivateKeyToPem(X509Certificate2 cert)
    {
        using var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Certificate does not have an RSA private key");

        var privateKeyBytes = rsa.ExportPkcs8PrivateKey();
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PRIVATE KEY-----");
        sb.AppendLine(Convert.ToBase64String(privateKeyBytes, Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END PRIVATE KEY-----");
        return sb.ToString();
    }

    /// <summary>
    /// Generates a self-signed certificate that is NOT signed by the CA.
    /// Used for testing invalid certificate rejection.
    /// </summary>
    public static X509Certificate2 GenerateUntrustedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Untrusted Server, O=Evil Corp, C=XX",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1));

        return X509CertificateLoader.LoadPkcs12(
            cert.Export(X509ContentType.Pfx, "untrusted"),
            "untrusted",
            X509KeyStorageFlags.Exportable);
    }

    /// <summary>
    /// Generates an expired certificate signed by the CA.
    /// Used for testing expired certificate rejection.
    /// </summary>
    public X509Certificate2 GenerateExpiredCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Expired Server, O=Dekaf Test, C=US",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));

        var serialNumber = new byte[16];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] &= 0x7F;

        var cert = request.Create(
            CaCertificate,
            DateTimeOffset.UtcNow.AddYears(-2),
            DateTimeOffset.UtcNow.AddYears(-1), // Expired
            serialNumber);

        var certWithKey = cert.CopyWithPrivateKey(rsa);

        return X509CertificateLoader.LoadPkcs12(
            certWithKey.Export(X509ContentType.Pfx, StorePassword),
            StorePassword,
            X509KeyStorageFlags.Exportable);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        CaCertificate.Dispose();
        ServerCertificate.Dispose();
        ClientCertificate.Dispose();

        try
        {
            if (Directory.Exists(CertificateDirectory))
            {
                Directory.Delete(CertificateDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of temp files
        }
    }
}
