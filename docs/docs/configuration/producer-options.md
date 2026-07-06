---
sidebar_position: 2
---

# Producer Options

Complete reference for all producer configuration options.

These methods are available anywhere a `ProducerBuilder<TKey,TValue>` is used, including dependency injection registration:

```csharp
// Before: DI examples only showed the small common subset.
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers("localhost:9092"));
});

// After: DI uses the full producer builder.
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers("localhost:9092")
        .WithLinger(TimeSpan.FromMilliseconds(5))
        .WithBatchSize(64 * 1024)
        .WithBufferMemory(256 * 1024 * 1024)
        .WithSaslScramSha512("user", "password")
        .ForHighThroughput());
});
```

## Configuration Binding

`Dekaf.Extensions.DependencyInjection` can bind producer settings from an `IConfiguration` section. Keys use `ProducerOptions` property names:

```json
{
  "Kafka": {
    "Producers": {
      "Orders": {
        "BootstrapServers": "broker1:9092,broker2:9092",
        "ClientId": "orders-producer",
        "Acks": "All",
        "LingerMs": 5,
        "BatchSize": 65536,
        "CompressionType": "Lz4",
        "EnableIdempotence": true,
        "UseTls": true,
        "SaslMechanism": "ScramSha512",
        "SaslUsername": "user",
        "SaslPassword": "password"
      }
    }
  }
}
```

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, Order>(
        builder.Configuration.GetSection("Kafka:Producers:Orders"),
        producer => producer.WithValueSerializer(new JsonSerializer<Order>()));
});
```

Configuration is applied before the optional fluent callback, so fluent calls can override values from `appsettings.json`.

| Fluent API | Config key | Notes |
|------------|------------|-------|
| `WithBootstrapServers(...)` | `BootstrapServers` | Server list (prefer `params string[]` in code; comma-separated string and JSON arrays are also supported) |
| `WithClientId(...)` | `ClientId` | String |
| `WithClientDnsLookup(...)` | `ClientDnsLookup` | `UseAllDnsIps` or `ResolveCanonicalBootstrapServersOnly` |
| `WithAcks(...)` | `Acks` | `None`, `Leader`, `All` |
| `WithLinger(...)` | `LingerMs` | Milliseconds |
| `WithBatchSize(...)` | `BatchSize` | Bytes |
| `WithBufferMemory(...)` | `BufferMemory` | Bytes; omit to keep auto-tuning |
| `WithMaxBlock(...)` | `MaxBlockMs` | Milliseconds |
| `WithDeliveryTimeout(...)` | `DeliveryTimeoutMs` | Milliseconds |
| `WithRequestTimeout(...)` | `RequestTimeoutMs` | Milliseconds |
| `WithIdempotence(...)` | `EnableIdempotence` | Boolean |
| `WithConnectionsMaxIdle(...)` | `ConnectionsMaxIdleMs` | Milliseconds; `-1` disables |
| `WithConnectionTimeout(...)` | `ConnectionTimeout` | TimeSpan |
| `WithTcpKeepAlive(...)` | `EnableTcpKeepAlive`, `TcpKeepAliveTime`, `TcpKeepAliveInterval`, `TcpKeepAliveRetryCount` | Socket keepalive |
| `WithConnectionsPerBroker(...)` | `ConnectionsPerBroker` | Integer |
| `WithAdaptiveConnections(...)` | `EnableAdaptiveConnections`, `MaxConnectionsPerBroker` | Set `EnableAdaptiveConnections` to `false` to disable |
| `WithTransactionalId(...)` | `TransactionalId` | String |
| `WithTransactionTimeout(...)` | `TransactionTimeoutMs` | Milliseconds |
| `UseCompression(...)` | `CompressionType` | `None`, `Gzip`, `Snappy`, `Lz4`, `Zstd` |
| `WithCompressionLevel(...)` | `CompressionLevel` | Codec-specific integer |
| `WithPartitioner(...)` | `Partitioner` | `Default`, `Sticky`, `RoundRobin` |
| `UseTls(...)` | `UseTls`, `TlsConfig` | `TlsConfig` can bind certificate path fields |
| `WithRemoteCertificateValidationCallback(...)` | Runtime callback | Custom TLS certificate validation |
| `WithSaslPlain(...)` / `WithSaslScramSha512(...)` | `SaslMechanism`, `SaslUsername`, `SaslPassword` | `SaslMechanism` values match the enum names |
| `WithGssapi(...)` | `SaslMechanism`, `GssapiConfig` | Use `SaslMechanism: Gssapi` |
| `WithOAuthBearer(...)` | `SaslMechanism`, `OAuthBearerConfig` | Use `SaslMechanism: OAuthBearer` |
| `WithOAuthBearerJwtBearer(...)` | Runtime callback | Signs JWT assertions with RSA/ECDSA keys |
| `WithSocketSendBufferBytes(...)` | `SocketSendBufferBytes` | Bytes |
| `WithSocketReceiveBufferBytes(...)` | `SocketReceiveBufferBytes` | Bytes |
| `WithMetadataRecoveryStrategy(...)` | `MetadataRecoveryStrategy` | `None` or `Rebootstrap` |
| `WithMetadataRecoveryRebootstrapTrigger(...)` | `MetadataRecoveryRebootstrapTriggerMs` | Milliseconds |

Serializers, custom partitioners, interceptors, and retry policies are objects, so configure those in the fluent callback.

## Connection Settings

### WithBootstrapServers

Kafka broker addresses for initial connection. Prefer the typed `params string[]` overload in code; the single-string overload remains a convenience for configuration-style comma-separated values.

```csharp
// Single server
.WithBootstrapServers("localhost:9092")

