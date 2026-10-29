---
sidebar_position: 12
description: "Dekaf.Testing provides in-memory producers and consumers for fast unit tests, plus DI wiring, admin and offset fakes, and NativeAOT smoke validation."
---

# Testing

`Dekaf.Testing` provides in-memory implementations of the main Dekaf client interfaces for fast application unit tests:

- `InMemoryKafkaCluster`
- `InMemoryProducer<TKey, TValue>`
- `InMemoryConsumer<TKey, TValue>`
- `InMemoryShareConsumer<TKey, TValue>`
- `InMemoryAdminClient`

The cluster stores serialized bytes, not typed values, so custom serializers and deserializers still run during tests.

## Documentation snippet gate

CI extracts every C# fence from this documentation and the README, then compiles it against the current source projects. Shared imports and application context let fragments validate public API shape; this gate does not prove every fence is a standalone program. Shell examples are checked against packable package IDs and existing project paths. New C# fences compile by default.

Run the gate locally with:

```bash
dotnet run --project tests/Dekaf.DocTests --configuration Release --framework net10.0 -- --repository-root .
```

Every extracted C# fence must compile. The gate has no failure baseline, exclusions, or suppression directives: one compiler or source-generator error fails the run. Keep fragments focused by using the shared application context only for prerequisites that the surrounding documentation intentionally omits.

## Direct use

```csharp
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Testing;

var cluster = new InMemoryKafkaCluster();
cluster.CreateTopic("orders", partitionCount: 3);

var producer = new InMemoryProducer<string, string>(cluster);
var consumer = new InMemoryConsumer<string, string>(
    cluster,
    new InMemoryConsumerOptions
    {
        GroupId = "orders-service",
        AutoOffsetReset = AutoOffsetReset.Earliest
    });

await producer.ProduceAsync(new ProducerMessage<string, string>
{
    Topic = "orders",
    Key = "order-1",
    Value = "created"
});

consumer.Subscribe("orders");
var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(1));
```

## Prepared transaction recovery

Set `InMemoryProducerOptions.TransactionalId` when testing prepared transaction recovery (KIP-939).
Pass the same identity to the original producer and each replacement producer, using the
`InMemoryProducer(cluster, options)` overload or the serializer overloads with options last.
Without an explicit identity, each producer has a private identity and cannot recover another
producer's prepared transaction.

After `InitTransactionsAsync(keepPreparedTransaction: true)` adopts a prepared transaction,
`BeginTransaction()` rejects new work until `CompletePreparedTransactionAsync` completes it.
The default `InitTransactionsAsync()` and the explicit `false` overload abort a prepared transaction
for that identity, discard its pending consumer offsets, and unblock read-committed consumers.
Completion with a missing or mismatched pending state throws `TransactionException` with
`ErrorCode.InvalidTxnState`.

For dependency injection, register `InMemoryProducerOptions` with the desired `TransactionalId`
before or after `AddDekafInMemory()`. All producers resolved with those options share that identity;
use separate registrations or construct producers directly when testing multiple transactional identities.

## Asynchronous serializers

Serdes that perform per-message I/O (`IAsyncSerializer<T>`, `IAsyncDeserializer<T>`, `IAsyncSerde<T>`)
can be passed to the in-memory clients, so code built around them is unit-testable without a broker.
Each component is independent — mix a synchronous serializer for the key with an asynchronous one for
the value if that is how the real client is configured:

```csharp
using Dekaf.Serialization;
using Dekaf.Testing;

var cluster = new InMemoryKafkaCluster();
IAsyncSerde<Order> orderSerde = new EncryptingOrderSerde(keyVault);

var producer = new InMemoryProducer<string, Order>(cluster, Serializers.String, orderSerde);
var consumer = new InMemoryConsumer<string, Order>(
    cluster,
    Serializers.String,
    orderSerde,
    new InMemoryConsumerOptions
    {
        GroupId = "orders-service",
        AutoOffsetReset = AutoOffsetReset.Earliest
    });
```

The in-memory clients await asynchronous serdes exactly where the real clients do: on
`ProduceAsync`/`FireAsync` and on `ConsumeAsync`/`ConsumeOneAsync` (and `PollAsync` for the share
consumer). `InMemoryShareConsumer<TKey, TValue>` accepts the same combinations.

## Share consumer offset reset

Share groups start at `Latest` by default, matching Kafka. The first poll of each partition
persists the group's start offset, even when the poll returns no records. Later members and
polls reuse that offset. To replay records produced before the first poll, set
`InMemoryShareConsumerOptions.AutoOffsetReset` to `AutoOffsetReset.Earliest`.
For a timestamp lookback, use `AutoOffsetReset.ByDuration` and set `AutoOffsetResetDuration`.
An explicit policy applies to every member of the group and does not rewind initialized partitions.
The most recently configured explicit policy applies to partitions that have not yet been initialized.
Leaving the policy null preserves the group's existing policy.

