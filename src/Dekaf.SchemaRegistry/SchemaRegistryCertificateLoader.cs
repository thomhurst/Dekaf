using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Dekaf.SchemaRegistry;

internal sealed class SchemaRegistryCertificateMaterial : IDisposable
{
    private static readonly string[] SupportedCaExtensions = [".cer", ".crt", ".p12", ".pem", ".pfx"];

    private readonly X509Certificate2Collection? _ownedCaCertificates;
    private readonly X509Certificate2Collection? _ownedClientCertificates;
    private int _disposed;

    private SchemaRegistryCertificateMaterial(
        X509Certificate2Collection? caCertificates,
        X509Certificate2? clientCertificate,
        X509Certificate2Collection? clientIntermediateCertificates,
        X509Certificate2Collection? ownedCaCertificates,
        X509Certificate2Collection? ownedClientCertificates)
    {
        CaCertificates = caCertificates;
        ClientCertificate = clientCertificate;
        ClientIntermediateCertificates = clientIntermediateCertificates;
        _ownedCaCertificates = ownedCaCertificates;
        _ownedClientCertificates = ownedClientCertificates;
    }

    internal X509Certificate2Collection? CaCertificates { get; }
    internal X509Certificate2? ClientCertificate { get; }
    internal X509Certificate2Collection? ClientIntermediateCertificates { get; }

    internal static SchemaRegistryCertificateMaterial Load(SchemaRegistryTlsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Validate(config);

        var caCertificates = LoadCaCertificates(config, out var ownsCaCertificates);
        X509Certificate2Collection? clientCertificates = null;
        var ownsClientCertificates = false;
        try
        {
            clientCertificates = LoadClientCertificates(config, out ownsClientCertificates);
            var clientCertificate = SelectClientCertificate(clientCertificates);
            var clientIntermediateCertificates = GetIntermediateCertificates(
                clientCertificates,
                clientCertificate);
            return new SchemaRegistryCertificateMaterial(
                caCertificates,
                clientCertificate,
                clientIntermediateCertificates,
                ownsCaCertificates ? caCertificates : null,
                ownsClientCertificates ? clientCertificates : null);
        }
        catch
        {
            if (ownsCaCertificates && caCertificates is not null)
                DisposeCertificates(caCertificates);
            if (ownsClientCertificates && clientCertificates is not null)
                DisposeCertificates(clientCertificates);
            throw;
        }
    }

    private static void Validate(SchemaRegistryTlsConfig config)
    {
        var caSourceCount = CountSet(
            config.CaCertificatePath,
            config.CaCertificatePem,
            config.CaCertificate,
            config.CaCertificates);
        if (caSourceCount > 1)
        {
            throw new ArgumentException(
                "Only one CA certificate source may be configured.",
                nameof(config));
        }

        var clientSourceCount = CountSet(
            config.ClientCertificate,
            config.ClientCertificatePath,
            config.ClientCertificatePem);
        if (clientSourceCount > 1)
        {
            throw new ArgumentException(
                "Only one client certificate source may be configured.",
                nameof(config));
        }

        if (config.CaCertificatePassword is not null)
        {
            if (config.CaCertificatePath is null)
                throw new ArgumentException("A CA certificate path is required when a CA password is configured.", nameof(config));

            if (!Directory.Exists(config.CaCertificatePath) && !IsPkcs12(config.CaCertificatePath))
            {
                throw new ArgumentException(
                    "A CA password can only be used with a PKCS#12 file or certificate directory.",
                    nameof(config));
            }
        }

        if (config.ClientCertificate is not null)
        {
            if (!config.ClientCertificate.HasPrivateKey)
                throw new ArgumentException("The client certificate must contain a private key.", nameof(config));

            if (config.ClientPrivateKeyPath is not null ||
                config.ClientPrivateKeyPem is not null ||
                config.ClientCertificatePassword is not null)
            {
                throw new ArgumentException(
                    "Private-key path, PEM, and password settings cannot be combined with an in-memory client certificate.",
                    nameof(config));
            }
        }

        if (config.ClientCertificatePath is not null)
        {
            var isPkcs12 = IsPkcs12(config.ClientCertificatePath);
            if (isPkcs12 && config.ClientPrivateKeyPath is not null)
                throw new ArgumentException("A separate private key cannot be combined with a PKCS#12 client certificate.", nameof(config));

            if (!isPkcs12 && config.ClientPrivateKeyPath is null)
                throw new ArgumentException("A private-key path is required for a PEM client certificate.", nameof(config));

            if (config.ClientPrivateKeyPem is not null)
                throw new ArgumentException("A private-key PEM string cannot be combined with a client certificate path.", nameof(config));
        }
        else if (config.ClientCertificatePem is not null)
        {
            if (config.ClientPrivateKeyPem is null)
                throw new ArgumentException("A private-key PEM string is required for a PEM client certificate.", nameof(config));

            if (config.ClientPrivateKeyPath is not null)
                throw new ArgumentException("A private-key path cannot be combined with a client certificate PEM string.", nameof(config));
        }
        else if (config.ClientPrivateKeyPath is not null ||
                 config.ClientPrivateKeyPem is not null ||
                 config.ClientCertificatePassword is not null)
        {
            throw new ArgumentException("A client certificate is required when private-key settings are configured.", nameof(config));
        }
    }

