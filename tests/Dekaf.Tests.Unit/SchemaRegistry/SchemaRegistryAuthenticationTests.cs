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
    public async Task Client_CustomBearerTokenProvider_TakesPrecedenceOverStaticBearerToken()
    {
        var handler = new CapturingSchemaRegistryHandler();
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            BearerAuthToken = "static-token",
            OAuthBearerTokenProvider = _ => ValueTask.FromResult(NewToken("provider-token"))
        }, handler);

        _ = await client.GetAllSubjectsAsync();

        var authorization = handler.AuthorizationHeaders[0];
        await Assert.That(authorization?.Scheme).IsEqualTo("Bearer");
        await Assert.That(authorization?.Parameter).IsEqualTo("provider-token");
    }

    [Test]
    public async Task Client_BearerToken_TakesPrecedenceOverOAuthConfig()
    {
        OAuthBearerConfig? capturedConfig = null;
        var handler = new CapturingSchemaRegistryHandler();
        using var client = new SchemaRegistryClient(
            new SchemaRegistryConfig
            {
                Url = "http://schema-registry.local",
                BearerAuthToken = "static-token",
                OAuthBearerConfig = NewOAuthConfig()
            },
            handler,
            oauthBearerTokenProviderFactory: oauthConfig =>
            {
                capturedConfig = oauthConfig;
                return _ => ValueTask.FromResult(NewToken("oidc-token"));
            });

        _ = await client.GetAllSubjectsAsync();

        await Assert.That(capturedConfig).IsNull();
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
            OAuthBearerConfig = NewOAuthConfig()
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
    public async Task Client_OAuthConfig_TakesPrecedenceOverBasicAuth()
    {
        var handler = new CapturingSchemaRegistryHandler();
        using var client = new SchemaRegistryClient(
            new SchemaRegistryConfig
            {
                Url = "http://schema-registry.local",
                BasicAuthUserInfo = "user:secret",
                OAuthBearerConfig = NewOAuthConfig()
            },
            handler,
            oauthBearerTokenProviderFactory: _ => _ => ValueTask.FromResult(NewToken("oidc-token")));

        _ = await client.GetAllSubjectsAsync();

        var authorization = handler.AuthorizationHeaders[0];
        await Assert.That(authorization?.Scheme).IsEqualTo("Bearer");
        await Assert.That(authorization?.Parameter).IsEqualTo("oidc-token");
    }

    [Test]
    [Arguments(300, 120, 2)]
    [Arguments(300, 600, 1)]
    [Arguments(null, 120, 1)]
    [Arguments(null, 30, 2)]
    [Arguments(10, 30, 1)]
    [Arguments(10, 0, 2)]
    public async Task Client_OAuthConfig_HonorsProviderRefreshBuffer(
        int? refreshBufferSeconds, int expiresInSeconds, int expectedTokenRequests)
    {
        var config = new OAuthBearerConfig
        {
            TokenEndpointUrl = "https://auth.local/token",
            ClientId = "schema-registry-client",
            ClientSecret = "secret",
            TokenRefreshBufferSeconds = refreshBufferSeconds ?? NewOAuthConfig().TokenRefreshBufferSeconds
        };
        using var tokenEndpoint = new TokenEndpointHandler(expiresInSeconds);
        using var tokenHttpClient = new HttpClient(tokenEndpoint);
        using var tokenProvider = new OAuthBearerTokenProvider(config, tokenHttpClient);
        var registry = new CapturingSchemaRegistryHandler();
        using var client = new SchemaRegistryClient(
            new SchemaRegistryConfig { Url = "http://schema-registry.local", OAuthBearerConfig = config },
            registry,
            oauthBearerTokenProviderFactory: _ => tokenProvider.GetTokenAsync);

        _ = await client.GetAllSubjectsAsync();
        _ = await client.GetAllSubjectsAsync();

        await Assert.That(tokenEndpoint.RequestCount).IsEqualTo(expectedTokenRequests);
        await Assert.That(registry.AuthorizationHeaders.Count).IsEqualTo(2);
        await Assert.That(registry.AuthorizationHeaders[0]?.Parameter).IsEqualTo("token-1");
        await Assert.That(registry.AuthorizationHeaders[1]?.Parameter).IsEqualTo($"token-{expectedTokenRequests}");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task Client_WaitsForOAuthToken_AndObservesCancellation(bool configuredProvider, bool cancel)
    {
        var pendingToken = new TaskCompletionSource<OAuthBearerToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        ValueTask<OAuthBearerToken> GetToken(CancellationToken token)
        {
            requested.SetResult();
            return new(pendingToken.Task.WaitAsync(token));
        }

        var registry = new CapturingSchemaRegistryHandler();
        using var client = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://schema-registry.local",
            OAuthBearerConfig = configuredProvider ? NewOAuthConfig() : null,
            OAuthBearerTokenProvider = configuredProvider ? null : GetToken
        }, registry, oauthBearerTokenProviderFactory: _ => GetToken);

        var request = client.GetAllSubjectsAsync(cancellation.Token);
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(request.IsCompleted).IsFalse();
        await Assert.That(registry.AuthorizationHeaders).IsEmpty();

        if (cancel)
        {
            await cancellation.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await request);
            await Assert.That(registry.AuthorizationHeaders).IsEmpty();
        }
        else
        {
            pendingToken.SetResult(NewToken("ready-token"));
            _ = await request;
            await Assert.That(registry.AuthorizationHeaders[0]?.Parameter).IsEqualTo("ready-token");
        }
    }

    private sealed class TokenEndpointHandler(int expiresInSeconds) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
                {
                    access_token = $"token-{RequestCount}",
                    token_type = "Bearer",
                    expires_in = expiresInSeconds
                }))
            });
        }
    }

    private static OAuthBearerToken NewToken(string tokenValue) => new()
    {
        TokenValue = tokenValue,
        Expiration = DateTimeOffset.UtcNow.AddHours(1),
        PrincipalName = "schema-registry"
    };

    private static OAuthBearerConfig NewOAuthConfig() => new()
    {
        TokenEndpointUrl = "https://auth.local/token",
        ClientId = "schema-registry-client",
        ClientSecret = "secret"
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
