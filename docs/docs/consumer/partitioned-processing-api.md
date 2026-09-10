---
sidebar_position: 7
description: "RunPartitionedAsync keeps per-key work ordered while processing partitions in parallel, covering commit semantics, backpressure, and error policy."
---

# Partitioned Async Processing

`RunPartitionedAsync` is the high-level consumer API for work that must stay
ordered within each Kafka partition but can run in parallel across partitions.

Use it when:

- a key, customer, tenant, aggregate, or stream shard must be processed in offset order
- different partitions can run at the same time
- offset commits must reflect completed processing, not just fetched records
- partition revoke, lost, and shutdown behavior must be explicit

The method owns the consume loop while it runs. Do not call `ConsumeAsync`,
`ConsumeBatchAsync`, `ConsumeRawBatchAsync`, `consumer.Partitions.Assign`,
`consumer.Partitions.Unassign`, `consumer.Partitions.Pause`, or
`consumer.Partitions.Resume` concurrently on the same consumer.

## Basic Usage

Use manual offset commits for at-least-once partitioned processing:

```csharp
await using var consumer = await Kafka.CreateConsumer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-workers")
    .WithOffsetCommitMode(OffsetCommitMode.Manual)
    .SubscribeTo("orders")
    .BuildAsync();

var options = new PartitionedProcessingOptions
{
    MaxBufferedRecordsPerPartition = 256,
    BackpressureMode = PartitionBackpressureMode.PauseResume,
    StopPolicy = PartitionStopPolicy.Drain,
    CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke
};

await consumer.RunPartitionedAsync(
    async (partition, ct) =>
    {
        await foreach (var message in partition.Messages.WithCancellation(ct))
        {
            await ProcessOrderAsync(message.Value, ct);
            partition.MarkProcessed(message);
        }
    },
    options,
    stoppingToken);
```

Dekaf starts one processor invocation for each assigned `TopicPartition`. Each
processor receives only the ordered stream for that partition. Processors for
different partitions run concurrently.

## Record Storage Lifetime

Record and batch handlers can read raw keys, raw values, and lazy headers across
asynchronous suspension until the handler returns. Dekaf retains the owning fetch
storage while records are queued or being processed. For the long-lived partition
processor's `Messages` stream, each record remains valid until the enumerator
advances or is disposed. Copy borrowed data if it must outlive that boundary.

Batch handlers also borrow the `IReadOnlyList` view. Its storage is reused after the
handler completes. Copy the records, along with any borrowed key/value/header data,
before retaining a batch outside its handler.

Custom deserializers may return slices of their input: that fetch storage follows
the same lifetime. A deserializer's own reusable scratch buffer is not fetch
storage; return an owned value instead of exposing scratch memory that the next
deserialization will overwrite.

Retention ends after completion, failure, or cancellation. A handler that ignores
cancellation retains its active storage until it actually exits, even when the
runtime's stop timeout has elapsed. Revoke and lost-assignment cleanup use the same
ownership rules. Key ordering also retains the input backing a dictionary key
until its key lane is removed.

For key ordering, keep each key's hash code and equality stable while records are
queued or running. If cleanup cannot remove a key, dispatch fails and observes
in-flight handlers before releasing retained storage.

`MaxBufferedRecordsPerPartition` bounds the partition queue, not retained bytes.
Partition ordering additionally holds the active handler batch (one record for a
record handler). Key ordering additionally holds at most that many dispatched
records and one retained key per active key lane. Completed records release their
dispatch slots and fetch storage immediately, including behind an unfinished
earlier record; commits still wait for that gap to close. One borrowed record can
pin an entire fetch response and its decompressed
record storage. Budget those buffers separately from consumer prefetch; fetch-size
settings and compression affect their byte cost. Slow handlers do not accumulate
an unbounded history of completed fetches.

## Ordering And Parallelism

The processor callback is long-lived. It starts when a partition is assigned and
ends when the partition is revoked, lost, stopped, failed, or when the whole
consumer shuts down.

Within one partition:

- messages are yielded in Kafka offset order
- at most one processor invocation is active
- `MarkProcessed` records completed offsets; commits advance only through
  contiguous completed offsets

Across partitions:

- processors run independently
- slow partitions do not block processing already queued for other partitions
- shared application state must still be protected by your code

