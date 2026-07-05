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
