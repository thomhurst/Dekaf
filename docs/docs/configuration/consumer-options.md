---
sidebar_position: 3
---

# Consumer Options

Complete reference for all consumer configuration options.

These methods are available anywhere a `ConsumerBuilder<TKey,TValue>` is used, including dependency injection registration:

```csharp
// Before: DI examples only showed connection and group settings.
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddConsumer<string, string>(consumer => consumer
        .WithBootstrapServers("localhost:9092")
        .WithGroupId("orders"));
});

// After: DI uses the full consumer builder.
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddConsumer<string, string>(consumer => consumer
        .WithBootstrapServers("localhost:9092")
        .WithGroupId("orders")
        .WithFetchMinBytes(1024)
        .WithFetchMaxBytes(50 * 1024 * 1024)
        .WithPrefetchPipelineDepth(4)
        .WithSaslScramSha512("user", "password")
        .SubscribeTo("orders"));
});
```

## Configuration Binding

`Dekaf.Extensions.DependencyInjection` can bind consumer settings from an `IConfiguration` section. Keys use `ConsumerOptions` property names:

```json
{
  "Kafka": {
    "Consumers": {
      "Orders": {
        "BootstrapServers": [
          "broker1:9092",
          "broker2:9092"
        ],
        "ClientId": "orders-consumer",
        "GroupId": "orders",
        "AutoOffsetReset": "Earliest",
        "OffsetCommitMode": "Manual",
        "FetchMinBytes": 1024,
        "FetchMaxBytes": 52428800,
        "FetchMaxWaitMs": 200,
        "MaxPollRecords": 500,
        "UseTls": true
      }
    }
  }
}
```

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddConsumer<string, Order>(
        builder.Configuration.GetSection("Kafka:Consumers:Orders"),
        consumer => consumer
            .WithValueDeserializer(new JsonDeserializer<Order>())
            .SubscribeTo("orders"));
});
```

Configuration is applied before the optional fluent callback, so fluent calls can override values from `appsettings.json`.

| Fluent API | Config key | Notes |
|------------|------------|-------|
| `WithBootstrapServers(...)` | `BootstrapServers` | Server list (prefer `params string[]` in code; comma-separated string and JSON arrays are also supported) |
| `WithClientId(...)` | `ClientId` | String |
| `WithClientDnsLookup(...)` | `ClientDnsLookup` | `UseAllDnsIps` or `ResolveCanonicalBootstrapServersOnly` |
| `WithGroupId(...)` | `GroupId` | String |
| `WithGroupInstanceId(...)` | `GroupInstanceId` | String |
| `WithGroupRemoteAssignor(...)` | `GroupRemoteAssignor` | Common values: `uniform`, `range` |
| `WithOffsetCommitMode(...)` | `OffsetCommitMode` | `Auto` or `Manual` |
| `WithAutoCommitInterval(...)` | `AutoCommitIntervalMs` | Milliseconds |
| `WithAutoOffsetReset(...)` | `AutoOffsetReset` | `Latest`, `Earliest`, `None` |
| `WithAutoOffsetResetByDuration(...)` | `AutoOffsetReset`, `AutoOffsetResetDuration` | Use `AutoOffsetReset: ByDuration` plus a duration, or Kafka-style `by_duration:PT24H` |
| `WithFetchMinBytes(...)` | `FetchMinBytes` | Bytes |
| `WithFetchMaxBytes(...)` | `FetchMaxBytes` | Bytes |
| `WithMaxPartitionFetchBytes(...)` | `MaxPartitionFetchBytes` | Bytes |
| `WithFetchMaxWait(...)` | `FetchMaxWaitMs` | Milliseconds |
| `WithFetchSessions(...)` | `EnableFetchSessions` | Boolean |
| `WithMaxPollRecords(...)` | `MaxPollRecords` | Integer |
| `WithSessionTimeout(...)` | `SessionTimeoutMs` | Milliseconds |
| `WithHeartbeatInterval(...)` | `HeartbeatIntervalMs` | Milliseconds |
| `WithIsolationLevel(...)` | `IsolationLevel` | `ReadUncommitted` or `ReadCommitted` |
| `WithPartitionEof(...)` | `EnablePartitionEof` | Boolean |
| `WithQueuedMinMessages(...)` | `QueuedMinMessages` | Integer |
| `WithQueuedMaxMessagesKbytes(...)` | `QueuedMaxMessagesKbytes` | KiB; omit to keep auto-tuning |
| `WithPrefetchPipelineDepth(...)` | `PrefetchPipelineDepth` | Integer |
| `WithConnectionsMaxIdle(...)` | `ConnectionsMaxIdleMs` | Milliseconds; `-1` disables |
| `WithConnectionsPerBroker(...)` | `ConnectionsPerBroker` | Integer |
| `WithAdaptiveConnections(...)` | `EnableAdaptiveConnections`, `MaxConnectionsPerBroker` | Set `EnableAdaptiveConnections` to `false` to disable |
| `WithAdaptiveFetchSizing(...)` | `EnableAdaptiveFetchSizing`, `AdaptiveFetchSizingOptions` | Bind nested adaptive sizing fields |
| `UseTls(...)` | `UseTls`, `TlsConfig` | `TlsConfig` can bind certificate path fields |
| `WithSaslPlain(...)` / `WithSaslScramSha512(...)` | `SaslMechanism`, `SaslUsername`, `SaslPassword` | `SaslMechanism` values match the enum names |
| `WithGssapi(...)` | `SaslMechanism`, `GssapiConfig` | Use `SaslMechanism: Gssapi` |
| `WithOAuthBearer(...)` | `SaslMechanism`, `OAuthBearerConfig` | Use `SaslMechanism: OAuthBearer` |
| `WithOAuthBearerJwtBearer(...)` | Runtime callback | Signs JWT assertions with RSA/ECDSA keys |
| `WithMetadataRecoveryStrategy(...)` | `MetadataRecoveryStrategy` | `None` or `Rebootstrap` |
| `WithMetadataRecoveryRebootstrapTrigger(...)` | `MetadataRecoveryRebootstrapTriggerMs` | Milliseconds |

Topics, deserializers, rebalance listeners, interceptors, and retry policies are objects or runtime choices, so configure those in the fluent callback.

## Connection Settings

### WithBootstrapServers

Kafka broker addresses. Prefer the typed `params string[]` overload in code; the single-string overload remains a convenience for configuration-style comma-separated values.

```csharp
.WithBootstrapServers("localhost:9092")
.WithBootstrapServers("broker1:9092", "broker2:9092")
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
.WithAutoCommitInterval(TimeSpan.FromSeconds(5))  // Same, using TimeSpan
```

### WithAutoOffsetReset

Where to start when no committed offset exists:

```csharp
.WithAutoOffsetReset(AutoOffsetReset.Latest)    // New messages only (default)
.WithAutoOffsetReset(AutoOffsetReset.Earliest)  // From beginning
.WithAutoOffsetReset(AutoOffsetReset.None)      // Throw exception
.WithAutoOffsetResetByDuration(TimeSpan.FromHours(24))
```

Configuration can use either a separate duration value:

```json
{
  "AutoOffsetReset": "ByDuration",
  "AutoOffsetResetDuration": "24:00:00"
}
```

or Kafka's ISO-8601 form:

```json
{
  "AutoOffsetReset": "by_duration:PT24H"
}
```

## Fetch Settings

### WithMaxPollRecords

Maximum messages per poll:

```csharp
.WithMaxPollRecords(500)  // Default: 500
```

### Fetch Tuning

Control how data is fetched from brokers:

```csharp
.WithFetchMinBytes(1024)
.WithFetchMaxBytes(50 * 1024 * 1024)
.WithMaxPartitionFetchBytes(4 * 1024 * 1024)
.WithFetchMaxWait(TimeSpan.FromMilliseconds(200))
.WithFetchSessions(enabled: true)
```

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

### SubscribeToPattern

Subscribe with a broker-side topic name pattern:

```csharp
.SubscribeToPattern("orders-.*")
```

Kafka evaluates the pattern on the broker using RE2/J-compatible syntax. Dekaf sends the pattern as-is; .NET regular expression syntax is not translated.

Server-side pattern subscription requires Kafka 4.1+ brokers with `ConsumerGroupHeartbeat` v1. Use `Subscribe(Func<string, bool>)` on `IKafkaConsumer` when you need arbitrary .NET predicates or compatibility with older brokers. That overload remains client-side and refreshes metadata to find matching topics.

## Rebalancing

### WithRebalanceListener

Get notified of partition changes. If the listener also implements
`IPartitionStopListener`, `OnPartitionsStoppedAsync` runs during graceful
`CloseAsync` or `DisposeAsync` with the current assignment before final
auto-commit, `LeaveGroup`, assignment cleanup, and resource disposal:

```csharp
.WithRebalanceListener(new MyRebalanceListener())
```

## Networking

### WithConnectionsMaxIdle

Maximum time an unused broker connection stays open before the client closes it:

```csharp
.WithConnectionsMaxIdle(TimeSpan.FromMinutes(9)) // Default: 540000ms
.WithConnectionsMaxIdle(Timeout.InfiniteTimeSpan) // Disable idle reaping
```

The default is 9 minutes, slightly below Kafka's broker-side `connections.max.idle.ms` default of 10 minutes. Connections with in-flight requests are not reaped.

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
.WithOAuthBearerJwtBearer(options =>
{
    options.TokenEndpoint = "https://auth.example.com/oauth2/token";
    options.ClientId = "my-kafka-client";
    options.PrivateKey = rsaOrEcdsaPrivateKey;
    options.Audience = "kafka";
    options.Scopes = ["kafka:consume"];
})
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

## All Options Reference

| Method | Default | Description |
|--------|---------|-------------|
| `WithBootstrapServers` | (required) | Broker addresses |
| `WithClientId` | "dekaf-consumer" | Client identifier |
| `WithClientDnsLookup` | UseAllDnsIps | DNS lookup mode |
| `WithGroupId` | null | Consumer group ID |
| `WithGroupInstanceId` | null | Static membership ID |
| `WithOffsetCommitMode` | Auto | Offset management mode |
| `WithAutoCommitInterval` | 5000ms | Auto-commit interval |
| `WithAutoOffsetReset`, `WithAutoOffsetResetByDuration` | Latest | Start position |
| `WithFetchMinBytes` | 1 | Minimum fetch bytes |
| `WithFetchMaxBytes` | 52428800 | Maximum total fetch bytes |
| `WithMaxPartitionFetchBytes` | 1048576 | Maximum fetch bytes per partition |
| `WithFetchMaxWait` | 200ms | Maximum fetch wait |
| `WithFetchSessions` | true | Enable incremental fetch sessions |
| `WithMaxPollRecords` | 500 | Max messages per poll |
| `WithSessionTimeout` | 45000ms | Session timeout |
| `WithHeartbeatInterval` | 3000ms | Group heartbeat interval |
| `SubscribeTo` | (none) | Topics to subscribe |
| `SubscribeToPattern` | (none) | Broker-side topic regex subscription; Kafka 4.1+ |
| `WithRebalanceListener` | null | Rebalance callbacks |
| `WithPartitionEof` | false | EOF notifications |
| `WithQueuedMinMessages` | 100000 | Prefetch target count |
| `WithQueuedMaxMessagesKbytes` | auto-tuned | Prefetch memory limit |
| `WithPrefetchPipelineDepth` | 3 | Overlapping prefetch operations |
| `WithConnectionsMaxIdle` | 540000ms | Close unused broker connections; `Timeout.InfiniteTimeSpan` disables |
| `WithConnectionsPerBroker` | 2 | TCP connections per broker |
| `WithAdaptiveConnections` | enabled (max 4) | Auto-scale connections under load |
| `UseTls` | false | Enable TLS |
