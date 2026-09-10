---
sidebar_position: 8
description: "At-least-once publishing from your database to Kafka without distributed transactions, covering schema, ordering, deduplication, tuning, and custom stores."
---

# Transactional Outbox

The transactional outbox pattern gives you **at-least-once publishing from a database to Kafka** without distributed transactions. Instead of writing to the database and producing to Kafka as two separate operations (either of which can fail while the other succeeds), the service writes the business row *and* the outgoing message into the same database transaction. A background **relay** then publishes pending messages and removes them once the broker acknowledges delivery.

Dekaf ships this as two packages:

- **`Dekaf.Outbox`** — the relay engine, storage contract, and ordering model. No database dependency.
- **`Dekaf.Outbox.EntityFrameworkCore`** — an Entity Framework Core store: schema mapping, bucket leases, and enqueue helpers. Works with any relational EF Core provider (PostgreSQL, SQL Server, MySQL, SQLite, ...).

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

## Setup

Add the outbox model to your `DbContext`:

```csharp
using Dekaf.Outbox.EntityFrameworkCore;

public class OrdersContext(DbContextOptions<OrdersContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseDekafOutbox();  // adds dekaf_outbox_messages, _leases, _relays
    }
}
```

Register the store and the relay:

```csharp
using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;

builder.Services.AddDekafEntityFrameworkCoreOutboxStore<OrdersContext>((services, options) =>
{
    // Configure the EF Core provider used by your application here.
    options.EnableDetailedErrors();
});
builder.Services.AddDekafOutboxRelay(
    producer => producer.WithBootstrapServers("localhost:9092"));
```

This overload registers the context factory and commit interceptors together. If the application already registers its factory (including a pooled factory), keep that registration, add `options.UseDekafOutboxNotifications(services.GetRequiredService<IOutboxNotifier>())` in its options callback, and use the parameterless `AddDekafEntityFrameworkCoreOutboxStore<OrdersContext>()` overload. That parameterless overload does not modify existing context options.

### Commit notifications and fallback polling

The default fallback interval is one second. An idle relay holding buckets makes roughly one pending-message query per second, plus lease maintenance. Successful publication continues draining immediately. Explicit `PollInterval` overrides remain supported; a longer interval is capped by the next lease-renewal deadline.

The EF interceptors wake the local relay only after a successful implicit commit or an explicit EF Core transaction commit. `SaveChanges` and `SaveChangesAsync` inside an explicit transaction do not notify until `Commit` or `CommitAsync`. Ambient transactions notify after successful transaction completion; the database provider must support ambient enlistment. A rollback or failed save does not trigger publication. The caller does not wait for Kafka acknowledgement as part of the notification.

Notifications coalesce and remain pending if a commit races with the relay entering its idle wait. They do not interrupt error backoff, bypass bucket ownership, change ordering, or remove rows before acknowledgement. One notifier belongs to one local relay. A notification received by a relay that does not own the row's bucket cannot wake the remote owner; that owner discovers the row through periodic polling.

External writers, contexts without the interceptors, commits performed directly on an externally owned database transaction, process restarts, and missed notifications all rely on polling. The polling component of discovery latency can therefore approach one second, excluding query and scheduling time, compared with the previous 100 ms default. Normal local writes using the registration above do not wait for that timer. Custom stores can resolve `IOutboxNotifier` and call `NotifyCommitted()` after a confirmed commit; never notify before commit or bypass the relay with an independent publisher.

The relay **enforces** `Acks.All`, idempotence, and a key-respecting partitioner (`Murmur2RandomPartitioner`) on its producer after your `configureProducer` delegate runs — durable acks make prefix deletion safe, idempotence sequences admitted batches, and the partitioner maps equal keys to one partition, so none of them can be downgraded there (any partitioner set in the delegate is overridden). Murmur2-random rather than the stock default because the default sticky-rotates zero-length keys, while the outbox treats an empty serialized key as a real key with an ordering requirement; placement for non-empty keys is identical. These settings do not prevent consumer-visible reordering after partial publish failures. If you need different producer semantics, register your own `IOutboxPublisher` instead (the deliberate opt-out).

