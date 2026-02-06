using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dekaf.Security;

namespace Dekaf.Tests.Unit.Security;

/// <summary>
/// Tests for certificate validation logic including custom CA validation.
/// These tests verify the certificate chain validation behavior by creating
/// test certificates and validating them against custom trust stores.
/// </summary>
[NotInParallel("CertificateGeneration")]
public class CertificateValidationTests
{
    [Test]
    public async Task SelfSignedCert_ValidatesAgainstItselfAsCA()
    {
        // Create a self-signed CA certificate
        using var caCert = TestCertificateHelper.CreateCaCertificate("CN=Test CA");

        // Create a certificate signed by the CA
        using var serverCert = TestCertificateHelper.CreateSignedCertificate("CN=Server", caCert);

        // Build chain with custom trust store
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(caCert);

        var result = chain.Build(serverCert);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CertificateChain_RejectsUntrustedRoot()
    {
        // Create an untrusted CA
        using var untrustedCa = TestCertificateHelper.CreateCaCertificate("CN=Untrusted CA");
        using var trustedCa = TestCertificateHelper.CreateCaCertificate("CN=Trusted CA");

        // Create a certificate signed by the untrusted CA
        using var serverCert = TestCertificateHelper.CreateSignedCertificate("CN=Server", untrustedCa);

        // Build chain with only the trusted CA in trust store
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(trustedCa);

        var result = chain.Build(serverCert);

        // Build will fail because the root is not trusted
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ChainStatus_IncludesUntrustedRootForSelfSigned()
    {
        // Create a self-signed certificate (not in any trust store)
        using var selfSignedCert = TestCertificateHelper.CreateCaCertificate("CN=Self Signed");

        // Build chain with empty custom trust store
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        // Empty custom trust store

        chain.Build(selfSignedCert);

        // Chain status should indicate untrusted root or partial chain
        // Note: macOS may return PartialChain instead of UntrustedRoot in this scenario
        var hasChainIssue = chain.ChainStatus.Any(s =>
            s.Status == X509ChainStatusFlags.UntrustedRoot ||
            s.Status == X509ChainStatusFlags.PartialChain);
        await Assert.That(hasChainIssue).IsTrue();
    }

    [Test]
    public async Task ChainStatus_DetectsExpiredCertificate()
    {
        // Create an expired certificate using the helper with custom dates
        using var expiredCert = TestCertificateHelper.CreateCaCertificateWithCustomDates("CN=Expired Cert",
            DateTimeOffset.UtcNow.AddYears(-2),
            DateTimeOffset.UtcNow.AddDays(-1));

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

        chain.Build(expiredCert);

        // Chain status should indicate the certificate is not time valid
        var hasNotTimeValid = chain.ChainStatus.Any(s => s.Status == X509ChainStatusFlags.NotTimeValid);
        await Assert.That(hasNotTimeValid).IsTrue();
    }

    [Test]
    public async Task ChainValidation_AllowsUntrustedRootInCustomTrustMode()
    {
        // Create a CA and signed certificate
        using var caCert = TestCertificateHelper.CreateCaCertificate("CN=Test CA");
        using var serverCert = TestCertificateHelper.CreateSignedCertificate("CN=Server", caCert);

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(caCert);

        var result = chain.Build(serverCert);

        await Assert.That(result).IsTrue();

        // Check that chain status only has acceptable statuses in custom trust mode
        // Different platforms may return different status flags:
        // - macOS may return PartialChain
        // - Windows may return RevocationStatusUnknown or OfflineRevocation even with NoCheck
        foreach (var status in chain.ChainStatus)
        {
            var isAcceptable = status.Status == X509ChainStatusFlags.NoError ||
                              status.Status == X509ChainStatusFlags.UntrustedRoot ||
                              status.Status == X509ChainStatusFlags.PartialChain ||
                              status.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                              status.Status == X509ChainStatusFlags.OfflineRevocation;
            await Assert.That(isAcceptable).IsTrue();
        }
    }

    [Test]
    public async Task ChainValidation_RejectsNotTimeValidEvenWithCustomCA()
    {
        // Create a CA certificate that spans a longer time period (starting 5 years ago)
        using var caCert = TestCertificateHelper.CreateCaCertificateWithCustomDates("CN=Test CA",
            DateTimeOffset.UtcNow.AddYears(-5),
            DateTimeOffset.UtcNow.AddYears(10));

        using var rsa = TestCertificateHelper.CreateRsaKey();
        var request = new CertificateRequest("CN=Expired Server", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Create an expired certificate (expired yesterday, but within the CA's validity period)
        using var expiredCert = request.Create(
            caCert,
            DateTimeOffset.UtcNow.AddYears(-2),
            DateTimeOffset.UtcNow.AddDays(-1),
            TestCertificateHelper.FixedSerialNumber);

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(caCert);

        chain.Build(expiredCert);

        // Should have NotTimeValid status
        var hasNotTimeValid = chain.ChainStatus.Any(s => s.Status == X509ChainStatusFlags.NotTimeValid);
        await Assert.That(hasNotTimeValid).IsTrue();
    }

    [Test]
    public async Task MultipleCAsInTrustStore_ValidatesCorrectly()
    {
        // Create multiple CAs
        using var ca1 = TestCertificateHelper.CreateCaCertificate("CN=CA1");
        using var ca2 = TestCertificateHelper.CreateCaCertificate("CN=CA2");
        using var ca3 = TestCertificateHelper.CreateCaCertificate("CN=CA3");

        // Create certificate signed by CA2
        using var serverCert = TestCertificateHelper.CreateSignedCertificate("CN=Server", ca2);

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca1);
        chain.ChainPolicy.CustomTrustStore.Add(ca2);
        chain.ChainPolicy.CustomTrustStore.Add(ca3);

        var result = chain.Build(serverCert);

        await Assert.That(result).IsTrue();

        // Verify the root certificate in the chain matches CA2
        var rootCert = chain.ChainElements[^1].Certificate;
        await Assert.That(rootCert.Thumbprint).IsEqualTo(ca2.Thumbprint);
    }

    [Test]
    public async Task ChainStatus_MustBeCheckedForNonRootErrors()
    {
        // This test demonstrates why checking ChainStatus is critical for security
        // Even if Build() returns true with AllowUnknownCertificateAuthority,
        // we must check for other errors like NotTimeValid, Revoked, etc.

        using var caCert = TestCertificateHelper.CreateCaCertificate("CN=Test CA");
        using var serverCert = TestCertificateHelper.CreateSignedCertificate("CN=Server", caCert);

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(caCert);

        var buildResult = chain.Build(serverCert);

        // Build succeeds
        await Assert.That(buildResult).IsTrue();

        // Verify there are no critical errors (only trust-related statuses and NoError are acceptable)
        // Different platforms may return different status flags:
        // - macOS may return PartialChain
        // - Windows may return RevocationStatusUnknown or OfflineRevocation even with NoCheck
        var hasSecurityIssue = chain.ChainStatus.Any(s =>
            s.Status != X509ChainStatusFlags.UntrustedRoot &&
            s.Status != X509ChainStatusFlags.PartialChain &&
            s.Status != X509ChainStatusFlags.NoError &&
            s.Status != X509ChainStatusFlags.RevocationStatusUnknown &&
            s.Status != X509ChainStatusFlags.OfflineRevocation);

        await Assert.That(hasSecurityIssue).IsFalse();
    }
}
