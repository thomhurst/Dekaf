---
sidebar_position: 1
description: "Ready-made producer and consumer configurations for common scenarios, how to override individual values, and how to define your own preset."
---

# Configuration Presets

Not sure which settings to use? Dekaf provides configuration presets for common scenarios. These set sensible defaults that you can override as needed.

## Producer Presets

### ForHighThroughput

Optimized for sending many messages with maximum efficiency. This producer preset
selects LZ4: install `Dekaf.Compression.Lz4` and explicitly register it at startup, as
shown below. See [Compression](../compression.md) for registration in consumer applications.

```csharp
using Dekaf;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;

CompressionCodecRegistry.Default.AddLz4();

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()
    .BuildAsync();
```

**Settings applied:**
| Setting | Value | Effect |
|---------|-------|--------|
| Acks | Leader (All by default, see below) | Faster acknowledgment when idempotence is disabled |
| LingerMs | 100 | Allows batching |
| BatchSize | 2MB | Larger batches |
| Compression | LZ4 | Reduces network usage |

**Best for:** Log aggregation, metrics, analytics, high-volume event streams

### ForLowLatency

Optimized for minimal delay between sending and delivery:

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForLowLatency()
    .BuildAsync();
```

**Settings applied:**
| Setting | Value | Effect |
|---------|-------|--------|
| Acks | Leader (All by default, see below) | Fast acknowledgment when idempotence is disabled |
| LingerMs | 0 | No batching delay |
| BatchSize | 256KB | Smaller batches |

**Best for:** Real-time notifications, interactive applications, low-volume critical messages

### Acks and Idempotence

`ForHighThroughput()` and `ForLowLatency()` set `Acks.Leader`, but idempotence is enabled by default and
idempotent producers require `Acks.All`. When idempotence is enabled, `BuildAsync()` logs a warning and
overrides acks to `All`, so the final acks for these presets is `All`. To keep `Acks.Leader`, disable
idempotence:

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForLowLatency()
    .WithIdempotence(false) // Keeps Acks.Leader; retries may write duplicates
    .BuildAsync();
```

### ForReliability

Optimized for maximum durability and exactly-once semantics:

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForReliability()
    .BuildAsync();
```

**Settings applied:**
| Setting | Value | Effect |
|---------|-------|--------|
| Acks | All | Wait for all replicas |
| EnableIdempotence | true (default, not changed by the preset) | Prevent duplicates |

**Best for:** Financial transactions, order processing, any data that cannot be lost

## Consumer Presets

### ForHighThroughput

Optimized for processing many messages efficiently:

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .ForHighThroughput()
    .BuildAsync();
```

**Settings applied:**
| Setting | Value | Effect |
|---------|-------|--------|
| MaxPollRecords | 1000 | Larger batches |
| FetchMinBytes | 1KB | Wait for more data |
| FetchMaxWaitMs | 200ms | Same as the default; higher values stall when the prefetch pipeline restarts after hitting memory limits |
| MaxPartitionFetchBytes | 4MB | Larger fetch responses reduce round-trip overhead per byte |
| FetchMaxBytes | 100MB, adapting up to 200MB | Larger fetch responses across partitions |
| FetchBufferMemory | 1000MB unless configured | Bounds queued and in-flight fetch responses (pipeline depth × adaptive fetch maximum) |
| PrefetchPipelineDepth | 5 | Prefetches aggressively to hide network latency |
| ConnectionsPerBroker | 3 (standalone consumers) | Two fetch connections plus one coordination connection |
| AdaptiveFetchSizing | Enabled | Grows fetch sizes while the consumer keeps up and shrinks them under prefetch memory pressure |
| CheckCrcs | false | Skips client-side CRC32C re-validation; re-enable with `WithCheckCrcs(true)` after the preset |

**Best for:** Batch processing, ETL pipelines, analytics

### ForLowLatency

Optimized for processing messages as quickly as possible:

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .ForLowLatency()
    .BuildAsync();
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
using Dekaf;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;

CompressionCodecRegistry.Default.AddLz4();

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()
    .WithAcks(Acks.All)        // Override: want reliability too
    .WithLinger(TimeSpan.FromMilliseconds(10)) // Override: even more batching
    .BuildAsync();
```

The order matters - later calls override earlier ones:

```csharp
using Dekaf;

// Final linger is 0 and batch size is 256KB (from ForLowLatency).
// Final acks is All: ForLowLatency sets Acks.Leader, but BuildAsync()
// overrides it to All because idempotence is enabled by default.
var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForReliability()      // Sets Acks.All
    .ForLowLatency()       // Overrides to Acks.Leader, linger 0, batch size 256KB
    .BuildAsync();
```

## Combining Presets with Security

Presets work alongside security configuration:

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseTls()
    .WithSaslScramSha512("username", "password")
    .ForReliability()  // Performance preset
    .BuildAsync();
```

## Custom Presets

Create your own preset extensions for consistency across your application:

```csharp
using Dekaf;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;

CompressionCodecRegistry.Default.AddLz4();

// Usage
var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .ForOrderProcessing()
    .BuildAsync();

public static class DekafPresets
{
    public static ProducerBuilder<TKey, TValue> ForOrderProcessing<TKey, TValue>(
        this ProducerBuilder<TKey, TValue> builder)
    {
        return builder
            .ForReliability()
            .UseLz4Compression()
            .WithLinger(TimeSpan.FromMilliseconds(1)); // Slight batching for efficiency
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
using Dekaf;
using Dekaf.Compression;
using Dekaf.Compression.Zstd;

CompressionCodecRegistry.Default.AddZstd();

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.All)
    .WithLinger(TimeSpan.FromMilliseconds(2))
    .WithBatchSize(32768)
    .WithIdempotence(true)
    .UseZstdCompression()
    .BuildAsync();
```

See [Producer Options](./producer-options) and [Consumer Options](./consumer-options) for all available settings.
