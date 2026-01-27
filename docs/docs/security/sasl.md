---
sidebar_position: 2
---

# SASL Authentication

SASL (Simple Authentication and Security Layer) provides username/password authentication for Kafka.

## SASL/PLAIN

Simple username/password authentication:

```csharp
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .UseTls()  // Always use TLS with PLAIN to encrypt credentials
    .WithSaslPlain("username", "password")
    .Build();
```

:::warning
SASL/PLAIN sends credentials in clear text. Always combine with TLS encryption.
:::

## SASL/SCRAM

Challenge-response authentication that doesn't send passwords:

### SCRAM-SHA-256

```csharp
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .UseTls()
    .WithSaslScramSha256("username", "password")
    .Build();
```

### SCRAM-SHA-512 (Recommended)

```csharp
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .UseTls()
    .WithSaslScramSha512("username", "password")
    .Build();
```

## SASL/GSSAPI (Kerberos)

For Kerberos authentication:

```csharp
var gssapiConfig = new GssapiConfig
{
    ServicePrincipal = "kafka/broker.example.com@EXAMPLE.COM",
    KeytabPath = "/path/to/client.keytab",
    Principal = "client@EXAMPLE.COM"
};

var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .WithGssapi(gssapiConfig)
    .Build();
```

## Consumer Configuration

Same methods work for consumers:

```csharp
var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .WithGroupId("my-group")
    .UseTls()
    .WithSaslScramSha512("username", "password")
    .SubscribeTo("my-topic")
    .Build();
```

## Confluent Cloud Example

```csharp
var apiKey = Environment.GetEnvironmentVariable("CONFLUENT_API_KEY");
var apiSecret = Environment.GetEnvironmentVariable("CONFLUENT_API_SECRET");

var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("pkc-xxxxx.us-east-1.aws.confluent.cloud:9092")
    .UseTls()
    .WithSaslPlain(apiKey, apiSecret)
    .Build();
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
public class SecureKafkaClient
{
    private readonly IConfiguration _config;

    public IKafkaProducer<string, string> CreateProducer()
    {
        return Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(_config["Kafka:BootstrapServers"])
            .UseTls()
            .WithSaslScramSha512(
                _config["Kafka:Username"],
                _config["Kafka:Password"]
            )
            .ForReliability()
            .Build();
    }
}
```
