---
sidebar_position: 11
---

# Dependency Injection

Dekaf plays nicely with ASP.NET Core's DI container. Here's how to wire it up.

DI registration owns each registered client for you. For manual wiring outside DI, prefer `Kafka.Connect(...)` when multiple clients should share a connection pool and memory budget.

## Installation

```bash
dotnet add package Dekaf.Extensions.DependencyInjection
```

## Basic Registration

Use `AddDekaf` to register producers and consumers:

```csharp
using Dekaf.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers(builder.Configuration["Kafka:BootstrapServers"]!)
        .WithLinger(TimeSpan.FromMilliseconds(5))
        .WithBatchSize(64 * 1024)
        .ForReliability());

    dekaf.AddConsumer<string, string>(consumer => consumer
        .WithBootstrapServers(builder.Configuration["Kafka:BootstrapServers"]!)
        .WithGroupId("my-service")
        .WithFetchMinBytes(1024));
});
```

## Full Builder Surface

The DI callback receives the same `ProducerBuilder<TKey,TValue>` and `ConsumerBuilder<TKey,TValue>` used by the non-DI API. Any option available during manual construction is available during registration too.

Before:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers("localhost:9092")
        .WithAcks(Acks.All));
});
```

After:

```csharp
var retryPolicy = new FixedDelayRetryPolicy
{
    Delay = TimeSpan.FromMilliseconds(100),
    MaxAttempts = 3
};

builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers("localhost:9092")
        .WithLinger(TimeSpan.FromMilliseconds(5))
        .WithBatchSize(64 * 1024)
        .WithBufferMemory(256 * 1024 * 1024)
        .WithSaslScramSha512("user", "password")
        .WithRetryPolicy(retryPolicy)
        .ForHighThroughput());

    dekaf.AddConsumer<string, string>(consumer => consumer
        .WithBootstrapServers("localhost:9092")
        .WithGroupId("orders")
        .WithFetchMinBytes(1024)
        .WithFetchMaxBytes(50 * 1024 * 1024)
        .WithPrefetchPipelineDepth(4)
        .WithRetryPolicy(retryPolicy)
        .SubscribeTo("orders"));
});
```

## Using in Services

Inject the interfaces:

```csharp
public class OrderService
{
    private readonly IKafkaProducer<string, string> _producer;

    public OrderService(IKafkaProducer<string, string> producer)
    {
        _producer = producer;
    }

    public async Task PublishOrderAsync(Order order)
    {
        await _producer.ProduceAsync("orders", order.Id, JsonSerializer.Serialize(order));
    }
}
```

## Multiple Producers/Consumers

Register multiple producers or consumers with different type parameters normally:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers(config["Kafka:BootstrapServers"]!)
        .WithLinger(TimeSpan.FromMilliseconds(5))
        .ForReliability());

    dekaf.AddProducer<string, byte[]>(producer => producer
        .WithBootstrapServers(config["Kafka:BootstrapServers"]!)
        .WithBatchSize(128 * 1024)
        .ForHighThroughput());
});
```

If two clients use the same `<TKey, TValue>` pair, register them with service keys.

Before:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers(config["Kafka:Orders:BootstrapServers"]!)
        .WithClientId("orders-producer"));

    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers(config["Kafka:Payments:BootstrapServers"]!)
        .WithClientId("payments-producer"));
});

public class OrderHandler(IKafkaProducer<string, string> producer)
{
    // Ambiguous: both registrations use the same service type.
}
```

After:

```csharp
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, string>("orders", producer => producer
        .WithBootstrapServers(config["Kafka:Orders:BootstrapServers"]!)
        .WithClientId("orders-producer"));

    dekaf.AddProducer<string, string>("payments", producer => producer
        .WithBootstrapServers(config["Kafka:Payments:BootstrapServers"]!)
        .WithClientId("payments-producer"));

    dekaf.AddConsumer<string, string>("orders", consumer => consumer
        .WithBootstrapServers(config["Kafka:Orders:BootstrapServers"]!)
        .WithGroupId("orders-service"));
});

public class OrderHandler(
    [FromKeyedServices("orders")] IKafkaProducer<string, string> producer)
{
    // Receives the orders producer.
}

public class PaymentHandler(
    [FromKeyedServices("payments")] IKafkaProducer<string, string> producer)
{
    // Receives the payments producer.
}
```

The existing unkeyed overload remains the default-client registration. You can use one
unkeyed producer or consumer for the common case, then add keyed clients for additional
clusters, tenants, or workloads that share the same generic type.

Keyed clients also support configuration sections:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, Order>(
        "orders",
        builder.Configuration.GetSection("Kafka:Producers:Orders"),
        producer => producer.WithValueSerializer(new JsonSerializer<Order>()));

    dekaf.AddConsumer<string, Order>(
        "orders",
        builder.Configuration.GetSection("Kafka:Consumers:Orders"),
        consumer => consumer
            .WithValueDeserializer(new JsonDeserializer<Order>())
            .SubscribeTo("orders"));
});
```

## Global Interceptors

