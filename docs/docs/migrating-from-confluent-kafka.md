---
sidebar_position: 3
description: "Move a .NET application from Confluent.Kafka to Dekaf, including producers, consumers, delivery semantics, serializers, transactions, and configuration."
---

# Migrate from Confluent.Kafka

Dekaf speaks the Kafka protocol directly, so migrating does not touch your topics, records,
consumer-group offsets, or brokers. What changes is the client code. Dekaf is not an
API-compatible replacement for `Confluent.Kafka`: you replace how clients are built and called,
then check the handful of behavioral differences called out below.

This guide covers application code. If you only want to reuse an existing `ProducerConfig` or
`ConsumerConfig`, see [Confluent configuration migration](./configuration/confluent-migration.md).

## Check compatibility first

Before changing packages, confirm these boundaries:

- Dekaf group consumers use Kafka's KIP-848 `consumer` group protocol and need Kafka 4.0 or
  later. Classic consumer groups and custom client-side assignors are not supported. Kafka can
  migrate compatible groups between Classic and Consumer during a rolling deployment; read the
  [consumer-group migration details](./consumer/consumer-groups.md#classic-protocol-support-decision)
  before mixing clients in one group.
- The core `Dekaf` package targets `net10.0` and `netstandard2.0`. Compression, serialization,
  Schema Registry, Dependency Injection, and Hosting packages target `net8.0` and `net10.0`.
  See the [package compatibility matrix](./compatibility.md#current-support).
- Audit features, not just configuration keys. Rebalance handlers, offset timing, serializers,
  transactions, statistics/error callbacks, and admin operations all have different APIs or
  semantics. The [API map](#api-map) below lists the equivalents.

## Replace packages

Start with the core client. Add optional packages only for features the application uses:

```bash
dotnet add package Dekaf
dotnet add package Dekaf.Extensions.DependencyInjection
dotnet add package Dekaf.Extensions.Hosting
dotnet add package Dekaf.Serialization.Json
dotnet add package Dekaf.Compression.Lz4
dotnet add package Dekaf.SchemaRegistry
dotnet add package Dekaf.SchemaRegistry.Avro
dotnet add package Dekaf.SchemaRegistry.Protobuf
```

Two things that catch people out at this stage:

- **Compression codecs are separate packages.** librdkafka bundles LZ4, Zstd, and Snappy, so
  Confluent's `CompressionType = Lz4` works with no extra packages. In Dekaf, add
  `Dekaf.Compression.Lz4`, `Dekaf.Compression.Zstd`, or `Dekaf.Compression.Snappy` to every
  producer *and every consumer* that handles those topics. A referenced codec package registers
  itself; there is nothing else to configure. Gzip is built in. See [Compression](./compression.md).
- **Both libraries use the same type names.** `Acks`, `AutoOffsetReset`,
  `ConsumeResult<TKey, TValue>`, `Headers`, `Header`, `TopicPartition`, `TopicPartitionOffset`,
  `IAdminClient`, `Ignore`, `ISerializer<T>`, `IDeserializer<T>`, and `KafkaException` exist in
  both `Confluent.Kafka` and Dekaf. A file that imports both will not compile (CS0104). Migrate
  file by file, or alias one side (`using DekafAcks = Dekaf.Producer.Acks;`) in the few files
  that must reference both.

Keep `Confluent.Kafka` installed so both implementations compile during a staged migration.
Remove it, its Schema Registry SerDes packages, and any direct `librdkafka.redist` reference
only after every client and serializer has moved.

Dekaf's types are spread over a few namespaces: `Dekaf` (the `Kafka` entry point,
`TopicPartition`), `Dekaf.Producer` (`Acks`, `ProducerMessage`, `RecordMetadata`),
`Dekaf.Consumer` (`AutoOffsetReset`, `ConsumeResult`, `IRebalanceListener`),
`Dekaf.Serialization` (`Headers`, `Ignore`, serializer interfaces), and `Dekaf.Errors`
(exceptions). The examples below include the `using` directives they need.

## API map

### Producer

| Confluent.Kafka | Dekaf | Migration note |
| --- | --- | --- |
| `ProducerBuilder<TKey, TValue>` | `Kafka.CreateProducer<TKey, TValue>()` | Configure with fluent `With...` methods, then `BuildAsync()` |
| `IProducer<TKey, TValue>` | `IKafkaProducer<TKey, TValue>` | Reuse one thread-safe instance; dispose with `await using` |
| `Message<TKey, TValue>` | `ProducerMessage<TKey, TValue>` | Topic belongs on the Dekaf message, or use the topic/key/value overload |
| `DeliveryResult<TKey, TValue>` | `RecordMetadata` | Topic, partition, offset, and timestamp |
| `ProduceAsync` | `ProduceAsync` | Dekaf returns `ValueTask<RecordMetadata>` |
| `Produce` with delivery handler | `FireAsync` with delivery handler | Awaiting `FireAsync` waits for local enqueue/backpressure, not broker delivery |
| `Flush` before `Dispose` | `FlushAsync` (optional) | `DisposeAsync` flushes pending messages; use `FlushAsync` only for an explicit checkpoint |
| `ProduceException<TKey, TValue>`, `Error.IsFatal` | `ProduceException`, `KafkaException.IsRetriable` | All Dekaf exceptions derive from `KafkaException` in `Dekaf.Errors` |
| `InitTransactions` / transaction methods | `InitTransactionsAsync` / `ITransaction<TKey, TValue>` | A Dekaf transaction owns produce, offset, commit, and abort operations |
| `KafkaTxnRequiresAbortException` | `AbortableTransactionException` / `FatalTransactionException` | Abortable: abort, then retry in a new transaction. Fatal: the producer is fenced or unusable, so recreate it |
| `Null` producer key | A nullable reference-type key passed as `null` | Dekaf detects null before serialization and writes a Kafka null key |

### Consumer

| Confluent.Kafka | Dekaf | Migration note |
| --- | --- | --- |
| `ConsumerBuilder<TKey, TValue>` | `Kafka.CreateConsumer<TKey, TValue>()` | Subscription can be configured on the builder |
| `IConsumer<TKey, TValue>` | `IKafkaConsumer<TKey, TValue>` | Async-disposable; `DisposeAsync` performs graceful close |
| `Consume(CancellationToken)` | `ConsumeAsync(CancellationToken)` | Long-lived `IAsyncEnumerable` |
| `Consume(TimeSpan)` | `ConsumeOneAsync(TimeSpan, CancellationToken)` | Returns `null` when the timeout elapses |
| `ConsumeResult<TKey, TValue>` | `ConsumeResult<TKey, TValue>` | Key and value are on the result itself, not under `.Message` |
| `Commit` / `StoreOffset` | `CommitAsync` / `StoreOffset` | Explicit commits are asynchronous; stored offsets stay local until committed |
| `SetPartitionsAssignedHandler`, `SetPartitionsRevokedHandler`, `SetPartitionsLostHandler` | `WithRebalanceListener(IRebalanceListener)` | One interface with `OnPartitionsAssignedAsync`, `OnPartitionsRevokedAsync`, and `OnPartitionsLostAsync`. See [rebalance listener](./consumer/consumer-groups.md#rebalance-listener) |
| `PartitionAssignmentStrategy` | `WithGroupRemoteAssignor("uniform")` or `"range"` | Assignment runs on the broker under KIP-848, and every rebalance is cooperative. See [assignors](./consumer/consumer-groups.md#rebalance-protocols-and-assignors) |
| `Assign`, `Seek`, `Pause`, `Resume` | `Assign`, `Seek`, `Pause`, `Resume` | Same names on the consumer. See [manual assignment](./consumer/manual-assignment.md) |
| `Position`, `Committed`, `QueryWatermarkOffsets` | `Positions.GetPosition`, `GetCommittedOffsetsAsync`, `QueryWatermarkOffsetsAsync` | See [offset management](./consumer/offset-management.md) |
| `Ignore` consumer key | `Dekaf.Serialization.Ignore` | Ignores key bytes on read; producing an `Ignore` value writes an empty key, not a null key |
| Poll loop inside a `BackgroundService` | `KafkaConsumerService<TKey, TValue>` | From `Dekaf.Extensions.Hosting`; override `ProcessAsync`. See [hosted consumer services](./hosted-services.md) |

### Diagnostics and admin

| Confluent.Kafka | Dekaf | Migration note |
| --- | --- | --- |
| `SetErrorHandler`, `SetLogHandler` | `WithLoggerFactory(ILoggerFactory)` | Dekaf logs through `Microsoft.Extensions.Logging`. Errors that need a decision are thrown from the call that failed |
| `SetStatisticsHandler` | OpenTelemetry `Meter` named `"Dekaf"` | No periodic JSON blob; metrics are standard .NET instruments. See [observability](./observability.md) |
| `AdminClientBuilder` / `IAdminClient` | `Kafka.CreateAdminClient()` / `Dekaf.Admin.IAdminClient` | Topics, partitions, configs, ACLs, consumer-group offsets, and more |

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
using Dekaf.Producer;

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

`ProduceAsync` returns a `ValueTask`, so await it straight away. For parallel bulk production
use `ProduceAllAsync`, or call `.AsTask()` if you need to hold on to a result. Never collect raw
`ValueTask` instances and await them later.

Disposal flushes. `await using` waits for pending messages before closing, so the `Flush` call
Confluent needs before `Dispose` has nothing to port to. `FlushAsync` still exists for an
explicit checkpoint, such as before acknowledging an upstream message.

### Replace callback-based production

Confluent's `Produce` returns after local enqueue and reports delivery through a callback. The
Dekaf counterpart is `FireAsync`:

```csharp
using Dekaf.Producer;

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

Awaiting `FireAsync` means serialization and local backpressure completed. It does not mean
Kafka acknowledged the record. Delivery failures go to the callback; without a callback they are
logged and otherwise dropped. Use `ProduceAsync` whenever application flow depends on delivery
success. See [fire-and-forget production](./producer/fire-and-forget.md) for shutdown and error
semantics.

### Preserve record metadata

Use `ProducerMessage<TKey, TValue>` when the record has headers, an explicit partition, or a
timestamp:

```csharp
using Dekaf.Producer;
using Dekaf.Serialization;

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

Dekaf `Header.Value` is `ReadOnlyMemory<byte>`, and `Header.IsValueNull` distinguishes a null
value from an empty one. On the consumer side, headers are read lazily from pooled fetch
buffers. Read or copy them while you are processing the record; if a header must outlive that
(queued for later, say), copy it before the fetch batch that owns it is disposed.

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
using Dekaf.Consumer;

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

Cancelling the token ends the `await foreach` and hands control back to you. The consumer itself
stays alive and keeps prefetching until it is disposed. `await using` runs the graceful close: it
stops prefetch, does the final offset handling, and leaves the group. Call `CloseAsync` yourself
only when you need to observe close timing or close errors before disposal.

Use `ConsumeOneAsync(timeout, cancellationToken)` when an existing design genuinely needs one
record at a time or a timeout. Prefer `ConsumeAsync` or `ConsumeBatchAsync` for continuous
processing.

If the Confluent loop lived in a `BackgroundService`, consider
`KafkaConsumerService<TKey, TValue>` from `Dekaf.Extensions.Hosting` instead of porting the loop.
It owns subscription, cancellation on host shutdown, per-message retries, dead-letter routing,
and graceful close; you implement a single `ProcessAsync` override. See
[hosted consumer services](./hosted-services.md).

## Choose offset semantics explicitly

This is the most important behavioral difference, and it is easy to miss because the code looks
the same.

With Confluent's defaults (`EnableAutoOffsetStore = true`, `EnableAutoCommit = true`), a
record's offset is stored the moment `Consume` returns it, before your code runs. If processing
then throws, the next auto-commit can still commit that offset, and the record is never
redelivered. That is effectively at-most-once.

Dekaf's default is at-least-once: a record is staged for commit only when your loop asks for
the next one. In practice:

- An exception that escapes the loop leaves the failing record uncommitted, so it is redelivered.
- Catching the exception and continuing counts as "processed". The next iteration stages the
  record, and it will be committed.
- Offsets are positions, not per-record acknowledgements. Committing record 42 also commits
  record 41, whether or not 41 succeeded.

Pick one of these three modes deliberately.

**Keep the at-least-once default.** There is nothing to configure. `WithAtLeastOnceProcessing()`
exists if you want the choice visible in code:

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders-service")
    .WithAtLeastOnceProcessing()
    .SubscribeTo("orders")
    .BuildAsync(cancellationToken);
```

**Match Confluent's timing** for a behavior-compatible migration where nothing else should
change yet:

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders-service")
    .WithAtMostOnceProcessing()
    .SubscribeTo("orders")
    .BuildAsync(cancellationToken);
```

**Acknowledge explicitly** when you catch and handle failures inside the loop. Turn off
automatic offset storage and store each offset yourself. Background commits stay on, so this
adds no per-message round trip. Because offsets are positions, park a failed record somewhere
durable (a dead-letter topic, for example) before storing its offset; otherwise the next
successful record on that partition commits past it:

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders-service")
    .WithAutoOffsetStore(false)
    .SubscribeTo("orders")
    .BuildAsync(cancellationToken);

await foreach (var result in consumer.ConsumeAsync(cancellationToken))
{
    try
    {
        await HandleOrderAsync(result.Value, cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Order failed; sending to dead-letter topic");

        // `producer` is any IKafkaProducer<string, string> the service already owns.
        await producer.ProduceAsync("orders.dead-letter", result.Key, result.Value, cancellationToken);
    }

    consumer.StoreOffset(result);
}
```

If you would rather stop and redeliver than park the record, let the exception escape the loop
instead of catching it.

Read [Delivery Semantics](./consumer/delivery-semantics.md) before choosing. When work is handed
to parallel tasks, use the [partitioned processing API](./consumer/partitioned-processing-api.md)
rather than letting the consume loop run ahead of unfinished records.

## Migrate serializers

Built-in `string`, `byte[]`, `ReadOnlyMemory<byte>`, `int`, `long`, `Guid`, and `Ignore` types
are selected automatically. For application types, configure Dekaf serializers on the fluent
builder:

```csharp
using Dekaf.Serialization.Json;

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

Confluent's synchronous serializers return a `byte[]`. Dekaf's `ISerializer<T>` writes directly
to a caller-provided `IBufferWriter<byte>`, and its `IDeserializer<T>` reads
`ReadOnlyMemory<byte>`. Port custom serializers to those contracts instead of allocating an
intermediate array. See [custom serializers](./serialization/custom.md) and
[JSON serialization](./serialization/json.md).

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

Where Confluent throws `KafkaTxnRequiresAbortException` or sets `Error.IsFatal`, Dekaf throws
one of two types. `AbortableTransactionException` means the current transaction is broken: abort
it, then start a new one and retry. `FatalTransactionException` means the producer itself is
unusable (fenced by another instance, for example): dispose it and create a new producer.

For consume-transform-produce, call `transaction.SendOffsetsToTransactionAsync` with the
consumer's next offsets and its `ConsumerGroupMetadata`. Use a unique transactional ID per live
producer instance and test fencing and recovery before rollout. See
[Transactions](./producer/transactions.md).

## Reuse existing configuration

`Dekaf.Extensions.DependencyInjection` can translate supported Confluent-style configuration
without taking a runtime dependency on `Confluent.Kafka`:

```csharp
using Dekaf.Extensions.DependencyInjection;

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
that cannot be represented exactly, so configuration drift shows up at startup rather than in
production. Review the full [compatibility matrix](./configuration/confluent-migration.md#compatibility-matrix)
and use the post-translation fluent callback for native Dekaf settings or deliberate overrides.
TLS and SASL settings translate too; for the native builder methods see the
[security](./security/tls.md) pages.

## Roll out safely

1. Freeze record contracts: topic, partitioning, key/value bytes, headers, timestamps, and Schema
   Registry subject strategy.
2. Run both clients against a non-production topic and compare produced bytes plus consumed
   values. Use different consumer group IDs when both clients must observe every record.
3. Test cancellation, retry, shutdown, rebalance, and poison-message behavior. Confirm the
   offset semantics you chose with a forced processing failure.
4. For a rolling consumer-group migration, set the broker's `group.consumer.migration.policy`
   to allow the direction you need, and confirm the assignor before both client types share a
   group.
5. Compare throughput, p50/p99/max latency, CPU, and allocations under the application's real
   message sizes and concurrency. Retune batching, fetch, compression, and connection settings;
   similarly named defaults are not evidence of equivalent performance.
6. Remove Confluent packages and native deployment assets only after no code path constructs a
   Confluent client or serializer.

Continue with [Producer Basics](./producer/basics.md), [Consumer Basics](./consumer/basics.md),
and [Dependency Injection](./dependency-injection.md) for Dekaf-native patterns.
