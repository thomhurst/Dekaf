---
sidebar_position: 2
description: "Reference for the Dekaf.Streams topology DSL: stateless operators, grouping and aggregation, windows, stream-table joins and the processor API."
---

# Topology Operators

A topology starts from `StreamsBuilder.Stream` or `StreamsBuilder.Table` and ends in `ToTopic`. Three rules apply throughout:

- **Stateful operators are named.** The name becomes the store name and part of the internal topic names, so it must be unique in the topology and stable across deployments.
- **Serdes are explicit.** You pass a serde wherever a type first appears or crosses a topic or a store. There is no default serde.
- **Repartitioning is visible.** After the key changes, a keyed operator is a build-time error until the stream is repartitioned.

Pass `static` lambdas where you can: they cannot capture state by accident, and they allocate nothing.

## Stateless operators

| Operator | Effect |
| --- | --- |
| `Where(predicate)` | Keeps matching records |
| `Select(selector)` | Replaces the value; the key is unchanged |
| `SelectKey(selector)` | Replaces the key; the stream is no longer partitioned by key |
| `SelectMany(selector)` | Emits zero or more values per record through a sink |
| `Peek(action)` | Runs an action and forwards the record |
| `Merge(other)` | Interleaves two streams |
| `Repartition(name, keySerde, valueSerde)` | Writes to an internal topic and reads back, partitioned by the current key |
| `ToTopic(topic, keySerializer, valueSerializer)` | Writes the stream to a topic |

```csharp
using Dekaf.Serialization;
using Dekaf.Streams;

var topology = new StreamsBuilder("payments");
var payments = topology.Stream("payments", Serializers.String, Serializers.Int64);

payments
    .Where(static (account, cents) => cents > 0)
    .Select(static (account, cents) => cents / 100.0)
    .ToTopic("payments-in-units", Serializers.String, Serializers.Double);
```

`SelectMany` takes a sink instead of returning a sequence, so emitting several values allocates no enumerator.

## Grouping and aggregation

`GroupByKey` groups by the existing key. `GroupBy` groups by a new key and repartitions through an internal topic named after the aggregation that follows.

```csharp
using Dekaf.Serialization;
using Dekaf.Streams;

var topology = new StreamsBuilder("payments");
var payments = topology.Stream("payments", Serializers.String, Serializers.Int64);

ITable<string, long> totals = payments
    .GroupByKey()
    .Aggregate("totals", static () => 0L, static (account, cents, total) => total + cents, Serializers.Int64);

ITable<string, long> largest = payments
    .GroupByKey()
    .Reduce("largest", static (current, next) => Math.Max(current, next), Serializers.Int64);

totals.ToStream().ToTopic("account-totals", Serializers.String, Serializers.Int64);
```

`Count`, `Aggregate` and `Reduce` return a table. `ToStream` turns its updates back into a stream; every update is forwarded. Records with a null key are skipped.

## Windows

`WindowedBy` aggregates per key and time window. Windows are aligned to the Unix epoch and use the record timestamp.

```csharp
using Dekaf.Serialization;
using Dekaf.Streams;

var topology = new StreamsBuilder("clicks");
var windows = TimeWindows.Tumbling(TimeSpan.FromMinutes(5))
    .WithGrace(TimeSpan.FromSeconds(30))
    .EmitOnClose();

topology.Stream("clicks", Serializers.String, Serializers.String)
    .GroupByKey()
    .WindowedBy(windows)
    .Count("clicks-per-user")
    .ToStream()
    .ToTopic("clicks-per-user", WindowedSerde.Create(Serializers.String, windows.Size), Serializers.Int64);
```

- `TimeWindows.Tumbling(size)` creates windows that do not overlap; `TimeWindows.Hopping(size, advance)` creates overlapping ones.
- A window closes when the operator has seen a record whose timestamp is at least the window end plus the grace period. Records for a closed window are dropped.
- Without `EmitOnClose`, every update is forwarded. With it, each window is forwarded once, when it closes.
- The result key is `Windowed<TKey>`. `WindowedSerde` writes the key followed by the window start as a big-endian 64-bit integer, the format of the Java time-windowed serde.

A window that has closed is removed from its store.

## Stream-table joins

```csharp
using Dekaf.Serialization;
using Dekaf.Streams;

var topology = new StreamsBuilder("orders");
var customers = topology.Table("customers", "customers-store", Serializers.String, Serializers.String);
var orders = topology.Stream("orders", Serializers.String, Serializers.String);

orders
    .Join(customers, static (order, customer) => $"{customer}: {order}")
    .ToTopic("orders-with-customer", Serializers.String, Serializers.String);

orders
    .LeftJoin(customers, static (order, customer, found) => found ? $"{customer}: {order}" : $"unknown: {order}")
    .ToTopic("all-orders", Serializers.String, Serializers.String);
```

Each stream record is joined with the table value for its key at the time it is processed. A table update does not re-emit earlier results. The stream and the table topic must have the same number of partitions and be partitioned the same way; the coordinator checks the partition counts.

A record with a null value on the table topic deletes its key.

## Processor API

When the DSL is not enough, `Process` runs your own code. The handler form keeps one state value per record key in a changelog-backed store:

```csharp
using Dekaf.Serialization;
using Dekaf.Streams;

var topology = new StreamsBuilder("events");
topology.Stream("events", Serializers.String, Serializers.String)
    .Process<long, string, string>(
        "latest-only",
        Serializers.Int64,
        static (in Record<string, string> record, IValueState<long> lastSeen, IForwarder<string, string> output) =>
        {
            if (lastSeen.TryGet(out var timestamp) && timestamp >= record.Timestamp)
            {
                return;
            }

            lastSeen.Set(record.Timestamp);
            output.Forward(in record);
        })
    .ToTopic("latest-events", Serializers.String, Serializers.String);
```

The class form declares any number of stores and resolves them in `Init`:

```csharp
using Dekaf.Serialization;
using Dekaf.Streams;

var topology = new StreamsBuilder("events");
topology.Stream("amounts", Serializers.String, Serializers.Int64)
    .Process("running-total", static () => new RunningTotal(), Stores.KeyValue("totals", Serializers.String, Serializers.Int64))
    .ToTopic("running-totals", Serializers.String, Serializers.Int64);

sealed class RunningTotal : IProcessor<string, long, string, long>
{
    private IProcessorContext<string, long> _context = null!;
    private IKeyValueStore<string, long> _totals = null!;

    public void Init(IProcessorContext<string, long> context)
    {
        _context = context;
        _totals = context.GetKeyValueStore<string, long>("totals");
    }

    public void Process(in Record<string, long> record)
    {
        _totals.TryGet(record.Key, out var total);
        total += record.Value;
        _totals.Put(record.Key, total);
        _context.Forward(record.WithValue(total));
    }
}
```

One processor instance is created per task. A processor may emit any key, so its output counts as not partitioned by key: call `Repartition` before a keyed operator.
