---
sidebar_position: 12
---

# Hosted Consumer Services

`Dekaf.Extensions.Hosting` provides `KafkaConsumerService<TKey, TValue>` — a `BackgroundService` base class that runs a consumer for the lifetime of your application and handles the concerns you would otherwise wire up by hand:

- Consumer initialization and topic subscription
- The consume loop, with cancellation on host shutdown
- Per-message error handling with optional in-place retries (`IRetryPolicy`)
- Optional [tiered retry topics and dead letter queue routing](consumer/dead-letter-queues.md)
- Graceful shutdown: drain buffered messages, commit final offsets, flush and dispose the DLQ producer

**This is the recommended way to run a continuous consumer in any app built on the .NET generic host** (ASP.NET Core, Worker Services). Write your processing logic in one override; the service supplies correct lifecycle, shutdown, and failure handling around it. Drop down to a hand-rolled `await foreach` loop only when you need full control over the consume loop itself.

## Installation

```bash
dotnet add package Dekaf.Extensions.Hosting
```

## Minimal Service

Subclass `KafkaConsumerService<TKey, TValue>` and override two members:

```csharp
using Dekaf.Consumer;
using Dekaf.Extensions.Hosting;

public sealed class OrderProcessorService : KafkaConsumerService<string, Order>
{
    private readonly IOrderRepository _repository;

    public OrderProcessorService(
        IKafkaConsumer<string, Order> consumer,
        ILogger<OrderProcessorService> logger,
        IOrderRepository repository)
        : base(consumer, logger)
    {
        _repository = repository;
    }

    protected override IEnumerable<string> Topics => ["orders"];

    protected override async ValueTask ProcessAsync(
        ConsumeResult<string, Order> result, CancellationToken cancellationToken)
    {
        await _repository.SaveAsync(result.Value, cancellationToken);
    }
}
```

Register the consumer and the service in one call with `AddConsumerService`:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddConsumerService<OrderProcessorService, string, Order>(consumer => consumer
        .WithBootstrapServers(builder.Configuration["Kafka:BootstrapServers"]!)
        .WithGroupId("orders-service")
        .WithValueDeserializer(new JsonDeserializer<Order>()));
});
```

This registers the `IKafkaConsumer<string, Order>` singleton and the hosted service together — equivalent to `AddConsumer` followed by `builder.Services.AddHostedService<OrderProcessorService>()`, which remains available when you want to register them separately. Overloads accept a fluent configuration callback, typed `ConsumerOptions`, or an `IConfiguration` section, each with an optional dead-letter-queue callback.

To run several hosted services whose consumers share the same `TKey`/`TValue` pair, use the keyed overloads — each takes a `serviceKey` as the first argument and hands the service its own consumer (and dead-letter options) directly, with no `[FromKeyedServices]` attribute needed on the constructor:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddConsumerService<OrderService, string, string>("orders", consumer => consumer
        .WithBootstrapServers("localhost:9092").WithGroupId("orders"));

    dekaf.AddConsumerService<PaymentService, string, string>("payments", consumer => consumer
        .WithBootstrapServers("localhost:9092").WithGroupId("payments"));
});
```

When a DLQ callback is supplied, registration verifies at service construction that your subclass actually forwards `DeadLetterOptions` to the base constructor, and fails fast with a clear error if the constructor omits it — a forgotten parameter cannot silently disable dead-lettering.

The service subscribes to `Topics` itself — do not call `SubscribeTo` on the consumer registration as well.

## Override Points

| Member | Required | Purpose |
|---|---|---|
| `Topics` | Yes | Topics to subscribe to. |
| `ProcessAsync` | Yes | Handle one record. Throwing signals a processing failure. |
| `OnErrorAsync` | No | Called on every processing failure before any retry or routing decision. Default logs. |
| `OnDeadLetterRoutingFailedAsync` | No | Called when a DLQ produce itself fails. Default logs. |
| `OnRetryTopicRoutingFailedAsync` | No | Called when a retry-topic produce fails. Default logs. |

## Lifetime and Scoped Dependencies

Like every hosted service, the service is a **singleton**: one instance is constructed at startup and lives until shutdown. Constructor injection follows singleton rules:

- Singleton dependencies — `IKafkaProducer<TKey, TValue>`, `ILogger<T>`, repositories over a connection pool — inject fine.
- **Scoped services (`DbContext`, anything registered with `AddScoped`) cannot be constructor-injected.** With the host's default scope validation this fails at startup with "Cannot consume scoped service from singleton"; without validation it silently becomes a captive dependency — one `DbContext` instance shared by every message for the life of the app.
- Transient *disposables* injected into the constructor are also captive: created once, never disposed until shutdown.

For per-message scoped work, inject `IServiceScopeFactory` and create a scope inside `ProcessAsync`:

