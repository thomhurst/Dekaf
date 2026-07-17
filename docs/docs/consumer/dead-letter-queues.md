---
sidebar_position: 7
---

# Dead Letter Queues and Retry Topics

When processing a record fails repeatedly, you have three choices: stop consuming, silently skip the record, or park it somewhere it can be inspected and replayed. The dead letter queue (DLQ) pattern is the third option: failed records are produced to a companion topic (`orders` → `orders.DLQ`) with headers describing where they came from and why they failed.

Dekaf ships DLQ and tiered retry-topic support in the [hosted consumer service](../hosted-services.md) (`KafkaConsumerService<TKey, TValue>`). It is not part of the raw consumer API — routing decisions belong at the layer that owns the processing loop and offset commits.

## Enabling the DLQ

Through DI, pass a DLQ callback when registering the service. The DLQ producer inherits the consumer's bootstrap servers unless you override them:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddConsumerService<OrderProcessorService, string, Order>(
        consumer => consumer
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("orders-service")
            .WithValueDeserializer(new JsonDeserializer<Order>()),
        dlq => dlq
            .WithMaxFailures(3));
});
```

(The same `dlq` callback is available on `AddConsumer` if you register the consumer and hosted service separately.)

Options are registered per consumer registration, so multiple DLQ-enabled consumers each get their own configuration. If your service's constructor forgets the `DeadLetterOptions` parameter, `AddConsumerService` fails at service construction with a clear error rather than silently running without dead-lettering.

This registers a `DeadLetterOptions` singleton, which your service passes to the base constructor:

```csharp
public sealed class OrderProcessorService : KafkaConsumerService<string, Order>
{
    public OrderProcessorService(
        IKafkaConsumer<string, Order> consumer,
        ILogger<OrderProcessorService> logger,
        DeadLetterOptions deadLetterOptions)
        : base(consumer, logger, deadLetterOptions)
    {
    }
    // ...
}
```

Without DI, construct `DeadLetterOptions` directly — the same properties the builder sets are all `init`-settable.

## Options

| Option | Builder method | Default | Meaning |
|---|---|---|---|
| `TopicSuffix` | `WithTopicSuffix` | `".DLQ"` | Appended to the source topic to form the DLQ topic name. |
| `MaxFailures` | `WithMaxFailures` | `1` | Processing failures required before dead-lettering. |
| `IncludeExceptionInHeaders` | `ExcludeExceptionFromHeaders` | `true` | Include exception message and type in DLQ headers. |
| `AwaitDelivery` | `AwaitDelivery` / `FireAndForget` | `true` | Await broker acknowledgment of the DLQ write before continuing. |
| `BootstrapServers` | `WithBootstrapServers` | consumer's servers | Cluster for the DLQ producer. |
| `ConfigureProducer` | `WithProducerConfig` | — | Customize the internal `byte[]` DLQ producer (acks, compression, TLS, ...). |
| `RetryTopics` | `WithRetryTopics` | disabled | Tiered retry topics tried before the DLQ (below). |

## What a Dead-Lettered Message Looks Like

The DLQ copy carries the **original raw key and value bytes** — the service captures them from the fetch buffer on the first failure, so nothing is re-serialized and records that fail *because* their payload is malformed are preserved byte-for-byte. All original headers are kept, and DLQ metadata is appended:

| Header | Content |
|---|---|
| `dlq.source.topic` | Original topic. |
| `dlq.source.partition` | Original partition. |
| `dlq.source.offset` | Original offset. |
| `dlq.failure.count` | Total processing failures across all hops. |
| `dlq.timestamp` | When the record was dead-lettered (ISO 8601, UTC). |
| `dlq.error.message` | Exception message (unless excluded). |
| `dlq.error.type` | Exception type name (unless excluded). |

The header keys are available as constants on `DeadLetterHeaders`, so a DLQ inspector or replayer never needs magic strings.

## Delivery Guarantee

By default (`AwaitDelivery = true`), the service awaits the broker's acknowledgment of the DLQ write before moving past the failed record. Combined with the consumer's [at-least-once commit staging](delivery-semantics.md), this means a failed record cannot be committed away before its dead-letter copy is durable — a crash mid-routing redelivers the original record rather than losing it.

Call `FireAndForget()` (or set `AwaitDelivery = false`) to trade that guarantee for lower per-failure latency: DLQ writes become fire-and-forget, and a crash after the offset commits but before the DLQ write lands can lose the dead-letter copy. This is only worth considering when failures are frequent enough that the extra round-trip matters — for the typical case where dead-lettering is rare, keep the default.

If the DLQ produce itself fails (DLQ topic missing, cluster unreachable), the service invokes `OnDeadLetterRoutingFailedAsync`, which logs by default, and then moves on — the record is *not* redelivered. Override the hook to raise an alert or stop the service if a lost dead letter is unacceptable in your system:

```csharp
protected override ValueTask OnDeadLetterRoutingFailedAsync(
    Exception exception, ConsumeResult<string, Order> result, CancellationToken cancellationToken)
{
    _metrics.DlqRoutingFailures.Add(1);
    throw new InvalidOperationException(
        $"Could not dead-letter {result.Topic}[{result.Partition}]@{result.Offset}; stopping.", exception);
}
```

## Failure Counting

- **Without retry topics or a retry policy:** the record is retried in place until `MaxFailures` is reached, then dead-lettered. The default `MaxFailures = 1` dead-letters on the first failure.
- **With an `IRetryPolicy`:** the policy's in-place retries run first; when it is exhausted, the total attempt count is compared against `MaxFailures`. Keep `MaxFailures` ≤ the policy's maximum attempts — if the policy gives up before `MaxFailures` is reached, the record is skipped without being dead-lettered (the service logs a warning whenever a failed record is skipped while a DLQ is configured).
- **With retry topics:** each hop makes one local attempt (plus any retry-policy attempts), and the cumulative count travels with the record in headers.

## Tiered Retry Topics

For transient failures (a dependency briefly down), immediate in-place retries are often wasted. Retry topics give failed records escalating delays without ever blocking the main topic's partition:

```csharp
dekaf.AddConsumerService<OrderProcessorService, string, Order>(
    consumer => /* ... */,
    dlq => dlq
        .WithRetryTopics(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5))
        .WithMaxFailures(4));
