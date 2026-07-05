---
sidebar_position: 6
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

## Ordering And Parallelism

The processor callback is long-lived. It starts when a partition is assigned and
ends when the partition is revoked, lost, stopped, failed, or when the whole
consumer shuts down.

Within one partition:

- messages are yielded in Kafka offset order
- at most one processor invocation is active
- `MarkProcessed` records the highest completed offset for that partition

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

`CommitProcessedAsync` commits only the current partition's completed offset.
Runtime-managed revoke and shutdown commits may batch completed offsets for
multiple partitions into one `CommitAsync` call.

If the consumer uses auto commit mode, use `PartitionCommitPolicy.UserManaged`
and understand that auto commit can commit consumed positions before partition
processing finishes. For at-least-once partitioned processing, prefer
`OffsetCommitMode.Manual`.

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
