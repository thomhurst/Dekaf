---
sidebar_position: 4
---

# Message Headers

Headers allow you to attach metadata to messages without including it in the message value. Common uses include trace IDs, message types, routing information, and timestamps.

## Adding Headers

### Using the Headers Builder

The fluent `Headers` API makes it easy to build headers:

```csharp
var headers = Headers.Create()
    .Add("correlation-id", correlationId)
    .Add("source-service", "order-api")
    .Add("message-type", "OrderCreated");

await producer.ProduceAsync(new ProducerMessage<string, string>
{
    Topic = "orders",
    Key = orderId,
    Value = orderJson,
    Headers = headers
});
```

### Quick Creation

Create headers with a single key-value pair:

```csharp
var headers = Headers.Create("trace-id", traceId);
```

### Extension Method

Use the extension method for a cleaner API:

```csharp
using Dekaf.Producer;

await producer.ProduceAsync("orders", orderId, orderJson, headers);
```

## Conditional Headers

Add headers only when certain conditions are met:

```csharp
var headers = Headers.Create()
    .Add("request-id", requestId)
    .AddIfNotNull("user-id", userId)           // Only if userId != null
    .AddIfNotNullOrEmpty("tenant", tenantId)   // Only if not null or empty
    .AddIf(isRetry, "retry-count", retryCount.ToString());  // Only if condition is true
```

This is cleaner than:

```csharp
// ❌ Verbose alternative
var headers = Headers.Create().Add("request-id", requestId);
if (userId != null) headers.Add("user-id", userId);
if (!string.IsNullOrEmpty(tenantId)) headers.Add("tenant", tenantId);
if (isRetry) headers.Add("retry-count", retryCount.ToString());
```

## Adding Multiple Headers

Add headers from a dictionary or collection:

```csharp
var metadata = new Dictionary<string, string>
{
    ["version"] = "1.0",
    ["encoding"] = "utf-8",
    ["schema-id"] = "12345"
};

var headers = Headers.Create().AddRange(metadata);
```

## Binary Headers

Headers can contain binary data:

```csharp
// Add binary header
var signature = ComputeSignature(messageBody);
headers.Add("signature", signature);  // byte[]

// Or from existing memory
headers.Add("checksum", checksumBytes.AsMemory());
```

## Reading Headers (Consumer Side)

When consuming messages, access headers from the `ConsumeResult`:

```csharp
await foreach (var message in consumer.ConsumeAsync(ct))
{
    // Headers is IReadOnlyList<RecordHeader>
    if (message.Headers != null)
    {
        foreach (var header in message.Headers)
        {
            Console.WriteLine($"{header.Key}: {header.GetValueString()}");
        }
    }

    // Or get a specific header
    var traceId = message.Headers?.FirstOrDefault(h => h.Key == "trace-id")?.GetValueString();
}
```

## Common Header Patterns

### Distributed Tracing

```csharp
var headers = Headers.Create()
    .Add("trace-id", Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString())
    .Add("span-id", Activity.Current?.SpanId.ToString() ?? "")
    .Add("parent-span-id", Activity.Current?.ParentSpanId.ToString() ?? "");
```

### Message Typing

```csharp
var headers = Headers.Create()
    .Add("message-type", typeof(OrderCreatedEvent).FullName!)
    .Add("message-version", "2")
    .Add("content-type", "application/json");
```

### Routing

```csharp
var headers = Headers.Create()
    .Add("tenant-id", tenantId)
    .Add("region", region)
    .Add("priority", priority.ToString());
```

### Dead Letter Queue Context

```csharp
var headers = Headers.Create()
    .Add("original-topic", originalTopic)
    .Add("original-partition", partition.ToString())
    .Add("original-offset", offset.ToString())
    .Add("failure-reason", exception.Message)
    .Add("failure-time", DateTimeOffset.UtcNow.ToString("O"));
```

## Performance Considerations

- Headers are included in the message size limit
- String values are UTF-8 encoded
- Headers are compressed along with the message body
- Keep header keys short and meaningful

```csharp
// ✅ Good - short, meaningful keys
.Add("tid", traceId)
.Add("src", "order-svc")

// ❌ Avoid - verbose keys waste bytes
.Add("x-correlation-trace-identifier", traceId)
.Add("originating-service-name", "order-service")
```

## Complete Example

```csharp
public class OrderEventPublisher
{
    private readonly IKafkaProducer<string, string> _producer;

    public async Task PublishOrderCreatedAsync(Order order, string userId, string traceId)
    {
        var headers = Headers.Create()
            // Tracing
            .Add("trace-id", traceId)
            .Add("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())

            // Event metadata
            .Add("event-type", "OrderCreated")
            .Add("event-version", "1")

            // Context
            .AddIfNotNull("user-id", userId)
            .Add("source", "order-api");

        var message = new ProducerMessage<string, string>
        {
            Topic = "order-events",
            Key = order.Id,
            Value = JsonSerializer.Serialize(order),
            Headers = headers
        };

        await _producer.ProduceAsync(message);
    }
}
```
