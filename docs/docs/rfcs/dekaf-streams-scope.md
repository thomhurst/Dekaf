---
sidebar_position: 2
description: "Scoping companion to the Dekaf.Streams RFC: verified inventory of existing Dekaf primitives, gap analysis, design decisions, and a phased plan mapped to issue #2748's reopen gates."
---

# Dekaf.Streams Scope: Primitives, Gaps, and Phasing

**Status:** Scoping only — not implementation authorization. All reopen gates in
[#2748](https://github.com/thomhurst/Dekaf/issues/2748) remain intact.

**Date:** 2026-08-19

**Parent RFC:** [Evaluate a Separate Dekaf.Streams Runtime](./dekaf-streams.md) (Deferred)

## Purpose

The parent RFC decided *whether* (deferred) and defined *what a prototype must
prove*. This document scopes *how* a runtime would map onto today's shipped
Dekaf APIs: which primitives already exist and were verified in source, which
gaps block or complicate the design, which decisions must be made before a
prototype, and a phase plan. It exists so that if #2748's gates are ever
satisfied, the prototype starts from a verified inventory instead of
rediscovery.

## Verified Primitive Inventory

Every row below was confirmed against current source, not assumed.

### Requirements fully met today

| Streams requirement | Dekaf API (verified) | Notes |
| --- | --- | --- |
| Transactional produce with fencing | `IKafkaProducer.InitTransactionsAsync` / `BeginTransaction()` → `ITransaction` (`Producer/IKafkaProducer.cs`) | TV2/KIP-890 aware; KIP-1050 error classification in `TransactionErrorClassifier` (Fatal/Abortable/Retriable maps directly onto task lifecycle) |
| Offsets-in-transaction with zombie fencing | `ITransaction.SendOffsetsToTransactionAsync(offsets, ConsumerGroupMetadata)` | Full quartet (group, member, epoch, instance) flows to `TxnOffsetCommitRequest`; leader epoch included via `TopicPartitionOffset.LeaderEpoch` |
| Live consumer group metadata | `IKafkaConsumer.ConsumerGroupMetadata` (null until group is stable) | `GenerationId` carries the KIP-848 member epoch |
| Cooperative rebalance | KIP-848 only (`ConsumerCoordinator`, `ConsumerGroupHeartbeat` v0–v2) | Incremental by construction; classic protocol intentionally absent |
| Async drain/commit window on revoke | Builder-registered `IRebalanceListener.OnPartitionsRevokedAsync` | Heartbeat loop awaits the callback before advertising the reduced owned set — a genuine pre-release window |
| Manual assignment for restore/repartition consumers | `IConsumerPartitions.Assign` / `IncrementalAssign` / `Seek` / `SeekToBeginning` | Separate consumer instances for subscribed sources vs assigned restore consumers (normal Streams shape) |
| Bounded changelog restoration | `ConsumeSnapshotAsync()` (`BoundedConsumerExtensions`) | Captures isolation-aware end offsets per partition and terminates there; read-committed aware; effectively a ready-made restore primitive |
| Read-committed fetches | `WithIsolationLevel(IsolationLevel.ReadCommitted)` | Mandatory for source/repartition/changelog consumers |
| Internal topic provisioning with configs | `IAdminClient.CreateTopicsAsync` with `NewTopic.Configs` | `cleanup.policy=compact`, retention, segments all settable at create time; `IncrementalAlterConfigsAsync` for later tuning |
| Changelog end offsets / reset tooling | `ListOffsetsAsync`, `AlterConsumerGroupOffsetsAsync`, `DeleteRecordsAsync`, `DeleteTopicsAsync` | A full `streams-application-reset` equivalent is composable today |
| Transactional-ID remediation | `FenceProducersAsync`, `ListTransactionsAsync` (with `TransactionalIdPattern`) | Directly usable for scale-in fencing of stale task owners |
| Java-compatible repartitioning | `Murmur2Partitioner` / default partitioner (`Producer/Partitioner.cs`) | Byte-for-byte the Java `DefaultPartitioner` contract — co-partitioning with Java Streams holds |
| Zero-allocation serde with context | `ISerializer<T>`/`IDeserializer<T>` + `SerializationContext` (topic, headers, `KeyData`, `IsNull`) | Null-vs-empty distinction gives the KTable tombstone signal; `IAsyncSerializerPreparer` keeps steady-state sync |
| Static membership / rolling upgrade | `WithGroupInstanceId`, `CloseAsync(ConsumerCloseOptions)` with `GroupMembershipOperation` | Close-without-leave supports rolling restarts |
| Multi-instance hosting in one process | Keyed DI registrations + `AddSingleton<IHostedService>` pattern (`Dekaf.Extensions.Hosting`) | The keyed consumer scale-out mechanics map directly onto N stream-thread hosts |
| Deterministic test substrate | `Dekaf.Testing` (`InMemoryKafkaCluster`, full `IKafkaConsumer`/`IKafkaProducer` surfaces) | Plausible base for a `Dekaf.Streams.Testing` topology driver |
| Streams-group observability | `DescribeStreamsGroupsAsync` / `ListStreamsGroupsAsync` (KIP-1071 describe path) | The full task model (subtopologies, active/standby/warmup tasks) is already typed in `Admin/StreamsGroupTypes.cs` |

### Blocking gaps

| # | Gap | Consequence |
| --- | --- | --- |
| B1 | **No `StreamsGroupHeartbeat` (KIP-1071)** — Dekaf can describe streams groups but not join one ([#2766](https://github.com/thomhurst/Dekaf/issues/2766)) | No broker-side task-aware assignment, no standby/warmup tasks, no broker-registered topology. A runtime on today's APIs runs as an ordinary KIP-848 consumer group |
| B2 | **No client-side assignor pluggability** — server-side assignor name selection only (`WithGroupRemoteAssignor`), no userData/metadata channel | Task-aware sticky assignment cannot be implemented client-side either; KIP-848 removed that extension point by design, so B1 ([#2766](https://github.com/thomhurst/Dekaf/issues/2766)) is the only real path to Streams-grade assignment |

Consequence for scoping: a v0 prototype **must** target topologies where
`task ≈ (subtopology, input partition)` works under plain `uniform` assignment
— i.e. single-subtopology or independently-assignable subtopologies with no
standbys. That is exactly the RFC's word-count prototype, so B1/B2 do not block
the prototype; they block the *product* beyond it.

### Core Dekaf enablers (each independently valuable, per the RFC's rule)

These are client improvements a Streams runtime needs that also stand alone for
ordinary consumer/producer users. They should be filed and justified
individually, not as "Streams hooks":

| # | Enabler | Issue | Today | Independent value |
| --- | --- | --- | --- | --- |
| E1 | Additive rebalance listeners (`AddRebalanceListener`) — the internal `RegisterRuntimeRebalanceListener` is not public and the builder has one mutually-exclusive slot | [#2760](https://github.com/thomhurst/Dekaf/issues/2760) | A runtime library steals the user's listener slot | Any middleware/framework layered on the consumer needs this |
| E2 | Bulk committed-offset fetch on the consumer (`FetchOffsetsAsync` is internal; `GetCommittedOffsetAsync` is per-partition) | [#2761](https://github.com/thomhurst/Dekaf/issues/2761) | N round trips on restore | Any multi-partition startup path benefits |
| E3 | Headers + leader epoch on `ConsumeRawRecord`; partition-EOF signal on batch/raw APIs | [#2762](https://github.com/thomhurst/Dekaf/issues/2762) | Raw path unusable where header propagation or caught-up detection is needed | Raw-path users lose headers today; EOF detection is generally useful |
| E4 | `CommitAsync` throws (or reports) when no coordinator exists instead of silently no-opping (`KafkaConsumer.cs` early return) | [#2763](https://github.com/thomhurst/Dekaf/issues/2763) | Silent data-loss footgun for group-less consumers | Correctness fix for all users |
| E5 | Configurable `IPartitionStopListener` timeout (hard-coded 5 s) | [#2764](https://github.com/thomhurst/Dekaf/issues/2764) | Checkpoint-on-shutdown can be truncated | Anyone flushing state on close |
| E6 | `PublicAPI.Shipped.txt` baseline for core `Dekaf` (satellites have one; core does not) | [#2765](https://github.com/thomhurst/Dekaf/issues/2765) | No guard on the surface Streams would pin against | Protects all downstream consumers of the API |
| E7 | Pre-processing assignment hook and/or timer callbacks in `RunPartitionedAsync` (optional — see Runtime Substrate decision) | — | Restore-before-process and punctuation are not expressible | Useful for any stateful partitioned worker, not only Streams |

### Traps to encode in the design (no code change needed)

1. `SendOffsetsToTransactionAsync(offsets, string groupId)` sends
   `generationIdOrMemberEpoch: -1` — **fencing disabled**. The runtime must use
   the `ConsumerGroupMetadata` overload exclusively; ban the string overload by
   convention and analyzer.
2. Transactional producers are forced to `ConnectionsPerBroker == 1` (build-time
   throw otherwise). Producer-per-task multiplies connections; see the EOS
   decision below.
3. `WithInlineTransactionCompletions` defaults **true** — continuations run on a
   shared broker sender thread. A Streams task lane must set it to `false`.
4. `ListOffsetsOptions.IsolationLevel` defaults `ReadUncommitted` — restoration
   end-offset queries must pass `ReadCommitted` explicitly.
5. `CreateTopicsAsync` retry treats `TopicAlreadyExists` as success
   (deliberately). Concurrent provisioning from N instances is safe, but configs
   are not guaranteed to be yours — always verify `cleanup.policy` via
   `DescribeConfigsAsync` after create.
6. Producer interceptors force the producer off the zero-allocation fast path.
   The runtime must not register any; topology-level hooks live in Streams.
7. Rebalance-callback exceptions are swallowed and logged — a runtime cannot
   fail a rebalance by throwing. Self-fence instead: mark the task failed, stop
   its lane, release cleanly on the next cycle.
8. During `OnPartitionsRevokedAsync`, `consumer.Assignment` already excludes the
   revoked set; only the callback argument carries it.
9. Async deserializers disable `ConsumeBatchAsync`. Prefer
   `IAsyncSerializerPreparer` (async setup, sync steady state) for Schema
   Registry-style serdes.
10. `RecordMetadata.KeySize/ValueSize` are always 0 from the real client — no
    byte-accounting metrics on them.

## Design Decisions Required Before a Prototype

### D1. Group protocol: KIP-848 consumer group now, KIP-1071 streams group later

**Recommendation:** prototype and v0 on a plain KIP-848 consumer group;
implement `StreamsGroupHeartbeat` as a separate, later protocol work item.

Rationale: the describe-side KIP-1071 model is already typed in Dekaf, so the
protocol work is well-scoped, but it is a coordinator-path feature with its own
correctness surface (topology epoch negotiation, task offsets in heartbeats)
and should not gate the prototype. The v0 restriction (no standbys, task ≈
input partition, subtopologies assigned independently) is acceptable for the
word-count prototype and for a useful first product slice. Multi-subtopology
topologies still *work* under KIP-848 — each subtopology's source (or
repartition) topics are just group subscriptions and tasks follow partition
assignment — they simply don't get task-aware stickiness or standbys.

### D2. EOS model: thread-level producer (KIP-447 / EOSv2), not producer-per-task

The parent RFC's prototype text specifies one transaction per task lane with a
transactional ID per `(appId, taskId)` — the EOSv1 shape. Two verified facts
argue for amending that to the EOSv2 shape (one transactional producer per
worker thread, transactional ID per `(appId, processId, threadId)`, fencing via
`SendOffsetsToTransactionAsync` with live `ConsumerGroupMetadata`):

1. `ConnectionsPerBroker` is forced to 1 for transactional producers, so
   producer-per-task means `tasks × brokers` TCP connections — directly against
   the CPU/footprint goals.
2. Dekaf already classifies `StaleMemberEpoch` as Abortable — the member-epoch
   fencing path KIP-447 relies on is wired end-to-end.

Cost: all tasks on a thread share one transaction, so an abort rolls back the
whole thread's batch (Kafka Streams accepts the same trade). The
crash-repair protocol in the RFC (buffered local mutations published only after
commit; discard on abort; replay changelog on doubt) is unchanged — only the
transaction scope widens from task to thread.

**Recommendation:** adopt EOSv2 scope; update the RFC's prototype section when
(if) the prototype is authorized. Keep per-task transactional IDs as a
rejected-alternative note.

### D3. Runtime substrate: build task lanes directly on the consumer, not on `RunPartitionedAsync`

`RunPartitionedAsync` looks like the task scheduler but is missing, in order of
severity: a restore-before-process barrier (lanes start pulling immediately), a
blocking revoke window (its internal listener is fire-and-forget `TryWrite`, so
drain can happen after the revocation is acked — weaker than a hand-registered
listener), punctuation/timers, and access to the underlying consumer from the
lane. Retrofitting all of that turns it into a Streams runtime anyway.

**Recommendation:** the Streams runtime owns its own task scheduler built on
`ConsumeBatchAsync` + `WithRebalanceListener` (builder slot, until E1 lands) +
manually-assigned restore consumers using `ConsumeSnapshotAsync`.
`RunPartitionedAsync` remains the app-level API for non-Streams partitioned
work; E7 improvements to it are optional and independent.

### D4. State engine for v0

Byte-oriented store contracts per the RFC. In-memory engine ships in
`Dekaf.Streams`; the prototype's "persistent key-value store" gate is satisfied
by the first `Dekaf.Streams.Storage.*` package. Options:

- **RocksDB** (`Dekaf.Streams.Storage.RocksDb`): the ecosystem default,
  native-lib packaging works under NativeAOT, but pulls a large native
  dependency and its tuning surface. Streamiz parity comparisons are easiest
  here.
- **Custom log-structured store**: zero external deps, full allocation control,
  but a correctness-critical storage engine is its own multi-release product —
  out of scope for a prototype.

**Recommendation:** RocksDB for the persistent package; treat a custom engine
as explicitly rejected for v0. The abstraction (`open / restore batch / flush /
checkpoint / close`, range iteration capability flag) must not leak RocksDB
types.

### D5. Repository boundary

The #2748 gate requires the package boundary separate from `src/Dekaf`; the RFC
prefers a separate repository. Trade-off: a separate repo enforces the
public-API-only rule mechanically (cannot see internals) and keeps release
cadence independent; a monorepo folder shares CI/tooling but invites internal
coupling. **Recommendation:** separate repository, pinning released `Dekaf`
(or `Dekaf.Abstractions` for contracts) packages. The prototype is disposable
and lives in an experimental repo either way, per the RFC.

## Package Layout (unchanged from RFC, with contents firmed up)

```text
Dekaf.Streams.Abstractions   topology node model, IProcessor, store contracts,
                             serde binding points, task/topology identifiers
Dekaf.Streams                topology builder + compiler, internal-topic planner,
                             task scheduler, EOS commit loop, restoration,
                             in-memory stores, metrics (own Meter/ActivitySource)
Dekaf.Streams.Storage.RocksDb persistent KV + windowed stores
Dekaf.Streams.Testing        deterministic topology driver (over Dekaf.Testing),
                             fault-injection harness
              |
              v
            Dekaf            client, protocol, transactions (public API only)
```

## v0 API Sketch (shape only, not a commitment)

```csharp
var topology = StreamsTopology.Create("word-count-app")
    .Stream("sentences", Serdes.String, Serdes.String)
    .FlatMapValues(static (v, sink) => Tokenize(v, sink))     // sink-style, no IEnumerable/yield
    .GroupByKey()                                             // repartition boundary if key changed
    .Count(Stores.Persistent("word-counts"))                  // changelog-backed
    .ToStream()
    .To("word-counts-out", Serdes.String, Serdes.Int64)
    .Build();                                                 // compiles, fingerprints, plans internal topics

await using var host = KafkaStreams.Create(topology)
    .WithBootstrapServers("...")
    .WithApplicationId("word-count-app")
    .WithProcessingGuarantee(ProcessingGuarantee.ExactlyOnce)
    .WithStateDirectory("/var/lib/app/state")
    .Build();

await host.RunAsync(stoppingToken);
```

Allocation rules baked into the DSL from day one: operators are `static`
lambdas with explicit state or implement `IProcessor<TIn,TOut>`; per-record
forwarding goes through generic-constrained visitors (no boxing, no interface
enumerators); collection-returning operators use sink/writer patterns instead
of `IEnumerable`/`yield`; store APIs are span-based at the byte layer.

## Phasing

**Phase 0 — gate work (no code):** find the owner and the three production use
cases #2748 requires; land this scope doc; decide D1–D5. Exit: gates 1–4 of
#2748 checked or the effort stays deferred.

**Phase 1 — core enablers (independent of any Streams decision):** implement
E1–E6 ([#2760](https://github.com/thomhurst/Dekaf/issues/2760)–[#2765](https://github.com/thomhurst/Dekaf/issues/2765))
as ordinary Dekaf improvements, each with its own justification and benchmarks.
These are worth doing even if Streams never happens; E4 in particular is a
correctness fix. Also fix/document the `RebalanceTimeoutMs` unused-option wart
([#2767](https://github.com/thomhurst/Dekaf/issues/2767)) and the
`ConsumerGroupMetadata.GenerationId` naming.

**Phase 2 — prototype (requires explicit authorization per #2748):** word-count
prototype in a disposable repo per the RFC's nine demonstration requirements,
using the D1–D4 recommendations (KIP-848 group, EOSv2 commit scope, own task
scheduler, RocksDB store). Benchmarked against Streamiz and a hand-written
Dekaf pipeline per the RFC's gates.

**Phase 3 — v0 product (only if the prototype passes every gate):** DSL subset
(map/filter/flatMap/groupByKey/count/aggregate/reduce, KTable materialization,
`ToStream`/`To`), tumbling windows, queryable local state, topology describe,
reset tool. No joins, no session/sliding windows, no standbys.

**Phase 4 — protocol track (parallel, optional):** KIP-1071
`StreamsGroupHeartbeat` in core Dekaf protocol + coordinator path
([#2766](https://github.com/thomhurst/Dekaf/issues/2766)), unlocking
task-aware assignment, standbys, and broker-registered topologies. This is the
only phase that touches `src/Dekaf` beyond E1–E6, and it is justified as
protocol completeness (the describe side already shipped).

## Mapping to #2748 Gates

| Gate | Where this doc lands |
| --- | --- |
| Named maintainer | Open — Phase 0 |
| Documented production use cases | Open — Phase 0 |
| Package/repo boundary separate | D5 recommendation: separate repo |
| Prototype explicitly authorized | Unchanged — this doc does not authorize |
| Topology compilation + internal-topic planning | Scoped: compiler + planner in `Dekaf.Streams`, admin APIs verified sufficient |
| Changelog restoration after crash | Scoped: `ConsumeSnapshotAsync` + checkpoint protocol; RocksDB store |
| Safe task migration during cooperative rebalance | Scoped: builder rebalance listener gives an awaited revoke window; E1 removes the single-slot conflict |
| Kafka-atomic output/changelog/offset commits | Scoped: `SendOffsetsToTransactionAsync(…, ConsumerGroupMetadata)`; D2 amends scope to EOSv2 |
| Fault tests (fencing, abort, restore interrupt, rolling upgrade) | Inherited from RFC test plan; static membership + close-without-leave verified present |
| State-format & topology-compatibility contracts | Fingerprint model in RFC; formats defined in Phase 2 exit criteria |
| NativeAOT/trimming constraints | RFC rules apply; DSL sketch avoids reflection by construction; RocksDB native packaging noted |
| Benchmark plan vs Streamiz + hand-written | Inherited from RFC; unchanged |
| Zero steady-state allocation per record | DSL allocation rules above; hard gate unchanged |
