# Dekaf.Streams — Design

**Issue:** #3475
**Date:** 2026-11-14
**Supersedes:** the deferred RFC from #2562 and its scoping companion (#2768), both removed from the published docs in 63b2ae7ea.
**Client prerequisites:** #4142, #4143, #4144

## Overview

`Dekaf.Streams` is a stream-processing library for .NET on top of the Dekaf client: a typed topology DSL, a processor API, changelog-backed state, windows and joins, and a deterministic test driver. It is a set of separate packages. Core `Dekaf` never depends on them, and they use only public Dekaf API.

The August RFC deferred this work behind two blocking gaps. Both are gone: Dekaf ships streams-group membership (`IStreamsGroupMember`, KIP-1071), and Kafka 4.4 is released. The design therefore targets the streams group protocol only. The broker assigns tasks, validates co-partitioning and creates internal topics, which removes most of the client-side machinery that makes Java Kafka Streams hard to operate.

## Decisions

| # | Decision | Reason |
| --- | --- | --- |
| 1 | Separate packages in this repository (`src/Dekaf.Streams*`), public Dekaf API only | Shares CI, the performance gate and `Dekaf.Testing`; the boundary is kept by not granting `InternalsVisibleTo` from `Dekaf` |
| 2 | LINQ names for stateless operators (`Where`, `Select`, `SelectMany`, `GroupBy`, `Join`), Kafka names for Kafka concepts (`Table`, `WindowedBy`, `ToTopic`, `Repartition`) | The audience is .NET developers |
| 3 | Byte-oriented store contract with a pluggable engine. In-memory engine built in; ZoneTree prototyped as the first persistent engine, RocksDB as an optional alternative | A fully managed default fits the client. ZoneTree has ordered range scans; its Native AOT behavior must be verified in the prototype. FASTER is unmaintained and Tsavorite has no standalone package |
| 4 | Kafka 4.4+ and the streams group protocol only | No consumer-group fallback, no client-side assignor |
| 5 | Exactly-once by default, at-least-once as an opt-out | Correct by default; Transactions V2 is already complete in the client |

## What is taken from other libraries

| Source | Taken | Avoided |
| --- | --- | --- |
| Kafka Streams (Java) | Stream/table duality, a task per (subtopology, partition), changelog-backed stores, a processor API under the DSL, Java-compatible windowed key format | Positional auto-naming of stores and internal topics, default serdes that fail at runtime, the `Materialized`/`Grouped`/`Named` overload families, `suppress`, implicit repartitioning |
| Recent Kafka KIPs | Changelog offsets kept inside the store (KIP-1035), transactional stores (KIP-892), one error handler with a dead-letter outcome (KIP-1033/1034), emit on window close (KIP-825) | Adding these after the fact |
| Flink | Typed keyed state handed to a processor, explicit event time and grace | A separate cluster and checkpoint coordinator |
| Quix Streams, Goka | State as a handler parameter; one small builder object | Dynamic typing |
| Responsive | Async processing with per-key ordering, as a later phase | Remote state by default |
| .NET | Fluent builders, `IHostedService`, static lambdas, Native AOT without reflection | Reusing `IAsyncEnumerable` LINQ as the DSL: it cannot express state, repartitioning or commits |

## API

```csharp
var builder = new StreamsBuilder("order-totals");

var orders    = builder.Stream("orders", Serializers.String, orderSerde);
var customers = builder.Table("customers", "customers-store", Serializers.String, customerSerde);

orders
    .Where(static (key, order) => order.Total > 0)
    .Join(customers, static (order, customer) => new Enriched(order, customer))
    .GroupBy(static (key, e) => e.Customer.Region, Serializers.String, enrichedSerde)
    .WindowedBy(TimeWindows.Tumbling(TimeSpan.FromMinutes(5)).WithGrace(TimeSpan.FromSeconds(30)).EmitOnClose())
    .Aggregate("region-totals", static () => 0L, static (region, e, total) => total + e.Order.Total, Serializers.Int64)
    .ToStream()
    .ToTopic("region-totals", WindowedSerde.Create(Serializers.String, TimeSpan.FromMinutes(5)), Serializers.Int64);

Topology topology = builder.Build();
```

Rules:

- **Names.** Every stateful operator takes a required name. Store, changelog and repartition topic names derive from it (`<application>-<name>-changelog`, `<application>-<name>-repartition`), never from operator position.
- **Serdes.** Passed wherever a type first appears or crosses a topic or store. There is no default-serde configuration. A stream remembers the serdes it still knows, so `GroupByKey()` needs none.
- **Repartitioning is visible.** `GroupBy` repartitions and takes the serdes for it. After `SelectKey` or `Process`, a keyed operator is a build-time error until `Repartition(name, keySerde, valueSerde)` is called.
- **One signature per operator.** No overload families.
- **Records.** `Record<TKey, TValue>` carries key, value, timestamp and headers and is passed by `in`.
- **Tombstones.** A null value on a table topic deletes the key.

Processor API, with state as a parameter:

