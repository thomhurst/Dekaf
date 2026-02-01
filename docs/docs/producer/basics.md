---
sidebar_position: 1
---

# Producer Basics

The producer sends messages to Kafka. Let's cover the essentials: creating a producer, sending messages, and understanding delivery guarantees.

## Creating a Producer

Use the fluent builder API to create a producer:

```csharp
using Dekaf;

await using var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();
```

The type parameters `<TKey, TValue>` define the types for message keys and values. Dekaf includes built-in serializers for common types.

## Sending Messages

### With Acknowledgment (Recommended)

Use `ProduceAsync` to send a message and wait for the broker to acknowledge it:

```csharp
var metadata = await producer.ProduceAsync("my-topic", "key", "value");

Console.WriteLine($"Partition: {metadata.Partition}");
Console.WriteLine($"Offset: {metadata.Offset}");
Console.WriteLine($"Timestamp: {metadata.Timestamp}");
```

This method returns a `ValueTask<RecordMetadata>` containing details about where the message was stored.

### Simple Overload

For quick sends to a topic:

```csharp
await producer.ProduceAsync("my-topic", "key", "value");
```

### With a Message Object

For more control, create a `ProducerMessage`:

```csharp
var message = new ProducerMessage<string, string>
{
    Topic = "my-topic",
    Key = "key",
    Value = "value",
    Headers = Headers.Create().Add("trace-id", traceId),
    Partition = 0,  // Optional: specific partition
    Timestamp = DateTimeOffset.UtcNow  // Optional: custom timestamp
};

await producer.ProduceAsync(message);
```

Or use the factory method:

```csharp
var message = ProducerMessage<string, string>.Create("my-topic", "key", "value");
await producer.ProduceAsync(message);
```

## Delivery Guarantees (Acks)

The `Acks` setting controls when the broker considers a message "delivered":

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.All)  // Wait for all in-sync replicas
    .Build();
```

| Acks Value | Behavior | Trade-off |
|------------|----------|-----------|
| `Acks.None` | Don't wait for any acknowledgment | Fastest, but messages can be lost |
| `Acks.Leader` | Wait for partition leader only | Good balance of speed and safety |
| `Acks.All` | Wait for all in-sync replicas | Safest, but slower |

:::tip
For most applications, use `Acks.All` (the default) to ensure messages aren't lost if a broker fails.
:::

## Batching

Dekaf automatically batches messages for efficiency. You can tune the batching behavior:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithLingerMs(5)       // Wait up to 5ms to collect more messages
    .WithBatchSize(65536)  // Maximum batch size in bytes
    .Build();
```

- **`LingerMs`** - How long to wait before sending a batch that isn't full. Higher values = better batching but more latency.
- **`BatchSize`** - Maximum size of a batch. Larger batches are more efficient but use more memory.

## Compression

Enable compression to reduce network usage:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .UseLz4Compression()  // Fast and good compression
    .Build();
```

Available compression methods:

| Method | Package | Characteristics |
|--------|---------|-----------------|
| `UseLz4Compression()` | `Dekaf.Compression.Lz4` | Fast, good compression ratio |
| `UseZstdCompression()` | `Dekaf.Compression.Zstd` | Best compression ratio |
| `UseSnappyCompression()` | `Dekaf.Compression.Snappy` | Very fast, moderate compression |
| `UseGzipCompression()` | Built-in | Widely compatible, slower |

:::tip
LZ4 is recommended for most use cases - it provides a good balance of speed and compression ratio.
:::

## Idempotent Producer

Enable idempotence to prevent duplicate messages during retries:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .EnableIdempotence()
    .Build();
```

With idempotence enabled:
- Retries won't create duplicates
- Messages are delivered in order
- Requires `Acks.All` (set automatically)

## Error Handling

`ProduceAsync` throws exceptions for delivery failures:

```csharp
try
{
    await producer.ProduceAsync("my-topic", "key", "value");
}
catch (ProduceException ex)
{
    Console.WriteLine($"Delivery failed: {ex.Message}");
    Console.WriteLine($"Topic: {ex.Topic}");
    Console.WriteLine($"Retriable: {ex.IsRetriable}");
}
```

Check `IsRetriable` to determine if retrying might succeed.

## Producer Lifecycle

Producers aren't cheap to create—they establish TCP connections, negotiate protocol versions, and fetch topic metadata. So:

1. **Create once, reuse forever** - Don't create a producer per message or per request
2. **Use `await using`** - This flushes pending messages and closes connections cleanly
3. **Thread-safe** - Call it from anywhere, it handles the synchronization

```csharp
// Good: Single producer for the application lifetime
public class OrderService
{
    private readonly IKafkaProducer<string, string> _producer;

    public OrderService(IKafkaProducer<string, string> producer)
    {
        _producer = producer;
    }

    public async Task ProcessOrderAsync(Order order)
    {
        await _producer.ProduceAsync("orders", order.Id, JsonSerializer.Serialize(order));
    }
}
```

## Flushing

When disposing a producer, pending messages are automatically flushed. You can also flush manually:

```csharp
// Flush all pending messages
await producer.FlushAsync();
```

This is useful when you need to ensure all messages are sent before proceeding.
