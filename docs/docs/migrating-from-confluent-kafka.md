---
sidebar_position: 3
description: "Move a .NET application from Confluent.Kafka to Dekaf, including producers, consumers, delivery semantics, serializers, transactions, and configuration."
---

# Migrate from Confluent.Kafka

Nothing on the Kafka side changes when you move to Dekaf: same topics, same records, same
consumer-group offsets, same brokers. Only your client code changes. Dekaf isn't a drop-in
replacement for `Confluent.Kafka`, though. You'll rewrite how clients are built and called, and a
few things behave differently in ways that matter. This guide walks through both.

It covers application code. If all you want is to reuse an existing `ProducerConfig` or
`ConsumerConfig`, see [Confluent configuration migration](./configuration/confluent-migration.md).

## Check compatibility first

Before you touch any packages, make sure these three things hold:

- **Your brokers run Kafka 4.0 or later.** Dekaf group consumers use the KIP-848 `consumer`
  group protocol. There's no support for Classic consumer groups or custom client-side
  assignors. Kafka can move a group between Classic and Consumer during a rolling deployment,
  but read the [consumer-group migration details](./consumer/consumer-groups.md#classic-protocol-support-decision)
  before you put both kinds of client in one group.
- **Your target framework is supported.** The core `Dekaf` package targets `net10.0` and
  `netstandard2.0`. The compression, serialization, Schema Registry, Dependency Injection, and
  Hosting packages target `net8.0` and `net10.0`. See the
  [package compatibility matrix](./compatibility.md#current-support).
- **You know which features you use, not just which config keys.** Rebalance handlers, offset
  timing, serializers, transactions, statistics and error callbacks, and admin operations all
  work differently in Dekaf. The [API map](#api-map) below shows what each one maps to.

## Replace packages

Start with the core client and add the optional packages your application actually uses:

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

Two things catch people out here:

- **Compression codecs are separate packages.** librdkafka bundles LZ4, Zstd, and Snappy, so
  `CompressionType = Lz4` just works in Confluent. In Dekaf you add `Dekaf.Compression.Lz4`,
  `Dekaf.Compression.Zstd`, or `Dekaf.Compression.Snappy`, and you need it on every consumer of
  those topics too, not just the producers. Referencing the package is enough; it registers
  itself. Gzip is built in. See [Compression](./compression.md).
- **Both libraries use the same type names.** `Acks`, `AutoOffsetReset`,
  `ConsumeResult<TKey, TValue>`, `Headers`, `Header`, `TopicPartition`, `TopicPartitionOffset`,
  `IAdminClient`, `Ignore`, `ISerializer<T>`, `IDeserializer<T>`, and `KafkaException` all exist
  on both sides. A file that imports both namespaces won't compile (CS0104). Migrate one file at
  a time, and in the few files that really need both, alias one side:
  `using DekafAcks = Dekaf.Producer.Acks;`.

Leave `Confluent.Kafka` installed while you migrate so everything keeps compiling. Once nothing
constructs a Confluent client or serializer any more, remove it along with its Schema Registry
SerDes packages and any direct `librdkafka.redist` reference.

One more thing to know before the examples: Dekaf's types live in a few namespaces rather than
one. `Dekaf` has the `Kafka` entry point and `TopicPartition`. `Dekaf.Producer` has `Acks`,
`ProducerMessage`, and `RecordMetadata`. `Dekaf.Consumer` has `AutoOffsetReset`,
`ConsumeResult`, and `IRebalanceListener`. `Dekaf.Serialization` has `Headers`, `Ignore`, and
the serializer interfaces. `Dekaf.Errors` has the exceptions. Each example below includes the
`using` lines it needs.

## API map

Here's where each Confluent type or call ends up. The sections after this walk through the
important ones with code.

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

Here's a typical Confluent producer:

```csharp
using Confluent.Kafka;

using var producer = new Confluent.Kafka.ProducerBuilder<string, string>(new ProducerConfig
{
    BootstrapServers = "localhost:9092",
    Acks = Confluent.Kafka.Acks.All,
    EnableIdempotence = true,
    LingerMs = 5
}).Build();

var result = await producer.ProduceAsync("orders", new Message<string, string>
{
    Key = "order-123",
    Value = orderJson
}, cancellationToken);
```

And the same thing in Dekaf:

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

`ProduceAsync` returns a `ValueTask`, so await it right away. If you want to produce many
messages in parallel, use `ProduceAllAsync`, or call `.AsTask()` on each result before you store
it. Don't collect raw `ValueTask` instances in a list and await them later.

You can also drop the `producer.Flush()` call Confluent needs before `Dispose`. `await using`
flushes pending messages for you. `FlushAsync` is still there if you want an explicit checkpoint,
before acknowledging an upstream message, say.

### Replace callback-based production

Confluent's `Produce` returns as soon as the message is queued locally and reports delivery
through a callback. Dekaf's equivalent is `FireAsync`:

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

When `await producer.FireAsync(...)` returns, the message has been serialized and accepted into
Dekaf's local buffer. That's all. Kafka hasn't acknowledged it yet. If delivery fails, your
callback hears about it; if you didn't pass a callback, the failure is logged and that's the end
of it. So whenever your code needs to know the message actually landed, use `ProduceAsync`
instead. [Fire-and-forget production](./producer/fire-and-forget.md) covers shutdown and error
handling in more depth.

### Preserve record metadata

Use `ProducerMessage<TKey, TValue>` when a record needs headers, a specific partition, or a
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

A Dekaf `Header.Value` is a `ReadOnlyMemory<byte>`, and `Header.IsValueNull` tells a null value
apart from an empty one.

On the consumer side, headers are copied out of Dekaf's fetch buffers the first time you touch
them, not up front. Reading them while you handle the record is fine, and once read they're plain
copies you can keep. What you can't do is hold on to a `ConsumeResult`, let the consumer move on
past the batch it came from, and only then read its headers for the first time. By then the
buffer may have gone back to the pool, and you'll get an `ObjectDisposedException`.

## Migrate a consumer

A typical Confluent poll loop:

```csharp
using Confluent.Kafka;

using var consumer = new Confluent.Kafka.ConsumerBuilder<string, string>(new ConsumerConfig
{
    BootstrapServers = "localhost:9092",
    GroupId = "orders-service",
    AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest
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

Dekaf gives you an async stream instead:

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

When you cancel the token, the `await foreach` ends and control comes back to you. The consumer
itself is still alive and still prefetching; that stops when you dispose it. `await using` does
the graceful close: stop prefetching, commit any offsets that are ready, leave the group. You only
need to call `CloseAsync` yourself if you want to see how long close takes, or catch close errors,
before disposal.

`ConsumeOneAsync(timeout, cancellationToken)` is there for code that genuinely needs one record
at a time, or a timeout. For continuous processing, stick with `ConsumeAsync` or
`ConsumeBatchAsync`.

If your Confluent loop lived inside a `BackgroundService`, look at
`KafkaConsumerService<TKey, TValue>` from `Dekaf.Extensions.Hosting` before porting the loop by
hand. It handles subscription, cancellation on host shutdown, per-message retries, dead-letter
routing, and graceful close. You write one `ProcessAsync` override. See
[hosted consumer services](./hosted-services.md).

## Choose offset semantics explicitly

This is the difference most likely to bite you, because the code looks the same on both sides.

With Confluent's defaults (`EnableAutoOffsetStore = true`, `EnableAutoCommit = true`), a record's
offset is stored the moment `Consume` hands it to you, before your code runs. If your handler
throws, the next auto-commit can commit that offset anyway, and you never see the record again.
In effect, that's at-most-once.

Dekaf defaults to at-least-once. A record isn't staged for commit until your loop asks for the
next one. That means:

- If an exception escapes the loop, the failing record hasn't been committed, so it's redelivered.
- If you catch the exception and carry on, Dekaf treats the record as processed. The next
  iteration stages it, and it gets committed.
- Offsets are positions, not per-record ticks. Committing record 42 also commits record 41,
  whether or not 41 succeeded.

Choose one of these three deliberately.

**Keep the at-least-once default.** Nothing to configure. `WithAtLeastOnceProcessing()` is there
if you'd like the choice to be visible in code:

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders-service")
    .WithAtLeastOnceProcessing()
    .SubscribeTo("orders")
    .BuildAsync(cancellationToken);
```

**Match Confluent's timing** if you want a like-for-like migration first and behavior changes
later:

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders-service")
    .WithAtMostOnceProcessing()
    .SubscribeTo("orders")
    .BuildAsync(cancellationToken);
```

**Acknowledge explicitly** if you catch failures inside the loop and keep going. Turn off
automatic offset storage and store each offset yourself once the record has succeeded.
Auto-commit stays on, so there's no per-message round trip. One rule to remember: because
offsets are positions, a failed record has to go somewhere durable, such as a dead-letter topic,
*before* you store its offset. Otherwise the next successful record on that partition commits
right past it.

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

If you'd rather stop and let the record be redelivered, just don't catch the exception.

[Delivery Semantics](./consumer/delivery-semantics.md) goes deeper on all three. And if you hand
records to parallel tasks, use the [partitioned processing API](./consumer/partitioned-processing-api.md)
rather than letting the consume loop run ahead of work that hasn't finished.

## Migrate serializers

Dekaf picks a built-in serializer automatically for `string`, `byte[]`, `ReadOnlyMemory<byte>`,
`int`, `long`, `Guid`, and `Ignore`. For your own types, set the serializer on the builder:

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

Confluent's synchronous serializers return a `byte[]`. Dekaf's `ISerializer<T>` writes straight
into the `IBufferWriter<byte>` it's given, and `IDeserializer<T>` reads from a
`ReadOnlyMemory<byte>`. When you port a custom serializer, write to that buffer directly rather
than building an array and copying it. See [custom serializers](./serialization/custom.md) and
[JSON serialization](./serialization/json.md).

For Schema Registry, point Dekaf's Avro or Protobuf serializer at the same registry with the same
subject-naming strategy. Check existing payloads and subjects in staging before you switch
writers. See [Schema Registry](./serialization/schema-registry.md), especially the section on
subject naming and the
[identity-framing migration guidance](./serialization/schema-registry.md#schema-identity-framing-and-confluent-interoperability).

## Migrate transactions

In Dekaf, an active transaction is an `ITransaction<TKey, TValue>` object. Initialize the
producer once, open a transaction, and do all the transactional work through it:

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
one of two exceptions. `AbortableTransactionException` means this transaction is broken: abort
it, open a new one, and retry. `FatalTransactionException` means the producer itself is done,
because another instance fenced it, for example. Dispose it and create a new producer.

For consume-transform-produce, call `transaction.SendOffsetsToTransactionAsync` with the
consumer's next offsets and its `ConsumerGroupMetadata`. Give each live producer its own
transactional ID, and test fencing and recovery before you roll out. See
[Transactions](./producer/transactions.md).

## Reuse existing configuration

`Dekaf.Extensions.DependencyInjection` can read Confluent-style configuration sections directly,
with no runtime dependency on `Confluent.Kafka`:

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

Translation is strict on purpose. An unknown property, an unsupported value, or a setting Dekaf
can't represent exactly fails at registration, so you find out at startup rather than in
production. The full list is in the
[compatibility matrix](./configuration/confluent-migration.md#compatibility-matrix). Use the
fluent callback after translation for native Dekaf settings or deliberate overrides. TLS and
SASL settings translate as well; for the native builder methods, see the
[security](./security/tls.md) pages.

## Roll out safely

1. Freeze the record contract: topic, partitioning, key and value bytes, headers, timestamps, and
   Schema Registry subject strategy.
2. Run both clients against a non-production topic and compare the bytes produced and the values
   consumed. Give them different consumer group IDs if both need to see every record.
3. Test cancellation, retry, shutdown, rebalance, and poison messages. Force a processing failure
   and confirm the offset semantics you picked do what you expect.
4. For a rolling consumer-group migration, set the broker's `group.consumer.migration.policy` to
   allow the direction you need, and check the assignor before both client types share a group.
5. Measure throughput, p50/p99/max latency, CPU, and allocations with your real message sizes and
   concurrency, then retune batching, fetch, compression, and connection settings. A setting with
   the same name doesn't mean the same performance.
6. Remove the Confluent packages and native deployment assets only once nothing constructs a
   Confluent client or serializer.

From here, [Producer Basics](./producer/basics.md), [Consumer Basics](./consumer/basics.md), and
[Dependency Injection](./dependency-injection.md) cover the Dekaf-native patterns.