## Database Schema

`UseDekafOutbox()` maps three tables. `EnsureCreated()` or an EF Core migration in your project generates them — the entities live in *your* `DbContext` model, so `dotnet ef migrations add` picks them up like any other entity. For DBA review or hand-written DDL, this is the shape:

**`dekaf_outbox_messages`** — pending messages, deleted after broker acknowledgment:

| Column | Type (portable) | Constraints |
|---|---|---|
| `Id` | 64-bit integer | Primary key, auto-increment. Submission order within a bucket. |
| `MessageId` | GUID/UUID | Required. Stable dedup id, stamped as the `x-outbox-message-id` header. |
| `Bucket` | 32-bit integer | Required. Ordering bucket. |
| `Topic` | string(249) | Required. |
| `Key` | binary blob | Nullable (keyless record). |
| `Value` | binary blob | Nullable (tombstone). |
| `Headers` | binary blob | Nullable. Versioned header encoding. |
| `Partition` | 32-bit integer | Nullable (explicit partition override). |
| `CreatedAtUtc` | provider-native timestamp with offset | Required. |

Index: **`(Bucket, Id)`** — the relay's only read path (oldest rows per bucket). The table stays small in steady state; its size is your publish backlog.

**`dekaf_outbox_leases`** — one row per bucket, single-writer coordination:

| Column | Type (portable) | Constraints |
|---|---|---|
| `Bucket` | 32-bit integer | Primary key (not generated). |
| `Owner` | string(128) | Nullable. Relay id currently holding the lease. |
| `ExpiresAtUtc` | 64-bit integer | Required. **Stored as UTC ticks**, not a native timestamp, so expiry comparisons run server-side identically on every provider. |

**`dekaf_outbox_relays`** — relay heartbeats driving fair bucket distribution:

| Column | Type (portable) | Constraints |
|---|---|---|
| `RelayId` | string(128) | Primary key. |
| `LastSeenUtc` | 64-bit integer | Required. UTC ticks, same rationale as above. |

The two ticks columns read as raw `long`s in ad-hoc queries; convert with `new DateTimeOffset(ticks, TimeSpan.Zero)` when inspecting during an incident.

### Custom Table Names and Schema

Point the tables anywhere with `OutboxModelOptions`:

```csharp
public sealed class CustomOutboxContext(DbContextOptions<CustomOutboxContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every property is optional - omit any property to keep its default.
        modelBuilder.UseDekafOutbox(new OutboxModelOptions
        {
            Schema = "messaging",
            MessagesTableName = "orders_outbox",
            LeasesTableName = "orders_outbox_leases",
            RelaysTableName = "orders_outbox_relays"
        });
    }
}
```

For anything beyond naming — column names, provider-specific column types, extra indexes — configure the entities *after* the `UseDekafOutbox()` call; later fluent configuration wins in EF Core:

```csharp
modelBuilder.UseDekafOutbox();
modelBuilder.Entity<OutboxMessage>()
    .Property(m => m.Value).HasColumnName("payload").HasColumnType("jsonb");
```

### Multiple Logical Outboxes

The `AddDekafOutboxRelay` / `AddDekafEntityFrameworkCoreOutboxStore` helpers register **one** unkeyed store, publisher, and relay per host — calling them twice does not create a second pipeline. To run several logical outboxes (e.g. one per bounded context) in one process, wire the additional relays explicitly; every piece has a public constructor:

```csharp
// Registered (keyed) so the container owns the publisher's disposal - the relay
// deliberately does not dispose the publisher it is given. CreateRelayProducerBuilder
// applies the same enforced delivery guarantees (Acks.All, idempotence, key-respecting
// partitioner) as AddDekafOutboxRelay.
services.AddKeyedSingleton<IOutboxPublisher>("second-outbox", (provider, _) =>
    new DekafOutboxPublisher(OutboxServiceCollectionExtensions.CreateRelayProducerBuilder(
            producer => producer
                .WithBootstrapServers("localhost:9092")
                .WithClientId("second-outbox-relay"),
            provider.GetService<ILoggerFactory>())
        .Build()));

services.AddSingleton<IHostedService>(provider => new OutboxRelayService(
    new EfCoreOutboxStore<SecondContext>(
        provider.GetRequiredService<IDbContextFactory<SecondContext>>()),
    provider.GetRequiredKeyedService<IOutboxPublisher>("second-outbox"),
    new OutboxRelayOptions { /* per-outbox tuning */ },
    provider.GetRequiredService<ILogger<OutboxRelayService>>()));
```

