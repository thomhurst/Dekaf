---
sidebar_position: 8
sidebar_label: Overview
slug: /producer/outbox
description: "At-least-once publishing from your database to Kafka without distributed transactions, covering delivery, ordering, scaling, deduplication, tuning, and metrics for every outbox store."
---

# Transactional Outbox

The transactional outbox pattern gives you **at-least-once publishing from a database to Kafka** without distributed transactions. Instead of writing to the database and producing to Kafka as two separate operations (either of which can fail while the other succeeds), the service writes the business row *and* the outgoing message into the same database transaction. A background **relay** then publishes pending messages and removes them once the broker acknowledges delivery.

Dekaf ships the relay and one package per database:

| Package | What it is | Guide |
|---|---|---|
| **`Dekaf.Outbox`** | The relay engine, storage contract, and ordering model. No database dependency. | This page |
| **`Dekaf.Outbox.EntityFrameworkCore`** | Store for any relational EF Core provider (PostgreSQL, SQL Server, MySQL, SQLite, ...): schema mapping, bucket leases, and enqueue helpers. | [Entity Framework Core](./entity-framework-core.md) |
| **`Dekaf.Outbox.DynamoDB`** | Store for Amazon DynamoDB: single-table layout, bucket leases, and a writer for `TransactWriteItems`. | [Amazon DynamoDB](./dynamodb.md) |
| Your own | Anything with an atomic conditional write: Dapper, MongoDB, Cosmos DB, Redis. | [Custom stores](./custom-stores.md) |

Start with the guide for your database: it covers registration, the schema to provision, and how to enqueue. This page describes what every store shares: delivery, ordering, the relay, scaling, deduplication, tuning, and metrics.

## How Delivery Works

