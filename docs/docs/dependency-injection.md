---
sidebar_position: 11
---

# Dependency Injection

Dekaf plays nicely with ASP.NET Core's DI container. Here's how to wire it up.

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
        .ForReliability());

    dekaf.AddConsumer<string, string>(consumer => consumer
        .WithBootstrapServers(builder.Configuration["Kafka:BootstrapServers"]!)
        .WithGroupId("my-service"));
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

Register multiple producers or consumers with different type parameters:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers(config["Kafka:BootstrapServers"]!)
        .ForReliability());

    dekaf.AddProducer<string, byte[]>(producer => producer
        .WithBootstrapServers(config["Kafka:BootstrapServers"]!)
        .ForHighThroughput());
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
    "BootstrapServers": "localhost:9092",
    "Producer": {
      "ClientId": "my-service-producer"
    },
    "Consumer": {
      "ClientId": "my-service-consumer",
      "GroupId": "my-service"
    }
  }
}
```

```csharp
builder.Services.AddDekaf(dekaf =>
{
    var config = builder.Configuration.GetSection("Kafka");

    dekaf.AddProducer<string, string>(producer => producer
        .WithBootstrapServers(config["BootstrapServers"]!)
        .WithClientId(config["Producer:ClientId"]!));

    dekaf.AddConsumer<string, string>(consumer => consumer
        .WithBootstrapServers(config["BootstrapServers"]!)
        .WithClientId(config["Consumer:ClientId"]!)
        .WithGroupId(config["Consumer:GroupId"]!));
});
```

## Lifetime Management

Both producers and consumers are registered as singletons. This is intentional—they're expensive to create, thread-safe, and meant to be reused. They'll be disposed automatically when your app shuts down.

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
