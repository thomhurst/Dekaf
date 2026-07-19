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

For services with multiple Kafka clients, create a root client once and build consumers from it. Bootstrap servers, TLS/SASL, and memory budgeting are owned by the root; group IDs and subscription settings stay on the consumer builder.

```csharp
await using var kafka = Kafka.Connect("localhost:9092");

await using var consumer = await kafka.CreateConsumer<string, string>("my-consumer-group")
    .SubscribeTo("my-topic")
    .BuildAsync();
```

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

Consumer mutation methods are commands and return `void`. Keep live consumer operations as separate statements:

```csharp
// Before
consumer.Subscribe("my-topic").Pause(new TopicPartition("my-topic", 0));

// After
consumer.Subscribe("my-topic");
consumer.Partitions.Pause(new TopicPartition("my-topic", 0));
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

The loop continues until the cancellation token is triggered or an error occurs. Cancel the token to unblock an in-flight fetch and stop the consume loop.

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
| `Headers` | `IReadOnlyList<Header>` | Message headers (empty when the record has none; never null) |
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

var recentConsumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("recent-only")
    .WithAutoOffsetResetByDuration(TimeSpan.FromHours(24))
    .BuildAsync();
```

| Value | Behavior |
|-------|----------|
| `Earliest` | Start from the oldest available message |
| `Latest` | Start from new messages only (default) |
| `None` | Throw an exception if no offset is committed |
| `ByDuration` | Start from the first offset at or after `DateTimeOffset.UtcNow - duration` |

### Protecting newly expanded partitions

Kafka 4.4's KIP-1327 can apply a separate reset policy when a topic gains partitions. This
avoids the blind window where a `Latest` consumer would otherwise skip records written before
it discovers the new partition:

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("live-orders")
    .WithAutoOffsetReset(AutoOffsetReset.Latest) // Skip history predating the group
    .WithAutoOffsetResetNewPartitions(AutoOffsetReset.Earliest) // Read all hot expansion data
    .BuildAsync();
```

The new-partition policy is optional and supports `Earliest`, `Latest`, and `ByDuration` (through
`WithAutoOffsetResetNewPartitionsByDuration`). Committed offsets always win. If a committed offset
becomes out of range, the base policy is used. Brokers older than Kafka 4.4 omit the classification,
so Dekaf safely keeps using the base policy until the coordinator supports
`ConsumerGroupHeartbeat` v2. The option requires group subscription and is rejected for manual
assignment.

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

:::caution Catching an exception still acknowledges the message
Dekaf's default is at-least-once: a message that makes your handler **throw out of the loop**
is not committed and is redelivered after a restart or rebalance. But the loop cannot see
inside your `catch` — if you log-and-continue like above, the next loop iteration counts the
failed message as processed and its offset is committed within the auto-commit interval
(5 seconds by default). The message is then never redelivered.

If you need to catch exceptions and keep consuming *without* losing failed messages, disable
auto offset store and store offsets only after processing succeeds (see the complete example
below), or switch to [manual commits](offset-management.md). The full contract is on the
[Delivery Semantics](delivery-semantics.md) page.
:::

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

// Handle shutdown signals by cancelling the consume token
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
consumer.Partitions.Pause(new TopicPartition("my-topic", 0));

// Check what's paused
var paused = consumer.Partitions.Paused;

// Resume
consumer.Partitions.Resume(new TopicPartition("my-topic", 0));
```

This is useful for backpressure - if your processing can't keep up, pause some partitions.

## Accessing Metadata

```csharp
// Current subscription
IReadOnlySet<string> topics = consumer.Subscription;

// Current assignment (partitions being consumed)
IReadOnlySet<TopicPartition> partitions = consumer.Partitions.Assignment;

// Consumer's member ID in the group
string? memberId = consumer.MemberId;
```

## Complete Example

This example processes orders with strict at-least-once semantics: automatic offset storage is
disabled, offsets are stored only after processing succeeds, and a processing failure stops the
consumer instead of skipping the order — so the failed order is redelivered on restart rather
than being silently committed away. Background auto-commit still handles the actual commits.

The explicit `StoreOffset` pattern is stricter than Dekaf's (already at-least-once) default:
it keeps failed orders safe even when an exception is caught somewhere and the loop continues,
because nothing is committed without your explicit acknowledgment.

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
            .WithAutoOffsetStore(false) // Only commit offsets we explicitly store
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

                    // Mark this offset as safe to commit only after processing succeeded
                    consumer.StoreOffset(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process order {OrderId}", message.Key);
                    // Offset not stored: rethrow so the failure isn't silently
                    // swallowed. By default this stops the host process; pair it
                    // with a process supervisor (systemd, Kubernetes, container
                    // restart policy) so the service restarts and the order is
                    // redelivered. Do NOT swallow and continue — storing any later
                    // offset would commit past this one and lose it. If you must
                    // keep consuming, dead-letter the failed order first.
                    throw;
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

If you would rather not maintain this loop yourself, `KafkaConsumerService<TKey, TValue>` packages the same lifecycle — plus retries, [dead letter queue routing](dead-letter-queues.md), and graceful shutdown — behind a single `ProcessAsync` override. See [Hosted Consumer Services](../hosted-services.md); it is the recommended starting point for background consumers in hosted apps.
