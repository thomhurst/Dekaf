---
sidebar_position: 10
description: "The available compression codecs, how to install and enable batch compression, and how to pick one for your throughput and CPU budget."
---

# Compression

Compression trades CPU for smaller messages. If you're moving lots of data or paying for network bandwidth, it's usually worth it.

## Available Codecs

| Codec | Package | Speed | Ratio | Best For |
|-------|---------|-------|-------|----------|
| LZ4 | `Dekaf.Compression.Lz4` | Very Fast | Good | General purpose (recommended) |
| Zstd | `Dekaf.Compression.Zstd` | Fast | Excellent | Storage optimization |
| Snappy | `Dekaf.Compression.Snappy` | Very Fast | Moderate | Low latency |
| Gzip | Built-in | Slow | Good | Compatibility |

## Installation

Install the codec package you need:

```bash
# Recommended for most use cases
dotnet add package Dekaf.Compression.Lz4

# Best compression ratio
dotnet add package Dekaf.Compression.Zstd

# Alternative fast codec
dotnet add package Dekaf.Compression.Snappy
```

Gzip is built into .NET, no additional package needed.

## Registering Codecs

Explicitly register the codecs your application needs at startup, before building any
producers or consumers. Do not rely on installing the package alone:

```csharp
using Dekaf.Compression;
using Dekaf.Compression.Lz4;
using Dekaf.Compression.Snappy;
using Dekaf.Compression.Zstd;

// Install and register only the codecs your application needs.
CompressionCodecRegistry.Default
    .AddLz4()
    .AddZstd()
    .AddSnappy();
```

Registration affects all clients in the process, so do this before building producers or
consumers. `None` and `Gzip` are built in and need no extra registration.

## Enabling Compression

### Using Convenience Methods

After registration, select the producer's compression type with the matching builder
helper. For example, install `Dekaf.Compression.Lz4` and use:

```csharp
using Dekaf;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;

CompressionCodecRegistry.Default.AddLz4();

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .UseLz4Compression()
    .BuildAsync();
```

### Using Enum

`UseCompression(...)` only selects the compression type. Register the codec first.
The producer's `ForHighThroughput()` preset also selects LZ4, so register it before
using that preset unless you override compression with `None` or `Gzip`.

```csharp
using Dekaf;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;
using Dekaf.Protocol.Records;

CompressionCodecRegistry.Default.AddLz4();

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .UseCompression(CompressionType.Lz4)
    .BuildAsync();
```

Other choices are `CompressionType.Zstd`, `CompressionType.Snappy`,
`CompressionType.Gzip`, and `CompressionType.None` (disable compression).

## How It Works

Compression happens per batch, not per message. The producer collects messages into a batch, compresses the whole thing, and sends it. The broker stores it compressed. The consumer decompresses when it reads.

What this means in practice:
- Tiny messages don't benefit much (the overhead is per-batch)
- Bigger batches compress better, so tune your `LingerMs` and `BatchSize`
- Both producer and consumer spend CPU on compression

## Choosing a Codec

### LZ4 (Recommended)

Best all-around choice for most applications:

```csharp
.UseLz4Compression()
```

- Very fast compression and decompression
- Good compression ratio (typically 2-4x)
- Low CPU overhead
- Well-suited for high-throughput scenarios

### Zstd

Best compression ratio, good for storage-sensitive scenarios:

```csharp
.UseZstdCompression()
```

- Excellent compression ratio (typically 3-5x)
- Faster than Gzip
- Good for archival or when storage is expensive
- Higher CPU than LZ4

### Snappy

Alternative fast codec:

```csharp
.UseSnappyCompression()
```

- Very fast
- Lower compression ratio than LZ4
- Good for extremely latency-sensitive cases

### Gzip

Maximum compatibility:

```csharp
.UseGzipCompression()
```

- Universally supported
- Slower than other options
- Good compression ratio
- Use when interoperating with systems that only support Gzip

## Compression and Batching

Compression works best with batching. Configure linger time to allow batches to fill:

```csharp
using Dekaf;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;

CompressionCodecRegistry.Default.AddLz4();

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .UseLz4Compression()
    .WithLinger(TimeSpan.FromMilliseconds(5)) // Wait up to 5ms to fill batches
    .WithBatchSize(65536)   // 64KB batches
    .BuildAsync();
```

## Consumer Decompression

Consumers detect the compression type from each record batch, but the matching codec
must be registered. In a consumer application reading LZ4-compressed records, install
`Dekaf.Compression.Lz4` and register it before building the consumer:

```csharp
using Dekaf;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;

CompressionCodecRegistry.Default.AddLz4();

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .SubscribeTo("events")
    .BuildAsync();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    // msg.Value is already decompressed
    Console.WriteLine(msg.Value);
}
```

:::note
Register every optional codec the consumer may encounter, using `AddLz4()`, `AddZstd()`,
or `AddSnappy()` and the matching package namespace. There is no consumer
`UseLz4Compression()` method: consumers read whichever codec each batch specifies.
Installing the package alone is not enough; a missing codec causes decompression to fail
with `NotSupportedException`.
:::

## Performance Impact

Typical performance characteristics:

| Codec | Compression Speed | Decompression Speed | Ratio |
|-------|-------------------|---------------------|-------|
| LZ4 | ~400 MB/s | ~800 MB/s | 2.1x |
| Zstd | ~200 MB/s | ~600 MB/s | 2.8x |
| Snappy | ~500 MB/s | ~1000 MB/s | 1.8x |
| Gzip | ~50 MB/s | ~200 MB/s | 2.5x |

*Actual performance varies based on data characteristics and hardware.*

## When to Use Compression

**Use compression when:**
- Network bandwidth is limited or expensive
- Storage costs matter
- Messages are text-based (JSON, XML) - compresses well
- You're sending many similar messages

**Skip compression when:**
- Messages are already compressed (images, video)
- Latency is absolutely critical
- Messages are very small (< 100 bytes)
- CPU is the bottleneck

## Example: High-Throughput with Compression

```csharp
using Dekaf;
using Dekaf.Compression;
using Dekaf.Compression.Lz4;

CompressionCodecRegistry.Default.AddLz4();

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()     // Sets batching parameters
    .UseLz4Compression()     // Add compression
    .BuildAsync();

// Send many messages
for (int i = 0; i < 1_000_000; i++)
{
    await producer.FireAsync("events", $"event-{i}", largeJsonPayload);
}

await producer.FlushAsync();
```
