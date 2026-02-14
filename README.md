# Dekaf

**Taking the Java out of Kafka.**

Dekaf is a high-performance, pure C# Apache Kafka client for .NET 10+. No JVM, no interop, no native dependencies—just clean, modern C# all the way down.

**[View Full Documentation](https://thomhurst.github.io/Dekaf/)**

## Why Dekaf?

Unlike libraries that wrap librdkafka, Dekaf is a native .NET implementation with no external dependencies:

- **Pure C#** - No native dependencies, no interop overhead
- **Zero-allocation hot paths** - Uses `Span<T>`, `ref struct`, and object pooling for minimal GC pressure
- **Modern .NET** - Built for .NET 10+ with nullable reference types, `IAsyncEnumerable`, and all the good stuff
- **Simple API** - Intuitive fluent builders that do what you'd expect

## Getting Started

```bash
dotnet add package Dekaf
```

### Producing Messages

The simplest way to send a message:

```csharp
using Dekaf;

await using var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .BuildAsync();

// Wait for acknowledgment
var metadata = await producer.ProduceAsync("my-topic", "key", "Hello, Kafka!");
Console.WriteLine($"Sent to partition {metadata.Partition} at offset {metadata.Offset}");
```

For high-throughput scenarios where you don't need to wait:

```csharp
// Fire and forget - returns immediately
producer.Send("my-topic", "key", "value");

// Make sure everything's delivered before shutting down
await producer.FlushAsync();
```

### Topic-Specific Producers

When you're always producing to the same topic, use a topic producer for a cleaner API:

```csharp
await using var producer = Kafka.CreateTopicProducer<string, string>(
    "localhost:9092", "orders");

// No topic parameter needed
await producer.ProduceAsync("order-123", orderJson);
producer.Send("order-456", orderJson);
```

You can also create multiple topic producers that share the same connection:

```csharp
await using var baseProducer = Kafka.CreateProducer<string, string>("localhost:9092");

var orders = baseProducer.ForTopic("orders");
var events = baseProducer.ForTopic("events");

await orders.ProduceAsync("order-1", orderJson);
await events.ProduceAsync("event-1", eventJson);
```

### Consuming Messages

```csharp
using Dekaf;

await using var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-consumer-group")
    .SubscribeTo("my-topic")
    .BuildAsync();

await foreach (var message in consumer.ConsumeAsync(cancellationToken))
{
    Console.WriteLine($"Got: {message.Key} = {message.Value}");
}
```

## Configuration Presets

Not sure which settings to use? We've got you covered with presets for common scenarios:

```csharp
using Dekaf;

// Maximize throughput (batching, compression, relaxed durability)
var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()
    .BuildAsync();

// Minimize latency (no batching delay, smaller batches)
var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForLowLatency()
    .BuildAsync();

// Maximum reliability (all replicas must ack, idempotent)
var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForReliability()
    .BuildAsync();
```

You can override individual settings after applying a preset.

## Batch Production

Need to send a bunch of messages? `ProduceAllAsync` handles the tricky `ValueTask` semantics for you:

```csharp
var messages = new[]
{
    ProducerMessage<string, string>.Create("orders", "order-1", orderJson1),
    ProducerMessage<string, string>.Create("orders", "order-2", orderJson2),
    ProducerMessage<string, string>.Create("orders", "order-3", orderJson3),
};

var results = await producer.ProduceAllAsync(messages);
```

Or if all your messages go to the same topic:

```csharp
var results = await producer.ProduceAllAsync("orders", new[]
{
    ("order-1", orderJson1),
    ("order-2", orderJson2),
    ("order-3", orderJson3),
});
```

## Working with Headers

Headers are great for metadata like correlation IDs, trace context, or routing hints:

```csharp
using Dekaf;

var headers = Headers.Create()
    .Add("correlation-id", correlationId)
    .Add("source", "order-service")
    .AddIfNotNull("user-id", userId)           // Only adds if not null
    .AddIf(isRetry, "retry-count", "1");       // Only adds if condition is true

await producer.ProduceAsync("orders", orderId, orderJson, headers);
```

## Consumer LINQ Extensions

Process consumed messages with familiar LINQ-style operations:

```csharp
// Filter and limit
await foreach (var message in consumer.ConsumeAsync(ct)
    .Where(m => m.Value.Contains("important"))
    .Take(100))
{
    await ProcessAsync(message);
}

// Batch processing - great for bulk database inserts
await foreach (var batch in consumer.ConsumeAsync(ct).Batch(100))
{
    await BulkInsertAsync(batch);
    await consumer.CommitAsync();
}

// Simple processing loop
await consumer.ForEachAsync(async msg =>
{
    await HandleMessageAsync(msg);
}, cancellationToken);
```

## Offset Management

Dekaf gives you control over when offsets are committed:

```csharp
using Dekaf;

// Auto mode (default): Offsets committed automatically in the background
// Good for: Log processing, analytics, cases where losing a message is OK
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithOffsetCommitMode(OffsetCommitMode.Auto)
    .BuildAsync();

// Manual mode: You control when to commit by calling CommitAsync()
// Dekaf tracks consumed offsets for you - CommitAsync() commits the latest
// consumed position for each partition. This gives you at-least-once semantics:
// if your app crashes before committing, messages will be redelivered on restart.
// Good for: Payment processing, order handling, anything where you can't lose messages
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithOffsetCommitMode(OffsetCommitMode.Manual)
    .BuildAsync();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessAsync(msg);
    await consumer.CommitAsync();  // Commits offset for all consumed messages
}
```

## Compression

Dekaf supports all standard Kafka compression codecs. Just add the relevant package:

```bash
dotnet add package Dekaf.Compression.Lz4     # Fast, good compression
dotnet add package Dekaf.Compression.Zstd    # Best compression ratio
dotnet add package Dekaf.Compression.Snappy  # Balanced
```

Then enable it:

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .UseLz4Compression()
    .BuildAsync();
```

## Serialization

Built-in serializers handle common types automatically:

- `string`
- `byte[]` and `ReadOnlyMemory<byte>`
- `int`, `long`, `Guid`

For JSON, add the serialization package:

```bash
dotnet add package Dekaf.Serialization.Json
```

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(new JsonSerializer<Order>())
    .BuildAsync();

await producer.ProduceAsync("orders", order.Id, order);
```

## Security

### TLS

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseTls()
    .BuildAsync();
```

### SASL Authentication

```csharp
using Dekaf;

// SASL/PLAIN
var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseTls()
    .WithSaslPlain("username", "password")
    .BuildAsync();

// SASL/SCRAM-SHA-512
var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseTls()
    .WithSaslScramSha512("username", "password")
    .BuildAsync();
```

## Dependency Injection

For ASP.NET Core or other DI scenarios:

```bash
dotnet add package Dekaf.Extensions.DependencyInjection
```

```csharp
using Dekaf.Extensions.DependencyInjection;

services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers(configuration["Kafka:BootstrapServers"]!));

    dekaf.AddConsumer<string, string>(consumer => consumer
        .WithBootstrapServers(configuration["Kafka:BootstrapServers"]!)
        .WithGroupId("my-service"));
});
```

Then inject `IKafkaProducer<string, string>` or `IKafkaConsumer<string, string>` wherever you need them.

### Global Interceptors

Register cross-cutting interceptors (tracing, metrics, audit logging) that apply to all producers or consumers:

```csharp
services.AddDekaf(dekaf =>
{
    // Global interceptors apply to every producer/consumer
    dekaf.AddGlobalProducerInterceptor(typeof(TracingInterceptor<,>));
    dekaf.AddGlobalConsumerInterceptor(typeof(MetricsInterceptor<,>));

    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers("localhost:9092"));

    dekaf.AddConsumer<string, string>(consumer => consumer
        .WithBootstrapServers("localhost:9092")
        .WithGroupId("my-service"));
});
```

Global interceptors execute before per-instance interceptors, in registration order. They are constructed via `ActivatorUtilities`, so their dependencies are resolved from the DI container.

