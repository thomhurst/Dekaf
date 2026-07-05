---
sidebar_position: 6
---

# Partitioned Async Processing API Proposal

:::info
This is the proposed API contract for first-class partitioned async processing. It is not implemented yet. The runtime work is tracked by #1268.
:::

Dekaf already exposes the low-level pieces needed for partition-aware consumers: `ConsumeAsync`, manual commits, `consumer.Partitions.Pause` / `consumer.Partitions.Resume`, and `IRebalanceListener`. The proposed partitioned processing API should make the common advanced model first-class:

- one ordered async lane per assigned `TopicPartition`
- parallel processing across partitions
- bounded per-partition buffers
- deterministic assignment, revoke, lost, and graceful stop behavior
- offset commits based on completed processing, not fetched records

## Proposed Shape

Add an extension method on `IKafkaConsumer<TKey, TValue>`:

```csharp
await consumer.RunPartitionedAsync(
    ProcessPartitionAsync,
    new PartitionedProcessingOptions
    {
        MaxBufferedRecordsPerPartition = 256,
        BackpressureMode = PartitionBackpressureMode.PauseResume,
        StopPolicy = PartitionStopPolicy.Drain,
        ErrorPolicy = PartitionWorkerErrorPolicy.StopConsumer,
        CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke
    },
    cancellationToken);

static async ValueTask ProcessPartitionAsync(
    PartitionProcessorContext<string, Order> partition,
    CancellationToken cancellationToken)
{
    await foreach (var message in partition.Messages.WithCancellation(cancellationToken))
    {
        await SaveOrderAsync(message.Value, cancellationToken);
        partition.MarkProcessed(message);
    }
}
```

The method owns the consume loop while it runs. Applications should not call `ConsumeAsync`, `ConsumeBatchAsync`, `ConsumeRawBatchAsync`, `consumer.Partitions.Assign`, `consumer.Partitions.Unassign`, `consumer.Partitions.Pause`, or `consumer.Partitions.Resume` concurrently with `RunPartitionedAsync` on the same consumer.

## Proposed Public Types

```csharp
public static class PartitionedConsumerExtensions
{
    public static ValueTask RunPartitionedAsync<TKey, TValue>(
        this IKafkaConsumer<TKey, TValue> consumer,
        PartitionProcessor<TKey, TValue> processor,
        PartitionedProcessingOptions? options = null,
        CancellationToken cancellationToken = default);
}

public delegate ValueTask PartitionProcessor<TKey, TValue>(
    PartitionProcessorContext<TKey, TValue> context,
    CancellationToken cancellationToken);

public sealed class PartitionProcessorContext<TKey, TValue>
{
    public TopicPartition TopicPartition { get; }
    public IAsyncEnumerable<ConsumeResult<TKey, TValue>> Messages { get; }
    public CancellationToken StoppingToken { get; }

    public void MarkProcessed(ConsumeResult<TKey, TValue> message);
    public ValueTask CommitProcessedAsync(CancellationToken cancellationToken = default);
    public long? LastProcessedOffset { get; }
}

public sealed class PartitionedProcessingOptions
{
    public int MaxBufferedRecordsPerPartition { get; init; } = 256;
    public PartitionBackpressureMode BackpressureMode { get; init; } = PartitionBackpressureMode.PauseResume;
    public PartitionStopPolicy StopPolicy { get; init; } = PartitionStopPolicy.Drain;
    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public PartitionWorkerErrorPolicy ErrorPolicy { get; init; } = PartitionWorkerErrorPolicy.StopConsumer;
    public PartitionCommitPolicy CommitPolicy { get; init; } = PartitionCommitPolicy.CommitCompletedOnRevoke;
    public TimeSpan CommitInterval { get; init; } = TimeSpan.FromSeconds(5);
}

public enum PartitionBackpressureMode
{
    PauseResume,
    AwaitCapacity
}

public enum PartitionStopPolicy
{
    Drain,
    Cancel
}

public enum PartitionWorkerErrorPolicy
{
    StopConsumer,
    StopPartition,
    Ignore
}

public enum PartitionCommitPolicy
{
    UserManaged,
    CommitCompletedOnRevoke,
    CommitCompletedPeriodically
}
```

The context is intentionally partition-scoped. It exposes only the messages for one partition and commit helpers for offsets completed by that lane. It does not expose the global consumer because direct consumer mutation would bypass the runtime's ownership checks.

## Basic Example

```csharp
await using var consumer = await Kafka.CreateConsumer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-workers")
    .WithOffsetCommitMode(OffsetCommitMode.Manual)
    .SubscribeTo("orders")
    .BuildAsync();

await consumer.RunPartitionedAsync(
    async (partition, ct) =>
    {
        await foreach (var message in partition.Messages.WithCancellation(ct))
        {
            await ProcessOrderAsync(message.Value, ct);
            partition.MarkProcessed(message);
        }
    },
    cancellationToken: stoppingToken);
```

This preserves order for each partition while different partitions run concurrently.

## Advanced Example

```csharp
var options = new PartitionedProcessingOptions
{
    MaxBufferedRecordsPerPartition = 1024,
    BackpressureMode = PartitionBackpressureMode.PauseResume,
    StopPolicy = PartitionStopPolicy.Drain,
    StopTimeout = TimeSpan.FromSeconds(20),
    ErrorPolicy = PartitionWorkerErrorPolicy.StopConsumer,
    CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke
};

await consumer.RunPartitionedAsync(
    async (partition, ct) =>
    {
        await foreach (var message in partition.Messages.WithCancellation(ct))
        {
            using var activity = Telemetry.StartPartitionWork(partition.TopicPartition, message.Offset);

            await HandleAsync(message.Value, ct);
            partition.MarkProcessed(message);

            if (ShouldFlush(partition.LastProcessedOffset))
            {
                await partition.CommitProcessedAsync(ct);
            }
        }
    },
    options,
    stoppingToken);
```

