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

`AddConsumerService` uses the unkeyed consumer registration, where the last-registered consumer of a given `TKey`/`TValue` wins. To run several hosted services whose consumers share the same type pair, register each consumer with a service key via `AddConsumer(serviceKey, ...)`, add the services with `AddHostedService`, and inject the right consumer with `[FromKeyedServices]` — see [keyed clients](dependency-injection.md#multiple-producersconsumers).

The service subscribes to `Topics` itself — do not call `SubscribeTo` on the consumer registration as well.

## Override Points

| Member | Required | Purpose |
|---|---|---|
| `Topics` | Yes | Topics to subscribe to. |
| `ProcessAsync` | Yes | Handle one record. Throwing signals a processing failure. |
| `OnErrorAsync` | No | Called on every processing failure before any retry or routing decision. Default logs. |
| `OnDeadLetterRoutingFailedAsync` | No | Called when a DLQ produce itself fails. Default logs. |
| `OnRetryTopicRoutingFailedAsync` | No | Called when a retry-topic produce fails. Default logs. |

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

`DeadLetterOptions` is registered as a singleton when you pass a DLQ callback to `AddConsumer` (see [Dead Letter Queues](consumer/dead-letter-queues.md#enabling-the-dlq)), so the constructor parameter above resolves automatically.

## Shutdown Behavior

`KafkaConsumerServiceOptions` (fifth constructor parameter) controls shutdown:

| Option | Default | Meaning |
|---|---|---|
| `DrainOnShutdown` | `true` | After the consume loop is cancelled, keep processing already-fetched messages until the buffer is empty or the timeout elapses. |
| `ShutdownTimeout` | 30 seconds | Cap on draining and on consumer disposal. |

The full stop sequence is: cancel the consume loop → drain buffered messages (if enabled) → commit final offsets → flush and dispose the DLQ producer → dispose the consumer. The service implements `IAsyncDisposable`; when registered via `AddHostedService`, the generic host uses the async path automatically, so shutdown never blocks a thread pool thread.

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
