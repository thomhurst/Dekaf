---
sidebar_position: 12
---

# Performance

Performance isn't an afterthought in Dekaf—it's the reason the library exists. We wrote a pure C# Kafka client specifically to get zero allocations in hot paths and avoid the overhead of crossing into native code.

## How Dekaf Stays Fast

### No Heap Allocations in Hot Paths

The critical paths—protocol serialization, message production, and consumption—don't allocate on the heap. We use `ref struct` and `Span<T>` throughout:

```csharp
// Internal protocol writer uses ref struct
public ref struct KafkaProtocolWriter
{
    private readonly IBufferWriter<byte> _output;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt32(int value)
    {
        var span = _output.GetSpan(4);
        BinaryPrimitives.WriteInt32BigEndian(span, value);
        _output.Advance(4);
    }
}
```

### Buffer Pooling

Instead of allocating fresh byte arrays, we rent them from `ArrayPool<byte>` and return them when we're done:

```csharp
// Buffers are rented from the pool, not allocated
var buffer = ArrayPool<byte>.Shared.Rent(minSize);
try
{
    // Use buffer
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

### System.IO.Pipelines

All network I/O goes through `System.IO.Pipelines`, which gives us back-pressure (so we don't blow up memory under load), zero-copy reading where possible, and efficient buffer management without manual bookkeeping.

## Tuning for Your Use Case

Different workloads need different settings. Here's how to configure Dekaf for common scenarios.

### Producer Tuning

#### High Throughput

When you need to push as many messages as possible:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()  // Preset configuration
    .Build();

// Or manual configuration
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.Leader)           // Don't wait for all replicas
    .WithLingerMs(5)                  // Batch for 5ms
    .WithBatchSize(65536)             // 64KB batches
    .WithCompression(CompressionType.Lz4)  // Fast compression
    .Build();
```

#### Low Latency

When every millisecond counts:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForLowLatency()  // Preset configuration
    .Build();

// Or manual configuration
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.Leader)
    .WithLingerMs(0)      // Send immediately
    .WithBatchSize(16384) // Smaller batches
    .Build();
```

#### Maximum Reliability

When you absolutely cannot lose a message:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForReliability()  // Preset configuration
    .Build();

// Or manual configuration
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.All)
    .WithIdempotence(true)
    .Build();
```

### Consumer Tuning

#### High Throughput

```csharp
var consumer = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .ForHighThroughput()
    .SubscribeTo("events")
    .Build();

// Process in batches
await foreach (var batch in consumer.ConsumeAsync(cts.Token).Batch(100))
{
    await ProcessBatchAsync(batch);
    await consumer.CommitAsync();
}
```

#### Low Latency

```csharp
var consumer = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .ForLowLatency()
    .SubscribeTo("events")
    .Build();
```

## Compression Trade-offs

Compression can dramatically reduce network usage, but it costs CPU. Here's how the codecs stack up:

| Codec | Speed | Ratio | CPU Usage |
|-------|-------|-------|-----------|
| None | Fastest | 1:1 | None |
| LZ4 | Very Fast | Good | Low |
| Snappy | Fast | Good | Low |
| Zstd | Medium | Best | Medium |
| Gzip | Slow | Good | High |

### Recommendations

- **High throughput**: Use LZ4 or Snappy
- **Limited bandwidth**: Use Zstd
- **CPU constrained**: Use no compression or LZ4
- **Compatibility**: Use Gzip (universal support)

```csharp
// LZ4 for balanced performance
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithCompression(CompressionType.Lz4)
    .Build();
```

## What to Expect

These numbers are from our benchmarks on modern hardware. Your mileage will vary based on network, broker configuration, and message size—but they give you a rough idea:

### Message Production

| Scenario | Messages/sec | Latency (p99) |
|----------|-------------|---------------|
| Fire-and-forget | 500,000+ | < 1ms |
| Acks=Leader | 200,000+ | < 5ms |
| Acks=All | 100,000+ | < 10ms |

### Message Consumption

| Scenario | Messages/sec |
|----------|-------------|
| Single partition | 300,000+ |
| Multiple partitions | 500,000+ |

### Memory Usage

The zero-allocation design pays off here. Once warmed up, Dekaf doesn't trigger garbage collection during normal operation. Your memory usage stays flat and predictable, even under heavy load. No Gen2 collections sneaking in to add latency spikes.

## Keeping an Eye on Things

### Built-in Metrics

Hook into Dekaf's metrics to see what's happening:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithMetrics(metrics =>
    {
        metrics.OnMessageProduced += (topic, partition, latency) =>
        {
            // Record metrics
        };
    })
    .Build();
```

### Logging

Enable debug logging for performance troubleshooting:

```csharp
var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithLoggerFactory(loggerFactory)
    .Build();
```

## Common Mistakes to Avoid

### Creating Clients Per Request

Producers and consumers are expensive to create—they establish connections, negotiate protocol versions, and fetch metadata. Create them once and reuse:

```csharp
// Good - singleton
public class MessageService
{
    private readonly IKafkaProducer<string, string> _producer;

    public MessageService(IKafkaProducer<string, string> producer)
    {
        _producer = producer;
    }
}

// Bad - creating per request
public async Task SendAsync(string message)
{
    await using var producer = Kafka.CreateProducer<string, string>()
        .WithBootstrapServers("localhost:9092")
        .Build();
    // ...
}
```

### Awaiting Each Message Individually

If you have a batch of messages, don't await each one in a loop:

```csharp
// Slower - waits for each message before sending the next
foreach (var msg in messages)
{
    await producer.ProduceAsync(msg);
}

// Faster - sends all messages concurrently
var results = await producer.ProduceAllAsync(messages);
```

### Over-Engineering Reliability

Don't use `Acks.All` when you don't need it. For logs and metrics, `Acks.None` or `Acks.Leader` is usually fine:

```csharp
// For logs/metrics where some loss is acceptable
.WithAcks(Acks.None)

// For most use cases
.WithAcks(Acks.Leader)

// For critical data
.WithAcks(Acks.All)
```

### Ignoring Batch Settings

The default batch settings are conservative. If you can tolerate some latency, bump up the linger time:

```csharp
// High latency tolerance, maximize throughput
.WithLingerMs(100)
.WithBatchSize(1048576)

// Low latency required
.WithLingerMs(0)
.WithBatchSize(16384)
```

### Using JSON for Everything

JSON is convenient but not always the right choice. Binary formats are smaller and faster:

| Format | Speed | Size | Schema |
|--------|-------|------|--------|
| Raw bytes | Fastest | Smallest | No |
| JSON | Fast | Large | Optional |
| Protobuf | Fast | Small | Required |
| Avro | Medium | Small | Required |

## Profiling Your Application

If you're not hitting the performance you expect, measure before optimizing.

### BenchmarkDotNet

Set up proper benchmarks to measure your specific patterns:

```csharp
[MemoryDiagnoser]
public class MyBenchmarks
{
    private IKafkaProducer<string, string> _producer;

    [GlobalSetup]
    public void Setup()
    {
        _producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();
    }

    [Benchmark]
    public async Task ProduceMessage()
    {
        await _producer.ProduceAsync("topic", "key", "value");
    }
}
```

### dotnet-counters

Monitor runtime metrics:

```bash
dotnet-counters monitor --process-id <pid> --counters System.Runtime
```

### dotnet-trace

Capture detailed traces:

```bash
dotnet-trace collect --process-id <pid> --providers Microsoft-DotNETCore-SampleProfiler
```