## Threading And Ordering Guarantees

For each assigned partition, the runtime starts at most one active processor invocation. Messages for that partition are delivered to that processor in Kafka offset order.

Different partitions may run concurrently. User code must protect shared application state the same way it would when running multiple tasks.

The runtime may use `ConsumeAsync` or batch fetch APIs internally, but it must route each record to exactly one partition lane. It must not process a later offset for a partition until every earlier yielded offset for that partition has been delivered to the same lane.

The processor callback is long-lived. It starts when a partition is assigned and ends when that partition is revoked, lost, stopped, or when the whole consumer fails or is cancelled.

## Backpressure And Memory Behavior

Each partition lane has a bounded queue. `MaxBufferedRecordsPerPartition` is the maximum number of records the runtime may buffer for a single partition before applying backpressure.

Default backpressure mode should be `PauseResume`:

- when a partition queue is full, the runtime pauses that partition
- when queue capacity returns, the runtime resumes that partition
- pause and resume are owned by the partitioned runtime while it is active

`AwaitCapacity` is useful for simpler runtimes or tests. It waits for lane capacity before routing more records. This is easier to reason about but can reduce fairness when one slow partition blocks dispatch.

Memory is bounded by:

```text
assigned partition count * MaxBufferedRecordsPerPartition * average record size
```

The runtime should also respect existing consumer prefetch limits such as `QueuedMaxMessagesKbytes`. Bounded lane queues do not replace the consumer's fetch buffer limit; they add an application-processing boundary after fetch.

## Assignment Lifecycle

When partitions are assigned:

1. Create partition state before routing records.
2. Start exactly one processor lane per partition.
3. Begin routing records only after lane startup succeeds.

If startup fails, apply `ErrorPolicy`. The default should fail the whole partitioned run because a partition without a processor cannot make progress safely.

## Revoke Lifecycle

When partitions are revoked during cooperative rebalance:

1. Stop routing new records to revoked partitions.
2. Remove queued or prefetched records for revoked partitions before yielding more records. This depends on #1265.
3. Apply `StopPolicy`.
4. If draining, wait until completed records are marked processed or until `StopTimeout`.
5. Commit only offsets that have completed processing when `CommitPolicy` allows runtime-managed commits.
6. Dispose partition state and complete that partition's message stream.

The runtime must never commit offsets for records that were fetched but not marked processed.

## Lost Lifecycle

When partitions are lost involuntarily, the runtime cancels those partition lanes and completes their streams. It must not commit offsets for lost partitions by default because ownership is no longer guaranteed.

Applications that write idempotently may still persist their own external offsets, but the runtime-managed Kafka commit policy should remain conservative.

## Graceful Stop Lifecycle

On `RunPartitionedAsync` cancellation or consumer close:

1. Stop consuming new records.
2. Stop routing records into partition queues.
3. Apply `StopPolicy` to all active lanes.
4. Commit completed offsets when `CommitPolicy` permits it.
5. Dispose partition state.

If #1266 lands as a public graceful partition stop callback, the partitioned runtime can compose with that hook. If not, the runtime should own this lifecycle internally and document that it is the only supported graceful stop path for partitioned processors.

## Error Policy

Default behavior should be fail-fast:

- processor exception stops the partitioned run
- all active lanes are cancelled or drained according to stop policy
- the exception is propagated from `RunPartitionedAsync`

`StopPartition` can be added for advanced users, but it needs explicit semantics for ownership, commits, and observability because a stopped partition that remains assigned can cause unbounded lag.

`Ignore` should be opt-in only. It is appropriate when user code handles failures internally, such as retry plus dead-letter routing.

## Commit Semantics

The runtime tracks completed offsets per partition. Calling `MarkProcessed(message)` marks `message.Offset + 1` as eligible for commit for that partition.

Runtime-managed Kafka commits require manual offset mode:

```csharp
.WithOffsetCommitMode(OffsetCommitMode.Manual)
```

If the consumer uses auto commit mode, `PartitionCommitPolicy.UserManaged` should be used or the runtime should reject commit policies that imply ownership of commit timing. Auto commit can commit fetched or yielded positions before partition processing completes, which conflicts with at-least-once partitioned processing.

`CommitProcessedAsync` commits only the calling partition's completed offset. Revoke and graceful stop commits may batch completed offsets for multiple partitions into one `CommitAsync` call.

Transactions remain user-managed. When a processor writes to Kafka transactionally, it should use `SendOffsetsToTransactionAsync` with offsets derived from `MarkProcessed` state, and the runtime should not also commit those offsets outside the transaction.

## Compatibility

`ConsumeAsync`, `ConsumeBatchAsync`, and `ConsumeRawBatchAsync` remain the low-level APIs. `RunPartitionedAsync` is a higher-level owner of the consume loop and should not be mixed with them on the same consumer while active.

`KafkaConsumerService<TKey, TValue>` can compose in two ways:

- add a new `PartitionedKafkaConsumerService<TKey, TValue>` base class
- add an opt-in service option that runs `RunPartitionedAsync` instead of `ConsumeAsync`

The separate base class is the clearer first version because it keeps existing hosted service behavior unchanged.

## Prerequisites

- #1265: coordinator-driven revocation must discard queued and prefetched records for removed partitions before any consume API yields them.
- #1266: graceful partition stop semantics should be available, unless `RunPartitionedAsync` owns the full stop lifecycle internally.
- #1268: implement the runtime after this proposal is accepted.
- #1269: replace this proposal page with user-facing docs and samples after the runtime lands.
