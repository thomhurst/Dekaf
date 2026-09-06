---
sidebar_position: 9
description: "Query complete consumer-group checkpoints, batch groups, and wait for transactionally stable offsets."
---

# Consumer group offsets

Query multiple groups with complete committed offsets, leader epochs, and application metadata:

```csharp
using Dekaf;
using Dekaf.Admin;

await using var admin = Kafka.CreateAdminClient()
    .WithBootstrapServers("localhost:9092")
    .Build();

var results = await admin.ListConsumerGroupOffsetsAsync(
    new Dictionary<string, ListConsumerGroupOffsetsSpec>
    {
        ["orders"] = new() { TopicPartitions = [new("orders", 0)] },
        ["payments"] = new() // All committed partitions
    },
    new ListConsumerGroupOffsetsOptions
    {
        RequireStable = true,
        TimeoutMs = 30_000
    },
    cancellationToken);
```

`TopicPartitions = null` fetches all committed partitions. An empty selection fetches none. Duplicate or invalid partitions are rejected before the query runs. Groups sharing a coordinator are batched when that coordinator supports multi-group OffsetFetch; older destinations receive individual requests. Each destination's capabilities determine the request version.

## Results and checkpoint reuse

Check `results[groupId].ErrorCode` for a group-level failure, then each partition result's `ErrorCode`. Successful partitions remain available alongside failed partitions or groups. A partition with `ErrorCode.None` and `Offset == null` has no committed offset; a failed lookup also has a null offset but carries its error code. A selected partition omitted from a malformed broker response is an error, not an absent commit.

A non-null `Offset` is a `TopicPartitionOffset` containing `Offset`, `LeaderEpoch`, and `Metadata`. It can be passed directly to `AlterConsumerGroupOffsetsAsync` or a transaction's `SendOffsetsToTransactionAsync`, preserving the checkpoint's epoch and application metadata.

The original `ListConsumerGroupOffsetsAsync(string groupId, CancellationToken)` remains available and returns only committed `long` offsets. It omits absent commits and throws for lookup errors. Existing `IAdminClient` implementations remain compatible: the rich overload is an extension backed by the optional `IConsumerGroupOffsetQueryAdminClient` capability. Implementations without that capability throw `NotSupportedException` for the rich overload.

## Transactional stability

`RequireStable = false` returns the last committed checkpoint even if a transaction has newer offset commits pending. With `RequireStable = true`, Dekaf retries `UnstableOffsetCommit` until the transaction commits or aborts, or the operation's total timeout/cancellation budget expires. A commit exposes the new checkpoint; an abort leaves the previous checkpoint visible. Stability requires OffsetFetch v7 or newer; an unsupported destination fails with `BrokerVersionException`. Kafka defines this behavior in [OffsetFetch](https://github.com/apache/kafka/blob/trunk/clients/src/main/resources/common/message/OffsetFetchRequest.json) and exposes the same flag in its [admin options](https://kafka.apache.org/43/javadoc/org/apache/kafka/clients/admin/ListConsumerGroupOffsetsOptions.html).

`TimeoutMs` covers initialization, coordinator discovery, network operations, and all stability retries together. Expiry throws `KafkaTimeoutException`; caller cancellation throws `OperationCanceledException`. Completed groups are retained while pending groups are retried. Normal transient coordinator or partition failures still use bounded request retries and retain their final error codes in the result.