    private static int CountSet(
        object? first,
        object? second,
        object? third,
        object? fourth = null)
    {
        var count = 0;
        if (first is not null)
            count++;
        if (second is not null)
            count++;
        if (third is not null)
            count++;
        if (fourth is not null)
            count++;

        return count;
    }

    private static X509Certificate2Collection? LoadCaCertificates(
        SchemaRegistryTlsConfig config,
        out bool ownsCertificates)
    {
        ownsCertificates = false;
        if (config.CaCertificates is not null)
            return config.CaCertificates;

        if (config.CaCertificate is not null)
            return [config.CaCertificate];

        if (config.CaCertificatePem is not null)
        {
            var collection = new X509Certificate2Collection();
            try
            {
                collection.ImportFromPem(config.CaCertificatePem);
                EnsureNotEmpty(collection, "The CA certificate PEM string contains no certificates.");
                ownsCertificates = true;
                return collection;
            }
            catch
            {
                DisposeCertificates(collection);
                throw;
            }
        }

        if (config.CaCertificatePath is null)
            return null;

        var loaded = Directory.Exists(config.CaCertificatePath)
            ? LoadCaDirectory(config.CaCertificatePath, config.CaCertificatePassword)
            : LoadCaFile(config.CaCertificatePath, config.CaCertificatePassword);
        try
        {
            EnsureNotEmpty(loaded, "The configured CA certificate path contains no certificates.");
            ownsCertificates = true;
            return loaded;
        }
        catch
        {
            DisposeCertificates(loaded);
            throw;
        }
    }

    private static X509Certificate2Collection LoadCaDirectory(string path, string? password)
    {
        var files = new List<string>();
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
        {
            if (IsSupportedCaExtension(Path.GetExtension(file)))
                files.Add(file);
        }

        if (files.Count == 0)
        {
            throw new ArgumentException(
                "The CA certificate directory contains no supported certificate files.",
                nameof(path));
        }

        files.Sort(StringComparer.Ordinal);

        var certificates = new X509Certificate2Collection();
        try
        {
            foreach (var file in files)
            {
                var loaded = LoadCaFile(file, password);
                try
                {
                    while (loaded.Count > 0)
                    {
                        var certificate = loaded[0];
                        loaded.RemoveAt(0);
                        certificates.Add(certificate);
                    }
                }
                finally
                {
                    DisposeCertificates(loaded);
                }
            }

            return certificates;
        }
        catch
        {
            DisposeCertificates(certificates);
            throw;
        }
    }

    private static X509Certificate2Collection LoadCaFile(string path, string? password)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("CA certificate file was not found.", path);

        var extension = Path.GetExtension(path);
        if (!IsSupportedCaExtension(extension))
            throw new ArgumentException("Unsupported CA certificate file extension.", nameof(path));