```csharp
await consumer.RunPartitionedAsync(
    async (partition, ct) =>
    {
        var topicPartition = partition.TopicPartition;

        await foreach (var message in partition.Messages.WithCancellation(ct))
        {
            await HandlePartitionRecordAsync(
                topicPartition,
                message.Offset,
                message.Value,
                ct);

            partition.MarkProcessed(message);
        }
    },
    cancellationToken: stoppingToken);
```

This pattern lets partition `orders-0` continue in offset order while
`orders-1`, `orders-2`, and other assigned partitions run their own ordered
lanes at the same time.

### Key-ordered handlers

Use the record-handler overload when you want Dekaf to invoke your handler per
record and mark records processed after the handler returns successfully:

```csharp
await consumer.RunPartitionedAsync(
    async (partition, message, ct) =>
    {
        await HandleOrderAsync(message.Key, message.Value, ct);
    },
    new PartitionedProcessingOptions
    {
        Ordering = PartitionedProcessingOrder.Key,
        MaxConcurrentHandlersPerPartition = 8,
        MaxBufferedRecordsPerPartition = 512
    },
    stoppingToken);
```

With `PartitionedProcessingOrder.Key`, different keys from the same Kafka
partition can run concurrently, but records with the same key are processed in
offset order. Dekaf tracks offset gaps, so a later key cannot advance commits
past an earlier unfinished record.

Key equality applies to **deserialized keys within one partition**. By default,
`byte[]`, `ReadOnlyMemory<byte>`, `Memory<byte>`, and `ArraySegment<byte>` keys
compare their byte content, including slice boundaries. Equal bytes share a lane
even when deserialization creates separate arrays or memory slices. Empty binary
keys share a lane. Kafka null keys share a separate lane, including when a binary
value-type deserializer represents both null and empty bytes as its default value.
Other key types use
`EqualityComparer<TKey>.Default`, preserving ordinal string and value-type equality.
These binary defaults apply when `TKey` is one of the listed types; wrapper or
polymorphic key types can supply a comparer.

Default binary hashing reads at most 64 bytes and includes the key length. Dispatch
caches that hash through lane removal. Equality still compares every byte, so keys
that differ outside the sampled regions remain distinct. Repeated keys and hash
collisions can therefore still cost more as key length grows. For such workloads,
consider deserializing a compact application identity and supplying a comparer
that preserves the required equality. A hash alone does not establish identity.

For custom key equality, supply an `IEqualityComparer<TKey>` to either handler
overload. For example, a string-keyed consumer can group case-insensitive keys:

```csharp
await consumer.RunPartitionedAsync(
    async (partition, message, ct) =>
    {
        await HandleOrderAsync(message.Key, message.Value, ct);
    },
    new PartitionedProcessingOptions
    {
        Ordering = PartitionedProcessingOrder.Key,
        MaxConcurrentHandlersPerPartition = 8
    },
    StringComparer.OrdinalIgnoreCase,
    stoppingToken);
```

The comparer must be thread-safe, give equal keys equal hash codes, and keep both
equality and hash codes stable while a key's lane remains active. Do not mutate
binary key bytes or fields used by the comparer during processing. Dekaf retains
fetch storage backing a lane's representative key until that lane becomes idle;
application-owned key storage must obey the same lifetime. Copy borrowed data if
you keep it after processing completes.

Choose equality that matches your application's serialized Kafka key identity.
A custom comparer can deliberately group different wire keys (for example,
case-insensitive strings), but cannot impose order across different partitions.
Custom deserializers that transform or discard key information must provide an
appropriate comparer when their default equality does not preserve that identity.
Retain the identity fields in the deserialized key, or use a binary key type;
a comparer cannot recover discarded wire data.
Comparers do not receive null keys and are ignored for partition ordering.

### Batch handlers

Use `RunPartitionedBatchesAsync` when your application works more efficiently
on groups of records:

```csharp
await consumer.RunPartitionedBatchesAsync(
    async (partition, messages, ct) =>
    {
        await SaveBatchAsync(messages.Select(static m => m.Value), ct);
    },
    new PartitionedProcessingOptions
    {
        Ordering = PartitionedProcessingOrder.Key,
        MaxConcurrentHandlersPerPartition = 4,
        MaxBufferedRecordsPerPartition = 400,
        MaxHandlerBatchSize = 100
    },
    stoppingToken);
```

Batch handlers receive up to `MaxHandlerBatchSize` records. In key-ordered mode,
each batch contains records for one key lane. Records in a handler or batch are
marked processed only after the callback completes successfully.

