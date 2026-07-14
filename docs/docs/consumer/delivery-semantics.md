---
sidebar_position: 2
---

# Delivery Semantics

What happens to a message when your processing fails? This page is the definitive reference for
Dekaf's delivery guarantees: what the defaults give you, exactly when an offset becomes
committable, and how to switch to a different guarantee when your workload needs it.

## TL;DR

- **Dekaf's default is at-least-once.** A record's offset only becomes committable once your
  code has demonstrably moved past it. If processing throws out of the consume loop, that
  record is *not* committed and is redelivered after a restart or rebalance.
- **Duplicates are part of the contract.** At-least-once means "never lost, possibly repeated" —
  make processing idempotent.
- **This differs from Confluent.Kafka**, whose auto-commit default is effectively at-most-once
  (offsets become committable at delivery, before processing). Use
  `WithAtMostOnceProcessing()` if you migrated from Confluent and want that behavior.
- **Catching an exception and continuing still counts as processed.** The consume loop cannot
  see inside your `catch`. For strict per-record acknowledgment, use
  [explicit offset storage](#explicit-acknowledgment-strict-at-least-once).

## How the Default Works

Kafka consumers do not acknowledge individual messages; they commit *positions* ("everything
before offset N is done"). The design question every client must answer is: **when does a
delivered record's offset become committable?**

Dekaf's answer, by default: **when the application has proven it processed the record** — by
asking for the next one.

```csharp
await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessAsync(msg);   // If this throws, msg is NOT committed → redelivered.
}                              // Asking for the next msg proves this one was processed.
```

In a sequential `await foreach` loop, the loop body must finish before the enumerator is asked
for the next record. Dekaf uses that request as the acknowledgment signal:

1. When a record is yielded to you, it is **in doubt** — delivered, but not yet committable.
2. When you request the next record (or batch), the previous one is marked **proven**.
3. Proven offsets are staged in batches (once per fetch, not per message — this costs nothing
   on the hot path) and the background auto-commit loop commits them every
   `AutoCommitIntervalMs` (default 5000 ms).
4. If your loop body throws — or you `break` — the enumerator is disposed without that final
   request, so the in-doubt record is never staged. The next consumer to own the partition
   starts from the last proven offset and redelivers it.

The same "a new call proves the previous record" contract applies to `ConsumeOneAsync` and to
`ConsumeBatchAsync` (where the unit of proof is the whole batch).

This is the same contract as the Java client's poll-loop auto-commit — commits only ever cover
records from *completed* loop iterations — but tracked per record rather than per poll.

## Choosing a Guarantee

| You want | Configuration | On processing failure |
|----------|---------------|----------------------|
| At-least-once (default) | Nothing — or `WithAtLeastOnceProcessing()` to make it explicit | Record redelivered if the failure exits the loop |
| At-least-once, strict per-record acks | `WithAutoOffsetStore(false)` + `StoreOffset` after success | Record redelivered even if you catch and continue |
| At-most-once (Confluent-style) | `WithAtMostOnceProcessing()` | Record may be committed anyway — never redelivered |
| Full manual control | `WithOffsetCommitMode(OffsetCommitMode.Manual)` + `CommitAsync` | You decide |
| Exactly-once | Transactions or transactional outbox | See [Exactly-Once](offset-management.md#achieving-exactly-once) |

### At-Least-Once (the default)

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithAtLeastOnceProcessing() // optional — this is the default; call it to state intent
    .SubscribeTo("my-topic")
    .BuildAsync();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessAsync(msg);
}
```

Guarantees, precisely:

- Processing throws and the exception leaves the loop → the failing record and everything after
  it is redelivered. **No loss.**
- Process crash / OOM / kill → everything not yet committed is redelivered. **No loss.**
- Graceful shutdown by cancelling the token → all processed records are committed on dispose.
  **No loss, no duplicates.**
- Rebalance → partitions hand off at the last committed offset; the in-flight tail is
  redelivered to the new owner. **No loss, possible duplicates.**

What it does *not* protect against:

:::caution Catch-and-continue acknowledges the record
```csharp
await foreach (var msg in consumer.ConsumeAsync(ct))
{
    try { await ProcessAsync(msg); }
    catch (Exception ex) { _logger.LogError(ex, "oops"); } // swallowed
    // The loop continues → the next iteration proves msg "processed" → it commits.
}
```
The consume loop cannot distinguish "processed successfully" from "failed but the exception was
swallowed". If you need failed records preserved while the loop continues, either dead-letter
them before continuing, or use [explicit acknowledgment](#explicit-acknowledgment-strict-at-least-once).
:::

:::caution Breaking out of the loop leaves the last record uncommitted
`break` and an exception look identical to the enumerator, so the record you were holding when
you broke is conservatively treated as unproven — it will be redelivered on the next start.
That is safe (a duplicate, not a loss), but if you want an exact handoff, commit explicitly
after the loop:

```csharp
await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessAsync(msg);
    if (SomeCondition) break;
}
await consumer.CommitAsync(); // vouches for everything delivered so far
```

Shutting down by cancelling the consume token does *not* have this caveat — cancellation is
observed while waiting for the next record, after the previous one was proven.
:::

### Explicit Acknowledgment (strict at-least-once)

Auto-commit stays on, but nothing is staged automatically: only offsets you explicitly store
are ever committed. This survives catch-and-continue, hands the acknowledgment decision to your
code, and still avoids per-message commit round-trips.

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithAutoOffsetStore(false)   // nothing staged automatically
    .SubscribeTo("my-topic")
    .BuildAsync();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessAsync(msg);
    consumer.StoreOffset(msg);    // cheap, in-memory; background loop commits it
}
```

:::caution Offsets are positions, not per-message acks
Storing a later offset also commits everything before it. If record 41 failed and you keep
consuming and store record 42, record 41 is committed away with it. Dead-letter the failure or
stop the partition before moving on.
:::

### At-Most-Once (Confluent-style)

A record's offset is staged for commit the moment it is delivered — before your code runs. A
record whose processing fails may already be committed, and is then never redelivered.

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithAtMostOnceProcessing()
    .SubscribeTo("my-topic")
    .BuildAsync();
```

Use when occasional loss is preferable to duplicate work: metrics, logs, ephemeral state where
a stale update is worse than a missing one. This matches the Confluent.Kafka / librdkafka
auto-commit convention, so it is also the drop-in choice when migrating a workload that already
tolerates it.

### Manual Commits

```csharp
var consumer = await Kafka.CreateConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("my-group")
    .WithOffsetCommitMode(OffsetCommitMode.Manual)
    .SubscribeTo("my-topic")
    .BuildAsync();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    await ProcessAsync(msg);
    await consumer.CommitAsync();  // network round-trip; batch for throughput
}
```

See [Offset Management](offset-management.md) for commit batching strategies.

## How Other Clients Compare

| | Dekaf (default) | Java client (default) | Confluent.Kafka (default) |
|---|---|---|---|
| Auto-commit | background loop, 5 s | inside `poll()`, 5 s | background thread, 5 s |
| Offset committable | after processing proven (next pull) | previous poll's records, on next `poll()` | at delivery, before processing |
| Throw out of the loop | redelivered | redelivered | **lost** (commit may already cover it) |
| Catch-and-continue | committed (acknowledged by continuing) | committed | committed |
| Effective guarantee | at-least-once | at-least-once* | at-most-once |

\* For the canonical single-threaded poll loop. Both Dekaf's and Java's guarantee assume the
records from an iteration are processed before the next pull; handing records to background
workers and pulling immediately voids it in every client. For that pattern use the
[Partitioned Processing API](partitioned-processing-api.md), which tracks per-record completion
explicitly — Dekaf refuses to run it on a default-configured consumer for exactly this reason.

## Explicit Commit Semantics

`CommitAsync()` (no arguments) commits the **current position** — everything delivered so far,
*including* a record you are still holding. An explicit commit is you vouching for what you have
seen; it is the escape hatch for clean handoffs after `break` and before `CloseAsync()`.

`CloseAsync()` / `DisposeAsync()` commit only **proven** offsets. Closing because processing
blew up must not commit the record that blew up.

`GetPosition()` reports the position including in-doubt records, but never stages anything.

## Duplicates: the fine print

At-least-once redelivery happens at these boundaries — size your idempotency accordingly:

- **Failure exit:** the failing record and any records after it in already-fetched batches.
- **Crash:** everything after the last background commit (up to `AutoCommitIntervalMs` of work,
  plus the current fetch).
- **`break` without `CommitAsync()`:** exactly one record (the one you were holding).
- **Rebalance:** the in-flight tail of each moved partition.

Idempotent processing turns all of these into non-events. Common patterns: upsert by key,
unique-constraint insert with conflict-ignore, or dedup on `(topic, partition, offset)`.