        if (IsPkcs12(path))
        {
            var bytes = File.ReadAllBytes(path);
            try
            {
                return LoadPkcs12Collection(bytes, password);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        if (string.Equals(extension, ".cer", StringComparison.OrdinalIgnoreCase))
            return [LoadCertificateFromFile(path)];

        var collection = new X509Certificate2Collection();
        try
        {
            collection.ImportFromPemFile(path);
            if (collection.Count != 0)
                return collection;

            if (string.Equals(extension, ".crt", StringComparison.OrdinalIgnoreCase))
                return [LoadCertificateFromFile(path)];

            throw new ArgumentException("The CA certificate file contains no certificates.", nameof(path));
        }
        catch
        {
            DisposeCertificates(collection);
            throw;
        }
    }

    private static X509Certificate2Collection? LoadClientCertificates(
        SchemaRegistryTlsConfig config,
        out bool ownsCertificates)
    {
        ownsCertificates = false;
        if (config.ClientCertificate is not null)
            return [config.ClientCertificate];

        X509Certificate2? certificate = null;
        if (config.ClientCertificatePath is not null)
        {
            if (!File.Exists(config.ClientCertificatePath))
                throw new FileNotFoundException("Client certificate file was not found.", config.ClientCertificatePath);

            if (IsPkcs12(config.ClientCertificatePath))
            {
                var bytes = File.ReadAllBytes(config.ClientCertificatePath);
                try
                {
                    var certificates = LoadPkcs12Collection(bytes, config.ClientCertificatePassword);
                    try
                    {
                        PreparePkcs12ClientCertificates(certificates);
                        ownsCertificates = true;
                        return certificates;
                    }
                    catch
                    {
                        DisposeCertificates(certificates);
                        throw;
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bytes);
                }
            }
            else
            {
                if (!File.Exists(config.ClientPrivateKeyPath!))
                    throw new FileNotFoundException("Client private-key file was not found.", config.ClientPrivateKeyPath);

                certificate = string.IsNullOrEmpty(config.ClientCertificatePassword)
                    ? X509Certificate2.CreateFromPemFile(config.ClientCertificatePath, config.ClientPrivateKeyPath)
                    : X509Certificate2.CreateFromEncryptedPemFile(
                        config.ClientCertificatePath,
                        config.ClientCertificatePassword,
                        config.ClientPrivateKeyPath);
                certificate = PrepareWindowsClientCertificate(certificate);
            }
        }
        else if (config.ClientCertificatePem is not null)
        {
            certificate = string.IsNullOrEmpty(config.ClientCertificatePassword)
                ? X509Certificate2.CreateFromPem(config.ClientCertificatePem, config.ClientPrivateKeyPem)
                : X509Certificate2.CreateFromEncryptedPem(
                    config.ClientCertificatePem,
                    config.ClientPrivateKeyPem,
                    config.ClientCertificatePassword);
            certificate = PrepareWindowsClientCertificate(certificate);
        }

        if (certificate is null)
            return null;

        ownsCertificates = true;
        return [certificate];
    }

    private static X509Certificate2? SelectClientCertificate(X509Certificate2Collection? certificates)
    {
        X509Certificate2? selectedCertificate = null;
        if (certificates is null)
            return null;

        foreach (var certificate in certificates)
        {
            if (!certificate.HasPrivateKey)
                continue;

            if (selectedCertificate is not null)
                throw new ArgumentException("The client certificate source contains multiple certificates with private keys.");

            selectedCertificate = certificate;
        }

        return selectedCertificate ??
               throw new ArgumentException("The loaded client certificate does not contain a private key.");
    }

    private static X509Certificate2Collection? GetIntermediateCertificates(
        X509Certificate2Collection? certificates,
        X509Certificate2? clientCertificate)
    {
        if (certificates is null || clientCertificate is null || certificates.Count == 1)
            return null;

        var intermediateCertificates = new X509Certificate2Collection();
        foreach (var certificate in certificates)
        {
            if (!ReferenceEquals(certificate, clientCertificate))
                intermediateCertificates.Add(certificate);
        }

        return intermediateCertificates;
    }

    /// <summary>
    /// Re-imports a loaded client certificate through PKCS#12 so Windows Schannel receives
    /// a persisted key-provider handle. Ephemeral keys can otherwise fail credential
    /// acquisition with SEC_E_UNKNOWN_CREDENTIALS.
    /// </summary>
    private static X509Certificate2 PrepareWindowsClientCertificate(X509Certificate2 certificate)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return certificate;

        byte[]? pkcs12 = null;
        try
        {
            pkcs12 = certificate.Export(X509ContentType.Pfx);
            return LoadPkcs12(pkcs12, password: null);
        }
        finally
        {
            if (pkcs12 is not null)
                CryptographicOperations.ZeroMemory(pkcs12);
            certificate.Dispose();
        }
    }

