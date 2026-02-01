---
sidebar_position: 5
---

# Partitioning

Kafka topics are divided into partitions for parallelism and scalability. Understanding how messages are assigned to partitions is important for both performance and ordering guarantees.

## How Partitioning Works

When you send a message, Dekaf determines which partition it goes to:

1. **Explicit partition** - If you specify a partition, that's where it goes
2. **Key-based** - If you provide a key, it's hashed to determine the partition
3. **Round-robin** - If no key, messages are distributed across partitions

## Key-Based Partitioning

Messages with the same key always go to the same partition:

```csharp
// All messages for order-123 go to the same partition
await producer.ProduceAsync("orders", "order-123", event1);
await producer.ProduceAsync("orders", "order-123", event2);
await producer.ProduceAsync("orders", "order-123", event3);
```

This guarantees ordering for messages with the same key - they'll be consumed in the order they were produced.

:::tip
Use meaningful keys like user IDs, order IDs, or entity IDs to keep related messages together.
:::

## Explicit Partition Assignment

Send to a specific partition:

```csharp
var message = new ProducerMessage<string, string>
{
    Topic = "events",
    Partition = 0,  // Always send to partition 0
    Key = "key",
    Value = "value"
};

await producer.ProduceAsync(message);
```

Or using the factory method:

```csharp
var message = ProducerMessage<string, string>.Create(
    topic: "events",
    partition: 2,
    key: "key",
    value: "value"
);
```

:::warning
Using explicit partitions couples your code to the topic's partition count. If the topic is re-partitioned, your code may break.
:::

## Null Keys

When you don't provide a key (or it's null), the partitioner distributes messages across partitions:

```csharp
// These messages may go to different partitions
await producer.ProduceAsync("events", null, "event1");
await producer.ProduceAsync("events", null, "event2");
await producer.ProduceAsync("events", null, "event3");
```

## Partitioner Types

Dekaf supports different partitioning strategies:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithPartitioner(PartitionerType.Sticky)  // Change partitioner
    .Build();
```

| Partitioner | Behavior |
|-------------|----------|
| `Default` | Hash key for keyed messages, round-robin for null keys |
| `Sticky` | Sticks to one partition for null keys until batch is full |
| `RoundRobin` | Distributes all messages evenly |

### Sticky Partitioner

The sticky partitioner improves batching efficiency for null-key messages:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithPartitioner(PartitionerType.Sticky)
    .WithLingerMs(5)
    .Build();

// These will likely batch together in one partition
producer.Send("events", null, "event1");
producer.Send("events", null, "event2");
producer.Send("events", null, "event3");
```

## Partition Count Considerations

The number of partitions affects:

- **Parallelism** - More partitions = more concurrent consumers
- **Ordering** - Only guaranteed within a partition
- **Resource usage** - Each partition has overhead

```csharp
// Get partition count for a topic
var metadata = await producer.GetMetadataAsync("my-topic");
var partitionCount = metadata.Partitions.Count;
```

## Ordering Guarantees

Kafka guarantees message ordering **within a partition**, not across partitions.

```csharp
// ✅ Guaranteed order - same key = same partition
await producer.ProduceAsync("orders", "order-123", "created");
await producer.ProduceAsync("orders", "order-123", "paid");
await producer.ProduceAsync("orders", "order-123", "shipped");
// Consumer will see: created -> paid -> shipped

// ⚠️ No ordering guarantee - different keys may go to different partitions
await producer.ProduceAsync("orders", "order-1", "created");
await producer.ProduceAsync("orders", "order-2", "created");
await producer.ProduceAsync("orders", "order-1", "paid");
// order-2's "created" might be consumed before order-1's "paid"
```

## Practical Example: Event Sourcing

For event sourcing, use the aggregate ID as the key:

```csharp
public class EventStore
{
    private readonly IKafkaProducer<string, string> _producer;

    public async Task AppendEventAsync(string aggregateId, object @event)
    {
        // All events for an aggregate go to the same partition
        // Guarantees they're consumed in order
        await _producer.ProduceAsync(
            "events",
            aggregateId,  // Key = aggregate ID
            JsonSerializer.Serialize(@event)
        );
    }
}

// Usage
var store = new EventStore(producer);
await store.AppendEventAsync("user-123", new UserRegistered { ... });
await store.AppendEventAsync("user-123", new EmailVerified { ... });
await store.AppendEventAsync("user-123", new ProfileUpdated { ... });
// These will always be consumed in this order
```

## Practical Example: Multi-Tenant System

Route tenant data to specific partitions:

```csharp
public class TenantProducer
{
    private readonly IKafkaProducer<string, string> _producer;

    public async Task PublishAsync(string tenantId, string eventType, string payload)
    {
        // All data for a tenant goes to the same partition
        var message = new ProducerMessage<string, string>
        {
            Topic = "tenant-events",
            Key = tenantId,  // Tenant ID as key
            Value = payload,
            Headers = Headers.Create()
                .Add("tenant-id", tenantId)
                .Add("event-type", eventType)
        };

        await _producer.ProduceAsync(message);
    }
}
```
