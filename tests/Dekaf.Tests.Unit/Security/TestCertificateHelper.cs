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
    /// Pre-generated RSA 2048-bit private key in PKCS#1 PEM format.
    /// This key has been verified to work for PEM and PFX export without ASN.1 errors.
    /// </summary>
    private const string FixedRsaPrivateKeyPem = """
        -----BEGIN RSA PRIVATE KEY-----
        MIIEowIBAAKCAQEAuj7vjWbLtZlYq3g811iOkBY3swengYBBx7v57PmgZNbeBH/e
        avJGGxz7+W0UU4yQoVa99RwDipsfoSLGDj41KOzKObQsVqvALTrIAueWGSJysb6s
        kD7CnDLPXc6lbiuEqDDMAXnwTQ2RhWWbPt2RPojMOPI4Z1nAo+TQxgzdDEWNzlbi
        VD77klyk7L3QwriS3D0al8TuxdfhGBmQhKYjbxtb8kZfV7SCEKlAtNFOrC5g5xHk
        n4HPX94MtsHFZlN4gAsmh93OZ8mra4wdtqLDvI9AsGiXsDydSt23oBi2HlWBtJ8P
        fpwuYQGshLj8TTF2TgadZTf2o4jKoRa/RPZEZwIDAQABAoIBAAzT/Of+ZpWRhFv7
        IiWrAdbG0PtR0aXH0cASIHrEDtojOpOQwx8WpOGFM43Qh4/hpKyYLulNDqljWeBd
        ZsrgWgUBmkQzNsKZfdkfrVsV3G7Kwp+fhH9C82CS11mcvCREdxSml8aaQYTtJFLN
        1s2TsUdfYMGjXWMw+WXQgtjBVhBcM8+ny/fBBUZqV76WsLnNKGuv9Ep6JHGFORd0
        bcvHW6nYrxKKwOWUvAal2KB4O0/eFKQgjRXGZ65IWPnjDdkCOLJeBJR60ZlUuBLV
        n0c+rdfSleR89lTYCUfQIYr58Irpyh37EgdxfLh5PsBdFmfW03Sqs5s6EYyM7NZ3
        O/WsfSECgYEA85s666xWbmfYrPlix5hJLCE8q67V/GQ6nOvX364d2Pi4yUr4Q8iK
        8EUawNVJu9hmfkPdbeOAhI5eflYUtkEFjvjqLAdjFhxldncnhXf4GWxbIcBFjyTq
        ZIysMGPPNLg5A/tA5lPwPtTL2WUiaa7OTocoYHtWIV8v7jIsCFWHfrkCgYEAw7ih
        8I5x9svw2O7LQH0973kUn8WFLr4TFH74dfVukzaB03aK0mGWI7HRAfQfzVlQ/Lal
        esyYEJemY7dxwMP7Wwhkhqq1IRcyrIeivsKVDYgZB0jmawIOYvIP4Ko5pzwDBocN
        oOoL/1DkFtw5ESX5QaqdzA9UsEFfBUz/rI/nTB8CgYBriQCvfDoDLrBFWykxtpXG
        dz2TA/DOI2iEUM/Qm8ntN45KvV9ufJ5ohfjTWtPbqiFEZ6zdj2nyGe64kkM+WOGd
        RWAJ45Dn980KSHsXvee1QVHRSlDqaX9Wt4pjKgwT16bDjSwPAMqy0bjS1IQmZtYH
        cD5wqMFSpfRAj8FERI01cQKBgBa+nhMWeqfzi0mqdnRIGap6p6rpiVClRhJbrwQG
        QZNaAjxQylEohgof3+oaNJfoiUDU+OYMYJ+NAAkWiGGeZNdvYj9EF0iBKaJjIMaK
        Vkf8SOxPzHcjBgj5mF7DaW/FyZQ4nZzVlg9VlywQ65DSmOTIrw3Huk/BSQmUqSGk
        l3yBAoGBAOazopZDSmaE4/vV+eMH6vSptkGKGSkg3fjZHRferxwstEGY5mZkbSMv
        +dnvErfSG8LooEW0cOZR67s/opAPhtySpb/MN/B0V1GodH+c8D8XCnxLpPdOf3Aw
        RSy+PZyYcqKJuLmFozxdk1Yu5FqQU4nX8VNvz48ivkPFLvmBBH8/
        -----END RSA PRIVATE KEY-----
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
        var hash = System.Security.Cryptography.SHA256.HashData(
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