`AddDekafInMemory` preserves the group ID and reset policy configured through
`ShareConsumerBuilder`, including keyed and hosted registrations. Builder registrations still require
bootstrap servers and a group ID, even though the in-memory client does not connect to a broker.
A registered `InMemoryShareConsumerOptions.MemberId` is preserved alongside these builder settings.

## Dependency Injection

Use `AddDekafInMemory()` to replace producer, consumer, share consumer, and admin client registrations with in-memory doubles backed by one shared cluster:

```csharp
using Dekaf.Testing;

services.AddDekafInMemory(options =>
{
    options.DefaultPartitionCount = 3;
});
```

Register `ISerializer<T>` and `IDeserializer<T>` in DI when your application uses custom payload types. Built-in Dekaf serializers are used automatically for common primitive types.

## Partitioning

Records produced without an explicit partition go where the real producer's default partitioner puts them: keyed records use Kafka-compatible Murmur2, so a key lands on the same partition as with `KafkaProducer` or the Java client. Records with a null or empty key rotate across partitions; the real producer uses sticky partitioning for them, so tests should not depend on their placement.

When your application configures a different partitioner, set the same one on the cluster options so tests see the same placement:

```csharp
var cluster = new InMemoryKafkaCluster(new InMemoryKafkaClusterOptions
{
    Partitioner = new Murmur2RandomPartitioner()
});
```

The cluster calls `IPartitioner.Partition` only. It does not simulate producer batches or call batch callbacks. Configured `DefaultPartitioner` and `StickyPartitioner` instances can hash nonempty keys, but a record that needs their sticky selection throws `NotSupportedException`: null or empty keys, or any key when `ignoreKeys: true`. An explicit record partition bypasses the partitioner. Use a real broker to test sticky batch placement.

## Admin and offsets

`InMemoryAdminClient` supports common unit-test operations such as creating, deleting, listing, and describing topics, altering/listing group offsets, deleting records, creating partitions, and listing offsets. Broker-only APIs such as ACL, SCRAM, and config mutation are accepted as no-ops or return empty results.

Use Testcontainers or another real broker for protocol compatibility and integration coverage.

## Protocol capability regression tests

Physical-connection API negotiation has deterministic coverage for heterogeneous brokers,
reconnect generations, `ApiVersions` fallback, finalized-feature epochs, concurrent replacement,
and handshake disposal. Run the focused cross-platform suite with:

```bash
dotnet run --project tests/Dekaf.Tests.Unit/Dekaf.Tests.Unit.csproj \
  --framework net10.0 -- \
  --treenode-filter "/*/*/(KafkaConnectionCapabilitiesTests|KafkaConnectionCapabilityHandshakeTests)/*"
```

Run the capability creation and lookup benchmarks with:

```bash
dotnet run --configuration Release \
  --project tools/Dekaf.Benchmarks/Dekaf.Benchmarks.csproj -- \
  --filter "*ApiVersionNegotiationBenchmarks*"
```

The ready-connection send path performs an O(1), allocation-free packed-array lookup and no
network call. Mandatory negotiation adds one `ApiVersions` request/response round trip when a
physical connection is created. A broker that rejects v4 adds one v3 fallback round trip. Snapshot
creation happens once per physical connection generation and is measured separately from lookup.

The release integration matrix retains Kafka 4.0.2 as the compatibility floor and Kafka 4.4.0 as
the current release, with intermediate 4.1.2, 4.2.1 and 4.3.1 coverage. NativeAOT release gates cover the
4.0.2 floor and 4.4.0 current release.

## NativeAOT smoke validation

CI runs a `NativeAOT Smoke` pull-request job for the supported `linux-x64`
runtime. It publishes each smoke executable with `PublishAot=true` and
`TreatWarningsAsErrors=true`, then runs the published binary.

Run the same validation locally on Linux after installing the NativeAOT
toolchain:

```bash
sudo apt-get update && sudo apt-get install -y clang zlib1g-dev

dotnet publish tests/Dekaf.Tests.Aot/Dekaf.Tests.Aot.csproj \
  --configuration Release \
  --framework net10.0 \
  --runtime linux-x64 \
  --self-contained true \
  --output artifacts/aot/core \
  -p:PublishAot=true \
  -p:ContinuousIntegrationBuild=true \
  -p:TreatWarningsAsErrors=true
./artifacts/aot/core/Dekaf.Tests.Aot

dotnet publish tests/Dekaf.Tests.Aot.DependencyInjection/Dekaf.Tests.Aot.DependencyInjection.csproj \
  --configuration Release \
  --framework net10.0 \
  --runtime linux-x64 \
  --self-contained true \
  --output artifacts/aot/dependency-injection \
  -p:PublishAot=true \
  -p:ContinuousIntegrationBuild=true \
  -p:TreatWarningsAsErrors=true
./artifacts/aot/dependency-injection/Dekaf.Tests.Aot.DependencyInjection
```
