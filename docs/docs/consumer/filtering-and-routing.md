---
sidebar_position: 3
description: "Reject records or choose a deserializer from raw keys, values, and headers before typed deserialization."
---

# Pre-deserialization filtering and routing

Dekaf can inspect raw record metadata and bytes before invoking key or value deserializers. Use a
record filter to reject records cheaply, or `HeaderRoutingDeserializer<T>` to select a cached child
deserializer from a header value. Both run client-side: Kafka still sends every fetched record.

## Filter before deserialization

Implement `IConsumerRecordFilter` and register the instance on the consumer builder:

```csharp
using Dekaf.Consumer;
using Dekaf.Serialization;

sealed class EventFilter : IConsumerRecordFilter
{
    public bool ShouldDeserialize(scoped in ConsumerRecordFilterContext context)
    {
        // Header-only routing. Scan backward when the last duplicate header should win.
        var headers = context.Headers;
        for (var i = headers.Length - 1; i >= 0; i--)
        {
            ref readonly var header = ref headers[i];
            if (header.Key == "event-type")
                return !header.IsValueNull && header.Value.Span.SequenceEqual("order"u8);
        }

        return false;
    }
}

await using var consumer = await Kafka.CreateConsumer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders")
    .WithRecordFilter(new EventFilter())
    .WithValueDeserializer(orderDeserializer)
    .SubscribeTo("events")
    .BuildAsync();
```

The context exposes `Topic`, `Partition`, `Offset`, timestamp information, leader epoch, raw
`Key` and `Value` bytes with null flags, and parsed `Headers`. A predicate can therefore be
key-only, value-only, header-only, or combine those fields without creating strings or arrays.
The bytes and headers may reference pooled receive memory; do not retain the context or any of
its memory after `ShouldDeserialize` returns.

The same filter applies to `ConsumeAsync`, `ConsumeOneAsync`, `ConsumeBatchAsync`, and the
partitioned processing APIs. It runs synchronously before tracing, deserialization,
interceptors, and application delivery, and never runs while a coordinator or hot-path lock is
held.

## Route deserialization by header

`HeaderRoutingDeserializer<T>` chooses a child deserializer from the last header with the
configured name. Missing, null, unknown values use the fallback. Route values and child
deserializers are copied and indexed at construction; warmed dispatch allocates `0 B`.

```csharp
var eventDeserializer = new HeaderRoutingDeserializer<IEvent>(
    "event-type",
    fallbackDeserializer,
    new HeaderDeserializerRoute<IEvent>("order"u8.ToArray(), orderDeserializer),
    new HeaderDeserializerRoute<IEvent>("payment"u8.ToArray(), paymentDeserializer));

await using var consumer = await Kafka.CreateConsumer<string, IEvent>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("events")
    .WithValueDeserializer(eventDeserializer)
    .SubscribeTo("events")
    .BuildAsync();
```

The `ToArray()` calls happen once during configuration. Duplicate route values are rejected. The
consumer passes its parsed header span directly to the router without creating a `Headers`
collection or copying record data.

Exceptions from the selected child or fallback propagate as ordinary deserialization failures;
the record position is rewound for retry. Invalid router configuration, including duplicate route
values or a missing child deserializer, throws from the constructor before consumption starts.

## Delivery and offset semantics

| Event | Behavior |
|---|---|
| Predicate returns `true` | Normal deserialization and delivery continue. |
| Predicate returns `false` | No deserializer, interceptor, or application callback runs; the consumer position advances past the record. |
| Automatic offset storage enabled | A rejected record is immediately proven processed and becomes eligible for auto-commit. |
| Manual commit | Committing a later position also commits earlier rejected records, because Kafka commits positions rather than individual records. |
| Predicate throws | The same exception propagates; the failed record is not advanced and can be retried. |
| Partition EOF enabled | Filtering does not suppress the partition EOF event emitted after the fetched records. |
| Cancellation | The synchronous predicate has no cancellation token. The surrounding consume operation observes cancellation before or after the call. |
| Batch APIs | `MaxPollRecords` counts inspected records; `ConsumeBatch.Count` counts delivered records. |

With manual commits, process the delivered record successfully before committing. The committed
position safely includes any earlier records rejected by the filter:

```csharp
await foreach (var record in consumer.ConsumeAsync(cancellationToken))
{
    await ProcessAsync(record, cancellationToken);

    // Vouches for this delivered record and every earlier filtered record.
    await consumer.CommitAsync(
        [new TopicPartitionOffset(record.Topic, record.Partition, record.Offset + 1)],
        cancellationToken);
}
```

Do not commit past a delivered record whose processing failed. As with all Kafka commits, a later
position acknowledges every lower offset in that partition.
