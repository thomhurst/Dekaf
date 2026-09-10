---
sidebar_position: 12
description: "KafkaConsumerService runs a consumer for your application's lifetime, handling scoped dependencies, failure handling, and shutdown behaviour."
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
        .WithValueDeserializer(new JsonSerializer<Order>()));
});
```

This registers the `IKafkaConsumer<string, Order>` singleton and the hosted service together — equivalent to `AddConsumer` followed by `builder.Services.AddHostedService<OrderProcessorService>()`, which remains available when you want to register them separately. Overloads accept a fluent configuration callback, typed `ConsumerOptions`, or an `IConfiguration` section, each with an optional dead-letter-queue callback. The fluent callback also has a service-provider-aware form, `(serviceProvider, consumer) => ...`, for resolving registered dependencies when the consumer singleton is created.

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

### Scale Out One Consumer Workload In-Process

You can also register the same hosted service type more than once to increase consumer-group
parallelism within one process. Give each registration a unique service key and the same
`GroupId`. Each registration creates a separate hosted-service singleton with its own consumer:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddConsumerService<OrderProcessorService, string, Order>("orders-1", consumer => consumer
        .WithBootstrapServers("localhost:9092")
        .WithGroupId("order-workers"));

    dekaf.AddConsumerService<OrderProcessorService, string, Order>("orders-2", consumer => consumer
        .WithBootstrapServers("localhost:9092")
        .WithGroupId("order-workers"));
});
```

Kafka assigns each partition to at most one consumer in the group at a time, so these service
instances normally process different records. Committed offsets belong to the consumer group,
not an individual service instance; after a shutdown or rebalance, a new partition owner resumes
from the group's committed offset. Uncommitted records can be delivered again, so processing
must remain idempotent. If there are more service instances than partitions, the extra instances
remain idle.

This is competing-consumer scale-out, not broadcast fan-out. To have every service receive every
record independently, give each registration a different `GroupId`; each group then maintains its
own offsets.

When a DLQ callback is supplied, registration verifies at service construction that your subclass actually forwards `DeadLetterOptions` to the base constructor, and fails fast with a clear error if the constructor omits it — a forgotten parameter cannot silently disable dead-lettering.

The service subscribes to `Topics` itself — do not call `SubscribeTo` on the consumer registration as well.

## Override Points

| Member | Required | Purpose |
|---|---|---|
| `Topics` | Yes | Topics to subscribe to. |
| `ProcessAsync` | Yes | Handle one record. Throwing signals a processing failure. |
| `OnErrorAsync` | No | Called on every processing failure before any retry or routing decision. Default logs. |
| `GetFailureDispositionAsync` | No | Makes the terminal retry/discard decision when no retry or routing path succeeds. Default: `Retry`. |
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

When `ProcessAsync` throws, the service works through these layers:

