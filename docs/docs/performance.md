---
sidebar_position: 12
description: "What makes Dekaf fast, how to tune batching and compression for your workload, what throughput to expect, and the common mistakes that cost you."
---

import ComparisonChart, {ComparisonChartGrid} from '@site/src/components/ComparisonChart';

# Performance

Performance isn't an afterthought in Dekaf—it's the reason the library exists. We wrote a pure C# Kafka client specifically to get zero allocations in hot paths and avoid the overhead of crossing into native code.

## Measured against Confluent.Kafka

The published 2026-08-16 paired stress run shows the practical effect across common workloads. Bars show the measured values, with the relative comparison in brackets; see the [benchmark results](./benchmarks.md) and [stress test results](./stress-tests.md) for confidence and methodology.

<ComparisonChartGrid>

<ComparisonChart
  title="Sustained throughput"
  metric="Broker-confirmed messages per second"
  description="Higher means more broker-confirmed work completed in the same time."
  items={[{"label":"Fire-and-forget produce","dekaf":1451115,"confluent":1174152,"dekafDisplay":"1.45M msg/s (1.24×)","confluentDisplay":"1.17M msg/s"},{"label":"Acks=all produce","dekaf":1552262,"confluent":1373448,"dekafDisplay":"1.55M msg/s (1.13×)","confluentDisplay":"1.37M msg/s"},{"label":"Produce + consume round-trip","dekaf":2254179,"confluent":1211189,"dekafDisplay":"2.25M msg/s (1.86×)","confluentDisplay":"1.21M msg/s"},{"label":"Transactional produce","dekaf":1259,"confluent":172,"dekafDisplay":"1.26K msg/s (7.32×)","confluentDisplay":"172 msg/s"},{"label":"Consume messages","dekaf":1643250,"confluent":1088637,"dekafDisplay":"1.64M msg/s (1.51×)","confluentDisplay":"1.09M msg/s"}]}
/>

<ComparisonChart
  title="CPU cost per message"
  metric="Median client CPU time"
  description="CPU time needed to deliver one message; shorter bars are better."
  better="lower"
  items={[{"label":"Fire-and-forget produce","dekaf":0.75,"confluent":1.49,"dekafDisplay":"0.75 μs/msg (1.99× less)","confluentDisplay":"1.49 μs/msg"},{"label":"Acks=all produce","dekaf":0.71,"confluent":1.31,"dekafDisplay":"0.71 μs/msg (1.85× less)","confluentDisplay":"1.31 μs/msg"},{"label":"Produce + consume round-trip","dekaf":0.91,"confluent":2.34,"dekafDisplay":"0.91 μs/msg (2.57× less)","confluentDisplay":"2.34 μs/msg"},{"label":"Transactional produce","dekaf":225.52,"confluent":292.34,"dekafDisplay":"225.52 μs/msg (1.30× less)","confluentDisplay":"292.34 μs/msg"},{"label":"Consume messages","dekaf":0.8,"confluent":1.13,"dekafDisplay":"0.80 μs/msg (1.41× less)","confluentDisplay":"1.13 μs/msg"}]}
/>

</ComparisonChartGrid>

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

### Adaptive Connection Scaling

Dekaf automatically scales up TCP connections per broker when the producer detects sustained buffer backpressure. This means the producer starts with a single connection and adds more only when needed—no manual tuning required.

**How it works:**
1. When the producer's buffer fills up (messages are being produced faster than the network can send them), `ReserveMemorySync` blocks the producer thread
2. The send loop monitors buffer pressure events and utilization
3. When pressure is sustained (100+ events, >80% buffer utilization, 30-second cooldown), a new connection is added to the broker
4. More connections = more parallel TCP sends = faster buffer drainage = less backpressure

**What you see in practice:** A producer under load might start at 300K msg/sec on one connection and automatically scale to 400K+ msg/sec as connections are added, without any configuration changes.

```csharp
// Adaptive scaling is enabled by default — just build the producer
var defaultProducer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

// Customize the maximum connections
var adaptiveProducer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAdaptiveConnections(maxConnections: 5)
    .Build();

// Disable if you need fixed connection topology
var fixedConnectionProducer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithoutAdaptiveConnections()
    .WithConnectionsPerBroker(3)
    .Build();
```

**Note:** Adaptive scaling only applies to non-idempotent producers. Idempotent producers require partition affinity on a fixed connection count for sequence number ordering. Connections are only scaled up, never down—connections added during a traffic spike persist for the lifetime of the producer.

### FIFO Buffer Backpressure

When the producer's buffer is full, blocked threads are managed with a FIFO waiter queue. Each thread gets its own wait handle, and `ReleaseMemory` wakes exactly one thread at a time in FIFO order. This eliminates the "thundering herd" problem where all blocked threads wake up simultaneously and race for buffer space.

## Tuning for Your Use Case

Different workloads need different settings. Here's how to configure Dekaf for common scenarios.

### Producer Tuning

