---
sidebar_position: 8
description: "Queue-style consumption with share groups (KIP-932): record-level acknowledgement, Accept/Release/Reject, implicit vs explicit modes, and acquisition lock renewal."
---

# Share Consumers (KIP-932)

Share consumers implement [KIP-932 "Queues for Kafka"](https://cwiki.apache.org/confluence/display/KAFKA/KIP-932%3A+Queues+for+Kafka). Instead of assigning each partition to exactly one group member, a **share group** lets every member consume from any partition, with the broker handing out records under short-lived acquisition locks. Each record is acknowledged individually — accepted, released for redelivery, or rejected — giving you traditional message-queue semantics on top of Kafka topics.

## Consumer or Share Consumer?

The two models differ in who owns partitions and how progress is tracked:

| | Consumer group | Share group |
|---|---|---|
| Partition ownership | Each partition assigned to exactly one member | None — any member fetches from any partition |
| Max parallelism | Partition count (extra consumers idle) | Unlimited — scale consumers past partition count |
| Ordering | Guaranteed within a partition | Not guaranteed; records from one partition process concurrently |
| Progress tracking | Committed offset per partition | Per-record acknowledgement (Accept / Release / Reject) |
| Failure handling | Coarse: reprocess from committed offset; one poison message blocks the partition behind it | Per-record: release or reject one record, the rest keep flowing |
| Position control | Seek, pause, offset reset, replay history | None — the broker manages the delivery window |
| Delivery counting | Not tracked | `DeliveryCount` per record, enabling max-attempts logic |
| Broker requirement | Kafka 4.0+ | Kafka 4.2+ with `group.share.enable=true` |

**Pick a [regular consumer group](./consumer-groups)** when you need per-partition ordering, offset-based replay, or stream-processing semantics — event sourcing, changelog consumption, windowed aggregation.

**Pick a share consumer** when you want work-queue semantics — more workers than partitions, per-message retry without blocking neighbors, or you are replacing a queue system (RabbitMQ, SQS, Azure Service Bus) with Kafka.

If you are unsure, start with a regular consumer group: it is the standard Kafka model, has no broker feature flag, and supports the full offset toolbox. Reach for share groups when partition-count ceilings or head-of-line blocking become the actual problem.

## Requirements

Share groups require **Kafka 4.2+** with share groups enabled on the broker:

```properties
group.share.enable=true
```

## Creating a Share Consumer

Use the fluent builder:

```csharp
using Dekaf;

await using var consumer = await Kafka.CreateShareConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-workers")   // Share group ID (required)
    .SubscribeTo("orders")
    .BuildAsync();
```

Or from a root `KafkaClient` when multiple clients share connections:

```csharp
await using var kafka = Kafka.Connect("localhost:9092");

await using var consumer = await kafka.CreateShareConsumer<string, string>("order-workers")
    .SubscribeTo("orders")
    .BuildAsync();
```

Share groups do not support manual partition assignment — `Subscribe` is the only way to receive records. The share group coordinator decides which partitions each member fetches from; the current set is exposed via `consumer.Assignment`.

## Consuming and Acknowledging

`PollAsync` returns an `IAsyncEnumerable` of acquired records:

```csharp
await foreach (var record in consumer.PollAsync(cancellationToken))
{
    try
    {
        await ProcessAsync(record.Value);
        consumer.Acknowledge(record, AcknowledgeType.Accept);
    }
    catch (TransientException)
    {
        // Redeliver to any group member (this one or another)
        consumer.Acknowledge(record, AcknowledgeType.Release);
    }
    catch (PoisonMessageException)
    {
        // Permanently reject - never redelivered
        consumer.Acknowledge(record, AcknowledgeType.Reject);
    }
}
```

The three acknowledgement types:

| Type | Effect |
|------|--------|
| `Accept` | Record processed successfully; removed from the share partition |
| `Release` | Record returned to the group for redelivery (increments its delivery count) |
| `Reject` | Record is unprocessable; permanently discarded, never redelivered |

`ShareConsumeResult<TKey, TValue>` carries the usual `Topic`, `Partition`, `Offset`, `Key`, `Value`, `Headers`, and `Timestamp`, plus `DeliveryCount` — how many times the broker has delivered this record (first delivery = 1). Use it to dead-letter records that keep failing:

```csharp
if (record.DeliveryCount >= 5)
{
    await deadLetterProducer.ProduceAsync("orders-dlq", record.Key, record.Value);
    consumer.Acknowledge(record, AcknowledgeType.Reject);
    return;
}
```

## Acknowledgement Modes

The mode controls what happens to records you do *not* explicitly acknowledge. It maps to Kafka's `share.acknowledgement.mode`:

```csharp
await using var consumer = await Kafka.CreateShareConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-workers")
    .WithAcknowledgementMode(ShareAcknowledgementMode.Explicit)
    .BuildAsync(cancellationToken);
```

**Implicit (default):** records from the previous poll that were not passed to `Acknowledge` are automatically accepted when the next `PollAsync` iteration or `CommitAsync` sends acknowledgements. Call `Acknowledge(record, Release)` or `Reject` *before* the next poll if a record must not be auto-accepted.

**Explicit:** only records passed to `Acknowledge` are acknowledged. Unacknowledged records stay locked until their acquisition lock expires, then return to the group for redelivery.

Acknowledgements are batched and piggy-backed onto the next ShareFetch. To flush them immediately without fetching more records, call:

```csharp
await consumer.CommitAsync(cancellationToken);
```

## Observing Acknowledgement Outcomes

Register an acknowledgement commit callback when application bookkeeping must observe the broker's final result:

```csharp
await using var consumer = await Kafka.CreateShareConsumer<string, string>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-workers")
    .WithAcknowledgementCommitCallback(results =>
    {
        foreach (var result in results)
        {
            if (result.Exception is null)
            {
                Console.WriteLine($"Acknowledged {result.TopicPartition}: " +
                    $"{result.Offsets.Length} record(s)");
            }
            else
            {
                Console.Error.WriteLine(
                    $"Acknowledgement failed for {result.TopicPartition}: {result.Exception.Message}");
            }
        }
    })
    .BuildAsync();
```

One `ShareAcknowledgementCommitResult` is reported per topic-partition. `Offsets` are ascending, `Succeeded` is true when `Exception` is null, and results are ordered by topic (ordinal) then partition.

The result span is valid only while the callback runs. Copy individual result values when they must be retained. Each result's `Offsets` is an allocation-free view that supports indexed access, `foreach`, and `CopyTo`.

The callback covers both acknowledgement transports:

- inline acknowledgements piggy-backed by `PollAsync`;
- standalone acknowledgements sent by `CommitAsync` or the close/dispose flush.

Dekaf invokes it once after broker retries finish and after successful acknowledgements are applied and failed acknowledgements are requeued. If cancellation ends a commit, failed partitions are requeued and reported before `OperationCanceledException` reaches the caller. A callback exception is logged and ignored; it never replaces the broker outcome or changes retry state.

The callback runs synchronously on the thread continuing the poll, commit, or close operation. Keep it short and non-blocking. Re-entering the same consumer from the callback is unsupported; record work for later processing instead.

## Acquisition Locks and Renewal

Records are delivered under a broker-side acquisition lock (default 30 seconds, broker config `group.share.record.lock.duration.ms`). If the lock expires before the record is acknowledged, the broker redelivers it to another member. The active timeout is exposed via `consumer.AcquisitionLockTimeoutMs`.

For work that outlives the lock, renew it:

```csharp
consumer.Acknowledge(record, AcknowledgeType.Renew);
await consumer.CommitAsync(cancellationToken); // Sends the renewal
// ...continue long-running processing, then Accept/Release/Reject as normal
```

Renewal requires explicit acknowledgement mode and brokers supporting ShareFetch/ShareAcknowledge v2; older brokers throw `BrokerVersionException`.

## Configuration

Common builder options beyond the connection/TLS/SASL settings shared with other clients:

| Option | Default | Description |
|--------|---------|-------------|
| `WithGroupId` | — (required) | Share group ID |
| `WithAcknowledgementMode` | `Implicit` | Implicit vs explicit acknowledgement (`share.acknowledgement.mode`) |
| `WithAcknowledgementCommitCallback` | — | Reports ordered per-partition broker outcomes after retries and internal bookkeeping |
| `WithShareAcquireMode` | `BatchOptimized` | `BatchOptimized` acquires along producer batch boundaries; `RecordLimit` strictly caps at `MaxPollRecords` (`share.acquire.mode`) |
| `WithMaxPollRecords` | 500 | Maximum records per poll |
| `WithFetchMinBytes` / `WithFetchMaxBytes` | 1 / 50 MiB | Broker fetch accumulation bounds |
| `WithMaxPartitionFetchBytes` | 1 MiB | Per-partition fetch cap |
| `WithFetchMaxWaitMs` | 200 | Max broker wait for `FetchMinBytes` |
| `WithSessionTimeoutMs` | 45000 | Coordinator removes the member without a heartbeat within this window |
| `WithHeartbeatIntervalMs` | 3000 | Initial heartbeat interval (broker may adjust) |

## Thread Safety

`IKafkaShareConsumer<TKey, TValue>` is **not thread-safe**. Call `Subscribe`, `PollAsync`, `Acknowledge`, `CommitAsync`, and `Unsubscribe` from a single thread or with external synchronization. Run multiple consumer instances for parallelism — that is the point of share groups.

## Shutdown

`Unsubscribe`, `CloseAsync`, and `DisposeAsync` flush pending acknowledgements, leave the share group, and release any still-locked records back to the group (best effort) so other members can claim them without waiting for lock expiry:

```csharp
await consumer.CloseAsync();
// or rely on await using for disposal
```

## Administration

`IAdminClient` covers share group operations: `ListShareGroupsAsync`, `DescribeShareGroupsAsync`, `DeleteShareGroupsAsync`, `DescribeShareGroupOffsetsAsync`, `AlterShareGroupOffsetsAsync`, and `DeleteShareGroupOffsetsAsync`.

Group deletion returns one result per requested ID, so a batch preserves partial failures instead of throwing away successful results:

```csharp
var results = await admin.DeleteShareGroupsAsync(["jobs-a", "jobs-b"]);
foreach (var (groupId, result) in results)
{
    Console.WriteLine($"{groupId}: {result.ErrorCode}");
}
```

The operation uses the group coordinator and Kafka's `DeleteGroups` API, matching Kafka 4.3's `deleteShareGroups` implementation. Active groups normally return `NonEmptyGroup`; close their consumers before deletion.

Per-group results cover terminal error codes only. If a request keeps failing with a retriable error, the call throws after retries are exhausted and returns no results. Duplicate group IDs raise `ArgumentException` before any request is sent. Dekaf's built-in and in-memory admin clients expose deletion through `IShareGroupDeletionAdminClient`; the `IAdminClient` extension preserves the same call syntax for binary compatibility.

## Testing

`Dekaf.Testing` provides `InMemoryShareConsumer<TKey, TValue>` for broker-free unit tests, and `AddDekafInMemory()` swaps DI registrations for in-memory doubles. See [Testing](../testing).