Each context keeps its own `UseDekafOutbox(...)` table naming, so the outboxes stay fully independent.

## Enqueuing Messages

Write the outbox row in the same transaction as the business change:

```csharp
using Dekaf.Outbox.EntityFrameworkCore;
using Dekaf.Serialization;

public sealed class OrderApplicationService(IDbContextFactory<OrdersContext> contextFactory)
{
    public async Task PlaceOrderAsync(Order order, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.Orders.Add(order);
        context.AddOutboxMessage(
            topic: "orders",
            key: order.Id,
            value: JsonSerializer.Serialize(order),
            keySerializer: Serializers.String,
            valueSerializer: Serializers.String);

        // One commit: business row and message are atomic.
        await context.SaveChangesAsync(cancellationToken);
    }
}
```

Key and value are stored **pre-serialized** — the relay is a byte pass-through and never re-serializes, so serialization cost is paid exactly once, in your transaction.

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
| `LeaseDuration` | 30 s | Time until takeover after the last successful renewal. The EF store renews during slow publishes, so the default 120 s producer delivery timeout does not expire an otherwise healthy relay's lease. Longer leases tolerate longer store/process stalls but delay takeover. |
| `LeaseRenewInterval` | 10 s | Renewal cadence, including pending publishes. Leave enough slack for database latency, scheduling pauses, and clock skew. |
| `MaxPublishDuration` | `null` | Required only for stores without `IOutboxLeaseRenewalStore`. Bound the **entire** publish call, not one record's delivery timeout. The budget plus a renewal interval must fit inside `LeaseDuration`. |
| `MessageIdHeaderName` | `x-outbox-message-id` | Dedup header stamped on every record. |

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

## Custom Stores (Relational, NoSQL, or Anything Else)

### Lease timing and migration

The EF store implements `IOutboxLeaseRenewalStore`. Its renewal atomically checks ownership and unexpired leases, extends them, and refreshes the relay heartbeat. It does **not** acquire or relinquish buckets while a publish is pending. Fair-share rebalancing resumes between publish calls. Store calls remain serialized: renewal runs alongside the publisher, never alongside another store operation from that relay.

Custom stores should implement the same optional capability. Without it, registration must provide `MaxPublishDuration`; an unspecified bound now fails at startup with `OutboxMisconfigurationException`. For example, if measurement establishes that a custom publisher's whole batch completes within two minutes:

```csharp
var relayOptions = new OutboxRelayOptions
{
    MaxPublishDuration = TimeSpan.FromMinutes(2),
    LeaseDuration = TimeSpan.FromMinutes(3),
    LeaseRenewInterval = TimeSpan.FromSeconds(10)
};
```

The relay measures lease age from **before** acquisition, then rechecks after the pending-bucket probe and batch fetch. Before a legacy-store publish, it reserves the full publish budget plus one renewal interval, renewing first if necessary. If acquisition latency still leaves too little time, it keeps the rows unpublished and logs the configuration problem. `BatchSize`, sequential submission, backpressure and delivery attempts all affect the whole-call bound. Raising a lease above one record's timeout alone does not establish safety.

`MaxPublishDuration` is a timing contract, not a timeout that aborts Kafka delivery. Exceeding it faults the relay instead of repeatedly publishing under an invalid assumption. Custom publishers must yield during asynchronous waits, honor shutdown cancellation, and account for all work covered by their bound.

Renewal cannot protect against a process pause or database outage longer than the remaining lease. After losing a lease during a pending publish, the relay observes the publisher's completion and retains the rows for takeover; it does not start another publish concurrently. Cancellation cannot retract records already appended to Kafka. Such records can still arrive after takeover, so consumer-side message-ID deduplication remains necessary.