#### High Throughput

When you need to push as many messages as possible:

```csharp
using Dekaf;

var presetProducer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()  // Preset configuration
    .BuildAsync();

// Or manual configuration
var manuallyTunedProducer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.Leader)           // Don't wait for all replicas
    .WithLinger(TimeSpan.FromMilliseconds(5)) // Batch for 5ms
    .WithBatchSize(65536)             // 64KB batches
    .UseCompression(CompressionType.Lz4) // Fast compression
    .BuildAsync();
```

#### Low Latency

When every millisecond counts:

```csharp
using Dekaf;

var presetProducer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForLowLatency()  // Preset configuration
    .BuildAsync();

// Or manual configuration
var manuallyTunedProducer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.Leader)
    .WithLinger(TimeSpan.Zero) // Send immediately
    .WithBatchSize(16384) // Smaller batches
    .BuildAsync();
```

#### Maximum Reliability

When you absolutely cannot lose a message:

```csharp
using Dekaf;

var presetProducer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForReliability()  // Preset configuration
    .BuildAsync();

// Or manual configuration
var manuallyTunedProducer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.All)
    .WithIdempotence(true)
    .BuildAsync();
```

### Consumer Tuning

#### High Throughput

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .ForHighThroughput()
    .SubscribeTo("events")
    .BuildAsync();

// Process in batches
await foreach (var batch in consumer.ConsumeAsync(cts.Token).Batch(100))
{
    await ProcessBatchAsync(batch);
    await consumer.CommitAsync();
}
```

For sustained CPU-bound consumers on .NET 10 Server GC, watch throughput and GC
telemetry over time. If permanent throughput steps line up with Gen2 collections and
a changing GC heap count, benchmark with dynamic GC adaptation disabled:

```bash
DOTNET_GCDynamicAdaptationMode=0 dotnet MyConsumer.dll
```

Set the variable before process startup. Disabling DATAS keeps the Server GC heap
topology stable, which can improve sustained throughput on tightly CPU-pinned
workloads, but it may retain more managed memory. Keep the runtime default unless an
A/B test of your production-shaped workload shows the same Gen2-correlated decay.

#### Low Latency

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .ForLowLatency()
    .SubscribeTo("events")
    .BuildAsync();
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
using Dekaf;

// LZ4 for balanced performance
var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .UseCompression(CompressionType.Lz4)
    .BuildAsync();
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

### Broker Telemetry Application Metrics

Register application metrics when you want the broker client telemetry subscription to request
and receive your own measurements alongside Dekaf client metrics:

```csharp
using Dekaf;
using Dekaf.Telemetry;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .RegisterMetricForSubscription(new ApplicationTelemetryMetric(
        "com.example.queue.depth",
        ApplicationTelemetryMetricKind.Gauge,
        () => queueDepth))
    .BuildAsync();

producer.UnregisterMetricFromSubscription("com.example.queue.depth");
```

### Logging

Enable debug logging for performance troubleshooting:

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithLoggerFactory(loggerFactory)
    .BuildAsync();
```

## Common Mistakes to Avoid

### Creating Clients Per Request

Producers and consumers are expensive to create—they establish connections, negotiate protocol versions, and fetch metadata. Create them once and reuse:

```csharp
using Dekaf;

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
public class PerRequestMessageService
{
    public async Task SendAsync(string message)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .BuildAsync();
        // ...
    }
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
.WithLinger(TimeSpan.FromMilliseconds(100))
.WithBatchSize(1048576)

// Low latency required
.WithLinger(TimeSpan.Zero)
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
using Dekaf;

[MemoryDiagnoser]
public class MyBenchmarks
{
    private IKafkaProducer<string, string> _producer;

    [GlobalSetup]
    public async Task Setup()
    {
        _producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .BuildAsync();
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

#### Interpreting round-trip CPU results

The `producer-roundtrip` stress scenario measures one process across both phases: bulk
production, then consumption and strict record validation. Its CPU-per-message result is
therefore not a producer-only measurement.

A controlled 250,000-message CPU-sampling investigation compared the three Dekaf consumer
surfaces in that validation phase. Absolute times include tracing overhead, so use the
relative differences:

| Consumer surface | CPU µs/message | Ordering violations | Relative result |
|------------------|---------------:|--------------------:|-----------------|
| `ConsumeAsync` | 25.875 | 0 | Correct baseline |
| `ConsumeOneAsync` | 24.063 | 982 | 7% lower CPU, invalid result |
| `ConsumeBatchAsync` | 15.620 | 11,784 | 40% lower CPU, invalid result |

This attributes a material part of the round-trip CPU gap to per-record async-enumerable
work rather than producer acknowledgement handling. The strict stress scenario remains on
`ConsumeAsync`, the only surface that preserved per-partition ordering in this experiment.
The buffered-path correctness defect is tracked in
[#1813](https://github.com/thomhurst/Dekaf/issues/1813); lower CPU is not accepted when it
changes record order.
