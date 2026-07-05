using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Dekaf.SchemaRegistry;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.SchemaRegistry;

public sealed class SchemaRegistryAuthenticationTests
{
    [Test]
    public async Task Client_UsesBasicAuth_WhenConfigured()
    {
        var handler = new CapturingSchemaRegistryHandler();
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            BasicAuthUserInfo = "user:secret"
        }, handler);

        _ = await client.GetAllSubjectsAsync();

        var authorization = handler.AuthorizationHeaders[0];
        await Assert.That(authorization?.Scheme).IsEqualTo("Basic");
        await Assert.That(authorization?.Parameter).IsEqualTo("dXNlcjpzZWNyZXQ=");
    }

    [Test]
    public async Task Client_UsesStaticBearerToken_WhenConfigured()
    {
        var handler = new CapturingSchemaRegistryHandler();
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            BearerAuthToken = "static-token"
        }, handler);

        _ = await client.GetAllSubjectsAsync();

        var authorization = handler.AuthorizationHeaders[0];
        await Assert.That(authorization?.Scheme).IsEqualTo("Bearer");
        await Assert.That(authorization?.Parameter).IsEqualTo("static-token");
    }

    [Test]
    public async Task Client_BearerToken_TakesPrecedenceOverBasicAuth()
    {
        var handler = new CapturingSchemaRegistryHandler();
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            BasicAuthUserInfo = "user:secret",
            BearerAuthToken = "static-token"
        }, handler);

        _ = await client.GetAllSubjectsAsync();

        var authorization = handler.AuthorizationHeaders[0];
        await Assert.That(authorization?.Scheme).IsEqualTo("Bearer");
        await Assert.That(authorization?.Parameter).IsEqualTo("static-token");
    }

    [Test]
    public async Task Client_UsesCustomBearerTokenProvider_WhenConfigured()
    {
        var providerCalls = 0;
        var handler = new CapturingSchemaRegistryHandler();
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            OAuthBearerTokenProvider = _ =>
            {
                providerCalls++;
                return ValueTask.FromResult(NewToken("provider-token"));
            }
        }, handler);

        _ = await client.GetAllSubjectsAsync();
        _ = await client.GetAllSubjectsAsync();

        await Assert.That(providerCalls).IsEqualTo(1);
        await Assert.That(handler.AuthorizationHeaders.Count).IsEqualTo(2);
        foreach (var authorization in handler.AuthorizationHeaders)
        {
            await Assert.That(authorization?.Scheme).IsEqualTo("Bearer");
            await Assert.That(authorization?.Parameter).IsEqualTo("provider-token");
        }
    }

    [Test]
    public async Task Client_UsesOAuthConfigProvider_WhenConfigured()
    {
        OAuthBearerConfig? capturedConfig = null;
        var handler = new CapturingSchemaRegistryHandler();
        var config = new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            OAuthBearerConfig = new OAuthBearerConfig
            {
                TokenEndpointUrl = "https://auth.local/token",
                ClientId = "schema-registry-client",
                ClientSecret = "secret"
            }
        };

        using var client = new SchemaRegistryClient(
            config,
            handler,
            oauthBearerTokenProviderFactory: oauthConfig =>
            {
                capturedConfig = oauthConfig;
                return _ => ValueTask.FromResult(NewToken("oidc-token"));
            });

        _ = await client.GetAllSubjectsAsync();

        await Assert.That(capturedConfig).IsSameReferenceAs(config.OAuthBearerConfig);
        var authorization = handler.AuthorizationHeaders[0];
        await Assert.That(authorization?.Scheme).IsEqualTo("Bearer");
        await Assert.That(authorization?.Parameter).IsEqualTo("oidc-token");
    }

    [Test]
    public async Task CreateHttpHandler_AddsClientCertificate()
    {
        using var certificate = CreateSelfSignedCertificate();
        using var handler = SchemaRegistryClient.CreateHttpHandler(certificate);

        var certificates = handler.SslOptions.ClientCertificates;
        await Assert.That(certificates).IsNotNull();
        await Assert.That(certificates!.Count).IsEqualTo(1);
        await Assert.That(certificates![0]).IsSameReferenceAs(certificate);
    }

    private static OAuthBearerToken NewToken(string tokenValue) => new()
    {
        TokenValue = tokenValue,
        Expiration = DateTimeOffset.UtcNow.AddHours(1),
        PrincipalName = "schema-registry"
    };

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=schema-registry-client",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(5));
    }

    private sealed class CapturingSchemaRegistryHandler : HttpMessageHandler
    {
        public List<AuthenticationHeaderValue?> AuthorizationHeaders { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationHeaders.Add(request.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        }
    }
}
