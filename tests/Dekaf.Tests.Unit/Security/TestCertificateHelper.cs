using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Dekaf.Tests.Unit.Security;

/// <summary>
/// Shared helper for TLS certificate tests.
/// Uses a fixed, pre-generated RSA 2048-bit key to eliminate randomness
/// that causes intermittent "first 9 bits" ASN.1 encoding failures
/// when RSA.Create(2048) produces parameters with leading zero bytes.
/// </summary>
internal static class TestCertificateHelper
{
    /// <summary>
    /// Pre-generated RSA 2048-bit private key in PKCS#8 PEM format.
    /// PKCS#8 stores full CRT parameters, avoiding OpenSSL "iqmp not inverse of q"
    /// validation errors that occur with some PKCS#1 keys on Linux.
    /// This key has been verified to work for PEM and PFX export on both Windows and Linux.
    /// </summary>
    private const string FixedRsaPrivateKeyPem = """
        -----BEGIN PRIVATE KEY-----
        MIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQDbEmbq4ecg/TGs
        VUWztR5dPrjbV1xQnQ+ixZZQPckmc8ywcEIJ8hEdaYVaORFkawZJfkuIDgpoDiwl
        W7f3sCUhIfs18fB9Bonna8HiRUY5EAle9eujSEKdn8dTWnTKRaRtbvbLH05qTrYR
        VQ09RZM30CMvW5Kc9cXo4z9DOnJJNltiiQi/fAbfOS+ES+dUER+R1eyrCRFghppL
        pcPO5CZXdQ+gV+XV2l+Zczsa9SRLlbSiZqgGcn6lx8S8g6oobco47Pf4enqRBYyi
        J4HgvUghZrI91O9m6QDNM+9bLLiOX2PaCXhviInNIb7ew+YGEx9qZHbVR/olmH+H
        m+pisBWJAgMBAAECggEAap23FsLgcG6o+Rz68i3IXEsFPkJy/AykKmyM7fpT5fHf
        gVLw4NQ9Pho3uyQg8cUgZy4e5lUm/WNAWuIbU2EXgNt/3c4kY0SGYulPj6Z1OZoz
        ZVK73lhxizLSmB9izXo9PsENPOe8iwJJm3/QFKzCrBwQs6CWZus56VCHXlmYe0Yh
        cbsTE5+hiwdxDNP9L1EkAqEvBY9HvmnTb55jBNXkDNh+NdkJmdpsiFDwNgQLLW0B
        KKKdsjTOk4GuAuaeZosDrIMHuzWdsv7G7LP2jWacOSkkKKf8OowsWeNQIXJFvRoL
        mmhWHJSu8LmvknnDZnvLGTyu1T31NWUPQBYZs5fNXQKBgQDl7VB0QHBBI7r7A2Q/
        hh+bpA2vCE7LfxNI0lxLN6yONuvdiAWne/owkLefhK4Z9Qm7kfnDP2o+Pv3saBUJ
        QLfh2/UmsbblegDZHeHJRxPh1FyYdMGvqYOaJh1st6latDGl4uKsELqV4rVYbBfS
        d/laRLmxD1+6PzTczabhERFf4wKBgQDz6ffLA3mQNR2+czQhXGJa4D+MCfna7JYh
        Myd/nKjlGg3nFg5MCW+BxYxy5NRHPPcix6kQf5PnToG5aF9eOJSjAuo4CzCSnvTj
        1uNpmBZWCIw8t7qnu5hQFxUEayIHnUKxcuDFu7P1lwZXymGb6Zt8sXKONyLtMZoS
        t3lmgc5YowKBgQDcqjppp6JUUedUmneuo5lYNUVQs6dzk9y9Ke6b3a3Eux74+F98
        0vZVf75K4Pp6PPp/QuSypvzfCnOGXIm73JndsM0ButMuPz3rIcuc8ZM6TCYlxwBQ
        B18fJO9edJGbVI7Fhw9GVbPMv8yNNQhT3QK5yHVyYa/cvmaMdu5u2IOVQwKBgQCs
        WvoUZMIz2rTH7VQ69rMxkCCXbj02K9PyZdlVXXgjXAPS9UzpAgnfY57ZWUV/iV8B
        HqEi3WPAIUOdplktlUHC5r5nF9Ec6mIV1bUg2q194dBm31VwTSlV/tmFI8cKJmAI
        UCrwzrBdrHh49LOAntSWijVutRtjDJfY/fk1LCiJjQKBgEeS2pSLRNrlIdb4qhzK
        +7xgFINesUruTJuSoezt2cbFWSP8gcQB7pYuiI+nDDU7ZIYgxA4WX8F63J5BH0VF
        yDoA3gQ+5+q90wZh1gNjSTOeccWzJOU7QyCdoPfraW0WUlXlc1hm3LGw0wJrWybz
        p/rS60ZgPDQeOJ8QDtM6YA7w
        -----END PRIVATE KEY-----
        """;

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
    /// Creates an RSA instance loaded with the fixed pre-generated key.
    /// Caller is responsible for disposing the returned instance.
    /// </summary>
    internal static RSA CreateRsaKey()
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(FixedRsaPrivateKeyPem);
        return rsa;
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
    /// Uses Create() with a fixed serial number instead of CreateSelfSigned()
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
    /// Uses Create() with a fixed serial number instead of CreateSelfSigned()
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