    private static void PreparePkcs12ClientCertificates(X509Certificate2Collection certificates)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        for (var index = 0; index < certificates.Count; index++)
        {
            var certificate = certificates[index];
            if (!certificate.HasPrivateKey)
                continue;

            var preparedCertificate = PrepareWindowsClientCertificate(certificate);
            certificates.RemoveAt(index);
            try
            {
                certificates.Insert(index, preparedCertificate);
            }
            catch
            {
                preparedCertificate.Dispose();
                throw;
            }
        }
    }

    private static bool IsPkcs12(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".pfx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".p12", StringComparison.OrdinalIgnoreCase);
    }

    private static X509Certificate2Collection LoadPkcs12Collection(byte[] bytes, string? password)
    {
        var keyStorageFlags = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? X509KeyStorageFlags.Exportable
            : X509KeyStorageFlags.DefaultKeySet;
#if NET10_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12Collection(
            bytes,
            password,
            keyStorageFlags,
            Pkcs12LoaderLimits.Defaults);
#else
        var collection = new X509Certificate2Collection();
#pragma warning disable SYSLIB0057
        collection.Import(bytes, password, keyStorageFlags);
#pragma warning restore SYSLIB0057
        return collection;
#endif
    }

    private static X509Certificate2 LoadCertificateFromFile(string path)
    {
#if NET10_0_OR_GREATER
        return X509CertificateLoader.LoadCertificateFromFile(path);
#else
#pragma warning disable SYSLIB0057
        return new X509Certificate2(path);
#pragma warning restore SYSLIB0057
#endif
    }

    private static X509Certificate2 LoadCertificate(byte[] bytes)
    {
#if NET10_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(bytes);
#else
#pragma warning disable SYSLIB0057
        return new X509Certificate2(bytes);
#pragma warning restore SYSLIB0057
#endif
    }

    private static X509Certificate2 LoadPkcs12(byte[] bytes, string? password)
    {
#if NET10_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(bytes, password);
#else
#pragma warning disable SYSLIB0057
        return new X509Certificate2(bytes, password, X509KeyStorageFlags.DefaultKeySet);
#pragma warning restore SYSLIB0057
#endif
    }

    private static bool IsSupportedCaExtension(string extension)
    {
        foreach (var candidate in SupportedCaExtensions)
        {
            if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void EnsureNotEmpty(X509Certificate2Collection certificates, string message)
    {
        if (certificates.Count == 0)
            throw new ArgumentException(message);
    }

    private static void DisposeCertificates(X509Certificate2Collection certificates)
    {
        foreach (var certificate in certificates)
            certificate.Dispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_ownedCaCertificates is not null)
            DisposeCertificates(_ownedCaCertificates);

        if (_ownedClientCertificates is not null)
            DisposeCertificates(_ownedClientCertificates);
    }
}

internal static class SchemaRegistryCertificateValidator
{
    private static readonly Oid ServerAuthenticationOid = new("1.3.6.1.5.5.7.3.1");

    internal static bool Validate(
        X509Certificate? certificate,
        X509Chain? serverChain,
        SslPolicyErrors sslPolicyErrors,
        X509Certificate2Collection trustedCertificates,
        bool validateHostName,
        bool checkCertificateRevocation)
    {
        if (certificate is null)
            return false;

        if (!validateHostName)
            sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;

        if ((sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
            return false;

        X509Certificate2? ownedCertificate = null;
        try
        {
            var certificate2 = certificate as X509Certificate2 ??
                (ownedCertificate = new X509Certificate2(certificate));
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = checkCertificateRevocation
                ? X509RevocationMode.Online
                : X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            chain.ChainPolicy.ApplicationPolicy.Add(ServerAuthenticationOid);

            foreach (var trustedCertificate in trustedCertificates)
            {
                chain.ChainPolicy.CustomTrustStore.Add(trustedCertificate);
                chain.ChainPolicy.ExtraStore.Add(trustedCertificate);
            }

            if (serverChain is not null)
            {
                for (var index = 1; index < serverChain.ChainElements.Count; index++)
                    chain.ChainPolicy.ExtraStore.Add(serverChain.ChainElements[index].Certificate);
            }

            return trustedCertificates.Count > 0 &&
                   chain.Build(certificate2) &&
                   IsChainTerminatedByTrustedCertificate(chain, trustedCertificates);
        }
        finally
        {
            ownedCertificate?.Dispose();
        }
    }

    private static bool IsChainTerminatedByTrustedCertificate(
        X509Chain chain,
        X509Certificate2Collection trustedCertificates)
    {
        if (chain.ChainElements.Count == 0)
            return false;

        var terminalCertificate = chain.ChainElements[^1].Certificate;
        foreach (var trustedCertificate in trustedCertificates)
        {
            if (terminalCertificate.RawDataMemory.Span.SequenceEqual(trustedCertificate.RawDataMemory.Span))
                return true;
        }

        return false;
    }
}
