---
sidebar_position: 3
---

# Fire-and-Forget Production

For high-throughput scenarios where you don't need to wait for acknowledgment, Dekaf provides fire-and-forget methods that return immediately.

## The Send() Method

`Send()` queues a message for delivery and returns immediately without waiting:

```csharp
// Returns immediately - doesn't wait for broker acknowledgment
producer.Send("my-topic", "key", "value");
```

Compare this to `ProduceAsync()`:

```csharp
// Waits for broker acknowledgment
await producer.ProduceAsync("my-topic", "key", "value");
```

## When to Use Fire-and-Forget

Fire-and-forget is ideal when:

- **High throughput** - You're sending many messages per second
- **Eventual delivery is OK** - You don't need immediate confirmation
- **Latency matters** - You can't afford to wait for network round-trips
- **Logs/metrics** - Data that's valuable but not critical

:::caution
Fire-and-forget means you won't know if a message fails to deliver. Use it only when losing occasional messages is acceptable.
:::

## Ensuring Delivery

Messages sent via `Send()` are buffered internally. To ensure all buffered messages are delivered:

```csharp
// Send many messages quickly
for (int i = 0; i < 10000; i++)
{
    producer.Send("events", $"event-{i}", eventData);
}

// Wait for all to be delivered
await producer.FlushAsync();
```

:::tip
Always call `FlushAsync()` before disposing the producer, or use `await using` which does this automatically.
:::

## With Delivery Callbacks

If you need to know about delivery success/failure without blocking, use the callback overload:

```csharp
producer.Send(
    ProducerMessage<string, string>.Create("orders", orderId, orderJson),
    (metadata, error) =>
    {
        if (error != null)
        {
            _logger.LogError(error, "Failed to deliver order {OrderId}", orderId);
            // Queue for retry, send to dead letter, etc.
        }
        else
        {
            _logger.LogDebug("Order {OrderId} delivered to partition {Partition}",
                orderId, metadata.Partition);
        }
    }
);
```

The callback is invoked on a background thread when delivery completes (successfully or not).

:::warning
Don't perform blocking operations in the callback - it runs on the producer's internal thread pool.
:::

## With Headers

Use the extension method to send with headers:

```csharp
using Dekaf.Producer;

var headers = Headers.Create()
    .Add("trace-id", traceId)
    .Add("source", "event-generator");

producer.Send("events", eventKey, eventValue, headers);
```

## Performance Comparison

Here's how the different methods compare:

| Method | Blocks? | Knows Result? | Throughput |
|--------|---------|---------------|------------|
| `await ProduceAsync()` | Yes | Immediately | Lower |
| `Send()` | No | Never | Highest |
| `Send(..., callback)` | No | Via callback | High |

## Real-World Example: Event Streaming

Here's a pattern for high-throughput event streaming:

```csharp
public class EventPublisher : IAsyncDisposable
{
    private readonly IKafkaProducer<string, byte[]> _producer;
    private readonly ILogger<EventPublisher> _logger;
    private long _successCount;
    private long _failureCount;

    public EventPublisher(IKafkaProducer<string, byte[]> producer, ILogger<EventPublisher> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public void Publish(string eventType, byte[] payload)
    {
        var message = new ProducerMessage<string, byte[]>
        {
            Topic = "events",
            Key = eventType,
            Value = payload,
            Headers = Headers.Create()
                .Add("event-type", eventType)
                .Add("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())
        };

        _producer.Send(message, (metadata, error) =>
        {
            if (error != null)
            {
                Interlocked.Increment(ref _failureCount);
                _logger.LogWarning(error, "Failed to publish {EventType} event", eventType);
            }
            else
            {
                Interlocked.Increment(ref _successCount);
            }
        });
    }

    public (long Success, long Failure) GetStats() =>
        (Interlocked.Read(ref _successCount), Interlocked.Read(ref _failureCount));

    public async ValueTask DisposeAsync()
    {
        await _producer.FlushAsync();
        await _producer.DisposeAsync();

        var (success, failure) = GetStats();
        _logger.LogInformation("EventPublisher shutdown: {Success} succeeded, {Failure} failed",
            success, failure);
    }
}

// Usage
var publisher = new EventPublisher(producer, logger);

// Publish many events quickly
foreach (var evt in events)
{
    publisher.Publish(evt.Type, evt.Serialize());
}

// When shutting down
await publisher.DisposeAsync();
```

## Combining with Batching Settings

For maximum throughput with fire-and-forget, tune the batching settings:

```csharp
using Dekaf;

var producer = Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .ForHighThroughput()  // Preset with good defaults
    .WithLingerMs(10)     // Allow more time for batching
    .WithBatchSize(131072) // Larger batches (128KB)
    .UseLz4Compression()  // Compress batches
    .Build();
```

This configuration:
- Waits up to 10ms to fill batches
- Uses larger 128KB batches
- Compresses data to reduce network usage
- Uses leader-only acks for speed
