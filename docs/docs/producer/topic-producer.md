---
sidebar_position: 2
---

# Topic-Specific Producers

When your application produces to a single topic, you can use `ITopicProducer<TKey, TValue>` for a cleaner API. It binds to a specific topic at construction time, so you don't need to specify the topic on every call.

## Creating a Topic Producer

### Direct Creation

The simplest way to create a topic producer:

```csharp
await using var producer = Dekaf.CreateTopicProducer<string, string>(
    "localhost:9092", "orders");

await producer.ProduceAsync("order-123", orderJson);
```

### From the Builder

Use the builder for more configuration options:

```csharp
await using var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.All)
    .EnableIdempotence()
    .BuildForTopic("orders");

await producer.ProduceAsync("order-123", orderJson);
```

### From an Existing Producer

Create topic producers from a shared base producer. This is useful when you have a few fixed topics but want to share connections and resources:

```csharp
await using var baseProducer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.All)
    .Build();

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
await producer.ProduceAsync("key", "value");
await producer.ProduceAsync("key2", "value2");
```

## Available Methods

### ProduceAsync

Send a message and wait for acknowledgment:

```csharp
// Key and value
var metadata = await producer.ProduceAsync("key", "value");

// With headers
var metadata = await producer.ProduceAsync("key", "value", headers);

// To a specific partition
var metadata = await producer.ProduceAsync(partition: 2, "key", "value");

// Full control with TopicProducerMessage
var metadata = await producer.ProduceAsync(new TopicProducerMessage<string, string>
{
    Key = "key",
    Value = "value",
    Headers = headers,
    Partition = 2,
    Timestamp = DateTimeOffset.UtcNow
});
```

### Send (Fire-and-Forget)

Send without waiting for acknowledgment:

```csharp
// Basic fire-and-forget
producer.Send("key", "value");

// With headers
producer.Send("key", "value", headers);

// With delivery callback
producer.Send("key", "value", (metadata, error) =>
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
var results = await producer.ProduceAllAsync(new[]
{
    ("key1", "value1"),
    ("key2", "value2"),
    ("key3", "value3")
});

// With TopicProducerMessage for full control
var results = await producer.ProduceAllAsync(new[]
{
    new TopicProducerMessage<string, string> { Key = "key1", Value = "value1" },
    new TopicProducerMessage<string, string> { Key = "key2", Value = "value2", Partition = 0 }
});
```

### FlushAsync

Ensure all pending messages are delivered:

```csharp
await producer.FlushAsync();
```

## Disposal Semantics

The disposal behavior depends on how the topic producer was created:

| Creation Method | On Dispose |
|----------------|------------|
| `CreateTopicProducer()` | Disposes underlying producer |
| `BuildForTopic()` | Disposes underlying producer |
| `ForTopic()` | Does NOT dispose base producer |

This allows safe resource sharing:

```csharp
await using var baseProducer = Dekaf.CreateProducer<string, string>("localhost:9092");

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
// Registration
services.AddSingleton<ITopicProducer<string, OrderEvent>>(sp =>
{
    return Dekaf.CreateProducer<string, OrderEvent>()
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
