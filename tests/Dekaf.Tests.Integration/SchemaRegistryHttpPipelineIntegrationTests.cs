using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Integration;

[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistryHttpPipelineIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task CustomDelegatingHandler_SeesVersionedDefaultUserAgent()
    {
        using var handler = new CapturingDelegatingHandler(new SocketsHttpHandler());
        using var client = new SchemaRegistryClient(
            new SchemaRegistryConfig { Url = testInfra.RegistryUrl },
            handler);

        _ = await client.GetAllSubjectsAsync();

        await Assert.That(handler.UserAgent).StartsWith("Dekaf.SchemaRegistry/");
    }

    private sealed class CapturingDelegatingHandler(HttpMessageHandler innerHandler)
        : DelegatingHandler(innerHandler)
    {
        internal string? UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            UserAgent = request.Headers.UserAgent.ToString();
            return base.SendAsync(request, cancellationToken);
        }
    }
}

public sealed class SchemaRegistryTlsIntegrationTests
{
    [Test]
    public async Task CustomCa_AuthenticatesTrustedServer()
    {
        using var root = CreateCertificateAuthority("CN=Dekaf Schema Registry Test Root");
        using var serverCertificate = CreateLeafCertificate("CN=localhost", root, server: true);
        await using var server = new LocalTlsServer(serverCertificate, "localhost");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = server.Url,
            Tls = new SchemaRegistryTlsConfig { CaCertificate = root }
        });

        var subjects = await SendAsync(client, server);

        await Assert.That(subjects).IsEmpty();
        await server.Completion;
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CustomCaChain_AuthenticatesRootAndIntermediate(bool useDirectory)
    {
        using var root = CreateCertificateAuthority("CN=Chain Test Root");
        using var intermediate = CreateCertificateAuthority("CN=Chain Test Intermediate", root);
        using var serverCertificate = CreateLeafCertificate("CN=localhost", intermediate, server: true);
        var directory = CreateTemporaryDirectory();
        try
        {
            SchemaRegistryTlsConfig tls;
            if (useDirectory)
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "a-root.pem"), root.ExportCertificatePem());
                await File.WriteAllTextAsync(
                    Path.Combine(directory, "b-intermediate.pem"),
                    intermediate.ExportCertificatePem());
                tls = new SchemaRegistryTlsConfig { CaCertificatePath = directory };
            }
            else
            {
                tls = new SchemaRegistryTlsConfig
                {
                    CaCertificatePem = root.ExportCertificatePem() + Environment.NewLine +
                                       intermediate.ExportCertificatePem()
                };
            }

            await using var server = new LocalTlsServer(serverCertificate, "localhost");
            using var client = new SchemaRegistryClient(new SchemaRegistryConfig
            {
                Url = server.Url,
                Tls = tls
            });

            var subjects = await SendAsync(client, server);

            await Assert.That(subjects).IsEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task CustomCa_RejectsUntrustedServer()
    {
        using var trustedRoot = CreateCertificateAuthority("CN=Trusted Test Root");
        using var untrustedRoot = CreateCertificateAuthority("CN=Untrusted Test Root");
        using var serverCertificate = CreateLeafCertificate("CN=localhost", untrustedRoot, server: true);
        await using var server = new LocalTlsServer(serverCertificate, "localhost");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = server.Url,
            Tls = new SchemaRegistryTlsConfig { CaCertificate = trustedRoot }
        });

        Func<Task> request = async () => _ = await client.GetAllSubjectsAsync();
        await Assert.That(request).Throws<HttpRequestException>();
    }

    [Test]
    public async Task HostNameValidation_CanBeDisabledExplicitly()
    {
        using var root = CreateCertificateAuthority("CN=Hostname Test Root");
        using var serverCertificate = CreateLeafCertificate("CN=localhost", root, server: true);
        await using var server = new LocalTlsServer(serverCertificate, "127.0.0.1");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = server.Url,
            Tls = new SchemaRegistryTlsConfig
            {
                CaCertificate = root,
                ValidateServerCertificateHostName = false
            }
        });

        var subjects = await SendAsync(client, server);

        await Assert.That(subjects).IsEmpty();
        await server.Completion;
    }

    [Test]
    public async Task HostNameValidation_RejectsMismatchByDefault()
    {
        using var root = CreateCertificateAuthority("CN=Hostname Rejection Test Root");
        using var serverCertificate = CreateLeafCertificate("CN=localhost", root, server: true);
        await using var server = new LocalTlsServer(serverCertificate, "127.0.0.1");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = server.Url,
            Tls = new SchemaRegistryTlsConfig { CaCertificate = root }
        });

        Func<Task> request = async () => _ = await client.GetAllSubjectsAsync();
        await Assert.That(request).Throws<HttpRequestException>();
    }

    [Test]
    [Arguments(ClientCredentialSource.InMemory)]
    [Arguments(ClientCredentialSource.PemFiles)]
    [Arguments(ClientCredentialSource.PemStrings)]
    [Arguments(ClientCredentialSource.Pfx)]
    public async Task MutualTls_PresentsConfiguredClientCertificate(ClientCredentialSource source)
    {
        const string password = "schema-registry-test-password";
        using var root = CreateCertificateAuthority($"CN={source} Mutual TLS Test Root");
        using var serverCertificate = CreateLeafCertificate("CN=localhost", root, server: true);
        using var clientCertificate = CreateLeafCertificate(
            $"CN=Dekaf {source} Test Client",
            root,
            server: false,
            out var clientPrivateKeyPem);
        var directory = CreateTemporaryDirectory();
        try
        {
            var tls = await CreateClientTlsConfigAsync(
                source,
                root,
                clientCertificate,
                clientPrivateKeyPem,
                directory,
                password);
            await using var server = new LocalTlsServer(
                serverCertificate,
                "localhost",
                requireClientCertificate: true,
                trustedClientRoot: root);
            using var client = new SchemaRegistryClient(new SchemaRegistryConfig
            {
                Url = server.Url,
                Tls = tls
            });

            _ = await SendAsync(client, server);

            await Assert.That(server.SawClientCertificate).IsTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task CustomIntermediateCa_AuthenticatesAsExplicitTrustAnchor()
    {
        using var root = CreateCertificateAuthority("CN=Intermediate Anchor Root");
        using var intermediate = CreateCertificateAuthority("CN=Explicit Intermediate Anchor", root);
        using var serverCertificate = CreateLeafCertificate("CN=localhost", intermediate, server: true);
        await using var server = new LocalTlsServer(serverCertificate, "localhost");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = server.Url,
            Tls = new SchemaRegistryTlsConfig { CaCertificate = intermediate }
        });

        var subjects = await SendAsync(client, server);

        await Assert.That(subjects).IsEmpty();
        await server.Completion;
    }

    [Test]
    public async Task MutualTls_PfxPresentsIntermediateCertificateChain()
    {
        const string password = "schema-registry-test-password";
        using var root = CreateCertificateAuthority("CN=Mutual TLS Chain Root");
        using var intermediate = CreateCertificateAuthority("CN=Mutual TLS Chain Intermediate", root);
        using var publicIntermediate = LoadCertificate(intermediate.RawData);
        using var serverCertificate = CreateLeafCertificate("CN=localhost", root, server: true);
        using var clientCertificate = CreateLeafCertificate("CN=Chained Client", intermediate, server: false);
        var directory = CreateTemporaryDirectory();
        var pfxPath = Path.Combine(directory, "client-chain.pfx");
        try
        {
            var collection = new X509Certificate2Collection
            {
                clientCertificate,
                publicIntermediate
            };
            await File.WriteAllBytesAsync(pfxPath, collection.Export(X509ContentType.Pkcs12, password)!);
            await using var server = new LocalTlsServer(
                serverCertificate,
                "localhost",
                requireClientCertificate: true,
                trustedClientRoot: root);
            using var client = new SchemaRegistryClient(new SchemaRegistryConfig
            {
                Url = server.Url,
                Tls = new SchemaRegistryTlsConfig
                {
                    CaCertificate = root,
                    ClientCertificatePath = pfxPath,
                    ClientCertificatePassword = password
                }
            });

            _ = await SendAsync(client, server);

            await Assert.That(server.SawClientCertificate).IsTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task MutualTls_RejectsMissingClientCertificate()
    {
        using var root = CreateCertificateAuthority("CN=Required Mutual TLS Test Root");
        using var serverCertificate = CreateLeafCertificate("CN=localhost", root, server: true);
        await using var server = new LocalTlsServer(
            serverCertificate,
            "localhost",
            requireClientCertificate: true,
            trustedClientRoot: root);
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = server.Url,
            Tls = new SchemaRegistryTlsConfig { CaCertificate = root }
        });

        Func<Task> request = async () => _ = await client.GetAllSubjectsAsync();
        await Assert.That(request).Throws<HttpRequestException>();
    }

    [Test]
    public async Task MutualTls_RejectsUntrustedClientCertificate()
    {
        using var trustedRoot = CreateCertificateAuthority("CN=Trusted Mutual TLS Root");
        using var untrustedRoot = CreateCertificateAuthority("CN=Untrusted Mutual TLS Root");
        using var serverCertificate = CreateLeafCertificate("CN=localhost", trustedRoot, server: true);
        using var clientCertificate = CreateLeafCertificate("CN=Untrusted Client", untrustedRoot, server: false);
        await using var server = new LocalTlsServer(
            serverCertificate,
            "localhost",
            requireClientCertificate: true,
            trustedClientRoot: trustedRoot);
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = server.Url,
            Tls = new SchemaRegistryTlsConfig
            {
                CaCertificate = trustedRoot,
                ClientCertificate = clientCertificate
            }
        });

        Func<Task> request = async () => _ = await client.GetAllSubjectsAsync();
        await Assert.That(request).Throws<HttpRequestException>();
    }

    [Test]
    public async Task CertificateFileRotation_NewClientUsesUpdatedTrust()
    {
        using var firstRoot = CreateCertificateAuthority("CN=Rotation First Root");
        using var secondRoot = CreateCertificateAuthority("CN=Rotation Second Root");
        using var firstServerCertificate = CreateLeafCertificate("CN=localhost", firstRoot, server: true);
        using var secondServerCertificate = CreateLeafCertificate("CN=localhost", secondRoot, server: true);
        var directory = CreateTemporaryDirectory();
        var caPath = Path.Combine(directory, "ca.pem");
        try
        {
            await File.WriteAllTextAsync(caPath, firstRoot.ExportCertificatePem());
            await using (var server = new LocalTlsServer(firstServerCertificate, "localhost"))
            using (var client = CreateFileTrustClient(server.Url, caPath))
                _ = await SendAsync(client, server);

            await File.WriteAllTextAsync(caPath, secondRoot.ExportCertificatePem());
            await using (var server = new LocalTlsServer(secondServerCertificate, "localhost"))
            using (var client = CreateFileTrustClient(server.Url, caPath))
                _ = await SendAsync(client, server);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Failover_PreservesTlsAndAuthentication()
    {
        using var root = CreateCertificateAuthority("CN=Failover Test Root");
        using var serverCertificate = CreateLeafCertificate("CN=localhost", root, server: true);
        await using var rejectingEndpoint = new RejectingTlsEndpoint();
        await using var server = new LocalTlsServer(serverCertificate, "localhost");
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = rejectingEndpoint.Url,
            Urls = [rejectingEndpoint.Url, server.Url],
            BasicAuthUserInfo = "user:pass",
            RequestTimeoutMs = 5_000,
            Tls = new SchemaRegistryTlsConfig { CaCertificate = root }
        });

        _ = await SendAsync(client, server);

        await Assert.That(server.RequestText).Contains("Authorization: Basic dXNlcjpwYXNz");
    }

    private static SchemaRegistryClient CreateFileTrustClient(string url, string caPath) =>
        new(new SchemaRegistryConfig
        {
            Url = url,
            Tls = new SchemaRegistryTlsConfig { CaCertificatePath = caPath }
        });

    private static async Task<SchemaRegistryTlsConfig> CreateClientTlsConfigAsync(
        ClientCredentialSource source,
        X509Certificate2 root,
        X509Certificate2 clientCertificate,
        string clientPrivateKeyPem,
        string directory,
        string password)
    {
        switch (source)
        {
            case ClientCredentialSource.InMemory:
                return new SchemaRegistryTlsConfig
                {
                    CaCertificate = root,
                    ClientCertificate = clientCertificate
                };
            case ClientCredentialSource.PemFiles:
                var certificatePath = Path.Combine(directory, "client.pem");
                var keyPath = Path.Combine(directory, "client-key.pem");
                await File.WriteAllTextAsync(certificatePath, clientCertificate.ExportCertificatePem());
                await File.WriteAllTextAsync(keyPath, clientPrivateKeyPem);
                return new SchemaRegistryTlsConfig
                {
                    CaCertificate = root,
                    ClientCertificatePath = certificatePath,
                    ClientPrivateKeyPath = keyPath
                };
            case ClientCredentialSource.PemStrings:
                return new SchemaRegistryTlsConfig
                {
                    CaCertificate = root,
                    ClientCertificatePem = clientCertificate.ExportCertificatePem(),
                    ClientPrivateKeyPem = clientPrivateKeyPem
                };
            case ClientCredentialSource.Pfx:
                var pfxPath = Path.Combine(directory, "client.pfx");
                await File.WriteAllBytesAsync(pfxPath, clientCertificate.Export(X509ContentType.Pfx, password));
                return new SchemaRegistryTlsConfig
                {
                    CaCertificate = root,
                    ClientCertificatePath = pfxPath,
                    ClientCertificatePassword = password
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private static async Task<IReadOnlyList<string>> SendAsync(
        SchemaRegistryClient client,
        LocalTlsServer server)
    {
        var request = client.GetAllSubjectsAsync();
        if (await Task.WhenAny(request, server.Completion) == server.Completion && server.Completion.IsFaulted)
            await server.Completion;

        return await request;
    }

    private static X509Certificate2 CreateCertificateAuthority(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 CreateCertificateAuthority(string subject, X509Certificate2 issuer)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var certificate = request.Create(
            issuer,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddDays(14),
            CreateSerialNumber());
        return AttachPlatformSafePrivateKey(certificate, key);
    }

    private static X509Certificate2 CreateLeafCertificate(
        string subject,
        X509Certificate2 issuer,
        bool server) =>
        CreateLeafCertificateCore(subject, issuer, server, exportPrivateKey: false, out _);

    private static X509Certificate2 CreateLeafCertificate(
        string subject,
        X509Certificate2 issuer,
        bool server,
        out string privateKeyPem)
    {
        var certificate = CreateLeafCertificateCore(
            subject,
            issuer,
            server,
            exportPrivateKey: true,
            out var exportedPrivateKeyPem);
        privateKeyPem = exportedPrivateKeyPem!;
        return certificate;
    }

    private static X509Certificate2 CreateLeafCertificateCore(
        string subject,
        X509Certificate2 issuer,
        bool server,
        bool exportPrivateKey,
        out string? privateKeyPem)
    {
        using var key = RSA.Create(2048);
        privateKeyPem = exportPrivateKey ? key.ExportPkcs8PrivateKeyPem() : null;
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid(server ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2")],
            true));

        if (server)
        {
            var subjectAlternativeName = new SubjectAlternativeNameBuilder();
            subjectAlternativeName.AddDnsName("localhost");
            request.CertificateExtensions.Add(subjectAlternativeName.Build());
        }

        using var certificate = request.Create(
            issuer,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddDays(7),
            CreateSerialNumber());
        return AttachPlatformSafePrivateKey(certificate, key);
    }

    private static byte[] CreateSerialNumber()
    {
        var serialNumber = RandomNumberGenerator.GetBytes(16);
        serialNumber[0] &= 0x7f;
        serialNumber[0] |= 0x01;
        return serialNumber;
    }

    private static X509Certificate2 AttachPlatformSafePrivateKey(X509Certificate2 certificate, RSA key)
    {
        using var certificateWithKey = certificate.CopyWithPrivateKey(key);
        var pfx = certificateWithKey.Export(X509ContentType.Pfx);
        try
        {
            const X509KeyStorageFlags flags = X509KeyStorageFlags.Exportable |
                                              X509KeyStorageFlags.PersistKeySet |
                                              X509KeyStorageFlags.UserKeySet;
#if NET10_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12(pfx, password: null, flags);
#else
            return new X509Certificate2(pfx, (string?)null, flags);
#endif
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }

    private static X509Certificate2 LoadCertificate(byte[] rawData)
    {
#if NET10_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(rawData);
#else
#pragma warning disable SYSLIB0057
        return new X509Certificate2(rawData);
#pragma warning restore SYSLIB0057
#endif
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dekaf-schema-registry-tls-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public enum ClientCredentialSource
    {
        InMemory,
        PemFiles,
        PemStrings,
        Pfx
    }

    private sealed class LocalTlsServer : IAsyncDisposable
    {
        private static readonly byte[] Response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n[]");

        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stopping = new();
        private readonly X509Certificate2 _serverCertificate;
        private readonly bool _requireClientCertificate;
        private readonly X509Certificate2? _trustedClientRoot;

        internal LocalTlsServer(
            X509Certificate2 serverCertificate,
            string urlHost,
            bool requireClientCertificate = false,
            X509Certificate2? trustedClientRoot = null)
        {
            _serverCertificate = serverCertificate;
            _requireClientCertificate = requireClientCertificate;
            _trustedClientRoot = trustedClientRoot;
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"https://{urlHost}:{port}";
            Completion = ServeAsync();
        }

        internal string Url { get; }
        internal Task Completion { get; }
        internal bool SawClientCertificate { get; private set; }
        internal string? RequestText { get; private set; }

        private async Task ServeAsync()
        {
            using var tcpClient = await _listener.AcceptTcpClientAsync(_stopping.Token);
            await using var stream = new SslStream(
                tcpClient.GetStream(),
                leaveInnerStreamOpen: false,
                (_, certificate, chain, _) => ValidateClientCertificate(certificate, chain));
            await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = _serverCertificate,
                ClientCertificateRequired = _requireClientCertificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, _stopping.Token);

            var buffer = new byte[4096];
            var received = 0;
            while (received < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(received), _stopping.Token);
                if (read == 0)
                    return;

                received += read;
                if (ContainsHeaderTerminator(buffer.AsSpan(0, received)))
                    break;
            }

            RequestText = Encoding.ASCII.GetString(buffer, 0, received);
            await stream.WriteAsync(Response, _stopping.Token);
            await stream.FlushAsync(_stopping.Token);
        }

        private static bool ContainsHeaderTerminator(ReadOnlySpan<byte> bytes) =>
            bytes.IndexOf("\r\n\r\n"u8) >= 0;

        private bool ValidateClientCertificate(X509Certificate? certificate, X509Chain? presentedChain)
        {
            SawClientCertificate = certificate is not null;
            if (!_requireClientCertificate)
                return true;

            if (certificate is null || _trustedClientRoot is null)
                return false;

            X509Certificate2? ownedCertificate = null;
            try
            {
                var certificate2 = certificate as X509Certificate2 ??
                    (ownedCertificate = new X509Certificate2(certificate));
                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(_trustedClientRoot);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.2"));
                if (presentedChain is not null)
                {
                    for (var index = 1; index < presentedChain.ChainElements.Count; index++)
                        chain.ChainPolicy.ExtraStore.Add(presentedChain.ChainElements[index].Certificate);
                }

                return chain.Build(certificate2);
            }
            finally
            {
                ownedCertificate?.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stopping.CancelAsync();
            _listener.Stop();
            try
            {
                await Completion;
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or AuthenticationException)
            {
            }
            finally
            {
                _stopping.Dispose();
            }
        }
    }

    private sealed class RejectingTlsEndpoint : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stopping = new();
        private readonly Task _completion;

        internal RejectingTlsEndpoint()
        {
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"https://localhost:{port}";
            _completion = RejectConnectionsAsync();
        }

        internal string Url { get; }

        private async Task RejectConnectionsAsync()
        {
            while (!_stopping.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stopping.CancelAsync();
            _listener.Stop();
            try
            {
                await _completion;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _stopping.Dispose();
            }
        }
    }
}
