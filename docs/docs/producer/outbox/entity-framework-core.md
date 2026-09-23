---
sidebar_position: 1
sidebar_label: Entity Framework Core
description: "Register the Entity Framework Core outbox store, map and provision its three tables, customize table names, and enqueue messages in the same transaction as your business data."
---

# Entity Framework Core Outbox Store

`Dekaf.Outbox.EntityFrameworkCore` stores the [transactional outbox](./index.md) in your relational database through your own `DbContext`. It works with any relational EF Core provider (PostgreSQL, SQL Server, MySQL, SQLite, ...). Delivery guarantees, ordering, deduplication, tuning and metrics are the same for every store and are described on the [outbox overview](./index.md).

```bash
dotnet add package Dekaf.Outbox.EntityFrameworkCore
```

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
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Outbox;
using Dekaf.Outbox.EntityFrameworkCore;

builder.Services.AddDekaf(dekaf => dekaf
    .AddEntityFrameworkCoreOutboxStore<OrdersContext>((services, options) =>
    {
        // Required: your application's EF Core provider, for example
        //   options.UseNpgsql(connectionString);
        //   options.UseSqlServer(connectionString);
        options.EnableDetailedErrors();
    })
    .AddOutboxRelay(producer => producer.WithBootstrapServers("localhost:9092")));
```

This overload registers the context factory and commit interceptors together. If the application already registers its factory (including a pooled factory), keep that registration, add `options.UseDekafOutboxNotifications(services.GetRequiredService<IOutboxNotifier>())` in its options callback, and use the parameterless `AddEntityFrameworkCoreOutboxStore<OrdersContext>()` overload. That parameterless overload does not modify existing context options.

### Commit notifications

The EF interceptors wake the local relay only after a successful implicit commit or an explicit EF Core transaction commit. `SaveChanges` and `SaveChangesAsync` inside an explicit transaction do not notify until `Commit` or `CommitAsync`. Ambient transactions notify after successful transaction completion; the database provider must support ambient enlistment. A rollback or failed save does not trigger publication. The caller does not wait for Kafka acknowledgement as part of the notification.

The interceptors collect the distinct bucket IDs of the added outbox rows, accumulate them across saves in a transaction, and hand them to the notifier after commit, so the relay fetches exactly those buckets. Contexts without the interceptors, and commits performed directly on an externally owned database transaction, do not notify; the relay finds those rows by polling.

The relay's side of notifications, fallback polling and cross-pod hints is described on the [overview](./index.md#commit-notifications-and-fallback-polling).

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
| `StoppedAtUtc` | 64-bit integer | Nullable. UTC ticks. Set when the relay stopped gracefully; null while it runs. |

The ticks columns read as raw `long`s in ad-hoc queries; convert with `new DateTimeOffset(ticks, TimeSpan.Zero)` when inspecting during an incident.

A relay that stops gracefully keeps its row and stamps `StoppedAtUtc` instead of deleting the row. Peers stop counting it at once. The row stays behind so that a statement from the round the stop cancelled cannot bring the relay back: a late heartbeat carries an earlier time than the stamp and is refused, and a late claim is refused for a stopped relay. A relay restarted under the same `RelayId` clears the stamp with its first heartbeat. Relays delete a stopped row one `LeaseDuration` after the stop, and a crashed relay's row after ten.

### Upgrading from a version without `StoppedAtUtc`

Earlier versions mapped `dekaf_outbox_relays` without the `StoppedAtUtc` column, and the relay queries it on every round, so **add the column before you deploy the new version**. It is nullable, so adding it does not rewrite existing rows:

- With EF Core migrations, run `dotnet ef migrations add AddDekafOutboxRelayStoppedAt` after upgrading the package, and apply the migration.
- With hand-written DDL, add a nullable 64-bit integer column, for example `ALTER TABLE dekaf_outbox_relays ADD StoppedAtUtc BIGINT NULL` (SQL Server) or `ALTER TABLE dekaf_outbox_relays ADD COLUMN "StoppedAtUtc" bigint NULL` (PostgreSQL). Use your custom table name and schema if you set them.
- `EnsureCreated()` does not change an existing database. Add the column by hand.

A rolling deployment that mixes old and new relays keeps working once the column exists. Older relays still delete their row on stop, and still count a stopped new relay until its row is older than `LeaseDuration`, so while they run a stop hands buckets over more slowly, as before this change.

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

The `AddOutboxRelay` / `AddEntityFrameworkCoreOutboxStore` helpers register **one** unkeyed store, publisher, and relay per host — calling them twice does not create a second pipeline. To run several logical outboxes (e.g. one per bounded context) in one process, wire the additional relays explicitly; every piece has a public constructor:

```csharp
// Registered (keyed) so the container owns the publisher's disposal - the relay
// deliberately does not dispose the publisher it is given. CreateRelayProducerBuilder
// applies the same enforced delivery guarantees (Acks.All, idempotence, key-respecting
// partitioner) as AddOutboxRelay.
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
