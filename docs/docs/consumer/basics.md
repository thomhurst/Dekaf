---
sidebar_position: 1
---

# Consumer Basics

The consumer reads messages from Kafka topics. We use `IAsyncEnumerable` so you can process messages with a simple `await foreach` loop.

## Creating a Consumer

Use the fluent builder API:

```csharp
using Dekaf;

await using var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-consumer-group")
    .BuildAsync();
```

The type parameters `<TKey, TValue>` define the expected key and value types.

## Subscribing to Topics

Before consuming, subscribe to one or more topics:

```csharp
using Dekaf;

// Single topic
consumer.Subscribe("my-topic");

// Multiple topics
consumer.Subscribe("topic1", "topic2", "topic3");

// Using the builder
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .SubscribeTo("my-topic")  // Subscribe during build
    .BuildAsync();
```

## Consuming Messages

The primary way to consume is with `await foreach`:

```csharp
await foreach (var message in consumer.ConsumeAsync(cancellationToken))
{
    Console.WriteLine($"Key: {message.Key}");
    Console.WriteLine($"Value: {message.Value}");
    Console.WriteLine($"Topic: {message.Topic}");
    Console.WriteLine($"Partition: {message.Partition}");
    Console.WriteLine($"Offset: {message.Offset}");
    Console.WriteLine($"Timestamp: {message.Timestamp}");
}
```

The loop continues until the cancellation token is triggered or an error occurs.

## ConsumeResult Properties

Each message gives you access to:

| Property | Type | Description |
|----------|------|-------------|
| `Key` | `TKey?` | The deserialized message key |
| `Value` | `TValue` | The deserialized message value |
| `Topic` | `string` | The topic name |
| `Partition` | `int` | The partition number |
| `Offset` | `long` | The offset within the partition |
| `Timestamp` | `DateTimeOffset` | When the message was produced |
| `TimestampType` | `TimestampType` | Whether timestamp is create time or log append time |
| `Headers` | `IReadOnlyList<RecordHeader>?` | Optional message headers |
| `LeaderEpoch` | `int?` | The leader epoch (for exactly-once) |

## Consuming a Single Message

For polling-style consumption:

```csharp
var message = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cancellationToken);

if (message != null)
{
    ProcessMessage(message);
}
else
{
    Console.WriteLine("No message received within timeout");
}
```

## Auto Offset Reset

When a consumer starts with no committed offset, `AutoOffsetReset` determines where to begin:

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithAutoOffsetReset(AutoOffsetReset.Earliest)  // Start from beginning
    .BuildAsync();
```

| Value | Behavior |
|-------|----------|
| `Earliest` | Start from the oldest available message |
| `Latest` | Start from new messages only (default) |
| `None` | Throw an exception if no offset is committed |

## Error Handling

Wrap your consume loop in try-catch:

```csharp
try
{
    await foreach (var message in consumer.ConsumeAsync(ct))
    {
        try
        {
            await ProcessAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message at offset {Offset}", message.Offset);
            // Decide: continue, break, or rethrow
        }
    }
}
catch (OperationCanceledException)
{
    _logger.LogInformation("Consumer cancelled");
}
catch (ConsumeException ex)
{
    _logger.LogError(ex, "Consume error: {Reason}", ex.Message);
}
```

## Graceful Shutdown

Always dispose the consumer properly:

```csharp
using Dekaf;

await using var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .SubscribeTo("my-topic")
    .BuildAsync();

using var cts = new CancellationTokenSource();

// Handle shutdown signals
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await foreach (var msg in consumer.ConsumeAsync(cts.Token))
    {
        await ProcessAsync(msg);
    }
}
catch (OperationCanceledException)
{
    // Expected on shutdown
}

// Consumer.DisposeAsync() is called by await using:
// - Commits pending offsets (if auto-commit enabled)
// - Leaves the consumer group
// - Closes connections
```

## Pausing and Resuming

Temporarily stop consuming from specific partitions:

```csharp
// Pause consumption from a partition
consumer.Pause(new TopicPartition("my-topic", 0));

// Check what's paused
var paused = consumer.Paused;

// Resume
consumer.Resume(new TopicPartition("my-topic", 0));
```

This is useful for backpressure - if your processing can't keep up, pause some partitions.

## Accessing Metadata

```csharp
// Current subscription
IReadOnlySet<string> topics = consumer.Subscription;

// Current assignment (partitions being consumed)
IReadOnlySet<TopicPartition> partitions = consumer.Assignment;

// Consumer's member ID in the group
string? memberId = consumer.MemberId;
```

## Complete Example

```csharp
using Dekaf;

public class OrderConsumer : BackgroundService
{
    private readonly ILogger<OrderConsumer> _logger;

    public OrderConsumer(ILogger<OrderConsumer> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var consumer = await Kafka.CreateConsumer<string, Order>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("order-processor")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .SubscribeTo("orders")
            .BuildAsync();

        _logger.LogInformation("Order consumer started");

        try
        {
            await foreach (var message in consumer.ConsumeAsync(stoppingToken))
            {
                _logger.LogInformation(
                    "Processing order {OrderId} from partition {Partition}",
                    message.Key,
                    message.Partition
                );

                try
                {
                    await ProcessOrderAsync(message.Value, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process order {OrderId}", message.Key);
                    // Continue processing other messages
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Order consumer stopping");
        }
    }

    private async Task ProcessOrderAsync(Order order, CancellationToken ct)
    {
        // Your business logic here
        await Task.Delay(100, ct);
    }
}
```
