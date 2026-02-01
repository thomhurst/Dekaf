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
    TokenEndpoint = "https://auth.example.com/oauth2/token",
    ClientId = "my-kafka-client",
    ClientSecret = "client-secret",
    Scope = "kafka"
};

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .UseTls()
    .WithOAuthBearer(oauthConfig)
    .Build();
```

## Custom Token Provider

For more control, implement a custom token provider:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .UseTls()
    .WithOAuthBearer(async ct =>
    {
        var token = await GetTokenFromIdentityProviderAsync(ct);
        return new OAuthBearerToken
        {
            Value = token.AccessToken,
            ExpiresAt = token.ExpiresAt,
            Principal = token.Subject
        };
    })
    .Build();
```

## Azure AD Example

```csharp
using Dekaf;

var credential = new DefaultAzureCredential();

var producer = Kafka.CreateProducer<string, string>()
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
            Value = token.Token,
            ExpiresAt = token.ExpiresOn
        };
    })
    .Build();
```

## AWS MSK IAM

For AWS MSK with IAM authentication:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("broker.msk.us-east-1.amazonaws.com:9098")
    .UseTls()
    .WithOAuthBearer(async ct =>
    {
        // Use AWS SDK to generate IAM token
        var token = await GenerateMskIamTokenAsync(ct);
        return new OAuthBearerToken
        {
            Value = token,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
    })
    .Build();
```

## Token Refresh

Dekaf automatically refreshes tokens before they expire. The token provider is called whenever a new token is needed.

```csharp
.WithOAuthBearer(async ct =>
{
    _logger.LogDebug("Refreshing OAuth token");

    var token = await _tokenService.GetTokenAsync(ct);

    _logger.LogDebug("Token expires at {ExpiresAt}", token.ExpiresAt);

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

    public IKafkaProducer<string, string> CreateProducer()
    {
        return Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_config["Kafka:BootstrapServers"])
            .UseTls()
            .WithOAuthBearer(GetTokenAsync)
            .Build();
    }

    private async ValueTask<OAuthBearerToken> GetTokenAsync(CancellationToken ct)
    {
        var token = await _tokenService.GetAccessTokenAsync(
            _config["OAuth:Scope"],
            ct
        );

        return new OAuthBearerToken
        {
            Value = token.AccessToken,
            ExpiresAt = token.ExpiresAt,
            Principal = token.Claims.Subject
        };
    }
}
```