// Multiple servers (typed params)
.WithBootstrapServers("broker1:9092", "broker2:9092", "broker3:9092")

// Convenience for comma-separated configuration values
.WithBootstrapServers("broker1:9092,broker2:9092,broker3:9092")
```

### WithClientId

Identifier sent to brokers for logging and metrics:

```csharp
.WithClientId("order-service-producer")
```

## Delivery Settings

### WithAcks

Controls when the broker considers a message delivered:

```csharp
.WithAcks(Acks.All)     // Wait for all in-sync replicas (safest)
.WithAcks(Acks.Leader)  // Wait for leader only (faster)
.WithAcks(Acks.None)    // Don't wait (fastest, may lose messages)
```

### WithIdempotence

Prevents duplicate messages during retries:

```csharp
.WithIdempotence(true)
```

Automatically sets `Acks.All` and enables sequence numbers.

## Batching Settings

### WithLinger

Time to wait before sending a batch:

```csharp
.WithLinger(TimeSpan.FromMilliseconds(5))  // Same, using TimeSpan
```

Higher values = more batching, higher latency.

### WithBatchSize

Maximum batch size in bytes:

```csharp
.WithBatchSize(65536)  // 64KB batches
```

## Compression

### UseCompression / Specific Methods

Enable message compression:

```csharp
.UseLz4Compression()    // Fast, good ratio (recommended)
.UseZstdCompression()   // Best ratio, more CPU
.UseSnappyCompression() // Very fast, lower ratio
.UseGzipCompression()   // Compatible, slower

// Or specify directly
.UseCompression(CompressionType.Lz4)
```

## Partitioning

### WithPartitioner

Control how messages are assigned to partitions:

```csharp
.WithPartitioner(PartitionerType.Default)     // Hash key or sticky null keys
.WithPartitioner(PartitionerType.Sticky)      // Stick null keys for batching
.WithPartitioner(PartitionerType.RoundRobin)  // Even distribution
.WithPartitioner(PartitionerType.ConsistentRandom) // librdkafka default
.WithPartitioner(PartitionerType.Fnv1ARandom)      // librdkafka/Sarama-compatible
```

## Transactions

### WithTransactionalId

Enable transactional producer:

```csharp
.WithTransactionalId("my-service-tx-1")
```

Must be unique per producer instance.

### WithTwoPhaseCommit

Enable KIP-939 two-phase commit participation for transactional producers:

```csharp
.WithTransactionalId("my-service-tx-1")
.WithTwoPhaseCommit()
```

Requires broker support for `transaction.version` 3 and `InitProducerId` v6.

## Security

### UseTls

Enable TLS encryption:

```csharp
.UseTls()                    // Basic TLS
.UseTls(tlsConfig)           // Custom TLS config
.UseMutualTls(caCert, clientCert, clientKey)  // mTLS
```

### SASL Authentication

```csharp
.WithSaslPlain("username", "password")
.WithSaslScramSha256("username", "password")
.WithSaslScramSha512("username", "password")
.WithGssapi(gssapiConfig)
.WithOAuthBearer(oauthConfig)
.WithOAuthBearerJwtBearer(options =>
{
    options.TokenEndpoint = "https://auth.example.com/oauth2/token";
    options.ClientId = "my-kafka-client";
    options.PrivateKey = rsaOrEcdsaPrivateKey;
    options.Audience = "kafka";
    options.Scopes = ["kafka:produce"];
})
```

## Serialization

### WithKeySerializer / WithValueSerializer

Custom serializers:

```csharp
.WithKeySerializer(new JsonSerializer<OrderKey>())
.WithValueSerializer(new JsonSerializer<Order>())
```

## Networking

### WithConnectionsPerBroker

Number of TCP connections to each broker:

```csharp
.WithConnectionsPerBroker(3)  // 3 parallel connections per broker
```

Default: 1. Must be 1 for idempotent producers (partition affinity requires a fixed connection).

### WithConnectionsMaxIdle

Maximum time an unused broker connection stays open before the client closes it:

```csharp
.WithConnectionsMaxIdle(TimeSpan.FromMinutes(9)) // Default: 540000ms
.WithConnectionsMaxIdle(Timeout.InfiniteTimeSpan) // Disable idle reaping
```

The default is 9 minutes, slightly below Kafka's broker-side `connections.max.idle.ms` default of 10 minutes. This lets Dekaf close unused connections first and avoid a request racing a broker idle close.

### WithConnectionTimeout

Maximum time allowed for socket connection setup, including TLS and SASL handshakes:

```csharp
.WithConnectionTimeout(TimeSpan.FromSeconds(10))
```

### WithTcpKeepAlive

Enable, disable, or tune TCP keepalive probes:

```csharp
.WithTcpKeepAlive(false) // Disable keepalive
.WithTcpKeepAlive(TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30), retryCount: 3)
```

### WithRemoteCertificateValidationCallback

Attach a custom TLS certificate validation callback for pinning or private PKI.
Setting a callback enables TLS for the connection.

```csharp
.WithRemoteCertificateValidationCallback((sender, cert, chain, errors) =>
    errors == SslPolicyErrors.None)
