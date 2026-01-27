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
