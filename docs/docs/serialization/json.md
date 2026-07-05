---
sidebar_position: 2
---

# JSON Serialization

JSON is the easy choice when you're working with complex objects. Install the package and you're off.

## Installation

```bash
dotnet add package Dekaf.Serialization.Json
```

## Basic Usage

### Producer

```csharp
using Dekaf.Serialization.Json;

var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(new JsonSerializer<Order>())
    .BuildAsync();

var order = new Order
{
    Id = "order-123",
    CustomerId = "customer-456",
    Total = 99.99m,
    Items = new[] { "item1", "item2" }
};

await producer.ProduceAsync("orders", order.Id, order);
```

### Consumer

```csharp
using Dekaf.Serialization.Json;

var consumer = await Kafka.CreateConsumer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processors")
    .WithValueDeserializer(new JsonSerializer<Order>())
    .SubscribeTo("orders")
    .BuildAsync();

await foreach (var message in consumer.ConsumeAsync(ct))
{
    Order order = message.Value;
    Console.WriteLine($"Processing order {order.Id} for ${order.Total}");
}
```

## Custom JsonSerializerOptions

Configure System.Text.Json behavior:

```csharp
using Dekaf;

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(new JsonSerializer<Order>(options))
    .BuildAsync();
```

## NativeAOT

For NativeAOT, use System.Text.Json source-generated metadata instead of reflection-based `JsonSerializerOptions`:

```csharp
using System.Text.Json.Serialization;
using Dekaf.Serialization.Json;

[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(OrderKey))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class OrderJsonContext : JsonSerializerContext { }

var serializer = new JsonSerializer<Order>(OrderJsonContext.Default.Order);

await using var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(serializer)
    .BuildAsync();
```

The fluent helpers also accept source-generated metadata:

```csharp
await using var producer = await Kafka.CreateProducer<OrderKey, Order>()
    .WithBootstrapServers("localhost:9092")
    .UseJsonKeySerializer(OrderJsonContext.Default.OrderKey)
    .UseJsonSerializer(OrderJsonContext.Default.Order)
    .BuildAsync();

await using var consumer = await Kafka.CreateConsumer<OrderKey, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processors")
    .UseJsonKeyDeserializer(OrderJsonContext.Default.OrderKey)
    .UseJsonDeserializer(OrderJsonContext.Default.Order)
    .SubscribeTo("orders")
    .BuildAsync();
```

`JsonSerializerOptions` overloads remain available for non-AOT applications, but they can require runtime reflection depending on the configured converters and payload types.

## Both Key and Value

Serialize both key and value as JSON:

```csharp
using Dekaf;

var producer = await Kafka.CreateProducer<OrderKey, OrderEvent>()
    .WithBootstrapServers("localhost:9092")
    .WithKeySerializer(new JsonSerializer<OrderKey>())
    .WithValueSerializer(new JsonSerializer<OrderEvent>())
    .BuildAsync();

await producer.ProduceAsync("order-events",
    new OrderKey { TenantId = "acme", OrderId = "123" },
    new OrderCreated { Amount = 99.99m }
);
```

## Error Handling

JSON deserialization errors throw `SerializationException`:

```csharp
try
{
    await foreach (var message in consumer.ConsumeAsync(ct))
    {
        ProcessOrder(message.Value);
    }
}
catch (SerializationException ex)
{
    _logger.LogError(ex, "Failed to deserialize message");
    // Handle malformed JSON
}
```

## Polymorphic Serialization

For polymorphic types, configure the serializer:

```csharp
var options = new JsonSerializerOptions
{
    TypeInfoResolver = new DefaultJsonTypeInfoResolver()
};

// With .NET 7+ polymorphism attributes
[JsonDerivedType(typeof(OrderCreated), "created")]
[JsonDerivedType(typeof(OrderShipped), "shipped")]
public abstract class OrderEvent { }

public class OrderCreated : OrderEvent { public decimal Amount { get; set; } }
public class OrderShipped : OrderEvent { public string TrackingId { get; set; } }
```

## Performance Considerations

- JSON serialization adds overhead compared to binary formats
- Use source generators for NativeAOT and better performance:

```csharp
[JsonSerializable(typeof(Order))]
public partial class OrderJsonContext : JsonSerializerContext { }

var serializer = new JsonSerializer<Order>(OrderJsonContext.Default.Order);
```

## Complete Example

```csharp
using Dekaf;

public record Order(
    string Id,
    string CustomerId,
    decimal Total,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderItem> Items
);

public record OrderItem(string ProductId, int Quantity, decimal Price);

// Producer
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

await using var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(new JsonSerializer<Order>(jsonOptions))
    .BuildAsync();

var order = new Order(
    Id: "order-123",
    CustomerId: "cust-456",
    Total: 149.99m,
    CreatedAt: DateTimeOffset.UtcNow,
    Items: new[]
    {
        new OrderItem("prod-1", 2, 49.99m),
        new OrderItem("prod-2", 1, 50.01m)
    }
);

await producer.ProduceAsync("orders", order.Id, order);

// Consumer
await using var consumer = await Kafka.CreateConsumer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processors")
    .WithValueDeserializer(new JsonSerializer<Order>(jsonOptions))
    .SubscribeTo("orders")
    .BuildAsync();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    Console.WriteLine($"Order {msg.Value.Id}: ${msg.Value.Total}");
}
```
