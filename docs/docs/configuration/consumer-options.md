---
sidebar_position: 3
---

# Consumer Options

Complete reference for all consumer configuration options.

## Connection Settings

### WithBootstrapServers

Kafka broker addresses:

```csharp
.WithBootstrapServers("localhost:9092")
.WithBootstrapServers("broker1:9092,broker2:9092")
```

### WithClientId

Client identifier:

```csharp
.WithClientId("order-processor")
```

## Consumer Group Settings

### WithGroupId

Consumer group identifier (required for group consumption):

```csharp
.WithGroupId("order-processors")
```

### WithGroupInstanceId

Static membership ID for faster rebalances:

```csharp
.WithGroupInstanceId("instance-1")
```

## Offset Management

### WithOffsetCommitMode

How offsets are committed (matches Kafka's `enable.auto.commit`):

```csharp
.WithOffsetCommitMode(OffsetCommitMode.Auto)    // Automatic commit in background (default)
.WithOffsetCommitMode(OffsetCommitMode.Manual)  // You call CommitAsync() explicitly
```

### WithAutoCommitInterval

Control how often offsets are committed in Auto mode:

```csharp
.WithAutoCommitInterval(5000)                      // Commit every 5 seconds (default)
.WithAutoCommitInterval(TimeSpan.FromSeconds(5))  // Same, using TimeSpan
```

### WithAutoOffsetReset

Where to start when no committed offset exists:

```csharp
.WithAutoOffsetReset(AutoOffsetReset.Latest)    // New messages only (default)
.WithAutoOffsetReset(AutoOffsetReset.Earliest)  // From beginning
.WithAutoOffsetReset(AutoOffsetReset.None)      // Throw exception
```

## Fetch Settings

### WithMaxPollRecords

Maximum messages per poll:

```csharp
.WithMaxPollRecords(500)  // Default: 500
```

### Fetch Tuning (via presets or internal settings)

Control how data is fetched from brokers. These are set by presets:

- **FetchMinBytes** - Minimum data to return (wait for more)
- **FetchMaxWaitMs** - Maximum wait time for FetchMinBytes

## Session Settings

### WithSessionTimeout

How long before consumer is considered dead:

```csharp
.WithSessionTimeout(45000)                      // 45 seconds (default)
.WithSessionTimeout(TimeSpan.FromSeconds(45))  // Same, using TimeSpan
```

## Subscription

### SubscribeTo

Subscribe to topics during build:

```csharp
.SubscribeTo("orders")
.SubscribeTo("orders", "payments", "notifications")
```

## Rebalancing

### WithRebalanceListener

Get notified of partition changes:

```csharp
.WithRebalanceListener(new MyRebalanceListener())
```

### WithPartitionAssignmentStrategy

Partition assignment algorithm:

```csharp
.WithPartitionAssignmentStrategy(PartitionAssignmentStrategy.CooperativeSticky)  // Default
.WithPartitionAssignmentStrategy(PartitionAssignmentStrategy.Range)
.WithPartitionAssignmentStrategy(PartitionAssignmentStrategy.RoundRobin)
```

## Security

### UseTls

Enable TLS:

```csharp
.UseTls()
.UseTls(tlsConfig)
.UseMutualTls(caCert, clientCert, clientKey)
```

### SASL Authentication

```csharp
.WithSaslPlain("username", "password")
.WithSaslScramSha256("username", "password")
.WithSaslScramSha512("username", "password")
```

## Serialization

### WithKeyDeserializer / WithValueDeserializer

Custom deserializers:

```csharp
.WithKeyDeserializer(new JsonDeserializer<OrderKey>())
.WithValueDeserializer(new JsonDeserializer<Order>())
```

## Advanced Settings

### WithPartitionEof

Receive notification when reaching end of partition:

```csharp
.WithPartitionEof(true)
```

### WithIsolationLevel

For transactional reads:

```csharp
.WithIsolationLevel(IsolationLevel.ReadCommitted)    // Only committed messages
.WithIsolationLevel(IsolationLevel.ReadUncommitted)  // All messages (default)
```

## Observability

### WithLoggerFactory

```csharp
.WithLoggerFactory(loggerFactory)
```

### WithStatisticsInterval / WithStatisticsHandler

```csharp
.WithStatisticsInterval(TimeSpan.FromSeconds(30))
.WithStatisticsHandler(stats => { /* ... */ })
```

## All Options Reference

| Method | Default | Description |
|--------|---------|-------------|
| `WithBootstrapServers` | (required) | Broker addresses |
| `WithClientId` | "dekaf-consumer" | Client identifier |
| `WithGroupId` | null | Consumer group ID |
| `WithGroupInstanceId` | null | Static membership ID |
| `WithOffsetCommitMode` | Auto | Offset management mode |
| `WithAutoCommitInterval` | 5000ms | Auto-commit interval |
| `WithAutoOffsetReset` | Latest | Start position |
| `WithMaxPollRecords` | 500 | Max messages per poll |
| `WithSessionTimeout` | 45000ms | Session timeout |
| `SubscribeTo` | (none) | Topics to subscribe |
| `WithRebalanceListener` | null | Rebalance callbacks |
| `WithPartitionEof` | false | EOF notifications |
| `UseTls` | false | Enable TLS |
