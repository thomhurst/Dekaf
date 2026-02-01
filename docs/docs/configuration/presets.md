---
sidebar_position: 1
---

# Configuration Presets

Not sure which settings to use? Dekaf provides configuration presets for common scenarios. These set sensible defaults that you can override as needed.

## Producer Presets

### ForHighThroughput

Optimized for sending many messages with maximum efficiency:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()
    .Build();
```

**Settings applied:**
| Setting | Value | Effect |
|---------|-------|--------|
| Acks | Leader | Faster acknowledgment |
| LingerMs | 5 | Allows batching |
| BatchSize | 64KB | Larger batches |
| Compression | LZ4 | Reduces network usage |

**Best for:** Log aggregation, metrics, analytics, high-volume event streams

### ForLowLatency

Optimized for minimal delay between sending and delivery:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForLowLatency()
    .Build();
```

**Settings applied:**
| Setting | Value | Effect |
|---------|-------|--------|
| Acks | Leader | Fast acknowledgment |
| LingerMs | 0 | No batching delay |
| BatchSize | 16KB | Smaller batches |

**Best for:** Real-time notifications, interactive applications, low-volume critical messages

### ForReliability

Optimized for maximum durability and exactly-once semantics:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForReliability()
    .Build();
```

**Settings applied:**
| Setting | Value | Effect |
|---------|-------|--------|
| Acks | All | Wait for all replicas |
| EnableIdempotence | true | Prevent duplicates |

**Best for:** Financial transactions, order processing, any data that cannot be lost

## Consumer Presets

### ForHighThroughput

Optimized for processing many messages efficiently:

```csharp
var consumer = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .ForHighThroughput()
    .Build();
```

**Settings applied:**
| Setting | Value | Effect |
|---------|-------|--------|
| MaxPollRecords | 1000 | Larger batches |
| FetchMinBytes | 1KB | Wait for more data |
| FetchMaxWaitMs | 500ms | Allow batching |

**Best for:** Batch processing, ETL pipelines, analytics

### ForLowLatency

Optimized for processing messages as quickly as possible:

```csharp
var consumer = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .ForLowLatency()
    .Build();
```

**Settings applied:**
| Setting | Value | Effect |
|---------|-------|--------|
| MaxPollRecords | 100 | Smaller batches |
| FetchMinBytes | 1 | Return immediately |
| FetchMaxWaitMs | 100ms | Reduce waiting |

**Best for:** Real-time processing, alerts, notifications

## Overriding Preset Values

Presets are just starting points. Override any setting by calling the appropriate method after the preset:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()
    .WithAcks(Acks.All)        // Override: want reliability too
    .WithLingerMs(10)          // Override: even more batching
    .Build();
```

The order matters - later calls override earlier ones:

```csharp
// Final acks will be Leader (from ForLowLatency)
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForReliability()      // Sets Acks.All
    .ForLowLatency()       // Overrides to Acks.Leader
    .Build();
```

## Combining Presets with Security

Presets work alongside security configuration:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseTls()
    .WithSaslScramSha512("username", "password")
    .ForReliability()  // Performance preset
    .Build();
```

## Custom Presets

Create your own preset extensions for consistency across your application:

```csharp
public static class DekafPresets
{
    public static ProducerBuilder<TKey, TValue> ForOrderProcessing<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder)
    {
        return builder
            .ForReliability()
            .UseLz4Compression()
            .WithLingerMs(1);  // Slight batching for efficiency
    }

    public static ConsumerBuilder<TKey, TValue> ForOrderProcessing<TKey, TValue>(
        this ConsumerBuilder<TKey, TValue> builder)
    {
        return builder
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .ForLowLatency();
    }
}

// Usage
var producer = Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .ForOrderProcessing()
    .Build();
```

## Choosing a Preset

| Scenario | Producer Preset | Consumer Preset |
|----------|-----------------|-----------------|
| Log aggregation | ForHighThroughput | ForHighThroughput |
| Metrics collection | ForHighThroughput | ForHighThroughput |
| Real-time alerts | ForLowLatency | ForLowLatency |
| Order processing | ForReliability | ForLowLatency |
| Financial transactions | ForReliability | ForLowLatency |
| ETL pipelines | ForHighThroughput | ForHighThroughput |
| Chat messages | ForLowLatency | ForLowLatency |
| Audit logs | ForReliability | ForHighThroughput |

## What If No Preset Fits?

If none of the presets match your needs, configure settings individually:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.All)
    .WithLingerMs(2)
    .WithBatchSize(32768)
    .EnableIdempotence()
    .UseZstdCompression()
    .Build();
```

See [Producer Options](./producer-options) and [Consumer Options](./consumer-options) for all available settings.
