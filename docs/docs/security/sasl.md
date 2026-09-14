---
sidebar_position: 2
description: "SASL/PLAIN, SCRAM, GSSAPI (Kerberos), and AWS_MSK_IAM authentication for Dekaf, including Confluent Cloud setup and securing credentials."
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

### Server iteration limit

Dekaf rejects SCRAM challenges requesting more than **1,000,000 PBKDF2 iterations** before deriving the password key. This bounds server-requested authentication CPU work. Set a lower limit to match your broker credentials, or raise it deliberately for credentials configured above the default:

```csharp
var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .UseTls()
    .WithSaslScramSha256("username", "password")
    .WithSaslScramMaxIterations(8192)
    .BuildAsync();
```

`WithSaslScramMaxIterations` is available on producer, consumer, share consumer, admin, and shared `KafkaClient` builders. Configure it on the shared client when connections are shared. Options objects and dependency injection configuration use `SaslScramMaxIterations`. The value must be positive; it limits both SCRAM hashes and delegation-token authentication, without changing the broker's iteration count.

Malformed challenges, duplicate attributes, unsupported mandatory extensions, invalid encodings, and excessive iteration counts fail with `Dekaf.Errors.AuthenticationException`. Valid optional extensions are ignored, and server signatures use constant-time comparison. These checks follow the SCRAM message grammar and extension handling in [RFC 5802](https://datatracker.ietf.org/doc/html/rfc5802).

## SASL/GSSAPI (Kerberos)

GSSAPI is implemented in the `net8.0` and `net10.0` core package assets using
`System.Net.Security.NegotiateAuthentication`. No optional authentication package
is needed. The `netstandard2.0` asset exposes configuration but throws
`PlatformNotSupportedException` when authentication starts.

| Client OS | Credential backend and requirements |
| --- | --- |
| Windows | SSPI and the Windows credential store or process service identity. Explicit `KeytabPath` throws `NotSupportedException` during configuration validation. |
| Linux | MIT/Heimdal Kerberos libraries (`libgssapi_krb5`) and credentials available through a ticket cache or configured client keytab. |
| macOS | Platform Heimdal Kerberos and available credentials. |

CI runs local MIT KDC round trips on Linux for clean .NET 8 and .NET 10 package
consumers. Windows/macOS backend support is established by the implementation;
those round trips do not validate a Windows domain or macOS deployment.
Initialize credentials before starting the process (for example, `kinit -kt ...`
with `KRB5_CONFIG` and `KRB5CCNAME`). Keytab selection is process-wide; different
keytabs in one process are rejected. `Principal` selects an identity, not a stored
password, and the platform must be able to obtain credentials for that identity.

For Kerberos authentication:

```csharp
using Dekaf;
using Dekaf.Security.Sasl;

var gssapiConfig = new GssapiConfig
{
    ServiceName = "kafka",
    Realm = "EXAMPLE.COM",
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
var environmentPassword = Environment.GetEnvironmentVariable("KAFKA_PASSWORD");

// ✅ Good - from configuration
var configuredPassword = configuration["Kafka:Password"];

// ✅ Good - from secret manager
var managedPassword = await secretManager.GetSecretAsync("kafka-password");

// ❌ Bad - hardcoded
var insecureProducer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithSaslPlain("user", "MySecretPassword123");
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
