using System.Net;
using System.Security.Cryptography;
using System.Text;
using Dekaf.Serialization;
using Dekaf.Security.Sasl;

await AotSmoke.RunAsync();

internal static class AotSmoke
{
    public static async Task RunAsync()
    {
        using var rsa = RSA.Create(2048);
        using var provider = new OAuthBearerTokenProvider(CreateJwtBearerConfig(rsa), CreateHttpClient());

        var token = await provider.GetTokenAsync(CancellationToken.None);
        Require(token.TokenValue == "access-token", "OAuth token value mismatch.");
        Require(token.PrincipalName == "aot-principal", "OAuth principal mismatch.");

        var headers = Headers.Create("aot", "ok");
        Require(headers.Count == 1, "Header smoke failed.");
        Require(headers[0].Key == "aot", "Header key mismatch.");
    }

    private static OAuthBearerConfig CreateJwtBearerConfig(RSA rsa) =>
        new()
        {
            GrantType = OAuthBearerGrantType.JwtBearer,
            TokenEndpointUrl = "https://auth.example.test/token",
            ClientId = "aot-client",
            Scope = "kafka:produce",
            JwtBearer = new OAuthBearerJwtBearerOptions
            {
                TokenEndpoint = "https://auth.example.test/token",
                ClientId = "aot-client",
                PrivateKey = rsa,
                Audience = "kafka",
                Scopes = new List<string> { "kafka:produce" },
                AdditionalClaims = new Dictionary<string, object?>
                {
                    ["tenant"] = "aot",
                    ["enabled"] = true,
                    ["metadata"] = new Dictionary<string, object?>
                    {
                        ["region"] = "test"
                    }
                }
            }
        };

    private static HttpClient CreateHttpClient() => new(new TokenEndpointHandler())
    {
        BaseAddress = new Uri("https://auth.example.test/")
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class TokenEndpointHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Require(body.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer", StringComparison.Ordinal),
                "JWT bearer grant type missing.");
            Require(body.Contains("assertion=", StringComparison.Ordinal), "JWT assertion missing.");

            const string json = """{"access_token":"access-token","expires_in":3600,"sub":"aot-principal"}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
