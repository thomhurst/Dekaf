---
sidebar_position: 4
---

# LINQ Extensions

Dekaf provides LINQ-like extension methods for `IAsyncEnumerable<ConsumeResult<TKey, TValue>>`, making it easy to filter, transform, and batch consumed messages.

## Available Extensions

All extensions are in the `Dekaf.Consumer` namespace:

```csharp
using Dekaf.Consumer;
```

## Filtering Skips Messages Permanently

:::warning Filtered messages are gone once you commit past them

The filtering operators (`Where`, `SkipWhile`, `TakeWhile`) run **after** the consumer has already consumed the message and advanced its position. Kafka commits are watermarks, not per-message acknowledgements: committing offset N tells the broker that *everything* below N is done (see [Understanding Offsets](./offset-management.md#understanding-offsets)). There is no way to commit a later message while "keeping" an earlier skipped one.

That means once any commit advances past a skipped message — the periodic auto-commit (the default `OffsetCommitMode.Auto`), `CommitAsync()`, or committing a stored/specific offset for a later message — the skipped message will **never be redelivered** to your consumer group.

Use these operators only when you want to **permanently ignore** some messages on a topic (tombstones, other tenants' keys, irrelevant event types). For a deterministic predicate this is exactly right: redelivering a skipped message would just get it filtered again.

Do **not** use them to *defer* messages you intend to process later:

```csharp
// ❌ DATA LOSS - messages skipped while unhealthy are dropped forever
await foreach (var msg in consumer.ConsumeAsync(ct)
    .Where(_ => downstream.IsHealthy))
{
    await ProcessAsync(msg);
    consumer.StoreOffset(msg);
    await consumer.CommitAsync(); // commits past every skipped message
}
```

If you need to defer instead of drop:

- **Pause consumption** while you can't process — no messages are consumed, so no offsets advance. See [Pausing and Resuming](./basics.md#pausing-and-resuming).
- **Park unprocessable messages** on a retry or dead-letter topic (and await delivery) *before* committing, then continue.
- **Use manual commit mode** and only commit offsets where everything below them is truly handled.

:::

## Where - Filtering Messages

Filter messages based on a predicate:

```csharp
await foreach (var msg in consumer.ConsumeAsync(ct)
    .Where(m => m.Key != null))
{
    // Only messages with non-null keys
}

// Multiple conditions
await foreach (var msg in consumer.ConsumeAsync(ct)
    .Where(m => m.Partition == 0)
    .Where(m => m.Value.Contains("important")))
{
    // Partition 0 messages containing "important"
}
```

Filtered-out messages are skipped permanently (see [warning above](#filtering-skips-messages-permanently)). Keep predicates deterministic: a message should be filtered because of *what it is*, not because of *when it arrived* or the current state of your application.

## Select - Transforming Messages

Project messages to a different type:

```csharp
// Extract just the values
await foreach (var value in consumer.ConsumeAsync(ct)
    .Select(m => m.Value))
{
    Console.WriteLine(value);
}

// Transform to a different type
await foreach (var order in consumer.ConsumeAsync(ct)
    .Select(m => JsonSerializer.Deserialize<Order>(m.Value)!))
{
    await ProcessOrderAsync(order);
}

// Create a view model
await foreach (var vm in consumer.ConsumeAsync(ct)
    .Select(m => new
    {
        m.Key,
        m.Value,
        m.Timestamp,
        Age = DateTimeOffset.UtcNow - m.Timestamp
    }))
{
    Console.WriteLine($"{vm.Key}: {vm.Value} (age: {vm.Age})");
}
```

## Take - Limiting Messages

Consume only a specific number of messages:

```csharp
// Consume exactly 100 messages then stop
await foreach (var msg in consumer.ConsumeAsync(ct).Take(100))
{
    await ProcessAsync(msg);
}
Console.WriteLine("Processed 100 messages");

// Useful for testing or sampling
var sample = new List<ConsumeResult<string, string>>();
await foreach (var msg in consumer.ConsumeAsync(ct).Take(10))
{
    sample.Add(msg);
}
```

## TakeWhile - Conditional Limiting

Consume while a condition is true:

```csharp
// Process until we see a "stop" message
await foreach (var msg in consumer.ConsumeAsync(ct)
    .TakeWhile(m => m.Value != "STOP"))
{
    await ProcessAsync(msg);
}

// Process until a specific time
var deadline = DateTimeOffset.UtcNow.AddMinutes(5);
await foreach (var msg in consumer.ConsumeAsync(ct)
    .TakeWhile(m => m.Timestamp < deadline))
{
    await ProcessAsync(msg);
}
```

:::caution

The first message that fails the predicate (the `"STOP"` message above) is consumed but never yielded to your loop, and its offset can be committed like any [skipped message](#filtering-skips-messages-permanently) — a restarted consumer resumes *after* it. Don't rely on seeing that boundary message again.

:::

## SkipWhile - Skipping Initial Messages

Skip messages until a condition becomes false:

```csharp
// Skip messages until we find the start marker
await foreach (var msg in consumer.ConsumeAsync(ct)
    .SkipWhile(m => m.Value != "START")
    .Take(100))  // Then take 100
{
    await ProcessAsync(msg);
}
```

:::caution

Everything skipped before the marker is [dropped permanently once committed past](#filtering-skips-messages-permanently). To start from a known position without consuming and discarding messages, prefer [`Seek`](./offset-management.md#seeking-to-offsets).

:::

## Batch - Grouping Messages

Group messages into batches for bulk processing:

```csharp
// Process in batches of 100
await foreach (var batch in consumer.ConsumeAsync(ct).Batch(100))
{
    Console.WriteLine($"Processing batch of {batch.Count} messages");

    // Batch is IReadOnlyList<ConsumeResult<TKey, TValue>>
    await BulkInsertAsync(batch.Select(m => m.Value));

    // Commit after processing the batch
    await consumer.CommitAsync();
}
```

Batching is great for:
- Bulk database inserts
- Batch API calls
- Reducing commit frequency
- Aggregation

```csharp
// Batch insert to database
await foreach (var batch in consumer.ConsumeAsync(ct).Batch(500))
{
    using var transaction = await db.BeginTransactionAsync();

    foreach (var msg in batch)
    {
        await db.InsertAsync(msg.Value, transaction);
    }

    await transaction.CommitAsync();
    await consumer.CommitAsync();

    _logger.LogInformation("Inserted {Count} records", batch.Count);
}
```

## ForEachAsync - Simple Processing

For simple message processing loops:

```csharp
// Async processor
await consumer.ForEachAsync(async msg =>
{
    await ProcessAsync(msg);
}, cancellationToken);

// Sync processor
await consumer.ForEachAsync(msg =>
{
    Process(msg);
}, cancellationToken);
```

This is equivalent to:

```csharp
await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessAsync(msg);
}
```

## Chaining Extensions

Extensions can be chained for complex pipelines:

```csharp
// Filter, transform, batch
await foreach (var batch in consumer.ConsumeAsync(ct)
    .Where(m => m.Key != null)
    .Where(m => m.Value.Length > 0)
    .Select(m => new ProcessedMessage(m.Key!, m.Value, m.Timestamp))
    .Batch(50))
{
    await ProcessBatchAsync(batch);
    await consumer.CommitAsync();
}
```

```csharp
// Skip old messages, take a sample
var recentMessages = new List<ConsumeResult<string, string>>();
var cutoff = DateTimeOffset.UtcNow.AddHours(-1);

await foreach (var msg in consumer.ConsumeAsync(ct)
    .SkipWhile(m => m.Timestamp < cutoff)
    .Take(1000))
{
    recentMessages.Add(msg);
}
```

## Performance Considerations

### Filtering Early

Filter as early as possible to avoid unnecessary work:

```csharp
// ✅ Good - filter before expensive operations
await foreach (var msg in consumer.ConsumeAsync(ct)
    .Where(m => m.Key?.StartsWith("order-") == true)
    .Select(m => ExpensiveDeserialization(m.Value)))
{
    // Only deserialize relevant messages
}

// ❌ Less efficient - deserialize everything then filter
await foreach (var item in consumer.ConsumeAsync(ct)
    .Select(m => ExpensiveDeserialization(m.Value))
    .Where(item => item.Type == "Order"))
{
    // Wasted work on non-order messages
}
```

### Batch Size

Choose batch sizes based on your use case:

```csharp
// Small batches for low-latency processing
.Batch(10)

// Larger batches for bulk operations
.Batch(1000)

// Consider memory when batching large messages
.Batch(100)  // If messages are large
```

## Real-World Examples

### Log Processing

```csharp
// Process only error logs, batch for Elasticsearch
await foreach (var batch in consumer.ConsumeAsync(ct)
    .Where(m => m.Value.Contains("\"level\":\"error\""))
    .Select(m => JsonSerializer.Deserialize<LogEntry>(m.Value)!)
    .Batch(200))
{
    await elasticClient.BulkIndexAsync(batch);
    await consumer.CommitAsync();
}
```

### Event Aggregation

```csharp
// Aggregate events by user
await foreach (var batch in consumer.ConsumeAsync(ct)
    .Where(m => m.Key != null)
    .Batch(1000))
{
    var byUser = batch.GroupBy(m => m.Key);

    foreach (var userEvents in byUser)
    {
        await UpdateUserAggregateAsync(userEvents.Key!, userEvents.ToList());
    }

    await consumer.CommitAsync();
}
```

### Sampling

```csharp
// Take a sample of messages for analysis
var sample = new List<string>();

await foreach (var value in consumer.ConsumeAsync(ct)
    .Where(m => Random.Shared.NextDouble() < 0.01)  // 1% sample
    .Select(m => m.Value)
    .Take(1000))
{
    sample.Add(value);
}

await AnalyzeSampleAsync(sample);
```

Sampling deliberately discards the other 99% of messages. Run it under a dedicated consumer group ID so its committed offsets don't advance past messages a real processing group still needs.