1. Your service serializes the message **once**, inside its own database transaction, and inserts it as an outbox row. If the business transaction rolls back, the message is never sent.
2. After commit, a local notification wakes the relay (a hosted service) to publish pending rows to Kafka with `acks=all` and idempotence enabled. It **deletes rows only after broker acknowledgment**. Polling remains the recovery fallback.
3. A crash at any point republishes rather than loses: delivery is **at-least-once**. Every record carries an `x-outbox-message-id` header (the row's stable GUID) so consumers can deduplicate for effectively-once processing.

## Ordering

The outbox uses **buckets** to serialize submission and database acknowledgement accounting for records sharing a key. This is not an unconditional consumer-observed ordering guarantee across partial failures:

- Each row's key is hashed to one of N buckets (default 8) at enqueue time. Rows with an explicit partition override are bucketed by that partition instead, so records pinned to one Kafka partition share the same submission sequence.
- Each bucket is leased to exactly **one relay instance** at a time, and that relay submits the bucket's rows in insertion order.
- After a partial publish failure, only the **contiguous acknowledged prefix** of a batch is removed — rows are never marked out of order.

Running multiple service instances is safe: relays register heartbeats and divide the buckets fairly among themselves, taking over expired leases when an instance dies. A relay that stalls past its lease may cause **duplicates** (another relay republishes rows it had not yet marked), never loss. During such a takeover the duplicate copies from the old and new owner can interleave on the topic, so a consumer that does **not** deduplicate on the message-id header may briefly observe an older copy after a newer row for the same key — there is no broker-side fencing of an in-flight stale publish short of Kafka transactions. Message-id deduplication removes repeated copies; it does not repair a first delivery that arrives after a later row.

One boundary shared by every identity-ordered outbox (not specific to Dekaf): **"enqueue order" means commit order for a key, and only writers that serialize their writes to a key have one.** If two uncoordinated transactions enqueue for the same key concurrently, the database can hand the earlier transaction a lower id while the later one commits first — the relay may then publish the higher id before the lower one exists to read. In practice this doesn't bite, because aggregates written under any concurrency control (optimistic rowversion, `UPDATE ... WHERE version = @n`, a unique constraint) serialize their commits and therefore their ids; two truly uncoordinated concurrent writes to one aggregate have no defined order at the business level either. If you have keys written concurrently without any such control, that's the thing to fix.

Lease expiry is compared against timestamps written by the relay hosts themselves, so **relay host clocks must be synchronized** (ordinary NTP is plenty): the tolerable skew is the renewal slack, `LeaseDuration − LeaseRenewInterval` (20 s at defaults). Skew beyond that lets a fast-clocked relay treat a live peer's lease as expired, which produces the same duplicates-never-loss takeover window described above.

:::warning
`BucketCount` must be identical across every writer and relay sharing an outbox table. Changing it requires draining the table and deleting the lease rows first. If the store finds rows in buckets the relay can never claim (a writer configured with a larger count), it throws `OutboxMisconfigurationException` and the relay **faults instead of retrying** — under the default host behavior the application stops, turning the silent-loss misconfiguration into an unmissable failure.
:::

### Partial failures and consumer order

The default `DekafOutboxPublisher` starts all produce operations in a batch before awaiting their results. An earlier row can fail locally (for example, exceeding the producer's request-size limit) or be permanently rejected by the broker (`MESSAGE_TOO_LARGE`), while later rows on the same key and partition succeed. Idempotence does not make these separate produce operations atomic.

A concrete Kafka-tested example uses rows **1, 2, 3**, all sharing one key and partition. Row 1 is too large; Kafka accepts rows 2 and 3. The acknowledged prefix is empty, so all three database rows remain pending. After repairing row 1's payload while preserving its message ID, retrying the batch yields consumer order **2, 3, 1, 2, 3**. Deduplicating by `x-outbox-message-id` leaves **2, 3, 1**, not enqueue order. This occurs with the real default publisher and its enforced idempotent producer, without a custom publisher or mocked delivery results.

The contract is **ordered submission, front-to-back deletion, and at-least-once delivery**. When all rows succeed, records on a partition retain submission order. Transient retries of an admitted idempotent batch retain producer sequencing, but a permanently rejected or locally unappended row has no earlier delivery for deduplication to preserve. Applications requiring strict business ordering across such failures need an application sequence with consumer-side gap handling or a publisher protocol that prevents later records becoming visible before earlier ones succeed. A custom `IOutboxPublisher` must state and test its own ordering guarantees; returning a contiguous prefix alone is insufficient.

The built-in publisher retains concurrent sends so Kafka can batch records. It does not wait for one broker round trip per outbox row.

## Running the Relay

The relay is a hosted service. Register it next to the store from your database's guide; every instance of your service can run it:

```csharp
builder.Services.AddDekafOutboxRelay(
    producer => producer.WithBootstrapServers("localhost:9092"));
```

### Commit notifications and fallback polling

The default fallback interval is one second. An idle relay holding buckets makes roughly one pending-message query per second, plus lease maintenance. Successful publication continues immediately in bounded sweeps: one batch per ready bucket, so a continuously full bucket cannot starve the others. With one owned bucket, the relay keeps draining until it is empty or rebalancing is due. Full buckets remain ready without another discovery query per batch. While busy, discovery runs at the fallback interval to find newly active buckets. Rebalancing has a separate deadline from in-flight lease renewal and runs after a sweep, even when slow publishes keep extending leases. Explicit `PollInterval` overrides remain supported; a longer interval is capped by the next lease-renewal deadline.

Each store package wakes the local relay after a commit. The EF Core interceptors do it for every committed `SaveChanges` ([details](./entity-framework-core.md#commit-notifications)); on DynamoDB you call `NotifyCommitted` after your transaction ([details](./dynamodb.md#enqueuing-messages)). A rollback or failed write never triggers publication, and the caller does not wait for Kafka acknowledgement as part of the notification.

Notifications coalesce and remain pending if a commit races with the relay entering its idle wait. The built-in notifier implements `IOutboxBucketNotifier`: EF collects distinct bucket IDs from added outbox rows, accumulates them across saves in a transaction, and checks ownership after commit. The relay retains those IDs in bounded storage and fetches hinted owned buckets directly. An isolated partial batch normally needs only a fetch and a bulk delete after initial discovery, without discovery before and after every batch. Periodic discovery keeps its own deadline even when local hints arrive continuously. Unknown-bucket notifications force discovery, and existing custom notifiers keep their discovery behavior. Ownership changes and missed hints remain covered by acquisition and polling; no debounce delay is added. Notifications do not interrupt error backoff, bypass bucket ownership, change ordering, or remove rows before acknowledgement.

One notifier belongs to one local relay. Commits containing only remotely owned buckets do not wake the local relay. Without the optional transport below, the remote owner discovers the row through periodic polling. Relays owning no buckets wait until the next ownership refresh instead of scheduling a polling timer every second; they still heartbeat and participate in fair-share acquisition.

External writers, contexts without the interceptors, commits performed directly on an externally owned database transaction, process restarts, and missed notifications all rely on polling. The polling component of discovery latency can therefore approach one second, excluding query and scheduling time, compared with the previous 100 ms default. Normal local writes using the registration above do not wait for that timer. Custom stores can resolve `IOutboxNotifier` and call `NotifyCommitted()` after a confirmed commit; never notify before commit or bypass the relay with an independent publisher.

The relay **enforces** `Acks.All`, idempotence, and a key-respecting partitioner (`Murmur2RandomPartitioner`) on its producer after your `configureProducer` delegate runs — durable acks make prefix deletion safe, idempotence sequences admitted batches, and the partitioner maps equal keys to one partition, so none of them can be downgraded there (any partitioner set in the delegate is overridden). Murmur2-random rather than the stock default because the default sticky-rotates zero-length keys, while the outbox treats an empty serialized key as a real key with an ordering requirement; placement for non-empty keys is identical. These settings do not prevent consumer-visible reordering after partial publish failures. If you need different producer semantics, register your own `IOutboxPublisher` instead (the deliberate opt-out).

### Optional cross-pod notifications

**For horizontally scaled applications, implement `IOutboxNotificationTransport` when low discovery latency matters across instances.** A commit in pod A can belong to a bucket leased by pod B. The local notifier cannot wake pod B; without a transport, pod B discovers the row on its next poll. This also applies when writers and relay workers run in separate processes. The transport is optional for correctness: polling alone is sufficient if its discovery latency is acceptable.

Cross-process hints provide three benefits:

- The owning relay can discover committed work without waiting for the next polling interval (one second by default). Transport, query, scheduling, and publishing time still apply; this is not a delivery deadline.
- Exact bucket hints let the owner fetch the indicated bucket directly, avoiding a pending-bucket discovery query for that hint.
- You can retain the fallback polling interval instead of shortening it across every instance to reduce discovery latency. Periodic queries still run, and notifications add broadcast traffic; they do not eliminate polling or guarantee higher throughput.

Implement the transport as an adapter to your application's broadcast infrastructure, then register it alongside the relay on each participating instance:

```csharp
services.AddDekafOutboxNotificationTransport<ApplicationOutboxTransport>();
```

`ApplicationOutboxTransport` is application code implementing the interface; no transport provider is bundled. An existing `IOutboxNotificationTransport` singleton registration is preserved, allowing instance or factory registration. The transport is disposed by the DI container. This integration requires the built-in notifier; custom notifiers can continue using local notifications without registering the transport.

#### Implementing the transport

1. **Configure broadcast delivery.** Use a distinct channel per outbox table/environment and the same `BucketCount` on every writer and relay. Every relay sharing the table needs its own subscription that receives every broadcast. Do not use a shared competing-consumer subscription: it could deliver the hint to a relay that does not own the bucket.
2. **Implement `PublishAsync(ReadOnlyMemory<int>, CancellationToken)`.** Encode and broadcast the supplied committed bucket IDs. A single `-1` means the bucket is unknown and requests discovery. Send only these hints, not outbox payloads or message IDs. Consume the supplied memory before the method completes; do not retain it because Dekaf reuses the buffer. Dekaf calls this method from one background sender, never from the application's commit thread.
3. **Implement `ListenAsync(Action<int>, CancellationToken)`.** Keep the subscription active until cancelled. Decode each broadcast and invoke the callback once for each received bucket ID, including `-1` and remotely owned buckets; Dekaf handles ownership filtering. The adapter owns transport reconnection. If the subscription fails or ends unexpectedly, Dekaf calls `ListenAsync` again after `ErrorBackoff`, so release the previous subscription before returning or throwing.
4. **Support concurrency and shutdown.** Listening and publishing run concurrently on the singleton transport. Honor cancellation in both methods, unsubscribe during shutdown, and ensure all callbacks have finished before `ListenAsync` completes. Dispose transport resources owned by the adapter through the DI container.
5. **Connect committed writes to the built-in notifier.** The EF Core registration above already does this. A custom store or writer resolves `IOutboxNotifier` and calls `NotifyCommitted()` only after a confirmed commit. It does not need to implement `IOutboxNotifier` or call the transport directly. When bucket IDs are available, use the built-in notifier's `IOutboxBucketNotifier` capability to preserve exact hints.

Validate the adapter with two relay instances: commit in one instance to a bucket owned by the other and verify that the owner wakes before its fallback poll. Also test duplicate hints, transport disconnection, recovery through polling, and cancellation while callbacks are active. Hints are advisory and may be duplicated, reordered, or lost; they never authorize publication without a bucket lease or change at-least-once delivery guarantees.

Commit callbacks only update a bounded, coalescing buffer. Background workers send and receive independently of database commits and Kafka publishing. Coalescing adds no debounce delay. Incoming broadcasts never get rebroadcast, including self-echoes. Send failures drop that advisory batch and apply `ErrorBackoff`; subscription failures or unexpected completion also retry with backoff. Polling always remains enabled, covering startup, lost hints and transport outages. Both methods must honor cancellation, and `ListenAsync` must finish all callbacks before returning. Shutdown observes both workers and does not guarantee delivery of buffered hints.

The transport preserves exact IDs for EF's `HashSet<int>`, `ImmutableHashSet<int>`, individual bucket notifications, and `SortedSet<int>` containing at most two buckets. Other set types and larger sorted sets emit one coalesced unknown-bucket hint, avoiding allocation or a scan of all configured buckets inside the commit callback. That hint makes receiving relays query for pending work in their owned buckets. Custom writers should use `HashSet<int>` or individual bucket notifications when they need exact remote hints without that discovery query.

### Horizontal scaling

The default eight buckets permit at most eight active draining relays. Extra application pods still maintain membership but own no buckets. Scaling application writers and relay workers separately can bound coordination work. For low discovery latency across pods or separate workers, use [cross-pod notifications](#optional-cross-pod-notifications); otherwise, accept polling discovery latency. Local notifications alone cannot wake a remote bucket owner. Increasing `BucketCount` still requires draining the table first.

To avoid querying the same whole-table backlog from every pod, opt in on **all** relays sharing that store:

```csharp
services.AddDekafOutboxRelay(
    producer => producer.WithBootstrapServers("localhost:9092"),
    new OutboxRelayOptions { CollectMetricsOnBucketZeroOwnerOnly = true });
```

Only the relay holding a locally valid lease for bucket zero samples backlog metrics. Non-owners report unavailable backlog observations. Samples completing after ownership loss are discarded, and a new owner starts sampling on its next collection interval; handover may temporarily leave no sample. Existing leases coordinate sampling without a new table or migration. This is advisory sampling, not a distributed lock for arbitrary work: an already-running query can overlap takeover if its cancellation is delayed. Every sampler retains its query timeout.

The default remains per-relay sampling for compatibility with per-pod dashboards. When coordinating, aggregate backlog count and age across pods using **maximum**, not sum, and handle unavailable observations. Publish counters and owned-bucket gauges remain per relay. A rolling deployment with older or unconfigured relays continues to work but still includes their duplicate metric queries.

## Consumer-Side Deduplication

At-least-once means consumers may see a record twice (crash between broker ack and row deletion, or a lease takeover). Deduplicate on the stamped header:

```csharp
var messageId = outboxResult.Headers.FirstOrDefault(h => h.Key == "x-outbox-message-id");
// Track processed ids (e.g. an inbox table keyed by the GUID) and skip repeats.
```

## Tuning

| Option | Default | Notes |
|---|---|---|
| `BucketCount` | 8 | Upper bound on relay parallelism. Must match across all writers and relays. |
| `BatchSize` | 500 | Rows fetched and published per database round trip. |
| `PollInterval` | 1 second | Fallback discovery interval; local commit notifications interrupt idle waiting. |
| `LeaseDuration` | 30 s | Time until takeover after the last successful renewal. The EF Core and DynamoDB stores renew during slow publishes, so the default 120 s producer delivery timeout does not expire an otherwise healthy relay's lease. Longer leases tolerate longer store/process stalls but delay takeover of a crashed relay. A gracefully stopped relay [releases its leases](./custom-stores.md#stable-ownership-and-graceful-handover), so peers take over on their next acquisition. |
| `LeaseRenewInterval` | 10 s | Renewal cadence, including pending publishes. Leave enough slack for database latency, scheduling pauses, and clock skew. |
| `MaxPublishDuration` | `null` | Required only for stores without `IOutboxLeaseRenewalStore`. Bound the **entire** publish call, not one record's delivery timeout. The budget plus a renewal interval must fit inside `LeaseDuration`. |
| `MessageIdHeaderName` | `x-outbox-message-id` | Dedup header stamped on every record. |
| `MetricsName` | `outbox` | Stable logical store name in the `outbox.name` metric tag; 1–64 nonblank characters. Use the same name for replicas sharing a store, distinct names for independent stores. |
| `MetricsCollectionInterval` | 30 s | Minimum delay after each optional backlog query completes. No catch-up bursts. |
| `MetricsCollectionTimeout` | 5 s | Cancellation deadline for an optional backlog query. The store must honor cancellation. |
| `CollectMetricsOnBucketZeroOwnerOnly` | `false` | Sample backlog only on the bucket-zero owner; enable across every relay sharing the store. Non-owners report unavailable backlog metrics. |

Pass options at registration:

```csharp
builder.Services.AddDekafOutboxRelay(
    producer => producer.WithBootstrapServers("localhost:9092"),
    new OutboxRelayOptions
    {
        BucketCount = 16,
        BatchSize = 1000,
        PollInterval = TimeSpan.FromMilliseconds(50)
    });
```

## Operational metrics

The relay exposes the `Dekaf.Outbox` meter through `OutboxDiagnostics.MeterName`. Subscribe with your existing .NET metrics pipeline; for example, add `.AddMeter(OutboxDiagnostics.MeterName)` to an OpenTelemetry `MeterProviderBuilder`. Meter callbacks read cached state; they never query the database.

Every instrument has only one library tag, `outbox.name`. Do not put message IDs, keys, tenant IDs, or generated `RelayId` values in `MetricsName`. Exporter resource attributes can identify service instances. Counters and histograms are recorded once per batch or cycle, without a per-message instrumentation loop. Their listener callbacks execute inline, so exporters must keep callbacks fast. Listener exceptions do not change publication or deletion outcomes.

| Instrument | Type / unit | Meaning |
| --- | --- | --- |
| `dekaf.outbox.owned_buckets` | Gauge / buckets | Buckets in the relay's current local lease set. Updated on acquisition or invalidation; this is not a live database ownership query. |
| `dekaf.outbox.publish.acknowledged` | Counter / messages | Contiguous acknowledged prefix reported by the publisher, counted before lease validation and database deletion. |
| `dekaf.outbox.publish.failures` | Counter / attempts | Batch publish calls that throw or return `FirstError`, once per attempt. Cooperative shutdown cancellation is excluded. Store query, deletion, and lease errors are not publish failures. |
| `dekaf.outbox.lease.expirations` | Counter / events | Observed expiry of a nonempty owned lease set, once when that local set is invalidated. This does not count individual buckets or unobserved expiry while the process is stopped. |
| `dekaf.outbox.publish.duration` | Histogram / seconds | Entire batch publisher call, including failed and cancelled calls. |
| `dekaf.outbox.cycle.duration` | Histogram / seconds | Acquisition, pending probe, and draining for one relay cycle, including failure paths. Excludes idle delay and error backoff. |
| `dekaf.outbox.pending.messages` | Gauge / messages | Latest available whole-store pending count, including buckets this relay does not own. |
| `dekaf.outbox.pending.oldest_age` | Gauge / seconds | Age of the oldest pending row from the latest sample. Its age increases between samples. Future timestamps are clamped to zero. |
| `dekaf.outbox.pending.available` | Gauge / dimensionless | `1` when a pending-count snapshot is available; `0` before sampling, when unsupported, or after a failed/timed-out/null sample. |

Acknowledgements count attempts, not unique messages. For example, publishing two rows successfully and then failing to delete them adds two acknowledgements. A successful retry adds two more. Later acknowledgements outside a failed batch's contiguous prefix cannot be inferred from `IOutboxPublisher` and are not counted. This counter cannot prove exactly-once delivery; consumers still need message-ID deduplication.

Backlog sampling is optional. Existing `IOutboxStore` implementations keep working without implementing `IOutboxMetricsStore`; their backlog is **unavailable**, not zero. A known empty snapshot emits count `0` and age `0`. A nonempty snapshot without a timestamp emits its count but omits age. Failed, timed-out, or null samples clear the previous snapshot and omit both count and age until a successful sample. Stopping or disposing a relay unregisters its observations.

The optional collector runs separately from publication, with at most one outstanding query per relay. It queries only while a pending instrument has a listener, then waits `MetricsCollectionInterval` after completion. There is no query per message or per scrape. Sampling is concurrent with ordinary store operations, so implement this capability with a separate database context/connection and honor the supplied cancellation token. A custom store that ignores cancellation can delay shutdown; the relay does not abandon queries and start overlapping replacements.

`EfCoreOutboxStore` implements this capability using a separate context. Where supported, one aggregate query computes count and minimum timestamp together rather than issuing two independent queries. It still reads the whole backlog; coordinated sampling reduces duplicate work across relays. No new timestamp index is added to enqueue/delete paths. SQLite's native `DateTimeOffset` mapping cannot translate timestamp aggregation, so SQLite supplies the count and leaves nonempty age unavailable. No schema conversion or client-side timestamp scan is performed. See the [EF Core SQLite limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations).

Custom stores can implement `IOutboxMetricsStore.GetPendingMetricsAsync` alongside `IOutboxStore`, returning `new OutboxPendingMetrics(count, oldestCreatedAtUtc)`. Return `null` if no snapshot is available, or use a null timestamp when only the count is known. The count must be nonnegative. Use a bounded query strategy appropriate to your database and collection interval.

When several relays in one process share `MetricsName`, owned buckets are summed; whole-store pending count and known age use the maximum available sample. Across process replicas, also use `max` for backlog and age to avoid multiplying the same database rows. Samples may differ temporarily. Use distinct names for independent databases whose counts should be added.

### Example alerts and queries

These PromQL examples assume classic OpenTelemetry Prometheus translation: dots become underscores, counters gain `_total`, and seconds gain `_seconds`. Check your exporter's [translation configuration](https://opentelemetry.io/docs/specs/otel/metrics/sdk_exporters/prometheus/) if names differ. Scope queries to one service/environment with your resource labels, and adjust thresholds to your delivery objective.

```promql
# Acknowledgements per second, including re-acknowledged retries.
sum by (outbox_name) (rate(dekaf_outbox_publish_acknowledged_total[5m]))

# Any publish failures in five minutes.
sum by (outbox_name) (increase(dekaf_outbox_publish_failures_total[5m])) > 0

# Oldest pending row exceeds five minutes; require this for a sustained alert window.
max by (outbox_name) (dekaf_outbox_pending_oldest_age_seconds) > 300

# Whole-store backlog exceeds an application-specific capacity threshold.
max by (outbox_name) (dekaf_outbox_pending_messages) > 10000

# No replica has an available backlog sample.
max by (outbox_name) (dekaf_outbox_pending_available) == 0

# An observed lease expiry needs investigation even if publishing later recovers.
sum by (outbox_name) (increase(dekaf_outbox_lease_expirations_total[5m])) > 0

# p99 publisher duration from classic histogram buckets.
histogram_quantile(0.99, sum by (le, outbox_name)
  (rate(dekaf_outbox_publish_duration_seconds_bucket[5m])))
```

Also alert on a missing scrape target: no series is different from `pending.available == 0`. An unavailable oldest-age series with `pending.available == 1` can mean the store knows the count but cannot provide timestamps. Never replace missing backlog or age with zero in a dashboard.

## Outbox vs. Kafka Transactions

Kafka [transactions](../transactions.md) make multiple *Kafka* writes atomic; they cannot span your database. The outbox exists precisely for the database-and-Kafka atomicity case. Dekaf also supports two-phase-commit prepared transactions (`PrepareAsync` / `CompletePreparedTransactionAsync`, KIP-939) for coordinator-driven setups, but the outbox is the simpler, broker-version-independent default for service integration.
