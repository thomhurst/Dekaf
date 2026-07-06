using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dekaf.Security.Sasl;

namespace Dekaf.Tests.Unit.Security.Sasl;

public sealed class OAuthBearerTokenProviderTests
{
    [Test]
    public async Task JwtBearerAssertion_WithRsaKey_SignsAssertionAndWritesClaims()
    {
        using var rsa = RSA.Create(2048);
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var config = CreateJwtBearerConfig(rsa, additionalClaims: new Dictionary<string, object?>
        {
            ["tenant"] = "alpha"
        });

        var assertion = OAuthBearerJwtAssertion.Create(config, now);

        var parts = assertion.Split('.');
        await Assert.That(parts.Length).IsEqualTo(3);

        var header = DecodeJwtPart(parts[0]);
        await Assert.That(header.GetProperty("alg").GetString()).IsEqualTo("RS256");
        await Assert.That(header.GetProperty("typ").GetString()).IsEqualTo("JWT");
        await Assert.That(header.GetProperty("kid").GetString()).IsEqualTo("key-1");

        var payload = DecodeJwtPart(parts[1]);
        await Assert.That(payload.GetProperty("iss").GetString()).IsEqualTo("client");
        await Assert.That(payload.GetProperty("sub").GetString()).IsEqualTo("client");
        await Assert.That(payload.GetProperty("aud").GetString()).IsEqualTo("kafka");
        await Assert.That(payload.GetProperty("iat").GetInt64()).IsEqualTo(1_700_000_000);
        await Assert.That(payload.GetProperty("exp").GetInt64()).IsEqualTo(1_700_000_300);
        await Assert.That(payload.GetProperty("tenant").GetString()).IsEqualTo("alpha");
        await Assert.That(payload.GetProperty("jti").GetString()).IsNotNull();

        var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        var signature = Base64UrlDecode(parts[2]);
        await Assert.That(rsa.VerifyData(signingInput, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            .IsTrue();
    }

    [Test]
    public async Task JwtBearerAssertion_WithAdditionalClaimValues_WritesAotSafeJson()
    {
        using var rsa = RSA.Create(2048);
        var config = CreateJwtBearerConfig(rsa, additionalClaims: new Dictionary<string, object?>
        {
            ["tenant"] = "alpha",
            ["enabled"] = true,
            ["retries"] = 3,
            ["scopes"] = new List<string> { "read", "write" },
            ["metadata"] = new Dictionary<string, object?>
            {
                ["region"] = "eu",
                ["level"] = 2
            },
            ["optional"] = null
        });

        var assertion = OAuthBearerJwtAssertion.Create(
            config,
            DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        var payload = DecodeJwtPart(assertion.Split('.')[1]);
        await Assert.That(payload.GetProperty("tenant").GetString()).IsEqualTo("alpha");
        await Assert.That(payload.GetProperty("enabled").GetBoolean()).IsTrue();
        await Assert.That(payload.GetProperty("retries").GetInt32()).IsEqualTo(3);
        await Assert.That(payload.GetProperty("scopes")[0].GetString()).IsEqualTo("read");
        await Assert.That(payload.GetProperty("scopes")[1].GetString()).IsEqualTo("write");
        await Assert.That(payload.GetProperty("metadata").GetProperty("region").GetString()).IsEqualTo("eu");
        await Assert.That(payload.GetProperty("metadata").GetProperty("level").GetInt32()).IsEqualTo(2);
        await Assert.That(payload.GetProperty("optional").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task JwtBearerAssertion_WithUnsupportedAdditionalClaimValue_ThrowsInvalidOperationException()
    {
        using var rsa = RSA.Create(2048);
        var config = CreateJwtBearerConfig(rsa, additionalClaims: new Dictionary<string, object?>
        {
            ["custom"] = new UnsupportedClaimValue("alpha")
        });

        await Assert.That(() => OAuthBearerJwtAssertion.Create(
                config,
                DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("unsupported value type");
    }

    [Test]
    public async Task JwtBearerAssertion_WithNonFiniteAdditionalClaimNumber_ThrowsInvalidOperationException()
    {
        using var rsa = RSA.Create(2048);
        var config = CreateJwtBearerConfig(rsa, additionalClaims: new Dictionary<string, object?>
        {
            ["score"] = double.NaN
        });

        await Assert.That(() => OAuthBearerJwtAssertion.Create(
                config,
                DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)))
            .Throws<InvalidOperationException>()
            .And.HasMessageContaining("score")
            .And.HasMessageContaining("unsupported value type");
    }

    [Test]
    public async Task JwtBearerAssertion_WithEcdsaKey_SignsAssertion()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var config = CreateJwtBearerConfig(ecdsa, signingAlgorithm: OAuthBearerJwtSigningAlgorithm.Es256);

        var assertion = OAuthBearerJwtAssertion.Create(config, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        var parts = assertion.Split('.');
        var header = DecodeJwtPart(parts[0]);
        await Assert.That(header.GetProperty("alg").GetString()).IsEqualTo("ES256");

        var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        var signature = Base64UrlDecode(parts[2]);
        await Assert.That(ecdsa.VerifyData(
                signingInput,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            .IsTrue();
    }

    [Test]
    public async Task JwtBearerAssertion_WithExplicitRsaAlgorithms_SignsAssertion()
    {
        var cases = new[]
        {
            (OAuthBearerJwtSigningAlgorithm.Rs384, "RS384", HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1),
            (OAuthBearerJwtSigningAlgorithm.Rs512, "RS512", HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1),
            (OAuthBearerJwtSigningAlgorithm.Ps256, "PS256", HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            (OAuthBearerJwtSigningAlgorithm.Ps384, "PS384", HashAlgorithmName.SHA384, RSASignaturePadding.Pss),
            (OAuthBearerJwtSigningAlgorithm.Ps512, "PS512", HashAlgorithmName.SHA512, RSASignaturePadding.Pss)
        };

        foreach (var (algorithm, headerValue, hashAlgorithm, padding) in cases)
        {
            using var rsa = RSA.Create(2048);
            var config = CreateJwtBearerConfig(rsa, signingAlgorithm: algorithm);

            var assertion = OAuthBearerJwtAssertion.Create(config, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

            var parts = assertion.Split('.');
            var header = DecodeJwtPart(parts[0]);
            await Assert.That(header.GetProperty("alg").GetString()).IsEqualTo(headerValue);

            var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            var signature = Base64UrlDecode(parts[2]);
            await Assert.That(rsa.VerifyData(signingInput, signature, hashAlgorithm, padding)).IsTrue();
        }
    }

    [Test]
    public async Task JwtBearerAssertion_WithEcdsaKey_InfersAlgorithmFromKeySize()
    {
        var cases = new[]
        {
            (ECCurve.NamedCurves.nistP256, "ES256", HashAlgorithmName.SHA256),
            (ECCurve.NamedCurves.nistP384, "ES384", HashAlgorithmName.SHA384),
            (ECCurve.NamedCurves.nistP521, "ES512", HashAlgorithmName.SHA512)
        };

        foreach (var (curve, headerValue, hashAlgorithm) in cases)
        {
            using var ecdsa = ECDsa.Create(curve);
            var config = CreateJwtBearerConfig(ecdsa);

            var assertion = OAuthBearerJwtAssertion.Create(config, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

            var parts = assertion.Split('.');
            var header = DecodeJwtPart(parts[0]);
            await Assert.That(header.GetProperty("alg").GetString()).IsEqualTo(headerValue);

            var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            var signature = Base64UrlDecode(parts[2]);
            await Assert.That(ecdsa.VerifyData(
                    signingInput,
                    signature,
                    hashAlgorithm,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                .IsTrue();
        }
    }

    [Test]
    public async Task JwtBearerAssertion_WithMismatchedAlgorithmAndKey_ThrowsInvalidOperationException()
    {
        using var rsa = RSA.Create(2048);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        await Assert.That(() => OAuthBearerJwtAssertion.Create(
                CreateJwtBearerConfig(rsa, signingAlgorithm: OAuthBearerJwtSigningAlgorithm.Es256),
                DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)))
            .Throws<InvalidOperationException>();

        await Assert.That(() => OAuthBearerJwtAssertion.Create(
                CreateJwtBearerConfig(ecdsa, signingAlgorithm: OAuthBearerJwtSigningAlgorithm.Rs256),
                DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task JwtBearerAssertion_WithReservedAdditionalClaim_ThrowsInvalidOperationException()
    {
        using var rsa = RSA.Create(2048);
        var config = CreateJwtBearerConfig(rsa, additionalClaims: new Dictionary<string, object?>
        {
            ["iss"] = "other-issuer"
        });

        await Assert.That(() => OAuthBearerJwtAssertion.Create(
                config,
                DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task JwtBearerAssertion_WithNonPositiveLifetime_ThrowsInvalidOperationException()
    {
        using var rsa = RSA.Create(2048);
        var config = new OAuthBearerConfig
        {
            GrantType = OAuthBearerGrantType.JwtBearer,
            TokenEndpointUrl = "https://auth.example.test/token",
            ClientId = "client",
            JwtBearer = new OAuthBearerJwtBearerOptions
            {
                PrivateKey = rsa,
                Audience = "kafka",
                AssertionLifetime = TimeSpan.Zero
            }
        };

        await Assert.That(() => OAuthBearerJwtAssertion.Create(
                config,
                DateTimeOffset.FromUnixTimeSeconds(1_700_000_000)))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task CreateHttpHandler_SetsPooledConnectionLifetime()
    {
        using var handler = OAuthBearerTokenProvider.CreateHttpHandler();

        await Assert.That(handler).IsTypeOf<SocketsHttpHandler>();
        await Assert.That(((SocketsHttpHandler)handler).PooledConnectionLifetime).IsEqualTo(TimeSpan.FromMinutes(2));
    }

    [Test]
    public async Task GetTokenAsync_WithJwtBearer_SendsAssertionGrant()
    {
        using var rsa = RSA.Create(2048);
        var handler = new CapturingTokenEndpointHandler();
        using var provider = new OAuthBearerTokenProvider(CreateJwtBearerConfig(rsa), new HttpClient(handler));

        var token = await provider.GetTokenAsync();

        await Assert.That(token.TokenValue).IsEqualTo("access-token-1");
        await Assert.That(token.PrincipalName).IsEqualTo("principal-1");
        await Assert.That(handler.Requests.Count).IsEqualTo(1);

        var form = handler.Requests[0];
        await Assert.That(form["grant_type"]).IsEqualTo("urn:ietf:params:oauth:grant-type:jwt-bearer");
        await Assert.That(form["client_id"]).IsEqualTo("client");
        await Assert.That(form["scope"]).IsEqualTo("kafka:produce kafka:consume");
        await Assert.That(form["resource"]).IsEqualTo("cluster-a");
        await Assert.That(form["assertion"].Split('.').Length).IsEqualTo(3);
    }

    [Test]
    public async Task GetTokenAsync_WithAzureImds_SendsMetadataGetRequest()
    {
        var handler = new CapturingAzureImdsHandler();
        var options = new OAuthBearerAzureImdsOptions
        {
            TokenEndpoint = "http://169.254.169.254/metadata/identity/oauth2/token",
            Resource = "api://kafka",
            ClientId = "managed-identity-client"
        };
        using var provider = new OAuthBearerTokenProvider(options.ToOAuthBearerConfig(), new HttpClient(handler));

        var token = await provider.GetTokenAsync();

        await Assert.That(token.TokenValue).IsEqualTo("azure-access-token");
        await Assert.That(token.PrincipalName).IsEqualTo("managed-identity-client");
        await Assert.That(handler.Requests.Count).IsEqualTo(1);

        var request = handler.Requests[0];
        await Assert.That(request.Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(request.HasContent).IsFalse();
        await Assert.That(request.MetadataHeader).IsEqualTo("true");
        await Assert.That(request.Query["api-version"]).IsEqualTo("2018-02-01");
        await Assert.That(request.Query["resource"]).IsEqualTo("api://kafka");
        await Assert.That(request.Query["client_id"]).IsEqualTo("managed-identity-client");
    }

    [Test]
    public async Task GetTokenAsync_WithAzureImdsSystemAssignedIdentity_OmitsClientId()
    {
        var handler = new CapturingAzureImdsHandler();
        var options = new OAuthBearerAzureImdsOptions
        {
            Resource = "https://eventhubs.azure.net/"
        };
        using var provider = new OAuthBearerTokenProvider(options.ToOAuthBearerConfig(), new HttpClient(handler));

        _ = await provider.GetTokenAsync();

        await Assert.That(handler.Requests[0].Query.ContainsKey("client_id")).IsFalse();
    }

    [Test]
    public async Task ToOAuthBearerConfig_WithAzureImdsMissingResource_ThrowsInvalidOperationException()
    {
        var options = new OAuthBearerAzureImdsOptions { Resource = string.Empty };

        await Assert.That(() => options.ToOAuthBearerConfig()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetTokenAsync_WithValidCachedToken_ReusesToken()
    {
        using var rsa = RSA.Create(2048);
        var handler = new CapturingTokenEndpointHandler();
        using var provider = new OAuthBearerTokenProvider(CreateJwtBearerConfig(rsa), new HttpClient(handler));

        var first = await provider.GetTokenAsync();
        var second = await provider.GetTokenAsync();

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(handler.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetTokenAsync_WhenCachedTokenInsideRefreshBuffer_RefreshesToken()
    {
        using var rsa = RSA.Create(2048);
        var handler = new CapturingTokenEndpointHandler { ExpiresInSeconds = 30 };
        using var provider = new OAuthBearerTokenProvider(CreateJwtBearerConfig(rsa), new HttpClient(handler));

        var first = await provider.GetTokenAsync();
        var second = await provider.GetTokenAsync();

        await Assert.That(first.TokenValue).IsEqualTo("access-token-1");
        await Assert.That(second.TokenValue).IsEqualTo("access-token-2");
        await Assert.That(handler.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ToOAuthBearerConfig_ClonesMutableJwtBearerOptions()
    {
        using var rsa = RSA.Create(2048);
        var scopes = new List<string> { "kafka:produce" };
        var additionalClaims = new Dictionary<string, object?> { ["tenant"] = "alpha" };
        var additionalParameters = new Dictionary<string, string> { ["resource"] = "cluster-a" };
        var options = new OAuthBearerJwtBearerOptions
        {
            TokenEndpoint = "https://auth.example.test/token",
            ClientId = "client",
            PrivateKey = rsa,
            Audience = "kafka",
            Scopes = scopes,
            AdditionalClaims = additionalClaims,
            AdditionalParameters = additionalParameters
        };

        var config = options.ToOAuthBearerConfig();

        scopes[0] = "mutated";
        additionalClaims["tenant"] = "mutated";
        additionalParameters["resource"] = "mutated";

        await Assert.That(config.Scope).IsEqualTo("kafka:produce");
        await Assert.That(config.JwtBearer!.Scopes![0]).IsEqualTo("kafka:produce");
        await Assert.That(config.JwtBearer.AdditionalClaims!["tenant"]).IsEqualTo("alpha");
        await Assert.That(config.AdditionalParameters!["resource"]).IsEqualTo("cluster-a");
    }

    private static OAuthBearerConfig CreateJwtBearerConfig(
        AsymmetricAlgorithm privateKey,
        OAuthBearerJwtSigningAlgorithm? signingAlgorithm = null,
        IReadOnlyDictionary<string, object?>? additionalClaims = null)
    {
        return new OAuthBearerJwtBearerOptions
        {
            TokenEndpoint = "https://auth.example.test/token",
            ClientId = "client",
            PrivateKey = privateKey,
            Audience = "kafka",
            KeyId = "key-1",
            Scopes = ["kafka:produce", "kafka:consume"],
            AdditionalClaims = additionalClaims,
            AdditionalParameters = new Dictionary<string, string> { ["resource"] = "cluster-a" },
            AssertionLifetime = TimeSpan.FromMinutes(5),
            SigningAlgorithm = signingAlgorithm
        }.ToOAuthBearerConfig();
    }

    private static JsonElement DecodeJwtPart(string value)
    {
        using var document = JsonDocument.Parse(Base64UrlDecode(value));
        return document.RootElement.Clone();
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        var padding = base64.Length % 4;
        if (padding > 0)
            base64 += new string('=', 4 - padding);

        return Convert.FromBase64String(base64);
    }

    private sealed record UnsupportedClaimValue(string Value);

    private sealed class CapturingTokenEndpointHandler : HttpMessageHandler
    {
        public List<IReadOnlyDictionary<string, string>> Requests { get; } = [];
        public int ExpiresInSeconds { get; init; } = 3600;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Add(ParseForm(body));

            var count = Requests.Count;
            var json = $$"""{"access_token":"access-token-{{count}}","expires_in":{{ExpiresInSeconds}},"sub":"principal-{{count}}"}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private static IReadOnlyDictionary<string, string> ParseForm(string body)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                var key = DecodeFormValue(parts[0]);
                var value = parts.Length == 2 ? DecodeFormValue(parts[1]) : string.Empty;
                result[key] = value;
            }

            return result;
        }

        private static string DecodeFormValue(string value) =>
            Uri.UnescapeDataString(value.Replace('+', ' '));
    }

    private sealed class CapturingAzureImdsHandler : HttpMessageHandler
    {
        public List<AzureImdsRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new AzureImdsRequest(
                request.Method,
                request.Content is not null,
                request.Headers.TryGetValues("Metadata", out var metadataValues)
                    ? metadataValues.Single()
                    : null,
                ParseQuery(request.RequestUri!.Query)));

            const string json = """{"access_token":"azure-access-token","expires_in":"3600","client_id":"managed-identity-client"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        private static IReadOnlyDictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(parts[0]);
                var value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                result[key] = value;
            }

            return result;
        }
    }

    private sealed record AzureImdsRequest(
        HttpMethod Method,
        bool HasContent,
        string? MetadataHeader,
        IReadOnlyDictionary<string, string> Query);
}
