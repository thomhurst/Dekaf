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
using Dekaf;

// Instance 1
var consumer1 = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processors")  // Same group ID
    .SubscribeTo("orders")
    .BuildAsync();

// Instance 2 (different machine/process)
var consumer2 = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processors")  // Same group ID
    .SubscribeTo("orders")
    .BuildAsync();
```

## Server-side Pattern Subscriptions

Kafka 4.1+ brokers can evaluate topic name patterns during group coordination:

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processors")
    .SubscribeToPattern("orders-.*")
    .BuildAsync();
```

The pattern is sent through `ConsumerGroupHeartbeat` v1 as `SubscribedTopicRegex`. Kafka evaluates it with RE2/J-compatible syntax, and Dekaf does not translate .NET regex syntax.

For arbitrary .NET predicates, use `consumer.Subscribe(Func<string, bool>)` after building the consumer. That mode is client-side and polls metadata for matching topics, so it works with older brokers but does not use broker-side regex subscription.

## Rebalancing

When the group membership changes, Kafka rebalances partitions:

- A new consumer joins
- A consumer leaves (graceful shutdown)
- A consumer is considered dead (heartbeat timeout)
- Topic partition count changes

### Rebalance Listener

Get notified when partitions are assigned, revoked, lost, or stopped during graceful close:

```csharp
using Dekaf;

public sealed class MyRebalanceListener : IRebalanceListener, IPartitionStopListener
{
    public ValueTask OnPartitionsAssignedAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        Console.WriteLine($"Assigned: {string.Join(", ", partitions)}");
        // Initialize resources for these partitions
        return ValueTask.CompletedTask;
    }

    public ValueTask OnPartitionsRevokedAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        Console.WriteLine($"Revoked: {string.Join(", ", partitions)}");
        // Commit completed offsets, clean up resources
        return ValueTask.CompletedTask;
    }

    public ValueTask OnPartitionsLostAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        Console.WriteLine($"Lost: {string.Join(", ", partitions)}");
        // Partitions were taken away. Do not commit offsets here.
        return ValueTask.CompletedTask;
    }

    public ValueTask OnPartitionsStoppedAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        Console.WriteLine($"Stopped: {string.Join(", ", partitions)}");
        // Normal shutdown: drain and dispose partition-scoped resources
        return ValueTask.CompletedTask;
    }
}

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithRebalanceListener(new MyRebalanceListener())
    .BuildAsync();
```

Callback semantics:

| Callback | When it runs | Offset rule |
| --- | --- | --- |
| `OnPartitionsAssignedAsync` | After the group assigns partitions to this consumer. | Initialize partition-scoped state before records are processed. |
| `OnPartitionsRevokedAsync` | During cooperative rebalance before ownership is transferred. | Commit only offsets for records that have completed processing, then dispose partition-scoped state. |
| `OnPartitionsLostAsync` | After ownership was lost involuntarily, such as heartbeat timeout or unknown member recovery. | Do not commit offsets for lost partitions unless your application has a separate ownership guarantee. |
| `OnPartitionsStoppedAsync` | During graceful `CloseAsync` or `DisposeAsync`, after heartbeat, leader-refresh, auto-commit, and prefetch tasks stop and before final auto-commit, `LeaveGroup`, assignment cleanup, and resource disposal. | Drain local work if needed, commit completed offsets, then release resources. |

Non-cancellation callback exceptions are logged and suppressed. `OperationCanceledException` follows the supplied cancellation token.

For low-level consumers, track completed offsets only after durable processing.
On revoke or graceful stop, commit those completed offsets. On lost, remove local
state without committing:

