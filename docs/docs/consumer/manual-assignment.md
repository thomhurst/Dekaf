---
sidebar_position: 5
---

# Manual Partition Assignment

Instead of using consumer groups, you can manually assign specific partitions to a consumer. This gives you full control over which partitions are consumed.

## When to Use Manual Assignment

Manual assignment is useful when:

- You need to consume from specific partitions only
- You're implementing your own partition assignment logic
- You want to replay data from specific offsets
- You don't need consumer group coordination

## Assigning Partitions

Use `Assign` instead of `Subscribe`:

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    // No group ID needed for manual assignment
    .BuildAsync();

// Assign specific partitions
consumer.Assign(
    new TopicPartition("my-topic", 0),
    new TopicPartition("my-topic", 1)
);

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    Console.WriteLine($"Partition {msg.Partition}: {msg.Value}");
}
```

## Assign with Starting Offsets

Specify where to start consuming:

```csharp
consumer.Assign(
    new TopicPartitionOffset("my-topic", 0, 100),  // Start at offset 100
    new TopicPartitionOffset("my-topic", 1, 200)   // Start at offset 200
);
```

## Differences from Subscribe

| Feature | Subscribe (Consumer Group) | Assign (Manual) |
|---------|---------------------------|-----------------|
| Partition assignment | Automatic | Manual |
| Offset tracking | Per group | You manage |
| Rebalancing | Automatic | None |
| Scaling | Add consumers | You coordinate |
| Group ID | Required | Optional |

:::caution
Don't mix `Subscribe` and `Assign` on the same consumer. Use one or the other.
:::

## Managing Offsets Manually

With manual assignment, you're responsible for tracking offsets:

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .BuildAsync();

// Load saved offsets from your storage
var savedOffsets = await LoadOffsetsFromDatabaseAsync();

consumer.Assign(savedOffsets.Select(o =>
    new TopicPartitionOffset(o.Topic, o.Partition, o.Offset)
));

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessAsync(msg);

    // Save offset to your storage
    await SaveOffsetAsync(msg.Topic, msg.Partition, msg.Offset + 1);
}
```

## Incremental Assignment

Add or remove partitions without replacing the entire assignment:

```csharp
// Initial assignment
consumer.Assign(new TopicPartition("my-topic", 0));

// Later, add partition 1
consumer.IncrementalAssign(new[]
{
    new TopicPartitionOffset("my-topic", 1, Offset.Beginning)
});

// Remove partition 0
consumer.IncrementalUnassign(new[]
{
    new TopicPartition("my-topic", 0)
});
```

## Seeking

With manual assignment, you can freely seek to any offset:

```csharp
consumer.Assign(new TopicPartition("my-topic", 0));

// Seek to beginning
consumer.SeekToBeginning(new TopicPartition("my-topic", 0));

// Seek to end
consumer.SeekToEnd(new TopicPartition("my-topic", 0));

// Seek to specific offset
consumer.Seek(new TopicPartitionOffset("my-topic", 0, 12345));
```

## Unassigning

Remove all partition assignments:

```csharp
consumer.Unassign();
```

## Complete Example: Partition Reader

```csharp
using Dekaf;

public class PartitionReader
{
    public async Task ReadPartitionAsync(
        string bootstrapServers,
        string topic,
        int partition,
        long startOffset,
        long? endOffset,
        Func<ConsumeResult<string, string>, Task> processor,
        CancellationToken ct)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.None)
            .BuildAsync();

        consumer.Assign(new TopicPartitionOffset(topic, partition, startOffset));

        await foreach (var msg in consumer.ConsumeAsync(ct))
        {
            await processor(msg);

            // Stop if we've reached the end offset
            if (endOffset.HasValue && msg.Offset >= endOffset.Value)
            {
                break;
            }
        }
    }
}

// Usage: Read messages 100-200 from partition 0
var reader = new PartitionReader();
await reader.ReadPartitionAsync(
    "localhost:9092",
    "my-topic",
    partition: 0,
    startOffset: 100,
    endOffset: 200,
    async msg => Console.WriteLine(msg.Value),
    cancellationToken
);
```

## Complete Example: Multi-Partition Worker

```csharp
using Dekaf;

public class MultiPartitionWorker
{
    private readonly IKafkaConsumer<string, string> _consumer;
    private readonly ConcurrentDictionary<int, long> _offsets = new();

    public MultiPartitionWorker(string bootstrapServers)
    {
        _consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(bootstrapServers)
            .BuildAsync();
    }

    public async Task StartAsync(string topic, int[] partitions, CancellationToken ct)
    {
        // Load offsets from storage
        foreach (var partition in partitions)
        {
            var offset = await LoadOffsetAsync(topic, partition);
            _offsets[partition] = offset;
        }

        // Assign with loaded offsets
        _consumer.Assign(partitions.Select(p =>
            new TopicPartitionOffset(topic, p, _offsets[p])
        ));

        // Process messages
        await foreach (var msg in _consumer.ConsumeAsync(ct))
        {
            await ProcessAsync(msg);

            // Update local offset tracking
            _offsets[msg.Partition] = msg.Offset + 1;

            // Periodically save offsets
            if (msg.Offset % 100 == 0)
            {
                await SaveOffsetsAsync();
            }
        }

        // Save final offsets on shutdown
        await SaveOffsetsAsync();
    }

    private async Task SaveOffsetsAsync()
    {
        foreach (var (partition, offset) in _offsets)
        {
            await SaveOffsetAsync(partition, offset);
        }
    }

    private Task<long> LoadOffsetAsync(string topic, int partition) => Task.FromResult(0L);
    private Task SaveOffsetAsync(int partition, long offset) => Task.CompletedTask;
    private Task ProcessAsync(ConsumeResult<string, string> msg) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _consumer.DisposeAsync();
    }
}
```
