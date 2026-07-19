---
sidebar_position: 3
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

Dekaf provides two modes for managing offsets, matching standard Apache Kafka's `enable.auto.commit` setting:

### Auto Mode (Default)

Offsets are automatically committed in the background:

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithOffsetCommitMode(OffsetCommitMode.Auto)
    .BuildAsync();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    // Offset is automatically committed periodically
    ProcessMessage(msg);
}
```

**Pros:** Simple, no extra code needed — and safe by default
**Cons:** Duplicates are possible after failures; processing must be idempotent

:::info Dekaf's auto-commit default is at-least-once
Unlike Confluent.Kafka, a message's offset only becomes committable once your loop has
demonstrably moved past it — by requesting the next message. If your handler throws and the
exception exits the loop, the failed message is **not** committed and is redelivered after a
restart or rebalance. See [Delivery Semantics](delivery-semantics.md) for the full contract,
including the two caveats (catch-and-continue counts as processed; `break` leaves the last
message uncommitted unless you `CommitAsync()`).
Prefer stating the intent explicitly with `WithAtLeastOnceProcessing()`. For Confluent-style
at-most-once staging, use `WithAtMostOnceProcessing()`.
:::

### Auto-Commit with Manual Offset Store

Keep background auto-commit, but only let it commit offsets you have explicitly marked as
processed. This is the strict form of at-least-once: unlike the default, it survives
catch-and-continue, because acknowledgment is your explicit `StoreOffset` call rather than
loop progress.

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithAutoOffsetStore(false) // Auto-commit only commits explicitly stored offsets
    .BuildAsync();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessMessageAsync(msg);  // Process first
    consumer.StoreOffset(msg);       // Then mark as safe to commit (no network call)
}
```

`StoreOffset` is a cheap in-memory operation; the background auto-commit loop batches the actual
network commits. If processing throws before `StoreOffset`, the message's offset is never staged
and it will be redelivered — even if you catch the exception and keep consuming.

:::caution
Offsets are positions, not per-message acknowledgements. Storing a later offset also commits
everything before it — if you skip a failed message and keep storing later offsets, the failed
message is still lost. To preserve it, stop consuming the partition, or route the failure to a
dead-letter topic before moving on.
:::

### Manual Mode

You control when offsets are committed by calling `CommitAsync()`:

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithOffsetCommitMode(OffsetCommitMode.Manual)
    .BuildAsync();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessMessageAsync(msg);  // Process first
    await consumer.CommitAsync();    // Then commit
}
```

**Pros:** Commit after processing ensures at-least-once delivery
**Cons:** Slightly more code, some overhead from frequent commits

## Committing Offsets

### Commit All Consumed Offsets

```csharp
await consumer.CommitAsync();
```

This commits the current position for all assigned partitions - i.e., the offset of the next message to be consumed.

### Commit Specific Offsets

For fine-grained control, you can commit specific offsets:

```csharp
await consumer.CommitAsync(new[]
{
    new TopicPartitionOffset("my-topic", 0, 100),
    new TopicPartitionOffset("my-topic", 1, 50)
});
```

This is useful when you need to:
- Skip messages that failed processing
- Implement custom batching logic
- Coordinate commits with external systems

:::caution

Skipping by committing past a message is permanent for the consumer group — it will never be redelivered. If the message should be processed *eventually*, park it on a retry or dead-letter topic before committing, or pause the partition instead. See [Filtering Skips Messages Permanently](./linq-extensions.md#filtering-skips-messages-permanently).

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
long? committed = await consumer.Positions.GetCommittedOffsetAsync(
    new TopicPartition("my-topic", 0)
);

// Get current position (next offset to be consumed)
long? position = consumer.Positions.GetPosition(new TopicPartition("my-topic", 0));
```

## Seeking to Offsets

Jump to a specific position:

```csharp
// Seek to specific offset
consumer.Positions.Seek(new TopicPartitionOffset("my-topic", 0, 100));

// Seek to beginning
consumer.Positions.SeekToBeginning(new TopicPartition("my-topic", 0));

// Seek to end
consumer.Positions.SeekToEnd(new TopicPartition("my-topic", 0));
```

Seek and pause/resume operations mutate current consumer state and return `void`. Keep them as separate statements:

```csharp
// Before
consumer.Seek(new TopicPartitionOffset("my-topic", 0, 100))
    .Pause(new TopicPartition("my-topic", 0));

// After
consumer.Positions.Seek(new TopicPartitionOffset("my-topic", 0, 100));
consumer.Partitions.Pause(new TopicPartition("my-topic", 0));
```

### Seek by Timestamp

Find offsets for a specific time:

```csharp
var targetTime = DateTimeOffset.UtcNow.AddHours(-1);

var offsets = await consumer.Offsets.GetOffsetsForTimesAsync(new[]
{
    new TopicPartitionTimestamp("my-topic", 0, targetTime)
});

foreach (var (tp, offset) in offsets)
{
    consumer.Positions.Seek(new TopicPartitionOffset(tp.Topic, tp.Partition, offset));
}
```

### Log Truncation Recovery

After broker failover, leader-epoch validation can report that a new leader's log ends before
records already prefetched from the previous leader. Dekaf clears stale buffered records for the
affected partition. When an automatic offset-reset policy is configured, Dekaf resumes from the
broker's precise divergence offset and logs a warning. Offsets from the truncated tail can therefore
appear again with different records; this is required to avoid silently skipping the replacement log.

With `AutoOffsetReset.None`, Dekaf throws `LogTruncationException` instead. Its
`TruncationOffsets` identify the first divergent offset and last common leader epoch for each
affected partition. Catch it, reconcile downstream state, then seek to the selected recovery offset.
Other `OffsetOutOfRange` handling continues to follow the configured auto-offset-reset policy.

## Delivery Semantics

The full contract — including exactly when an offset becomes committable and how Dekaf compares
to the Java and Confluent clients — lives on the
[Delivery Semantics](delivery-semantics.md) page. Summary:

| Mode | Semantics | Risk |
|------|-----------|------|
| Auto (defaults, = `WithAtLeastOnceProcessing()`) | At-least-once | May reprocess after failures; swallowed exceptions still commit |
| `WithAutoOffsetStore(false)` + `StoreOffset` after process | At-least-once (strict) | May reprocess on crash |
| `WithAtMostOnceProcessing()` | At-most-once | Loses messages whose processing failed |
| Manual (commit after process) | At-least-once | May reprocess on crash |
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

1. **Make commits reflect completed processing** - the default already does this for sequential loops; add `WithAutoOffsetStore(false)` + `StoreOffset` after each successfully processed message when you catch exceptions and keep consuming, or use Manual mode

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
