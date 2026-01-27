---
sidebar_position: 2
---

# Offset Management

Offsets are how Kafka tracks where you left off. Get this wrong and you'll either lose messages or process them twice. Neither is fun.

## Understanding Offsets

Each message in a Kafka partition has a unique, sequential offset:

```
Partition 0: [msg@0] [msg@1] [msg@2] [msg@3] [msg@4] ...
                                       ^
                                   committed offset = 3
                                   (next read will be offset 3)
```

When you commit offset 3, you're saying "I've processed messages 0, 1, and 2. Start at 3 next time."

## Offset Commit Modes

Dekaf provides three modes for managing offsets, controlled by `OffsetCommitMode`:

### Auto Mode (Default)

Offsets are automatically stored and committed in the background:

```csharp
var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithOffsetCommitMode(OffsetCommitMode.Auto)
    .Build();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    // Offset is automatically committed periodically
    ProcessMessage(msg);
}
```

**Pros:** Simple, no extra code needed
**Cons:** Messages might be committed before processing completes

:::caution
If your application crashes after committing but before processing, messages may be lost. Use this mode only when occasional message loss is acceptable (logs, metrics, etc.).
:::

### ManualCommit Mode

Offsets are automatically tracked as you consume, but you control when to commit:

```csharp
var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithOffsetCommitMode(OffsetCommitMode.ManualCommit)
    .Build();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessMessageAsync(msg);  // Process first
    await consumer.CommitAsync();    // Then commit
}
```

**Pros:** Commit after processing ensures at-least-once delivery
**Cons:** Slightly more code, some overhead from frequent commits

### Manual Mode

Full control - you must explicitly store and commit offsets:

```csharp
var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithOffsetCommitMode(OffsetCommitMode.Manual)
    .Build();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    if (await TryProcessAsync(msg))
    {
        consumer.StoreOffset(msg);  // Mark this message as processed
    }
    // Skipped messages won't have their offset stored

    // Periodically commit stored offsets
    if (ShouldCommit())
    {
        await consumer.CommitAsync();
    }
}
```

**Pros:** Maximum control, can skip messages
**Cons:** More complex, easy to make mistakes

## Committing Offsets

### Commit All Stored Offsets

```csharp
await consumer.CommitAsync();
```

This commits all offsets that have been stored (automatically or via `StoreOffset`).

### Commit Specific Offsets

```csharp
await consumer.CommitAsync(new[]
{
    new TopicPartitionOffset("my-topic", 0, 100),
    new TopicPartitionOffset("my-topic", 1, 50)
});
```

### Storing Offsets

In Manual mode, use `StoreOffset` to mark messages as processed:

```csharp
// From a ConsumeResult
consumer.StoreOffset(message);

// Or with explicit offset (commits offset + 1)
consumer.StoreOffset(new TopicPartitionOffset("topic", partition, offset + 1));
```

:::note
Store the **next** offset to consume, not the current one. `StoreOffset(message)` handles this automatically.
:::

## Commit Strategies

### Commit After Each Message

Safest, but slowest:

```csharp
await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessAsync(msg);
    await consumer.CommitAsync();  // Network round-trip per message
}
```

### Commit in Batches

Better performance while maintaining safety:

```csharp
await foreach (var batch in consumer.ConsumeAsync(ct).Batch(100))
{
    foreach (var msg in batch)
    {
        await ProcessAsync(msg);
    }
    await consumer.CommitAsync();  // One commit per 100 messages
}
```

### Commit Periodically

Good for high-throughput:

```csharp
var lastCommit = DateTime.UtcNow;
var commitInterval = TimeSpan.FromSeconds(5);

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessAsync(msg);

    if (DateTime.UtcNow - lastCommit > commitInterval)
    {
        await consumer.CommitAsync();
        lastCommit = DateTime.UtcNow;
    }
}
```

## Checking Committed Offsets

```csharp
// Get committed offset for a partition
long? committed = await consumer.GetCommittedOffsetAsync(
    new TopicPartition("my-topic", 0)
);

// Get current position (next offset to be consumed)
long? position = consumer.GetPosition(new TopicPartition("my-topic", 0));
```

## Seeking to Offsets

Jump to a specific position:

```csharp
// Seek to specific offset
consumer.Seek(new TopicPartitionOffset("my-topic", 0, 100));

// Seek to beginning
consumer.SeekToBeginning(new TopicPartition("my-topic", 0));

// Seek to end
consumer.SeekToEnd(new TopicPartition("my-topic", 0));
```

### Seek by Timestamp

Find offsets for a specific time:

```csharp
var targetTime = DateTimeOffset.UtcNow.AddHours(-1);

var offsets = await consumer.GetOffsetsForTimesAsync(new[]
{
    new TopicPartitionTimestamp("my-topic", 0, targetTime)
});

foreach (var (tp, offset) in offsets)
{
    consumer.Seek(new TopicPartitionOffset(tp, offset));
}
```

## Delivery Semantics

| Mode | Semantics | Risk |
|------|-----------|------|
| Auto | At-most-once | May lose messages on crash |
| ManualCommit (commit after process) | At-least-once | May reprocess on crash |
| Manual + External storage | Exactly-once | Most complex |

### Achieving Exactly-Once

True exactly-once requires coordinating offset commits with your output:

```csharp
// Using transactions
await producer.BeginTransactionAsync();
await producer.ProduceAsync("output", key, result);
await producer.SendOffsetsToTransactionAsync(consumer.ConsumerGroupMetadata, offsets);
await producer.CommitTransactionAsync();

// Or with a database transaction
using var dbTransaction = await db.BeginTransactionAsync();
await SaveResultAsync(result, dbTransaction);
await SaveOffsetAsync(message.TopicPartitionOffset, dbTransaction);
await dbTransaction.CommitAsync();
```

## Best Practices

1. **Use ManualCommit mode** for most applications - it's a good balance of safety and simplicity

2. **Batch your commits** - committing after every message is slow

3. **Make processing idempotent** - then at-least-once becomes effectively exactly-once

4. **Don't commit before processing** - the offset says "I'm done with everything up to here"

5. **Handle rebalances** - commits may fail during rebalancing; wrap in try-catch

```csharp
try
{
    await consumer.CommitAsync();
}
catch (KafkaException ex) when (ex.Message.Contains("rebalance"))
{
    _logger.LogWarning("Commit failed due to rebalance, offsets will be recommitted");
}
```