```

### WithAdaptiveConnections

Configure adaptive connection scaling. When sustained buffer backpressure is detected, the producer automatically adds connections per broker to increase drain throughput:

```csharp
// Use defaults (max 10 connections per broker)
.WithAdaptiveConnections()

// Custom maximum
.WithAdaptiveConnections(maxConnections: 5)
```

Adaptive scaling is **enabled by default** for non-idempotent producers. It monitors three signals before scaling up:
- **Pressure delta**: at least 100 buffer-full events since the last check
- **Utilization**: buffer is over 80% full
- **Cooldown**: at least 30 seconds since the last scale-up

Connections are only scaled up, never down. Connections added during a traffic spike persist for the lifetime of the producer. Idempotent producers ignore this setting.

### WithoutAdaptiveConnections

Disable adaptive scaling and use a fixed connection count:

```csharp
.WithoutAdaptiveConnections()
```

### WithBufferMemory

Maximum memory the producer uses for buffering unsent messages:

```csharp
.WithBufferMemory(256 * 1024 * 1024)  // 256MB
```

Default: 2GB. When the buffer is full, `ProduceAsync` and `Send` block until space is freed (controlled by `WithMaxBlockMs`). Increase if profiling shows significant time in backpressure waits; decrease in memory-constrained environments.

### WithSocketSendBufferBytes / WithSocketReceiveBufferBytes

TCP socket buffer sizes:

```csharp
.WithSocketSendBufferBytes(1_048_576)    // 1MB send buffer
.WithSocketReceiveBufferBytes(1_048_576) // 1MB receive buffer
```

## Observability

### WithLoggerFactory

Enable logging:

```csharp
.WithLoggerFactory(loggerFactory)
```

## All Options Reference

| Method | Default | Description |
|--------|---------|-------------|
| `WithBootstrapServers` | (required) | Broker addresses |
| `WithClientId` | "dekaf-producer" | Client identifier |
| `WithClientDnsLookup` | UseAllDnsIps | DNS lookup mode |
| `WithAcks` | All | Acknowledgment mode |
| `WithLinger` | 0ms | Batch wait time |
| `WithBatchSize` | 1048576 | Max batch size in bytes |
| `WithIdempotence` | true | Prevent duplicates |
| `WithTransactionalId` | null | Transaction ID |
| `WithTransactionTimeout` | 60000ms | Transaction timeout |
| `UseCompression` | None | Compression codec |
| `WithCompressionLevel` | null | Codec-specific compression level |
| `WithPartitioner` | Default | Partition strategy |
| `WithConnectionsPerBroker` | 1 | TCP connections per broker |
| `WithConnectionsMaxIdle` | 540000ms | Close unused broker connections; `Timeout.InfiniteTimeSpan` disables |
| `WithConnectionTimeout` | 30000ms | Socket connection setup timeout |
| `WithTcpKeepAlive` | enabled | TCP keepalive; 2m idle, 30s interval, 3 retries |
| `WithAdaptiveConnections` | enabled (max 10) | Auto-scale connections under load |
| `WithoutAdaptiveConnections` | - | Disable adaptive scaling |
| `WithBufferMemory` | auto-tuned | Max buffer for unsent messages |
| `WithMaxBlock` | 60000ms | Max time produce calls wait for metadata or buffer space |
| `WithDeliveryTimeout` | 120000ms | Max time for delivery success or failure |
| `WithRequestTimeout` | 30000ms | Per-request timeout |
| `WithSocketSendBufferBytes` | OS default | TCP send buffer size |
| `WithSocketReceiveBufferBytes` | OS default | TCP receive buffer size |
| `UseTls` | false | Enable TLS |
| `WithRemoteCertificateValidationCallback` | null | Custom TLS certificate validation |
| `WithKeySerializer` | inferred | Key serializer |
| `WithValueSerializer` | inferred | Value serializer |
