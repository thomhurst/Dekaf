---
sidebar_position: 2
---

# SASL Authentication

SASL (Simple Authentication and Security Layer) provides username/password authentication for Kafka.

## SASL/PLAIN

Simple username/password authentication:

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .WithTls()  // Always use TLS with PLAIN to encrypt credentials
    .WithSaslPlain("username", "password")
    .BuildAsync();
```

:::warning
SASL/PLAIN sends credentials in clear text. Always combine with TLS encryption.
:::

## SASL/SCRAM

Challenge-response authentication that doesn't send passwords:

### SCRAM-SHA-256

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .WithTls()
    .WithSaslScramSha256("username", "password")
    .BuildAsync();
```

### SCRAM-SHA-512 (Recommended)

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .WithTls()
    .WithSaslScramSha512("username", "password")
    .BuildAsync();
```

## SASL/GSSAPI (Kerberos)

For Kerberos authentication:

```csharp
using Dekaf;

var gssapiConfig = new GssapiConfig
{
    ServicePrincipal = "kafka/broker.example.com@EXAMPLE.COM",
    KeytabPath = "/path/to/client.keytab",
    Principal = "client@EXAMPLE.COM"
};

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .WithGssapi(gssapiConfig)
    .BuildAsync();
```

## Consumer Configuration

Same methods work for consumers:

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .WithGroupId("my-group")
    .WithTls()
    .WithSaslScramSha512("username", "password")
    .SubscribeTo("my-topic")
    .BuildAsync();
```

## Confluent Cloud Example

```csharp
using Dekaf;

var apiKey = Environment.GetEnvironmentVariable("CONFLUENT_API_KEY");
var apiSecret = Environment.GetEnvironmentVariable("CONFLUENT_API_SECRET");

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("pkc-xxxxx.us-east-1.aws.confluent.cloud:9092")
    .WithTls()
    .WithSaslPlain(apiKey, apiSecret)
    .BuildAsync();
```

## Securing Credentials

Never hardcode credentials:

```csharp
// ✅ Good - from environment
var password = Environment.GetEnvironmentVariable("KAFKA_PASSWORD");

// ✅ Good - from configuration
var password = configuration["Kafka:Password"];

// ✅ Good - from secret manager
var password = await secretManager.GetSecretAsync("kafka-password");

// ❌ Bad - hardcoded
.WithSaslPlain("user", "MySecretPassword123")
```

## Complete Example

```csharp
using Dekaf;

public class SecureKafkaClient
{
    private readonly IConfiguration _config;

    public async Task<IKafkaProducer<string, string>> CreateProducer()
    {
        return await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_config["Kafka:BootstrapServers"])
            .WithTls()
            .WithSaslScramSha512(
                _config["Kafka:Username"],
                _config["Kafka:Password"]
            )
            .ForReliability()
            .BuildAsync();
    }
}
```
