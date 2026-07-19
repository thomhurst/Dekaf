---
sidebar_position: 12
---

# Testing

`Dekaf.Testing` provides in-memory implementations of the main Dekaf client interfaces for fast application unit tests:

- `InMemoryKafkaCluster`
- `InMemoryProducer<TKey, TValue>`
- `InMemoryConsumer<TKey, TValue>`
- `InMemoryShareConsumer<TKey, TValue>`
- `InMemoryAdminClient`

The cluster stores serialized bytes, not typed values, so custom serializers and deserializers still run during tests.

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

The release integration matrix retains Kafka 4.0.2 as the compatibility floor and Kafka 4.3.1 as
the current release, with intermediate 4.1.2 and 4.2.1 coverage. NativeAOT release gates cover the
4.0.2 floor and 4.3.1 current release.

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
