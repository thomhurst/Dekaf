---
sidebar_position: 2
---

# Batch Production

When you need to send multiple messages, Dekaf provides efficient batch APIs that handle the complexities of concurrent production.

## The Challenge with ValueTask

`ProduceAsync` returns a `ValueTask<RecordMetadata>` for performance reasons - it can complete synchronously when messages are added to an internal buffer. However, this creates a challenge when sending multiple messages:

```csharp
// ❌ Don't do this - ValueTask can only be awaited once
var tasks = messages.Select(m => producer.ProduceAsync("topic", m.Key, m.Value));
await Task.WhenAll(tasks);  // This doesn't work with ValueTask!
```

## ProduceAllAsync

Dekaf provides `ProduceAllAsync` which handles the `ValueTask` conversion internally:

```csharp
// ✅ Use ProduceAllAsync for batch sends
var results = await producer.ProduceAllAsync(messages);
```

### With Message Objects

```csharp
var messages = new[]
{
    ProducerMessage<string, string>.Create("orders", "order-1", orderJson1),
    ProducerMessage<string, string>.Create("orders", "order-2", orderJson2),
    ProducerMessage<string, string>.Create("orders", "order-3", orderJson3),
};

var results = await producer.ProduceAllAsync(messages);

// Results are in the same order as input
for (int i = 0; i < results.Length; i++)
{
    Console.WriteLine($"Message {i}: partition {results[i].Partition}, offset {results[i].Offset}");
}
```

### With Tuples (Same Topic)

When all messages go to the same topic, use the tuple overload to avoid creating message objects:

```csharp
var keyValuePairs = new[]
{
    ("order-1", orderJson1),
    ("order-2", orderJson2),
    ("order-3", orderJson3),
};

var results = await producer.ProduceAllAsync("orders", keyValuePairs);
```

### From a Collection

Works with any `IEnumerable`:

```csharp
var orders = await GetPendingOrdersAsync();

var messages = orders.Select(order =>
    ProducerMessage<string, string>.Create("orders", order.Id, JsonSerializer.Serialize(order))
);

var results = await producer.ProduceAllAsync(messages);
```

## Error Handling

If any message fails, the entire `ProduceAllAsync` throws an `AggregateException`:

```csharp
try
{
    var results = await producer.ProduceAllAsync(messages);
}
catch (AggregateException ex)
{
    foreach (var inner in ex.InnerExceptions)
    {
        if (inner is ProduceException produceEx)
        {
            Console.WriteLine($"Failed to produce: {produceEx.Message}");
        }
    }
}
```

## Partial Success Handling

If you need to handle partial failures (some messages succeed, some fail), use explicit Task conversion:

```csharp
var tasks = messages.Select(async m =>
{
    try
    {
        return (m, Result: await producer.ProduceAsync(m).AsTask(), Error: (Exception?)null);
    }
    catch (Exception ex)
    {
        return (m, Result: default(RecordMetadata), Error: ex);
    }
}).ToList();

var results = await Task.WhenAll(tasks);

var succeeded = results.Where(r => r.Error == null).ToList();
var failed = results.Where(r => r.Error != null).ToList();

Console.WriteLine($"Succeeded: {succeeded.Count}, Failed: {failed.Count}");
```

## Performance Considerations

### Batch Size

Sending many messages at once is more efficient than sending them one by one:

```csharp
// ❌ Inefficient - waits for each message
foreach (var msg in messages)
{
    await producer.ProduceAsync("topic", msg.Key, msg.Value);
}

// ✅ Efficient - sends all at once
await producer.ProduceAllAsync(messages);
```

### Memory Usage

`ProduceAllAsync` materializes the input enumerable to get a count. For very large collections, consider batching:

```csharp
var allMessages = GetMillionsOfMessages();

// Process in batches to control memory
foreach (var batch in allMessages.Chunk(1000))
{
    await producer.ProduceAllAsync(batch);
}
```

### Combining with Fire-and-Forget

For maximum throughput when you don't need results, combine `Send()` with `FlushAsync()`:

```csharp
// Send all messages without waiting
foreach (var msg in messages)
{
    producer.Send("topic", msg.Key, msg.Value);
}

// Ensure all are delivered before continuing
await producer.FlushAsync();
```

## Real-World Example

Here's a complete example processing a batch of orders:

```csharp
public async Task ProcessOrderBatchAsync(IReadOnlyList<Order> orders)
{
    var messages = orders.Select(order => ProducerMessage<string, Order>.Create(
        topic: "orders",
        key: order.Id,
        value: order,
        headers: Headers.Create()
            .Add("source", "batch-processor")
            .Add("batch-size", orders.Count.ToString())
    )).ToList();

    try
    {
        var results = await _producer.ProduceAllAsync(messages);

        _logger.LogInformation(
            "Sent {Count} orders across {Partitions} partitions",
            results.Length,
            results.Select(r => r.Partition).Distinct().Count()
        );
    }
    catch (AggregateException ex)
    {
        _logger.LogError(ex, "Failed to send some orders in batch");
        throw;
    }
}
```
