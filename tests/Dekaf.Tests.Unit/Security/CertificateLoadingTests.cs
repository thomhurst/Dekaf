using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dekaf.Security;

namespace Dekaf.Tests.Unit.Security;

/// <summary>
/// Tests for certificate loading functionality.
/// Tests PEM and PFX certificate loading from files.
/// </summary>
[NotInParallel("CertificateGeneration")]
public class CertificateLoadingTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _tempFiles = [];

    public CertificateLoadingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dekaf-cert-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { /* ignore */ }
        }
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
        GC.SuppressFinalize(this);
    }

    [Test]
    public async Task TlsConfig_CaCertificatePath_CanPointToPemFile()
    {
        // Create a test CA certificate
        using var caCert = TestCertificateHelper.CreateSelfSignedCertificate("CN=Test CA");
        var pemPath = WriteCertificateToPem(caCert, "ca.pem");

        var config = new TlsConfig
        {
            CaCertificatePath = pemPath
        };

        await Assert.That(config.CaCertificatePath).IsNotNull();
        await Assert.That(File.Exists(config.CaCertificatePath!)).IsTrue();
    }

    [Test]
    public async Task TlsConfig_CaCertificatePath_CanPointToPfxFile()
    {
        // Create a test CA certificate
        using var caCert = TestCertificateHelper.CreateSelfSignedCertificate("CN=Test CA");
        var pfxPath = WriteCertificateToPfx(caCert, "ca.pfx", password: null);

        var config = new TlsConfig
        {
            CaCertificatePath = pfxPath
        };

        await Assert.That(config.CaCertificatePath).IsNotNull();
        await Assert.That(Path.GetExtension(config.CaCertificatePath!).ToLowerInvariant()).IsEqualTo(".pfx");
    }

    [Test]
    public async Task TlsConfig_ClientCertificatePath_WithKeyPath_ForPem()
    {
        // Create a test client certificate with private key
        using var clientCert = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Test Client");
        var (certPath, keyPath) = WriteCertificateAndKeyToPem(clientCert, "client.pem", "client.key");

        var config = new TlsConfig
        {
            ClientCertificatePath = certPath,
            ClientKeyPath = keyPath
        };

        await Assert.That(config.ClientCertificatePath).IsNotNull();
        await Assert.That(config.ClientKeyPath).IsNotNull();
        await Assert.That(File.Exists(config.ClientCertificatePath!)).IsTrue();
        await Assert.That(File.Exists(config.ClientKeyPath!)).IsTrue();
    }

    [Test]
    public async Task TlsConfig_ClientCertificatePath_ForPfx()
    {
        // Create a test client certificate with private key
        using var clientCert = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Test Client");
        var pfxPath = WriteCertificateToPfx(clientCert, "client.pfx", password: "testpass");

        var config = new TlsConfig
        {
            ClientCertificatePath = pfxPath,
            ClientKeyPassword = "testpass"
        };

        await Assert.That(config.ClientCertificatePath).IsNotNull();
        await Assert.That(config.ClientKeyPassword).IsEqualTo("testpass");
    }

    [Test]
    public async Task PemFile_CanContainMultipleCertificates()
    {
        // Create multiple CA certificates
        using var ca1 = TestCertificateHelper.CreateSelfSignedCertificate("CN=CA1");
        using var ca2 = TestCertificateHelper.CreateSelfSignedCertificate("CN=CA2");

        var pemPath = WriteMultipleCertificatesToPem([ca1, ca2], "ca-bundle.pem");

        // Verify the file contains both certificates
        var pemContent = await File.ReadAllTextAsync(pemPath);
        var certCount = pemContent.Split("-----BEGIN CERTIFICATE-----").Length - 1;

        await Assert.That(certCount).IsEqualTo(2);
    }

    [Test]
    public async Task X509Certificate2Collection_ImportFromPemFile_LoadsMultipleCerts()
    {
        // Create multiple CA certificates
        using var ca1 = TestCertificateHelper.CreateSelfSignedCertificate("CN=CA1");
        using var ca2 = TestCertificateHelper.CreateSelfSignedCertificate("CN=CA2");

        var pemPath = WriteMultipleCertificatesToPem([ca1, ca2], "ca-bundle.pem");

        var collection = new X509Certificate2Collection();
        collection.ImportFromPemFile(pemPath);

        await Assert.That(collection.Count).IsEqualTo(2);

        // Cleanup
        foreach (var cert in collection)
        {
            cert.Dispose();
        }
    }

    [Test]
    public async Task CreateMutualTls_WithFilePaths_SetsAllPaths()
    {
        // Create test certificates
        using var caCert = TestCertificateHelper.CreateSelfSignedCertificate("CN=Test CA");
        using var clientCert = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Test Client");

        var caPemPath = WriteCertificateToPem(caCert, "ca.pem");
        var (clientCertPath, clientKeyPath) = WriteCertificateAndKeyToPem(clientCert, "client.pem", "client.key");

        var config = TlsConfig.CreateMutualTls(
            caCertPath: caPemPath,
            clientCertPath: clientCertPath,
            clientKeyPath: clientKeyPath,
            keyPassword: null);

        await Assert.That(config.CaCertificatePath).IsEqualTo(caPemPath);
        await Assert.That(config.ClientCertificatePath).IsEqualTo(clientCertPath);
        await Assert.That(config.ClientKeyPath).IsEqualTo(clientKeyPath);
        await Assert.That(config.ValidateServerCertificate).IsTrue();
    }

    [Test]
    public async Task PfxFile_CanBeLoadedWithPassword()
    {
        using var cert = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Test");
        var pfxPath = WriteCertificateToPfx(cert, "test.pfx", "mypassword");

        // Verify the PFX can be loaded with the correct password using modern API
        using var loadedCert = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, "mypassword");

        await Assert.That(loadedCert.Subject).IsEqualTo("CN=Test");
        await Assert.That(loadedCert.HasPrivateKey).IsTrue();
    }

    [Test]
    public async Task PfxFile_WithWrongPassword_ThrowsException()
    {
        using var cert = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Test");
        var pfxPath = WriteCertificateToPfx(cert, "test.pfx", "correctpassword");

        // Verify that wrong password throws
        await Assert.That(() => X509CertificateLoader.LoadPkcs12FromFile(pfxPath, "wrongpassword"))
            .Throws<CryptographicException>();
    }

    private string WriteCertificateToPem(X509Certificate2 cert, string fileName)
    {
        var path = Path.Combine(_tempDir, fileName);
        var pem = cert.ExportCertificatePem();
        File.WriteAllText(path, pem);
        _tempFiles.Add(path);
        return path;
    }

    private string WriteMultipleCertificatesToPem(X509Certificate2[] certs, string fileName)
    {
        var path = Path.Combine(_tempDir, fileName);
        var pem = string.Join("\n", certs.Select(c => c.ExportCertificatePem()));
        File.WriteAllText(path, pem);
        _tempFiles.Add(path);
        return path;
    }

    private string WriteCertificateToPfx(X509Certificate2 cert, string fileName, string? password)
    {
        var path = Path.Combine(_tempDir, fileName);
        var pfxBytes = cert.Export(X509ContentType.Pfx, password);
        File.WriteAllBytes(path, pfxBytes);
        _tempFiles.Add(path);
        return path;
    }

    private (string certPath, string keyPath) WriteCertificateAndKeyToPem(X509Certificate2 cert, string certFileName, string keyFileName)
    {
        var certPath = Path.Combine(_tempDir, certFileName);
        var keyPath = Path.Combine(_tempDir, keyFileName);

        var certPem = cert.ExportCertificatePem();
        File.WriteAllText(certPath, certPem);
        _tempFiles.Add(certPath);

        using var rsa = cert.GetRSAPrivateKey();
        if (rsa is not null)
        {
            var keyPem = rsa.ExportRSAPrivateKeyPem();
            File.WriteAllText(keyPath, keyPem);
            _tempFiles.Add(keyPath);
        }

        return (certPath, keyPath);
    }
}
