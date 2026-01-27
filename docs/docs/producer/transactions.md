---
sidebar_position: 6
---

# Transactions

Kafka transactions enable exactly-once semantics (EOS) by allowing you to atomically write to multiple partitions and topics. Either all messages in a transaction are committed, or none are.

## When to Use Transactions

Transactions are useful when you need to:

- **Atomically write to multiple topics** - All succeed or all fail
- **Implement exactly-once processing** - Consume, process, produce without duplicates
- **Maintain consistency** - Ensure related messages are visible together

## Setting Up a Transactional Producer

Create a producer with a transactional ID:

```csharp
await using var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithTransactionalId("my-service-instance-1")  // Must be unique per instance
    .Build();

// Initialize transactions (required before first transaction)
await producer.InitTransactionsAsync();
```

:::warning
The transactional ID must be unique per producer instance. If two producers use the same ID, one will be fenced (killed) by Kafka.
:::

## Basic Transaction Flow

```csharp
try
{
    // Start the transaction
    await producer.BeginTransactionAsync();

    // Send messages within the transaction
    await producer.ProduceAsync("orders", orderId, orderJson);
    await producer.ProduceAsync("audit-log", orderId, auditEntry);
    await producer.ProduceAsync("notifications", userId, notification);

    // Commit - all messages become visible atomically
    await producer.CommitTransactionAsync();
}
catch (Exception ex)
{
    // Abort - none of the messages become visible
    await producer.AbortTransactionAsync();
    throw;
}
```

## Exactly-Once Processing (Consume-Transform-Produce)

The most common use case for transactions is exactly-once stream processing:

```csharp
await using var consumer = Dekaf.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("processor-group")
    .WithOffsetCommitMode(OffsetCommitMode.Manual)  // We'll commit via transaction
    .WithIsolationLevel(IsolationLevel.ReadCommitted)  // Only read committed messages
    .SubscribeTo("input-topic")
    .Build();

await using var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithTransactionalId($"processor-{Environment.MachineName}")
    .Build();

await producer.InitTransactionsAsync();

await foreach (var message in consumer.ConsumeAsync(ct))
{
    try
    {
        await producer.BeginTransactionAsync();

        // Process and produce output
        var result = Process(message.Value);
        await producer.ProduceAsync("output-topic", message.Key, result);

        // Commit offsets within the transaction
        await producer.SendOffsetsToTransactionAsync(
            consumer.ConsumerGroupMetadata,
            new[] { new TopicPartitionOffset(message.Topic, message.Partition, message.Offset + 1) }
        );

        await producer.CommitTransactionAsync();
    }
    catch (Exception ex)
    {
        await producer.AbortTransactionAsync();
        _logger.LogError(ex, "Failed to process message, transaction aborted");
    }
}
```

This pattern guarantees:
- Each input message is processed exactly once
- Output messages are produced exactly once
- Consumer offsets are committed atomically with outputs

## Transaction Isolation Levels

Consumers can choose whether to read uncommitted messages:

```csharp
// Read all messages, including uncommitted (default)
.WithIsolationLevel(IsolationLevel.ReadUncommitted)

// Only read committed messages
.WithIsolationLevel(IsolationLevel.ReadCommitted)
```

:::tip
Use `ReadCommitted` when consuming from topics that receive transactional writes to avoid seeing messages that might be aborted.
:::

## Transaction Timeouts

Transactions have a timeout to prevent hanging transactions:

```csharp
var producer = Dekaf.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithTransactionalId("my-service")
    .WithTransactionTimeout(TimeSpan.FromMinutes(2))  // Default is 1 minute
    .Build();
```

If a transaction isn't committed or aborted within the timeout, Kafka will abort it automatically.

## Error Handling

Different errors require different handling:

```csharp
try
{
    await producer.BeginTransactionAsync();
    // ... produce messages ...
    await producer.CommitTransactionAsync();
}
catch (ProducerFencedException)
{
    // Another producer with the same transactional ID took over
    // This producer is no longer valid - must recreate
    throw;
}
catch (TransactionAbortedException)
{
    // Transaction was aborted by Kafka (timeout, etc.)
    // Can retry with a new transaction
    await producer.AbortTransactionAsync();
}
catch (Exception ex)
{
    // Other errors - abort and possibly retry
    await producer.AbortTransactionAsync();
    throw;
}
```

## Best Practices

### 1. Keep Transactions Short

Long-running transactions:
- Increase memory usage on brokers
- May timeout
- Block consumers using `ReadCommitted`

```csharp
// ✅ Good - quick transaction
await producer.BeginTransactionAsync();
await producer.ProduceAsync("topic", key, value);
await producer.CommitTransactionAsync();

// ❌ Bad - long-running transaction
await producer.BeginTransactionAsync();
foreach (var item in millionsOfItems)  // Too many items!
{
    await producer.ProduceAsync("topic", item.Key, item.Value);
}
await producer.CommitTransactionAsync();
```

### 2. Unique Transactional IDs

Use instance-specific IDs to avoid fencing:

```csharp
// ✅ Good - unique per instance
var transactionalId = $"order-processor-{Environment.MachineName}-{Guid.NewGuid():N}";

// ❌ Bad - will cause fencing when scaled
var transactionalId = "order-processor";  // Same ID for all instances!
```

### 3. Idempotent Operations

Even with transactions, make your processing idempotent when possible:

```csharp
// Use deterministic IDs
var outputKey = $"{inputMessage.Topic}-{inputMessage.Partition}-{inputMessage.Offset}";

// Check if already processed (in your database, cache, etc.)
if (await IsAlreadyProcessedAsync(outputKey))
{
    // Skip - this handles edge cases during recovery
    continue;
}
```

## Complete Example

```csharp
public class ExactlyOnceProcessor
{
    private readonly IKafkaConsumer<string, string> _consumer;
    private readonly IKafkaProducer<string, string> _producer;
    private readonly ILogger _logger;

    public async Task RunAsync(CancellationToken ct)
    {
        await _producer.InitTransactionsAsync();

        await foreach (var batch in _consumer.ConsumeAsync(ct).Batch(100))
        {
            try
            {
                await _producer.BeginTransactionAsync();

                var offsets = new List<TopicPartitionOffset>();

                foreach (var msg in batch)
                {
                    var result = Transform(msg.Value);
                    await _producer.ProduceAsync("output", msg.Key, result);
                    offsets.Add(new TopicPartitionOffset(msg.Topic, msg.Partition, msg.Offset + 1));
                }

                await _producer.SendOffsetsToTransactionAsync(
                    _consumer.ConsumerGroupMetadata!,
                    offsets
                );

                await _producer.CommitTransactionAsync();
                _logger.LogInformation("Committed batch of {Count} messages", batch.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transaction failed, aborting");
                await _producer.AbortTransactionAsync();
            }
        }
    }

    private string Transform(string input) => input.ToUpperInvariant();
}
```
