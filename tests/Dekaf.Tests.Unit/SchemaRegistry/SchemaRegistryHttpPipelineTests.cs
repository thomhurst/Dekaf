using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dekaf.SchemaRegistry;
using Dekaf.Tests.Unit.Security;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryHttpPipelineTests
{
    [Test]
    public async Task Client_CustomHandler_AppliesHeadersAndDefaultUserAgent()
    {
        var handler = new TrackingHandler();
        using var client = new SchemaRegistryClient(
            new SchemaRegistryConfig
            {
                Url = "http://schema-registry.local",
                DefaultHeaders = new Dictionary<string, string> { ["X-Tenant"] = "dekaf" }
            },
            handler);

        _ = await client.GetAllSubjectsAsync();

        await Assert.That(handler.LastRequest).IsNotNull();
        await Assert.That(handler.LastRequest!.Headers.GetValues("X-Tenant").Single()).IsEqualTo("dekaf");
        await Assert.That(handler.LastRequest.Headers.UserAgent.ToString()).StartsWith("Dekaf.SchemaRegistry/");
    }

    [Test]
    public async Task Client_CustomUserAgent_ReplacesDefault()
    {
        var handler = new TrackingHandler();
        using var client = new SchemaRegistryClient(
            new SchemaRegistryConfig
            {
                Url = "http://schema-registry.local",
                UserAgent = "enterprise-client/2.0"
            },
            handler);

        _ = await client.GetAllSubjectsAsync();

        await Assert.That(handler.LastRequest!.Headers.UserAgent.ToString()).IsEqualTo("enterprise-client/2.0");
    }

    [Test]
    public async Task Client_CallerOwnedHandler_IsNotDisposed()
    {
        var handler = new TrackingHandler();
        var client = new SchemaRegistryClient(NewConfig(), handler);

        client.Dispose();

        await Assert.That(handler.IsDisposed).IsFalse();
    }

    [Test]
    public async Task Client_CallerOwnedHttpClient_IsNotDisposed()
    {
        var handler = new TrackingHandler();
        using var httpClient = new HttpClient(handler, disposeHandler: true);
        var client = new SchemaRegistryClient(NewConfig(), httpClient);

        client.Dispose();
        using var response = await httpClient.GetAsync("http://schema-registry.local/subjects");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(handler.IsDisposed).IsFalse();
    }

    [Test]
    public async Task Client_FactoryHandler_IsDisposed()
    {
        var handler = new TrackingHandler();
        var client = new SchemaRegistryClient(NewConfig(), () => handler);

        client.Dispose();

        await Assert.That(handler.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Client_ConstructorFailure_DisposesOnlyFactoryOwnedHandler()
    {
        var factoryHandler = new TrackingHandler();
        var callerHandler = new TrackingHandler();
        var httpClientHandler = new TrackingHandler();
        using var httpClient = new HttpClient(httpClientHandler, disposeHandler: true);
        var invalidConfig = new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            DefaultHeaders = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
        };

        await Assert.That(() => new SchemaRegistryClient(invalidConfig, () => factoryHandler))
            .Throws<ArgumentException>();
        await Assert.That(() => new SchemaRegistryClient(invalidConfig, callerHandler))
            .Throws<ArgumentException>();
        await Assert.That(() => new SchemaRegistryClient(invalidConfig, httpClient))
            .Throws<ArgumentException>();

        await Assert.That(factoryHandler.IsDisposed).IsTrue();
        await Assert.That(callerHandler.IsDisposed).IsFalse();
        using var response = await httpClient.GetAsync("http://schema-registry.local/subjects");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Client_CustomHandlerFailure_Propagates()
    {
        using var client = new SchemaRegistryClient(NewConfig(), new ThrowingHandler());

        await Assert.That(SendAsync)
            .Throws<InvalidOperationException>()
            .WithMessage("handler failure");

        async Task SendAsync() => _ = await client.GetAllSubjectsAsync();
    }

    [Test]
    public async Task Client_InfiniteTimeout_RemainsSupported()
    {
        using var client = new SchemaRegistryClient(
            new SchemaRegistryConfig
            {
                Url = "http://schema-registry.local",
                RequestTimeoutMs = -1
            },
            new TrackingHandler());

        var subjects = await client.GetAllSubjectsAsync();

        await Assert.That(subjects).IsEmpty();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-2)]
    public async Task Config_InvalidTimeout_IsRejected(int requestTimeoutMs)
    {
        var config = new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            RequestTimeoutMs = requestTimeoutMs
        };

        await Assert.That(() => new SchemaRegistryClient(config, new TrackingHandler()))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Config_ContentHeader_IsRejected()
    {
        var config = new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            DefaultHeaders = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
        };

        await Assert.That(() => new SchemaRegistryClient(config, new TrackingHandler()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Config_AcceptHeader_IsRejected()
    {
        var config = new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            DefaultHeaders = new Dictionary<string, string> { ["Accept"] = "application/json" }
        };

        await Assert.That(() => new SchemaRegistryClient(config, new TrackingHandler()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Config_CustomPipeline_RejectsTlsAndProxySettings()
    {
        var tlsConfig = new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            Tls = new SchemaRegistryTlsConfig()
        };
        var proxyConfig = new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            UseProxy = false
        };

        await Assert.That(() => new SchemaRegistryClient(tlsConfig, new TrackingHandler()))
            .Throws<ArgumentException>();
        await Assert.That(() => new SchemaRegistryClient(proxyConfig, new TrackingHandler()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task DefaultPipeline_ConfiguresProxy()
    {
        var proxy = new WebProxy("http://proxy.local:8080");
        using var handler = SchemaRegistryClient.CreateConfiguredHttpHandler(new SchemaRegistryConfig
        {
            Url = "https://schema-registry.local",
            Proxy = proxy
        });

        var socketsHandler = UnwrapSocketsHandler(handler);
        await Assert.That(socketsHandler.UseProxy).IsTrue();
        await Assert.That(socketsHandler.Proxy).IsSameReferenceAs(proxy);
    }

    [Test]
    public async Task DefaultPipeline_WiresCertificateCallback()
    {
        RemoteCertificateValidationCallback callback = static (_, _, _, errors) => errors == SslPolicyErrors.None;
        using var handler = SchemaRegistryClient.CreateConfiguredHttpHandler(new SchemaRegistryConfig
        {
            Url = "https://schema-registry.local",
            Tls = new SchemaRegistryTlsConfig { RemoteCertificateValidationCallback = callback }
        });

        var socketsHandler = UnwrapSocketsHandler(handler);
        await Assert.That(socketsHandler.SslOptions.RemoteCertificateValidationCallback)
            .IsSameReferenceAs(callback);
    }

    private static SchemaRegistryConfig NewConfig() => new() { Url = "http://schema-registry.local" };

    private static SocketsHttpHandler UnwrapSocketsHandler(HttpMessageHandler handler) =>
        handler switch
        {
            SocketsHttpHandler socketsHandler => socketsHandler,
            DelegatingHandler delegatingHandler when delegatingHandler.InnerHandler is SocketsHttpHandler socketsHandler =>
                socketsHandler,
            _ => throw new InvalidOperationException($"Unexpected handler type {handler.GetType().Name}.")
        };

    private sealed class TrackingHandler : HttpMessageHandler
    {
        internal HttpRequestMessage? LastRequest { get; private set; }
        internal bool IsDisposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = disposing;
            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("handler failure");
    }
}

public sealed class SchemaRegistryCertificateMaterialTests
{
    [Test]
    public async Task Load_CaPem_LoadsEveryCertificate()
    {
        using var first = TestCertificateHelper.CreateCaCertificate("CN=First CA");
        using var second = TestCertificateHelper.CreateCaCertificate("CN=Second CA");
        using var material = SchemaRegistryCertificateMaterial.Load(new SchemaRegistryTlsConfig
        {
            CaCertificatePem = first.ExportCertificatePem() + Environment.NewLine + second.ExportCertificatePem()
        });

        await Assert.That(material.CaCertificates).IsNotNull();
        await Assert.That(material.CaCertificates!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Load_CaDirectory_UsesDeterministicFileOrder()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            using var first = TestCertificateHelper.CreateCaCertificate("CN=First CA");
            using var second = TestCertificateHelper.CreateCaCertificate("CN=Second CA");
            await File.WriteAllTextAsync(Path.Combine(directory, "b.pem"), second.ExportCertificatePem());
            await File.WriteAllTextAsync(Path.Combine(directory, "a.pem"), first.ExportCertificatePem());

            using var material = SchemaRegistryCertificateMaterial.Load(new SchemaRegistryTlsConfig
            {
                CaCertificatePath = directory
            });

            await Assert.That(material.CaCertificates![0].Thumbprint).IsEqualTo(first.Thumbprint);
            await Assert.That(material.CaCertificates[1].Thumbprint).IsEqualTo(second.Thumbprint);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Load_CrtFile_AcceptsDerEncoding()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            using var certificate = TestCertificateHelper.CreateCaCertificate("CN=DER CA");
            var path = Path.Combine(directory, "ca.crt");
            await File.WriteAllBytesAsync(path, certificate.RawData);

            using var material = SchemaRegistryCertificateMaterial.Load(new SchemaRegistryTlsConfig
            {
                CaCertificatePath = path
            });

            await Assert.That(material.CaCertificates![0].Thumbprint).IsEqualTo(certificate.Thumbprint);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Load_CaDirectory_RejectsEmptyAndMalformedDirectories()
    {
        var emptyDirectory = CreateTemporaryDirectory();
        var malformedDirectory = CreateTemporaryDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(malformedDirectory, "bad.pem"), "not a certificate");
            var emptyConfig = new SchemaRegistryTlsConfig { CaCertificatePath = emptyDirectory };
            var malformedConfig = new SchemaRegistryTlsConfig { CaCertificatePath = malformedDirectory };

            await Assert.That(() => SchemaRegistryCertificateMaterial.Load(emptyConfig))
                .Throws<ArgumentException>();
            await Assert.That(() => SchemaRegistryCertificateMaterial.Load(malformedConfig))
                .Throws<Exception>();
        }
        finally
        {
            Directory.Delete(emptyDirectory, recursive: true);
            Directory.Delete(malformedDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Load_CaDirectory_DoesNotIgnoreMalformedCandidateBesideValidCertificate()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            using var certificate = TestCertificateHelper.CreateCaCertificate("CN=Valid CA");
            await File.WriteAllTextAsync(Path.Combine(directory, "a.pem"), certificate.ExportCertificatePem());
            await File.WriteAllTextAsync(Path.Combine(directory, "b.pem"), "not a certificate");
            var config = new SchemaRegistryTlsConfig { CaCertificatePath = directory };

            await Assert.That(() => SchemaRegistryCertificateMaterial.Load(config))
                .Throws<ArgumentException>();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Load_ClientPem_LoadsUnencryptedAndEncryptedKeys()
    {
        const string password = "correct horse battery staple";
        using var certificate = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Client");
        using var rsa = certificate.GetRSAPrivateKey()!;
        var certificatePem = certificate.ExportCertificatePem();
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        var encryptedPrivateKeyPem = rsa.ExportEncryptedPkcs8PrivateKeyPem(
            password,
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10_000));

        using var unencrypted = SchemaRegistryCertificateMaterial.Load(new SchemaRegistryTlsConfig
        {
            ClientCertificatePem = certificatePem,
            ClientPrivateKeyPem = privateKeyPem
        });
        using var encrypted = SchemaRegistryCertificateMaterial.Load(new SchemaRegistryTlsConfig
        {
            ClientCertificatePem = certificatePem,
            ClientPrivateKeyPem = encryptedPrivateKeyPem,
            ClientCertificatePassword = password
        });

        await Assert.That(unencrypted.ClientCertificate!.HasPrivateKey).IsTrue();
        await Assert.That(encrypted.ClientCertificate!.HasPrivateKey).IsTrue();
    }

    [Test]
    public async Task Load_ClientPfx_ValidatesPassword()
    {
        const string password = "pfx-password";
        var path = Path.Combine(CreateTemporaryDirectory(), "client.pfx");
        try
        {
            using var certificate = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=PFX Client");
            await File.WriteAllBytesAsync(path, certificate.Export(X509ContentType.Pfx, password));

            using var material = SchemaRegistryCertificateMaterial.Load(new SchemaRegistryTlsConfig
            {
                ClientCertificatePath = path,
                ClientCertificatePassword = password
            });
            await Assert.That(material.ClientCertificate!.HasPrivateKey).IsTrue();
            await Assert.That(() => SchemaRegistryCertificateMaterial.Load(new SchemaRegistryTlsConfig
            {
                ClientCertificatePath = path,
                ClientCertificatePassword = "wrong-password"
            })).Throws<Exception>();
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Test]
    public async Task Load_ClientPfx_PreservesIntermediateCertificates()
    {
        const string password = "pfx-password";
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "client.pfx");
        try
        {
            using var root = TestCertificateHelper.CreateCaCertificate("CN=PFX Root");
            using var intermediate = TestCertificateHelper.CreateCaCertificate("CN=PFX Intermediate", root);
            using var publicIntermediate = TestCertificateHelper.LoadCertificate(intermediate.RawData);
            using var client = TestCertificateHelper.CreateSignedCertificate("CN=PFX Client", intermediate);
            var collection = new X509Certificate2Collection
            {
                client,
                publicIntermediate
            };
            await File.WriteAllBytesAsync(path, collection.Export(X509ContentType.Pkcs12, password)!);

            using var material = SchemaRegistryCertificateMaterial.Load(new SchemaRegistryTlsConfig
            {
                ClientCertificatePath = path,
                ClientCertificatePassword = password
            });

            await Assert.That(material.ClientCertificate!.Thumbprint).IsEqualTo(client.Thumbprint);
            await Assert.That(material.ClientIntermediateCertificates).IsNotNull();
            await Assert.That(material.ClientIntermediateCertificates!.Count).IsEqualTo(1);
            await Assert.That(material.ClientIntermediateCertificates[0].Thumbprint)
                .IsEqualTo(intermediate.Thumbprint);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Load_CaPassword_RejectsNonPkcs12File()
    {
        var config = new SchemaRegistryTlsConfig
        {
            CaCertificatePath = "ca.pem",
            CaCertificatePassword = "unused-password"
        };

        await Assert.That(() => SchemaRegistryCertificateMaterial.Load(config))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Load_CallerOwnedCertificates_AreNotDisposed()
    {
        using var ca = TestCertificateHelper.CreateCaCertificate("CN=Caller CA");
        using var client = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Caller Client");
        var material = SchemaRegistryCertificateMaterial.Load(new SchemaRegistryTlsConfig
        {
            CaCertificate = ca,
            ClientCertificate = client
        });

        material.Dispose();

        await Assert.That(ca.GetCertHash()).IsNotEmpty();
        await Assert.That(client.HasPrivateKey).IsTrue();
    }

    [Test]
    public async Task Load_LoadedCertificates_AreDisposedWithMaterial()
    {
        using var ca = TestCertificateHelper.CreateCaCertificate("CN=Loaded CA");
        using var client = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Loaded Client");
        using var clientKey = client.GetRSAPrivateKey()!;
        var material = SchemaRegistryCertificateMaterial.Load(new SchemaRegistryTlsConfig
        {
            CaCertificatePem = ca.ExportCertificatePem(),
            ClientCertificatePem = client.ExportCertificatePem(),
            ClientPrivateKeyPem = clientKey.ExportPkcs8PrivateKeyPem()
        });
        var loadedCa = material.CaCertificates![0];
        var loadedClient = material.ClientCertificate!;

        material.Dispose();

        await Assert.That(loadedCa.Handle).IsEqualTo(IntPtr.Zero);
        await Assert.That(loadedClient.Handle).IsEqualTo(IntPtr.Zero);
    }

    [Test]
    public async Task Load_ConflictsFailWithoutLeakingPassword()
    {
        const string secret = "never-print-this-password";
        using var certificate = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Client");
        var config = new SchemaRegistryTlsConfig
        {
            ClientCertificate = certificate,
            ClientCertificatePassword = secret
        };

        var exception = await Assert.That(() => SchemaRegistryCertificateMaterial.Load(config))
            .Throws<ArgumentException>();
        await Assert.That(exception!.Message).DoesNotContain(secret);
    }

    [Test]
    public async Task Load_RejectsConflictingAndIncompleteSources()
    {
        using var ca = TestCertificateHelper.CreateCaCertificate("CN=Conflict CA");
        using var client = TestCertificateHelper.CreateSelfSignedCertificateWithKey("CN=Conflict Client");
        SchemaRegistryTlsConfig[] invalidConfigurations =
        [
            new() { CaCertificate = ca, CaCertificatePem = ca.ExportCertificatePem() },
            new() { ClientCertificate = client, ClientCertificatePath = "client.pfx" },
            new() { CaCertificatePassword = "password-without-path" },
            new() { ClientPrivateKeyPem = "key-without-certificate" },
            new() { ClientCertificatePath = "client.pem" },
            new() { ClientCertificatePath = "client.pfx", ClientPrivateKeyPath = "client-key.pem" }
        ];

        foreach (var config in invalidConfigurations)
        {
            await Assert.That(() => SchemaRegistryCertificateMaterial.Load(config))
                .Throws<ArgumentException>();
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dekaf-schema-registry-tls-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}

public sealed class SchemaRegistryCertificateValidatorTests
{
    [Test]
    public async Task Validate_RequiresConfiguredRootAndPreservesHostnamePolicy()
    {
        using var trustedRoot = TestCertificateHelper.CreateCaCertificate("CN=Trusted Root");
        using var untrustedRoot = TestCertificateHelper.CreateCaCertificate("CN=Untrusted Root");
        using var server = TestCertificateHelper.CreateSignedCertificate("CN=schema-registry.local", trustedRoot);

        var trusted = new X509Certificate2Collection { trustedRoot };
        var untrusted = new X509Certificate2Collection { untrustedRoot };
        var chainErrors = SslPolicyErrors.RemoteCertificateChainErrors;
        var chainAndHostErrors = chainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;

        await Assert.That(SchemaRegistryCertificateValidator.Validate(
            server, null, chainErrors, trusted, validateHostName: true, checkCertificateRevocation: false)).IsTrue();
        await Assert.That(SchemaRegistryCertificateValidator.Validate(
            server, null, chainErrors, untrusted, validateHostName: true, checkCertificateRevocation: false)).IsFalse();
        await Assert.That(SchemaRegistryCertificateValidator.Validate(
            server, null, chainAndHostErrors, trusted, validateHostName: true, checkCertificateRevocation: false)).IsFalse();
        await Assert.That(SchemaRegistryCertificateValidator.Validate(
            server, null, chainAndHostErrors, trusted, validateHostName: false, checkCertificateRevocation: false)).IsTrue();
    }

    [Test]
    public async Task Validate_AcceptsConfiguredIntermediateAsTrustAnchor()
    {
        using var root = TestCertificateHelper.CreateCaCertificate("CN=Root");
        using var intermediate = TestCertificateHelper.CreateCaCertificate("CN=Intermediate", root);
        using var server = TestCertificateHelper.CreateSignedCertificate("CN=schema-registry.local", intermediate);
        var trusted = new X509Certificate2Collection { intermediate };

        var result = SchemaRegistryCertificateValidator.Validate(
            server,
            null,
            SslPolicyErrors.RemoteCertificateChainErrors,
            trusted,
            validateHostName: true,
            checkCertificateRevocation: false);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Validate_RejectsCertificateWithoutServerAuthenticationUsage()
    {
        using var root = TestCertificateHelper.CreateCaCertificate("CN=Root");
        using var clientOnlyCertificate = TestCertificateHelper.CreateSignedCertificate(
            "CN=schema-registry.local",
            root,
            "1.3.6.1.5.5.7.3.2");
        var trusted = new X509Certificate2Collection { root };

        var result = SchemaRegistryCertificateValidator.Validate(
            clientOnlyCertificate,
            null,
            SslPolicyErrors.RemoteCertificateChainErrors,
            trusted,
            validateHostName: true,
            checkCertificateRevocation: false);

        await Assert.That(result).IsFalse();
    }
}