### Store contract

`IOutboxStore` is a four-method contract (`AcquireBucketLeasesAsync`, `GetBucketsWithPendingAsync`, `GetNextBatchAsync`, `MarkPublishedAsync`) with **no relational assumptions** — implement it for Dapper, raw ADO.NET, MongoDB, DynamoDB, Cosmos DB, or any storage that offers the two primitives below. The relay engine (`Dekaf.Outbox`) never touches a database API; the EF Core package is just one store.

What a storage technology must provide:

1. **An atomic conditional write** for leases — a SQL guarded `UPDATE ... WHERE owner IS NULL OR expires <= now`, MongoDB `findOneAndUpdate`, DynamoDB conditional `PutItem`, Redis `SET NX PX`. That single primitive is the entire concurrency model; no row locks, transactions across documents, or fencing tokens are required.
2. **Per-bucket enqueue-order reads** — `GetNextBatchAsync` must return a bucket's pending messages oldest-first. *How* is the store's business: an auto-increment column, a monotonic sequence, a time-ordered document id (e.g. ObjectId), or an explicit counter all satisfy it. The `OutboxMessage.Id` long is a relational convenience, not the contract's identity — non-relational stores may leave it zero.

**Message identity is opaque to the relay.** `MarkPublishedAsync` always receives the *same instances* `GetNextBatchAsync` returned — a contiguous prefix, in order. A store can therefore identify what to delete three ways:

```csharp
public sealed class MongoOutboxMessage : OutboxMessage
{
    public required string DocumentId { get; init; }
}

public sealed class MongoOutboxStore
{
    public ValueTask MarkPublishedAsync(
        int bucket,
        IReadOnlyList<OutboxMessage> published,
        CancellationToken cancellationToken)
    {
        // Relational stores can identify rows by Id; any store can use MessageId.
        var relationalIds = published.Select(message => message.Id);
        var messageIds = published.Select(message => message.MessageId);

        // A NoSQL store can instead return a subclass carrying its native identifier.
        var documentIds = published
            .Cast<MongoOutboxMessage>()
            .Select(message => message.DocumentId);

        return ValueTask.CompletedTask;
    }
}
```

Subclassing was chosen over a generic `IOutboxStore<TMessage>` deliberately: a generic parameter would ripple through the relay, the publisher, and every DI registration for all users, while buying nothing the instance pass-back doesn't already provide.

The enqueue side is equally storage-agnostic: `OutboxMessage.Create(...)` serializes with Dekaf serializers and computes the bucket; persist the result in your service's native transaction (a MongoDB session, a DynamoDB `TransactWriteItems`) alongside the business write.

Stores without an auto-increment primitive (e.g. DynamoDB) typically reserve a per-bucket sequence number with an atomic counter before the business transaction commits; sequence gaps from abandoned reservations are harmless — the ordering contract only needs monotonicity, not density.

Semantics your implementation must preserve, in exchange for the relay's guarantees: rows are removed only via `MarkPublishedAsync` (never expired away — a TTL on the pending collection would convert at-least-once into loss), lease grants respect fair-share behavior across active relays (or at minimum never grant one bucket to two live relays), and out-of-range buckets should fail loudly rather than sit unclaimed.

### Wiring a Custom Store

Register your store, then add the relay — the EF Core package is not involved:

```csharp
builder.Services.AddSingleton<IOutboxStore, DynamoDbOutboxStore>();
builder.Services.AddDekafOutboxRelay(
    producer => producer.WithBootstrapServers("localhost:9092"));
```

The relay resolves whatever `IOutboxStore` is registered; `AddDekafEntityFrameworkCoreOutboxStore` is just a convenience registration for the EF implementation.

## Outbox vs. Kafka Transactions

Kafka [transactions](./transactions.md) make multiple *Kafka* writes atomic; they cannot span your database. The outbox exists precisely for the database-and-Kafka atomicity case. Dekaf also supports two-phase-commit prepared transactions (`PrepareAsync` / `CompletePreparedTransactionAsync`, KIP-939) for coordinator-driven setups, but the outbox is the simpler, broker-version-independent default for service integration.
