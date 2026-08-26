---
sidebar_position: 3
description: "Move a .NET application from Confluent.Kafka to Dekaf, including producers, consumers, delivery semantics, serializers, transactions, and configuration."
---

# Migrate from Confluent.Kafka

Dekaf speaks the Kafka protocol directly, so migrating does not require changing topics, records,
consumer-group offsets, or brokers. It is not an API-compatible replacement for
`Confluent.Kafka`: replace client construction and call sites, then verify the behavioral choices
called out below.

Use this guide for application code. If you only need to reuse existing `ProducerConfig` or
`ConsumerConfig` configuration, see [Confluent configuration migration](./configuration/confluent-migration.md).

## Check compatibility first

Before changing packages, confirm these boundaries:

- Dekaf group consumers use Kafka's KIP-848 `consumer` group protocol and require Kafka 4.0 or
  later. Classic consumer groups and custom client-side assignors are not supported. Kafka can
  migrate compatible groups between Classic and Consumer during a rolling deployment; review the
  [consumer-group migration details](./consumer/consumer-groups.md#classic-protocol-support-decision)
  before mixing clients in one group.
- The core `Dekaf` package targets `net10.0` and `netstandard2.0`. Compression, serialization,
  Schema Registry, Dependency Injection, and Hosting packages currently target `net10.0`.
  See the [package compatibility matrix](./compatibility.md#current-support).
- Audit features, not only configuration keys. Rebalance handlers, offset timing, serializers,
  transactions, statistics/error callbacks, and admin operations have different APIs or semantics.

## Replace packages

Start with the core client. Add optional packages only for features the application uses:

```bash
dotnet add package Dekaf
dotnet add package Dekaf.Extensions.DependencyInjection
dotnet add package Dekaf.Serialization.Json
dotnet add package Dekaf.Compression.Lz4
dotnet add package Dekaf.SchemaRegistry
dotnet add package Dekaf.SchemaRegistry.Avro
dotnet add package Dekaf.SchemaRegistry.Protobuf
```

Keep `Confluent.Kafka` installed while both implementations compile during a staged migration.
Remove it, its Schema Registry SerDes packages, and direct `librdkafka.redist` references only
after every client and serializer has moved.

## API map

| Confluent.Kafka | Dekaf | Migration note |
| --- | --- | --- |
| `ProducerBuilder<TKey, TValue>` | `Kafka.CreateProducer<TKey, TValue>()` | Configure with fluent `With...` methods, then `BuildAsync()` |
| `IProducer<TKey, TValue>` | `IKafkaProducer<TKey, TValue>` | Reuse one thread-safe instance; dispose with `await using` |
| `Message<TKey, TValue>` | `ProducerMessage<TKey, TValue>` | Topic belongs on the Dekaf message, or use the topic/key/value overload |
| `DeliveryResult<TKey, TValue>` | `RecordMetadata` | Contains topic, partition, offset, and timestamp |
| `ProduceAsync` | `ProduceAsync` | Dekaf returns `ValueTask<RecordMetadata>` |
| `Produce` with delivery handler | `FireAsync` with delivery handler | Awaiting `FireAsync` waits for local enqueue/backpressure, not broker delivery |
| `ConsumerBuilder<TKey, TValue>` | `Kafka.CreateConsumer<TKey, TValue>()` | Subscription can be configured on the builder |
| `IConsumer<TKey, TValue>` | `IKafkaConsumer<TKey, TValue>` | Async-disposable; `DisposeAsync` performs graceful close |
| `Consume(CancellationToken)` | `ConsumeAsync(CancellationToken)` | Long-lived `IAsyncEnumerable`; use `ConsumeOneAsync` for polling |
| `ConsumeResult<TKey, TValue>` | `ConsumeResult<TKey, TValue>` | Dekaf exposes key/value directly instead of through `.Message` |
| `Commit` / `StoreOffset` | `CommitAsync` / `StoreOffset` | Explicit commits are asynchronous; stored offsets remain local until committed |
| `InitTransactions` / transaction methods | `InitTransactionsAsync` / `ITransaction<TKey, TValue>` | A Dekaf transaction owns produce, offset, commit, and abort operations |
| `Null` producer key | A nullable reference-type key passed as `null` | Dekaf detects null before serialization and writes a Kafka null key |
| `Ignore` consumer key | `Dekaf.Serialization.Ignore` | Ignores key bytes on read; producing an `Ignore` value writes an empty key, not a null key |

## Migrate a producer

Typical Confluent producer:

<!-- doc-test-ignore: Requires the Confluent.Kafka package being migrated away from. -->
```csharp
using Confluent.Kafka;

using var producer = new ProducerBuilder<string, string>(new ProducerConfig
{
    BootstrapServers = "localhost:9092",
    Acks = Acks.All,
    EnableIdempotence = true,
    LingerMs = 5
}).Build();

var result = await producer.ProduceAsync("orders", new Message<string, string>
{
    Key = "order-123",
    Value = orderJson
}, cancellationToken);
```

Dekaf equivalent:

```csharp
using Dekaf;

await using var producer = await Kafka.CreateProducer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithAcks(Acks.All)
    .WithIdempotence(true)
    .WithLinger(TimeSpan.FromMilliseconds(5))
    .BuildAsync(cancellationToken);

var metadata = await producer.ProduceAsync(
    "orders",
    "order-123",
    orderJson,
    cancellationToken);
```

`ProduceAsync` returns a `ValueTask`. Await it immediately. For parallel bulk production, use
`ProduceAllAsync`; do not collect `ValueTask` instances and await them later.

### Replace callback-based production

Confluent's `Produce` call returns after local enqueue and reports delivery through a callback.
The Dekaf counterpart is `FireAsync`:

```csharp
await producer.FireAsync(
    new ProducerMessage<string, string>
    {
        Topic = "orders",
        Key = "order-123",
        Value = orderJson
    },
    static (metadata, error) =>
    {
        if (error is not null)
            Console.Error.WriteLine(error);
    });

await producer.FlushAsync(cancellationToken);
```

Awaiting `FireAsync` means serialization and local backpressure completed. It does not mean Kafka
acknowledged the record. Delivery failures go to the callback; without a callback, they are logged.
Use `ProduceAsync` whenever application flow depends on delivery success. See
[fire-and-forget production](./producer/fire-and-forget.md) for shutdown and error semantics.

### Preserve record metadata

Use `ProducerMessage<TKey, TValue>` when the record has headers, an explicit partition, or a
timestamp:

```csharp
var headers = Headers.Create()
    .Add("correlation-id", correlationId)
    .Add("content-type", "application/json");

var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
{
    Topic = "orders",
    Key = orderId,
    Value = orderJson,
    Headers = headers,
    Partition = 0,
    Timestamp = DateTimeOffset.UtcNow
}, cancellationToken);
```

Dekaf `Header.Value` is `ReadOnlyMemory<byte>` and `Header.IsValueNull` distinguishes null from an
empty value. Consumed headers materialize lazily from pooled data. Enumerate or copy them before
their owning fetch batch is disposed when they must outlive the current processing scope.

## Migrate a consumer

Typical Confluent poll loop:

<!-- doc-test-ignore: Requires the Confluent.Kafka package being migrated away from. -->
```csharp
using Confluent.Kafka;

using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
{
    BootstrapServers = "localhost:9092",
    GroupId = "orders-service",
    AutoOffsetReset = AutoOffsetReset.Earliest
}).Build();

consumer.Subscribe("orders");

try
{
    while (true)
    {
        var result = consumer.Consume(cancellationToken);
        await HandleOrderAsync(result.Message.Value, cancellationToken);
    }
}
finally
{
    consumer.Close();
}
```

Dekaf uses an asynchronous stream:

```csharp
using Dekaf;

await using var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders-service")
    .WithAutoOffsetReset(AutoOffsetReset.Earliest)
    .SubscribeTo("orders")
    .BuildAsync(cancellationToken);

await foreach (var result in consumer.ConsumeAsync(cancellationToken))
{
    await HandleOrderAsync(result.Value, cancellationToken);
}
```

Cancellation ends the foreground enumeration and unblocks the caller. It does not stop the
consumer's lifetime background prefetch if the consumer remains alive. `await using` calls the
graceful close path, which stops prefetch, performs final offset handling, and leaves the group.
Call `CloseAsync` explicitly only when close timing or close errors must be observed before disposal.

Use `ConsumeOneAsync(timeout, cancellationToken)` when an existing design genuinely needs one
record or a timeout. Prefer `ConsumeAsync` or `ConsumeBatchAsync` for continuous processing.

## Choose offset semantics explicitly

This is the most important behavioral difference.

Confluent's default makes a record eligible for automatic commit immediately before delivering it
to application code. Dekaf's default stages it only after the sequential consume loop requests the
next record. Therefore:

- Confluent default behavior is effectively at-most-once when processing fails after delivery.
- Dekaf default behavior is at-least-once when an exception exits the loop; the failing record can
  be redelivered.
- Catching an exception and continuing tells Dekaf that the record was processed. Use explicit
  offset storage when failed records must remain unacknowledged.

Keep Dekaf's safer default:

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders-service")
    .WithAtLeastOnceProcessing()
    .SubscribeTo("orders")
    .BuildAsync(cancellationToken);
```

Or preserve Confluent's default timing during a behavior-compatible migration:

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders-service")
    .WithAtMostOnceProcessing()
    .SubscribeTo("orders")
    .BuildAsync(cancellationToken);
```

For strict at-least-once processing while catching failures, keep background commits but stage
offsets only after success:

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders-service")
    .WithAutoOffsetStore(false)
    .SubscribeTo("orders")
    .BuildAsync(cancellationToken);

await foreach (var result in consumer.ConsumeAsync(cancellationToken))
{
    await HandleOrderAsync(result.Value, cancellationToken);
    consumer.StoreOffset(result);
}
```

Read [Delivery Semantics](./consumer/delivery-semantics.md) before selecting a mode. When work is
handed to parallel tasks, use the [partitioned processing API](./consumer/partitioned-processing-api.md)
instead of advancing the consume loop ahead of incomplete records.

## Migrate serializers

Built-in `string`, `byte[]`, `ReadOnlyMemory<byte>`, `int`, `long`, `Guid`, and `Ignore` types are
selected automatically. For application types, configure Dekaf serializers on the fluent builder:

```csharp
var json = new JsonSerializer<Order>();

await using var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(json)
    .BuildAsync(cancellationToken);

await using var consumer = await Kafka.CreateConsumer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders-service")
    .WithValueDeserializer(json)
    .SubscribeTo("orders")
    .BuildAsync(cancellationToken);
```

Confluent synchronous serializers return a `byte[]`. Dekaf's `ISerializer<T>` writes directly to
a caller-provided `IBufferWriter<byte>` and its `IDeserializer<T>` reads `ReadOnlyMemory<byte>`.
Port custom serializers to those contracts instead of allocating an intermediate array. See
[custom serializers](./serialization/custom.md) and [JSON serialization](./serialization/json.md).

For Schema Registry, configure Dekaf's Avro or Protobuf serializer with the same registry and
subject-naming strategy. Validate existing payloads and subjects in staging before switching
writers. See [Schema Registry](./serialization/schema-registry.md), especially its subject naming
and [identity-framing migration guidance](./serialization/schema-registry.md#schema-identity-framing-and-confluent-interoperability).

## Migrate transactions

Dekaf represents an active transaction as an `ITransaction<TKey, TValue>`. Initialize the producer
once, create a transaction scope, and perform transaction operations through that scope:

```csharp
await producer.InitTransactionsAsync(cancellationToken);

await using var transaction = producer.BeginTransaction();
try
{
    await transaction.ProduceAsync(
        "orders",
        "order-123",
        orderJson,
        cancellationToken);

    await transaction.CommitAsync(cancellationToken);
}
catch
{
    await transaction.AbortAsync(cancellationToken);
    throw;
}
```

For consume-transform-produce, call `transaction.SendOffsetsToTransactionAsync` with the
consumer's current `ConsumerGroupMetadata` and next offsets. Use a unique transactional ID per live
producer instance and test fencing/recovery before rollout. See [Transactions](./producer/transactions.md).

## Reuse existing configuration

`Dekaf.Extensions.DependencyInjection` can translate supported Confluent-style configuration
without taking a runtime dependency on `Confluent.Kafka`:

```csharp
builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducerFromConfluentConfig<string, string>(
        builder.Configuration.GetSection("Kafka:Producer"));

    dekaf.AddConsumerFromConfluentConfig<string, string>(
        builder.Configuration.GetSection("Kafka:Consumer"),
        consumer => consumer.SubscribeTo("orders"));
});
```

Translation fails during registration for unknown properties, unsupported values, or settings
that cannot be represented exactly. This makes configuration drift visible. Review the full
[compatibility matrix](./configuration/confluent-migration.md#compatibility-matrix) and use the
post-translation fluent callback for native Dekaf settings or deliberate overrides.

## Roll out safely

1. Freeze record contracts: topic, partitioning, key/value bytes, headers, timestamps, and Schema
   Registry subject strategy.
2. Run both clients against a non-production topic and compare produced bytes plus consumed
   values. Use different consumer group IDs when both clients must observe every record.
3. Test cancellation, retry, shutdown, rebalance, and poison-message behavior. Confirm selected
   offset semantics with a forced processing failure.
4. For a rolling consumer-group migration, verify Kafka 4.0+ Consumer-protocol migration settings
   and assignor compatibility before putting both client types in the same group.
5. Compare throughput, p50/p99/max latency, CPU, and allocations under the application's real
   message sizes and concurrency. Retune batching, fetch, compression, and connection settings;
   similarly named defaults are not evidence of equivalent performance.
6. Remove Confluent packages and native deployment assets only after no code path constructs a
   Confluent client or serializer.

Continue with [Producer Basics](./producer/basics.md), [Consumer Basics](./consumer/basics.md),
and [Dependency Injection](./dependency-injection.md) for Dekaf-native patterns.
