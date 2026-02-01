---
sidebar_position: 1
---

# TLS Encryption

Encrypt communication between Dekaf and Kafka brokers using TLS.

## Basic TLS

Enable TLS for encrypted connections:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseTls()
    .Build();
```

This uses system CA certificates to validate the broker's certificate.

## Custom CA Certificate

If your Kafka uses a private CA:

```csharp
var tlsConfig = new TlsConfig
{
    CaCertificatePath = "/path/to/ca.crt"
};

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseTls(tlsConfig)
    .Build();
```

## Mutual TLS (mTLS)

For client certificate authentication:

```csharp
// Using file paths
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseMutualTls(
        caCertPath: "/path/to/ca.crt",
        clientCertPath: "/path/to/client.crt",
        clientKeyPath: "/path/to/client.key",
        keyPassword: "optional-password"
    )
    .Build();

// Using X509Certificate2
var clientCert = new X509Certificate2("client.pfx", "password");
var caCert = new X509Certificate2("ca.crt");

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseMutualTls(clientCert, caCert)
    .Build();
```

## TLS Configuration Options

```csharp
var tlsConfig = new TlsConfig
{
    // CA certificate for server validation
    CaCertificatePath = "/path/to/ca.crt",

    // Client certificate for mTLS
    ClientCertificatePath = "/path/to/client.crt",
    ClientKeyPath = "/path/to/client.key",
    ClientKeyPassword = "password",

    // Skip server certificate validation (NOT recommended for production)
    SkipServerCertificateValidation = false,

    // Allowed TLS protocols
    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
};

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseTls(tlsConfig)
    .Build();
```

## Common Configurations

### AWS MSK with IAM

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("broker1.msk.us-east-1.amazonaws.com:9098")
    .UseTls()
    .WithSaslOAuthBearer(new AwsMskTokenProvider())
    .Build();
```

### Confluent Cloud

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("pkc-xxxxx.us-east-1.aws.confluent.cloud:9092")
    .UseTls()
    .WithSaslPlain(apiKey, apiSecret)
    .Build();
```

## Troubleshooting

### Certificate Validation Fails

```
AuthenticationException: The remote certificate was rejected
```

Solutions:
1. Ensure CA certificate is correct
2. Check certificate chain is complete
3. Verify server hostname matches certificate

### For Development Only

```csharp
// ⚠️ NEVER use in production
var tlsConfig = new TlsConfig
{
    SkipServerCertificateValidation = true
};
```

### Debug Certificate Issues

```csharp
var tlsConfig = new TlsConfig
{
    CaCertificatePath = "/path/to/ca.crt",
    ServerCertificateValidationCallback = (sender, cert, chain, errors) =>
    {
        Console.WriteLine($"Subject: {cert.Subject}");
        Console.WriteLine($"Issuer: {cert.Issuer}");
        Console.WriteLine($"Errors: {errors}");
        return errors == SslPolicyErrors.None;
    }
};
```
