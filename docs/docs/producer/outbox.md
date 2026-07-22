---
sidebar_position: 8
---

# Transactional Outbox

The transactional outbox pattern gives you **at-least-once publishing from a database to Kafka** without distributed transactions. Instead of writing to the database and producing to Kafka as two separate operations (either of which can fail while the other succeeds), the service writes the business row *and* the outgoing message into the same database transaction. A background **relay** then publishes pending messages and removes them once the broker acknowledges delivery.

Dekaf ships this as two packages:

- **`Dekaf.Outbox`** — the relay engine, storage contract, and ordering model. No database dependency.
- **`Dekaf.Outbox.EntityFrameworkCore`** — an Entity Framework Core store: schema mapping, bucket leases, and enqueue helpers. Works with any relational EF Core provider (PostgreSQL, SQL Server, MySQL, SQLite, ...).

## How Delivery Works

1. Your service serializes the message **once**, inside its own database transaction, and inserts it as an outbox row. If the business transaction rolls back, the message is never sent.
2. The relay (a hosted service) polls the outbox, publishes each pending row to Kafka with `acks=all` and idempotence enabled, and **deletes rows only after broker acknowledgment**.
3. A crash at any point republishes rather than loses: delivery is **at-least-once**. Every record carries an `x-outbox-message-id` header (the row's stable GUID) so consumers can deduplicate for effectively-once processing.

## Ordering

Records that share a key must arrive in enqueue order. The outbox preserves this with **buckets**:

- Each row's key is hashed to one of N buckets (default 8) at enqueue time. Rows with an explicit partition override are bucketed by that partition instead, so records pinned to one Kafka partition keep their enqueue order too.
- Each bucket is leased to exactly **one relay instance** at a time, and that relay publishes the bucket's rows in insertion order.
- After a partial publish failure, only the **contiguous acknowledged prefix** of a batch is removed — rows are never marked out of order.

Running multiple service instances is safe: relays register heartbeats and divide the buckets fairly among themselves, taking over expired leases when an instance dies. A relay that stalls past its lease may cause **duplicates** (another relay republishes rows it had not yet marked), never loss. During such a takeover the duplicate copies from the old and new owner can interleave on the topic, so a consumer that does **not** deduplicate on the message-id header may briefly observe an older copy after a newer row for the same key — there is no broker-side fencing of an in-flight stale publish short of Kafka transactions. Deduplicating consumers are unaffected.

Lease expiry is compared against timestamps written by the relay hosts themselves, so **relay host clocks must be synchronized** (ordinary NTP is plenty): the tolerable skew is the renewal slack, `LeaseDuration − LeaseRenewInterval` (20 s at defaults). Skew beyond that lets a fast-clocked relay treat a live peer's lease as expired, which produces the same duplicates-never-loss takeover window described above.

:::warning
`BucketCount` must be identical across every writer and relay sharing an outbox table. Changing it requires draining the table and deleting the lease rows first. If the store finds rows in buckets the relay can never claim (a writer configured with a larger count), it throws `OutboxMisconfigurationException` and the relay **faults instead of retrying** — under the default host behavior the application stops, turning the silent-loss misconfiguration into an unmissable failure.
:::

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

builder.Services.AddDbContextFactory<OrdersContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDekafEntityFrameworkCoreOutboxStore<OrdersContext>();
builder.Services.AddDekafOutboxRelay(
    producer => producer.WithBootstrapServers("localhost:9092"));
```

The relay **enforces** `Acks.All`, idempotence, and a key-respecting partitioner (`Murmur2RandomPartitioner`) on its producer after your `configureProducer` delegate runs — durable acks and sequencing are what make contiguous-prefix accounting sound, and per-key ordering only survives if equal keys map to one partition, so none of them can be downgraded there (any partitioner set in the delegate is overridden). Murmur2-random rather than the stock default because the default sticky-rotates zero-length keys, while the outbox treats an empty serialized key as a real key with an ordering requirement; placement for non-empty keys is identical. If you genuinely need different producer semantics, register your own `IOutboxPublisher` instead (the deliberate opt-out).

## Database Schema

`UseDekafOutbox()` maps three tables. `EnsureCreated()` or an EF Core migration in your project generates them — the entities live in *your* `DbContext` model, so `dotnet ef migrations add` picks them up like any other entity. For DBA review or hand-written DDL, this is the shape:

**`dekaf_outbox_messages`** — pending messages, deleted after broker acknowledgment:

| Column | Type (portable) | Constraints |
|---|---|---|
| `Id` | 64-bit integer | Primary key, auto-increment. Publish order within a bucket. |
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
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Every property is optional - pick your own names or omit to keep the defaults.
    modelBuilder.UseDekafOutbox(new OutboxModelOptions
    {
        Schema = "messaging",                     // default: provider default schema
        MessagesTableName = "orders_outbox",      // default: dekaf_outbox_messages
        LeasesTableName = "orders_outbox_leases", // default: dekaf_outbox_leases
        RelaysTableName = "orders_outbox_relays"  // default: dekaf_outbox_relays
    });
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

public async Task PlaceOrderAsync(Order order, CancellationToken cancellationToken)
{
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

    context.Orders.Add(order);
    context.AddOutboxMessage(
        topic: "orders",
        key: order.Id.ToString(),
        value: JsonSerializer.Serialize(order),
        keySerializer: Serializers.String,
        valueSerializer: Serializers.String);

    // One commit: business row and message are atomic.
    await context.SaveChangesAsync(cancellationToken);
}
```

Key and value are stored **pre-serialized** — the relay is a byte pass-through and never re-serializes, so serialization cost is paid exactly once, in your transaction.

## Consumer-Side Deduplication

At-least-once means consumers may see a record twice (crash between broker ack and row deletion, or a lease takeover). Deduplicate on the stamped header:

```csharp
var messageId = result.Headers.FirstOrDefault(h => h.Key == "x-outbox-message-id");
// Track processed ids (e.g. an inbox table keyed by the GUID) and skip repeats.
```

## Tuning

| Option | Default | Notes |
|---|---|---|
| `BucketCount` | 8 | Upper bound on relay parallelism. Must match across all writers and relays. |
| `BatchSize` | 500 | Rows fetched and published per database round trip. |
| `PollInterval` | 100 ms | Idle delay when no bucket had work. |
| `LeaseDuration` | 30 s | Stalled-relay takeover time; also the duplicate window on takeover. Set it comfortably above the producer's delivery timeout: the relay yields for renewal between batches, so only a *single* batch publish outlasting the remaining lease can enter the takeover window, and a lease longer than the worst-case publish makes that unreachable. |
| `LeaseRenewInterval` | 10 s | Must be comfortably below `LeaseDuration`. |
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

`IOutboxStore` is a four-method contract (`AcquireBucketLeasesAsync`, `GetBucketsWithPendingAsync`, `GetNextBatchAsync`, `MarkPublishedAsync`) with **no relational assumptions** — implement it for Dapper, raw ADO.NET, MongoDB, DynamoDB, Cosmos DB, or any storage that offers the two primitives below. The relay engine (`Dekaf.Outbox`) never touches a database API; the EF Core package is just one store.

What a storage technology must provide:

1. **An atomic conditional write** for leases — a SQL guarded `UPDATE ... WHERE owner IS NULL OR expires <= now`, MongoDB `findOneAndUpdate`, DynamoDB conditional `PutItem`, Redis `SET NX PX`. That single primitive is the entire concurrency model; no row locks, transactions across documents, or fencing tokens are required.
2. **Per-bucket enqueue-order reads** — `GetNextBatchAsync` must return a bucket's pending messages oldest-first. *How* is the store's business: an auto-increment column, a monotonic sequence, a time-ordered document id (e.g. ObjectId), or an explicit counter all satisfy it. The `OutboxMessage.Id` long is a relational convenience, not the contract's identity — non-relational stores may leave it zero.

**Message identity is opaque to the relay.** `MarkPublishedAsync` always receives the *same instances* `GetNextBatchAsync` returned — a contiguous prefix, in order. A store can therefore identify what to delete three ways:

```csharp
// 1. Relational: by the Id column (what the EF Core store does)
var ids = published.Select(m => m.Id);

// 2. Any store: by MessageId, the always-present unique GUID
var ids = published.Select(m => m.MessageId);

// 3. NoSQL with native ids: subclass OutboxMessage, return your subclass
//    from GetNextBatchAsync, and downcast on the way back
private sealed class MongoOutboxMessage : OutboxMessage
{
    public required ObjectId DocumentId { get; init; }
}

public ValueTask MarkPublishedAsync(int bucket, IReadOnlyList<OutboxMessage> published, CancellationToken ct)
{
    var documentIds = published.Cast<MongoOutboxMessage>().Select(m => m.DocumentId);
    // collection.DeleteManyAsync(filter on documentIds)
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
