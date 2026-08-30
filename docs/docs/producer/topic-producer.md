---
sidebar_position: 2
description: "ITopicProducer binds to a single topic at construction so you skip the topic argument on every call, with disposal semantics and DI wiring."
---

# Topic-Specific Producers

When your application produces to a single topic, you can use `ITopicProducer<TKey, TValue>` for a cleaner API. It binds to a specific topic at construction time, so you don't need to specify the topic on every call.

## Creating a Topic Producer

### Builder Creation

The simplest way to create a topic producer:

```csharp
using Dekaf;

await using var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .BuildForTopicAsync("orders");

await producer.ProduceAsync("order-123", orderJson);
```

### With More Options

Use the builder for more configuration options:

```csharp
using Dekaf;

await using var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.All)
    .WithIdempotence(true)
    .BuildForTopicAsync("orders");

await producer.ProduceAsync("order-123", orderJson);
```

### From an Existing Producer

Create topic producers from a shared base producer. This is useful when you have a few fixed topics but want to share connections and resources:

```csharp
using Dekaf;

await using var baseProducer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.All)
    .BuildAsync();

// Create topic-specific wrappers (they share the base producer's resources)
var ordersProducer = baseProducer.ForTopic("orders");
var eventsProducer = baseProducer.ForTopic("events");

await ordersProducer.ProduceAsync("order-123", orderJson);
await eventsProducer.ProduceAsync("event-456", eventJson);
```

## API Comparison

With a regular producer, you specify the topic on every call:

```csharp
// Regular producer - topic on every call
await producer.ProduceAsync("orders", "key", "value");
await producer.ProduceAsync("orders", "key2", "value2");
```

With a topic producer, the topic is implicit:

```csharp
// Topic producer - no topic parameter
await topicProducer.ProduceAsync("key", "value");
await topicProducer.ProduceAsync("key2", "value2");
```

## Available Methods

### ProduceAsync

Send a message and wait for acknowledgment:

```csharp
// Key and value
var basicMetadata = await topicProducer.ProduceAsync("key", "value");

// With headers
var headerMetadata = await topicProducer.ProduceAsync("key", "value", headers);

// To a specific partition
var partitionMetadata = await topicProducer.ProduceAsync(partition: 2, "key", "value");

// Full control with TopicProducerMessage
var fullMetadata = await topicProducer.ProduceAsync(new TopicProducerMessage<string, string>
{
    Key = "key",
    Value = "value",
    Headers = headers,
    Partition = 2,
    Timestamp = DateTimeOffset.UtcNow
});
```

### FireAsync (Fire-and-Forget)

Queue a message without waiting for broker acknowledgment. The returned `ValueTask` completes once local backpressure accepts the record:

```csharp
// Basic fire-and-forget
await topicProducer.FireAsync("key", "value");

// With headers
await topicProducer.FireAsync("key", "value", headers);

// With delivery callback
await topicProducer.FireAsync("key", "value", (metadata, error) =>
{
    if (error is not null)
        Console.WriteLine($"Failed: {error.Message}");
    else
        Console.WriteLine($"Delivered to partition {metadata.Partition}");
});
```

### ProduceAllAsync

Send multiple messages and wait for all acknowledgments:

```csharp
// Simple tuples
var tupleResults = await topicProducer.ProduceAllAsync(new (string? Key, string Value)[]
{
    ("key1", "value1"),
    ("key2", "value2"),
    ("key3", "value3")
});

// With TopicProducerMessage for full control
var messageResults = await topicProducer.ProduceAllAsync(new[]
{
    new TopicProducerMessage<string, string> { Key = "key1", Value = "value1" },
    new TopicProducerMessage<string, string> { Key = "key2", Value = "value2", Partition = 0 }
});
```

### FlushAsync

Ensure all pending messages are delivered:

```csharp
await topicProducer.FlushAsync();
```

## Disposal Semantics

The disposal behavior depends on how the topic producer was created:

| Creation Method | On Dispose |
|----------------|------------|
| `BuildForTopic()` / `BuildForTopicAsync()` | Disposes underlying producer |
| `ForTopic()` | Does NOT dispose base producer |

This allows safe resource sharing:

```csharp
using Dekaf;

await using var baseProducer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .Build();

var orders = baseProducer.ForTopic("orders");
var events = baseProducer.ForTopic("events");

// Disposing topic producers doesn't affect the base producer
await orders.DisposeAsync();
await events.DisposeAsync();

// Base producer still works
await baseProducer.ProduceAsync("audit", "key", "value");
```

## When to Use Topic Producers

**Use topic producers when:**
- Your service produces to one or a few fixed topics
- You want a cleaner API without repeating topic names
- You're using dependency injection and want to inject a producer per topic

**Use regular producers when:**
- You produce to many different topics dynamically
- Topic names come from runtime data (e.g., routing based on message content)
- You want maximum flexibility

## Dependency Injection Example

Topic producers work well with DI:

```csharp
using Dekaf;

// Registration
services.AddSingleton<ITopicProducer<string, OrderEvent>>(sp =>
{
    return Kafka.CreateProducer<string, OrderEvent>()
        .WithBootstrapServers(config["Kafka:BootstrapServers"])
        .WithValueSerializer(new JsonSerializer<OrderEvent>())
        .BuildForTopic("orders");
});

// Usage
public class OrderService
{
    private readonly ITopicProducer<string, OrderEvent> _producer;

    public OrderService(ITopicProducer<string, OrderEvent> producer)
    {
        _producer = producer;
    }

    public async Task PlaceOrderAsync(Order order)
    {
        var @event = new OrderEvent { OrderId = order.Id, Status = "Placed" };
        await _producer.ProduceAsync(order.Id, @event);
    }
}
```

## Performance

Topic producers have zero overhead - they simply delegate to the underlying `IKafkaProducer` with the topic embedded. All the performance optimizations (batching, compression, connection pooling) work identically.
