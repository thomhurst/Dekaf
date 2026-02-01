---
sidebar_position: 3
---

# Consumer Groups

Consumer groups enable multiple consumer instances to share the work of consuming a topic. Kafka automatically distributes partitions among group members.

## How Consumer Groups Work

When multiple consumers share a group ID:

1. Kafka assigns partitions to consumers (1 partition = 1 consumer max)
2. Each message is delivered to exactly one consumer in the group
3. If a consumer fails, its partitions are reassigned to others

```
Topic with 4 partitions:

Group "my-group" with 2 consumers:
  Consumer A: [Partition 0] [Partition 1]
  Consumer B: [Partition 2] [Partition 3]

Group "my-group" with 4 consumers:
  Consumer A: [Partition 0]
  Consumer B: [Partition 1]
  Consumer C: [Partition 2]
  Consumer D: [Partition 3]
```

## Creating Consumer Group Members

Each consumer instance needs the same group ID:

```csharp
// Instance 1
var consumer1 = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processors")  // Same group ID
    .SubscribeTo("orders")
    .Build();

// Instance 2 (different machine/process)
var consumer2 = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processors")  // Same group ID
    .SubscribeTo("orders")
    .Build();
```

## Rebalancing

When the group membership changes, Kafka rebalances partitions:

- A new consumer joins
- A consumer leaves (graceful shutdown)
- A consumer is considered dead (heartbeat timeout)
- Topic partition count changes

### Rebalance Listener

Get notified when partitions are assigned or revoked:

```csharp
public class MyRebalanceListener : IRebalanceListener
{
    public async ValueTask OnPartitionsAssignedAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        Console.WriteLine($"Assigned: {string.Join(", ", partitions)}");
        // Initialize resources for these partitions
    }

    public async ValueTask OnPartitionsRevokedAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        Console.WriteLine($"Revoked: {string.Join(", ", partitions)}");
        // Commit offsets, clean up resources
    }

    public async ValueTask OnPartitionsLostAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        Console.WriteLine($"Lost: {string.Join(", ", partitions)}");
        // Partitions were taken away (e.g., due to timeout)
    }
}

var consumer = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithRebalanceListener(new MyRebalanceListener())
    .Build();
```

### Cooperative Rebalancing

Dekaf uses cooperative (incremental) rebalancing by default, which minimizes disruption:

```csharp
// Default: CooperativeSticky assignor
var consumer = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .Build();

// Or explicitly set the assignor
var consumer = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithPartitionAssignmentStrategy(PartitionAssignmentStrategy.CooperativeSticky)
    .Build();
```

With cooperative rebalancing:
- Only affected partitions are revoked
- Other partitions continue processing
- Reduces rebalance time significantly

## Static Membership

For faster rebalances with planned restarts, use static membership:

```csharp
var consumer = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithGroupInstanceId("instance-1")  // Must be unique within the group
    .Build();
```

Benefits:
- Consumer can rejoin and get the same partitions back
- No rebalance if consumer restarts within session timeout
- Great for rolling deployments

:::warning
Each instance in the group must have a unique `GroupInstanceId`. Using the same ID causes fencing.
:::

## Session and Heartbeat Configuration

```csharp
var consumer = Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithSessionTimeout(TimeSpan.FromSeconds(45))    // Max time before considered dead
    .WithHeartbeatInterval(TimeSpan.FromSeconds(3)) // How often to send heartbeats
    .Build();
```

Guidelines:
- `SessionTimeout` should be > 3x `HeartbeatInterval`
- Longer timeout = more time for slow consumers, but slower failure detection
- Shorter timeout = faster failure detection, but more spurious rebalances

## Consumer Group Metadata

Access information about the group:

```csharp
// Get member ID
string? memberId = consumer.MemberId;

// Get consumer group metadata (for transactions)
var metadata = consumer.ConsumerGroupMetadata;
```

## Scaling Consumers

### Adding Consumers

When you add consumers to a group:
1. New consumer joins and triggers rebalance
2. Partitions are redistributed
3. New consumer starts receiving messages

### Removing Consumers

When a consumer leaves gracefully (`await using` or `CloseAsync`):
1. Consumer commits offsets and leaves group
2. Remaining consumers get its partitions

### Maximum Parallelism

The maximum number of active consumers in a group equals the number of partitions:

```
Topic with 4 partitions:
  - 1 consumer: processes all 4 partitions
  - 2 consumers: each processes 2 partitions
  - 4 consumers: each processes 1 partition
  - 5 consumers: one consumer is idle!
```

## Multiple Consumer Groups

Different groups consume the same topic independently:

```csharp
// Analytics group - processes all messages
var analyticsConsumer = Kafka.CreateConsumer<string, string>()
    .WithGroupId("analytics")
    .SubscribeTo("orders")
    .Build();

// Notification group - also processes all messages
var notificationConsumer = Kafka.CreateConsumer<string, string>()
    .WithGroupId("notifications")
    .SubscribeTo("orders")
    .Build();
```

Each group:
- Tracks its own offsets
- Receives all messages
- Scales independently

## Complete Example

```csharp
public class OrderProcessor
{
    private readonly ILogger<OrderProcessor> _logger;

    public async Task RunAsync(string instanceId, CancellationToken ct)
    {
        await using var consumer = Kafka.CreateConsumer<string, Order>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("order-processors")
            .WithGroupInstanceId(instanceId)  // Static membership
            .WithRebalanceListener(new LoggingRebalanceListener(_logger))
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .SubscribeTo("orders")
            .Build();

        _logger.LogInformation(
            "Consumer {InstanceId} started, member ID: {MemberId}",
            instanceId,
            consumer.MemberId
        );

        await foreach (var batch in consumer.ConsumeAsync(ct).Batch(100))
        {
            _logger.LogInformation(
                "Processing batch of {Count} orders from partitions: {Partitions}",
                batch.Count,
                string.Join(", ", batch.Select(m => m.Partition).Distinct())
            );

            foreach (var msg in batch)
            {
                await ProcessOrderAsync(msg.Value);
            }

            await consumer.CommitAsync();
        }
    }

    private class LoggingRebalanceListener : IRebalanceListener
    {
        private readonly ILogger _logger;

        public LoggingRebalanceListener(ILogger logger) => _logger = logger;

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken ct)
        {
            _logger.LogInformation("Partitions assigned: {Partitions}", string.Join(", ", partitions));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken ct)
        {
            _logger.LogInformation("Partitions revoked: {Partitions}", string.Join(", ", partitions));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken ct)
        {
            _logger.LogWarning("Partitions lost: {Partitions}", string.Join(", ", partitions));
            return ValueTask.CompletedTask;
        }
    }

    private Task ProcessOrderAsync(Order order) => Task.Delay(10);
}
```
