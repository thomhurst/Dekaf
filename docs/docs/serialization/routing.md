---
sidebar_position: 4
---

# Routing Serdes

`Dekaf.Serialization.Routing` routes heterogeneous records without allocating during steady-state
dispatch. Registration is a cold-start operation; call `Freeze()` before passing a router to a
producer or consumer. Frozen routers are safe for concurrent use.

```bash
dotnet add package Dekaf.Serialization.Routing
```

## Route consumed values by topic

```csharp
using Dekaf.Serialization.Routing;

var deserializer = new TopicRoutingDeserializer<IEvent>()
    .Register("orders", orderDeserializer)
    .Register("payments", paymentDeserializer)
    .Freeze();

await using var consumer = await Kafka.CreateConsumer<string, IEvent>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("event-router")
    .WithValueDeserializer(deserializer)
    .BuildAsync();
```

Each registered `IDeserializer<TDerived>` remains strongly typed. No cast delegate or closure is
created while consuming.

## Route Schema Registry payloads by schema ID

`SchemaIdRoutingDeserializer<TBase>` reads the schema ID from the standard Confluent framing:
magic byte `0`, followed by a four-byte big-endian schema ID. The selected deserializer receives
the complete framed payload, so existing Schema Registry deserializers continue to work.

```csharp
using Dekaf.Serialization.Routing;

var deserializer = new SchemaIdRoutingDeserializer<IEvent>()
    .Register(17, customerV1Deserializer)
    .Register(23, customerV2Deserializer)
    .Freeze();
```

Malformed framing throws `SerializationException`. An unknown topic or schema ID also throws
unless a fallback was registered with `SetFallback(...)` before `Freeze()`.

## Route produced values

Use `TopicRoutingSerializer<TBase>` when the destination topic determines the format. Use
`TypeRoutingSerializer<TBase>` when the value's exact runtime type determines it.

```csharp
using Dekaf.Serialization.Routing;

var serializer = new TypeRoutingSerializer<IEvent>()
    .Register(orderSerializer)
    .Register(paymentSerializer)
    .Freeze();
```

Topic routes validate that the value matches the registered derived serializer type. Runtime-type
routes use exact type matching; derived subclasses need their own registration or a fallback.

## Inspect the raw record key

During value deserialization, `SerializationContext.KeyData` exposes the raw key bytes without a
copy. `SerializationContext.IsKeyNull` distinguishes a Kafka null key from a non-null empty key.

The memory can reference a pooled receive buffer. Read it only during the current deserializer
call; never store it, return it, or use it after deserialization completes. `KeyData` is not
meaningful during serialization or key deserialization.
