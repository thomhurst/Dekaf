---
sidebar_position: 3
---

# OAuth / OAUTHBEARER

OAUTHBEARER authentication allows using OAuth 2.0 tokens for Kafka authentication.

## Using OAuth Configuration

```csharp
using Dekaf;

var oauthConfig = new OAuthBearerConfig
{
    TokenEndpointUrl = "https://auth.example.com/oauth2/token",
    ClientId = "my-kafka-client",
    ClientSecret = "client-secret",
    Scope = "kafka"
};

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .UseTls()
    .WithOAuthBearer(oauthConfig)
    .BuildAsync();
```

## Custom Token Provider

For more control, implement a custom token provider:

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .UseTls()
    .WithOAuthBearer(async ct =>
    {
        var token = await GetTokenFromIdentityProviderAsync(ct);
        return new OAuthBearerToken
        {
            TokenValue = token.AccessToken,
            Expiration = token.ExpiresAt,
            PrincipalName = token.Subject
        };
    })
    .BuildAsync();
```

## JWT-Bearer Grant

Use the JWT-bearer grant when your identity provider exchanges a signed assertion for
an access token:

```csharp
using System.Security.Cryptography;
using Dekaf;

var privateKey = RSA.Create();
privateKey.ImportFromPem(File.ReadAllText("client-key.pem"));

await using var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .UseTls()
    .WithOAuthBearerJwtBearer(options =>
    {
        options.TokenEndpoint = "https://auth.example.com/oauth2/token";
        options.ClientId = "my-kafka-client";
        options.PrivateKey = privateKey;
        options.Audience = "kafka";
        options.Scopes = ["kafka:produce", "kafka:consume"];
        options.KeyId = "key-2026-07";
    })
    .BuildAsync();
```

Dekaf signs assertions with RSA or ECDSA keys, posts `grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer`
and `assertion=<jwt>` to the token endpoint, then caches the returned access token until it nears expiration.
Keep the `PrivateKey` object alive until every client configured with it has been disposed; token refreshes reuse
the same key object to sign new assertions.

## Azure AD Example

```csharp
using Dekaf;

var credential = new DefaultAzureCredential();

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .UseTls()
    .WithOAuthBearer(async ct =>
    {
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://eventhubs.azure.net/.default" }),
            ct
        );

        return new OAuthBearerToken
        {
            TokenValue = token.Token,
            Expiration = token.ExpiresOn,
            PrincipalName = "azure-ad"
        };
    })
    .BuildAsync();
```

## AWS MSK IAM

For AWS MSK with IAM authentication:

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("broker.msk.us-east-1.amazonaws.com:9098")
    .UseTls()
    .WithOAuthBearer(async ct =>
    {
        // Use AWS SDK to generate IAM token
        var token = await GenerateMskIamTokenAsync(ct);
        return new OAuthBearerToken
        {
            TokenValue = token,
            Expiration = DateTimeOffset.UtcNow.AddMinutes(5),
            PrincipalName = "aws-msk"
        };
    })
    .BuildAsync();
```

## Token Refresh

Dekaf automatically refreshes tokens before they expire. The token provider is called whenever a new token is needed.

```csharp
.WithOAuthBearer(async ct =>
{
    _logger.LogDebug("Refreshing OAuth token");

    var token = await _tokenService.GetTokenAsync(ct);

    _logger.LogDebug("Token expires at {ExpiresAt}", token.Expiration);

    return token;
})
```

## Complete Example

```csharp
using Dekaf;

public class OAuthKafkaClientFactory
{
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _config;

    public OAuthKafkaClientFactory(ITokenService tokenService, IConfiguration config)
    {
        _tokenService = tokenService;
        _config = config;
    }

    public async Task<IKafkaProducer<string, string>> CreateProducer()
    {
        return await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_config["Kafka:BootstrapServers"])
            .UseTls()
            .WithOAuthBearer(GetTokenAsync)
            .BuildAsync();
    }

    private async ValueTask<OAuthBearerToken> GetTokenAsync(CancellationToken ct)
    {
        var token = await _tokenService.GetAccessTokenAsync(
            _config["OAuth:Scope"],
            ct
        );

        return new OAuthBearerToken
        {
            TokenValue = token.AccessToken,
            Expiration = token.ExpiresAt,
            PrincipalName = token.Claims.Subject
        };
    }
}
```
