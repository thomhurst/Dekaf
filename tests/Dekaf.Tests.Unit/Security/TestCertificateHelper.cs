using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Dekaf.Tests.Unit.Security;

/// <summary>
/// Shared helper for TLS certificate tests.
/// Uses RSA.Create(2048) to generate a fresh key per certificate creation.
/// Each call gets its own independent RSA key — no caching, no shared RSAParameters —
/// which avoids the OpenSSL "iqmp not inverse of q" validation errors that occur
/// when a single hard-coded key with invalid CRT parameters is reused on Linux.
/// </summary>
internal static class TestCertificateHelper
{
    /// <summary>
    /// Fixed serial number for certificate creation (used by tests that reference it directly).
    /// Starts with 0x01 to avoid leading-zero ASN.1 encoding issues.
    /// </summary>
    internal static readonly byte[] FixedSerialNumber = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];

    /// <summary>
    /// Generates a deterministic serial number derived from the subject name.
    /// Each unique subject produces a unique serial, avoiding chain validation
    /// confusion when multiple CAs share the same key material.
    /// The first byte is always 0x01 to avoid leading-zero ASN.1 encoding issues.
    /// </summary>
    private static byte[] DeriveSerialNumber(string subjectName)
    {
        var hash = SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(subjectName));
        var serial = new byte[9];
        serial[0] = 0x01; // Ensure no leading-zero ASN.1 issue
        hash.AsSpan(0, 8).CopyTo(serial.AsSpan(1));
        return serial;
    }

    /// <summary>
    /// Creates a fresh RSA 2048-bit key.
    /// Caller is responsible for disposing the returned instance.
    /// </summary>
    internal static RSA CreateRsaKey()
    {
        return RSA.Create(2048);
    }

    /// <summary>
    /// Creates a self-signed certificate without a private key attached (public cert only).
    /// Suitable for CA trust store tests and PEM export.
    /// Uses CreateSelfSigned then strips the private key to avoid the ASN.1 encoding
    /// bug in request.Create() + X509SignatureGenerator path.
    /// </summary>
    internal static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
    {
        using var withKey = CreateSelfSignedCertificateWithKey(subjectName);
        return X509CertificateLoader.LoadCertificate(withKey.RawData);
    }

    /// <summary>
    /// Creates a self-signed certificate with the private key attached.
    /// Suitable for PFX export and client certificate tests.
    /// Uses Create() with a derived serial number instead of CreateSelfSigned()
    /// because CreateSelfSigned generates a random serial number that can
    /// trigger ASN.1 "first 9 bits" encoding failures in .NET 10+.
    /// </summary>
    internal static X509Certificate2 CreateSelfSignedCertificateWithKey(string subjectName)
    {
        using var rsa = CreateRsaKey();
        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

        var notBefore = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var notAfter = new DateTimeOffset(2034, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var generator = X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1);
        using var cert = request.Create(
            new X500DistinguishedName(subjectName),
            generator,
            notBefore,
            notAfter,
            DeriveSerialNumber(subjectName));

        return cert.CopyWithPrivateKey(rsa);
    }

    /// <summary>
    /// Creates a CA certificate (BasicConstraints CA=true) with default validity dates.
    /// </summary>
    internal static X509Certificate2 CreateCaCertificate(string subjectName)
    {
        return CreateCaCertificateWithCustomDates(subjectName,
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2034, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// Creates a CA certificate with custom validity dates.
    /// Useful for expired certificate tests.
    /// Uses Create() with a derived serial number instead of CreateSelfSigned()
    /// because CreateSelfSigned generates a random serial number that can
    /// trigger ASN.1 "first 9 bits" encoding failures in .NET 10+.
    /// </summary>
    internal static X509Certificate2 CreateCaCertificateWithCustomDates(string subjectName,
        DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = CreateRsaKey();
        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                critical: true));

        var generator = X509SignatureGenerator.CreateForRSA(rsa, RSASignaturePadding.Pkcs1);
        using var cert = request.Create(
            new X500DistinguishedName(subjectName),
            generator,
            notBefore,
            notAfter,
            DeriveSerialNumber(subjectName));

        return cert.CopyWithPrivateKey(rsa);
    }

    /// <summary>
    /// Creates a leaf certificate signed by the given issuer CA.
    /// Returns a certificate with its private key attached.
    /// </summary>
    internal static X509Certificate2 CreateSignedCertificate(string subjectName, X509Certificate2 issuer)
    {
        using var rsa = CreateRsaKey();
        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        var notBefore = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var notAfter = new DateTimeOffset(2034, 1, 1, 0, 0, 0, TimeSpan.Zero);

        using var certWithoutKey = request.Create(
            issuer,
            notBefore,
            notAfter,
            DeriveSerialNumber(subjectName));

        return certWithoutKey.CopyWithPrivateKey(rsa);
    }
}