Key-ordered processing divides reusable batch storage across the configured
workers. The effective worker count is the smaller of
`MaxConcurrentHandlersPerPartition` and `MaxBufferedRecordsPerPartition`.
The effective batch cap is the smaller of `MaxHandlerBatchSize` and
`MaxBufferedRecordsPerPartition / effectiveWorkerCount`, using integer division.
This cap applies even when fewer keys are active. With the default 256-record
buffer and four workers, requesting 100-record batches gives a cap of 64. The
example reserves 400 records to permit up to 100 per batch with four workers.
A batch can still contain fewer records when its key has less work available.

## Commit Semantics

`MarkProcessed(message)` marks `message.Offset + 1` as eligible for commit for
the current partition. Dekaf never commits a record merely because it was
fetched or yielded.

Commit policies:

| Policy | Behavior |
| --- | --- |
| `UserManaged` | Dekaf tracks processed offsets, but only your code calls `CommitProcessedAsync` or another commit mechanism. |
| `CommitCompletedOnRevoke` | Dekaf commits completed offsets when a partition is revoked and during graceful shutdown. This is the default. |
| `CommitCompletedPeriodically` | Dekaf commits completed offsets on `CommitInterval`, then again on revoke and graceful shutdown. |

Manual per-partition commits are useful when you want a tighter commit cadence
without waiting for a rebalance:

```csharp
await consumer.RunPartitionedAsync(
    async (partition, ct) =>
    {
        await foreach (var message in partition.Messages.WithCancellation(ct))
        {
            await SaveAsync(message.Value, ct);
            partition.MarkProcessed(message);

            if (ShouldFlushOffset(partition.LastProcessedOffset))
                await partition.CommitProcessedAsync(ct);
        }
    },
    new PartitionedProcessingOptions
    {
        CommitPolicy = PartitionCommitPolicy.UserManaged
    },
    stoppingToken);
```

`CommitProcessedAsync` commits only the current partition's contiguous completed
offset.
Runtime-managed revoke and shutdown commits may batch completed offsets for
multiple partitions into one `CommitAsync` call.

Auto commit mode with automatic offset store (`OffsetCommitMode.Auto` +
`EnableAutoOffsetStore = true`, the consumer defaults) stages offsets based on
consume-loop progress. The partitioned runtime pulls records off the loop and
dispatches them to workers, so loop progress no longer proves processing — the
background loop would stage and commit records regardless of `MarkProcessed`,
silently voiding the runtime's at-least-once tracking. This applies to both
`OffsetStoreTiming` values, since neither ties staging to worker completion.
`RunPartitionedAsync` therefore throws `InvalidOperationException` when the
consumer uses that combination together with a runtime-managed commit policy
(`CommitCompletedOnRevoke` or `CommitCompletedPeriodically`). For at-least-once
partitioned processing, configure the consumer with `OffsetCommitMode.Manual`, or
with `WithAutoOffsetStore(false)` — with the automatic store disabled, the
background loop has nothing of its own to commit, so only the runtime's
`MarkProcessed`-tracked commits advance offsets. The combination of auto commit
and `PartitionCommitPolicy.UserManaged` also remains allowed for applications
that accept auto-commit semantics.

This check inspects the commit configuration of Dekaf's own consumer
implementations (`KafkaConsumer` and the testing `InMemoryConsumer`). Custom
implementations and wrappers can opt into the same guard by implementing
`IConsumerCommitConfiguration` and forwarding `OffsetCommitMode`,
`EnableAutoOffsetStore`, and `HasConsumerGroup`. Types that do not implement that
interface cannot be inspected and will not fail fast, so ensure their underlying
configuration follows the rules above.

Transactions remain user-managed. If a processor writes transactionally, send
the processed offsets to that transaction and do not also let the partitioned
runtime commit them outside the transaction.

## Assignment Lifecycle

When partitions are assigned, Dekaf creates partition state, starts exactly one
processor lane per partition, then routes records to those lanes.

When partitions are revoked during cooperative rebalance, Dekaf:

1. Stops routing new records to the revoked partitions.
2. Removes queued or prefetched records for partitions no longer assigned.
3. Applies `StopPolicy`.
4. Commits only offsets that were marked processed when the commit policy allows it.
5. Disposes partition state and completes the partition message stream.

When partitions are lost involuntarily, such as after a heartbeat timeout, Dekaf
cancels those partition lanes and completes their streams. It does not commit
offsets for lost partitions because ownership is no longer guaranteed.