```csharp
orders.Process<long, string, Order>("dedupe", Serializers.Int64,
    static (in Record<string, Order> record, IValueState<long> lastSeen, IForwarder<string, Order> output) =>
    {
        if (lastSeen.TryGet(out var ts) && ts >= record.Timestamp) return;
        lastSeen.Set(record.Timestamp);
        output.Forward(in record);
    });
```

`IProcessor<TKeyIn, TValueIn, TKeyOut, TValueOut>` is the general form: it declares its stores and gets them from `IProcessorContext`.

Errors go to one handler, `StreamsBuilder.OnError`, which returns `Continue`, `Fail` or `DeadLetter(topic)` for deserialization and processing failures.

## Topology compilation

`StreamsBuilder.Build()` validates the graph and produces an immutable `Topology`:

1. Nodes are split into subtopologies at repartition topics; a join keeps the stream and its table in one subtopology. Subtopology ids are assigned in declaration order.
2. Each subtopology lists its source topics, repartition source and sink topics, changelog topics and co-partition groups.
3. `Topology.ToStreamsGroupTopology()` returns the `StreamsGroupTopology` that `IStreamsGroupMember.JoinAsync` sends to the coordinator, with a KIP-1331 description.
4. `Topology.Describe()` prints the same information for operators.

A table read directly from a topic uses that topic as its changelog.

## Runtime

A task is one (subtopology, partition) pair and is the unit of ownership.

- **Single writer.** One lane executes a task. Parallelism comes from tasks, never from concurrent access to a store.
- **Hot path.** A record is deserialized once at the source, flows through sealed nodes as a typed value, and is serialized once at a sink or store. Operators call the next node directly; there is no per-record allocation in the library's own code. Deserializing a reference type allocates whatever the user's serde allocates.
- **Stores.** `IKeyValueBytesStore` and `IWindowBytesStore` are span-based. Typed wrappers serialize into a reused buffer and report each write to the task's changelog writer. `IStateStoreEngine` creates them, so an engine only implements bytes in, bytes out. These contracts stay internal until the host lets an application choose an engine.
- **Time.** Event time is the record timestamp. A windowed operator tracks the highest timestamp it has seen; a record whose window closed before that (window end plus grace) is dropped and counted.
- **Windows.** Tumbling and hopping. Without `EmitOnClose` every update is forwarded; with it, a window is forwarded once when it closes. Closed windows are removed from the store.
- **Window key format.** Serialized key followed by the window start as a big-endian 64-bit integer, the same as Java's time-windowed serde.

The broker-facing host builds on that engine:

1. Join with `IStreamsGroupMember`, sending the compiled topology.
2. On assignment (#4142), create the task, restore each store from its changelog with a bounded read (`ConsumeSnapshotAsync`, read-committed), then start the lane.
3. Feed the lane with `ConsumeBatchAsync` from a manually assigned consumer.
4. Commit on an interval. Under exactly-once, one transactional producer per worker carries outputs, changelog records and input offsets (#4143) in one transaction. Store writes are buffered during the transaction and published with the changelog offset after it commits; an abort discards them. A crash between the two is repaired from the changelog.
5. On revocation, stop input, commit, close stores, then acknowledge.

Later additions on the same model: stream-stream and foreign-key joins, session and sliding windows, standby tasks, versioned stores, punctuation, async per-key processing, read-only store queries with the endpoint map the coordinator already returns, and a `Dekaf.Streams` meter and activity source.

## Packages

```text
Dekaf.Streams          DSL, topology compiler, task engine, in-memory stores, host
Dekaf.Streams.Testing  TopologyTestDriver
Dekaf.Streams.Storage.*  persistent engines (later)
        |
        v
      Dekaf            client, protocol, transactions (public API only)
```

## Testing

- `TopologyTestDriver` runs a topology in memory with one partition per topic: pipe typed records in with explicit timestamps, read typed output, inspect stores. Records sent to a repartition topic are fed to the downstream subtopology at once. It runs the same task engine as the host.
- The host is tested with `Dekaf.Testing` once #4144 lands, and against Kafka 4.4 in the integration suite, with crash injection at each step of the commit sequence.

## Performance

Per record, on the library's own path: one deserialize at the source, one virtual call per operator, one serialize per sink or store write, and one dictionary operation per store access. Nothing is allocated per record by the library; the in-memory store allocates when a key is first written or a value changes length. `StreamsTopologyBenchmarks` measures a stateless pipeline and a keyed count.

## Phases

1. **Foundation:** DSL, topology compiler, task engine, in-memory stores, tumbling and hopping windows, stream-table join, processor API, error handler, test driver, benchmark.
2. **Client prerequisites:** #4142 (assignment notification), #4143 (offset commit as a streams member), #4144 (`Dekaf.Testing` batch consume, rebalance callbacks, streams assignor).
3. **Host:** group membership, restore, exactly-once commit loop, dependency injection and hosting, crash and rebalance tests on Kafka 4.4, Native AOT smoke test.
4. **Persistent store engine:** ZoneTree prototype measured against RocksDB.
5. **Later operators and features** as listed under Runtime.