```

With this configuration a record that keeps failing flows `orders` → `orders-retry-5s` → `orders-retry-30s` → `orders-retry-5m` → `orders.DLQ`. How it works:

- Topic names come from `TopicSuffixFormat` (default `-retry-{0}`), where `{0}` is a compact delay label (`5s`, `30s`, `5m`, `1h`, `2d`). Override with `WithRetryTopicSuffixFormat`.
- The service **automatically subscribes to the retry topics** for each of its `Topics` — no extra configuration or second service.
- Each retry copy carries `retry.*` headers: original topic/partition/offset, cumulative failure count, tier delay, and a due timestamp.
- When a retry-topic record arrives before its due time, the service pauses that partition, waits out the remaining delay, then resumes and reprocesses. The main topic's partitions are never paused by this.
- When all tiers are exhausted, the record goes to the DLQ regardless of `MaxFailures`.

Retry-topic and DLQ writes use the same `AwaitDelivery` setting, and retry-topic produce failures invoke `OnRetryTopicRoutingFailedAsync`.

## Custom Routing Policy

The default policy dead-letters after `MaxFailures` and maps `orders` → `orders{TopicSuffix}`. Implement `IDeadLetterPolicy<TKey, TValue>` to route on exception type or send everything to one shared topic, and pass it as the `deadLetterPolicy` parameter of the `KafkaConsumerService` base constructor (alongside `DeadLetterOptions`, which still controls the producer and delivery settings). The service consults `ShouldDeadLetter` on every failure and `GetDeadLetterTopic` when routing:

```csharp
public sealed class PoisonOnlyPolicy : IDeadLetterPolicy<string, Order>
{
    public bool ShouldDeadLetter(ConsumeResult<string, Order> result, Exception exception, int failureCount)
        => exception is JsonException || failureCount >= 5;

    public string GetDeadLetterTopic(string sourceTopic) => "poison-messages";
}
```

```csharp
public OrderProcessorService(
    IKafkaConsumer<string, Order> consumer,
    ILogger<OrderProcessorService> logger,
    DeadLetterOptions deadLetterOptions)
    : base(consumer, logger, deadLetterOptions,
        retryPolicy: null, serviceOptions: null,
        deadLetterPolicy: new PoisonOnlyPolicy())
{
}
```

One thing the policy does *not* control: how many local attempts a record gets. That still comes from `MaxFailures` (or an `IRetryPolicy`), so for the `failureCount >= 5` branch above to ever be reached, the DLQ registration must allow five attempts — otherwise the record is skipped (with a warning) after the default single attempt:

```csharp
dlq => dlq.WithMaxFailures(5)   // align local attempts with the policy's threshold
```

## Topic Provisioning

Dekaf does not create DLQ or retry topics. Create them ahead of time (an `orders.DLQ` with the same partition count as `orders` preserves the ability to replay in order), or rely on broker auto-creation if your cluster allows it. Retention on DLQ topics is usually set much longer than the source topic — the whole point is that someone gets to look at these records later.
