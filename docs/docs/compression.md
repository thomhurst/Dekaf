---
sidebar_position: 10
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

## Enabling Compression

### Using Convenience Methods

```csharp
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .UseLz4Compression()    // or UseZstdCompression(), UseSnappyCompression(), UseGzipCompression()
    .Build();
```

### Using Enum

```csharp
.UseCompression(CompressionType.Lz4)
.UseCompression(CompressionType.Zstd)
.UseCompression(CompressionType.Snappy)
.UseCompression(CompressionType.Gzip)
.UseCompression(CompressionType.None)  // Disable
```

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
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .UseLz4Compression()
    .WithLingerMs(5)        // Wait up to 5ms to fill batches
    .WithBatchSize(65536)   // 64KB batches
    .Build();
```

## Consumer Decompression

Consumers automatically detect and decompress messages. No configuration needed:

```csharp
// Consumer handles decompression automatically
var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .Build();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    // msg.Value is already decompressed
    Console.WriteLine(msg.Value);
}
```

:::note
Make sure the compression codec package is installed in your consumer application too, or decompression will fail.
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
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()     // Sets batching parameters
    .UseLz4Compression()     // Add compression
    .Build();

// Send many messages
for (int i = 0; i < 1_000_000; i++)
{
    producer.Send("events", $"event-{i}", largeJsonPayload);
}

await producer.FlushAsync();
```