Register cross-cutting interceptors (tracing, metrics, audit logging) that apply to all producers or consumers automatically:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    // Global interceptors apply to every producer/consumer
    dekaf.AddGlobalProducerInterceptor(typeof(TracingInterceptor<,>));
    dekaf.AddGlobalConsumerInterceptor(typeof(MetricsInterceptor<,>));

    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers("localhost:9092"));

    dekaf.AddConsumer<string, string>(consumer => consumer
        .WithBootstrapServers("localhost:9092")
        .WithGroupId("my-service"));
});
```

Global interceptors execute before per-instance interceptors, in registration order. They are constructed via `ActivatorUtilities`, so their dependencies are resolved from the DI container.

Open generic interceptor types (e.g., `typeof(TracingInterceptor<,>)`) are automatically closed with the producer's or consumer's `TKey`/`TValue` type arguments. Concrete types that implement the interface for specific type combinations are also supported.

### Per-Instance Interceptors

You can also add interceptors to individual producers or consumers:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddGlobalProducerInterceptor(typeof(TracingInterceptor<,>));

    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers("localhost:9092")
        .AddInterceptor(new AuditInterceptor()));  // Runs after global interceptors
});
```

## Background Consumer Service

Create a hosted service for continuous consumption:

```csharp
public class OrderConsumerService : BackgroundService
{
    private readonly IKafkaConsumer<string, string> _consumer;
    private readonly ILogger<OrderConsumerService> _logger;

    public OrderConsumerService(
        IKafkaConsumer<string, string> consumer,
        ILogger<OrderConsumerService> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _consumer.ConsumeAsync(stoppingToken))
        {
            _logger.LogInformation("Received: {Key}", message.Key);
            // Process message
        }
    }
}

// Registration
builder.Services.AddHostedService<OrderConsumerService>();
```

## Configuration from appsettings.json

```json
{
  "Kafka": {
    "Producers": {
      "Orders": {
        "BootstrapServers": "broker1:9092,broker2:9092",
        "ClientId": "orders-producer",
        "Acks": "All",
        "LingerMs": 5,
        "CompressionType": "Lz4"
      }
    },
    "Consumers": {
      "Orders": {
        "BootstrapServers": [
          "broker1:9092",
          "broker2:9092"
        ],
        "ClientId": "orders-consumer",
        "GroupId": "orders",
        "AutoOffsetReset": "Earliest",
        "FetchMinBytes": 1024
      }
    },
    "Admin": {
      "BootstrapServers": "broker1:9092,broker2:9092",
      "ClientId": "orders-admin"
    }
  }
}
```

```csharp
builder.Services.AddDekaf(dekaf =>
{
    var kafka = builder.Configuration.GetSection("Kafka");

    dekaf.AddProducer<string, Order>(
        kafka.GetSection("Producers:Orders"),
        producer => producer.WithValueSerializer(new JsonSerializer<Order>()));

    dekaf.AddConsumer<string, Order>(
        kafka.GetSection("Consumers:Orders"),
        consumer => consumer
            .WithValueDeserializer(new JsonDeserializer<Order>())
            .SubscribeTo("orders"));

    dekaf.AddAdminClient(kafka.GetSection("Admin"));
});
```

The section keys match `ProducerOptions`, `ConsumerOptions`, and `AdminClientOptions` property names. Configuration is applied first; the optional fluent callback runs after binding, so it can override values or attach objects that cannot be represented in JSON, such as serializers, deserializers, interceptors, retry policies, and rebalance listeners.

## Lifetime Management

Both producers and consumers are registered as singletons. This is intentional—they're expensive to create, thread-safe, and meant to be reused. They'll be disposed automatically when your app shuts down.

Hosted consumer services should use the async disposal path provided by the generic host. `KafkaConsumerService<TKey, TValue>` implements `IAsyncDisposable`, so shutdown can flush and dispose its consumer and optional dead-letter producer without blocking a thread.

Before:

```csharp
service.Dispose(); // synchronous fallback; blocks while cleanup completes
```

After:

```csharp
await service.DisposeAsync(); // awaits consumer and DLQ producer disposal
```

When the service is registered with `AddHostedService`, the host calls the async path during normal shutdown.

## Complete Example

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

var kafkaConfig = builder.Configuration.GetSection("Kafka");

builder.Services.AddDekaf(dekaf =>
{
    // Register producer
    dekaf.AddProducer<string, Order>(producer => producer
        .WithBootstrapServers(kafkaConfig["BootstrapServers"]!)
        .WithValueSerializer(new JsonSerializer<Order>())
        .ForReliability());

    // Register consumer
    dekaf.AddConsumer<string, Order>(consumer => consumer
        .WithBootstrapServers(kafkaConfig["BootstrapServers"]!)
        .WithGroupId(kafkaConfig["GroupId"]!)
        .WithValueDeserializer(new JsonDeserializer<Order>()));
});

// Register background consumer
builder.Services.AddHostedService<OrderProcessorService>();

var app = builder.Build();
app.MapControllers();
app.Run();

// OrderController.cs
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IKafkaProducer<string, Order> _producer;

    public OrdersController(IKafkaProducer<string, Order> producer)
    {
        _producer = producer;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(Order order)
    {
        await _producer.ProduceAsync("orders", order.Id, order);
        return Accepted();
    }
}

// OrderProcessorService.cs
public class OrderProcessorService : BackgroundService
{
    private readonly IKafkaConsumer<string, Order> _consumer;
    private readonly IOrderRepository _repository;

    public OrderProcessorService(
        IKafkaConsumer<string, Order> consumer,
        IOrderRepository repository)
    {
        _consumer = consumer;
        _repository = repository;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var msg in _consumer.ConsumeAsync(ct))
        {
            await _repository.SaveAsync(msg.Value);
        }
    }
}
```
