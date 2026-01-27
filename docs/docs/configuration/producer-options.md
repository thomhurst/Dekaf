---
sidebar_position: 2
---

# Producer Options

Complete reference for all producer configuration options.

## Connection Settings

### WithBootstrapServers

Kafka broker addresses for initial connection:

```csharp
// Single server
.WithBootstrapServers("localhost:9092")

// Multiple servers (comma-separated)
.WithBootstrapServers("broker1:9092,broker2:9092,broker3:9092")

// Multiple servers (params)
.WithBootstrapServers("broker1:9092", "broker2:9092", "broker3:9092")
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

### EnableIdempotence

Prevents duplicate messages during retries:

```csharp
.EnableIdempotence()
```

Automatically sets `Acks.All` and enables sequence numbers.

## Batching Settings

### WithLingerMs / WithLinger

Time to wait before sending a batch:

```csharp
.WithLingerMs(5)                    // Wait up to 5ms
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
.WithPartitioner(PartitionerType.Default)     // Hash key or round-robin
.WithPartitioner(PartitionerType.Sticky)      // Stick to partition for batching
.WithPartitioner(PartitionerType.RoundRobin)  // Even distribution
```

## Transactions

### WithTransactionalId

Enable transactional producer:

```csharp
.WithTransactionalId("my-service-tx-1")
```

Must be unique per producer instance.

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
```

## Serialization

### WithKeySerializer / WithValueSerializer

Custom serializers:

```csharp
.WithKeySerializer(new JsonSerializer<OrderKey>())
.WithValueSerializer(new JsonSerializer<Order>())
```

## Observability

### WithLoggerFactory

Enable logging:

```csharp
.WithLoggerFactory(loggerFactory)
```

### WithStatisticsInterval / WithStatisticsHandler

Enable periodic statistics:

```csharp
.WithStatisticsInterval(TimeSpan.FromSeconds(30))
.WithStatisticsHandler(stats =>
{
    Console.WriteLine($"Messages sent: {stats.TotalMessagesSent}");
})
```

## All Options Reference

| Method | Default | Description |
|--------|---------|-------------|
| `WithBootstrapServers` | (required) | Broker addresses |
| `WithClientId` | "dekaf-producer" | Client identifier |
| `WithAcks` | All | Acknowledgment mode |
| `WithLingerMs` | 0 | Batch wait time (ms) |
| `WithBatchSize` | 16384 | Max batch size (bytes) |
| `EnableIdempotence` | false | Prevent duplicates |
| `WithTransactionalId` | null | Transaction ID |
| `UseCompression` | None | Compression codec |
| `WithPartitioner` | Default | Partition strategy |
| `UseTls` | false | Enable TLS |
| `WithKeySerializer` | (auto) | Key serializer |
| `WithValueSerializer` | (auto) | Value serializer |