1. **In-place retries** — if an `IRetryPolicy` was passed to the base constructor, the message is retried in place with the policy's delays until the policy is exhausted.
2. **Retry topics** — if `DeadLetterOptions.RetryTopics` is configured, the message is produced to the next retry tier and the offset moves on. See [Dead Letter Queues](consumer/dead-letter-queues.md#tiered-retry-topics).
3. **Dead letter queue** — if `DeadLetterOptions` is configured and the failure count reaches `MaxFailures` (or all retry tiers are exhausted), the original bytes are produced to the DLQ topic.
4. **Terminal disposition** — if no configured retry or durable routing operation succeeds, `GetFailureDispositionAsync` decides whether to preserve or discard the record.

The terminal disposition defaults to `MessageFailureDisposition.Retry`. The exception exits the consume loop, the failed record stays uncommitted, and it is redelivered after restart or rebalance. Under the generic host's default `BackgroundServiceExceptionBehavior`, the failure also stops the host. If that behavior is configured to ignore background-service exceptions, this consumer service still stops; the record remains available for a later service instance.

This guarantee requires after-processing offset staging. `KafkaConsumerService` rejects consumers configured with `WithAtMostOnceProcessing()` at startup because `OffsetStoreTiming.OnDelivery` can commit a record before `ProcessAsync` reports failure. Consumer decorators must forward `IConsumerOffsetStoreTimingConfiguration`; the service fails closed when automatic grouped commits are enabled but timing is hidden. Use the default `WithAtLeastOnceProcessing()` semantics with hosted consumer services.

Override `GetFailureDispositionAsync` to explicitly discard failures your application considers non-retryable. Returning `Discard` allows the loop to continue and acknowledges the record when the next message is pulled, so use it only when losing that record's work is intentional:

```csharp
public sealed class OrderProcessorService : KafkaConsumerService<string, Order>
{
    public OrderProcessorService(
        IKafkaConsumer<string, Order> consumer,
        ILogger<OrderProcessorService> logger)
        : base(consumer, logger) { }

    protected override IEnumerable<string> Topics => ["orders"];

    protected override ValueTask ProcessAsync(
        ConsumeResult<string, Order> result,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    protected override ValueTask<MessageFailureDisposition> GetFailureDispositionAsync(
        MessageFailureContext<string, Order> context,
        CancellationToken cancellationToken)
    {
        if (context.ProcessingException is OrderValidationException)
            return new(MessageFailureDisposition.Discard);

        return base.GetFailureDispositionAsync(context, cancellationToken);
    }
}
```

`MessageFailureContext` includes the record, original processing exception, local attempt number, cumulative failure count, terminal stage, and any retry-topic or DLQ routing exception. An exception thrown by the decision hook also exits the loop and preserves the record.

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

    protected override IEnumerable<string> Topics => ["orders"];

    protected override ValueTask ProcessAsync(
        ConsumeResult<string, Order> result,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
```

When you supply a DLQ callback to `AddConsumerService`, it passes the registered `DeadLetterOptions` into your constructor directly (see [Dead Letter Queues](consumer/dead-letter-queues.md#enabling-the-dlq)). The options are registered keyed per consumer registration — never as a plain singleton — so one consumer's DLQ settings cannot leak into another service. If you wire the hosted service manually with `AddHostedService`, resolve them with `[FromKeyedServices(typeof(IKafkaConsumer<TKey, TValue>))]` (or your service key).

### Exponential retry delays

`ExponentialBackoffRetryPolicy` calculates `min(BaseDelay * 2^(attempt - 1), MaxDelay)`
using one-based retry attempts. With `Jitter = false`, the delay stays capped even
at very large attempt numbers. The default `Jitter = true` multiplies the capped
delay by a random factor from 0.5 up to 1.5, then caps it again at `MaxDelay`.
Delays are truncated to whole ticks.

`BaseDelay`, `MaxDelay`, and `MaxAttempts` must be nonnegative; assigning a negative
value throws `ArgumentOutOfRangeException`. A zero base or maximum delay means
immediate retries, and `MaxAttempts = 0` disables retries. `BaseDelay` may exceed
`MaxDelay`; the maximum still applies to the first retry. Calling `GetNextDelay`
with an attempt less than one throws `ArgumentOutOfRangeException`; attempts
greater than `MaxAttempts` return `null` to stop retrying.

## Shutdown Behavior

`KafkaConsumerServiceOptions` (fifth constructor parameter) controls shutdown:

| Option | Default | Meaning |
|---|---|---|
| `DrainOnShutdown` | `true` | After the consume loop is cancelled, keep processing already-fetched messages until the buffer is empty or the timeout elapses. |
| `ShutdownTimeout` | 30 seconds | Cap on draining and on consumer disposal. |

The full stop sequence is: cancel the consume loop → drain buffered messages (if enabled) → commit final offsets → flush and dispose the DLQ producer → dispose the consumer. One safety exception: if shutdown interrupted a record mid-handling — whether it cancelled your `ProcessAsync`, an in-place retry, or an in-flight DLQ/retry-topic write, in the consume loop or during the drain itself — both draining and the final explicit commit are skipped, because pulling more records would mark the interrupted record processed and an explicit commit vouches for it directly. The consumer's close path still commits everything *proven* processed, so only the interrupted record is left uncommitted and redelivered on restart. This follows the consumer's [delivery contract](consumer/delivery-semantics.md): a record whose processing threw is never committed. One configuration is exempt: strict manual commit mode (`WithOffsetCommitMode(OffsetCommitMode.Manual)` + `WithAutoOffsetStore(false)`) still runs the final commit, because there it covers exactly the offsets your code explicitly stored — never the interrupted record. The service implements `IAsyncDisposable`; when registered via `AddHostedService`, the generic host uses the async path automatically, so shutdown never blocks a thread pool thread.

If the consumer's rebalance listener implements `IPartitionStopListener`, configure
its close callback with `WithPartitionStopTimeout` (default: 5 seconds).
`KafkaConsumerServiceOptions.ShutdownTimeout` is an independent outer wait cap for
draining and consumer disposal. Set it long enough for draining, final commit, DLQ
flush, the partition-stop timeout, and remaining consumer cleanup when shutdown
must await the callback. The generic host's own shutdown timeout may impose another
outer cap.

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

## Hosted Share Consumers

For queue semantics, derive from `KafkaShareConsumerService<TKey, TValue>`. Each record is
acquired from a Kafka share group and receives an explicit record acknowledgement.

```csharp
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Extensions.Hosting;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder();
builder.Services.AddDekaf(dekaf => dekaf
    .AddShareConsumerService<ShareOrderWorker, string, string>(consumer => consumer
        .WithBootstrapServers("localhost:9092")
        .WithGroupId("order-workers")));
await builder.Build().RunAsync();

public sealed class ShareOrderWorker : KafkaShareConsumerService<string, string>
{
    public ShareOrderWorker(
        IKafkaShareConsumer<string, string> consumer,
        ILogger<ShareOrderWorker> logger,
        DeadLetterOptions? deadLetterOptions = null)
        : base(consumer, logger, deadLetterOptions)
    {
    }

    protected override IEnumerable<string> Topics => ["orders"];

    protected override ValueTask ProcessAsync(
        ShareConsumeResult<string, string> record, CancellationToken cancellationToken)
    {
        // Complete the application operation before returning successfully.
        return ValueTask.CompletedTask;
    }
}
```

Fluent hosted registrations start with `ShareAcknowledgementMode.Explicit`. An override that
selects `Implicit` is rejected at service startup. Typed `ShareConsumerOptions` and
`IConfiguration` registrations preserve their configured acknowledgement mode: set
`AcknowledgementMode = ShareAcknowledgementMode.Explicit` explicitly. Custom consumer wrappers
must expose `IShareConsumerConfiguration`; an unverifiable mode is rejected too.

### Independent Workers and Service Keys

Each registration creates an independent hosted service and consumer, including repeated
registrations of the same service class. No constructor needs `[FromKeyedServices]`:

```csharp
builder.Services.AddDekaf(dekaf => dekaf
    .AddShareConsumerService<ShareOrderWorker, string, string>("worker-a", consumer => consumer
        .WithBootstrapServers("localhost:9092").WithGroupId("order-workers"))
    .AddShareConsumerService<ShareOrderWorker, string, string>("worker-b", consumer => consumer
        .WithBootstrapServers("localhost:9092").WithGroupId("order-workers"))
    .AddShareConsumerService<ShareOrderWorker, string, string>("audit", consumer => consumer
        .WithBootstrapServers("localhost:9092").WithGroupId("order-audit")));
```

`worker-a` and `worker-b` compete for records in `order-workers`. `audit` consumes independently
in `order-audit`. **DI service keys select local registrations; Kafka group IDs select broker-side
consumption state.** Different keys do not create independent Kafka subscriptions when the group
ID is the same. Share workers can compete within a partition; partition count does not impose the
same parallelism limit as an ordinary consumer group.

Different service classes with the same key/value types also receive independent consumers.
Registering the same service class with the same service key twice throws before changing any
existing wiring. Consumer configuration, DLQ options, processing failure state, and shutdown are
isolated from other share services and ordinary hosted consumers. Public keyed consumer aliases
resolve the matching registration; use distinct keys when resolving multiple consumers directly.

### Processing, Retries, and Durable Routing

The service handles one delivered record at a time. A successful `ProcessAsync` stages `Accept`.
In-place retries use `IRetryPolicy.GetNextDelay`; otherwise `DeadLetterOptions.MaxFailures` can
supply the in-place attempt limit. With retry topics enabled, an exhausted in-place policy advances
one retry tier per delivery. Retry-topic due times delay processing while the record remains acquired.
This deliberately holds up this worker; other share workers can continue acquiring work.

Retry controls are honored only for configured retry topics derived from `Topics`. Source-topic
records cannot supply scheduling or retry-count controls, and routing derives the source topic
from configuration. Application headers and the original record passed to hooks remain intact.

Configure routing using the same builder as ordinary hosted consumers:

```csharp
builder.Services.AddDekaf(dekaf => dekaf
    .AddShareConsumerService<ShareOrderWorker, string, string>("worker-a",
        consumer => consumer.WithBootstrapServers("localhost:9092").WithGroupId("order-workers"),
        dlq => dlq.WithMaxFailures(3).WithTopicSuffix(".DLQ")
            .WithRetryTopics(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30))));
```

The constructor must accept and forward its `DeadLetterOptions` to `base(...)`. The service
subscribes to source and configured retry topics. Routing copies the original serialized key,
value (including tombstones), and headers. Raw capture uses reusable storage per poll round;
failed routing materializes owned byte arrays. Built-in routing requires the built-in consumer's
raw capture capability. `FireAndForget()` is rejected, and the built-in routing producer always
uses `Acks.All`, including when `ConfigureProducer` requests weaker acknowledgements. Acceptance
follows confirmed delivery to a retry topic or DLQ. A crash between durable routing and source
acknowledgement can still produce duplicates; routing is not a transaction across topics.

Override `OnErrorAsync`, `OnRetryTopicRoutingFailedAsync`, or `OnDeadLetterRoutingFailedAsync` for
logging and metrics. An optional `IDeadLetterPolicy<TKey, TValue>` controls DLQ selection. When no
durable outcome is available, `GetFailureDispositionAsync` receives a
`ShareMessageFailureContext<TKey, TValue>` containing the share record (including `DeliveryCount`),
processing exception, attempt number, cumulative retry-topic failure count, failure stage, and
routing exception.

| Terminal decision | Acknowledgement | Service behavior |
| --- | --- | --- |
| `MessageFailureDisposition.Retry` (default) | `Release` | Stops with the processing or routing exception; host exception policy applies |
| `MessageFailureDisposition.Discard` | `Reject` | Continues polling |

Processing cancellation, interrupted routing, failed renewal, or a hook exception never stages
`Accept`. Final commit submits only explicit outcomes. Failed, released, expired, or unfinished
acquisitions remain eligible for redelivery, subject to broker delivery-count and retention limits.
Explicit discard is irreversible for that share group.

### Acquisition Locks and Shutdown

`KafkaShareConsumerServiceOptions` configures `DrainOnShutdown` (default `true`), `ShutdownTimeout`
(default 30 seconds), and `RenewalInterval` (default 10 seconds). Pass options to the base
constructor. While asynchronous processing, retries, or routing are pending, the service renews
the current acquisition. The renewal interval is capped at one third of the latest broker-reported
acquisition timeout. All polling, acknowledgement submission, renewal, close, and disposal operations
are serialized. Application overrides must not call the consumer or leave processing tasks running
in the background.

Renewal requires broker support for **ShareFetch/ShareAcknowledge v2**. On older brokers a required
renewal fails, cancels processing, and leaves the record for redelivery. Long synchronous handlers
must yield to permit renewal. The built-in consumer supplies a conservative fetch-start timestamp;
if its known acquisition deadline has elapsed, the service refuses to process or accept that record.
Renewal extends only the current record's lock. Other records acquired in the same batch may expire
while a slow record is processed. Broker failures, lock expiry, and process pauses can all cause
redelivery; make application effects idempotent. For tightly limited acquisition on v2 brokers,
configure `WithShareAcquireMode(ShareAcquireMode.RecordLimit)` along with `WithMaxPollRecords(...)`.

On shutdown, polling stops immediately. With draining enabled, only the currently delivered record
finishes within the shared shutdown budget; the helper does not resume `PollAsync` to discover
buffered records because that stream can fetch new acquisitions. With draining disabled, processing
is cancelled immediately. Remaining acquisitions are released on close where possible, or expire on
the broker. Final acknowledgement submission, session close, and producer flushing use the remaining
shutdown budget. A cancelled poll may leave acknowledgement submission uncertain, so successful
processing can be redelivered after shutdown too.

`StopAsync` bounds its wait even if application processing ignores cancellation. Asynchronous
resource disposal waits for that work to finish before disposing its consumer and producer; it cannot
force application code to stop. Use asynchronous host/provider disposal and honor the processing token.
Synchronous `Dispose` requests cancellation and starts the same observed cleanup chain.

### Singleton and Scoped Dependencies

Hosted services and their consumers are singletons. Inject only singleton-safe dependencies directly.
For scoped dependencies such as `DbContext`, inject `IServiceScopeFactory` and create/dispose an async
scope inside `ProcessAsync`; await every scoped operation before returning. Never retain a scoped
service or acquisition in detached work. Scope creation and application serialization can allocate;
they are separate from the helper's synchronous processing overhead.