```csharp
public sealed class OrderProcessorService : KafkaConsumerService<string, Order>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public OrderProcessorService(
        IKafkaConsumer<string, Order> consumer,
        ILogger<OrderProcessorService> logger,
        IServiceScopeFactory scopeFactory)
        : base(consumer, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override IEnumerable<string> Topics => ["orders"];

    protected override async ValueTask ProcessAsync(
        ConsumeResult<string, Order> result, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        db.Orders.Add(result.Value);
        await db.SaveChangesAsync(cancellationToken);
    }
}
```

A scope per message is the standard pattern and its cost is negligible next to any real per-message I/O. If your processing is scope-free, skip all of this and inject singletons directly.

## Failure Handling

When `ProcessAsync` throws, the service works through up to three layers, each optional:

1. **In-place retries** — if an `IRetryPolicy` was passed to the base constructor, the message is retried in place with the policy's delays until the policy is exhausted.
2. **Retry topics** — if `DeadLetterOptions.RetryTopics` is configured, the message is produced to the next retry tier and the offset moves on. See [Dead Letter Queues](consumer/dead-letter-queues.md#tiered-retry-topics).
3. **Dead letter queue** — if `DeadLetterOptions` is configured and the failure count reaches `MaxFailures` (or all retry tiers are exhausted), the original bytes are produced to the DLQ topic.

With none of these configured, the failure is logged via `OnErrorAsync` and the service moves to the next message. Note what that means for delivery: the consumer's [at-least-once offset staging](consumer/delivery-semantics.md) treats a *consumed* record as processed once the loop advances, so a skipped failure is effectively dropped. Configure a DLQ (or rethrow from `OnErrorAsync` after recording the failure) if failed records must be preserved.

```csharp
public sealed class OrderProcessorService : KafkaConsumerService<string, Order>
{
    public OrderProcessorService(
        IKafkaConsumer<string, Order> consumer,
        ILogger<OrderProcessorService> logger,
        DeadLetterOptions deadLetterOptions)   // resolved from DI when configured
        : base(
            consumer,
            logger,
            deadLetterOptions,
            retryPolicy: new FixedDelayRetryPolicy
            {
                Delay = TimeSpan.FromMilliseconds(200),
                MaxAttempts = 3
            })
    {
    }
    // ...
}
```

When you supply a DLQ callback to `AddConsumerService`, it passes the registered `DeadLetterOptions` into your constructor directly (see [Dead Letter Queues](consumer/dead-letter-queues.md#enabling-the-dlq)). The options are registered keyed per consumer registration — never as a plain singleton — so one consumer's DLQ settings cannot leak into another service. If you wire the hosted service manually with `AddHostedService`, resolve them with `[FromKeyedServices(typeof(IKafkaConsumer<TKey, TValue>))]` (or your service key).

## Shutdown Behavior

`KafkaConsumerServiceOptions` (fifth constructor parameter) controls shutdown:

| Option | Default | Meaning |
|---|---|---|
| `DrainOnShutdown` | `true` | After the consume loop is cancelled, keep processing already-fetched messages until the buffer is empty or the timeout elapses. |
| `ShutdownTimeout` | 30 seconds | Cap on draining and on consumer disposal. |

The full stop sequence is: cancel the consume loop → drain buffered messages (if enabled) → commit final offsets → flush and dispose the DLQ producer → dispose the consumer. One safety exception: if shutdown interrupted a record mid-handling — whether it cancelled your `ProcessAsync`, an in-place retry, or an in-flight DLQ/retry-topic write, in the consume loop or during the drain itself — both draining and the final explicit commit are skipped, because pulling more records would mark the interrupted record processed and an explicit commit vouches for it directly. The consumer's close path still commits everything *proven* processed, so only the interrupted record is left uncommitted and redelivered on restart. This follows the consumer's [delivery contract](consumer/delivery-semantics.md): a record whose processing threw is never committed. One configuration is exempt: strict manual commit mode (`WithOffsetCommitMode(OffsetCommitMode.Manual)` + `WithAutoOffsetStore(false)`) still runs the final commit, because there it covers exactly the offsets your code explicitly stored — never the interrupted record. The service implements `IAsyncDisposable`; when registered via `AddHostedService`, the generic host uses the async path automatically, so shutdown never blocks a thread pool thread.

## Delivery Semantics

The service inherits the consumer's guarantees — at-least-once by default, with offsets staged only for records the loop has yielded. See [Delivery Semantics](consumer/delivery-semantics.md) for the precise commit rules. Two service-specific points:

- The final commit during `StopAsync` runs after draining, so cleanly stopped services do not redeliver drained messages on restart.
- DLQ and retry-topic writes are awaited before the loop moves on (default `AwaitDelivery = true`), so a failed record's dead-letter copy is durable before its offset can be committed.

## When to Hand-Roll Instead

Use a plain `BackgroundService` with `consumer.ConsumeAsync(...)` when you need:

- Custom batching or windowing across records before processing
- The [partitioned processing API](consumer/partitioned-processing-api.md) for per-partition parallelism
- Manual offset storage decisions per record (`StoreOffset` on your own schedule)

Everything else — including error handling with DLQ, which is only available through the hosted service — is simpler and safer through `KafkaConsumerService`.