```csharp
using System.Collections.Concurrent;
using Dekaf;

public sealed class RebalanceCommitListener(
    ConcurrentDictionary<TopicPartition, TopicPartitionOffset> completedOffsets,
    Func<IReadOnlyCollection<TopicPartitionOffset>, CancellationToken, ValueTask> commitCompletedAsync)
    : IRebalanceListener, IPartitionStopListener
{
    public ValueTask OnPartitionsAssignedAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        foreach (var partition in partitions)
            completedOffsets.TryRemove(partition, out _);

        return ValueTask.CompletedTask;
    }

    public async ValueTask OnPartitionsRevokedAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        await CommitCompletedForAsync(partitions, ct);
    }

    public ValueTask OnPartitionsLostAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        foreach (var partition in partitions)
            completedOffsets.TryRemove(partition, out _);

        return ValueTask.CompletedTask;
    }

    public async ValueTask OnPartitionsStoppedAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        await CommitCompletedForAsync(partitions, ct);
    }

    private async ValueTask CommitCompletedForAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken ct)
    {
        var offsets = new List<TopicPartitionOffset>();

        foreach (var partition in partitions)
        {
            if (completedOffsets.TryRemove(partition, out var offset))
                offsets.Add(offset);
        }

        if (offsets.Count > 0)
            await commitCompletedAsync(offsets, ct);
    }
}
```

Update the tracker from the consume loop after work succeeds:

```csharp
completedOffsets[new TopicPartition(message.Topic, message.Partition)] =
    new TopicPartitionOffset(
        message.Topic,
        message.Partition,
        message.Offset + 1,
        message.LeaderEpoch ?? -1);
```

For the built-in partitioned runtime, prefer
[`RunPartitionedAsync`](./partitioned-processing-api.md). It owns the
channel-per-partition pattern, bounded queues, pause/resume backpressure, and
revoke/lost/shutdown commits for you.

### Cooperative Rebalancing

Dekaf uses cooperative (incremental) rebalancing by default, which minimizes disruption:

```csharp
using Dekaf;

// Default: CooperativeSticky assignor
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .BuildAsync();

// Or explicitly set the assignor
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithPartitionAssignmentStrategy(PartitionAssignmentStrategy.CooperativeSticky)
    .BuildAsync();
```

With cooperative rebalancing:
- Only affected partitions are revoked
- Other partitions continue processing
- Reduces rebalance time significantly

## Static Membership

For faster rebalances with planned restarts, use static membership:

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithGroupInstanceId("instance-1")  // Must be unique within the group
    .BuildAsync();
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
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithSessionTimeout(TimeSpan.FromSeconds(45))    // Max time before considered dead
    .WithHeartbeatInterval(TimeSpan.FromSeconds(3)) // How often to send heartbeats
    .BuildAsync();
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
1. Heartbeat, leader-refresh, auto-commit, and prefetch tasks stop
2. `IPartitionStopListener.OnPartitionsStoppedAsync` runs with the current assignment, if implemented by the configured rebalance listener
3. Final auto-commit runs when auto-commit mode has dirty offsets
4. Consumer sends `LeaveGroup` and releases resources
5. Remaining consumers get its partitions

The stop callback is bounded to five seconds. If it is cancelled, Dekaf still
clears local assignment and releases resources before `CloseAsync` rethrows the
cancellation.

Cancel the token passed to `ConsumeAsync` before closing when you need to stop a pending fetch promptly during shutdown.

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
using Dekaf;

// Analytics group - processes all messages
var analyticsConsumer = await Kafka.CreateConsumer<string, string>()
    .WithGroupId("analytics")
    .SubscribeTo("orders")
    .BuildAsync();

// Notification group - also processes all messages
var notificationConsumer = await Kafka.CreateConsumer<string, string>()
    .WithGroupId("notifications")
    .SubscribeTo("orders")
    .BuildAsync();
```

Each group:
- Tracks its own offsets
- Receives all messages
- Scales independently

## Complete Example

```csharp
using Dekaf;

public class OrderProcessor
{
    private readonly ILogger<OrderProcessor> _logger;

    public async Task RunAsync(string instanceId, CancellationToken ct)
    {
        await using var consumer = await Kafka.CreateConsumer<string, Order>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("order-processors")
            .WithGroupInstanceId(instanceId)  // Static membership
            .WithRebalanceListener(new LoggingRebalanceListener(_logger))
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .SubscribeTo("orders")
            .BuildAsync();

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
