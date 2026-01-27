---
sidebar_position: 11
---

# Dependency Injection

Dekaf plays nicely with ASP.NET Core's DI container. Here's how to wire it up.

## Installation

```bash
dotnet add package Dekaf.Extensions.DependencyInjection
```

## Registering Producers

```csharp
using Dekaf.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDekafProducer<string, string>(producer => producer
    .WithBootstrapServers(builder.Configuration["Kafka:BootstrapServers"])
    .ForReliability()
);
```

## Registering Consumers

```csharp
builder.Services.AddDekafConsumer<string, string>(consumer => consumer
    .WithBootstrapServers(builder.Configuration["Kafka:BootstrapServers"])
    .WithGroupId("my-service")
    .SubscribeTo("events")
);
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

Use keyed services for multiple instances:

```csharp
// Registration
builder.Services.AddKeyedDekafProducer<string, string>("orders", producer => producer
    .WithBootstrapServers(config["Kafka:BootstrapServers"])
    .ForReliability()
);

builder.Services.AddKeyedDekafProducer<string, string>("events", producer => producer
    .WithBootstrapServers(config["Kafka:BootstrapServers"])
    .ForHighThroughput()
);

// Injection
public class MyService
{
    public MyService(
        [FromKeyedServices("orders")] IKafkaProducer<string, string> orderProducer,
        [FromKeyedServices("events")] IKafkaProducer<string, string> eventProducer)
    {
        // ...
    }
}
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
      "ClientId": "my-service-producer",
      "Acks": "All"
    },
    "Consumer": {
      "ClientId": "my-service-consumer",
      "GroupId": "my-service",
      "Topics": ["orders", "events"]
    }
  }
}
```

```csharp
builder.Services.AddDekafProducer<string, string>(producer =>
{
    var config = builder.Configuration.GetSection("Kafka");
    producer
        .WithBootstrapServers(config["BootstrapServers"])
        .WithClientId(config["Producer:ClientId"]);
});
```

## Lifetime Management

Both producers and consumers are registered as singletons. This is intentional—they're expensive to create, thread-safe, and meant to be reused. They'll be disposed automatically when your app shuts down.

## Complete Example

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

var kafkaConfig = builder.Configuration.GetSection("Kafka");

// Register producer
builder.Services.AddDekafProducer<string, Order>(producer => producer
    .WithBootstrapServers(kafkaConfig["BootstrapServers"])
    .WithValueSerializer(new JsonSerializer<Order>())
    .ForReliability()
);

// Register consumer
builder.Services.AddDekafConsumer<string, Order>(consumer => consumer
    .WithBootstrapServers(kafkaConfig["BootstrapServers"])
    .WithGroupId(kafkaConfig["GroupId"])
    .WithValueDeserializer(new JsonDeserializer<Order>())
    .SubscribeTo("orders")
);

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
