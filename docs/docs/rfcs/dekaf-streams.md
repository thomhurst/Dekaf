---
sidebar_position: 1
---

# RFC: Evaluate a Separate Dekaf.Streams Runtime

**Status:** Deferred

**Date:** 2026-08-17

**Issue:** [#2562](https://github.com/thomhurst/Dekaf/issues/2562)

## Decision

Do not start `Dekaf.Streams` now. Keep Streams APIs and runtime code out of
`src/Dekaf`.

This is a defer decision, not a rejection. Dekaf has enough primitives to make a
separate runtime technically plausible: cooperative consumer rebalances,
partition-owned processing, transactions, offset-to-transaction commits, raw
batch consumption, and zero-allocation serialization. It does not yet have the
state, recovery, scheduling, compatibility, and testing substrate required to
claim Kafka Streams semantics.

The required prototype is intentionally not authorized by this RFC. A prototype
becomes worthwhile only after the reopen gates below identify a maintainer and
use cases that existing choices do not serve. No implementation commitment is
being made, so the issue's pre-implementation prototype gate remains intact.

## Why This Is Not a Core Client Feature

A Streams runtime is not a collection of LINQ operators. It owns:

- topology compilation and internal topic planning;
- partition-task assignment and scheduling;
- state-store lifecycle, changelogs, restoration, and local fencing;
- event-time progress, windows, grace periods, joins, and late records;
- transactional coupling of input offsets, output records, and changelog writes;
- crash recovery, rolling upgrades, topology compatibility, and diagnostics.

Those responsibilities have a different release cadence and dependency profile
from a Kafka client. `Dekaf.Streams` must therefore be a separate package and
preferably a separate repository. `Dekaf` may be a dependency of Streams;
`Dekaf` must never depend on Streams or a state-store engine.

## Target Use Cases

The possible product is for Kafka-native, continuously running applications
that need one or more of:

- keyed aggregation with recoverable local state;
- stream-table, table-table, or windowed stream-stream joins;
- event-time windows and deterministic late-record handling;
- repartitioning after a key-changing operation;
- Kafka-transactional outputs and consumed offsets;
- read-only queries over locally materialized state.

## Explicit Non-Goals

- Distributed SQL, batch analytics, arbitrary DAG compute, or workflow orchestration.
- Exactly-once claims for external databases or services.
- Hiding partitioning, serialization, or internal-topic compatibility from operators.
- Adding Streams types, state dependencies, or runtime branches to `src/Dekaf`.
- Matching every Kafka Streams operator in the first release.
- Automatic scaling, standby replicas, or remote query transport in the first release.

## Existing Choices

| Choice | Best fit | State/recovery ownership | Cost and constraints |
| --- | --- | --- | --- |
| Direct Dekaf consumer/producer | Stateless transforms, application-owned state, bespoke processing | Application | Smallest surface; direct access to Dekaf performance and NativeAOT support |
| [`RunPartitionedAsync`](../consumer/partitioned-processing-api.md) | Ordered per-partition or per-key work with bounded backpressure | Application; runtime tracks completed offsets only | No topology DSL, changelog, joins, windows, or state restoration |
| [Streamiz.Kafka.Net](https://lgouellec.github.io/streamiz/) | Applications needing an existing .NET Streams DSL and stateful runtime | Streamiz | Mature alternative; its official samples cover stateful aggregation, joins, windows, monitoring, and deduplication |
| Proposed Dekaf.Streams | Kafka-native stateful processing with Dekaf performance goals | Runtime | New correctness-critical product, not an incremental client feature |

Streamiz is the comparison baseline, not a dependency. Its current official
project uses Confluent.Kafka, RocksDB, Newtonsoft.Json, Microsoft.CSharp, and
System.Dynamic.Runtime, and targets through .NET 8 on its development branch
([project file](https://github.com/LGouellec/streamiz/blob/develop/core/Streamiz.Kafka.Net.csproj)).
Its [roadmap](https://github.com/LGouellec/streamiz/blob/develop/roadmap.md)
also demonstrates that parity is a moving target: restoration batching,
sliding/session windows, versioned stores, and other semantics remain distinct
work items. Reimplementing that surface is a multi-release commitment.

## Proposed Architecture If Reopened

### Package boundary

```text
Dekaf.Streams.Abstractions  topology nodes, processors, store contracts
Dekaf.Streams               compiler, task runtime, memory stores
Dekaf.Streams.Storage.*     optional persistent engines
Dekaf.Streams.Testing       deterministic topology driver and fault harness
             |
             v
           Dekaf            client, protocol, transactions
```

`Dekaf.Streams` must not modify producer or consumer hot paths. Integration uses
public Dekaf APIs or narrowly reviewed interfaces useful outside Streams.

### Topology model

Use an immutable typed graph built before startup:

- typed `KStream<TKey,TValue>` and `KTable<TKey,TValue>`-style handles;
- explicit serdes and state-store names;
- deterministic node and internal-topic names;
- repartition boundaries whenever a key-changing operation feeds a keyed operator;
- a topology fingerprint covering operators, stores, serdes, and internal topics.

Compilation divides the graph into subtopologies at repartition boundaries. A
task identity is `(subtopology, input partition)`. Lambdas are normal compiled
delegates; runtime type discovery, `dynamic`, and reflection-based operator
activation are forbidden.

### Runtime ownership

Each active task exclusively owns:

- its input partitions;
- processor instances and stream-time state;
- one instance of every task-local store;
- its output/changelog transaction scope.

One ordered lane executes a task. Parallelism comes from independent tasks, not
concurrent mutation of one store. Cooperative revoke stops new input, drains or
aborts the active transaction, checkpoints store state, and relinquishes the
task. Assignment opens the state directory, restores state, then exposes the
task as active. Processing must not start before restoration catches up.

[Kafka Streams' runtime model](https://kafka.apache.org/43/streams/developer-guide/running-app/)
uses the same essential invariant: migrated task state is restored from its
changelog before processing resumes. Standby/warm replicas are a later
availability optimization, not a first-release requirement.

### State stores and changelogs

Start with byte-oriented contracts to keep serialization explicit:

- key-value and timestamped key-value stores;
- ordered range iteration where the engine supports it;
- task-scoped lifecycle: open, restore batch, flush, checkpoint, close;
- read-only query views separated from mutation APIs;
- in-memory engine in the runtime; persistent engines in optional packages.

Every durable store has a compacted changelog topic. Window stores additionally
need retention derived from window size and grace. Restoration consumes the
changelog in batches and records the last applied changelog offset atomically
with local store state. Store directory names include application ID, topology
fingerprint, task ID, and store name; an OS-level lock fences duplicate local
owners.

This follows Kafka's core recovery contract: fault-tolerant stores are backed by
changelogs and rebuilt by replay
([state-store API](https://kafka.apache.org/43/javadoc/org/apache/kafka/streams/processor/StateStore.html)).

### Time, windows, and joins

- Record timestamps drive event time.
- Task stream time is the maximum observed timestamp, with explicit idling when
  one joined input is temporarily empty.
- Tumbling and hopping windows come before sliding and session windows.
- Grace is part of the operator contract. Records later than `window end + grace`
  are dropped or routed through an explicit late-record handler.
- Stream-stream joins buffer both sides until the join window and grace close.
- Stream-table joins use the table value visible at processing time; temporal
  joins require a separately versioned store design.
- Key-changing operations force a deterministic repartition topic before keyed
  aggregation or join.

### Exactly-once task model

Exactly-once means Kafka outputs, changelog records, and consumed offsets become
visible atomically. It does not include external side effects.

The prototype must use one active Kafka transaction per task lane, with a
transactional ID derived only from application ID and task ID. A new owner
initializing that stable ID obtains a new producer epoch and fences the prior
owner:

1. begin transaction;
2. process a bounded record/time batch;
3. buffer local store mutations while producing output and changelog records;
4. send input offsets plus live consumer group metadata to the transaction;
5. commit the Kafka transaction;
6. publish buffered local mutations/checkpoint, or discard them on abort.

Crash windows between broker commit and local publication are repaired by
replaying the committed changelog. If local publication fails without a process
crash, the task must stop, discard its local store, and restore before serving
queries or processing more input. A local update must never survive an aborted
Kafka transaction. Read-committed isolation is mandatory for source,
repartition, and changelog consumers.

Kafka's exactly-once contract likewise atomically couples produced records and
consumer offsets through transactions
([design documentation](https://kafka.apache.org/43/design/design/)). The
prototype must prove the extra local-store crash protocol rather than infer it
from producer transaction success.

### Queryable state

First release: read-only local queries with explicit store states (`Restoring`,
`Running`, `Revoked`, `Failed`). Queries must fail rather than serve a task after
ownership is lost.

Remote queries require application-supplied transport. The runtime may expose
metadata mapping a key/store to an owning instance, but it must not embed HTTP or
RPC. This matches Kafka Streams' division: it provides store/instance metadata,
while applications supply remote transport
([interactive queries](https://kafka.apache.org/43/streams/developer-guide/interactive-queries/)).

### Diagnostics

Required task/store metrics:

- active/restoring/suspended/failed task counts;
- input, output, dropped-late, and retry rates;
- process, commit, and punctuator latency distributions;
- transaction abort/fence counts;
- store size, cache hit rate, flush duration, and write amplification;
- restoration remaining offsets/bytes and records per second;
- per-partition lag and stream-time skew;
- repartition/changelog producer and consumer health.

Topology description must list source, sink, repartition, and changelog topics so
operators can provision and audit them.

### Upgrade and recovery model

- Persist the topology fingerprint with each state directory.
- Reject incompatible store/serde changes unless an explicit reset or migration
  is configured.
- Keep deterministic internal-topic names stable across compatible upgrades.
- Test rolling upgrades with old/new instances concurrently.
- Fence duplicate state directories and transactional IDs.
- Define reset tooling before public release.

State format is public operational compatibility. Kafka 4.3's own upgrade guide
shows why: changing persisted changelog-offset format creates downgrade and
restore constraints
([upgrade guide](https://kafka.apache.org/43/streams/upgrade-guide/)).

### NativeAOT and trimming

- No assembly scanning, `Assembly.Load`, `dynamic`, runtime code generation, or
  serializer discovery by type name.
- All operators, serdes, and stores are registered through generics or generated
  descriptors.
- Publish NativeAOT smoke applications for stateless and stateful topologies.
- Treat every trim warning as a release blocker.

This is consistent with Microsoft's trimming guidance: unbounded reflection and
dynamic plugin loading cannot be statically analyzed; known registrations or
source generation are preferred
([trim analysis](https://learn.microsoft.com/dotnet/core/deploying/trimming/trimming-concepts)).

## Required Prototype Before Build Approval

Build the prototype outside `src/Dekaf`, preferably in a disposable experimental
repository. It has exactly one topology: partitioned word count with a persistent
key-value store, compacted changelog, and output topic.

It must demonstrate:

1. two process instances sharing tasks;
2. clean scale-out and scale-in rebalance;
3. task restoration on the new owner before processing resumes;
4. hard process kill after local mutation, after changelog produce, before commit,
   and immediately after commit;
5. restart with no lost or duplicated committed counts;
6. transactional output, changelog, and source-offset commits;
7. state-directory fencing and stale-owner rejection;
8. trimmed and NativeAOT publish for the topology host;
9. steady-state `0 B` managed allocation per record.

Do not prototype joins or a broad DSL. Failure of any recovery invariant rejects
the design before API expansion.

## Correctness Test Plan

- Deterministic topology driver for timestamps, grace, punctuation, null/tombstone
  behavior, repartition planning, and store results.
- Real-broker integration tests for create/delete internal topics, cooperative
  rebalance, restoration, transaction fencing, read-committed visibility, and
  rolling restart.
- Fault injection at every transition in the transaction sequence.
- Long-running tests for changelog compaction, disk exhaustion, corrupt local
  state, broker outage, coordinator migration, and slow restoration.
- Model-based comparison of aggregation/window output against a single-threaded
  reference implementation.

No test may use delay-and-hope synchronization. Every failure is investigated;
CI jobs are not blindly rerun.

## Benchmark and Allocation Gates

Compare three implementations on the same VM, broker, topic layout, serdes,
durability, and input data:

1. prototype `Dekaf.Streams`;
2. current Streamiz.Kafka.Net;
3. hand-written Dekaf consumer/producer pipeline.

Workloads:

- pass-through;
- map/filter;
- keyed count using memory and persistent stores;
- tumbling-window count with late records;
- stream-table join;
- restoration from a 1 GiB changelog.

Record throughput, p50/p99/max end-to-end latency, CPU per record, managed
allocations per record, GC counts, disk bytes and write amplification, commit
latency, restoration records/second, and stability. Run steady state and recovery
separately; never average restoration into steady-state throughput.

Hard gates:

- `0 B` managed allocation per record in every steady-state hot path;
- no correctness failure under crash/rebalance injection;
- no protected metric regression between prototype iterations without explicit
  maintainer approval;
- stateless overhead versus the hand-written Dekaf pipeline must be within
  measured run variance, not justified as the price of convenience.

## Reopen Gates

Reopen implementation only when all are true:

1. A named maintainer owns a multi-release runtime, not only the initial DSL.
2. At least three concrete production use cases require recoverable Kafka-local
   state and are not adequately served by direct Dekaf or Streamiz.
3. The prototype plan has an approved time and paid-run budget.
4. State engine and package boundaries are agreed before public API work.
5. The prototype passes every correctness, NativeAOT, allocation, and benchmark
   gate above.

Until then, improve direct processing primitives only when they are independently
valuable to Kafka client users. Do not add speculative hooks to core for a future
Streams runtime.