## Shutdown

Cancelling the token passed to `RunPartitionedAsync` stops the consume loop and
then stops all active partition lanes.

`PartitionStopPolicy.Drain`:

- completes each partition message stream
- lets the processor finish already queued records
- waits up to `StopTimeout`
- commits completed offsets during graceful shutdown when the commit policy allows it

`PartitionStopPolicy.Cancel`:

- cancels each processor's token immediately
- does not wait for the remaining queued records to be processed
- commits only offsets already marked processed when the commit policy allows it

The `CancellationToken` passed to the processor is the partition stopping token.
With `Drain`, the normal signal is stream completion. With `Cancel`, lost
partitions, processor failure cleanup, or drain timeout, the token is cancelled.

If a processor does not finish within `StopTimeout`, Dekaf treats the timeout as
fatal because it can no longer guarantee a single active processor for that
partition. During shutdown, `StopTimeout` also bounds the final runtime-managed
commit attempt.

## Backpressure

Each partition lane has a bounded queue. `MaxBufferedRecordsPerPartition` limits
how many records Dekaf buffers for one partition after fetching and before your
processor handles them.

Memory for partition lanes is bounded by:

```text
assigned partition count * MaxBufferedRecordsPerPartition * average record size
```

Backpressure modes:

| Mode | Behavior | Use when |
| --- | --- | --- |
| `PauseResume` | Dekaf pauses a partition when its lane is full and resumes it when capacity returns. | Production default. It isolates slow partitions and keeps the shared consume loop fair. |
| `AwaitCapacity` | Dekaf waits for queue capacity without changing the consumer pause state. | Tests or simple deployments where pause/resume side effects are undesirable. |

While `RunPartitionedAsync` is active, Dekaf owns pause and resume for its
backpressure. Do not manually pause or resume partitions on the same consumer.

Bounded partition queues do not replace Kafka fetch limits such as
`QueuedMaxMessagesKbytes`; they add an application-processing boundary after
records have been fetched.

## Error Policy

`PartitionWorkerErrorPolicy.StopConsumer` is the default. A processor exception
stops the whole partitioned run and propagates from `RunPartitionedAsync`.

`StopPartition` stops only the failed partition and pauses it while it remains
assigned. Use this only when operations can tolerate lag on that partition until
the next revoke or reassignment.

`Ignore` logs the exception, waits with exponential backoff, and restarts the
failed lane while healthy partitions keep running. Prefer handling retries and
dead-letter routing inside your processor so unexpected exceptions remain
visible.

## Low-Level Rebalance Callbacks

`RunPartitionedAsync` builds on the lower-level rebalance lifecycle described in
[consumer groups](./consumer-groups.md#rebalance-listener). For most partitioned
work, use `RunPartitionedAsync` instead of writing your own channel-per-partition
dispatcher.

Use `IRebalanceListener` directly when you need full control over assignment
state, custom queues, or integration with an existing processing runtime. The
same safety rule applies: commit completed offsets on revoke or graceful stop,
but do not commit offsets for lost partitions unless your application has a
separate ownership guarantee.

## Migrating From Other APIs

### Confluent.Kafka

Map Confluent rebalance handlers to Dekaf callbacks:

| Confluent.Kafka | Dekaf |
| --- | --- |
| `SetPartitionsAssignedHandler` | `IRebalanceListener.OnPartitionsAssignedAsync` |
| `SetPartitionsRevokedHandler` | `IRebalanceListener.OnPartitionsRevokedAsync` |
| `SetPartitionsLostHandler` | `IRebalanceListener.OnPartitionsLostAsync` |

If your Confluent consumer starts one task or channel per assigned partition,
replace that dispatcher with `RunPartitionedAsync`. Move the per-partition work
into the processor callback, call `MarkProcessed` after durable processing, and
let `CommitPolicy` handle revoke and shutdown commits.

### Akka.Streams.Kafka

`RunPartitionedAsync` maps most closely to partitioned sources where each
assigned partition becomes an ordered substream. The processor callback is the
substream body, `partition.Messages` is the ordered stream, and `MarkProcessed`
is the point where a committable offset becomes eligible for commit.

If your Akka.Streams.Kafka flow commits offsets in a partition handler during
revoke, use `CommitCompletedOnRevoke` or call `CommitProcessedAsync` inside the
Dekaf processor. Keep per-partition ordering assumptions inside one processor
invocation, not in shared mutable state.
