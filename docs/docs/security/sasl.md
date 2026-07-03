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
    .UseTls()  // Always use TLS with PLAIN to encrypt credentials
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
    .UseTls()
    .WithSaslScramSha256("username", "password")
    .BuildAsync();
```

### SCRAM-SHA-512 (Recommended)

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .UseTls()
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

## AWS_MSK_IAM (Amazon MSK IAM)

Use the native AWS_MSK_IAM mechanism for Amazon MSK clusters with IAM access control:

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("b-1.example.c2.kafka.us-east-1.amazonaws.com:9098")
    .UseTls()
    .WithAwsMskIam()
    .BuildAsync();
```

Dekaf signs the SASL payload with SigV4 using the default AWS credential chain:
environment variables, web identity, shared AWS profiles, ECS credentials, then EC2 instance metadata.
The AWS region is inferred from standard MSK broker hostnames; set it explicitly when using a custom DNS name:

```csharp
using Dekaf;
using Dekaf.Security.Sasl;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.internal.example.com:9098")
    .UseTls()
    .WithAwsMskIam(new AwsMskIamConfig
    {
        Region = "us-east-1",
        ProfileName = "msk-prod"
    })
    .BuildAsync();
```

For non-standard credential sources, provide a custom credentials provider without adding the AWS SDK to Dekaf:

```csharp
using Dekaf;
using Dekaf.Security.Sasl;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("b-1.example.c2.kafka.us-east-1.amazonaws.com:9098")
    .UseTls()
    .WithAwsMskIam(new AwsMskIamConfig
    {
        CredentialsProviderFunc = async ct =>
        {
            var credentials = await LoadCredentialsAsync(ct);
            return new AwsCredentials(
                credentials.AccessKeyId,
                credentials.SecretAccessKey,
                credentials.SessionToken,
                credentials.ExpiresAt);
        }
    })
    .BuildAsync();
```

## Consumer Configuration

Same methods work for consumers:

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .WithGroupId("my-group")
    .UseTls()
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
    .UseTls()
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
            .UseTls()
            .WithSaslScramSha512(
                _config["Kafka:Username"],
                _config["Kafka:Password"]
            )
            .ForReliability()
            .BuildAsync();
    }
}
```
