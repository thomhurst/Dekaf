# Dekaf

**Taking the Java out of Kafka.**

Dekaf is a high-performance, pure C# Apache Kafka client for .NET 10+. No JVM, no interop, no native dependencies—just clean, modern C# all the way down.

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
await using var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

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
await using var producer = Dekaf.CreateTopicProducer<string, string>(
    "localhost:9092", "orders");

// No topic parameter needed
await producer.ProduceAsync("order-123", orderJson);
producer.Send("order-456", orderJson);
```

You can also create multiple topic producers that share the same connection:

```csharp
await using var baseProducer = Dekaf.CreateProducer<string, string>("localhost:9092");

var orders = baseProducer.ForTopic("orders");
var events = baseProducer.ForTopic("events");

await orders.ProduceAsync("order-1", orderJson);
await events.ProduceAsync("event-1", eventJson);
```

### Consuming Messages

```csharp
await using var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-consumer-group")
    .SubscribeTo("my-topic")
    .Build();

await foreach (var message in consumer.ConsumeAsync(cancellationToken))
{
    Console.WriteLine($"Got: {message.Key} = {message.Value}");
}
```

## Configuration Presets

Not sure which settings to use? We've got you covered with presets for common scenarios:

```csharp
// Maximize throughput (batching, compression, relaxed durability)
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()
    .Build();

// Minimize latency (no batching delay, smaller batches)
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForLowLatency()
    .Build();

// Maximum reliability (all replicas must ack, idempotent)
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForReliability()
    .Build();
```

You can always override individual settings after applying a preset:

```csharp
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()
    .WithAcks(Acks.All)  // Override just this one setting
    .Build();
```

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

Dekaf gives you control over when offsets are committed. The `OffsetCommitMode` makes this easy to understand:

```csharp
// Auto mode (default): Offsets committed automatically in the background
// Good for: Log processing, analytics, cases where losing a message is OK
var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithOffsetCommitMode(OffsetCommitMode.Auto)
    .Build();

// ManualCommit mode: You control when to commit, but offsets are tracked for you
// Good for: At-least-once processing where you commit after handling
var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithOffsetCommitMode(OffsetCommitMode.ManualCommit)
    .Build();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessAsync(msg);
    await consumer.CommitAsync();  // Commit after processing
}

// Manual mode: Full control - you must call StoreOffset() AND CommitAsync()
// Good for: Exactly-once semantics, selective acknowledgment
var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithOffsetCommitMode(OffsetCommitMode.Manual)
    .Build();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    if (await TryProcessAsync(msg))
    {
        consumer.StoreOffset(msg);  // Only store if processing succeeded
    }
    await consumer.CommitAsync();
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
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .UseLz4Compression()
    .Build();
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
var producer = Dekaf.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(new JsonSerializer<Order>())
    .Build();

await producer.ProduceAsync("orders", order.Id, order);
```

## Security

### TLS

```csharp
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseTls()
    .Build();
```

### SASL Authentication

```csharp
// SASL/PLAIN
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseTls()
    .WithSaslPlain("username", "password")
    .Build();

// SASL/SCRAM-SHA-512
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("kafka.example.com:9093")
    .UseTls()
    .WithSaslScramSha512("username", "password")
    .Build();
```

## Dependency Injection

For ASP.NET Core or other DI scenarios:

```bash
dotnet add package Dekaf.Extensions.DependencyInjection
```

```csharp
services.AddDekafProducer<string, string>(builder => builder
    .WithBootstrapServers(configuration["Kafka:BootstrapServers"])
    .ForReliability());

services.AddDekafConsumer<string, string>(builder => builder
    .WithBootstrapServers(configuration["Kafka:BootstrapServers"])
    .WithGroupId("my-service")
    .SubscribeTo("events"));
```

## Performance Tips

1. **Reuse producers** - Creating a producer is expensive. Create one and reuse it.

2. **Use `Send()` for fire-and-forget** - If you don't need acknowledgment, `Send()` is much faster than `ProduceAsync()`.

3. **Batch your commits** - Don't commit after every message. Process batches and commit periodically.

4. **Enable compression** - LZ4 adds minimal CPU overhead but can significantly reduce network traffic.

5. **Tune batch settings** - For throughput, increase `LingerMs` and `BatchSize`. For latency, keep them low.

## Contributing

Found a bug? Have an idea? We'd love to hear from you! Open an issue or submit a PR.

## License

MIT License - see [LICENSE](LICENSE) for details.

---

Built with care for .NET developers who just want Kafka to work. No Java required.
