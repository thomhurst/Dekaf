using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Dekaf.Security;

namespace Dekaf.Tests.Unit.Security;

[NotInParallel("CertificateGeneration")]
public class TlsConfigTests
{
    [Test]
    public async Task DefaultConfig_HasExpectedDefaults()
    {
        var config = new TlsConfig();

        await Assert.That(config.ValidateServerCertificate).IsTrue();
        await Assert.That(config.CheckCertificateRevocation).IsFalse();
        await Assert.That(config.CaCertificatePath).IsNull();
        await Assert.That(config.CaCertificateObject).IsNull();
        await Assert.That(config.CaCertificateCollection).IsNull();
        await Assert.That(config.ClientCertificate).IsNull();
        await Assert.That(config.ClientCertificatePath).IsNull();
        await Assert.That(config.ClientKeyPath).IsNull();
        await Assert.That(config.ClientKeyPassword).IsNull();
        await Assert.That(config.TargetHost).IsNull();
        await Assert.That(config.EnabledSslProtocols).IsNull();
    }

    [Test]
    public async Task CreateMutualTls_WithFilePaths_SetsCorrectProperties()
    {
        var config = TlsConfig.CreateMutualTls(
            caCertPath: "/path/to/ca.pem",
            clientCertPath: "/path/to/client.pem",
            clientKeyPath: "/path/to/client.key",
            keyPassword: "secret");

        await Assert.That(config.CaCertificatePath).IsEqualTo("/path/to/ca.pem");
        await Assert.That(config.ClientCertificatePath).IsEqualTo("/path/to/client.pem");
        await Assert.That(config.ClientKeyPath).IsEqualTo("/path/to/client.key");
        await Assert.That(config.ClientKeyPassword).IsEqualTo("secret");
        await Assert.That(config.ValidateServerCertificate).IsTrue();
    }

    [Test]
    public async Task CreateMutualTls_WithCertificateObjects_SetsCorrectProperties()
    {
        using var clientCert = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Client");
        using var caCert = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=CA");

        var config = TlsConfig.CreateMutualTls(clientCert, caCert);

        await Assert.That(config.ClientCertificate).IsEqualTo(clientCert);
        await Assert.That(config.CaCertificateObject).IsEqualTo(caCert);
        await Assert.That(config.ValidateServerCertificate).IsTrue();
    }

    [Test]
    public async Task CreateMutualTls_WithNullCaCertificate_AllowsNull()
    {
        using var clientCert = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Client");

        var config = TlsConfig.CreateMutualTls(clientCert, caCertificate: null);

        await Assert.That(config.ClientCertificate).IsEqualTo(clientCert);
        await Assert.That(config.CaCertificateObject).IsNull();
    }

    [Test]
    public async Task Config_CanSetCaCertificateCollection()
    {
        using var cert1 = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=CA1");
        using var cert2 = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=CA2");
        var collection = new X509Certificate2Collection { cert1, cert2 };

        var config = new TlsConfig
        {
            CaCertificateCollection = collection
        };

        await Assert.That(config.CaCertificateCollection).IsNotNull();
        await Assert.That(config.CaCertificateCollection!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Config_CanSetEnabledSslProtocols()
    {
        var config = new TlsConfig
        {
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        };

        await Assert.That(config.EnabledSslProtocols).IsEqualTo(SslProtocols.Tls12 | SslProtocols.Tls13);
    }

    [Test]
    public async Task Config_CanSetTargetHost()
    {
        var config = new TlsConfig
        {
            TargetHost = "kafka.example.com"
        };

        await Assert.That(config.TargetHost).IsEqualTo("kafka.example.com");
    }

    [Test]
    public async Task Config_CanDisableServerCertificateValidation()
    {
        var config = new TlsConfig
        {
            ValidateServerCertificate = false
        };

        await Assert.That(config.ValidateServerCertificate).IsFalse();
    }

    [Test]
    public async Task Config_CanEnableCertificateRevocationCheck()
    {
        var config = new TlsConfig
        {
            CheckCertificateRevocation = true
        };

        await Assert.That(config.CheckCertificateRevocation).IsTrue();
    }

    [Test]
    public async Task Config_AllCaCertificateTypesAreMutuallyExclusive()
    {
        // This test verifies that the three CA certificate properties can be set independently
        // Users should only use one of them

        // Path only
        var pathConfig = new TlsConfig { CaCertificatePath = "/path/to/ca.pem" };
        await Assert.That(pathConfig.CaCertificatePath).IsNotNull();
        await Assert.That(pathConfig.CaCertificateObject).IsNull();
        await Assert.That(pathConfig.CaCertificateCollection).IsNull();

        // Object only
        using var cert = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=CA");
        var objectConfig = new TlsConfig { CaCertificateObject = cert };
        await Assert.That(objectConfig.CaCertificatePath).IsNull();
        await Assert.That(objectConfig.CaCertificateObject).IsNotNull();
        await Assert.That(objectConfig.CaCertificateCollection).IsNull();

        // Collection only
        var collection = new X509Certificate2Collection { cert };
        var collectionConfig = new TlsConfig { CaCertificateCollection = collection };
        await Assert.That(collectionConfig.CaCertificatePath).IsNull();
        await Assert.That(collectionConfig.CaCertificateObject).IsNull();
        await Assert.That(collectionConfig.CaCertificateCollection).IsNotNull();
    }
}
